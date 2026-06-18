using MaterialPro.Application;
using MaterialPro.Domain;

namespace MaterialPro.UI;

public sealed class ProductsForm : Form
{
    private readonly IProductService _products;
    private readonly IInventoryService _inventory;
    private readonly IProductImportService _importer;
    private readonly IProductReportService _reports;
    private readonly ISupplierService? _suppliers;
    private readonly DataGridView _grid;
    private readonly TextBox _searchBox;
    private readonly CheckBox _lowStockCheck;
    private readonly TextBox _skuBox;
    private readonly TextBox _nameBox;
    private readonly TextBox _descriptionBox;
    private readonly TextBox _categoryBox;
    private readonly TextBox _brandBox;
    private readonly TextBox _unitBox;
    private readonly NumericUpDown _salePriceBox;
    private readonly NumericUpDown _costPriceBox;
    private readonly NumericUpDown _minimumStockBox;
    private readonly NumericUpDown _stockMoveBox;
    private readonly TextBox _barcodeBox;
    private readonly TextBox _ncmBox;
    private readonly TextBox _locationBox;
    private readonly ComboBox _supplierBox;
    private Guid? _selectedId;

    public ProductsForm(IProductService products, IInventoryService inventory, IProductImportService importer, IProductReportService reports, ISupplierService? suppliers = null)
    {
        _products = products;
        _inventory = inventory;
        _importer = importer;
        _reports = reports;
        _suppliers = suppliers;

        Text = "Sistema > Produtos";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 760);
        BackColor = Color.FromArgb(246, 248, 252);
        Font = new Font("Segoe UI", 10F);

        _searchBox = new TextBox { Width = 320, PlaceholderText = "Buscar por SKU, nome, categoria, marca ou barras" };
        _lowStockCheck = new CheckBox { Text = "Estoque baixo", AutoSize = true };
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false
        };
        ConfigureGrid();

        _skuBox = TextField();
        _nameBox = TextField();
        _descriptionBox = TextField();
        _categoryBox = TextField();
        _brandBox = TextField();
        _unitBox = TextField("UN");
        _salePriceBox = MoneyField();
        _costPriceBox = MoneyField();
        _minimumStockBox = QuantityField();
        _stockMoveBox = QuantityField();
        _barcodeBox = TextField();
        _ncmBox = TextField();
        _locationBox = TextField();
        _supplierBox = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Text", ValueMember = "Id" };
        LoadSuppliers();

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        top.Controls.AddRange([
            _searchBox,
            Button("Buscar", Color.FromArgb(30, 78, 140), (_, _) => LoadProducts()),
            _lowStockCheck,
            Button("Novo", Color.FromArgb(88, 98, 110), (_, _) => ClearForm()),
            Button("Salvar", Color.FromArgb(28, 120, 84), (_, _) => SaveProduct()),
            Button("Entrada", Color.FromArgb(78, 116, 40), (_, _) => MoveStock(Math.Abs(_stockMoveBox.Value), "Entrada manual")),
            Button("Saida", Color.FromArgb(170, 70, 50), (_, _) => MoveStock(-Math.Abs(_stockMoveBox.Value), "Saida manual")),
            Button("Importar CSV", Color.FromArgb(80, 90, 110), (_, _) => Import("CSV (*.csv)|*.csv", p => _importer.ImportCsv(p, ImportOptions()))),
            Button("Importar Excel", Color.FromArgb(80, 90, 110), (_, _) => Import("Excel (*.xlsx)|*.xlsx", p => _importer.ImportExcel(p, ImportOptions()))),
            Button("Importar DBF", Color.FromArgb(80, 90, 110), (_, _) => Import("DBF (*.dbf)|*.dbf", p => _importer.ImportDbf(p, ImportOptions()))),
            Button("PDF", Color.FromArgb(40, 92, 150), (_, _) => Export("PDF (*.pdf)|*.pdf", "produtos.pdf", p => File.WriteAllBytes(p, _reports.ExportPdf(ReportRequest())))),
            Button("Excel", Color.FromArgb(40, 110, 80), (_, _) => Export("Excel (*.xlsx)|*.xlsx", "produtos.xlsx", p => File.WriteAllBytes(p, _reports.ExportExcel(ReportRequest()))))
        ]);
        _lowStockCheck.CheckedChanged += (_, _) => LoadProducts();

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 720 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(BuildEditor());

        Controls.Add(split);
        Controls.Add(top);
        LoadProducts();
    }

    private Control BuildEditor()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        stack.Controls.Add(Field("SKU", _skuBox));
        stack.Controls.Add(Field("Nome", _nameBox));
        stack.Controls.Add(Field("Descricao", _descriptionBox));
        stack.Controls.Add(Field("Categoria", _categoryBox));
        stack.Controls.Add(Field("Marca", _brandBox));
        stack.Controls.Add(Field("Unidade", _unitBox));
        stack.Controls.Add(Field("Preco venda", _salePriceBox));
        stack.Controls.Add(Field("Preco custo", _costPriceBox));
        stack.Controls.Add(Field("Estoque minimo", _minimumStockBox));
        stack.Controls.Add(Field("Movimento estoque", _stockMoveBox));
        stack.Controls.Add(Field("Codigo barras", _barcodeBox));
        stack.Controls.Add(Field("NCM", _ncmBox));
        stack.Controls.Add(Field("Localizacao", _locationBox));
        stack.Controls.Add(Field("Fornecedor principal", _supplierBox));
        panel.Controls.Add(stack);
        return panel;
    }

    private void ConfigureGrid()
    {
        _grid.Columns.Add(Column(nameof(Product.Sku), "SKU", 90));
        _grid.Columns.Add(Column(nameof(Product.Name), "Produto", 220));
        _grid.Columns.Add(Column(nameof(Product.Category), "Categoria", 120));
        _grid.Columns.Add(Column(nameof(Product.Brand), "Marca", 100));
        _grid.Columns.Add(Column(nameof(Product.Unit), "UN", 50));
        _grid.Columns.Add(Column(nameof(Product.StockQuantity), "Estoque", 80));
        _grid.Columns.Add(Column(nameof(Product.MinimumStock), "Minimo", 80));
        _grid.Columns.Add(Column(nameof(Product.SalePrice), "Venda", 80));
        _grid.Columns.Add(Column(nameof(Product.Barcode), "Barras", 120));
        _grid.SelectionChanged += (_, _) => LoadSelectedProduct();
    }

    private static DataGridViewTextBoxColumn Column(string property, string header, int width)
    {
        return new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = header, Width = width };
    }

    private void LoadProducts()
    {
        _grid.DataSource = _products.Search(new ProductSearchRequest(_searchBox.Text, true, _lowStockCheck.Checked)).ToList();
    }

    private void LoadSelectedProduct()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Product product)
        {
            return;
        }

        _selectedId = product.Id;
        _skuBox.Text = product.Sku;
        _nameBox.Text = product.Name;
        _descriptionBox.Text = product.Description;
        _categoryBox.Text = product.Category;
        _brandBox.Text = product.Brand;
        _unitBox.Text = product.Unit;
        _salePriceBox.Value = Clamp(product.SalePrice, _salePriceBox);
        _costPriceBox.Value = Clamp(product.CostPrice, _costPriceBox);
        _minimumStockBox.Value = Clamp(product.MinimumStock, _minimumStockBox);
        _stockMoveBox.Value = 0;
        _barcodeBox.Text = product.Barcode;
        _ncmBox.Text = product.Ncm;
        _locationBox.Text = product.Location;
        SelectSupplier(product.SupplierId);
    }

    private void SaveProduct()
    {
        var request = new ProductUpsertRequest(
            _skuBox.Text,
            _nameBox.Text,
            _unitBox.Text,
            _salePriceBox.Value,
            _costPriceBox.Value,
            _minimumStockBox.Value,
            _barcodeBox.Text,
            _descriptionBox.Text,
            _categoryBox.Text,
            _brandBox.Text,
            _ncmBox.Text,
            _locationBox.Text,
            SelectedSupplierId());

        try
        {
            var product = _selectedId.HasValue ? _products.Update(_selectedId.Value, request) : _products.Create(request);
            _selectedId = product.Id;
            LoadProducts();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Produtos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void MoveStock(decimal quantity, string reason)
    {
        if (!_selectedId.HasValue || quantity == 0)
        {
            return;
        }

        _inventory.Move(_selectedId.Value, quantity, reason, "Produtos");
        LoadProducts();
    }

    private void Import(string filter, Func<string, ProductImportResult> import)
    {
        using var dialog = new OpenFileDialog { Filter = filter };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var result = import(dialog.FileName);
        LoadProducts();
        MessageBox.Show(this, $"Linhas: {result.TotalRows}\nImportados: {result.ImportedRows}\nAtualizados: {result.UpdatedRows}\nIgnorados: {result.IgnoredRows}\nErros: {result.ErrorRows}", "Importacao", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void Export(string filter, string fileName, Action<string> export)
    {
        using var dialog = new SaveFileDialog { Filter = filter, FileName = fileName };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            export(dialog.FileName);
        }
    }

    private ProductImportOptions ImportOptions() => new(UpdateExisting: true, IgnoreDuplicates: true);
    private ProductReportRequest ReportRequest() => new(_searchBox.Text, true, _lowStockCheck.Checked);

    private void ClearForm()
    {
        _selectedId = null;
        foreach (var textBox in new[] { _skuBox, _nameBox, _descriptionBox, _categoryBox, _brandBox, _barcodeBox, _ncmBox, _locationBox })
        {
            textBox.Clear();
        }

        _unitBox.Text = "UN";
        _salePriceBox.Value = 0;
        _costPriceBox.Value = 0;
        _minimumStockBox.Value = 0;
        _stockMoveBox.Value = 0;
        SelectSupplier(null);
    }

    private void LoadSuppliers()
    {
        var items = new List<SupplierComboItem> { new(null, "Sem fornecedor principal") };
        if (_suppliers is not null)
        {
            items.AddRange(_suppliers.Search(new SupplierSearchRequest()).Select(x => new SupplierComboItem(x.Id, string.IsNullOrWhiteSpace(x.FantasyName) ? x.Name : x.FantasyName)));
        }

        _supplierBox.DataSource = items;
    }

    private Guid? SelectedSupplierId()
    {
        return _supplierBox.SelectedItem is SupplierComboItem item ? item.Id : null;
    }

    private void SelectSupplier(Guid? supplierId)
    {
        foreach (SupplierComboItem item in _supplierBox.Items)
        {
            if (item.Id == supplierId)
            {
                _supplierBox.SelectedItem = item;
                return;
            }
        }

        UiKit.SelectIfAvailable(_supplierBox, 0);
    }

    private static TextBox TextField(string text = "") => new() { Dock = DockStyle.Top, Text = text };

    private static NumericUpDown MoneyField() => new()
    {
        Dock = DockStyle.Top,
        DecimalPlaces = 2,
        Maximum = 999999999,
        ThousandsSeparator = true
    };

    private static NumericUpDown QuantityField() => new()
    {
        Dock = DockStyle.Top,
        DecimalPlaces = 3,
        Maximum = 999999999,
        Minimum = -999999999,
        ThousandsSeparator = true
    };

    private static Panel Field(string label, Control control)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(0, 0, 0, 8) };
        panel.Controls.Add(control);
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 20 });
        return panel;
    }

    private static Button Button(string text, Color color, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 34, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        button.Click += click;
        return button;
    }

    private static decimal Clamp(decimal value, NumericUpDown control)
    {
        if (value < control.Minimum) return control.Minimum;
        if (value > control.Maximum) return control.Maximum;
        return value;
    }

    private sealed record SupplierComboItem(Guid? Id, string Text);
}
