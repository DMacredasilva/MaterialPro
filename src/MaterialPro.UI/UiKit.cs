namespace MaterialPro.UI;

internal static class UiKit
{
    public static readonly Color Ink = Color.FromArgb(25, 39, 52);
    public static readonly Color Muted = Color.FromArgb(91, 105, 122);
    public static readonly Color Surface = Color.FromArgb(242, 245, 248);
    public static readonly Color Navy = Color.FromArgb(24, 52, 82);
    public static readonly Color Blue = Color.FromArgb(38, 89, 143);
    public static readonly Color Green = Color.FromArgb(45, 126, 86);
    public static readonly Color Orange = Color.FromArgb(218, 124, 38);
    public static readonly Color Brick = Color.FromArgb(165, 74, 52);
    public static readonly Color Concrete = Color.FromArgb(218, 224, 231);

    public static Panel Header(string title, string subtitle)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(20, 14, 20, 10), BackColor = Color.White };
        panel.Controls.Add(new Label { Text = subtitle, Dock = DockStyle.Bottom, Height = 28, ForeColor = Muted });
        panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 42, ForeColor = Ink, Font = new Font("Segoe UI", 20F, FontStyle.Bold) });
        return panel;
    }

    public static Button Button(string text, Color color, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            Width = 150,
            Height = 36,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 10, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    public static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = true,
        AllowUserToAddRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None
    };

    public static void SelectIfAvailable(ComboBox comboBox, int index)
    {
        if (comboBox.Items.Count == 0 || index < 0 || index >= comboBox.Items.Count)
        {
            comboBox.SelectedIndex = -1;
            return;
        }

        comboBox.SelectedIndex = index;
    }
}
