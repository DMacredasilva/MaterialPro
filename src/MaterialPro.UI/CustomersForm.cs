using MaterialPro.Application;
using MaterialPro.Domain;

namespace MaterialPro.UI;

public sealed class CustomersForm : Form
{
    private readonly ICustomerService _customers;
    private readonly ICustomerReportService _reports;
    private readonly DataGridView _grid;
    private readonly DataGridView _purchaseGrid;
    private readonly DataGridView _financialGrid;
    private readonly TextBox _searchBox;
    private readonly CheckBox _onlyActiveCheck;
    private readonly CheckBox _includeBlockedCheck;
    private readonly Label _creditLabel;
    private readonly TextBox _codeBox;
    private readonly TextBox _nameBox;
    private readonly TextBox _documentBox;
    private readonly TextBox _stateRegistrationBox;
    private readonly TextBox _phoneBox;
    private readonly TextBox _whatsAppBox;
    private readonly TextBox _emailBox;
    private readonly TextBox _zipCodeBox;
    private readonly TextBox _addressBox;
    private readonly TextBox _numberBox;
    private readonly TextBox _complementBox;
    private readonly TextBox _districtBox;
    private readonly TextBox _cityBox;
    private readonly TextBox _stateBox;
    private readonly NumericUpDown _creditLimitBox;
    private readonly TextBox _notesBox;
    private readonly CheckBox _activeBox;
    private readonly CheckBox _blockedBox;
    private Guid? _selectedId;

    public CustomersForm(ICustomerService customers, ICustomerReportService reports)
    {
        _customers = customers;
        _reports = reports;

        Text = "Sistema > Clientes";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1220, 780);
        BackColor = Color.FromArgb(246, 248, 252);
        Font = new Font("Segoe UI", 10F);

        _searchBox = new TextBox { Width = 340, PlaceholderText = "Buscar por nome, CPF/CNPJ, telefone ou WhatsApp" };
        _onlyActiveCheck = new CheckBox { Text = "Somente ativos", Checked = true, AutoSize = true };
        _includeBlockedCheck = new CheckBox { Text = "Incluir bloqueados", Checked = true, AutoSize = true };
        _creditLabel = new Label { AutoSize = true, ForeColor = Color.FromArgb(30, 78, 140), Padding = new Padding(10, 8, 0, 0) };

        _grid = Grid();
        ConfigureCustomerGrid();
        _purchaseGrid = Grid();
        _financialGrid = Grid();

        _codeBox = TextField();
        _nameBox = TextField();
        _documentBox = TextField();
        _stateRegistrationBox = TextField();
        _phoneBox = TextField();
        _whatsAppBox = TextField();
        _emailBox = TextField();
        _zipCodeBox = TextField();
        _addressBox = TextField();
        _numberBox = TextField();
        _complementBox = TextField();
        _districtBox = TextField();
        _cityBox = TextField();
        _stateBox = TextField();
        _creditLimitBox = MoneyField();
        _notesBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
        _activeBox = new CheckBox { Text = "Ativo", Checked = true, AutoSize = true };
        _blockedBox = new CheckBox { Text = "Bloqueado", AutoSize = true };

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(12),
            BackColor = Color.White
        };
        actions.Controls.AddRange([
            _searchBox,
            Button("Buscar", Color.FromArgb(30, 78, 140), (_, _) => LoadCustomers()),
            _onlyActiveCheck,
            _includeBlockedCheck,
            Button("Novo", Color.FromArgb(88, 98, 110), (_, _) => ClearForm()),
            Button("Salvar", Color.FromArgb(28, 120, 84), (_, _) => SaveCustomer()),
            Button("Inativar", Color.FromArgb(170, 70, 50), (_, _) => InactivateCustomer()),
            Button("Bloquear", Color.FromArgb(150, 82, 40), (_, _) => BlockCustomer()),
            Button("Desbloquear", Color.FromArgb(70, 120, 78), (_, _) => UnblockCustomer()),
            Button("Ficha PDF", Color.FromArgb(40, 92, 150), (_, _) => SaveFicha()),
            Button("PDF", Color.FromArgb(40, 92, 150), (_, _) => Export("PDF (*.pdf)|*.pdf", "clientes.pdf", p => File.WriteAllBytes(p, _reports.ExportPdf(ReportRequest())))),
            Button("Excel", Color.FromArgb(40, 110, 80), (_, _) => Export("Excel (*.xlsx)|*.xlsx", "clientes.xlsx", p => File.WriteAllBytes(p, _reports.ExportExcel(ReportRequest())))),
            Button("CSV", Color.FromArgb(80, 90, 110), (_, _) => Export("CSV (*.csv)|*.csv", "clientes.csv", p => File.WriteAllBytes(p, _reports.ExportCsv(ReportRequest())))),
            _creditLabel
        ]);
        _onlyActiveCheck.CheckedChanged += (_, _) => LoadCustomers();
        _includeBlockedCheck.CheckedChanged += (_, _) => LoadCustomers();

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Page("Consulta", _grid));
        tabs.TabPages.Add(Page("Cadastro", BuildEditor()));
        tabs.TabPages.Add(Page("Historico de compras", _purchaseGrid));
        tabs.TabPages.Add(Page("Historico financeiro", _financialGrid));

        Controls.Add(tabs);
        Controls.Add(actions);
        LoadCustomers();
    }

    private Control BuildEditor()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), AutoScroll = true };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        Add(root, "Codigo", _codeBox, 0, 0);
        Add(root, "Nome", _nameBox, 1, 0);
        Add(root, "CPF/CNPJ", _documentBox, 0, 1);
        Add(root, "RG/IE", _stateRegistrationBox, 1, 1);
        Add(root, "Telefone", _phoneBox, 0, 2);
        Add(root, "WhatsApp", _whatsAppBox, 1, 2);
        Add(root, "E-mail", _emailBox, 0, 3);
        Add(root, "CEP", _zipCodeBox, 1, 3);
        Add(root, "Endereco", _addressBox, 0, 4);
        Add(root, "Numero", _numberBox, 1, 4);
        Add(root, "Complemento", _complementBox, 0, 5);
        Add(root, "Bairro", _districtBox, 1, 5);
        Add(root, "Cidade", _cityBox, 0, 6);
        Add(root, "Estado", _stateBox, 1, 6);
        Add(root, "Limite de credito", _creditLimitBox, 0, 7);

        var checks = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        checks.Controls.AddRange([_activeBox, _blockedBox]);
        Add(root, "Status", checks, 1, 7);

        var notesPanel = Field("Observacoes", _notesBox, 130);
        root.Controls.Add(notesPanel, 0, 8);
        root.SetColumnSpan(notesPanel, 2);
        return root;
    }

    private void ConfigureCustomerGrid()
    {
        _grid.AutoGenerateColumns = false;
        _grid.Columns.Add(Column(nameof(Customer.Code), "Codigo", 90));
        _grid.Columns.Add(Column(nameof(Customer.FullName), "Nome", 230));
        _grid.Columns.Add(Column(nameof(Customer.DocumentNumber), "CPF/CNPJ", 120));
        _grid.Columns.Add(Column(nameof(Customer.Phone), "Telefone", 110));
        _grid.Columns.Add(Column(nameof(Customer.WhatsApp), "WhatsApp", 110));
        _grid.Columns.Add(Column(nameof(Customer.City), "Cidade", 130));
        _grid.Columns.Add(Column(nameof(Customer.State), "UF", 45));
        _grid.Columns.Add(Column(nameof(Customer.CreditLimit), "Limite", 90));
        _grid.Columns.Add(Column(nameof(Customer.IsActive), "Ativo", 60));
        _grid.Columns.Add(Column(nameof(Customer.IsBlocked), "Bloqueado", 80));
        _grid.SelectionChanged += (_, _) => LoadSelectedCustomer();
    }

    private void LoadCustomers()
    {
        _grid.DataSource = _customers.Search(new CustomerSearchRequest(_searchBox.Text, _onlyActiveCheck.Checked, _includeBlockedCheck.Checked)).ToList();
    }

    private void LoadSelectedCustomer()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Customer customer)
        {
            return;
        }

        _selectedId = customer.Id;
        _codeBox.Text = customer.Code;
        _nameBox.Text = customer.FullName;
        _documentBox.Text = customer.DocumentNumber;
        _stateRegistrationBox.Text = customer.StateRegistration;
        _phoneBox.Text = customer.Phone;
        _whatsAppBox.Text = customer.WhatsApp;
        _emailBox.Text = customer.Email;
        _zipCodeBox.Text = customer.ZipCode;
        _addressBox.Text = customer.Address;
        _numberBox.Text = customer.AddressNumber;
        _complementBox.Text = customer.Complement;
        _districtBox.Text = customer.District;
        _cityBox.Text = customer.City;
        _stateBox.Text = customer.State;
        _creditLimitBox.Value = Clamp(customer.CreditLimit, _creditLimitBox);
        _notesBox.Text = customer.Notes;
        _activeBox.Checked = customer.IsActive;
        _blockedBox.Checked = customer.IsBlocked;

        LoadHistories(customer.Id);
    }

    private void LoadHistories(Guid customerId)
    {
        _purchaseGrid.DataSource = _customers.PurchaseHistory(customerId).ToList();
        _financialGrid.DataSource = _customers.FinancialHistory(customerId).ToList();
        var credit = _customers.CreditSummary(customerId);
        _creditLabel.Text = $"Limite: {credit.CreditLimit:C} | Aberto: {credit.OpenBalance:C} | Disponivel: {credit.AvailableCredit:C}";
    }

    private void SaveCustomer()
    {
        var request = RequestFromForm();
        try
        {
            var customer = _selectedId.HasValue ? _customers.Update(_selectedId.Value, request) : _customers.Create(request);
            _selectedId = customer.Id;
            LoadCustomers();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Clientes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void InactivateCustomer()
    {
        if (!_selectedId.HasValue) return;
        _customers.Inactivate(_selectedId.Value);
        LoadCustomers();
    }

    private void BlockCustomer()
    {
        if (!_selectedId.HasValue) return;
        _customers.Block(_selectedId.Value, "Bloqueio manual");
        LoadCustomers();
    }

    private void UnblockCustomer()
    {
        if (!_selectedId.HasValue) return;
        _customers.Unblock(_selectedId.Value);
        LoadCustomers();
    }

    private void SaveFicha()
    {
        if (!_selectedId.HasValue) return;
        Export("PDF (*.pdf)|*.pdf", $"ficha-cliente-{_codeBox.Text}.pdf", p => File.WriteAllBytes(p, _reports.CustomerFichaPdf(_selectedId.Value)));
    }

    private CustomerUpsertRequest RequestFromForm()
    {
        return new CustomerUpsertRequest(
            _nameBox.Text,
            _documentBox.Text,
            _phoneBox.Text,
            _emailBox.Text,
            _addressBox.Text,
            _cityBox.Text,
            _codeBox.Text,
            _stateRegistrationBox.Text,
            _whatsAppBox.Text,
            _zipCodeBox.Text,
            _numberBox.Text,
            _complementBox.Text,
            _districtBox.Text,
            _stateBox.Text,
            _creditLimitBox.Value,
            _notesBox.Text,
            _activeBox.Checked,
            _blockedBox.Checked);
    }

    private CustomerReportRequest ReportRequest() => new(_searchBox.Text, _onlyActiveCheck.Checked, _includeBlockedCheck.Checked);

    private void ClearForm()
    {
        _selectedId = null;
        foreach (var box in new[] { _codeBox, _nameBox, _documentBox, _stateRegistrationBox, _phoneBox, _whatsAppBox, _emailBox, _zipCodeBox, _addressBox, _numberBox, _complementBox, _districtBox, _cityBox, _stateBox, _notesBox })
        {
            box.Clear();
        }

        _creditLimitBox.Value = 0;
        _activeBox.Checked = true;
        _blockedBox.Checked = false;
        _purchaseGrid.DataSource = null;
        _financialGrid.DataSource = null;
        _creditLabel.Text = string.Empty;
    }

    private static void Export(string filter, string fileName, Action<string> export)
    {
        using var dialog = new SaveFileDialog { Filter = filter, FileName = fileName };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            export(dialog.FileName);
        }
    }

    private static DataGridView Grid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = true,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false
        };
    }

    private static TabPage Page(string title, Control control)
    {
        var page = new TabPage(title);
        page.Controls.Add(control);
        return page;
    }

    private static DataGridViewTextBoxColumn Column(string property, string header, int width)
    {
        return new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = header, Width = width };
    }

    private static TextBox TextField() => new() { Dock = DockStyle.Top };

    private static NumericUpDown MoneyField() => new()
    {
        Dock = DockStyle.Top,
        DecimalPlaces = 2,
        Maximum = 999999999,
        ThousandsSeparator = true
    };

    private static void Add(TableLayoutPanel root, string label, Control control, int column, int row)
    {
        root.Controls.Add(Field(label, control), column, row);
    }

    private static Panel Field(string label, Control control, int height = 62)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = height, Padding = new Padding(0, 0, 10, 8) };
        panel.Controls.Add(control);
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 20 });
        return panel;
    }

    private static Button Button(string text, Color color, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 34,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(4)
        };
        button.Click += click;
        return button;
    }

    private static decimal Clamp(decimal value, NumericUpDown control)
    {
        if (value < control.Minimum) return control.Minimum;
        if (value > control.Maximum) return control.Maximum;
        return value;
    }
}
