using MaterialPro.Application;
using MaterialPro.Domain;

namespace MaterialPro.UI;

public sealed class SuppliersForm : Form
{
    private static readonly Color Navy = Color.FromArgb(24, 57, 92);
    private static readonly Color Green = Color.FromArgb(42, 126, 91);
    private static readonly Color Orange = Color.FromArgb(215, 125, 44);

    private readonly ISupplierService _suppliers;
    private readonly ISupplierImportService _importer;
    private readonly ISupplierReportService _reports;
    private readonly DataGridView _grid;
    private readonly TextBox _searchBox;
    private readonly CheckBox _activeOnlyBox;
    private readonly Dictionary<string, Control> _fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly DataGridView _productsGrid;
    private readonly DataGridView _payablesGrid;
    private readonly DataGridView _purchasesGrid;
    private readonly Label _summaryLabel;
    private Guid? _selectedId;

    public SuppliersForm(ISupplierService suppliers, ISupplierImportService importer, ISupplierReportService reports)
    {
        _suppliers = suppliers;
        _importer = importer;
        _reports = reports;

        Text = "Sistema > Fornecedores";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1220, 780);
        BackColor = Color.FromArgb(246, 248, 252);
        Font = new Font("Segoe UI", 10F);

        _searchBox = new TextBox { Width = 360, PlaceholderText = "Buscar por codigo, fantasia, razao, CNPJ, telefone, WhatsApp, cidade ou UF" };
        _activeOnlyBox = new CheckBox { Text = "Somente ativos", Checked = true, AutoSize = true };
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            BackgroundColor = Color.White
        };
        ConfigureGrid();

        _productsGrid = Grid();
        _payablesGrid = Grid();
        _purchasesGrid = Grid();
        _summaryLabel = new Label { Dock = DockStyle.Top, Height = 34, ForeColor = Navy, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        ConfigureHistoryGrids();

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12), BackColor = Color.White };
        top.Controls.AddRange([
            _searchBox,
            Button("Buscar", Navy, (_, _) => LoadSuppliers()),
            _activeOnlyBox,
            Button("Novo", Color.FromArgb(90, 100, 115), (_, _) => ClearForm()),
            Button("Salvar", Green, (_, _) => SaveSupplier()),
            Button("Inativar", Color.FromArgb(170, 70, 50), (_, _) => InactivateSupplier()),
            Button("Ficha PDF", Navy, (_, _) => ExportFicha()),
            Button("PDF", Navy, (_, _) => Export("PDF (*.pdf)|*.pdf", "fornecedores.pdf", p => File.WriteAllBytes(p, _reports.ExportPdf(ReportRequest())))),
            Button("Excel", Green, (_, _) => Export("Excel (*.xlsx)|*.xlsx", "fornecedores.xlsx", p => File.WriteAllBytes(p, _reports.ExportExcel(ReportRequest())))),
            Button("Importar CSV", Color.FromArgb(80, 90, 110), (_, _) => Import("CSV (*.csv)|*.csv", p => _importer.ImportCsv(p, ImportOptions()))),
            Button("Importar Excel", Color.FromArgb(80, 90, 110), (_, _) => Import("Excel (*.xlsx)|*.xlsx", p => _importer.ImportExcel(p, ImportOptions()))),
            Button("Importar DBF", Color.FromArgb(80, 90, 110), (_, _) => Import("DBF (*.dbf)|*.dbf", p => _importer.ImportDbf(p, ImportOptions()))),
            Button("Conta a pagar", Orange, (_, _) => CreatePayable())
        ]);
        _activeOnlyBox.CheckedChanged += (_, _) => LoadSuppliers();

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 560 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(BuildTabs());

        Controls.Add(split);
        Controls.Add(top);
        LoadSuppliers();
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
        tabs.TabPages.Add(Tab("Dados principais", BuildPanel(("Code", "Codigo"), ("PersonType", "Tipo pessoa"), ("FantasyName", "Nome fantasia"), ("LegalName", "Razao social"), ("Cnpj", "CNPJ"), ("StateRegistration", "Inscricao Estadual"), ("MunicipalRegistration", "Inscricao Municipal"), ("IsActive", "Ativo"))));
        tabs.TabPages.Add(Tab("Contato", BuildPanel(("Phone", "Telefone"), ("MobilePhone", "Celular"), ("WhatsApp", "WhatsApp"), ("Email", "E-mail"), ("Website", "Site"), ("ContactName", "Contato responsavel"), ("ContactRole", "Cargo do contato"))));
        tabs.TabPages.Add(Tab("Endereco", BuildPanel(("ZipCode", "CEP"), ("Address", "Rua"), ("AddressNumber", "Numero"), ("Complement", "Complemento"), ("District", "Bairro"), ("City", "Cidade"), ("State", "Estado"))));
        tabs.TabPages.Add(Tab("Comercial", BuildPanel(("DefaultPaymentTermDays", "Prazo pagamento padrao"), ("PurchaseLimit", "Limite de compra"), ("Notes", "Observacoes"))));
        tabs.TabPages.Add(Tab("Historico", BuildHistoryPanel()));
        return tabs;
    }

    private Control BuildPanel(params (string Key, string Label)[] fields)
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(18), BackColor = Color.White };
        var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        foreach (var field in fields)
        {
            Control input = field.Key switch
            {
                "PersonType" => new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, DataSource = Enum.GetValues<PersonType>() },
                "IsActive" => new CheckBox { Dock = DockStyle.Top, Checked = true, Text = "Fornecedor ativo" },
                "DefaultPaymentTermDays" => Number(0),
                "PurchaseLimit" => Money(),
                "Notes" => new TextBox { Dock = DockStyle.Top, Multiline = true, Height = 90, ScrollBars = ScrollBars.Vertical },
                _ => new TextBox { Dock = DockStyle.Top }
            };
            _fields[field.Key] = input;
            stack.Controls.Add(Field(field.Label, input, field.Key == "Notes" ? 118 : 58));
        }

        panel.Controls.Add(stack);
        return panel;
    }

    private Control BuildHistoryPanel()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Tab("Produtos vinculados", _productsGrid));
        tabs.TabPages.Add(Tab("Compras realizadas", _purchasesGrid));
        var payablesPanel = new Panel { Dock = DockStyle.Fill };
        payablesPanel.Controls.Add(_payablesGrid);
        payablesPanel.Controls.Add(_summaryLabel);
        tabs.TabPages.Add(Tab("Contas a pagar", payablesPanel));
        return tabs;
    }

    private void ConfigureGrid()
    {
        _grid.Columns.Add(Column(nameof(Supplier.Code), "Codigo", 90));
        _grid.Columns.Add(Column(nameof(Supplier.FantasyName), "Nome fantasia", 180));
        _grid.Columns.Add(Column(nameof(Supplier.LegalName), "Razao social", 180));
        _grid.Columns.Add(Column(nameof(Supplier.Cnpj), "CNPJ", 130));
        _grid.Columns.Add(Column(nameof(Supplier.Phone), "Telefone", 110));
        _grid.Columns.Add(Column(nameof(Supplier.WhatsApp), "WhatsApp", 110));
        _grid.Columns.Add(Column(nameof(Supplier.City), "Cidade", 120));
        _grid.Columns.Add(Column(nameof(Supplier.State), "UF", 45));
        _grid.SelectionChanged += (_, _) => LoadSelectedSupplier();
    }

    private void ConfigureHistoryGrids()
    {
        _productsGrid.Columns.Add(Column(nameof(SupplierProductHistoryItem.Sku), "SKU", 90));
        _productsGrid.Columns.Add(Column(nameof(SupplierProductHistoryItem.Name), "Produto", 220));
        _productsGrid.Columns.Add(Column(nameof(SupplierProductHistoryItem.LastCostPrice), "Ultimo custo", 100));
        _productsGrid.Columns.Add(Column(nameof(SupplierProductHistoryItem.LastPurchaseAtUtc), "Ultima compra", 120));
        _productsGrid.Columns.Add(Column(nameof(SupplierProductHistoryItem.PurchasedQuantity), "Qtd comprada", 110));
        _purchasesGrid.Columns.Add(Column(nameof(SupplierPurchaseHistoryItem.Number), "Compra", 110));
        _purchasesGrid.Columns.Add(Column(nameof(SupplierPurchaseHistoryItem.PurchasedAtUtc), "Data", 130));
        _purchasesGrid.Columns.Add(Column(nameof(SupplierPurchaseHistoryItem.TotalAmount), "Total", 100));
        _purchasesGrid.Columns.Add(Column(nameof(SupplierPurchaseHistoryItem.Notes), "Obs", 240));
        _payablesGrid.Columns.Add(Column(nameof(AccountPayable.Number), "Numero", 110));
        _payablesGrid.Columns.Add(Column(nameof(AccountPayable.Description), "Descricao", 240));
        _payablesGrid.Columns.Add(Column(nameof(AccountPayable.DueDateUtc), "Vencimento", 120));
        _payablesGrid.Columns.Add(Column(nameof(AccountPayable.BalanceAmount), "Saldo", 100));
        _payablesGrid.Columns.Add(Column(nameof(AccountPayable.Status), "Status", 90));
    }

    private void LoadSuppliers()
    {
        _grid.DataSource = _suppliers.Search(new SupplierSearchRequest(_searchBox.Text, _activeOnlyBox.Checked)).ToList();
    }

    private void LoadSelectedSupplier()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Supplier supplier)
        {
            return;
        }

        _selectedId = supplier.Id;
        Set("Code", supplier.Code);
        Set("PersonType", supplier.PersonType);
        Set("FantasyName", supplier.FantasyName);
        Set("LegalName", supplier.LegalName);
        Set("Cnpj", supplier.Cnpj);
        Set("StateRegistration", supplier.StateRegistration);
        Set("MunicipalRegistration", supplier.MunicipalRegistration);
        Set("IsActive", supplier.IsActive);
        Set("Phone", supplier.Phone);
        Set("MobilePhone", supplier.MobilePhone);
        Set("WhatsApp", supplier.WhatsApp);
        Set("Email", supplier.Email);
        Set("Website", supplier.Website);
        Set("ContactName", supplier.ContactName);
        Set("ContactRole", supplier.ContactRole);
        Set("ZipCode", supplier.ZipCode);
        Set("Address", supplier.Address);
        Set("AddressNumber", supplier.AddressNumber);
        Set("Complement", supplier.Complement);
        Set("District", supplier.District);
        Set("City", supplier.City);
        Set("State", supplier.State);
        Set("DefaultPaymentTermDays", supplier.DefaultPaymentTermDays);
        Set("PurchaseLimit", supplier.PurchaseLimit);
        Set("Notes", supplier.Notes);
        LoadHistory();
    }

    private void LoadHistory()
    {
        if (!_selectedId.HasValue)
        {
            _productsGrid.DataSource = null;
            _purchasesGrid.DataSource = null;
            _payablesGrid.DataSource = null;
            _summaryLabel.Text = string.Empty;
            return;
        }

        _productsGrid.DataSource = _suppliers.Products(_selectedId.Value).ToList();
        _purchasesGrid.DataSource = _suppliers.PurchaseHistory(_selectedId.Value).ToList();
        _payablesGrid.DataSource = _suppliers.Payables(_selectedId.Value).ToList();
        var summary = _suppliers.FinancialSummary(_selectedId.Value);
        _summaryLabel.Text = $"Abertas: {summary.OpenCount} / {summary.OpenAmount:C}    Pagas: {summary.PaidCount} / {summary.PaidAmount:C}    Vencidas: {summary.OverdueCount} / {summary.OverdueAmount:C}";
    }

    private void SaveSupplier()
    {
        try
        {
            var request = Request();
            var supplier = _selectedId.HasValue ? _suppliers.Update(_selectedId.Value, request) : _suppliers.Create(request);
            _selectedId = supplier.Id;
            LoadSuppliers();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Fornecedores", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void InactivateSupplier()
    {
        if (!_selectedId.HasValue)
        {
            return;
        }

        try
        {
            _suppliers.Inactivate(_selectedId.Value);
            LoadSuppliers();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Fornecedores", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CreatePayable()
    {
        if (!_selectedId.HasValue)
        {
            return;
        }

        var number = $"CP-{DateTime.Now:yyyyMMddHHmmss}";
        try
        {
            _suppliers.CreatePayable(new SupplierPayableRequest(_selectedId.Value, number, "Conta vinculada ao fornecedor", 0m, DateTime.UtcNow.AddDays(30), "A definir"));
            LoadHistory();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Fornecedores", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private SupplierUpsertRequest Request()
    {
        return new SupplierUpsertRequest(
            FieldText("FantasyName"),
            FieldText("Cnpj"),
            FieldText("Phone"),
            FieldText("Email"),
            FieldText("Address"),
            Code: FieldText("Code"),
            PersonType: ((ComboBox)_fields["PersonType"]).SelectedItem is PersonType type ? type : PersonType.Juridica,
            FantasyName: FieldText("FantasyName"),
            LegalName: FieldText("LegalName"),
            StateRegistration: FieldText("StateRegistration"),
            MunicipalRegistration: FieldText("MunicipalRegistration"),
            MobilePhone: FieldText("MobilePhone"),
            WhatsApp: FieldText("WhatsApp"),
            Website: FieldText("Website"),
            ZipCode: FieldText("ZipCode"),
            AddressNumber: FieldText("AddressNumber"),
            Complement: FieldText("Complement"),
            District: FieldText("District"),
            City: FieldText("City"),
            State: FieldText("State"),
            ContactName: FieldText("ContactName"),
            ContactRole: FieldText("ContactRole"),
            DefaultPaymentTermDays: (int)((NumericUpDown)_fields["DefaultPaymentTermDays"]).Value,
            PurchaseLimit: ((NumericUpDown)_fields["PurchaseLimit"]).Value,
            Notes: FieldText("Notes"),
            IsActive: ((CheckBox)_fields["IsActive"]).Checked);
    }

    private void ClearForm()
    {
        _selectedId = null;
        foreach (var control in _fields.Values)
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.Clear();
                    break;
                case CheckBox checkBox:
                    checkBox.Checked = true;
                    break;
                case NumericUpDown numeric:
                    numeric.Value = 0;
                    break;
                case ComboBox combo:
                    combo.SelectedItem = PersonType.Juridica;
                    break;
            }
        }

        LoadHistory();
    }

    private void Import(string filter, Func<string, SupplierImportResult> import)
    {
        using var dialog = new OpenFileDialog { Filter = filter };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var result = import(dialog.FileName);
        LoadSuppliers();
        MessageBox.Show(this, $"Linhas: {result.TotalRows}\nImportados: {result.ImportedRows}\nAtualizados: {result.UpdatedRows}\nIgnorados: {result.IgnoredRows}\nErros: {result.ErrorRows}", "Importacao", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportFicha()
    {
        if (!_selectedId.HasValue)
        {
            return;
        }

        Export("PDF (*.pdf)|*.pdf", "ficha-fornecedor.pdf", p => File.WriteAllBytes(p, _reports.SupplierFichaPdf(_selectedId.Value)));
    }

    private void Export(string filter, string fileName, Action<string> export)
    {
        using var dialog = new SaveFileDialog { Filter = filter, FileName = fileName };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            export(dialog.FileName);
        }
    }

    private SupplierImportOptions ImportOptions() => new(UpdateExisting: true, IgnoreDuplicates: true);
    private SupplierReportRequest ReportRequest() => new(_searchBox.Text, _activeOnlyBox.Checked);

    private string FieldText(string key) => _fields[key] is TextBox textBox ? textBox.Text : string.Empty;

    private void Set(string key, object? value)
    {
        if (!_fields.TryGetValue(key, out var control))
        {
            return;
        }

        switch (control)
        {
            case TextBox textBox:
                textBox.Text = value?.ToString() ?? string.Empty;
                break;
            case CheckBox checkBox when value is bool boolValue:
                checkBox.Checked = boolValue;
                break;
            case NumericUpDown numeric when value is int intValue:
                numeric.Value = Math.Clamp(intValue, (int)numeric.Minimum, (int)numeric.Maximum);
                break;
            case NumericUpDown numeric when value is decimal decimalValue:
                numeric.Value = Math.Clamp(decimalValue, numeric.Minimum, numeric.Maximum);
                break;
            case ComboBox combo when value is PersonType type:
                combo.SelectedItem = type;
                break;
        }
    }

    private static TabPage Tab(string title, Control content)
    {
        var page = new TabPage(title) { BackColor = Color.White, Padding = new Padding(0) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        ReadOnly = true,
        AllowUserToAddRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        BackgroundColor = Color.White
    };

    private static NumericUpDown Number(decimal value) => new() { Dock = DockStyle.Top, Maximum = 9999, Value = value };

    private static NumericUpDown Money() => new()
    {
        Dock = DockStyle.Top,
        DecimalPlaces = 2,
        Maximum = 999999999,
        ThousandsSeparator = true
    };

    private static Panel Field(string label, Control control, int height)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = height, Padding = new Padding(0, 0, 0, 8) };
        panel.Controls.Add(control);
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 21, ForeColor = Color.FromArgb(45, 55, 72) });
        return panel;
    }

    private static DataGridViewTextBoxColumn Column(string property, string header, int width)
    {
        return new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = header, Width = width };
    }

    private static Button Button(string text, Color color, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 34, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        button.Click += click;
        return button;
    }
}
