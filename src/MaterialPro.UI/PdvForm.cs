using MaterialPro.Application;
using MaterialPro.Domain;

namespace MaterialPro.UI;

public sealed class PdvForm : Form
{
    private readonly IPdvService _pdv;
    private readonly IProductService _products;
    private readonly ICustomerService _customers;
    private readonly AppUser _user;
    private readonly DataGridView _itemsGrid;
    private readonly TextBox _productBox;
    private readonly NumericUpDown _quantityBox;
    private readonly NumericUpDown _discountBox;
    private readonly ComboBox _paymentBox;
    private readonly Label _saleLabel;
    private readonly Label _totalLabel;
    private Sale? _sale;
    private Customer? _customer;

    public PdvForm(IPdvService pdv, IProductService products, ICustomerService customers, AppUser user)
    {
        _pdv = pdv;
        _products = products;
        _customers = customers;
        _user = user;

        Text = "MaterialPro - PDV";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 760);
        BackColor = Color.FromArgb(246, 248, 252);
        Font = new Font("Segoe UI", 11F);
        KeyPreview = true;

        _saleLabel = new Label { AutoSize = true, Font = new Font("Segoe UI", 13F, FontStyle.Bold), Padding = new Padding(8) };
        _totalLabel = new Label { Dock = DockStyle.Right, Width = 280, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 26F, FontStyle.Bold), ForeColor = Color.FromArgb(45, 126, 86) };
        _productBox = new TextBox { Width = 300, PlaceholderText = "Código, barras ou nome" };
        _quantityBox = QuantityField();
        _quantityBox.Value = 1;
        _discountBox = MoneyField();
        _paymentBox = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _paymentBox.Items.AddRange(["DINHEIRO", "PIX", "CARTAO_DEBITO", "CARTAO_CREDITO", "PRAZO"]);
        _paymentBox.SelectedIndex = 0;

        _itemsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false
        };
        _itemsGrid.Columns.Add(Column(nameof(SaleItem.ProductCode), "Código", 110));
        _itemsGrid.Columns.Add(Column(nameof(SaleItem.ProductDescription), "Descrição", 330));
        _itemsGrid.Columns.Add(Column(nameof(SaleItem.Quantity), "Qtd", 80));
        _itemsGrid.Columns.Add(Column(nameof(SaleItem.UnitPrice), "Unitário", 100));
        _itemsGrid.Columns.Add(Column(nameof(SaleItem.DiscountAmount), "Desconto", 100));
        _itemsGrid.Columns.Add(Column(nameof(SaleItem.TotalItem), "Total", 110));

        var top = new Panel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(12), BackColor = Color.White };
        top.Controls.Add(_totalLabel);
        top.Controls.Add(_saleLabel);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        actions.Controls.AddRange([
            Button("Nova venda", Color.FromArgb(30, 78, 140), (_, _) => NewSale()),
            Button("Consumidor final", Color.FromArgb(88, 98, 110), (_, _) => { _customer = null; RefreshHeader(); }),
            Button("Cliente F3", Color.FromArgb(88, 98, 110), (_, _) => SelectCustomer()),
            _productBox,
            new Label { Text = "Qtd", AutoSize = true, Padding = new Padding(4, 8, 0, 0) },
            _quantityBox,
            Button("Adicionar", Color.FromArgb(45, 126, 86), (_, _) => AddItem()),
            new Label { Text = "Desconto", AutoSize = true, Padding = new Padding(4, 8, 0, 0) },
            _discountBox,
            Button("Aplicar F4", Color.FromArgb(218, 124, 38), (_, _) => ApplyDiscount()),
            _paymentBox,
            Button("Finalizar F5", Color.FromArgb(45, 126, 86), (_, _) => FinalizeSale()),
            Button("Cancelar item F6", Color.FromArgb(170, 70, 50), (_, _) => RemoveItem()),
            Button("2ª via F10", Color.FromArgb(40, 92, 150), (_, _) => SecondCopy())
        ]);

        Controls.Add(_itemsGrid);
        Controls.Add(actions);
        Controls.Add(top);
        KeyDown += OnKeyDown;
        NewSale();
    }

    private void NewSale()
    {
        try
        {
            _sale = _pdv.CreateSale(new PdvCreateSaleRequest(_customer?.Id, _user.Id, null));
            LoadItems();
            RefreshHeader();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "PDV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AddItem()
    {
        if (_sale is null)
        {
            return;
        }

        try
        {
            var product = _products.FindBySkuOrBarcode(_productBox.Text) ?? _products.Search(new ProductSearchRequest(_productBox.Text)).FirstOrDefault();
            if (product is null)
            {
                MessageBox.Show(this, "Produto não encontrado.", "PDV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _pdv.AddItem(_sale.Id, new PdvSaleItemRequest(product.Id, _quantityBox.Value, product.SalePrice));
            _productBox.Clear();
            _quantityBox.Value = 1;
            LoadItems();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "PDV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RemoveItem()
    {
        if (_itemsGrid.CurrentRow?.DataBoundItem is not SaleItem item)
        {
            return;
        }

        _sale = _pdv.RemoveItem(item.Id);
        LoadItems();
    }

    private void ApplyDiscount()
    {
        if (_sale is null)
        {
            return;
        }

        try
        {
            _sale = _pdv.ApplyDiscount(_sale.Id, _discountBox.Value, _user.Role is UserRole.Admin or UserRole.Manager);
            RefreshHeader();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Desconto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void FinalizeSale()
    {
        if (_sale is null)
        {
            return;
        }

        try
        {
            var method = _paymentBox.Text;
            var payments = new[] { new PdvPaymentRequest(method, _sale.TotalAmount, method == "PRAZO" ? 2 : 1, method == "PRAZO" ? DateTime.UtcNow.AddDays(30) : null) };
            _sale = _pdv.FinalizeSale(new PdvFinalizeRequest(_sale.Id, payments, _sale.DiscountAmount, _sale.SurchargeAmount, ManagerAuthorized: _user.Role is UserRole.Admin or UserRole.Manager));
            using var dialog = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = $"cupom-{_sale.ReceiptNumber}.pdf" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                File.WriteAllBytes(dialog.FileName, _pdv.GenerateReceiptPdf(new PdvReceiptRequest(_sale.Id)));
            }
            MessageBox.Show(this, "Venda finalizada.", "PDV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            NewSale();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Finalização", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SecondCopy()
    {
        var number = Microsoft.VisualBasic.Interaction.InputBox("Número da venda:", "Segunda via", _sale?.ReceiptNumber ?? "");
        if (string.IsNullOrWhiteSpace(number))
        {
            return;
        }

        var sale = _pdv.Search(new PdvSaleSearchRequest(Number: number)).FirstOrDefault();
        if (sale is null)
        {
            MessageBox.Show(this, "Venda não encontrada.", "Segunda via", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = $"segunda-via-{sale.ReceiptNumber}.pdf" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllBytes(dialog.FileName, _pdv.GenerateReceiptPdf(new PdvReceiptRequest(sale.Id, InternalPaperFormat.A4)));
        }
    }

    private void SelectCustomer()
    {
        var term = Microsoft.VisualBasic.Interaction.InputBox("Nome ou documento do cliente:", "Cliente", "");
        if (string.IsNullOrWhiteSpace(term))
        {
            return;
        }

        _customer = _customers.Search(new CustomerSearchRequest(term)).FirstOrDefault();
        RefreshHeader();
    }

    private void LoadItems()
    {
        _itemsGrid.DataSource = _sale is null ? new List<SaleItem>() : _pdv.Items(_sale.Id).ToList();
        if (_sale is not null)
        {
            _sale = _pdv.Search(new PdvSaleSearchRequest(Number: _sale.ReceiptNumber)).FirstOrDefault() ?? _sale;
        }
        RefreshHeader();
    }

    private void RefreshHeader()
    {
        _saleLabel.Text = $"Venda: {_sale?.ReceiptNumber ?? "-"}   Operador: {_user.FullName}   Cliente: {_customer?.FullName ?? "Consumidor final"}   {DateTime.Now:dd/MM/yyyy HH:mm}";
        _totalLabel.Text = (_sale?.TotalAmount ?? 0m).ToString("C");
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F2) _productBox.Focus();
        if (e.KeyCode == Keys.F3) SelectCustomer();
        if (e.KeyCode == Keys.F4) ApplyDiscount();
        if (e.KeyCode == Keys.F5) FinalizeSale();
        if (e.KeyCode == Keys.F6) RemoveItem();
        if (e.KeyCode == Keys.F10) SecondCopy();
        if (e.KeyCode == Keys.Escape) Close();
    }

    private static DataGridViewTextBoxColumn Column(string property, string header, int width)
    {
        return new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = header, Width = width };
    }

    private static Button Button(string text, Color color, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 34, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private static NumericUpDown QuantityField() => new() { Width = 80, DecimalPlaces = 3, Maximum = 999999, Minimum = 0 };
    private static NumericUpDown MoneyField() => new() { Width = 100, DecimalPlaces = 2, Maximum = 999999 };
}
