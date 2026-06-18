using MaterialPro.Application;
using MaterialPro.Infrastructure;

namespace MaterialPro.UI;

public sealed class DbfImportForm : Form
{
    private readonly IDbfImportService _service;
    private readonly TextBox _pathBox;
    private readonly ListBox _filesList;
    private readonly DataGridView _fieldsGrid;
    private readonly DataGridView _mappingGrid;
    private readonly TextBox _validationBox;
    private readonly TextBox _resultBox;
    private readonly CheckBox _updateExistingCheck;
    private readonly CheckBox _ignoreDuplicatesCheck;
    private readonly CheckBox _partialImportCheck;
    private readonly NumericUpDown _maxRecords;
    private DbfImportScanResult? _scan;

    public DbfImportForm(IDbfImportService service)
    {
        _service = service;
        Text = "Sistema > Importação > Arquivos DBF";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1100;
        Height = 760;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        _pathBox = new TextBox { Width = 520 };
        var fileButton = new Button { Text = "Arquivo DBF" };
        var folderButton = new Button { Text = "Pasta DBF" };
        var scanButton = new Button { Text = "Escanear" };
        fileButton.Click += (_, _) => SelectFile();
        folderButton.Click += (_, _) => SelectFolder();
        scanButton.Click += (_, _) => Scan();

        _updateExistingCheck = new CheckBox { Text = "Atualizar existentes" };
        _ignoreDuplicatesCheck = new CheckBox { Text = "Ignorar duplicados", Checked = true };
        _partialImportCheck = new CheckBox { Text = "Importação parcial" };
        _maxRecords = new NumericUpDown { Width = 90, Minimum = 1, Maximum = 100000, Value = 100 };

        top.Controls.AddRange([_pathBox, fileButton, folderButton, scanButton, _updateExistingCheck, _ignoreDuplicatesCheck, _partialImportCheck, _maxRecords]);
        root.Controls.Add(top);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        root.Controls.Add(tabs);

        _filesList = new ListBox { Dock = DockStyle.Fill };
        _filesList.SelectedIndexChanged += (_, _) => LoadPreview();
        tabs.TabPages.Add(CreatePage("Etapa 1 - Seleção", _filesList));

        _fieldsGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = true, ReadOnly = true };
        tabs.TabPages.Add(CreatePage("Etapa 2 - Campos", _fieldsGrid));

        _mappingGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = true };
        tabs.TabPages.Add(CreatePage("Etapa 3 - Mapeamento", _mappingGrid));

        var validatePanel = new Panel { Dock = DockStyle.Fill };
        var validateButton = new Button { Text = "Validar", Dock = DockStyle.Top, Height = 36 };
        validateButton.Click += (_, _) => ValidateSelection();
        _validationBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
        validatePanel.Controls.Add(_validationBox);
        validatePanel.Controls.Add(validateButton);
        tabs.TabPages.Add(CreatePage("Etapa 4 - Validação", validatePanel));

        var importPanel = new Panel { Dock = DockStyle.Fill };
        var importButton = new Button { Text = "Importar", Dock = DockStyle.Top, Height = 36 };
        importButton.Click += (_, _) => ImportSelection();
        _resultBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
        importPanel.Controls.Add(_resultBox);
        importPanel.Controls.Add(importButton);
        tabs.TabPages.Add(CreatePage("Etapa 5 - Importação", importPanel));
    }

    private static TabPage CreatePage(string title, Control control)
    {
        var page = new TabPage(title);
        control.Dock = DockStyle.Fill;
        page.Controls.Add(control);
        return page;
    }

    private void SelectFile()
    {
        using var dialog = new OpenFileDialog { Filter = "DBF (*.dbf)|*.dbf" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathBox.Text = dialog.FileName;
        }
    }

    private void SelectFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathBox.Text = dialog.SelectedPath;
        }
    }

    private void Scan()
    {
        if (string.IsNullOrWhiteSpace(_pathBox.Text))
        {
            return;
        }

        var selection = File.Exists(_pathBox.Text)
            ? new DbfImportSelection(DbfImportSelectionKind.File, _pathBox.Text)
            : new DbfImportSelection(DbfImportSelectionKind.Folder, _pathBox.Text);

        _scan = _service.Scan(selection);
        _filesList.DataSource = _scan.Files.ToList();
        _filesList.DisplayMember = nameof(DbfImportSourceFile.FileName);
    }

    private void LoadPreview()
    {
        if (_filesList.SelectedItem is not DbfImportSourceFile source)
        {
            return;
        }

        var preview = _service.Preview(source.FilePath);
        _fieldsGrid.DataSource = preview.Fields.ToList();
        _mappingGrid.DataSource = preview.SuggestedMappings.ToList();
    }

    private void ValidateSelection()
    {
        if (_filesList.SelectedItem is not DbfImportSourceFile source)
        {
            return;
        }

        var mappings = CurrentMappings();
        var result = _service.Validate(source.FilePath, mappings, _partialImportCheck.Checked ? (int)_maxRecords.Value : null);
        _validationBox.Text = string.Join(Environment.NewLine, result.Issues.Select(x => $"{x.Severity}: {x.Message}"));
        if (string.IsNullOrWhiteSpace(_validationBox.Text))
        {
            _validationBox.Text = "Validação concluída sem erros.";
        }
    }

    private void ImportSelection()
    {
        if (_scan is null || _scan.Files.Count == 0)
        {
            return;
        }

        var mappingsByFile = _scan.Files.ToDictionary(
            x => x.FilePath,
            _ => (IReadOnlyList<DbfFieldMapping>)CurrentMappings());

        var request = new DbfImportRequest(
            _scan.Files,
            mappingsByFile,
            _updateExistingCheck.Checked,
            _ignoreDuplicatesCheck.Checked,
            _partialImportCheck.Checked,
            _partialImportCheck.Checked ? (int)_maxRecords.Value : null);

        var result = _service.Import(request);
        _resultBox.Text = string.Join(
            Environment.NewLine,
            result.Files.Select(x => $"{x.FileName}: importados {x.ImportedRecords}, ignorados {x.IgnoredRecords}, erros {x.ErrorRecords}"));
    }

    private List<DbfFieldMapping> CurrentMappings()
    {
        if (_mappingGrid.DataSource is List<DbfFieldMapping> direct)
        {
            return direct;
        }

        return _mappingGrid.Rows
            .Cast<DataGridViewRow>()
            .Where(x => x.DataBoundItem is DbfFieldMapping)
            .Select(x => (DbfFieldMapping)x.DataBoundItem)
            .ToList();
    }
}
