using MaterialPro.Application;
using MaterialPro.Domain;

namespace MaterialPro.UI;

public sealed class PrintersForm : Form
{
    private static readonly Color Ink = Color.FromArgb(31, 41, 55);
    private static readonly Color Muted = Color.FromArgb(96, 108, 124);
    private static readonly Color Surface = Color.FromArgb(246, 248, 252);
    private static readonly Color Blue = Color.FromArgb(38, 89, 143);
    private static readonly Color Green = Color.FromArgb(42, 126, 86);

    private readonly IPrinterManagementService _printers;
    private readonly AppUser? _user;
    private readonly DataGridView _printerGrid = Grid();
    private readonly DataGridView _queueGrid = Grid();
    private readonly DataGridView _logsGrid = Grid();
    private readonly ComboBox _documentTypeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox _paperKindBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly NumericUpDown _left = MarginBox();
    private readonly NumericUpDown _right = MarginBox();
    private readonly NumericUpDown _top = MarginBox();
    private readonly NumericUpDown _bottom = MarginBox();
    private readonly CheckBox _cut = new() { Text = "Cortar papel", Checked = true, AutoSize = true };
    private readonly CheckBox _drawer = new() { Text = "Abrir gaveta", AutoSize = true };
    private readonly CheckBox _logo = new() { Text = "Imprimir logo", Checked = true, AutoSize = true };
    private readonly Label _summary = new() { Dock = DockStyle.Top, Height = 46, ForeColor = Muted, Padding = new Padding(10, 4, 10, 0) };

    public PrintersForm(IPrinterManagementService printers, AppUser? user)
    {
        _printers = printers;
        _user = user;

        Text = "MaterialPro - Sistema - Impressoras";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 760);
        BackColor = Surface;
        Font = new Font("Segoe UI", 10F);

        _documentTypeBox.Items.AddRange(Enum.GetValues<PrintDocumentType>().Cast<object>().ToArray());
        _paperKindBox.Items.AddRange(Enum.GetValues<PrinterKind>().Where(x => x != PrinterKind.Unknown).Cast<object>().ToArray());
        UiKit.SelectIfAvailable(_documentTypeBox, 0);
        var thermal80Index = _paperKindBox.Items.IndexOf(PrinterKind.Thermal80);
        UiKit.SelectIfAvailable(_paperKindBox, thermal80Index >= 0 ? thermal80Index : 0);

        var header = new Panel { Dock = DockStyle.Top, Height = 86, Padding = new Padding(18, 14, 18, 8), BackColor = Color.White };
        header.Controls.Add(new Label
        {
            Text = "Impressoras",
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Ink
        });
        header.Controls.Add(_summary);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildPrinterTab());
        tabs.TabPages.Add(BuildConfigTab());
        tabs.TabPages.Add(BuildQueueTab());
        tabs.TabPages.Add(BuildLogTab());

        Controls.Add(tabs);
        Controls.Add(header);
        RefreshAll(syncPrinters: true);
    }

    private TabPage BuildPrinterTab()
    {
        var page = Page("Detectadas");
        var buttons = Bar(
            Button("Atualizar lista", Blue, (_, _) => RefreshAll(syncPrinters: true)),
            Button("Definir como padrao", Green, (_, _) => SetDefault()),
            Button("Testar impressao", Color.FromArgb(218, 124, 38), (_, _) => TestPrint()));
        page.Controls.Add(_printerGrid);
        page.Controls.Add(buttons);
        return page;
    }

    private TabPage BuildConfigTab()
    {
        var page = Page("Configuracao");
        var form = new TableLayoutPanel { Dock = DockStyle.Top, Height = 190, Padding = new Padding(16), ColumnCount = 4, RowCount = 3 };
        for (var i = 0; i < 4; i++) form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        form.Controls.Add(Field("Documento", _documentTypeBox), 0, 0);
        form.Controls.Add(Field("Papel", _paperKindBox), 1, 0);
        form.Controls.Add(Field("Margem esquerda", _left), 2, 0);
        form.Controls.Add(Field("Margem direita", _right), 3, 0);
        form.Controls.Add(Field("Margem superior", _top), 0, 1);
        form.Controls.Add(Field("Margem inferior", _bottom), 1, 1);
        form.Controls.Add(_cut, 2, 1);
        form.Controls.Add(_drawer, 3, 1);
        form.Controls.Add(_logo, 0, 2);

        page.Controls.Add(new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true, DataSource = _printers.Configurations().Select(MapConfig).ToList() });
        page.Controls.Add(Bar(Button("Salvar configuracao", Green, (_, _) => SaveConfig())));
        page.Controls.Add(form);
        return page;
    }

    private TabPage BuildQueueTab()
    {
        var page = Page("Fila");
        page.Controls.Add(_queueGrid);
        page.Controls.Add(Bar(
            Button("Atualizar", Blue, (_, _) => RefreshAll()),
            Button("Reimprimir", Green, (_, _) => ReprintSelected()),
            Button("Cancelar item", Color.FromArgb(180, 70, 50), (_, _) => CancelSelected())));
        return page;
    }

    private TabPage BuildLogTab()
    {
        var page = Page("Logs");
        page.Controls.Add(_logsGrid);
        page.Controls.Add(Bar(Button("Atualizar", Blue, (_, _) => RefreshAll())));
        return page;
    }

    private void RefreshAll(bool syncPrinters = false)
    {
        if (syncPrinters)
        {
            _printers.SyncInstalledPrinters();
        }

        var devices = _printers.Printers();
        _printerGrid.DataSource = devices.Select(MapPrinter).ToList();
        _queueGrid.DataSource = _printers.Queue().Select(MapQueue).ToList();
        _logsGrid.DataSource = _printers.Logs().Select(MapLog).ToList();
        _summary.Text = $"{devices.Count} impressora(s) cadastrada(s) | Fila: {_printers.Queue().Count} item(ns) | Logs: {_printers.Logs().Count}";
    }

    private void SetDefault()
    {
        var id = SelectedId(_printerGrid);
        if (id is null) return;
        _printers.SetDefault(id.Value);
        RefreshAll();
    }

    private void TestPrint()
    {
        var id = SelectedId(_printerGrid);
        _printers.Print(new PrintDocumentRequest(
            PrintDocumentType.TestPage,
            null,
            "Teste de impressao",
            ["Teste MaterialPro", $"Computador: {Environment.MachineName}", $"Data: {DateTime.Now:dd/MM/yyyy HH:mm}"],
            _user?.Id,
            id));
        RefreshAll();
        MessageBox.Show(this, "Teste registrado na fila de impressao.", "Impressoras", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveConfig()
    {
        var id = SelectedId(_printerGrid);
        _printers.SaveConfiguration(new PrintConfigurationRequest(
            Environment.MachineName,
            _documentTypeBox.SelectedItem is PrintDocumentType type ? type : PrintDocumentType.SaleReceipt,
            id,
            _paperKindBox.SelectedItem is PrinterKind kind ? kind : PrinterKind.Thermal80,
            _left.Value,
            _right.Value,
            _top.Value,
            _bottom.Value,
            _cut.Checked,
            _drawer.Checked,
            _logo.Checked));
        RefreshAll();
        MessageBox.Show(this, "Configuracao salva.", "Impressoras", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ReprintSelected()
    {
        var id = SelectedId(_queueGrid);
        if (id is null) return;
        _printers.Reprint(id.Value, _user?.Id);
        RefreshAll();
    }

    private void CancelSelected()
    {
        var id = SelectedId(_queueGrid);
        if (id is null) return;
        _printers.CancelQueueItem(id.Value, _user?.Id);
        RefreshAll();
    }

    private static Guid? SelectedId(DataGridView grid)
    {
        if (grid.CurrentRow?.DataBoundItem is null) return null;
        var property = grid.CurrentRow.DataBoundItem.GetType().GetProperty("Id");
        return property?.GetValue(grid.CurrentRow.DataBoundItem) is Guid id ? id : null;
    }

    private static object MapPrinter(PrinterDevice x) => new
    {
        x.Id,
        Nome = x.Name,
        x.Driver,
        Porta = x.Port,
        Tipo = x.Kind,
        Status = x.Status,
        Padrao = x.IsWindowsDefault,
        Ativa = x.IsActive
    };

    private static object MapConfig(PrintConfiguration x) => new
    {
        x.Id,
        Computador = x.ComputerName,
        Documento = x.DocumentType,
        Papel = x.PaperKind,
        Impressora = x.PrinterId,
        Corte = x.CutPaper,
        Gaveta = x.OpenDrawer,
        Logo = x.PrintLogo
    };

    private static object MapQueue(PrintQueueItem x) => new
    {
        x.Id,
        Documento = x.DocumentType,
        Referencia = x.ReferenceId,
        Impressora = x.PrinterId,
        x.Status,
        Tentativas = x.Attempts,
        Erro = x.Error,
        Criado = x.CreatedAtUtc.ToLocalTime(),
        Impresso = x.PrintedAtUtc?.ToLocalTime()
    };

    private static object MapLog(PrintLog x) => new
    {
        x.Id,
        Usuario = x.UserId,
        Documento = x.DocumentType,
        Referencia = x.ReferenceId,
        Impressora = x.PrinterName,
        x.Status,
        Mensagem = x.Message,
        Data = x.LoggedAtUtc.ToLocalTime()
    };

    private static TabPage Page(string text) => new(text) { BackColor = Surface, Padding = new Padding(10) };

    private static FlowLayoutPanel Bar(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(6), WrapContents = false };
        panel.Controls.AddRange(controls);
        return panel;
    }

    private static Panel Field(string label, Control control)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 8) };
        control.Dock = DockStyle.Top;
        panel.Controls.Add(control);
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 23, ForeColor = Ink });
        return panel;
    }

    private static Button Button(string text, Color color, EventHandler click)
    {
        var button = new Button { Text = text, Width = 150, Height = 36, Margin = new Padding(4), BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        button.Click += click;
        return button;
    }

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = true,
        AllowUserToAddRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None
    };

    private static NumericUpDown MarginBox() => new() { Width = 100, Minimum = 0, Maximum = 80, DecimalPlaces = 1, Value = 4 };
}
