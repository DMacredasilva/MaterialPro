using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;

namespace MaterialPro.UI;

public sealed class NonFiscalNoteForm : Form
{
    private readonly IStoreProfileService _storeProfileService;
    private readonly INonFiscalNoteService _noteService;
    private readonly TextBox _numberBox;
    private readonly TextBox _storeNameBox;
    private readonly TextBox _storeDocumentBox;
    private readonly TextBox _customerNameBox;
    private readonly TextBox _customerDocumentBox;
    private readonly TextBox _customerAddressBox;
    private readonly TextBox _notesBox;
    private readonly DataGridView _itemsGrid;
    private readonly Label _totalLabel;

    public NonFiscalNoteForm(IStoreProfileService storeProfileService, INonFiscalNoteService noteService)
    {
        _storeProfileService = storeProfileService;
        _noteService = noteService;

        Text = "Nota avulsa nao fiscal";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 760);
        BackColor = Color.FromArgb(246, 248, 252);
        Font = new Font("Segoe UI", 10F);

        var profile = _storeProfileService.Get();

        _numberBox = new TextBox { Dock = DockStyle.Top, Text = $"NAF-{DateTime.UtcNow:yyyyMMddHHmmss}" };
        _storeNameBox = new TextBox { Dock = DockStyle.Top, Text = string.IsNullOrWhiteSpace(profile.StoreName) ? "Minha Loja" : profile.StoreName };
        _storeDocumentBox = new TextBox { Dock = DockStyle.Top, Text = profile.Cnpj };
        _customerNameBox = new TextBox { Dock = DockStyle.Top };
        _customerDocumentBox = new TextBox { Dock = DockStyle.Top };
        _customerAddressBox = new TextBox { Dock = DockStyle.Top, Multiline = true, Height = 60 };
        _notesBox = new TextBox { Dock = DockStyle.Top, Multiline = true, Height = 70 };
        _itemsGrid = CreateGrid();
        _totalLabel = new Label { Dock = DockStyle.Top, Height = 28, Text = "Total: R$ 0,00", Font = new Font("Segoe UI", 11F, FontStyle.Bold) };

        var header = new Label
        {
            Text = "Documento não fiscal para impressão e controle interno",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Color.FromArgb(90, 105, 130)
        };

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0),
            WrapContents = false
        };

        var addItemButton = MakeButton("Adicionar item", Color.FromArgb(30, 78, 140));
        addItemButton.Click += (_, _) => _itemsGrid.Rows.Add("", 1m, 0m, 0m);

        var saveButton = MakeButton("Salvar", Color.FromArgb(28, 120, 84));
        saveButton.Click += (_, _) => SaveNote();

        var printButton = MakeButton("Gerar PDF", Color.FromArgb(58, 86, 160));
        printButton.Click += (_, _) => GeneratePdf();

        toolbar.Controls.Add(addItemButton);
        toolbar.Controls.Add(saveButton);
        toolbar.Controls.Add(printButton);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        form.Controls.Add(MakeField("Numero", _numberBox), 0, 0);
        form.Controls.Add(MakeField("Loja", _storeNameBox), 1, 0);
        form.Controls.Add(MakeField("CNPJ", _storeDocumentBox), 0, 1);
        form.Controls.Add(MakeField("Cliente", _customerNameBox), 1, 1);
        form.Controls.Add(MakeField("Documento cliente", _customerDocumentBox), 0, 2);
        form.Controls.Add(MakeField("Endereco cliente", _customerAddressBox), 1, 2);
        var notesField = MakeField("Observacoes", _notesBox);
        form.Controls.Add(notesField, 0, 3);
        form.SetColumnSpan(notesField, 2);

        var itemsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        itemsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        itemsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        itemsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        itemsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        itemsPanel.Controls.Add(header, 0, 0);
        itemsPanel.Controls.Add(toolbar, 0, 1);
        itemsPanel.Controls.Add(_totalLabel, 0, 2);
        itemsPanel.Controls.Add(_itemsGrid, 0, 3);

        layout.Controls.Add(form, 0, 0);
        layout.Controls.Add(itemsPanel, 0, 1);
        Controls.Add(layout);
    }

    private DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Descricao", HeaderText = "Descricao" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantidade", HeaderText = "Qtd" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ValorUnitario", HeaderText = "Valor unit." });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Total", ReadOnly = true });
        grid.Rows.Add("Produto exemplo", 1m, 0m, 0m);
        grid.CellEndEdit += (_, _) => RecalcTotals(grid);
        grid.RowsRemoved += (_, _) => RecalcTotals(grid);
        return grid;
    }

    private static Panel MakeField(string label, Control control)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(0, 0, 10, 10) };
        panel.Controls.Add(control);
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 22 });
        control.Dock = DockStyle.Bottom;
        return panel;
    }

    private static Button MakeButton(string text, Color color)
    {
        return new Button
        {
            Text = text,
            Height = 38,
            Width = 160,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 10, 0)
        };
    }

    private void SaveNote()
    {
        RecalcTotals(_itemsGrid);
        var items = _itemsGrid.Rows.Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow && row.Cells[0].Value is not null && row.Cells[0].Value.ToString()?.Trim().Length > 0)
            .Select(row => new NonFiscalNoteItemRequest(
                row.Cells[0].Value?.ToString() ?? string.Empty,
                ParseDecimal(row.Cells[1].Value),
                ParseDecimal(row.Cells[2].Value)))
            .ToList();

        var note = _noteService.Create(new NonFiscalNoteRequest(
            _numberBox.Text,
            _storeNameBox.Text,
            _storeDocumentBox.Text,
            _customerNameBox.Text,
            _customerDocumentBox.Text,
            _customerAddressBox.Text,
            _notesBox.Text,
            items));

        MessageBox.Show(this, "Nota salva com sucesso.", "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var pdf = _noteService.GeneratePdf(note.Id);
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), $"materialpro-nota-{note.Number}.pdf"), pdf);
    }

    private void GeneratePdf()
    {
        RecalcTotals(_itemsGrid);
        SaveNote();
        MessageBox.Show(this, "PDF gerado no temp do sistema.", "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static decimal ParseDecimal(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        return decimal.TryParse(value.ToString(), out var result) ? result : 0;
    }

    private void RecalcTotals(DataGridView grid)
    {
        decimal total = 0;
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            var qty = ParseDecimal(row.Cells[1].Value);
            var unit = ParseDecimal(row.Cells[2].Value);
            var rowTotal = Math.Round(qty * unit, 2);
            row.Cells[3].Value = rowTotal;
            total += rowTotal;
        }

        _totalLabel.Text = $"Total: {total:C}";
    }
}
