using System.Net.Sockets;
using MaterialPro.Infrastructure;

namespace MaterialPro.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        if (AutoUpdateRunner.StartUpdateAndExitIfAvailable("client"))
        {
            return;
        }

        while (true)
        {
            try
            {
                System.Windows.Forms.Application.Run(new MainForm());
                return;
            }
            catch (Exception ex)
            {
                using var form = new ServerConnectionForm(AppContext.BaseDirectory, ex.Message);
                if (form.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
            }
        }
    }
}

internal sealed class ServerConnectionForm : Form
{
    private readonly string _basePath;
    private readonly TextBox _serverBox;
    private readonly TextBox _statusBox;

    public ServerConnectionForm(string basePath, string error)
    {
        _basePath = basePath;
        Text = "MaterialPro - Configurar servidor";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 640;
        Height = 340;
        Font = new Font("Segoe UI", 10F);

        var label = new Label
        {
            Text = "O MaterialPro não conseguiu conectar ao servidor. Confira o IP abaixo e clique em Testar conexão.",
            Left = 18,
            Top = 18,
            Width = 585,
            Height = 44
        };

        _serverBox = new TextBox
        {
            Left = 18,
            Top = 70,
            Width = 585,
            Text = ReadCurrentServer(basePath)
        };

        _statusBox = new TextBox
        {
            Left = 18,
            Top = 106,
            Width = 585,
            Height = 92,
            Multiline = true,
            ReadOnly = true,
            Text = FriendlyError(error),
            BackColor = Color.White
        };

        var localButton = new Button
        {
            Text = "Usar este computador",
            Left = 18,
            Top = 214,
            Width = 160
        };
        localButton.Click += (_, _) =>
        {
            _serverBox.Text = "127.0.0.1";
            TestConnection();
        };

        var testButton = new Button
        {
            Text = "Testar conexão",
            Left = 188,
            Top = 214,
            Width = 130
        };
        testButton.Click += (_, _) => TestConnection();

        var saveButton = new Button
        {
            Text = "Salvar e tentar novamente",
            Left = 385,
            Top = 254,
            Width = 218,
            DialogResult = DialogResult.OK
        };
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button
        {
            Text = "Cancelar",
            Left = 275,
            Top = 254,
            Width = 100,
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(label);
        Controls.Add(_serverBox);
        Controls.Add(_statusBox);
        Controls.Add(localButton);
        Controls.Add(testButton);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void Save()
    {
        var server = string.IsNullOrWhiteSpace(_serverBox.Text) ? "127.0.0.1" : _serverBox.Text.Trim();
        var json = BuildSettingsJson(server);
        File.WriteAllText(MaterialProSettingsLoader.WritableClientSettingsPath(), json);
    }

    private void TestConnection()
    {
        var server = string.IsNullOrWhiteSpace(_serverBox.Text) ? "127.0.0.1" : _serverBox.Text.Trim();
        try
        {
            using var tcp = new TcpClient();
            var connected = tcp.ConnectAsync(server, 3306).Wait(TimeSpan.FromSeconds(3)) && tcp.Connected;
            _statusBox.Text = connected
                ? "Conexão de rede OK. Clique em Salvar e tentar novamente. Se ainda falhar, no servidor abra o atalho MaterialPro Configurar Banco."
                : $"Não consegui acessar {server}:3306. No servidor, abra MaterialPro Painel do Servidor e escolha Liberar firewall ou Configurar banco.";
        }
        catch
        {
            _statusBox.Text = $"Não consegui acessar {server}:3306. Verifique se o servidor está ligado e se o atalho MaterialPro Liberar Firewall foi executado no servidor.";
        }
    }

    private static string ReadCurrentServer(string basePath)
    {
        var path = MaterialProSettingsLoader.CandidateSettingsPaths(basePath).FirstOrDefault(File.Exists)
            ?? Path.Combine(basePath, "appsettings.json");
        if (!File.Exists(path))
        {
            return "127.0.0.1";
        }

        var text = File.ReadAllText(path);
        var marker = "server=";
        var start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return "127.0.0.1";
        }

        start += marker.Length;
        var end = text.IndexOf(';', start);
        return end > start ? text[start..end] : "127.0.0.1";
    }

    private static string BuildSettingsJson(string server)
    {
        return $$"""
{
  "ConnectionStrings": {
    "MaterialPro": "server={{server}};port=3306;database=materialpro;user=materialpro_system;password=MaterialPro@123!;Connection Timeout=5;Default Command Timeout=60;"
  },
  "Fiscal": {
    "Enabled": false,
    "Environment": "homologacao",
    "Cnpj": "",
    "Uf": "SP",
    "CertificatePath": "",
    "CertificatePassword": "",
    "CscId": "",
    "CscToken": "",
    "SchemaVersion": "MOC 7.0",
    "NfeServiceUrl": "",
    "NfceServiceUrl": "",
    "UpdateFeedUrl": ""
  }
}
""";
    }

    private static string FriendlyError(string error)
    {
        if (error.Contains("users", StringComparison.OrdinalIgnoreCase)
            || error.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)
            || error.Contains("não existe", StringComparison.OrdinalIgnoreCase))
        {
            return "O banco foi encontrado, mas as tabelas do MaterialPro ainda não foram criadas. No servidor, abra MaterialPro Configurar Banco.";
        }

        if (error.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase)
            || error.Contains("transient", StringComparison.OrdinalIgnoreCase))
        {
            return "Não foi possível acessar o MySQL do servidor. No servidor, abra MaterialPro Liberar Firewall e MaterialPro Configurar Banco. Se este computador for o servidor, clique em Usar este computador.";
        }

        return "Não foi possível abrir o MaterialPro. Teste a conexão abaixo. Se falhar, use o Painel do Servidor para iniciar/reparar o banco e liberar o firewall.";
    }
}
