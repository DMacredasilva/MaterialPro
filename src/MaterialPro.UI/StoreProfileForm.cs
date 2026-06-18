using MaterialPro.Application;

namespace MaterialPro.UI;

public sealed class StoreProfileForm : Form
{
    private readonly IStoreProfileService _service;
    private readonly TextBox _programNameBox;
    private readonly TextBox _storeNameBox;
    private readonly TextBox _cnpjBox;
    private readonly TextBox _addressBox;
    private readonly TextBox _phoneBox;
    private readonly TextBox _logoPathBox;

    public StoreProfileForm(IStoreProfileService service)
    {
        _service = service;
        Text = "Dados da Loja";
        StartPosition = FormStartPosition.CenterParent;
        Width = 720;
        Height = 520;
        MinimumSize = new Size(680, 480);

        var profile = _service.Get();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(20),
            AutoScroll = true
        };
        Controls.Add(root);

        _programNameBox = CreateInput(root, "Nome do programa", profile.ProgramName);
        _storeNameBox = CreateInput(root, "Nome da loja", profile.StoreName);
        _cnpjBox = CreateInput(root, "CNPJ", profile.Cnpj);
        _addressBox = CreateInput(root, "Endereço", profile.Address);
        _phoneBox = CreateInput(root, "Telefone", profile.Phone);
        _logoPathBox = CreateInput(root, "Logo", profile.LogoPath);

        var logoButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 8, 0, 12) };
        var selectLogoButton = new Button { Text = "Escolher logo", AutoSize = true };
        selectLogoButton.Click += (_, _) => ChooseLogo();
        logoButtons.Controls.Add(selectLogoButton);
        root.Controls.Add(logoButtons);

        var saveButton = new Button
        {
            Text = "Salvar dados da loja",
            Height = 42,
            Dock = DockStyle.Top
        };
        saveButton.Click += (_, _) =>
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
        };
        root.Controls.Add(saveButton);
    }

    private static TextBox CreateInput(Control parent, string label, string value)
    {
        parent.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 4)
        });

        var box = new TextBox
        {
            Text = value,
            Dock = DockStyle.Top
        };
        parent.Controls.Add(box);
        return box;
    }

    private void ChooseLogo()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Imagens (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _logoPathBox.Text = dialog.FileName;
        }
    }
}
