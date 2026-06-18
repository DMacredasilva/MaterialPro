using MaterialPro.Application;
using MaterialPro.Domain;

namespace MaterialPro.UI;

public sealed class StockForm : Form
{
    private readonly IInventoryService _inventory;
    private readonly IStockImportService _importer;
    private readonly IStockReportService _reports;
    private readonly DataGridView _grid;
    private readonly DataGridView _movementsGrid;
    private readonly TextBox _searchBox;
    private readonly CheckBox _lowStockCheck;
    private readonly CheckBox _zeroStockCheck;
    private readonly NumericUpDown _quantityBox;
    private readonly TextBox _reasonBox;
    private readonly TextBox _warehouseBox;
    private readonly Label _dashboardLabel;
    private Guid? _selectedProductId;

    public StockForm(IInventoryService inventory, IStockImportService importer, IStockReportService reports)
    {
        _inventory = inventory;
        _importer = importer;
        _reports = reports;

        Text = "Sistema > Estoque";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1220, 760);
        BackColor = Color.FromArgb(246, 248, 252);
        Font = new Font("Segoe UI", 10F);

        _searchBox = new TextBox { Width = 300, PlaceholderText = "Código, barras, nome, categoria, marca" };
        _lowStockCheck = new CheckBox { Text = "Mínimo", AutoSize = true };
        _zeroStockCheck = new CheckBox { Text = "Zerados", AutoSize = true };
        _quantityBox = QuantityField();
        _reasonBox = new TextBox { Width = 240, PlaceholderText = "Motivo obrigatório para ajuste" };
        _warehouseBox = new TextBox { Width = 130, Text = "Loja" };
        _dashboardLabel = new Label { AutoSize = true, ForeColor = Color.FromArgb(31, 41, 55), Padding = new Padding(8, 8, 8, 0) };

        _grid = Grid();
        _grid.Columns.Add(Column(nameof(StockPositionItem.Sku), "SKU", 90));
        _grid.Columns.Add(Column(nameof(StockPositionItem.Name), "Produto", 240));
        _grid.Columns.Add(Column(nameof(StockPositionItem.Category), "Categoria", 120));
        _grid.Columns.Add(Column(nameof(StockPositionItem.SupplierName), "Fornecedor", 160));
        _grid.Columns.Add(Column(nameof(StockPositionItem.PhysicalStock), "Físico", 80));
        _grid.Columns.Add(Column(nameof(StockPositionItem.ReservedStock), "Reservado", 90));
        _grid.Columns.Add(Column(nameof(StockPositionItem.AvailableStock), "Disponível", 90));
        _grid.Columns.Add(Column(nameof(StockPositionItem.MinimumStock), "Mínimo", 80));
        _grid.SelectionChanged += (_, _) => LoadSelected();

        _movementsGrid = Grid();
        _movementsGrid.Height = 190;
        _movementsGrid.Columns.Add(Column(nameof(StockMovement.MovementAtUtc), "Data", 140));
        _movementsGrid.Columns.Add(Column(nameof(StockMovement.Type), "Operação", 130));
        _movementsGrid.Columns.Add(Column(nameof(StockMovement.Quantity), "Qtd", 80));
        _movementsGrid.Columns.Add(Column(nameof(StockMovement.PreviousStock), "Anterior", 80));
        _movementsGrid.Columns.Add(Column(nameof(StockMovement.CurrentStock), "Atual", 80));
        _movementsGrid.Columns.Add(Column(nameof(StockMovement.Reason), "Motivo", 220));

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        top.Controls.AddRange([
            _searchBox,
            Button("Buscar", Color.FromArgb(30, 78, 140), (_, _) => LoadStock()),
            _lowStockCheck,
            _zeroStockCheck,
            new Label { Text = "Qtd", AutoSize = true, Padding = new Padding(6, 8, 0, 0) },
            _quantityBox,
            _warehouseBox,
            _reasonBox,
            Button("Entrada", Color.FromArgb(45, 126, 86), (_, _) => RegisterEntry()),
            Button("Saída", Color.FromArgb(170, 70, 50), (_, _) => RegisterExit()),
            Button("Ajustar", Color.FromArgb(218, 124, 38), (_, _) => Adjust()),
            Button("Reservar", Color.FromArgb(80, 90, 140), (_, _) => Reserve()),
            Button("Inventário", Color.FromArgb(74, 93, 115), (_, _) => Inventory()),
            Button("Transferir", Color.FromArgb(74, 93, 115), (_, _) => Transfer()),
            Button("Importar CSV", Color.FromArgb(80, 90, 110), (_, _) => Import("CSV (*.csv)|*.csv", p => _importer.ImportCsv(p, ImportOptions()))),
            Button("Importar Excel", Color.FromArgb(80, 90, 110), (_, _) => Import("Excel (*.xlsx)|*.xlsx", p => _importer.ImportExcel(p, ImportOptions()))),
            Button("Importar DBF", Color.FromArgb(80, 90, 110), (_, _) => Import("DBF (*.dbf)|*.dbf", p => _importer.ImportDbf(p, ImportOptions()))),
            Button("PDF", Color.FromArgb(40, 92, 150), (_, _) => Export("PDF (*.pdf)|*.pdf", "estoque.pdf", p => File.WriteAllBytes(p, _reports.ExportPdf(ReportRequest())))),
            Button("Excel", Color.FromArgb(40, 110, 80), (_, _) => Export("Excel (*.xlsx)|*.xlsx", "estoque.xlsx", p => File.WriteAllBytes(p, _reports.ExportExcel(ReportRequest()))))
        ]);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 470 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_movementsGrid);
        split.Panel2.Controls.Add(_dashboardLabel);
        _dashboardLabel.Dock = DockStyle.Top;

        Controls.Add(split);
        Controls.Add(top);
        _lowStockCheck.CheckedChanged += (_, _) => LoadStock();
        _zeroStockCheck.CheckedChanged += (_, _) => LoadStock();
        LoadStock();
    }

    private void LoadStock()
    {
        _grid.DataSource = _inventory.Query(new StockQueryRequest(_searchBox.Text, OnlyLowStock: _lowStockCheck.Checked, OnlyZeroStock: _zeroStockCheck.Checked)).ToList();
        var dash = _inventory.Dashboard();
        _dashboardLabel.Text = $"Produtos: {dash.TotalProducts}   Estoque total: {dash.TotalStock:N3}   Abaixo mínimo: {dash.LowStockProducts}   Zerados: {dash.ZeroStockProducts}";
    }

    private void LoadSelected()
    {
        if (_grid.CurrentRow?.DataBoundItem is not StockPositionItem item)
        {
            return;
        }

        _selectedProductId = item.ProductId;
        _movementsGrid.DataSource = _inventory.Movements(item.ProductId).ToList();
    }

    private void RegisterEntry() => RegisterMovement(Math.Abs(_quantityBox.Value), StockMovementType.ManualEntry, "Entrada manual");
    private void RegisterExit() => RegisterMovement(-Math.Abs(_quantityBox.Value), StockMovementType.LossExit, "Saída manual");

    private void RegisterMovement(decimal quantity, StockMovementType type, string fallbackReason)
    {
        if (!_selectedProductId.HasValue || quantity == 0)
        {
            return;
        }

        try
        {
            var request = new StockMoveRequest(_selectedProductId.Value, quantity, type, Default(_reasonBox.Text, fallbackReason), "ESTOQUE", Warehouse: Default(_warehouseBox.Text, "Loja"));
            if (quantity > 0)
            {
                _inventory.EnterStock(request);
            }
            else
            {
                _inventory.ExitStock(request);
            }
            LoadStock();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Estoque", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Adjust()
    {
        if (!_selectedProductId.HasValue)
        {
            return;
        }

        try
        {
            _inventory.AdjustStock(new StockAdjustRequest(_selectedProductId.Value, _quantityBox.Value, _reasonBox.Text, Warehouse: Default(_warehouseBox.Text, "Loja")));
            LoadStock();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ajuste", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Reserve()
    {
        if (!_selectedProductId.HasValue)
        {
            return;
        }

        try
        {
            _inventory.Reserve(new StockReservationRequest(_selectedProductId.Value, Math.Abs(_quantityBox.Value), "Pedido", Default(_reasonBox.Text, "Reserva manual")));
            LoadStock();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Reserva", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Inventory()
    {
        if (!_selectedProductId.HasValue)
        {
            return;
        }

        try
        {
            var inventory = _inventory.StartInventory(new StockInventoryRequest(null, Default(_reasonBox.Text, "Inventario manual")));
            _inventory.CountInventoryItem(inventory.Id, new StockInventoryItemRequest(_selectedProductId.Value, _quantityBox.Value));
            _inventory.CloseInventory(inventory.Id, applyAdjustments: true);
            LoadStock();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Inventário", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Transfer()
    {
        if (!_selectedProductId.HasValue)
        {
            return;
        }

        try
        {
            _inventory.Transfer(new StockTransferRequest(_selectedProductId.Value, Math.Abs(_quantityBox.Value), Default(_warehouseBox.Text, "Loja"), "Depósito Principal", null, _reasonBox.Text));
            LoadStock();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Transferência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Import(string filter, Func<string, StockImportResult> import)
    {
        using var dialog = new OpenFileDialog { Filter = filter };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var result = import(dialog.FileName);
        LoadStock();
        MessageBox.Show(this, $"Linhas: {result.TotalRows}\nImportados: {result.ImportedRows}\nAtualizados: {result.UpdatedRows}\nIgnorados: {result.IgnoredRows}\nErros: {result.ErrorRows}", "Importação", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void Export(string filter, string fileName, Action<string> export)
    {
        using var dialog = new SaveFileDialog { Filter = filter, FileName = fileName };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            export(dialog.FileName);
        }
    }

    private StockImportOptions ImportOptions() => new(UpdateProducts: true, AllowNegative: false);
    private StockReportRequest ReportRequest() => new(_searchBox.Text, _lowStockCheck.Checked, _zeroStockCheck.Checked);

    private static DataGridView Grid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false
        };
    }

    private static DataGridViewTextBoxColumn Column(string property, string header, int width)
    {
        return new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = header, Width = width };
    }

    private static Button Button(string text, Color color, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Height = 34 };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private static NumericUpDown QuantityField()
    {
        return new NumericUpDown { Width = 90, DecimalPlaces = 3, Maximum = 9999999, Minimum = 0 };
    }

    private static string Default(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
