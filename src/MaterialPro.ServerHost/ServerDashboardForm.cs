using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.Devices;

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
    private readonly Label _adminStatus = new() { Dock = DockStyle.Bottom, Height = 28, ForeColor = Muted, AutoEllipsis = true };
    private readonly TextBox _logBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 10F) };
    private readonly PerformanceTile _memoryTile = new("Memoria do Windows", Green);
    private readonly PerformanceTile _cpuTile = new("CPU do servidor", Blue);
    private readonly PerformanceTile _diskTile = new("Disco do sistema", Orange);
    private readonly PerformanceTile _processTile = new("MaterialPro em RAM", Navy);
    private readonly BackupModuleService _backupService = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 5000 };
    private readonly string _connectionString;
    private TimeSpan _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
    private DateTime _lastCpuAt = DateTime.UtcNow;

    public ServerDashboardForm(MaterialProDbContext db)
    {
        _db = db;
        _connectionString = MaterialProSettingsLoader.Load(AppContext.BaseDirectory).ConnectionString;
        Text = "MaterialPro - Painel do Servidor";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1080, 780);
        Size = new Size(1180, 820);
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
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 6 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 226));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        for (var i = 0; i < 4; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.Controls.Add(MetricCard("Banco MySQL", _databaseValue, Green), 0, 0);
        metrics.Controls.Add(MetricCard("Usuarios", _usersValue, Blue), 1, 0);
        metrics.Controls.Add(MetricCard("Produtos", _productsValue, Orange), 2, 0);
        metrics.Controls.Add(MetricCard("Vendas", _salesValue, Navy), 3, 0);
        root.Controls.Add(metrics, 0, 0);

        root.Controls.Add(BuildPerformancePanel(), 0, 1);
        root.Controls.Add(BuildAdminPanel(), 0, 2);

        var versions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        versions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        versions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        versions.Controls.Add(InfoCard("Cliente instalado", _clientVersion, "Versao publicada no GitHub e pacote de update do cliente.", Blue), 0, 0);
        versions.Controls.Add(InfoCard("Servidor instalado", _serverVersion, "Versao publicada no GitHub e pacote de update do servidor.", Orange), 1, 0);
        root.Controls.Add(versions, 0, 3);

        root.Controls.Add(BuildPathCard(), 0, 4);
        root.Controls.Add(BuildLogCard(), 0, 5);
        return root;
    }

    private Control BuildPerformancePanel()
    {
        var panel = Card(Navy);
        panel.Padding = new Padding(20, 14, 20, 16);
        var header = new Label
        {
            Text = "Desempenho do servidor",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = Ink,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 8, 0, 0) };
        for (var i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        grid.Controls.Add(_memoryTile, 0, 0);
        grid.Controls.Add(_cpuTile, 1, 0);
        grid.Controls.Add(_diskTile, 2, 0);
        grid.Controls.Add(_processTile, 3, 0);

        panel.Controls.Add(grid);
        panel.Controls.Add(header);
        return panel;
    }

    private Control BuildAdminPanel()
    {
        var panel = Card(Orange);
        panel.Padding = new Padding(20, 14, 20, 16);
        var header = new Label
        {
            Text = "Administracao rapida",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Ink,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };

        var subtitle = new Label
        {
            Text = "Operacoes protegidas: update forcado, backup geral e restauracao do sistema.",
            Dock = DockStyle.Top,
            Height = 26,
            ForeColor = Muted
        };

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 8, 0, 0) };
        for (var i = 0; i < 4; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        actions.Controls.Add(ActionButton("Forcar update servidor", "Baixa do GitHub e aplica no servidor", Orange, (_, _) => ForceUpdate("server")), 0, 0);
        actions.Controls.Add(ActionButton("Forcar update cliente", "Baixa o pacote do computador cliente", Blue, (_, _) => ForceUpdate("client")), 1, 0);
        actions.Controls.Add(ActionButton("Backup geral", "Cria ZIP com arquivos e banco MySQL", Green, async (_, _) => await CreateFullBackup()), 2, 0);
        actions.Controls.Add(ActionButton("Restaurar backup", "Escolhe ZIP e recupera banco/arquivos", Brick, async (_, _) => await RestoreBackup()), 3, 0);

        panel.Controls.Add(actions);
        panel.Controls.Add(_adminStatus);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(header);
        return panel;
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
        _adminStatus.Text = IsAdministrator()
            ? "Painel aberto como Administrador. Acoes sensiveis liberadas."
            : "Abra como Administrador para executar update forcado, backup completo e restauracao.";
        _adminStatus.ForeColor = IsAdministrator() ? Green : Brick;

        var diagnostics = ReadDiagnostics();
        _logBox.Text = diagnostics.Length > 0
            ? diagnostics
            : $"Servidor iniciado em modo painel.{Environment.NewLine}Ultima verificacao: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

        RefreshPerformance();
    }

    private void ForceUpdate(string channel)
    {
        if (!EnsureAdministrator("forcar atualizacao"))
        {
            return;
        }

        var label = channel.Equals("server", StringComparison.OrdinalIgnoreCase) ? "servidor" : "cliente";
        if (MessageBox.Show(this, $"Forcar update do {label} agora?", "Atualizacao forcada", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        var started = AutoUpdateRunner.StartForcedUpdate(channel);
        _logBox.Text = started
            ? $"Update forcado do {label} iniciado como administrador. Aguarde o processo terminar."
            : $"Nao foi possivel iniciar update forcado do {label}. Verifique se o updater existe.";
    }

    private async Task CreateFullBackup()
    {
        if (!EnsureAdministrator("fazer backup geral"))
        {
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "Escolha onde salvar o backup geral do MaterialPro",
            UseDescriptionForTitle = true,
            SelectedPath = _backupService.DefaultDestinationFolder()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _logBox.Text = "Gerando backup geral. Aguarde...";
        UseWaitCursor = true;
        try
        {
            var request = new BackupRequest(AppContext.BaseDirectory, dialog.SelectedPath, _connectionString, IncludeFiles: true, IncludeDatabase: true);
            var result = await Task.Run(() => _backupService.CreateBackup(request));
            _logBox.Text = $"{result.Message}\r\n\r\nArquivo:\r\n{result.BackupPath}\r\n\r\nArquivos: {YesNo(result.FilesIncluded)}\r\nBanco MySQL: {YesNo(result.DatabaseIncluded)}\r\nRelatorio: {result.LogPath}";
            MessageBox.Show(this, result.Message, "Backup geral", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            if (File.Exists(result.BackupPath))
            {
                OpenFolder(Path.GetDirectoryName(result.BackupPath)!);
            }
        }
        catch (Exception ex)
        {
            _logBox.Text = $"Falha ao gerar backup geral: {ex.Message}";
            MessageBox.Show(this, "Nao foi possivel gerar o backup.", "Backup geral", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task RestoreBackup()
    {
        if (!EnsureAdministrator("restaurar backup"))
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Escolha o backup ZIP do MaterialPro",
            Filter = "Backup MaterialPro (*.zip)|*.zip|Todos os arquivos (*.*)|*.*",
            InitialDirectory = Directory.Exists(_backupService.DefaultDestinationFolder()) ? _backupService.DefaultDestinationFolder() : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            "Restaurar backup pode substituir dados do banco e arquivos do servidor. Confirma restaurar agora?",
            "Restaurar backup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _logBox.Text = "Restaurando backup. Aguarde...";
        UseWaitCursor = true;
        try
        {
            var request = new RestoreBackupRequest(dialog.FileName, AppContext.BaseDirectory, _connectionString, RestoreFiles: true, RestoreDatabase: true);
            var result = await Task.Run(() => _backupService.RestoreBackup(request));
            _logBox.Text = $"{result.Message}\r\n\r\nArquivos restaurados: {YesNo(result.FilesRestored)}\r\nBanco restaurado: {YesNo(result.DatabaseRestored)}\r\n\r\n{result.Log}";
            MessageBox.Show(this, result.Message, "Restaurar backup", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            RefreshStatus();
        }
        catch (Exception ex)
        {
            _logBox.Text = $"Falha ao restaurar backup: {ex.Message}";
            MessageBox.Show(this, "Nao foi possivel restaurar o backup.", "Restaurar backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private bool EnsureAdministrator(string action)
    {
        if (IsAdministrator())
        {
            return true;
        }

        var answer = MessageBox.Show(this, $"Para {action}, abra o painel como Administrador. Deseja reabrir agora?", "Permissao de administrador", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
        {
            return false;
        }

        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory });
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Nao foi possivel reabrir como Administrador: {ex.Message}", "Permissao de administrador", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return false;
    }

    private void RefreshPerformance()
    {
        var computer = new ComputerInfo();
        var totalMemory = Math.Max(1, computer.TotalPhysicalMemory);
        var availableMemory = computer.AvailablePhysicalMemory;
        var usedMemory = totalMemory - availableMemory;
        var memoryPercent = (double)usedMemory / totalMemory * 100;
        _memoryTile.Update(memoryPercent, $"{FormatBytes(usedMemory)} / {FormatBytes(totalMemory)}");

        var process = Process.GetCurrentProcess();
        var now = DateTime.UtcNow;
        var cpuTime = process.TotalProcessorTime;
        var elapsedMs = Math.Max(1, (now - _lastCpuAt).TotalMilliseconds);
        var cpuDeltaMs = (cpuTime - _lastCpuTime).TotalMilliseconds;
        var cpuPercent = Math.Clamp(cpuDeltaMs / (elapsedMs * Environment.ProcessorCount) * 100, 0, 100);
        _lastCpuAt = now;
        _lastCpuTime = cpuTime;
        _cpuTile.Update(cpuPercent, $"{cpuPercent:0}% do processo");

        var driveRoot = Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:\\";
        var drive = new DriveInfo(driveRoot);
        var diskPercent = drive.IsReady && drive.TotalSize > 0
            ? (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100
            : 0;
        _diskTile.Update(diskPercent, $"{FormatBytes(drive.TotalSize - drive.AvailableFreeSpace)} / {FormatBytes(drive.TotalSize)}");

        var processMb = process.WorkingSet64 / 1024d / 1024d;
        var processPercent = Math.Clamp(process.WorkingSet64 / (double)totalMemory * 100, 0, 100);
        _processTile.Update(processPercent, $"{processMb:0} MB usados");
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

    private static Button ActionButton(string title, string subtitle, Color color, EventHandler click)
    {
        var button = new Button
        {
            Text = $"{title}\r\n{subtitle}",
            Dock = DockStyle.Fill,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Padding = new Padding(14, 6, 10, 6),
            Margin = new Padding(0, 0, 12, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string YesNo(bool value) => value ? "Sim" : "Nao";

    private static void OpenFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
    }

    private static string FormatBytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private sealed class PerformanceTile : Panel
    {
        private readonly Color _accent;
        private readonly List<double> _history = [];
        private double _value;
        private string _detail = string.Empty;

        public PerformanceTile(string title, Color accent)
        {
            _accent = accent;
            Title = title;
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Margin = new Padding(0, 0, 12, 0);
            DoubleBuffered = true;
            MinimumSize = new Size(160, 150);
        }

        public string Title { get; }

        public void Update(double value, string detail)
        {
            _value = Math.Clamp(value, 0, 100);
            _detail = detail;
            _history.Add(_value);
            if (_history.Count > 36)
            {
                _history.RemoveAt(0);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            using var border = new Pen(Color.FromArgb(214, 222, 232));
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

            using var titleFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            using var valueFont = new Font("Segoe UI", 18F, FontStyle.Bold);
            using var detailFont = new Font("Segoe UI", 8.5F);
            using var ink = new SolidBrush(Ink);
            using var muted = new SolidBrush(Muted);
            using var accent = new SolidBrush(_accent);

            g.DrawString(Title, titleFont, ink, new PointF(14, 12));
            g.DrawString($"{_value:0}%", valueFont, accent, new PointF(14, 38));
            g.DrawString(_detail, detailFont, muted, new RectangleF(14, 78, Math.Max(90, Width - 120), 34));

            var ringSize = Math.Min(58, Math.Max(42, Width / 5));
            var ring = new Rectangle(Width - ringSize - 18, 30, ringSize, ringSize);
            using var ringTrack = new Pen(Color.FromArgb(231, 236, 243), 8F) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            using var ringPen = new Pen(_accent, 8F) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawArc(ringTrack, ring, -90, 360);
            g.DrawArc(ringPen, ring, -90, (float)(_value / 100d * 360d));

            using var centerFont = new Font("Segoe UI", 8F, FontStyle.Bold);
            var centerText = _value >= 75 ? "ALTO" : _value >= 45 ? "MEDIO" : "OK";
            var centerSize = g.MeasureString(centerText, centerFont);
            g.DrawString(centerText, centerFont, accent, ring.Left + (ring.Width - centerSize.Width) / 2, ring.Top + (ring.Height - centerSize.Height) / 2);

            var chart = new Rectangle(14, Height - 54, Width - 28, 36);
            using var gridPen = new Pen(Color.FromArgb(235, 239, 244));
            g.DrawLine(gridPen, chart.Left, chart.Top + chart.Height / 2, chart.Right, chart.Top + chart.Height / 2);
            g.DrawRectangle(gridPen, chart);

            if (_history.Count < 2)
            {
                return;
            }

            var points = _history.Select((sample, index) =>
            {
                var x = chart.Left + index * (chart.Width / Math.Max(1f, _history.Count - 1));
                var y = chart.Bottom - (float)(sample / 100d * chart.Height);
                return new PointF(x, y);
            }).ToArray();

            using var linePen = new Pen(_accent, 2.2F);
            g.DrawLines(linePen, points);
        }
    }
}
