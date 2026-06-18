using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using DbfDataReader;
using MaterialPro.Application;
using MaterialPro.Domain;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MaterialPro.Infrastructure;

public sealed class ProductImportService : IProductImportService
{
    private readonly MaterialProDbContext _db;

    public ProductImportService(MaterialProDbContext db)
    {
        _db = db;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public ProductImportResult ImportCsv(string filePath, ProductImportOptions options)
    {
        var rows = File.ReadAllLines(filePath, Encoding.UTF8)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        if (rows.Count == 0)
        {
            return new ProductImportResult(0, 0, 0, 0, 0, Array.Empty<ProductImportIssue>());
        }

        var separator = rows[0].Contains(';') ? ';' : ',';
        var headers = SplitCsv(rows[0], separator);
        return ImportRows(rows.Skip(1).Select((line, index) => ToDictionary(headers, SplitCsv(line, separator), index + 2)), options);
    }

    public ProductImportResult ImportExcel(string filePath, ProductImportOptions options)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed();
        if (used is null)
        {
            return new ProductImportResult(0, 0, 0, 0, 0, Array.Empty<ProductImportIssue>());
        }

        var firstRow = used.FirstRowUsed();
        var headers = firstRow.Cells().Select(x => x.GetString().Trim()).ToList();
        var rows = used.RowsUsed()
            .Skip(1)
            .Select(row => ToDictionary(headers, row.Cells(1, headers.Count).Select(x => x.GetString()).ToList(), row.RowNumber()));
        return ImportRows(rows, options);
    }

    public ProductImportResult ImportDbf(string filePath, ProductImportOptions options)
    {
        var rows = new List<ProductImportRow>();
        using var table = new DbfTable(filePath, Encoding.GetEncoding(1252));
        var record = new DbfRecord(table);
        var rowNumber = 1;
        while (table.Read(record))
        {
            rowNumber++;
            if (record.IsDeleted)
            {
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < table.Columns.Count; i++)
            {
                row[table.Columns[i].ColumnName] = record.Values[i].GetValue()?.ToString()?.Trim() ?? string.Empty;
            }

            rows.Add(new ProductImportRow(rowNumber, row));
        }

        return ImportRows(rows, options);
    }

    private ProductImportResult ImportRows(IEnumerable<ProductImportRow> inputRows, ProductImportOptions options)
    {
        var total = 0;
        var imported = 0;
        var updated = 0;
        var ignored = 0;
        var errors = 0;
        var issues = new List<ProductImportIssue>();

        foreach (var input in inputRows)
        {
            total++;
            try
            {
                var request = BuildRequest(input.Values);
                if (string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.Name))
                {
                    errors++;
                    issues.Add(new ProductImportIssue(input.RowNumber, "Erro", "SKU e Nome sao obrigatorios."));
                    continue;
                }

                var existing = _db.Products.FirstOrDefault(x => x.Sku == request.Sku);
                if (existing is not null)
                {
                    if (!options.UpdateExisting)
                    {
                        if (!options.IgnoreDuplicates)
                        {
                            issues.Add(new ProductImportIssue(input.RowNumber, "Aviso", $"Produto duplicado: {request.Sku}."));
                        }

                        ignored++;
                        continue;
                    }

                    Apply(existing, request);
                    updated++;
                    continue;
                }

                var product = new Product();
                Apply(product, request);
                _db.Products.Add(product);
                imported++;
            }
            catch (Exception ex)
            {
                errors++;
                issues.Add(new ProductImportIssue(input.RowNumber, "Erro", ex.Message));
            }
        }

        _db.SaveChanges();
        return new ProductImportResult(total, imported, updated, ignored, errors, issues);
    }

    private static ProductUpsertRequest BuildRequest(IReadOnlyDictionary<string, string> row)
    {
        return new ProductUpsertRequest(
            Value(row, "Sku", "SKU", "Codigo", "CodigoProduto", "CODIGO"),
            Value(row, "Name", "Nome", "Descricao", "DESCRICAO"),
            Default(Value(row, "Unit", "Unidade", "UNIDADE", "UN"), "UN"),
            DecimalValue(row, "SalePrice", "PrecoVenda", "Venda", "PRECO", "PRVENDA"),
            DecimalValue(row, "CostPrice", "PrecoCusto", "Custo", "PRCUSTO"),
            DecimalValue(row, "MinimumStock", "EstoqueMinimo", "Minimo", "ESTMIN"),
            Value(row, "Barcode", "CodigoBarras", "Barras", "EAN", "CODBARRA"),
            Value(row, "Description", "DescricaoLonga", "Observacao"),
            Value(row, "Category", "Categoria", "Grupo", "GRUPO"),
            Value(row, "Brand", "Marca", "MARCA"),
            Value(row, "Ncm", "NCM"),
            Value(row, "Location", "Localizacao", "Prateleira", "LOCAL"));
    }

    private static void Apply(Product product, ProductUpsertRequest request)
    {
        product.Sku = request.Sku.Trim();
        product.Name = request.Name.Trim();
        product.Description = request.Description.Trim();
        product.Category = request.Category.Trim();
        product.Brand = request.Brand.Trim();
        product.Unit = Default(request.Unit.Trim(), "UN");
        product.SalePrice = request.SalePrice;
        product.CostPrice = request.CostPrice;
        product.MinimumStock = request.MinimumStock;
        product.Barcode = request.Barcode.Trim();
        product.Ncm = request.Ncm.Trim();
        product.Location = request.Location.Trim();
        product.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static ProductImportRow ToDictionary(IReadOnlyList<string> headers, IReadOnlyList<string> values, int rowNumber)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(headers[i]))
            {
                continue;
            }

            row[headers[i]] = i < values.Count ? values[i].Trim() : string.Empty;
        }

        return new ProductImportRow(rowNumber, row);
    }

    private static List<string> SplitCsv(string line, char separator)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (ch == separator && !quoted)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }

    private static string Value(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string Default(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static decimal DecimalValue(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        var value = Value(row, keys).Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase).Trim().Replace(",", ".");
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    }

    private sealed record ProductImportRow(int RowNumber, IReadOnlyDictionary<string, string> Values);
}

public sealed class ProductReportService : IProductReportService
{
    private readonly MaterialProDbContext _db;

    public ProductReportService(MaterialProDbContext db) => _db = db;

    public byte[] ExportPdf(ProductReportRequest request)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var products = Query(request).ToList();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.Header().Text("Relatorio de Produtos").SemiBold().FontSize(18);
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    Header(table, "SKU");
                    Header(table, "Produto");
                    Header(table, "Estoque");
                    Header(table, "Minimo");
                    Header(table, "Venda");

                    foreach (var product in products)
                    {
                        table.Cell().Text(product.Sku);
                        table.Cell().Text(product.Name);
                        table.Cell().AlignRight().Text(product.StockQuantity.ToString("N3", CultureInfo.GetCultureInfo("pt-BR")));
                        table.Cell().AlignRight().Text(product.MinimumStock.ToString("N3", CultureInfo.GetCultureInfo("pt-BR")));
                        table.Cell().AlignRight().Text(product.SalePrice.ToString("C", CultureInfo.GetCultureInfo("pt-BR")));
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportExcel(ProductReportRequest request)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Produtos");
        var headers = new[] { "SKU", "Nome", "Descricao", "Categoria", "Marca", "Unidade", "PrecoVenda", "PrecoCusto", "Estoque", "Minimo", "CodigoBarras", "NCM", "Localizacao", "Ativo" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var product in Query(request))
        {
            sheet.Cell(row, 1).Value = product.Sku;
            sheet.Cell(row, 2).Value = product.Name;
            sheet.Cell(row, 3).Value = product.Description;
            sheet.Cell(row, 4).Value = product.Category;
            sheet.Cell(row, 5).Value = product.Brand;
            sheet.Cell(row, 6).Value = product.Unit;
            sheet.Cell(row, 7).Value = product.SalePrice;
            sheet.Cell(row, 8).Value = product.CostPrice;
            sheet.Cell(row, 9).Value = product.StockQuantity;
            sheet.Cell(row, 10).Value = product.MinimumStock;
            sheet.Cell(row, 11).Value = product.Barcode;
            sheet.Cell(row, 12).Value = product.Ncm;
            sheet.Cell(row, 13).Value = product.Location;
            sheet.Cell(row, 14).Value = product.IsActive ? "Sim" : "Nao";
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private IQueryable<Product> Query(ProductReportRequest request)
    {
        var term = request.Term.Trim().ToLower();
        var query = _db.Products.AsNoTracking().AsQueryable();
        if (request.OnlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (request.OnlyLowStock)
        {
            query = query.Where(x => x.StockQuantity <= x.MinimumStock);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x => x.Sku.ToLower().Contains(term) || x.Name.ToLower().Contains(term) || x.Category.ToLower().Contains(term) || x.Brand.ToLower().Contains(term));
        }

        return query.OrderBy(x => x.Name);
    }

    private static void Header(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(text).SemiBold();
    }
}
