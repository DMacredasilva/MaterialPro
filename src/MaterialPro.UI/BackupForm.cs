using MaterialPro.Domain;
using MaterialPro.Infrastructure;

namespace MaterialPro.UI;

public sealed class BackupForm : Form
{
    private readonly BackupModuleService _backupService = new();
    private readonly AppUser? _user;
    private readonly string _connectionString;
    private readonly TextBox _folderBox = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White };
    private readonly TextBox _statusBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White };
    private readonly CheckBox _filesBox = new() { Text = "Backup dos arquivos do sistema", Checked = true, AutoSize = true };
    private readonly CheckBox _databaseBox = new() { Text = "Backup do banco de dados MySQL", Checked = true, AutoSize = true };
    private readonly Button _backupButton;

    public BackupForm(AppUser? user, string connectionString)
    {
        _user = user;
        _connectionString = connectionString;

        Text = "MaterialPro - Backup do sistema";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 640);
        BackColor = UiKit.Surface;
        Font = new Font("Segoe UI", 10F);

        _folderBox.Text = _backupService.DefaultDestinationFolder();
        _backupButton = UiKit.Button("Fazer backup completo", UiKit.Green, async (_, _) => await CreateBackup());
        _backupButton.Width = 190;
        _backupButton.Enabled = IsAdmin();

        var folderPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 76, ColumnCount = 2, Padding = new Padding(18, 10, 18, 8), BackColor = Color.White };
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        folderPanel.Controls.Add(_folderBox, 0, 1);
        folderPanel.Controls.Add(UiKit.Button("Escolher pasta", UiKit.Blue, (_, _) => ChooseFolder()), 1, 1);
        folderPanel.Controls.Add(new Label { Text = "Pasta onde o backup sera salvo", Dock = DockStyle.Fill, ForeColor = UiKit.Muted }, 0, 0);

        var options = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(18, 8, 18, 8), BackColor = Color.White };
        options.Controls.Add(_filesBox);
        options.Controls.Add(_databaseBox);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(18, 8, 18, 8), BackColor = Color.White };
        actions.Controls.Add(_backupButton);
        actions.Controls.Add(UiKit.Button("Abrir pasta", UiKit.Blue, (_, _) => OpenFolder(_folderBox.Text)));

        _statusBox.Text = IsAdmin()
            ? "Pronto para gerar backup. O arquivo ZIP vai guardar os arquivos do MaterialPro e o banco quando o mysqldump estiver instalado."
            : "Somente usuario Administrador pode gerar backup completo.";

        Controls.Add(_statusBox);
        Controls.Add(actions);
        Controls.Add(options);
        Controls.Add(folderPanel);
        Controls.Add(UiKit.Header("Backup do sistema", "Crie uma copia de seguranca dos arquivos do MaterialPro e do banco de dados para recuperar em caso de erro."));
    }

    private async Task CreateBackup()
    {
        if (!IsAdmin())
        {
            MessageBox.Show(this, "Somente Administrador pode fazer backup.", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_filesBox.Checked && !_databaseBox.Checked)
        {
            MessageBox.Show(this, "Marque pelo menos uma opcao de backup.", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _backupButton.Enabled = false;
        _statusBox.Text = "Gerando backup, aguarde. Nao feche o MaterialPro durante o processo.";

        try
        {
            var request = new BackupRequest(
                AppContext.BaseDirectory,
                _folderBox.Text,
                _connectionString,
                _filesBox.Checked,
                _databaseBox.Checked);

            var result = await Task.Run(() => _backupService.CreateBackup(request));
            _statusBox.Text =
                $"{result.Message}\r\n\r\nArquivo gerado:\r\n{result.BackupPath}\r\n\r\nIncluiu arquivos: {YesNo(result.FilesIncluded)}\r\nIncluiu banco MySQL: {YesNo(result.DatabaseIncluded)}\r\n\r\nO relatorio esta dentro do ZIP como {result.LogPath}.";

            MessageBox.Show(this, result.Message, "Backup", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _statusBox.Text = $"Falha ao gerar backup: {ex.Message}";
            MessageBox.Show(this, "Nao foi possivel gerar o backup. Veja os detalhes na tela.", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _backupButton.Enabled = IsAdmin();
        }
    }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Escolha onde salvar os backups do MaterialPro",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_folderBox.Text) ? _folderBox.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folderBox.Text = dialog.SelectedPath;
        }
    }

    private bool IsAdmin() => _user?.Role == UserRole.Admin;

    private static string YesNo(bool value) => value ? "Sim" : "Nao";

    private static void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
    }
}
