using MaterialPro.Application;
using MaterialPro.Infrastructure;

namespace MaterialPro.UI;

public sealed class InternalDocumentsForm : Form
{
    private readonly IInternalDocumentService _documents;
    private readonly IPrintService _printService;
    private readonly TextBox _numberBox;
    private readonly TextBox _customerBox;
    private readonly TextBox _referenceBox;
    private readonly TextBox _totalBox;
    private readonly TextBox _paymentBox;
    private readonly TextBox _notesBox;
    private readonly TextBox _linesBox;
    private readonly ComboBox _kindBox;
    private readonly ComboBox _formatBox;

    public InternalDocumentsForm(IInternalDocumentService documents, IPrintService printService)
    {
        _documents = documents;
        _printService = printService;

        Text = "Documentos internos";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1000, 720);
        BackColor = Color.FromArgb(246, 248, 252);
        Font = new Font("Segoe UI", 10F);

        _kindBox = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        _kindBox.DataSource = Enum.GetValues(typeof(InternalDocumentKind));
        _formatBox = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        _formatBox.DataSource = Enum.GetValues(typeof(InternalPaperFormat));
        _kindBox.SelectedIndex = 0;
        _formatBox.SelectedIndex = 0;
        _numberBox = new TextBox { Dock = DockStyle.Top, Text = $"DOC-{DateTime.UtcNow:yyyyMMddHHmmss}" };
        _customerBox = new TextBox { Dock = DockStyle.Top };
        _referenceBox = new TextBox { Dock = DockStyle.Top };
        _totalBox = new TextBox { Dock = DockStyle.Top, Text = "0" };
        _paymentBox = new TextBox { Dock = DockStyle.Top };
        _notesBox = new TextBox { Dock = DockStyle.Top, Multiline = true, Height = 70 };
        _linesBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Padding = new Padding(20)
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        top.Controls.Add(Field("Tipo", _kindBox), 0, 0);
        top.Controls.Add(Field("Formato", _formatBox), 1, 0);
        top.Controls.Add(Field("Numero", _numberBox), 0, 1);
        top.Controls.Add(Field("Cliente", _customerBox), 1, 1);
        top.Controls.Add(Field("Referencia", _referenceBox), 0, 2);
        top.Controls.Add(Field("Total", _totalBox), 1, 2);
        top.Controls.Add(Field("Pagamento", _paymentBox), 0, 3);
        var notesField = Field("Observacoes", _notesBox);
        top.Controls.Add(notesField, 1, 3);

        var linesPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
        linesPanel.Controls.Add(_linesBox);
        linesPanel.Controls.Add(new Label { Text = "Linhas do documento (uma por linha)", Dock = DockStyle.Top, Height = 22 });

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(20, 8, 20, 8)
        };
        buttons.Controls.Add(MakeButton("Gerar PDF", Color.FromArgb(30, 78, 140), (_, _) => SavePdf()));
        buttons.Controls.Add(MakeButton("Imprimir", Color.FromArgb(28, 120, 84), (_, _) => Print()));

        Controls.Add(linesPanel);
        Controls.Add(buttons);
        Controls.Add(top);
    }

    private static Panel Field(string label, Control control)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(0, 0, 10, 10) };
        panel.Controls.Add(control);
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 22 });
        return panel;
    }

    private static Button MakeButton(string text, Color color, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            Height = 38,
            Width = 140,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        button.Click += click;
        return button;
    }

    private void SavePdf()
    {
        var pdf = _documents.GeneratePdf(BuildRequest());
        var file = Path.Combine(Path.GetTempPath(), $"materialpro-interno-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(file, pdf);
        MessageBox.Show(this, $"PDF salvo em {file}", "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void Print()
    {
        var request = BuildRequest();
        var lines = new List<string> { request.Kind.ToString(), request.Number };
        lines.AddRange(request.Lines);
        _printService.PrintText(request.Kind.ToString(), lines);
        MessageBox.Show(this, "Envio para impressao iniciado.", "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private InternalDocumentRequest BuildRequest()
    {
        return new InternalDocumentRequest(
            (InternalDocumentKind)_kindBox.SelectedItem!,
            (InternalPaperFormat)_formatBox.SelectedItem!,
            _numberBox.Text,
            _customerBox.Text,
            _referenceBox.Text,
            decimal.TryParse(_totalBox.Text, out var total) ? total : 0,
            _paymentBox.Text,
            _notesBox.Text,
            _linesBox.Lines);
    }
}
