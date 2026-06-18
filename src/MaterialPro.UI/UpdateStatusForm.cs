using System.Diagnostics;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MaterialPro.UI;

public sealed class UpdateStatusForm : Form
{
    private readonly MaterialProDbContext _db;
    private readonly AppUser? _user;
    private readonly FlowLayoutPanel _cards = new() { Dock = DockStyle.Fill, Padding = new Padding(18), AutoScroll = true, WrapContents = true };
    private readonly Label _health = new() { Dock = DockStyle.Top, Height = 94, Padding = new Padding(18, 10, 18, 10), BackColor = Color.White, ForeColor = UiKit.Muted };

    public UpdateStatusForm(MaterialProDbContext db, AppUser? user)
    {
        _db = db;
        _user = user;

        Text = "MaterialPro - Central de atualizacoes";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1120, 700);
        BackColor = UiKit.Surface;
        Font = new Font("Segoe UI", 10F);

        var header = UiKit.Header("Central de atualizacoes", "Confira versao, conexao com servidor, banco de dados e aplique updates com seguranca.");
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(18, 8, 18, 8), BackColor = Color.White };
        bar.Controls.Add(UiKit.Button("Atualizar status", UiKit.Blue, (_, _) => LoadStatus()));
        bar.Controls.Add(UiKit.Button("Abrir pasta atual", UiKit.Green, (_, _) => OpenFolder(AppContext.BaseDirectory)));

        Controls.Add(_cards);
        Controls.Add(_health);
        Controls.Add(bar);
        Controls.Add(header);
        _cards.Resize += (_, _) => ResizeCards();
        LoadStatus();
    }

    private void LoadStatus()
    {
        var dbHealth = CheckDatabase();
        _health.Text = $"Servidor/Banco: {dbHealth.Message}\r\nConexao: {dbHealth.ConnectionStringSummary}\r\nSituacao: {(dbHealth.CanConnect ? "Cliente conectado ao servidor e banco de dados." : "Cliente sem conexao confirmada.")}";

        _cards.Controls.Clear();
        _cards.Controls.Add(Card(AutoUpdateRunner.GetStatus("client"), "Cliente", dbHealth, allowForce: IsAdmin()));
        _cards.Controls.Add(Card(AutoUpdateRunner.GetStatus("server"), "Servidor", dbHealth, allowForce: IsAdmin()));
        ResizeCards();
    }

    private Control Card(UpdateStatusSnapshot status, string title, DatabaseHealth dbHealth, bool allowForce)
    {
        var upToDate = status.HasUpdater && status.RemoteVersion != "indisponivel" && string.Equals(status.LocalVersion, status.RemoteVersion, StringComparison.OrdinalIgnoreCase);
        var installed = Directory.Exists(status.InstallPath);
        var panel = new TableLayoutPanel
        {
            Width = 500,
            Height = 430,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 16, 16),
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 6
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 46, WrapContents = true };
        buttons.Controls.Add(UiKit.Button("Abrir pasta", UiKit.Blue, (_, _) => OpenFolder(status.InstallPath)));
        var force = UiKit.Button("Forcar instalar/update", allowForce ? UiKit.Orange : UiKit.Concrete, (_, _) => ForceUpdate(status));
        force.Width = 190;
        force.Enabled = allowForce;
        buttons.Controls.Add(force);

        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"{title}\r\nInstalada: {status.LocalVersion}\r\nPublicada no GitHub: {status.RemoteVersion}",
            ForeColor = UiKit.Ink,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold)
        }, 0, 0);
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = title.Equals("Cliente", StringComparison.OrdinalIgnoreCase)
                ? dbHealth.CanConnect ? "Rede: cliente conectado ao servidor e banco de dados." : "Rede: cliente sem conexao confirmada com o banco."
                : dbHealth.CanConnect ? "Servidor: banco MySQL respondendo para este cliente." : "Servidor: sem resposta confirmada do banco.",
            ForeColor = dbHealth.CanConnect ? UiKit.Green : UiKit.Brick,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        }, 0, 1);
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = StatusMessage(status, upToDate),
            ForeColor = upToDate ? UiKit.Green : UiKit.Orange,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        }, 0, 2);
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"Instalado no local certo: {(installed ? "Sim" : "Nao localizado")}\r\nLocal: {status.InstallPath}\r\nUpdater .exe: {(status.HasUpdater ? "Encontrado" : "Nao encontrado")}\r\nPacote local: {(status.HasLocalPackage ? "Sim" : "Nao, sera baixado do GitHub")}\r\nAdmin: pode forcar reinstalacao por cima da pasta correta.",
            ForeColor = UiKit.Muted
        }, 0, 3);
        panel.Controls.Add(buttons, 0, 4);

        var paintPanel = new Panel { Dock = DockStyle.Left, Width = 7, BackColor = upToDate ? UiKit.Green : UiKit.Orange };
        panel.Controls.Add(paintPanel, 0, 5);
        paintPanel.BringToFront();

        panel.Paint += (_, e) =>
            {
            using var brush = new SolidBrush(upToDate ? UiKit.Green : UiKit.Orange);
            e.Graphics.FillRectangle(brush, 0, 0, 7, panel.Height);
        };
        return panel;
    }

    private void ResizeCards()
    {
        if (_cards.ClientSize.Width <= 0)
        {
            return;
        }

        var columns = _cards.ClientSize.Width >= 1120 ? 2 : 1;
        var cardWidth = Math.Max(520, (_cards.ClientSize.Width - 54) / columns);
        foreach (Control control in _cards.Controls)
        {
            control.Width = cardWidth;
        }
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
                canConnect ? $"Conectado ao MySQL. Usuarios cadastrados: {users}. Versao MySQL: {serverVersion}" : "Nao conectado ao banco.",
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
            return "Updater nao encontrado. Reinstale pelo setup mais recente.";
        }

        return upToDate
            ? "Versao correta. Instalacao atualizada."
            : status.Message;
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
