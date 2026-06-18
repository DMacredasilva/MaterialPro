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

public sealed class StockReportService : IStockReportService
{
    private readonly MaterialProDbContext _db;
    private readonly InventoryService _inventory;

    public StockReportService(MaterialProDbContext db)
    {
        _db = db;
        _inventory = new InventoryService(db);
    }

    public byte[] ExportPdf(StockReportRequest request)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var rows = Query(request);
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.Header().Text("Relatorio de Estoque").SemiBold().FontSize(18);
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
                    Header(table, "Fisico");
                    Header(table, "Reservado");
                    Header(table, "Disponivel");

                    foreach (var item in rows)
                    {
                        table.Cell().Text(item.Sku);
                        table.Cell().Text(item.Name);
                        table.Cell().AlignRight().Text(item.PhysicalStock.ToString("N3", CultureInfo.GetCultureInfo("pt-BR")));
                        table.Cell().AlignRight().Text(item.ReservedStock.ToString("N3", CultureInfo.GetCultureInfo("pt-BR")));
                        table.Cell().AlignRight().Text(item.AvailableStock.ToString("N3", CultureInfo.GetCultureInfo("pt-BR")));
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportExcel(StockReportRequest request)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Estoque");
        var headers = new[] { "SKU", "Produto", "Categoria", "Marca", "Fornecedor", "Fisico", "Reservado", "Disponivel", "Minimo", "UltimaEntrada", "UltimaSaida" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var item in Query(request))
        {
            sheet.Cell(row, 1).Value = item.Sku;
            sheet.Cell(row, 2).Value = item.Name;
            sheet.Cell(row, 3).Value = item.Category;
            sheet.Cell(row, 4).Value = item.Brand;
            sheet.Cell(row, 5).Value = item.SupplierName;
            sheet.Cell(row, 6).Value = item.PhysicalStock;
            sheet.Cell(row, 7).Value = item.ReservedStock;
            sheet.Cell(row, 8).Value = item.AvailableStock;
            sheet.Cell(row, 9).Value = item.MinimumStock;
            sheet.Cell(row, 10).Value = item.LastEntryAtUtc;
            sheet.Cell(row, 11).Value = item.LastExitAtUtc;
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private IReadOnlyList<StockPositionItem> Query(StockReportRequest request)
    {
        if (request.OnlyEntries || request.OnlyExits)
        {
            var types = request.OnlyEntries ? InventoryService.EntryTypes() : InventoryService.ExitTypes();
            var productIds = _db.StockMovements.AsNoTracking().Where(x => types.Contains(x.Type)).Select(x => x.ProductId).Distinct().ToHashSet();
            return _inventory.Query(new StockQueryRequest(request.Term, SupplierId: request.SupplierId))
                .Where(x => productIds.Contains(x.ProductId))
                .ToList();
        }

        return _inventory.Query(new StockQueryRequest(request.Term, SupplierId: request.SupplierId, OnlyLowStock: request.OnlyLowStock, OnlyZeroStock: request.OnlyZeroStock));
    }

    private static void Header(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(text).SemiBold();
    }
}

public sealed class StockImportService : IStockImportService
{
    private readonly MaterialProDbContext _db;
    private readonly InventoryService _inventory;

    public StockImportService(MaterialProDbContext db, ISecurityService? security = null)
    {
        _db = db;
        _inventory = new InventoryService(db, security);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public StockImportResult ImportCsv(string filePath, StockImportOptions options)
    {
        var rows = File.ReadAllLines(filePath, Encoding.UTF8).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (rows.Count == 0)
        {
            return Empty();
        }

        var separator = rows[0].Contains(';') ? ';' : ',';
        var headers = SplitCsv(rows[0], separator);
        return ImportRows(rows.Skip(1).Select((line, index) => ToDictionary(headers, SplitCsv(line, separator), index + 2)), options);
    }

    public StockImportResult ImportExcel(string filePath, StockImportOptions options)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed();
        if (used is null)
        {
            return Empty();
        }

        var headers = used.FirstRowUsed().Cells().Select(x => x.GetString().Trim()).ToList();
        return ImportRows(used.RowsUsed().Skip(1).Select(row => ToDictionary(headers, row.Cells(1, headers.Count).Select(x => x.GetString()).ToList(), row.RowNumber())), options);
    }

    public StockImportResult ImportDbf(string filePath, StockImportOptions options)
    {
        var rows = new List<StockImportRow>();
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

            rows.Add(new StockImportRow(rowNumber, row));
        }

        return ImportRows(rows, options);
    }

    private StockImportResult ImportRows(IEnumerable<StockImportRow> inputRows, StockImportOptions options)
    {
        var total = 0;
        var imported = 0;
        var updated = 0;
        var ignored = 0;
        var errors = 0;
        var issues = new List<StockImportIssue>();

        foreach (var input in inputRows)
        {
            total++;
            try
            {
                var lookup = Value(input.Values, "ProductLookup", "Sku", "SKU", "Codigo", "CODIGO", "Barcode", "CODBARRA");
                var product = _db.Products.FirstOrDefault(x => x.Sku == lookup || x.Barcode == lookup || x.Name == lookup);
                if (product is null)
                {
                    ignored++;
                    issues.Add(new StockImportIssue(input.RowNumber, "Aviso", $"Produto nao encontrado: {lookup}."));
                    continue;
                }

                var quantity = DecimalValue(input.Values, "Quantity", "Quantidade", "ESTOQUE", "Saldo");
                if (quantity == 0)
                {
                    ignored++;
                    continue;
                }

                var reason = Default(Value(input.Values, "Reason", "Motivo"), "Importacao de estoque");
                var reference = Default(Value(input.Values, "Reference", "Referencia"), "IMPORT");
                var setAbsolute = BoolValue(input.Values, "SetAbsolute", "Absoluto");
                if (setAbsolute)
                {
                    _inventory.AdjustStock(new StockAdjustRequest(product.Id, quantity, reason, Warehouse: Default(Value(input.Values, "Warehouse", "Deposito"), "Loja")));
                    updated++;
                }
                else
                {
                    var type = quantity > 0 ? StockMovementType.ManualEntry : StockMovementType.AdjustmentExit;
                    var request = new StockMoveRequest(product.Id, quantity, type, reason, reference, Warehouse: Default(Value(input.Values, "Warehouse", "Deposito"), "Loja"), AllowNegative: options.AllowNegative);
                    if (quantity > 0)
                    {
                        _inventory.EnterStock(request);
                    }
                    else
                    {
                        _inventory.ExitStock(request);
                    }
                    imported++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                issues.Add(new StockImportIssue(input.RowNumber, "Erro", ex.Message));
            }
        }

        return new StockImportResult(total, imported, updated, ignored, errors, issues);
    }

    private static StockImportResult Empty() => new(0, 0, 0, 0, 0, Array.Empty<StockImportIssue>());

    private static StockImportRow ToDictionary(IReadOnlyList<string> headers, IReadOnlyList<string> values, int rowNumber)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(headers[i]))
            {
                row[headers[i]] = i < values.Count ? values[i].Trim() : string.Empty;
            }
        }

        return new StockImportRow(rowNumber, row);
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

    private static bool BoolValue(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        var value = Value(row, keys);
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("sim", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record StockImportRow(int RowNumber, IReadOnlyDictionary<string, string> Values);
}
