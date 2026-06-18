using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace MaterialPro.UI;

public sealed class RemoteAccessForm : Form
{
    private readonly Label _sessionId = ValueLabel();
    private readonly Label _temporaryPassword = ValueLabel();
    private readonly Label _status = new() { Dock = DockStyle.Top, Height = 46, Padding = new Padding(12), ForeColor = UiKit.Muted };
    private readonly TextBox _serverBox = new() { Width = 180, PlaceholderText = "IP do servidor" };
    private readonly TextBox _message = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
    private readonly ComboBox _accessLevel = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _toolBox = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _clientCodeBox = new() { Width = 220, PlaceholderText = "ID/codigo do cliente" };
    private readonly TextBox _clientPasswordBox = new() { Width = 150, PlaceholderText = "Senha do cliente" };
    private readonly TextBox _clientNameBox = new() { Width = 220, PlaceholderText = "Nome do cliente/loja" };
    private readonly ComboBox _adminAccessLevel = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _allowControl = new() { Text = "Permitir controle do mouse e teclado", Checked = true, AutoSize = true };
    private readonly CheckBox _allowAdmin = new() { Text = "Permitir suporte administrativo", AutoSize = true };
    private readonly CheckBox _serverSupport = new() { Text = "Este computador e o servidor", AutoSize = true };

    public RemoteAccessForm()
    {
        Text = "MaterialPro - Suporte remoto assistido";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1120, 720);
        BackColor = UiKit.Surface;
        Font = new Font("Segoe UI", 10F);

        ConfigureCombos();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(18) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var left = BuildAssistantPanel();
        var right = BuildMessagePanel();
        root.Controls.Add(left, 0, 0);
        root.Controls.Add(right, 1, 0);

        Controls.Add(root);
        Controls.Add(_status);
        Controls.Add(UiKit.Header("Suporte remoto assistido", "Tela simples para o cliente liberar acesso ao suporte, no estilo TeamViewer/AnyDesk."));

        NewSession();
    }

    private void ConfigureCombos()
    {
        _accessLevel.DisplayMember = nameof(Option.Name);
        _accessLevel.ValueMember = nameof(Option.Code);
        _accessLevel.Items.AddRange(
        [
            new Option("visualizacao", "Somente visualizacao"),
            new Option("controle", "Controle com autorizacao"),
            new Option("administrativo", "Administrador do sistema"),
            new Option("servidor", "Servidor e banco de dados")
        ]);
        UiKit.SelectIfAvailable(_accessLevel, 1);
        _accessLevel.SelectedIndexChanged += (_, _) => ApplyAccessLevel();

        _toolBox.DisplayMember = nameof(Option.Name);
        _toolBox.ValueMember = nameof(Option.Code);
        _toolBox.Items.AddRange(
        [
            new Option("quickassist", "Assistencia Rapida do Windows"),
            new Option("anydesk", "AnyDesk instalado"),
            new Option("teamviewer", "TeamViewer instalado"),
            new Option("rdp", "Area de Trabalho Remota")
        ]);
        UiKit.SelectIfAvailable(_toolBox, 0);

        _adminAccessLevel.DisplayMember = nameof(Option.Name);
        _adminAccessLevel.ValueMember = nameof(Option.Code);
        _adminAccessLevel.Items.AddRange(
        [
            new Option("visualizacao", "Somente visualizar"),
            new Option("controle", "Controlar com permissao"),
            new Option("administrativo", "Acesso administrativo"),
            new Option("servidor", "Servidor e banco")
        ]);
        UiKit.SelectIfAvailable(_adminAccessLevel, 1);
    }

    private Control BuildAssistantPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18) };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 156, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        actions.Controls.Add(WideButton("Iniciar atendimento", UiKit.Green, (_, _) => StartSelectedTool()));
        actions.Controls.Add(WideButton("Copiar ID e senha", UiKit.Blue, (_, _) => Clipboard.SetText($"ID: {_sessionId.Text} | Senha: {_temporaryPassword.Text}")));
        actions.Controls.Add(WideButton("Gerar nova senha", UiKit.Orange, (_, _) => NewSession()));
        actions.Controls.Add(WideButton("Testar servidor", UiKit.Blue, (_, _) => TestServer()));

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 14 };
        body.RowStyles.Clear();
        for (var i = 0; i < 14; i++) body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Controls.Add(Caption("Seu ID de suporte"), 0, 0);
        body.Controls.Add(_sessionId, 0, 1);
        body.Controls.Add(Caption("Senha temporaria"), 0, 2);
        body.Controls.Add(_temporaryPassword, 0, 3);
        body.Controls.Add(Caption("Nivel de acesso"), 0, 4);
        body.Controls.Add(_accessLevel, 0, 5);
        body.Controls.Add(Caption("Ferramenta de conexao"), 0, 6);
        body.Controls.Add(_toolBox, 0, 7);
        body.Controls.Add(Caption("Servidor MaterialPro"), 0, 8);
        body.Controls.Add(_serverBox, 0, 9);
        body.Controls.Add(_allowControl, 0, 10);
        body.Controls.Add(_allowAdmin, 0, 11);
        body.Controls.Add(_serverSupport, 0, 12);

        panel.Controls.Add(body);
        panel.Controls.Add(actions);
        return panel;
    }

    private Control BuildMessagePanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18), Margin = new Padding(16, 0, 0, 0) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, WrapContents = false };
        buttons.Controls.Add(UiKit.Button("Copiar convite", UiKit.Green, (_, _) => Clipboard.SetText(_message.Text)));
        buttons.Controls.Add(UiKit.Button("Atualizar dados", UiKit.Blue, (_, _) => BuildMessage()));
        buttons.Controls.Add(UiKit.Button("Abrir pasta", UiKit.Orange, (_, _) => OpenFolder(AppContext.BaseDirectory)));

        panel.Controls.Add(_message);
        panel.Controls.Add(BuildAdminConnectionPanel());
        panel.Controls.Add(buttons);
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = "Mensagem pronta para enviar ao suporte",
            ForeColor = UiKit.Ink,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold)
        });
        return panel;
    }

    private Control BuildAdminConnectionPanel()
    {
        var panel = new Panel { Dock = DockStyle.Bottom, Height = 178, Padding = new Padding(0, 12, 0, 0), BackColor = Color.White };
        var title = new Label
        {
            Text = "Atendente / Administrador - conectar no computador do cliente",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = UiKit.Ink,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };

        var fields = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 74, WrapContents = true };
        foreach (var box in new Control[] { _clientNameBox, _clientCodeBox, _clientPasswordBox, _adminAccessLevel })
        {
            box.Margin = new Padding(0, 0, 8, 8);
            box.Height = 30;
        }
        fields.Controls.Add(_clientNameBox);
        fields.Controls.Add(_clientCodeBox);
        fields.Controls.Add(_clientPasswordBox);
        fields.Controls.Add(_adminAccessLevel);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false };
        actions.Controls.Add(UiKit.Button("Conectar ao cliente", UiKit.Green, (_, _) => ConnectToClient()));
        actions.Controls.Add(UiKit.Button("Copiar dados", UiKit.Blue, (_, _) => Clipboard.SetText(AdminConnectionText())));
        actions.Controls.Add(UiKit.Button("Limpar", UiKit.Orange, (_, _) => ClearClientConnection()));

        panel.Controls.Add(actions);
        panel.Controls.Add(fields);
        panel.Controls.Add(title);
        return panel;
    }

    private void NewSession()
    {
        _sessionId.Text = BuildSessionId();
        _temporaryPassword.Text = BuildPassword();
        _serverBox.Text = DefaultServerIp();
        ApplyAccessLevel();
        BuildMessage();
    }

    private void ApplyAccessLevel()
    {
        var code = SelectedCode(_accessLevel);
        _allowControl.Checked = code is "controle" or "administrativo" or "servidor";
        _allowAdmin.Checked = code is "administrativo" or "servidor";
        _serverSupport.Checked = code == "servidor";
        BuildMessage();
    }

    private void BuildMessage()
    {
        var ips = LocalIps();
        var level = _accessLevel.SelectedItem is Option option ? option.Name : "Controle com autorizacao";
        var tool = _toolBox.SelectedItem is Option toolOption ? toolOption.Name : "Assistencia Rapida do Windows";

        _status.Text = $"ID: {_sessionId.Text} | Nivel: {level} | Computador: {Environment.MachineName} | IPs: {string.Join(", ", ips)}";
        _message.Text = $"""
MaterialPro - convite de suporte remoto

Meu ID: {_sessionId.Text}
Senha temporaria: {_temporaryPassword.Text}
Nivel de acesso: {level}
Ferramenta escolhida: {tool}

Computador: {Environment.MachineName}
Usuario do Windows: {Environment.UserName}
Pasta do MaterialPro: {AppContext.BaseDirectory}
IP do servidor informado: {_serverBox.Text}
IPs deste computador: {string.Join(", ", ips)}
Data/hora: {DateTime.Now:dd/MM/yyyy HH:mm}

Permissoes autorizadas:
- Visualizar tela: sim
- Controlar mouse e teclado: {YesNo(_allowControl.Checked)}
- Fazer ajustes administrativos: {YesNo(_allowAdmin.Checked)}
- Verificar servidor e banco de dados: {YesNo(_serverSupport.Checked)}

Passo a passo para o cliente:
1. Fique com esta tela aberta.
2. Clique em "Iniciar atendimento".
3. Informe ao suporte o ID e a senha temporaria acima.
4. Aceite a solicitacao de controle somente se reconhecer o atendente.
5. Ao terminar, feche a ferramenta de acesso remoto.

Aviso de seguranca:
Nao informe a senha para pessoas desconhecidas. A senha foi feita para este atendimento.
""";
    }

    private void StartSelectedTool()
    {
        var code = SelectedCode(_toolBox);
        try
        {
            switch (code)
            {
                case "anydesk":
                    StartExecutable(FindAnyDesk(), "AnyDesk nao encontrado. Instale ou selecione Assistencia Rapida.");
                    break;
                case "teamviewer":
                    StartExecutable(FindTeamViewer(), "TeamViewer nao encontrado. Instale ou selecione Assistencia Rapida.");
                    break;
                case "rdp":
                    Process.Start(new ProcessStartInfo { FileName = "mstsc.exe", UseShellExecute = true });
                    break;
                default:
                    OpenQuickAssist();
                    break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Suporte remoto", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ConnectToClient()
    {
        if (string.IsNullOrWhiteSpace(_clientCodeBox.Text))
        {
            MessageBox.Show(this, "Digite o ID/codigo informado pelo cliente.", "Conectar ao cliente", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _clientCodeBox.Focus();
            return;
        }

        Clipboard.SetText(AdminConnectionText());
        var code = SelectedCode(_toolBox);
        try
        {
            switch (code)
            {
                case "anydesk":
                    StartAnyDeskForClient(_clientCodeBox.Text.Trim());
                    break;
                case "teamviewer":
                    StartExecutable(FindTeamViewer(), "TeamViewer nao encontrado. Abra o TeamViewer e digite o ID do cliente manualmente.");
                    break;
                case "rdp":
                    StartRemoteDesktop(_clientCodeBox.Text.Trim());
                    break;
                default:
                    OpenQuickAssist();
                    break;
            }

            _status.Text = $"Conexao iniciada para cliente {_clientCodeBox.Text.Trim()}. Dados copiados para a area de transferencia.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Conectar ao cliente", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private string AdminConnectionText()
    {
        var access = _adminAccessLevel.SelectedItem is Option option ? option.Name : "Controlar com permissao";
        return $"""
MaterialPro - dados para conexao remota

Cliente/loja: {_clientNameBox.Text}
ID/codigo do cliente: {_clientCodeBox.Text}
Senha do cliente: {_clientPasswordBox.Text}
Nivel solicitado: {access}
Ferramenta: {(_toolBox.SelectedItem is Option tool ? tool.Name : "Assistencia Rapida do Windows")}
Atendente Windows: {Environment.UserName}
Data/hora: {DateTime.Now:dd/MM/yyyy HH:mm}
""";
    }

    private void ClearClientConnection()
    {
        _clientNameBox.Clear();
        _clientCodeBox.Clear();
        _clientPasswordBox.Clear();
        UiKit.SelectIfAvailable(_adminAccessLevel, 1);
    }

    private void TestServer()
    {
        var host = string.IsNullOrWhiteSpace(_serverBox.Text) ? "127.0.0.1" : _serverBox.Text.Trim();
        try
        {
            using var tcp = new TcpClient();
            var ok = tcp.ConnectAsync(host, 3306).Wait(TimeSpan.FromSeconds(3)) && tcp.Connected;
            _status.Text = ok
                ? $"Servidor {host}:3306 respondeu. Rede OK para suporte."
                : $"Servidor {host}:3306 nao respondeu. Verifique firewall, IP e MySQL.";
        }
        catch (Exception ex)
        {
            _status.Text = $"Nao foi possivel acessar {host}:3306. {ex.Message}";
        }

        BuildMessage();
    }

    private static void OpenQuickAssist()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "ms-quick-assist:", UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo { FileName = "quickassist.exe", UseShellExecute = true });
        }
    }

    private static void StartExecutable(string? path, string notFoundMessage)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidOperationException(notFoundMessage);
        }

        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private static void StartAnyDeskForClient(string clientId)
    {
        var path = FindAnyDesk();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidOperationException("AnyDesk nao encontrado. Instale o AnyDesk ou selecione Assistencia Rapida.");
        }

        Process.Start(new ProcessStartInfo { FileName = path, Arguments = clientId, UseShellExecute = true });
    }

    private static void StartRemoteDesktop(string host)
    {
        Process.Start(new ProcessStartInfo { FileName = "mstsc.exe", Arguments = $"/v:{host}", UseShellExecute = true });
    }

    private static string? FindAnyDesk()
    {
        return FirstExisting(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "AnyDesk", "AnyDesk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AnyDesk", "AnyDesk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AnyDesk", "AnyDesk.exe"));
    }

    private static string? FindTeamViewer()
    {
        return FirstExisting(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TeamViewer", "TeamViewer.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "TeamViewer", "TeamViewer.exe"));
    }

    private static string? FirstExisting(params string[] paths) => paths.FirstOrDefault(File.Exists);

    private static string BuildSessionId()
    {
        var machine = Math.Abs(Environment.MachineName.GetHashCode()).ToString("000000000");
        return $"{machine[..3]} {machine.Substring(3, 3)} {machine.Substring(6, 3)}";
    }

    private static string BuildPassword()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        var value = BitConverter.ToUInt32(bytes) % 900000 + 100000;
        return value.ToString("000000");
    }

    private static string DefaultServerIp()
    {
        return LocalIps().FirstOrDefault() ?? "127.0.0.1";
    }

    private static IReadOnlyList<string> LocalIps()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up)
            .SelectMany(x => x.GetIPProperties().UnicastAddresses)
            .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x.Address))
            .Select(x => x.Address.ToString())
            .Distinct()
            .DefaultIfEmpty("127.0.0.1")
            .ToList();
    }

    private static string SelectedCode(ComboBox combo)
    {
        return combo.SelectedItem is Option option ? option.Code : string.Empty;
    }

    private static string YesNo(bool value) => value ? "sim" : "nao";

    private static void OpenFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
    }

    private static Label Caption(string text) => new()
    {
        Text = text,
        Height = 24,
        Dock = DockStyle.Top,
        ForeColor = UiKit.Muted,
        Margin = new Padding(0, 8, 0, 0)
    };

    private static Label ValueLabel() => new()
    {
        Height = 42,
        Dock = DockStyle.Top,
        ForeColor = UiKit.Ink,
        BackColor = Color.FromArgb(245, 248, 250),
        BorderStyle = BorderStyle.FixedSingle,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Consolas", 18F, FontStyle.Bold)
    };

    private static Button WideButton(string text, Color color, EventHandler click)
    {
        var button = UiKit.Button(text, color, click);
        button.Width = 330;
        button.Margin = new Padding(0, 0, 0, 8);
        return button;
    }

    private sealed record Option(string Code, string Name);
}
