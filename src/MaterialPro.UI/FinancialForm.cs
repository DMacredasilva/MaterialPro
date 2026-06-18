using MaterialPro.Application;
using MaterialPro.Domain;

namespace MaterialPro.UI;

public sealed class FinancialForm : Form
{
    private readonly IFinancialService _financial;
    private readonly AppUser _user;
    private readonly DataGridView _payables = Grid();
    private readonly DataGridView _receivables = Grid();
    private readonly DataGridView _duplicates = Grid();
    private readonly DataGridView _settlements = Grid();
    private readonly DataGridView _flow = Grid();
    private readonly Label _summary = new() { Dock = DockStyle.Top, Height = 82, Padding = new Padding(10), ForeColor = Color.FromArgb(31, 41, 55) };
    private readonly TextBox _term = new() { Width = 220, PlaceholderText = "Pesquisar numero, nome ou descricao" };

    public FinancialForm(IFinancialService financial, AppUser user)
    {
        _financial = financial;
        _user = user;
        Text = "MaterialPro - Financeiro";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 720);
        Font = new Font("Segoe UI", 10F);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Tab("Dashboard", BuildDashboard()));
        tabs.TabPages.Add(Tab("Contas a pagar", BuildPayables()));
        tabs.TabPages.Add(Tab("Contas a receber", BuildReceivables()));
        tabs.TabPages.Add(Tab("Duplicatas", BuildDuplicates()));
        tabs.TabPages.Add(Tab("Baixas", BuildSettlements()));
        tabs.TabPages.Add(Tab("Fluxo de caixa", BuildFlow()));
        Controls.Add(tabs);
        RefreshAll();
    }

    private Control BuildDashboard()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var refresh = Button("Atualizar", (_, _) => RefreshAll());
        refresh.Dock = DockStyle.Top;
        panel.Controls.Add(_summary);
        panel.Controls.Add(refresh);
        return panel;
    }

    private Control BuildPayables()
    {
        var panel = BasePanel(_payables);
        panel.Controls.Add(Toolbar(
            Button("Nova pagar", (_, _) => CreatePayable()),
            Button("Baixar", (_, _) => SettlePayable()),
            Button("Cancelar", (_, _) => CancelPayable()),
            Button("Exportar PDF", (_, _) => SaveBytes("financeiro-pagar.pdf", _financial.ExportPdf(Request()))),
            Button("Exportar Excel", (_, _) => SaveBytes("financeiro-pagar.xlsx", _financial.ExportExcel(Request())))));
        return panel;
    }

    private Control BuildReceivables()
    {
        var panel = BasePanel(_receivables);
        panel.Controls.Add(Toolbar(
            Button("Nova receber", (_, _) => CreateReceivable()),
            Button("Baixar", (_, _) => SettleReceivable()),
            Button("Cancelar", (_, _) => CancelReceivable()),
            Button("Exportar PDF", (_, _) => SaveBytes("financeiro-receber.pdf", _financial.ExportPdf(Request()))),
            Button("Exportar Excel", (_, _) => SaveBytes("financeiro-receber.xlsx", _financial.ExportExcel(Request())))));
        return panel;
    }

    private Control BuildDuplicates()
    {
        var panel = BasePanel(_duplicates);
        panel.Controls.Add(Toolbar(
            Button("Baixar duplicata", (_, _) => SettleDuplicate()),
            Button("Cancelar", (_, _) => CancelDuplicate()),
            Button("Imprimir recibo", (_, _) => PrintLastSettlement()),
            Button("Atualizar", (_, _) => RefreshAll())));
        return panel;
    }

    private Control BuildSettlements()
    {
        var panel = BasePanel(_settlements);
        panel.Controls.Add(Toolbar(Button("Imprimir recibo", (_, _) => PrintSelectedSettlement()), Button("Atualizar", (_, _) => RefreshAll())));
        return panel;
    }

    private Control BuildFlow()
    {
        var panel = BasePanel(_flow);
        panel.Controls.Add(Toolbar(Button("7 dias", (_, _) => LoadFlow(7)), Button("30 dias", (_, _) => LoadFlow(30))));
        return panel;
    }

    private Panel BasePanel(DataGridView grid)
    {
        return new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), Controls = { grid } };
    }

    private FlowLayoutPanel Toolbar(params Control[] controls)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, WrapContents = false };
        bar.Controls.Add(_term);
        bar.Controls.Add(Button("Pesquisar", (_, _) => RefreshAll()));
        foreach (var control in controls) bar.Controls.Add(control);
        return bar;
    }

    private void RefreshAll()
    {
        var dashboard = _financial.Dashboard();
        _summary.Text =
            $"Pagar hoje: {dashboard.PayableToday:C} | Pagar vencidas: {dashboard.PayableOverdue:C} | Receber hoje: {dashboard.ReceivableToday:C} | Receber vencidas: {dashboard.ReceivableOverdue:C}\r\n" +
            $"Recebido no mes: {dashboard.ReceivedThisMonth:C} | Pago no mes: {dashboard.PaidThisMonth:C} | Saldo previsto: {dashboard.ForecastBalance:C}\r\n" +
            string.Join(" | ", dashboard.Alerts);
        _payables.DataSource = _financial.SearchPayables(Request()).ToList();
        _receivables.DataSource = _financial.SearchReceivables(Request()).ToList();
        _duplicates.DataSource = _financial.SearchDuplicates(Request()).ToList();
        _settlements.DataSource = _financial.Settlements().ToList();
        LoadFlow(7);
    }

    private void LoadFlow(int days)
    {
        var start = DateTime.UtcNow.Date;
        _flow.DataSource = _financial.CashFlow(start, start.AddDays(days - 1)).ToList();
    }

    private FinancialSearchRequest Request() => new(Term: _term.Text);

    private void CreatePayable()
    {
        using var dialog = new FinancialDocumentDialog("Conta a pagar");
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _financial.CreatePayable(new AccountPayableRequest(dialog.Number, dialog.PersonName, dialog.Description, dialog.Amount, dialog.DueDate, dialog.Method));
        RefreshAll();
    }

    private void CreateReceivable()
    {
        using var dialog = new FinancialDocumentDialog("Conta a receber");
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _financial.CreateReceivable(new AccountReceivableRequest(dialog.Number, null, null, dialog.PersonName, dialog.Description, dialog.Amount, dialog.DueDate, dialog.Method));
        RefreshAll();
    }

    private void SettlePayable()
    {
        if (_payables.CurrentRow?.DataBoundItem is not AccountPayable item) return;
        Settle(item.Id, FinancialType.Payable);
    }

    private void SettleReceivable()
    {
        if (_receivables.CurrentRow?.DataBoundItem is not AccountReceivable item) return;
        Settle(item.Id, FinancialType.Receivable);
    }

    private void SettleDuplicate()
    {
        if (_duplicates.CurrentRow?.DataBoundItem is not Duplicate item) return;
        Settle(item.Id, item.Type, true);
    }

    private void Settle(Guid id, FinancialType type, bool duplicate = false)
    {
        using var dialog = new FinancialSettlementDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var request = new FinancialSettlementRequest(id, type, dialog.Amount, dialog.Interest, dialog.Fine, dialog.Discount, dialog.Method, _user.Id, null, dialog.Observation);
        if (duplicate) _financial.SettleDuplicate(request);
        else if (type == FinancialType.Payable) _financial.SettlePayable(request);
        else _financial.SettleReceivable(request);
        RefreshAll();
    }

    private void CancelPayable()
    {
        if (_payables.CurrentRow?.DataBoundItem is not AccountPayable item) return;
        var reason = Prompt("Motivo do cancelamento");
        if (string.IsNullOrWhiteSpace(reason)) return;
        _financial.CancelPayable(item.Id, reason);
        RefreshAll();
    }

    private void CancelReceivable()
    {
        if (_receivables.CurrentRow?.DataBoundItem is not AccountReceivable item) return;
        var reason = Prompt("Motivo do cancelamento");
        if (string.IsNullOrWhiteSpace(reason)) return;
        _financial.CancelReceivable(item.Id, reason);
        RefreshAll();
    }

    private void CancelDuplicate()
    {
        if (_duplicates.CurrentRow?.DataBoundItem is not Duplicate item) return;
        var reason = Prompt("Motivo do cancelamento");
        if (string.IsNullOrWhiteSpace(reason)) return;
        _financial.CancelDuplicate(item.Id, reason);
        RefreshAll();
    }

    private void PrintSelectedSettlement()
    {
        if (_settlements.CurrentRow?.DataBoundItem is FinancialSettlement item)
        {
            SaveBytes("recibo-financeiro.pdf", _financial.PrintReceipt(item.Id));
        }
    }

    private void PrintLastSettlement()
    {
        var item = _financial.Settlements().FirstOrDefault();
        if (item is not null) SaveBytes("recibo-financeiro.pdf", _financial.PrintReceipt(item.Id));
    }

    private void SaveBytes(string fileName, byte[] bytes)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
        File.WriteAllBytes(path, bytes);
        MessageBox.Show(this, $"Arquivo gerado:\r\n{path}", "Financeiro", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string Prompt(string title)
    {
        using var form = new Form { Text = title, Width = 440, Height = 150, StartPosition = FormStartPosition.CenterParent };
        var box = new TextBox { Dock = DockStyle.Top, Margin = new Padding(10) };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
        form.Controls.Add(box);
        form.Controls.Add(ok);
        form.AcceptButton = ok;
        return form.ShowDialog() == DialogResult.OK ? box.Text : string.Empty;
    }

    private static Button Button(string text, EventHandler click)
    {
        var button = new Button { Text = text, Width = 130, Height = 32, Margin = new Padding(4), FlatStyle = FlatStyle.Flat };
        button.Click += click;
        return button;
    }

    private static TabPage Tab(string title, Control control)
    {
        var page = new TabPage(title);
        page.Controls.Add(control);
        return page;
    }

    private static DataGridView Grid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
    }
}

internal sealed class FinancialDocumentDialog : Form
{
    private readonly TextBox _number = new() { PlaceholderText = "Numero" };
    private readonly TextBox _person = new() { PlaceholderText = "Cliente/Fornecedor" };
    private readonly TextBox _description = new() { PlaceholderText = "Descricao" };
    private readonly NumericUpDown _amount = new() { DecimalPlaces = 2, Maximum = 9999999, Minimum = 0.01m, Value = 1 };
    private readonly DateTimePicker _due = new() { Value = DateTime.Today.AddDays(30) };
    private readonly ComboBox _method = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    public FinancialDocumentDialog(string title)
    {
        Text = title;
        Width = 420;
        Height = 330;
        StartPosition = FormStartPosition.CenterParent;
        _method.Items.AddRange(["DINHEIRO", "PIX", "CARTAO", "BOLETO", "TRANSFERENCIA", "CHEQUE"]);
        _method.SelectedIndex = 0;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, Padding = new Padding(12) };
        foreach (var control in new Control[] { _number, _person, _description, _amount, _due, _method })
        {
            control.Dock = DockStyle.Top;
            layout.Controls.Add(control);
        }
        var ok = new Button { Text = "Salvar", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
        layout.Controls.Add(ok);
        Controls.Add(layout);
        AcceptButton = ok;
    }

    public string Number => string.IsNullOrWhiteSpace(_number.Text) ? $"FIN-{DateTime.Now:yyyyMMddHHmmss}" : _number.Text;
    public string PersonName => _person.Text;
    public string Description => _description.Text;
    public decimal Amount => _amount.Value;
    public DateTime DueDate => _due.Value.Date;
    public string Method => _method.Text;
}

internal sealed class FinancialSettlementDialog : Form
{
    private readonly NumericUpDown _amount = new() { DecimalPlaces = 2, Maximum = 9999999, Minimum = 0.01m, Value = 1 };
    private readonly NumericUpDown _interest = new() { DecimalPlaces = 2, Maximum = 9999999 };
    private readonly NumericUpDown _fine = new() { DecimalPlaces = 2, Maximum = 9999999 };
    private readonly NumericUpDown _discount = new() { DecimalPlaces = 2, Maximum = 9999999 };
    private readonly ComboBox _method = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _obs = new() { PlaceholderText = "Observacao" };

    public FinancialSettlementDialog()
    {
        Text = "Baixa financeira";
        Width = 420;
        Height = 360;
        StartPosition = FormStartPosition.CenterParent;
        _method.Items.AddRange(["DINHEIRO", "PIX", "CARTAO_DEBITO", "CARTAO_CREDITO", "BOLETO", "TRANSFERENCIA", "CHEQUE"]);
        _method.SelectedIndex = 0;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8, Padding = new Padding(12) };
        foreach (var control in new Control[] { Label("Valor da baixa"), _amount, Label("Juros"), _interest, Label("Multa"), _fine, Label("Desconto"), _discount, _method, _obs })
        {
            control.Dock = DockStyle.Top;
            layout.Controls.Add(control);
        }
        var ok = new Button { Text = "Baixar", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
        layout.Controls.Add(ok);
        Controls.Add(layout);
        AcceptButton = ok;
    }

    public decimal Amount => _amount.Value;
    public decimal Interest => _interest.Value;
    public decimal Fine => _fine.Value;
    public decimal Discount => _discount.Value;
    public string Method => _method.Text;
    public string Observation => _obs.Text;

    private static Label Label(string text) => new() { Text = text, Height = 22 };
}
