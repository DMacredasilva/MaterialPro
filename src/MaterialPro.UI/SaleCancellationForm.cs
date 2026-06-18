using MaterialPro.Application;
using MaterialPro.Domain;

namespace MaterialPro.UI;

public sealed class SaleCancellationForm : Form
{
    private readonly ISalesService _salesService;
    private readonly ISaleCancellationService _cancellationService;
    private readonly AppUser _currentUser;
    private readonly DataGridView _salesGrid;
    private readonly DataGridView _cancelledGrid;
    private readonly TextBox _reasonBox;
    private readonly TextBox _managerPasswordBox;
    private readonly TextBox _observationBox;
    private readonly DateTimePicker _fromPicker;
    private readonly DateTimePicker _toPicker;

    public SaleCancellationForm(ISalesService salesService, ISaleCancellationService cancellationService, AppUser currentUser)
    {
        _salesService = salesService;
        _cancellationService = cancellationService;
        _currentUser = currentUser;

        Text = "Cancelamento de vendas";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1120, 760);
        BackColor = Color.FromArgb(246, 248, 252);
        Font = new Font("Segoe UI", 10F);

        _salesGrid = CreateGrid();
        _cancelledGrid = CreateGrid();
        _reasonBox = new TextBox { Dock = DockStyle.Top };
        _managerPasswordBox = new TextBox { Dock = DockStyle.Top, UseSystemPasswordChar = true };
        _observationBox = new TextBox { Dock = DockStyle.Top, Multiline = true, Height = 70 };
        _fromPicker = new DateTimePicker { Width = 160, Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(-30) };
        _toPicker = new DateTimePicker { Width = 160, Format = DateTimePickerFormat.Short, Value = DateTime.Today };

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildCancelTab());
        tabs.TabPages.Add(BuildCancelledTab());
        Controls.Add(tabs);
        LoadData();
    }

    private TabPage BuildCancelTab()
    {
        var page = new TabPage("Cancelar venda") { BackColor = BackColor };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        fields.Controls.Add(Field("Motivo obrigatório", _reasonBox), 0, 0);
        fields.Controls.Add(Field("Senha de gerente", _managerPasswordBox), 1, 0);
        fields.Controls.Add(Field("Observação", _observationBox), 2, 0);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var cancelButton = MakeButton("Cancelar venda", Color.FromArgb(180, 55, 55));
        cancelButton.Click += (_, _) => CancelSelectedSale();
        var refreshButton = MakeButton("Atualizar", Color.FromArgb(30, 78, 140));
        refreshButton.Click += (_, _) => LoadData();
        toolbar.Controls.Add(cancelButton);
        toolbar.Controls.Add(refreshButton);

        layout.Controls.Add(fields, 0, 0);
        layout.Controls.Add(toolbar, 0, 1);
        layout.Controls.Add(_salesGrid, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildCancelledTab()
    {
        var page = new TabPage("Canceladas e relatório") { BackColor = BackColor };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        toolbar.Controls.Add(new Label { Text = "De", Width = 28, TextAlign = ContentAlignment.MiddleLeft });
        toolbar.Controls.Add(_fromPicker);
        toolbar.Controls.Add(new Label { Text = "Até", Width = 34, TextAlign = ContentAlignment.MiddleLeft });
        toolbar.Controls.Add(_toPicker);

        var filterButton = MakeButton("Consultar", Color.FromArgb(30, 78, 140));
        filterButton.Click += (_, _) => LoadCancelledReport();
        var proofButton = MakeButton("Comprovante", Color.FromArgb(28, 120, 84));
        proofButton.Click += (_, _) => SaveCancellationProof();
        var reportButton = MakeButton("PDF relatório", Color.FromArgb(58, 86, 160));
        reportButton.Click += (_, _) => SaveCancellationReport();
        toolbar.Controls.Add(filterButton);
        toolbar.Controls.Add(proofButton);
        toolbar.Controls.Add(reportButton);

        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(_cancelledGrid, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private static DataGridView CreateGrid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
    }

    private static Panel Field(string label, Control control)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 110, Padding = new Padding(0, 0, 10, 10) };
        panel.Controls.Add(control);
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 24 });
        return panel;
    }

    private static Button MakeButton(string text, Color color)
    {
        return new Button
        {
            Text = text,
            Height = 38,
            Width = 145,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 8, 10, 0)
        };
    }

    private void LoadData()
    {
        _salesGrid.DataSource = _salesService.List()
            .Select(x => new
            {
                x.Id,
                Numero = string.IsNullOrWhiteSpace(x.ReceiptNumber) ? x.Id.ToString() : x.ReceiptNumber,
                Data = x.SoldAtUtc.ToLocalTime(),
                Total = x.TotalAmount,
                Pagamento = x.PaymentMethod,
                Status = x.Status.ToString()
            })
            .ToList();
        _salesGrid.Columns["Id"]!.Visible = false;
        LoadCancelledReport();
    }

    private void LoadCancelledReport()
    {
        var from = _fromPicker.Value.Date.ToUniversalTime();
        var to = _toPicker.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        _cancelledGrid.DataSource = _cancellationService.Report(from, to)
            .Select(x => new
            {
                x.Id,
                Venda = x.ReceiptNumber,
                Data = x.CancelledAtUtc.ToLocalTime(),
                Usuario = x.UserName,
                Total = x.TotalAmount,
                Motivo = x.Reason,
                Observacao = x.Observation
            })
            .ToList();
        _cancelledGrid.Columns["Id"]!.Visible = false;
    }

    private void CancelSelectedSale()
    {
        if (_salesGrid.CurrentRow?.DataBoundItem is null)
        {
            MessageBox.Show(this, "Selecione uma venda.", "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var idProperty = _salesGrid.CurrentRow.DataBoundItem.GetType().GetProperty("Id");
        var saleId = (Guid)(idProperty?.GetValue(_salesGrid.CurrentRow.DataBoundItem) ?? Guid.Empty);
        try
        {
            var cancellation = _cancellationService.CancelSale(new SaleCancellationRequest(
                saleId,
                _reasonBox.Text,
                _currentUser.Id,
                _managerPasswordBox.Text,
                _observationBox.Text));

            SavePdf(_cancellationService.GenerateProofPdf(cancellation.Id), $"materialpro-cancelamento-{cancellation.Id:N}.pdf");
            _reasonBox.Clear();
            _managerPasswordBox.Clear();
            _observationBox.Clear();
            LoadData();
            MessageBox.Show(this, "Venda cancelada. Estoque e financeiro foram estornados/cancelados.", "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SaveCancellationProof()
    {
        var id = SelectedCancellationId();
        if (id is null) return;
        SavePdf(_cancellationService.GenerateProofPdf(id.Value), $"materialpro-comprovante-cancelamento-{id:N}.pdf");
    }

    private void SaveCancellationReport()
    {
        var from = _fromPicker.Value.Date.ToUniversalTime();
        var to = _toPicker.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        SavePdf(_cancellationService.GenerateReportPdf(from, to), $"materialpro-relatorio-cancelamentos-{DateTime.Now:yyyyMMddHHmmss}.pdf");
    }

    private Guid? SelectedCancellationId()
    {
        if (_cancelledGrid.CurrentRow?.DataBoundItem is null)
        {
            MessageBox.Show(this, "Selecione um cancelamento.", "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        var idProperty = _cancelledGrid.CurrentRow.DataBoundItem.GetType().GetProperty("Id");
        return (Guid)(idProperty?.GetValue(_cancelledGrid.CurrentRow.DataBoundItem) ?? Guid.Empty);
    }

    private void SavePdf(byte[] pdf, string fileName)
    {
        var file = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllBytes(file, pdf);
        MessageBox.Show(this, $"PDF salvo em {file}", "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
