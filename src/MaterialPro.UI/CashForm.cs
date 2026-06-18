using MaterialPro.Application;
using MaterialPro.Domain;

namespace MaterialPro.UI;

public sealed class CashForm : Form
{
    private readonly ICashService _cash;
    private readonly ICashReportService _reports;
    private readonly AppUser _user;
    private readonly Label _summary;
    private readonly DataGridView _historyGrid;
    private readonly DataGridView _movementsGrid;
    private CashSession? _selected;

    public CashForm(ICashService cash, ICashReportService reports, AppUser user)
    {
        _cash = cash;
        _reports = reports;
        _user = user;

        Text = "Sistema > Caixa";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1120, 720);
        BackColor = Color.FromArgb(246, 248, 252);
        Font = new Font("Segoe UI", 10F);

        _summary = new Label { Dock = DockStyle.Top, Height = 82, Font = new Font("Segoe UI", 12F, FontStyle.Bold), Padding = new Padding(12), BackColor = Color.White };
        _historyGrid = Grid();
        _movementsGrid = Grid();

        _historyGrid.Columns.Add(Column(nameof(CashSession.Code), "Caixa", 120));
        _historyGrid.Columns.Add(Column(nameof(CashSession.OpenedAtUtc), "Abertura", 150));
        _historyGrid.Columns.Add(Column(nameof(CashSession.ClosedAtUtc), "Fechamento", 150));
        _historyGrid.Columns.Add(Column(nameof(CashSession.Status), "Status", 90));
        _historyGrid.Columns.Add(Column(nameof(CashSession.TotalSalesAmount), "Vendas", 100));
        _historyGrid.Columns.Add(Column(nameof(CashSession.ReportedAmount), "Informado", 100));
        _historyGrid.Columns.Add(Column(nameof(CashSession.DifferenceAmount), "Diferença", 100));
        _historyGrid.SelectionChanged += (_, _) => LoadSelected();

        _movementsGrid.Columns.Add(Column(nameof(CashMovement.MovementAtUtc), "Data", 150));
        _movementsGrid.Columns.Add(Column(nameof(CashMovement.Type), "Tipo", 120));
        _movementsGrid.Columns.Add(Column(nameof(CashMovement.PaymentMethod), "Forma", 120));
        _movementsGrid.Columns.Add(Column(nameof(CashMovement.Amount), "Valor", 100));
        _movementsGrid.Columns.Add(Column(nameof(CashMovement.Description), "Descrição", 260));

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        actions.Controls.AddRange([
            Button("Abrir caixa", Color.FromArgb(45, 126, 86), (_, _) => Open()),
            Button("Suprimento", Color.FromArgb(40, 92, 150), (_, _) => Supply()),
            Button("Sangria", Color.FromArgb(170, 70, 50), (_, _) => Withdraw()),
            Button("Fechar caixa", Color.FromArgb(218, 124, 38), (_, _) => CloseCash()),
            Button("Imprimir resumo", Color.FromArgb(74, 93, 115), (_, _) => PrintClosing()),
            Button("PDF", Color.FromArgb(40, 92, 150), (_, _) => Export("PDF (*.pdf)|*.pdf", "caixa.pdf", p => File.WriteAllBytes(p, _reports.ExportPdf(new CashReportRequest())))),
            Button("Excel", Color.FromArgb(40, 110, 80), (_, _) => Export("Excel (*.xlsx)|*.xlsx", "caixa.xlsx", p => File.WriteAllBytes(p, _reports.ExportExcel(new CashReportRequest()))))
        ]);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 360 };
        split.Panel1.Controls.Add(_historyGrid);
        split.Panel2.Controls.Add(_movementsGrid);

        Controls.Add(split);
        Controls.Add(actions);
        Controls.Add(_summary);
        LoadData();
    }

    private void Open()
    {
        var value = PromptDecimal("Valor inicial:", "Abrir caixa");
        try
        {
            _cash.Open(new CashOpenRequest(value, _user.Id));
            LoadData();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Caixa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Supply()
    {
        var cash = _cash.ActiveSession();
        if (cash is null) return;
        var value = PromptDecimal("Valor do suprimento:", "Suprimento");
        var reason = Prompt("Motivo:", "Suprimento");
        try
        {
            _cash.Supply(new CashSupplyRequest(cash.Id, value, reason, _user.Id));
            LoadData();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Suprimento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Withdraw()
    {
        var cash = _cash.ActiveSession();
        if (cash is null) return;
        var value = PromptDecimal("Valor da sangria:", "Sangria");
        var reason = Prompt("Motivo:", "Sangria");
        var password = Prompt("Senha gerente:", "Sangria");
        try
        {
            _cash.Withdraw(new CashWithdrawalRequest(cash.Id, value, reason, _user.Id, password));
            LoadData();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Sangria", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CloseCash()
    {
        var cash = _cash.ActiveSession();
        if (cash is null) return;
        var countedCash = PromptDecimal("Dinheiro contado:", "Fechar caixa");
        var countedPix = PromptDecimal("PIX contado:", "Fechar caixa");
        var countedDebit = PromptDecimal("Cartão débito contado:", "Fechar caixa");
        var countedCredit = PromptDecimal("Cartão crédito contado:", "Fechar caixa");
        var obs = Prompt("Observação:", "Fechar caixa");
        try
        {
            _cash.Close(new CashCloseRequest(cash.Id, _user.Id, countedCash, countedPix, countedDebit, countedCredit, obs));
            LoadData();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Fechamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PrintClosing()
    {
        var id = _selected?.Id ?? _cash.ActiveSession()?.Id;
        if (!id.HasValue) return;
        Export("PDF (*.pdf)|*.pdf", "fechamento-caixa.pdf", p => File.WriteAllBytes(p, _cash.PrintClosing(id.Value)));
    }

    private void LoadData()
    {
        var dashboard = _cash.Dashboard();
        _summary.Text = $"Status: {(dashboard.HasOpenCash ? "Aberto" : "Fechado")} {dashboard.OpenCashCode} | Vendas hoje: {dashboard.TodaySales:C} | Dinheiro: {dashboard.TodayCash:C} | PIX: {dashboard.TodayPix:C} | Débito: {dashboard.TodayDebitCard:C} | Crédito: {dashboard.TodayCreditCard:C} | Sangrias: {dashboard.TodayWithdrawals:C} | Suprimentos: {dashboard.TodaySupplies:C} | Diferenças: {dashboard.TodayDifference:C}";
        _historyGrid.DataSource = _cash.History(new CashHistoryRequest()).ToList();
        LoadSelected();
    }

    private void LoadSelected()
    {
        if (_historyGrid.CurrentRow?.DataBoundItem is CashSession cash)
        {
            _selected = cash;
            _movementsGrid.DataSource = _cash.Movements(cash.Id).ToList();
        }
    }

    private void Export(string filter, string fileName, Action<string> export)
    {
        using var dialog = new SaveFileDialog { Filter = filter, FileName = fileName };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            export(dialog.FileName);
        }
    }

    private static string Prompt(string text, string title) => Microsoft.VisualBasic.Interaction.InputBox(text, title, "");

    private static decimal PromptDecimal(string text, string title)
    {
        var value = Prompt(text, title).Replace(",", ".");
        return decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    }

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        ReadOnly = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AllowUserToAddRows = false
    };

    private static DataGridViewTextBoxColumn Column(string property, string header, int width)
        => new() { DataPropertyName = property, HeaderText = header, Width = width };

    private static Button Button(string text, Color color, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 34, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }
}
