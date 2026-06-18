using MaterialPro.Application;

namespace MaterialPro.UI;

public sealed class StoreProfileForm : Form
{
    private readonly IStoreProfileService _service;
    private readonly TextBox _programNameBox = Input();
    private readonly TextBox _storeNameBox = Input();
    private readonly TextBox _cnpjBox = Input();
    private readonly TextBox _addressBox = Input();
    private readonly TextBox _phoneBox = Input();
    private readonly TextBox _logoPathBox = Input();
    private readonly PictureBox _logoPreview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly Label _logoStatus = new() { Dock = DockStyle.Bottom, Height = 36, ForeColor = UiKit.Muted };

    public StoreProfileForm(IStoreProfileService service)
    {
        _service = service;
        Text = "MaterialPro - Dados da loja";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 620);
        BackColor = UiKit.Surface;
        Font = new Font("Segoe UI", 10F);

        var profile = _service.Get();
        _programNameBox.Text = profile.ProgramName;
        _storeNameBox.Text = profile.StoreName;
        _cnpjBox.Text = profile.Cnpj;
        _addressBox.Text = profile.Address;
        _phoneBox.Text = profile.Phone;
        _logoPathBox.Text = profile.LogoPath;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(18) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        root.Controls.Add(BuildFields(), 0, 0);
        root.Controls.Add(BuildLogoPanel(), 1, 0);

        Controls.Add(root);
        Controls.Add(UiKit.Header("Dados da loja", "Configure nome, contato e logo usada nos documentos e na impressao."));
        LoadLogoPreview();
    }

    private Control BuildFields()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18) };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        for (var i = 0; i < 6; i++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        fields.Controls.Add(Field("Nome do programa", _programNameBox), 0, 0);
        fields.Controls.Add(Field("Nome da loja", _storeNameBox), 0, 1);
        fields.Controls.Add(Field("CNPJ", _cnpjBox), 0, 2);
        fields.Controls.Add(Field("Endereco", _addressBox), 0, 3);
        fields.Controls.Add(Field("Telefone", _phoneBox), 0, 4);
        fields.Controls.Add(Field("Arquivo da logo", _logoPathBox), 0, 5);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, WrapContents = true };
        actions.Controls.Add(Button("Salvar dados", UiKit.Green, (_, _) => Save()));
        actions.Controls.Add(Button("Escolher logo", UiKit.Blue, (_, _) => ChooseLogo()));
        actions.Controls.Add(Button("Limpar logo", UiKit.Orange, (_, _) => ClearLogo()));

        panel.Controls.Add(fields);
        panel.Controls.Add(actions);
        return panel;
    }

    private Control BuildLogoPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18), Margin = new Padding(16, 0, 0, 0) };
        panel.Controls.Add(_logoPreview);
        panel.Controls.Add(_logoStatus);
        panel.Controls.Add(new Label { Text = "Previa da logo", Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = UiKit.Ink });
        return panel;
    }

    private void Save()
    {
        _service.Save(new StoreProfileRequest(
            _programNameBox.Text,
            _storeNameBox.Text,
            _cnpjBox.Text,
            _addressBox.Text,
            _phoneBox.Text,
            _logoPathBox.Text));

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ChooseLogo()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Imagens (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var stablePath = CopyLogoToProgramData(dialog.FileName);
        _logoPathBox.Text = stablePath;
        LoadLogoPreview();
    }

    private void ClearLogo()
    {
        _logoPathBox.Clear();
        LoadLogoPreview();
    }

    private void LoadLogoPreview()
    {
        try
        {
            _logoPreview.Image?.Dispose();
            _logoPreview.Image = null;
            if (string.IsNullOrWhiteSpace(_logoPathBox.Text) || !File.Exists(_logoPathBox.Text))
            {
                _logoStatus.Text = "Nenhuma logo selecionada.";
                return;
            }

            using var image = Image.FromFile(_logoPathBox.Text);
            _logoPreview.Image = new Bitmap(image);
            _logoStatus.Text = "Logo carregada sem distorcer.";
        }
        catch (Exception ex)
        {
            _logoStatus.Text = $"Nao foi possivel abrir a logo: {ex.Message}";
        }
    }

    private static string CopyLogoToProgramData(string source)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MaterialPro", "Client", "logos");
        Directory.CreateDirectory(root);
        var extension = Path.GetExtension(source);
        var target = Path.Combine(root, $"logo{extension}");
        File.Copy(source, target, overwrite: true);
        return target;
    }

    private static Panel Field(string label, Control control)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 8) };
        panel.Controls.Add(control);
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 22, ForeColor = UiKit.Muted });
        return panel;
    }

    private static TextBox Input() => new() { Dock = DockStyle.Top, Height = 32, BorderStyle = BorderStyle.FixedSingle };

    private static Button Button(string text, Color color, EventHandler click)
    {
        var button = new Button { Text = text, Width = 140, Height = 36, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 10, 8) };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }
}
