using System.Diagnostics;
using System.Text.Json;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MaterialPro.ServerHost;

public sealed class ServerDashboardForm : Form
{
    private static readonly Color Ink = Color.FromArgb(25, 39, 52);
    private static readonly Color Muted = Color.FromArgb(91, 105, 122);
    private static readonly Color Surface = Color.FromArgb(242, 245, 248);
    private static readonly Color Navy = Color.FromArgb(24, 52, 82);
    private static readonly Color Blue = Color.FromArgb(38, 89, 143);
    private static readonly Color Green = Color.FromArgb(45, 126, 86);
    private static readonly Color Orange = Color.FromArgb(218, 124, 38);
    private static readonly Color Brick = Color.FromArgb(165, 74, 52);

    private readonly MaterialProDbContext _db;
    private readonly Label _databaseValue = ValueLabel();
    private readonly Label _usersValue = ValueLabel();
    private readonly Label _productsValue = ValueLabel();
    private readonly Label _salesValue = ValueLabel();
    private readonly Label _clientVersion = ValueLabel(13);
    private readonly Label _serverVersion = ValueLabel(13);
    private readonly Label _installPath = new() { Dock = DockStyle.Fill, ForeColor = Muted, AutoEllipsis = true };
    private readonly TextBox _logBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 10F) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 5000 };

    public ServerDashboardForm(MaterialProDbContext db)
    {
        _db = db;
        Text = "MaterialPro - Painel do Servidor";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 680);
        BackColor = Surface;
        Font = new Font("Segoe UI", 10F);

        Controls.Add(BuildContent());
        Controls.Add(BuildHeader());

        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();
        RefreshStatus();
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 118, Padding = new Padding(24, 18, 24, 12), BackColor = Color.White };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 480, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        actions.Controls.Add(Button("Fechar painel", Brick, (_, _) => Close(), 130));
        actions.Controls.Add(Button("Atualizar", Blue, (_, _) => RefreshStatus(), 120));
        actions.Controls.Add(Button("Abrir pasta", Green, (_, _) => OpenFolder(AppContext.BaseDirectory), 120));
        panel.Controls.Add(actions);

        panel.Controls.Add(new Label
        {
            Text = "Painel do Servidor",
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = Ink
        });
        panel.Controls.Add(new Label
        {
            Text = "Acompanhe banco de dados, atualizacao, instalacao e diagnosticos do MaterialPro.",
            Dock = DockStyle.Bottom,
            Height = 28,
            ForeColor = Muted
        });
        return panel;
    }

    private Control BuildContent()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        for (var i = 0; i < 4; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.Controls.Add(MetricCard("Banco MySQL", _databaseValue, Green), 0, 0);
        metrics.Controls.Add(MetricCard("Usuarios", _usersValue, Blue), 1, 0);
        metrics.Controls.Add(MetricCard("Produtos", _productsValue, Orange), 2, 0);
        metrics.Controls.Add(MetricCard("Vendas", _salesValue, Navy), 3, 0);
        root.Controls.Add(metrics, 0, 0);

        var versions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        versions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        versions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        versions.Controls.Add(InfoCard("Cliente instalado", _clientVersion, "Versao publicada no GitHub e pacote de update do cliente.", Blue), 0, 0);
        versions.Controls.Add(InfoCard("Servidor instalado", _serverVersion, "Versao publicada no GitHub e pacote de update do servidor.", Orange), 1, 0);
        root.Controls.Add(versions, 0, 1);

        root.Controls.Add(BuildPathCard(), 0, 2);
        root.Controls.Add(BuildLogCard(), 0, 3);
        return root;
    }

    private Control BuildPathCard()
    {
        var panel = Card(Blue);
        panel.Padding = new Padding(20, 14, 20, 14);
        panel.Controls.Add(_installPath);
        panel.Controls.Add(new Label { Text = "Pasta do servidor", Dock = DockStyle.Top, Height = 28, ForeColor = Ink, Font = new Font("Segoe UI", 12F, FontStyle.Bold) });
        return panel;
    }

    private Control BuildLogCard()
    {
        var panel = Card(Navy);
        panel.Padding = new Padding(20, 14, 20, 18);
        panel.Controls.Add(_logBox);
        panel.Controls.Add(new Label { Text = "Diagnostico recente", Dock = DockStyle.Top, Height = 32, ForeColor = Ink, Font = new Font("Segoe UI", 12F, FontStyle.Bold) });
        return panel;
    }

    private void RefreshStatus()
    {
        try
        {
            var canConnect = _db.Database.CanConnect();
            _databaseValue.Text = canConnect ? "Conectado" : "Offline";
            _databaseValue.ForeColor = canConnect ? Green : Brick;
            _usersValue.Text = canConnect ? SafeCount(() => _db.Users.Count()).ToString() : "-";
            _productsValue.Text = canConnect ? SafeCount(() => _db.Products.Count()).ToString() : "-";
            _salesValue.Text = canConnect ? SafeCount(() => _db.Sales.Count()).ToString() : "-";
        }
        catch (Exception ex)
        {
            _databaseValue.Text = "Erro";
            _databaseValue.ForeColor = Brick;
            _logBox.Text = $"Falha ao consultar banco:{Environment.NewLine}{ex.Message}";
        }

        var client = AutoUpdateRunner.GetStatus("client");
        var server = AutoUpdateRunner.GetStatus("server");
        _clientVersion.Text = $"{client.LocalVersion} -> GitHub {client.RemoteVersion}";
        _serverVersion.Text = $"{server.LocalVersion} -> GitHub {server.RemoteVersion}";
        _installPath.Text = AppContext.BaseDirectory;

        var diagnostics = ReadDiagnostics();
        _logBox.Text = diagnostics.Length > 0
            ? diagnostics
            : $"Servidor iniciado em modo painel.{Environment.NewLine}Ultima verificacao: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
    }

    private static int SafeCount(Func<int> count)
    {
        try
        {
            return count();
        }
        catch
        {
            return 0;
        }
    }

    private static string ReadDiagnostics()
    {
        var diagnosticsPath = Path.Combine(AppContext.BaseDirectory, "diagnostics", "server-status.json");
        if (!File.Exists(diagnosticsPath))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(diagnosticsPath));
            var root = document.RootElement;
            var generatedAt = root.GetProperty("generatedAt").GetDateTimeOffset().ToLocalTime();
            var canConnect = root.GetProperty("canConnect").GetBoolean() ? "Conectado" : "Offline";
            var error = root.TryGetProperty("error", out var errorProperty) ? errorProperty.GetString() : string.Empty;
            return $"""
Ultimo diagnostico: {generatedAt:dd/MM/yyyy HH:mm:ss}
Banco: {canConnect}
Usuarios: {root.GetProperty("users").GetInt32()}
Produtos: {root.GetProperty("products").GetInt32()}
Vendas: {root.GetProperty("sales").GetInt32()}
Erro: {error}
""";
        }
        catch (Exception ex)
        {
            return $"Nao foi possivel ler diagnostico: {ex.Message}";
        }
    }

    private static Panel MetricCard(string title, Label value, Color accent)
    {
        var panel = Card(accent);
        panel.Padding = new Padding(18, 12, 18, 12);
        panel.Controls.Add(value);
        panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 28, ForeColor = Muted });
        return panel;
    }

    private static Panel InfoCard(string title, Label value, string subtitle, Color accent)
    {
        var panel = Card(accent);
        panel.Padding = new Padding(18, 12, 18, 12);
        panel.Controls.Add(new Label { Text = subtitle, Dock = DockStyle.Bottom, Height = 44, ForeColor = Muted });
        panel.Controls.Add(value);
        panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 28, ForeColor = Muted });
        return panel;
    }

    private static Panel Card(Color accent)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 0, 14, 14) };
        panel.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(accent);
            e.Graphics.FillRectangle(brush, 0, 0, 7, panel.Height);
            using var pen = new Pen(Color.FromArgb(214, 222, 232));
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        return panel;
    }

    private static Label ValueLabel(float size = 20) => new()
    {
        Dock = DockStyle.Fill,
        ForeColor = Ink,
        Font = new Font("Segoe UI", size, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true
    };

    private static Button Button(string text, Color color, EventHandler click, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 38,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(8, 0, 0, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private static void OpenFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
    }
}
