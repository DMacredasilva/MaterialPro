using MaterialPro.Application;
using MaterialPro.Domain;

namespace MaterialPro.UI;

public sealed class ReportsCenterForm : Form
{
    private readonly IReportsCenterService _reports;
    private readonly AppUser _user;
    private readonly ComboBox _reportBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly DateTimePicker _from = new() { Width = 130 };
    private readonly DateTimePicker _to = new() { Width = 130 };
    private readonly TextBox _term = new() { Width = 220, PlaceholderText = "Filtro/status/termo" };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true, AllowUserToAddRows = false };
    private readonly Label _dashboard = new() { Dock = DockStyle.Top, Height = 54, Padding = new Padding(10), ForeColor = Color.FromArgb(31, 41, 55), BackColor = Color.White };
    private readonly Panel _chart = new() { Dock = DockStyle.Top, Height = 150, Padding = new Padding(12), BackColor = Color.White };
    private ReportsDashboardSummary? _lastDashboard;
    private IReadOnlyList<ReportDefinition> _catalog = [];

    public ReportsCenterForm(IReportsCenterService reports, AppUser user)
    {
        _reports = reports;
        _user = user;
        Text = "MaterialPro - Central de Relatorios";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 720);
        Font = new Font("Segoe UI", 10F);

        _from.Value = DateTime.Today.AddDays(-30);
        _to.Value = DateTime.Today.AddDays(1).AddTicks(-1);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(8), WrapContents = true };
        top.Controls.AddRange([
            _reportBox,
            _from,
            _to,
            _term,
            Button("Visualizar", (_, _) => Preview()),
            Button("PDF", (_, _) => Save("PDF (*.pdf)|*.pdf", "relatorio.pdf", p => File.WriteAllBytes(p, _reports.ExportPdf(Request(), _user)))),
            Button("Excel", (_, _) => Save("Excel (*.xlsx)|*.xlsx", "relatorio.xlsx", p => File.WriteAllBytes(p, _reports.ExportExcel(Request(), _user)))),
            Button("Imprimir", (_, _) => Save("PDF (*.pdf)|*.pdf", "relatorio-impressao.pdf", p => File.WriteAllBytes(p, _reports.PrintSummary(Request(), _user, InternalPaperFormat.A4)))),
            Button("Agendar", (_, _) => Schedule())
        ]);

        _chart.Paint += PaintDashboardChart;

        Controls.Add(_grid);
        Controls.Add(_chart);
        Controls.Add(_dashboard);
        Controls.Add(top);
        LoadCatalog();
        LoadDashboard();
    }

    private void LoadCatalog()
    {
        _catalog = _reports.Catalog();
        _reportBox.DataSource = _catalog.Select(x => $"{GroupName(x.Group)} - {x.Title}").ToList();
        UiKit.SelectIfAvailable(_reportBox, 0);
    }

    private void Preview()
    {
        var result = _reports.Generate(Request(), _user);
        _grid.DataSource = result.Rows.Select(x => x.Values.ToDictionary()).ToList();
        LoadDashboard();
    }

    private void LoadDashboard()
    {
        var dash = _reports.Dashboard(Request(), _user);
        _lastDashboard = dash;
        _dashboard.Text = $"Vendido: {dash.TotalSales:C} | Recebido: {dash.TotalReceived:C} | A receber: {dash.TotalReceivable:C} | A pagar: {dash.TotalPayable:C}\r\n" +
                          $"Lucro bruto: {dash.GrossProfit:C} | Valor do estoque: {dash.StockValue:C} | Produtos destaque: {string.Join(", ", dash.BestProducts.Take(3).Select(x => x.Name))}";
        _chart.Invalidate();
    }

    private void PaintDashboardChart(object? sender, PaintEventArgs e)
    {
        e.Graphics.Clear(Color.White);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var dash = _lastDashboard;
        if (dash is null)
        {
            return;
        }

        var values = new[]
        {
            ("Vendido", dash.TotalSales, Color.FromArgb(38, 89, 143)),
            ("Recebido", dash.TotalReceived, Color.FromArgb(42, 126, 86)),
            ("A receber", dash.TotalReceivable, Color.FromArgb(218, 124, 38)),
            ("A pagar", dash.TotalPayable, Color.FromArgb(170, 70, 50)),
            ("Estoque", dash.StockValue, Color.FromArgb(90, 84, 154))
        };

        var bounds = _chart.ClientRectangle;
        var left = 16;
        var top = 14;
        var bottom = 34;
        var gap = 12;
        var width = Math.Max(30, (bounds.Width - left * 2 - gap * (values.Length - 1)) / values.Length);
        var max = Math.Max(1m, values.Max(x => Math.Abs(x.Item2)));

        using var axisPen = new Pen(Color.FromArgb(220, 226, 235));
        e.Graphics.DrawLine(axisPen, left, bounds.Height - bottom, bounds.Width - left, bounds.Height - bottom);

        for (var i = 0; i < values.Length; i++)
        {
            var (label, amount, color) = values[i];
            var barHeight = (int)Math.Round((double)(Math.Abs(amount) / max) * Math.Max(18, bounds.Height - top - bottom - 20));
            var x = left + i * (width + gap);
            var y = bounds.Height - bottom - barHeight;
            using var brush = new SolidBrush(color);
            e.Graphics.FillRectangle(brush, x, y, width, barHeight);
            e.Graphics.DrawString(label, Font, Brushes.DimGray, new RectangleF(x, bounds.Height - bottom + 4, width + gap, 18));
            e.Graphics.DrawString(amount.ToString("C0"), Font, Brushes.Black, new RectangleF(x, Math.Max(0, y - 22), width + gap, 20));
        }
    }

    private ReportFilterRequest Request()
    {
        var index = Math.Max(0, _reportBox.SelectedIndex);
        var definition = _catalog.Count == 0 ? _reports.Catalog().First() : _catalog[index];
        return new ReportFilterRequest(definition.Key, _from.Value.ToUniversalTime(), _to.Value.ToUniversalTime(), Term: _term.Text, Status: _term.Text);
    }

    private void Schedule()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var schedule = _reports.Schedule(Request().ReportKey, "Diario", dialog.SelectedPath);
        MessageBox.Show(this, $"Agendamento criado: {schedule.ReportKey}", "Relatorios", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void Save(string filter, string fileName, Action<string> action)
    {
        using var dialog = new SaveFileDialog { Filter = filter, FileName = fileName };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        action(dialog.FileName);
        MessageBox.Show(this, $"Arquivo gerado:\r\n{dialog.FileName}", "Relatorios", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static Button Button(string text, EventHandler click)
    {
        var button = new Button { Text = text, Width = 108, Height = 32, Margin = new Padding(4), FlatStyle = FlatStyle.Flat };
        button.Click += click;
        return button;
    }

    private static string GroupName(ReportGroup group) => group switch
    {
        ReportGroup.Sales => "Vendas",
        ReportGroup.Cash => "Caixa",
        ReportGroup.Stock => "Estoque",
        ReportGroup.Financial => "Financeiro",
        ReportGroup.Customers => "Clientes",
        ReportGroup.Products => "Produtos",
        ReportGroup.Suppliers => "Fornecedores",
        ReportGroup.Returns => "Devolucoes",
        ReportGroup.Cancellations => "Cancelamentos",
        _ => "Sistema"
    };
}
