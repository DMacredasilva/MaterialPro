using System.Diagnostics;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MaterialPro.UI;

public sealed class UpdateStatusForm : Form
{
    private readonly MaterialProDbContext _db;
    private readonly AppUser? _user;
    private readonly Label _healthTitle = new() { Dock = DockStyle.Top, Height = 30, ForeColor = UiKit.Ink, Font = new Font("Segoe UI", 13F, FontStyle.Bold) };
    private readonly Label _healthText = new() { Dock = DockStyle.Fill, ForeColor = UiKit.Muted };
    private readonly TableLayoutPanel _cards = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(18, 0, 18, 0) };
    private readonly Label _footer = new() { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(18, 0, 18, 10), ForeColor = UiKit.Muted };

    public UpdateStatusForm(MaterialProDbContext db, AppUser? user)
    {
        _db = db;
        _user = user;

        Text = "MaterialPro - Central de atualizacoes";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 620);
        Size = new Size(1120, 720);
        BackColor = UiKit.Surface;
        Font = new Font("Segoe UI", 10F);

        _cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Controls.Add(_cards);
        Controls.Add(BuildHealthPanel());
        Controls.Add(BuildActionBar());
        Controls.Add(BuildHeader());
        Controls.Add(_footer);
        LoadStatus();
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 108, Padding = new Padding(24, 18, 24, 10), BackColor = Color.White };
        panel.Controls.Add(new Label
        {
            Text = "Central de atualizacoes",
            Dock = DockStyle.Top,
            Height = 42,
            ForeColor = UiKit.Ink,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold)
        });
        panel.Controls.Add(new Label
        {
            Text = "Veja se cliente e servidor estao atualizados e aplique update com seguranca.",
            Dock = DockStyle.Bottom,
            Height = 28,
            ForeColor = UiKit.Muted
        });
        return panel;
    }

    private Control BuildActionBar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 62, Padding = new Padding(24, 10, 24, 10), BackColor = Color.White, WrapContents = false };
        bar.Controls.Add(UiKit.Button("Atualizar status", UiKit.Blue, (_, _) => LoadStatus()));
        bar.Controls.Add(WideButton("Abrir pasta atual", UiKit.Green, (_, _) => OpenFolder(AppContext.BaseDirectory), 170));
        return bar;
    }

    private Control BuildHealthPanel()
    {
        var panel = Card(UiKit.Green);
        panel.Dock = DockStyle.Top;
        panel.Height = 104;
        panel.Margin = Padding.Empty;
        panel.Padding = new Padding(24, 12, 24, 12);
        panel.Controls.Add(_healthText);
        panel.Controls.Add(_healthTitle);
        return panel;
    }

    private void LoadStatus()
    {
        var dbHealth = CheckDatabase();
        _healthTitle.Text = dbHealth.CanConnect ? "Servidor e banco conectados" : "Atencao: sem conexao confirmada";
        _healthTitle.ForeColor = dbHealth.CanConnect ? UiKit.Green : UiKit.Brick;
        _healthText.Text = $"{dbHealth.Message}{Environment.NewLine}{dbHealth.ConnectionStringSummary}";
        _footer.Text = $"Ultima verificacao: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

        _cards.Controls.Clear();
        _cards.Controls.Add(UpdateCard(AutoUpdateRunner.GetStatus("client"), "Cliente", dbHealth, IsAdmin()), 0, 0);
        _cards.Controls.Add(UpdateCard(AutoUpdateRunner.GetStatus("server"), "Servidor", dbHealth, IsAdmin()), 1, 0);
    }

    private Control UpdateCard(UpdateStatusSnapshot status, string title, DatabaseHealth dbHealth, bool allowForce)
    {
        var upToDate = status.HasUpdater
            && status.RemoteVersion != "indisponivel"
            && string.Equals(status.LocalVersion, status.RemoteVersion, StringComparison.OrdinalIgnoreCase);
        var installed = Directory.Exists(status.InstallPath);
        var accent = upToDate ? UiKit.Green : UiKit.Orange;

        var panel = Card(accent);
        panel.Padding = new Padding(24, 20, 24, 20);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        layout.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = UiKit.Ink,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        layout.Controls.Add(BuildVersionRow(status), 0, 1);
        layout.Controls.Add(BuildStatusPill(upToDate, status), 0, 2);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = UiKit.Muted,
            Text = DetailsText(status, installed, dbHealth, title),
            AutoEllipsis = true
        }, 0, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        buttons.Controls.Add(WideButton("Abrir pasta", UiKit.Blue, (_, _) => OpenFolder(status.InstallPath), 140));
        var force = WideButton("Forcar update", allowForce ? UiKit.Orange : UiKit.Concrete, (_, _) => ForceUpdate(status), 150);
        force.Enabled = allowForce;
        buttons.Controls.Add(force);
        layout.Controls.Add(buttons, 0, 4);

        layout.Controls.Add(new Label
        {
            Text = status.HasUpdater ? "Updater encontrado" : "Updater nao encontrado",
            Dock = DockStyle.Fill,
            ForeColor = status.HasUpdater ? UiKit.Green : UiKit.Brick,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        }, 0, 5);

        panel.Controls.Add(layout);
        return panel;
    }

    private static Control BuildVersionRow(UpdateStatusSnapshot status)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.Controls.Add(VersionBox("Instalada", status.LocalVersion), 0, 0);
        row.Controls.Add(VersionBox("GitHub", status.RemoteVersion), 1, 0);
        return row;
    }

    private static Control VersionBox(string label, string value)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };
        panel.Controls.Add(new Label { Text = value, Dock = DockStyle.Fill, ForeColor = UiKit.Ink, Font = new Font("Segoe UI", 13F, FontStyle.Bold), AutoEllipsis = true });
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 22, ForeColor = UiKit.Muted });
        return panel;
    }

    private static Control BuildStatusPill(bool upToDate, UpdateStatusSnapshot status)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 10) };
        var label = new Label
        {
            Text = upToDate ? "Atualizado" : StatusMessage(status, upToDate),
            Dock = DockStyle.Left,
            Width = 360,
            BackColor = upToDate ? Color.FromArgb(228, 245, 235) : Color.FromArgb(255, 241, 222),
            ForeColor = upToDate ? UiKit.Green : UiKit.Orange,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        };
        panel.Controls.Add(label);
        return panel;
    }

    private static string DetailsText(UpdateStatusSnapshot status, bool installed, DatabaseHealth dbHealth, string title)
    {
        var rede = title.Equals("Cliente", StringComparison.OrdinalIgnoreCase)
            ? dbHealth.CanConnect ? "Cliente conectado ao servidor e banco." : "Cliente sem conexao confirmada com o banco."
            : dbHealth.CanConnect ? "Servidor com MySQL respondendo." : "Servidor sem resposta confirmada do banco.";

        return $"""
{rede}

Pasta: {status.InstallPath}
Instalado no local certo: {(installed ? "Sim" : "Nao localizado")}
Pacote local: {(status.HasLocalPackage ? "Sim" : "Nao, baixa do GitHub")}
Permissao: {(status.HasUpdater ? "Pode atualizar" : "Reinstale pelo setup mais recente")}
""";
    }

    private void ForceUpdate(UpdateStatusSnapshot status)
    {
        if (!IsAdmin())
        {
            MessageBox.Show(this, "Somente Administrador pode forcar atualizacao.", "Atualizacoes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var target = status.Channel.Equals("server", StringComparison.OrdinalIgnoreCase) ? "servidor" : "cliente";
        var confirm = MessageBox.Show(
            this,
            $"Forcar atualizacao do {target} agora?\r\n\r\nO MaterialPro pode fechar durante o processo.",
            "Confirmar atualizacao",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        var started = AutoUpdateRunner.StartForcedUpdate(status.Channel);
        MessageBox.Show(
            this,
            started
                ? status.Channel.Equals("client", StringComparison.OrdinalIgnoreCase)
                    ? "Atualizador do cliente iniciado. O MaterialPro vai fechar para concluir a instalacao e abrir novamente."
                    : "Atualizador iniciado. Aguarde a conclusao."
                : "Nao foi possivel iniciar o atualizador. Verifique se o updater existe na instalacao.",
            "Atualizacoes",
            MessageBoxButtons.OK,
            started ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (started && status.Channel.Equals("client", StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.Forms.Application.Exit();
        }
    }

    private DatabaseHealth CheckDatabase()
    {
        try
        {
            var canConnect = _db.Database.CanConnect();
            var users = canConnect ? _db.Users.Count() : 0;
            var serverVersion = canConnect
                ? _db.Database.SqlQueryRaw<string>("SELECT VERSION() AS `Value`").AsEnumerable().FirstOrDefault() ?? "desconhecida"
                : "indisponivel";

            return new DatabaseHealth(
                canConnect,
                canConnect ? $"MySQL conectado. Usuarios: {users}. Versao MySQL: {serverVersion}" : "Nao conectado ao banco.",
                SummarizeConnection());
        }
        catch (Exception ex)
        {
            return new DatabaseHealth(false, $"Falha ao testar banco: {ex.Message}", SummarizeConnection());
        }
    }

    private string SummarizeConnection()
    {
        try
        {
            var connection = _db.Database.GetDbConnection();
            return $"Servidor: {connection.DataSource} | Banco: {connection.Database}";
        }
        catch
        {
            return "Nao foi possivel ler a conexao.";
        }
    }

    private bool IsAdmin() => _user?.Role == UserRole.Admin;

    private static string StatusMessage(UpdateStatusSnapshot status, bool upToDate)
    {
        if (!status.HasUpdater)
        {
            return "Updater nao encontrado";
        }

        return upToDate ? "Atualizado" : "Atualizacao disponivel";
    }

    private static Panel Card(Color accent)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 18, 14, 18) };
        panel.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(accent);
            e.Graphics.FillRectangle(brush, 0, 0, 8, panel.Height);
            using var pen = new Pen(Color.FromArgb(214, 222, 232));
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        return panel;
    }

    private static Button WideButton(string text, Color color, EventHandler click, int width)
    {
        var button = UiKit.Button(text, color, click);
        button.Width = width;
        button.Margin = new Padding(0, 0, 10, 0);
        return button;
    }

    private static void OpenFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
    }

    private sealed record DatabaseHealth(bool CanConnect, string Message, string ConnectionStringSummary);
}
