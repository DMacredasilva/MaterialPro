using System.IO.Compression;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;

namespace MaterialPro.UI;

public sealed class InternalDocumentsForm : Form
{
    private static readonly Color Ink = Color.FromArgb(25, 39, 52);
    private static readonly Color Muted = Color.FromArgb(91, 105, 122);
    private static readonly Color Surface = Color.FromArgb(242, 245, 248);
    private static readonly Color Blue = Color.FromArgb(38, 89, 143);
    private static readonly Color Green = Color.FromArgb(45, 126, 86);
    private static readonly Color Orange = Color.FromArgb(218, 124, 38);

    private readonly IInternalDocumentService _documents;
    private readonly IPrintService _printService;
    private readonly IStoreProfileService _storeProfileService;
    private readonly AppUser? _user;
    private readonly ComboBox _kindBox = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _formatBox = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _templateBox = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _numberBox = new() { Dock = DockStyle.Top };
    private readonly TextBox _customerBox = new() { Dock = DockStyle.Top };
    private readonly TextBox _referenceBox = new() { Dock = DockStyle.Top };
    private readonly TextBox _totalBox = new() { Dock = DockStyle.Top, Text = "0" };
    private readonly TextBox _paymentBox = new() { Dock = DockStyle.Top };
    private readonly TextBox _notesBox = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _linesBox = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _templateEditor = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10F) };
    private readonly TextBox _previewBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, Font = new Font("Consolas", 10F) };
    private readonly PictureBox _logoPreview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly Label _logoStatus = new() { Dock = DockStyle.Bottom, Height = 32, ForeColor = Muted };
    private readonly List<DocumentTemplate> _templates = [];

    public InternalDocumentsForm(IInternalDocumentService documents, IPrintService printService, IStoreProfileService storeProfileService, AppUser? user)
    {
        _documents = documents;
        _printService = printService;
        _storeProfileService = storeProfileService;
        _user = user;

        Text = "MaterialPro - Cupons, recibos e modelos";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 760);
        BackColor = Surface;
        Font = new Font("Segoe UI", 10F);

        ConfigureCombos();
        LoadTemplates();
        LoadLogoPreview();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(18) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildLeftPanel(), 0, 0);
        root.Controls.Add(BuildRightPanel(), 1, 0);

        Controls.Add(root);
        Controls.Add(UiKit.Header("Cupons, recibos e impressao", "Monte documentos bonitos em portugues, edite modelos, importe arquivos e confira a previa antes de imprimir."));
        RefreshPreview();
    }

    private void ConfigureCombos()
    {
        _kindBox.DisplayMember = nameof(KindOption.Name);
        _kindBox.ValueMember = nameof(KindOption.Kind);
        _kindBox.Items.AddRange(Enum.GetValues<InternalDocumentKind>().Select(kind => new KindOption(kind, KindName(kind))).Cast<object>().ToArray());
        UiKit.SelectIfAvailable(_kindBox, 0);
        _kindBox.SelectedIndexChanged += (_, _) =>
        {
            _numberBox.Text = DefaultNumber();
            RefreshPreview();
        };

        _formatBox.DisplayMember = nameof(FormatOption.Name);
        _formatBox.ValueMember = nameof(FormatOption.Format);
        _formatBox.Items.AddRange(
        [
            new FormatOption(InternalPaperFormat.Thermal58, "Bobina 58 mm"),
            new FormatOption(InternalPaperFormat.Thermal80, "Bobina 80 mm"),
            new FormatOption(InternalPaperFormat.A4, "Folha A4"),
            new FormatOption(InternalPaperFormat.Label, "Etiqueta")
        ]);
        UiKit.SelectIfAvailable(_formatBox, 1);
        _formatBox.SelectedIndexChanged += (_, _) => RefreshPreview();

        _numberBox.Text = DefaultNumber();
        foreach (var box in new[] { _numberBox, _customerBox, _referenceBox, _totalBox, _paymentBox, _notesBox, _linesBox, _templateEditor })
        {
            box.TextChanged += (_, _) => RefreshPreview();
        }
        _templateBox.SelectedIndexChanged += (_, _) => ApplySelectedTemplate();
    }

    private Control BuildLeftPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16) };
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildDocumentTab());
        tabs.TabPages.Add(BuildTemplateTab());
        tabs.TabPages.Add(BuildLogoTab());
        panel.Controls.Add(tabs);
        return panel;
    }

    private TabPage BuildDocumentTab()
    {
        var page = new TabPage("Documento") { BackColor = Color.White, Padding = new Padding(10) };
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 11 };
        for (var i = 0; i < 9; i++) body.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));

        body.Controls.Add(Field("Tipo de documento", _kindBox), 0, 0);
        body.Controls.Add(Field("Modelo salvo", _templateBox), 0, 1);
        body.Controls.Add(Field("Formato de impressao", _formatBox), 0, 2);
        body.Controls.Add(Field("Numero", _numberBox), 0, 3);
        body.Controls.Add(Field("Cliente", _customerBox), 0, 4);
        body.Controls.Add(Field("Referencia", _referenceBox), 0, 5);
        body.Controls.Add(Field("Total", _totalBox), 0, 6);
        body.Controls.Add(Field("Pagamento", _paymentBox), 0, 7);
        body.Controls.Add(LabelOnly("Itens do cupom ou texto do documento"), 0, 8);
        body.Controls.Add(_linesBox, 0, 9);
        body.Controls.Add(Field("Observacoes", _notesBox), 0, 10);
        page.Controls.Add(body);
        return page;
    }

    private TabPage BuildTemplateTab()
    {
        var page = new TabPage("Modelos") { BackColor = Color.White, Padding = new Padding(10) };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 90, WrapContents = true };
        actions.Controls.Add(Button("Salvar modelo", Green, (_, _) => SaveTemplate()));
        actions.Controls.Add(Button("Excluir modelo", Orange, (_, _) => DeleteTemplate()));
        actions.Controls.Add(Button("Importar Word/PDF/Imagem", Blue, (_, _) => ImportTemplate()));
        actions.Controls.Add(Button("Modelo padrao", Blue, (_, _) => UseDefaultTemplate()));

        page.Controls.Add(_templateEditor);
        page.Controls.Add(actions);
        page.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 46,
            Text = "Edite o texto do modelo. Campos aceitos: {loja}, {cnpj}, {telefone}, {numero}, {cliente}, {referencia}, {total}, {pagamento}, {itens}, {observacoes}, {data}.",
            ForeColor = Muted
        });
        return page;
    }

    private TabPage BuildLogoTab()
    {
        var page = new TabPage("Logo") { BackColor = Color.White, Padding = new Padding(10) };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, WrapContents = true };
        actions.Controls.Add(Button("Editar dados da loja", Blue, (_, _) => EditStore()));
        actions.Controls.Add(Button("Atualizar logo", Green, (_, _) => LoadLogoPreview()));
        page.Controls.Add(_logoPreview);
        page.Controls.Add(_logoStatus);
        page.Controls.Add(actions);
        return page;
    }

    private Control BuildRightPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16), Margin = new Padding(16, 0, 0, 0) };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, WrapContents = true };
        actions.Controls.Add(Button("Visualizar PDF", Orange, (_, _) => PreviewPdf()));
        actions.Controls.Add(Button("Gerar PDF", Blue, (_, _) => SavePdf()));
        actions.Controls.Add(Button("Imprimir", Green, (_, _) => Print()));
        actions.Controls.Add(Button("Copiar previa", Blue, (_, _) => Clipboard.SetText(_previewBox.Text)));
        panel.Controls.Add(_previewBox);
        panel.Controls.Add(actions);
        panel.Controls.Add(new Label { Text = "Previa antes da impressao", Dock = DockStyle.Top, Height = 34, ForeColor = Ink, Font = new Font("Segoe UI", 13F, FontStyle.Bold) });
        return panel;
    }

    private void LoadTemplates()
    {
        _templates.Clear();
        _templates.AddRange(DefaultTemplates());
        var path = TemplatesPath();
        if (File.Exists(path))
        {
            try
            {
                var saved = JsonSerializer.Deserialize<List<DocumentTemplate>>(File.ReadAllText(path)) ?? [];
                foreach (var template in saved)
                {
                    _templates.RemoveAll(x => x.Id == template.Id);
                    _templates.Add(template);
                }
            }
            catch
            {
                // Se o arquivo estiver corrompido, os modelos padrao continuam disponiveis.
            }
        }

        RefreshTemplateCombo();
        UseDefaultTemplate();
    }

    private void RefreshTemplateCombo()
    {
        var current = _templateBox.SelectedItem is DocumentTemplate selected ? selected.Id : string.Empty;
        _templateBox.DisplayMember = nameof(DocumentTemplate.Name);
        _templateBox.ValueMember = nameof(DocumentTemplate.Id);
        _templateBox.DataSource = null;
        _templateBox.DataSource = _templates.OrderBy(x => x.Kind).ThenBy(x => x.Name).ToList();
        var index = _templates.FindIndex(x => x.Id == current);
        UiKit.SelectIfAvailable(_templateBox, index >= 0 ? index : 0);
    }

    private void ApplySelectedTemplate()
    {
        if (_templateBox.SelectedItem is not DocumentTemplate template)
        {
            return;
        }

        _templateEditor.Text = template.Body;
        if (_kindBox.Items.Cast<object>().OfType<KindOption>().FirstOrDefault(x => x.Kind == template.Kind) is { } kind)
        {
            _kindBox.SelectedItem = kind;
        }
        RefreshPreview();
    }

    private void SaveTemplate()
    {
        if (!IsAdmin())
        {
            MessageBox.Show(this, "Somente Administrador pode salvar ou alterar modelos.", "Modelos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var name = Microsoft.VisualBasic.Interaction.InputBox("Nome do modelo:", "Salvar modelo", SelectedTemplate()?.Name ?? "Meu modelo");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var id = SelectedTemplate()?.Id;
        if (string.IsNullOrWhiteSpace(id) || id.StartsWith("padrao-", StringComparison.OrdinalIgnoreCase))
        {
            id = Guid.NewGuid().ToString("N");
        }

        _templates.RemoveAll(x => x.Id == id);
        _templates.Add(new DocumentTemplate(id, name.Trim(), SelectedKind(), _templateEditor.Text));
        SaveTemplates();
        RefreshTemplateCombo();
        MessageBox.Show(this, "Modelo salvo.", "Modelos", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void DeleteTemplate()
    {
        if (!IsAdmin())
        {
            MessageBox.Show(this, "Somente Administrador pode excluir modelos.", "Modelos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selected = SelectedTemplate();
        if (selected is null || selected.Id.StartsWith("padrao-", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Modelo padrao nao pode ser excluido.", "Modelos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _templates.RemoveAll(x => x.Id == selected.Id);
        SaveTemplates();
        RefreshTemplateCombo();
    }

    private void ImportTemplate()
    {
        if (!IsAdmin())
        {
            MessageBox.Show(this, "Somente Administrador pode importar modelos.", "Modelos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "Modelos (*.txt;*.docx;*.pdf;*.png;*.jpg;*.jpeg)|*.txt;*.docx;*.pdf;*.png;*.jpg;*.jpeg|Todos (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var imported = ImportText(dialog.FileName);
        _templateEditor.Text = imported;
        MessageBox.Show(this, "Arquivo importado para o editor. Confira a previa e clique em Salvar modelo.", "Modelos", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UseDefaultTemplate()
    {
        var template = _templates.FirstOrDefault(x => x.Kind == SelectedKind()) ?? _templates.FirstOrDefault();
        if (template is null)
        {
            return;
        }

        _templateBox.SelectedItem = template;
        _templateEditor.Text = template.Body;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        try
        {
            _previewBox.Text = BuildDocumentText();
        }
        catch
        {
            // Evita quebrar a digitacao enquanto o usuario edita campos.
        }
    }

    private string BuildDocumentText()
    {
        var profile = _storeProfileService.Get();
        var template = string.IsNullOrWhiteSpace(_templateEditor.Text) ? DefaultTemplateBody(SelectedKind()) : _templateEditor.Text;
        var items = string.Join(Environment.NewLine, _linesBox.Lines.Where(x => !string.IsNullOrWhiteSpace(x)));
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["loja"] = string.IsNullOrWhiteSpace(profile.StoreName) ? profile.ProgramName : profile.StoreName,
            ["programa"] = profile.ProgramName,
            ["cnpj"] = profile.Cnpj,
            ["telefone"] = profile.Phone,
            ["endereco"] = profile.Address,
            ["numero"] = _numberBox.Text,
            ["cliente"] = _customerBox.Text,
            ["referencia"] = _referenceBox.Text,
            ["total"] = MoneyText(),
            ["pagamento"] = _paymentBox.Text,
            ["itens"] = items,
            ["observacoes"] = _notesBox.Text,
            ["data"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        };

        return Regex.Replace(template, "\\{([a-zA-Z0-9_]+)\\}", match =>
        {
            var key = match.Groups[1].Value;
            return replacements.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    private void SavePdf()
    {
        using var dialog = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = $"{SelectedKindName().ToLowerInvariant().Replace(' ', '-')}-{_numberBox.Text}.pdf" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        File.WriteAllBytes(dialog.FileName, _documents.GeneratePdf(BuildRequest()));
        MessageBox.Show(this, $"PDF salvo em {dialog.FileName}", "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void PreviewPdf()
    {
        var file = Path.Combine(Path.GetTempPath(), $"materialpro-previa-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(file, _documents.GeneratePdf(BuildRequest()));
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    }

    private void Print()
    {
        var lines = BuildDocumentText().Split(Environment.NewLine, StringSplitOptions.None);
        _printService.PrintText(SelectedKindName(), lines);
        MessageBox.Show(this, "Envio para impressao iniciado.", "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private InternalDocumentRequest BuildRequest()
    {
        var profile = _storeProfileService.Get();
        return new InternalDocumentRequest(
            SelectedKind(),
            SelectedFormat(),
            _numberBox.Text,
            _customerBox.Text,
            _referenceBox.Text,
            decimal.TryParse(_totalBox.Text, out var total) ? total : 0,
            _paymentBox.Text,
            _notesBox.Text,
            BuildDocumentText().Split(Environment.NewLine, StringSplitOptions.None),
            string.IsNullOrWhiteSpace(profile.StoreName) ? profile.ProgramName : profile.StoreName,
            profile.Cnpj,
            profile.Phone,
            profile.Address,
            profile.LogoPath);
    }

    private void EditStore()
    {
        using var form = new StoreProfileForm(_storeProfileService);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            LoadLogoPreview();
            RefreshPreview();
        }
    }

    private void LoadLogoPreview()
    {
        try
        {
            _logoPreview.Image?.Dispose();
            _logoPreview.Image = null;
            var profile = _storeProfileService.Get();
            if (string.IsNullOrWhiteSpace(profile.LogoPath) || !File.Exists(profile.LogoPath))
            {
                _logoStatus.Text = "Nenhuma logo configurada.";
                return;
            }

            using var image = Image.FromFile(profile.LogoPath);
            _logoPreview.Image = new Bitmap(image);
            _logoStatus.Text = $"Logo carregada: {profile.LogoPath}";
        }
        catch (Exception ex)
        {
            _logoStatus.Text = $"Nao foi possivel carregar a logo: {ex.Message}";
        }
    }

    private static Panel Field(string label, Control control)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 8) };
        panel.Controls.Add(control);
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 22, ForeColor = Muted });
        return panel;
    }

    private static Label LabelOnly(string text) => new() { Text = text, Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.BottomLeft };

    private static Button Button(string text, Color color, EventHandler click)
    {
        var button = new Button { Text = text, Width = 170, Height = 36, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 10, 8) };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private void SaveTemplates()
    {
        var custom = _templates.Where(x => !x.Id.StartsWith("padrao-", StringComparison.OrdinalIgnoreCase)).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(TemplatesPath())!);
        File.WriteAllText(TemplatesPath(), JsonSerializer.Serialize(custom, new JsonSerializerOptions { WriteIndented = true }));
    }

    private DocumentTemplate? SelectedTemplate() => _templateBox.SelectedItem as DocumentTemplate;
    private bool IsAdmin() => _user?.Role == UserRole.Admin;
    private InternalDocumentKind SelectedKind() => _kindBox.SelectedItem is KindOption option ? option.Kind : InternalDocumentKind.SaleCoupon;
    private InternalPaperFormat SelectedFormat() => _formatBox.SelectedItem is FormatOption option ? option.Format : InternalPaperFormat.Thermal80;
    private string SelectedKindName() => KindName(SelectedKind());
    private string MoneyText() => decimal.TryParse(_totalBox.Text, out var value) ? value.ToString("C") : _totalBox.Text;
    private string DefaultNumber() => $"DOC-{DateTime.Now:yyyyMMddHHmmss}";

    private static string TemplatesPath()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MaterialPro", "Client", "modelos");
        return Path.Combine(root, "documentos-internos.json");
    }

    private static IReadOnlyList<DocumentTemplate> DefaultTemplates()
    {
        return Enum.GetValues<InternalDocumentKind>()
            .Select(kind => new DocumentTemplate($"padrao-{kind}", $"{KindName(kind)} padrao", kind, DefaultTemplateBody(kind)))
            .ToList();
    }

    private static string DefaultTemplateBody(InternalDocumentKind kind)
    {
        return kind == InternalDocumentKind.ProductLabel
            ? """
{loja}
ETIQUETA DE PRODUTO
{itens}
Preco: {total}
Codigo: {referencia}
"""
            : """
{loja}
CNPJ: {cnpj}  Tel: {telefone}
{endereco}

{data}
{numero}
Cliente: {cliente}
Referencia: {referencia}

{itens}

Total: {total}
Pagamento: {pagamento}
Observacoes: {observacoes}

Obrigado pela preferencia.
""";
    }

    private static string ImportText(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".txt")
        {
            return File.ReadAllText(path);
        }

        if (extension == ".docx")
        {
            using var zip = ZipFile.OpenRead(path);
            var entry = zip.GetEntry("word/document.xml");
            if (entry is null)
            {
                return $"Arquivo Word importado: {path}";
            }

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var xml = reader.ReadToEnd();
            var text = Regex.Replace(xml, "<[^>]+>", " ");
            return System.Net.WebUtility.HtmlDecode(Regex.Replace(text, "\\s+", " ")).Trim();
        }

        return $"Arquivo anexado ao modelo: {path}{Environment.NewLine}Use este arquivo como referencia visual do modelo importado.";
    }

    private static string KindName(InternalDocumentKind kind) => kind switch
    {
        InternalDocumentKind.SaleCoupon => "Cupom simples de venda",
        InternalDocumentKind.SaleReceipt => "Recibo de venda",
        InternalDocumentKind.Budget => "Orcamento",
        InternalDocumentKind.PaymentProof => "Comprovante de pagamento",
        InternalDocumentKind.SaleSecondCopy => "Segunda via de venda",
        InternalDocumentKind.ReturnProof => "Comprovante de devolucao",
        InternalDocumentKind.CancellationProof => "Comprovante de cancelamento",
        InternalDocumentKind.CashOpening => "Abertura de caixa",
        InternalDocumentKind.CashWithdrawal => "Sangria de caixa",
        InternalDocumentKind.CashSupply => "Suprimento de caixa",
        InternalDocumentKind.CashClosing => "Fechamento de caixa",
        InternalDocumentKind.A4Report => "Relatorio A4",
        InternalDocumentKind.ProductLabel => "Etiqueta de produto",
        _ => kind.ToString()
    };

    private sealed record KindOption(InternalDocumentKind Kind, string Name);
    private sealed record FormatOption(InternalPaperFormat Format, string Name);
    private sealed record DocumentTemplate(string Id, string Name, InternalDocumentKind Kind, string Body);
}
