using System.Globalization;
using System.Text;
using System.Text.Json;
using DbfDataReader;
using MaterialPro.Application;
using MaterialPro.Domain;
using Microsoft.EntityFrameworkCore;

namespace MaterialPro.Infrastructure;

public sealed class DbfImportService : IDbfImportService
{
    private readonly MaterialProDbContext _db;

    private static readonly IReadOnlyDictionary<string, DbfImportTable> KnownTables =
        new Dictionary<string, DbfImportTable>(StringComparer.OrdinalIgnoreCase)
        {
            ["CLIENTES.DBF"] = DbfImportTable.Clients,
            ["PRODUTOS.DBF"] = DbfImportTable.Products,
            ["FORNECEDORES.DBF"] = DbfImportTable.Suppliers,
            ["ESTOQUE.DBF"] = DbfImportTable.Stock,
            ["VENDAS.DBF"] = DbfImportTable.Sales,
            ["RECEBER.DBF"] = DbfImportTable.Receivable,
            ["PAGAR.DBF"] = DbfImportTable.Payable,
            ["DUPLICATAS.DBF"] = DbfImportTable.Duplicates
        };

    public DbfImportService(MaterialProDbContext db)
    {
        _db = db;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public DbfImportScanResult Scan(DbfImportSelection selection)
    {
        var files = selection.Kind == DbfImportSelectionKind.File
            ? new[] { selection.Path }
            : Directory.GetFiles(selection.Path, "*.dbf", SearchOption.TopDirectoryOnly);

        var results = files
            .Where(File.Exists)
            .Select(ReadSourceFile)
            .Where(x => x.Table != DbfImportTable.Unknown)
            .OrderBy(x => x.FileName)
            .ToList();

        return new DbfImportScanResult(results);
    }

    public DbfImportPreview Preview(string dbfPath)
    {
        var source = ReadSourceFile(dbfPath);
        var fields = ReadFields(dbfPath);
        var mappings = BuildSuggestedMappings(source.Table, fields);
        return new DbfImportPreview(source, fields, mappings);
    }

    public DbfImportValidationResult Validate(string dbfPath, IReadOnlyList<DbfFieldMapping> mappings, int? maxRecords = null)
    {
        var source = ReadSourceFile(dbfPath);
        var issues = new List<DbfValidationIssue>();
        var enabledMappings = mappings.Where(x => x.Enabled).ToList();

        if (enabledMappings.Count == 0)
        {
            issues.Add(new DbfValidationIssue("Erro", "Nenhum campo mapeado."));
        }

        var rows = ReadRows(dbfPath, maxRecords);
        var rowNumber = 0;
        foreach (var row in rows)
        {
            rowNumber++;
            foreach (var mapping in enabledMappings)
            {
                if (!row.ContainsKey(mapping.SourceField))
                {
                    issues.Add(new DbfValidationIssue("Erro", $"Campo origem ausente: {mapping.SourceField}", rowNumber));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row[mapping.SourceField]))
                {
                    issues.Add(new DbfValidationIssue("Aviso", $"Campo vazio: {mapping.SourceField}", rowNumber));
                }
            }
        }

        return new DbfImportValidationResult(source, source.RecordCount, issues);
    }

    public DbfImportResult Import(DbfImportRequest request)
    {
        var startedAt = DateTime.UtcNow;
        var reports = new List<DbfImportFileReport>();
        var importRoot = Path.Combine(AppContext.BaseDirectory, "imports", startedAt.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(importRoot);

        foreach (var source in request.Sources)
        {
            var fileRoot = Path.Combine(importRoot, Path.GetFileNameWithoutExtension(source.FileName));
            var backupRoot = Path.Combine(fileRoot, "backup");
            var logRoot = Path.Combine(fileRoot, "logs");
            Directory.CreateDirectory(backupRoot);
            Directory.CreateDirectory(logRoot);

            BackupTable(source.Table, backupRoot);
            var logPath = Path.Combine(logRoot, "import-log.json");

            var mappings = request.MappingsByFile.TryGetValue(source.FilePath, out var fileMappings)
                ? fileMappings.Where(x => x.Enabled).ToList()
                : new List<DbfFieldMapping>();

            var imported = 0;
            var ignored = 0;
            var errors = 0;
            var issues = new List<DbfValidationIssue>();
            var rows = ReadRows(source.FilePath, request.PartialImport ? request.MaxRecords : null);

            foreach (var row in rows)
            {
                try
                {
                    var mapped = MapRow(row, mappings);
                    var status = ImportMappedRow(source.Table, mapped, request.UpdateExisting, request.IgnoreDuplicates);
                    if (status == ImportStatus.Imported) imported++;
                    else ignored++;
                }
                catch (Exception ex)
                {
                    errors++;
                    issues.Add(new DbfValidationIssue("Erro", ex.Message));
                }
            }

            File.WriteAllText(logPath, JsonSerializer.Serialize(issues, new JsonSerializerOptions { WriteIndented = true }));

            reports.Add(new DbfImportFileReport(
                source.FileName,
                source.Table,
                source.RecordCount,
                imported,
                ignored,
                errors,
                backupRoot,
                logPath));
        }

        _db.SaveChanges();
        var finishedAt = DateTime.UtcNow;
        var summaryLogFile = Path.Combine(importRoot, "summary.json");
        File.WriteAllText(summaryLogFile, JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
        return new DbfImportResult(startedAt, finishedAt, reports, summaryLogFile);
    }

    private DbfImportSourceFile ReadSourceFile(string path)
    {
        using var table = new DbfTable(path, Encoding.GetEncoding(1252));
        var header = table.Header;
        var fileName = Path.GetFileName(path);
        return new DbfImportSourceFile(
            path,
            fileName,
            KnownTables.TryGetValue(fileName, out var tableKind) ? tableKind : DbfImportTable.Unknown,
            header.RecordCount > int.MaxValue ? int.MaxValue : (int)header.RecordCount,
            header.VersionDescription ?? "Desconhecido",
            table.Memo is not null);
    }

    private static IReadOnlyList<DbfFieldInfo> ReadFields(string path)
    {
        using var table = new DbfTable(path, Encoding.GetEncoding(1252));
        return table.Columns
            .Select(x => new DbfFieldInfo(x.ColumnName, x.ColumnType.ToString(), x.Length, x.DecimalCount))
            .ToList();
    }

    private static IReadOnlyList<DbfFieldMapping> BuildSuggestedMappings(DbfImportTable table, IReadOnlyList<DbfFieldInfo> fields)
    {
        var targets = table switch
        {
            DbfImportTable.Clients => new[] { "Code", "FullName", "DocumentNumber", "StateRegistration", "Phone", "WhatsApp", "Email", "ZipCode", "Address", "AddressNumber", "Complement", "District", "City", "State", "CreditLimit", "Notes" },
            DbfImportTable.Products => new[] { "Sku", "Name", "Description", "Category", "Brand", "Unit", "SalePrice", "CostPrice", "MinimumStock", "Barcode", "Ncm", "Location" },
            DbfImportTable.Suppliers => new[] { "Name", "Cnpj", "Phone", "Email", "Address" },
            DbfImportTable.Stock => new[] { "ProductLookup", "Quantity", "Reason", "Reference" },
            DbfImportTable.Sales => new[] { "ReceiptNumber", "CustomerDocument", "TotalAmount", "PaidAmount", "DiscountAmount", "PaymentMethod", "SoldAtUtc" },
            DbfImportTable.Receivable => new[] { "Number", "Amount", "DueDateUtc", "Description" },
            DbfImportTable.Payable => new[] { "Number", "SupplierName", "OriginalAmount", "DueDateUtc", "Description", "PaymentMethod" },
            DbfImportTable.Duplicates => new[] { "Number", "Amount", "DueDateUtc", "Description" },
            _ => Array.Empty<string>()
        };

        return fields.Select(field =>
        {
            var match = targets.FirstOrDefault(target => string.Equals(target, field.Name, StringComparison.OrdinalIgnoreCase))
                ?? targets.FirstOrDefault(target => field.Name.Contains(target, StringComparison.OrdinalIgnoreCase));
            return new DbfFieldMapping(field.Name, match ?? string.Empty, !string.IsNullOrWhiteSpace(match));
        }).ToList();
    }

    private static List<Dictionary<string, string>> ReadRows(string path, int? maxRecords)
    {
        var rows = new List<Dictionary<string, string>>();
        using var table = new DbfTable(path, Encoding.GetEncoding(1252));
        var record = new DbfRecord(table);
        while (table.Read(record))
        {
            if (record.IsDeleted)
            {
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < table.Columns.Count; i++)
            {
                row[table.Columns[i].ColumnName] = record.Values[i].GetValue()?.ToString()?.Trim() ?? string.Empty;
            }

            rows.Add(row);
            if (maxRecords.HasValue && rows.Count >= maxRecords.Value)
            {
                break;
            }
        }

        return rows;
    }

    private static Dictionary<string, string> MapRow(Dictionary<string, string> row, IReadOnlyList<DbfFieldMapping> mappings)
    {
        var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in mappings)
        {
            if (!row.TryGetValue(mapping.SourceField, out var value))
            {
                continue;
            }

            mapped[mapping.TargetField] = value;
        }
        return mapped;
    }

    private ImportStatus ImportMappedRow(DbfImportTable table, Dictionary<string, string> row, bool updateExisting, bool ignoreDuplicates)
    {
        return table switch
        {
            DbfImportTable.Clients => ImportClient(row, updateExisting, ignoreDuplicates),
            DbfImportTable.Products => ImportProduct(row, updateExisting, ignoreDuplicates),
            DbfImportTable.Suppliers => ImportSupplier(row, updateExisting, ignoreDuplicates),
            DbfImportTable.Stock => ImportStock(row),
            DbfImportTable.Sales => ImportSale(row),
            DbfImportTable.Payable => ImportPayable(row, updateExisting, ignoreDuplicates),
            DbfImportTable.Receivable => ImportReceivable(row, ignoreDuplicates),
            DbfImportTable.Duplicates => ImportDuplicate(row, ignoreDuplicates),
            _ => ImportStatus.Ignored
        };
    }

    private ImportStatus ImportClient(Dictionary<string, string> row, bool updateExisting, bool ignoreDuplicates)
    {
        var document = Value(row, "DocumentNumber");
        var existing = !string.IsNullOrWhiteSpace(document)
            ? _db.Customers.FirstOrDefault(x => x.DocumentNumber == document)
            : null;

        if (existing is not null)
        {
            if (ignoreDuplicates && !updateExisting) return ImportStatus.Ignored;
            if (updateExisting)
            {
                existing.FullName = Value(row, "FullName");
                existing.Phone = Value(row, "Phone");
                existing.WhatsApp = Value(row, "WhatsApp");
                existing.Email = Value(row, "Email");
                existing.StateRegistration = Value(row, "StateRegistration");
                existing.ZipCode = Value(row, "ZipCode");
                existing.Address = Value(row, "Address");
                existing.AddressNumber = Value(row, "AddressNumber");
                existing.Complement = Value(row, "Complement");
                existing.District = Value(row, "District");
                existing.City = Value(row, "City");
                existing.State = Value(row, "State").ToUpperInvariant();
                existing.CreditLimit = DecimalValue(row, "CreditLimit");
                existing.Notes = Value(row, "Notes");
                existing.UpdatedAtUtc = DateTime.UtcNow;
                return ImportStatus.Imported;
            }
        }

        _db.Customers.Add(new Customer
        {
            Code = Default(Value(row, "Code"), $"CLI-{(_db.Customers.Count() + 1):D6}"),
            FullName = Value(row, "FullName"),
            DocumentNumber = document,
            StateRegistration = Value(row, "StateRegistration"),
            Phone = Value(row, "Phone"),
            WhatsApp = Value(row, "WhatsApp"),
            Email = Value(row, "Email"),
            ZipCode = Value(row, "ZipCode"),
            Address = Value(row, "Address"),
            AddressNumber = Value(row, "AddressNumber"),
            Complement = Value(row, "Complement"),
            District = Value(row, "District"),
            City = Value(row, "City"),
            State = Value(row, "State").ToUpperInvariant(),
            CreditLimit = DecimalValue(row, "CreditLimit"),
            Notes = Value(row, "Notes")
        });
        return ImportStatus.Imported;
    }

    private ImportStatus ImportProduct(Dictionary<string, string> row, bool updateExisting, bool ignoreDuplicates)
    {
        var sku = Value(row, "Sku");
        var existing = !string.IsNullOrWhiteSpace(sku)
            ? _db.Products.FirstOrDefault(x => x.Sku == sku)
            : null;

        if (existing is not null)
        {
            if (ignoreDuplicates && !updateExisting) return ImportStatus.Ignored;
            if (updateExisting)
            {
                existing.Name = Value(row, "Name");
                existing.Description = Value(row, "Description");
                existing.Category = Value(row, "Category");
                existing.Brand = Value(row, "Brand");
                existing.Unit = Default(Value(row, "Unit"), "UN");
                existing.SalePrice = DecimalValue(row, "SalePrice");
                existing.CostPrice = DecimalValue(row, "CostPrice");
                existing.MinimumStock = DecimalValue(row, "MinimumStock");
                existing.Barcode = Value(row, "Barcode");
                existing.Ncm = Value(row, "Ncm");
                existing.Location = Value(row, "Location");
                existing.UpdatedAtUtc = DateTime.UtcNow;
                return ImportStatus.Imported;
            }
        }

        _db.Products.Add(new Product
        {
            Sku = sku,
            Name = Value(row, "Name"),
            Description = Value(row, "Description"),
            Category = Value(row, "Category"),
            Brand = Value(row, "Brand"),
            Unit = Default(Value(row, "Unit"), "UN"),
            SalePrice = DecimalValue(row, "SalePrice"),
            CostPrice = DecimalValue(row, "CostPrice"),
            MinimumStock = DecimalValue(row, "MinimumStock"),
            Barcode = Value(row, "Barcode"),
            Ncm = Value(row, "Ncm"),
            Location = Value(row, "Location")
        });
        return ImportStatus.Imported;
    }

    private ImportStatus ImportSupplier(Dictionary<string, string> row, bool updateExisting, bool ignoreDuplicates)
    {
        var cnpj = Value(row, "Cnpj");
        var existing = !string.IsNullOrWhiteSpace(cnpj)
            ? _db.Suppliers.FirstOrDefault(x => x.Cnpj == cnpj)
            : null;

        if (existing is not null)
        {
            if (ignoreDuplicates && !updateExisting) return ImportStatus.Ignored;
            if (updateExisting)
            {
                existing.Name = Value(row, "Name");
                existing.Phone = Value(row, "Phone");
                existing.Email = Value(row, "Email");
                existing.Address = Value(row, "Address");
                existing.UpdatedAtUtc = DateTime.UtcNow;
                return ImportStatus.Imported;
            }
        }

        _db.Suppliers.Add(new Supplier
        {
            Name = Value(row, "Name"),
            Cnpj = cnpj,
            Phone = Value(row, "Phone"),
            Email = Value(row, "Email"),
            Address = Value(row, "Address")
        });
        return ImportStatus.Imported;
    }

    private ImportStatus ImportStock(Dictionary<string, string> row)
    {
        var lookup = Default(Value(row, "ProductLookup"), Value(row, "Sku"));
        var product = _db.Products.FirstOrDefault(x => x.Sku == lookup || x.Barcode == lookup || x.Name == lookup);
        if (product is null)
        {
            throw new InvalidOperationException($"Produto não encontrado para estoque: {lookup}");
        }

        var quantity = DecimalValue(row, "Quantity");
        product.StockQuantity += quantity;
        product.UpdatedAtUtc = DateTime.UtcNow;
        _db.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            Quantity = quantity,
            Reason = Default(Value(row, "Reason"), "Importação DBF"),
            Reference = Default(Value(row, "Reference"), "DBF")
        });
        return ImportStatus.Imported;
    }

    private ImportStatus ImportSale(Dictionary<string, string> row)
    {
        _db.Sales.Add(new Sale
        {
            ReceiptNumber = Value(row, "ReceiptNumber"),
            PaymentMethod = Default(Value(row, "PaymentMethod"), "Importado"),
            TotalAmount = DecimalValue(row, "TotalAmount"),
            PaidAmount = DecimalValue(row, "PaidAmount"),
            DiscountAmount = DecimalValue(row, "DiscountAmount"),
            ChangeAmount = Math.Max(0, DecimalValue(row, "PaidAmount") - DecimalValue(row, "TotalAmount")),
            SoldAtUtc = DateTimeValue(row, "SoldAtUtc") ?? DateTime.UtcNow
        });
        return ImportStatus.Imported;
    }

    private ImportStatus ImportPayable(Dictionary<string, string> row, bool updateExisting, bool ignoreDuplicates)
    {
        var number = Value(row, "Number");
        var existing = _db.AccountsPayable.FirstOrDefault(x => x.Number == number);
        if (existing is not null)
        {
            if (ignoreDuplicates && !updateExisting) return ImportStatus.Ignored;
            if (updateExisting)
            {
                existing.SupplierName = Value(row, "SupplierName");
                existing.Description = Value(row, "Description");
                existing.OriginalAmount = DecimalValue(row, "OriginalAmount");
                existing.BalanceAmount = existing.OriginalAmount;
                existing.DueDateUtc = DateTimeValue(row, "DueDateUtc") ?? DateTime.UtcNow;
                existing.PaymentMethod = Value(row, "PaymentMethod");
                existing.UpdatedAtUtc = DateTime.UtcNow;
                return ImportStatus.Imported;
            }
        }

        _db.AccountsPayable.Add(new AccountPayable
        {
            Number = number,
            SupplierName = Value(row, "SupplierName"),
            Description = Value(row, "Description"),
            OriginalAmount = DecimalValue(row, "OriginalAmount"),
            BalanceAmount = DecimalValue(row, "OriginalAmount"),
            DueDateUtc = DateTimeValue(row, "DueDateUtc") ?? DateTime.UtcNow,
            PaymentMethod = Value(row, "PaymentMethod")
        });
        return ImportStatus.Imported;
    }

    private ImportStatus ImportReceivable(Dictionary<string, string> row, bool ignoreDuplicates)
    {
        return ImportDuplicate(row, ignoreDuplicates);
    }

    private ImportStatus ImportDuplicate(Dictionary<string, string> row, bool ignoreDuplicates)
    {
        var number = Value(row, "Number");
        var existing = _db.Duplicates.FirstOrDefault(x => x.Number == number);
        if (existing is not null && ignoreDuplicates)
        {
            return ImportStatus.Ignored;
        }

        _db.Duplicates.Add(new Duplicate
        {
            Number = number,
            Type = FinancialType.Receivable,
            Description = Value(row, "Description"),
            Amount = DecimalValue(row, "Amount"),
            BalanceAmount = DecimalValue(row, "Amount"),
            DueDateUtc = DateTimeValue(row, "DueDateUtc") ?? DateTime.UtcNow
        });
        return ImportStatus.Imported;
    }

    private void BackupTable(DbfImportTable table, string backupRoot)
    {
        var file = Path.Combine(backupRoot, $"{table}.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        var payload = table switch
        {
            DbfImportTable.Clients => _db.Customers.AsNoTracking().Select(x => (object)x).ToList(),
            DbfImportTable.Products => _db.Products.AsNoTracking().Select(x => (object)x).ToList(),
            DbfImportTable.Suppliers => _db.Suppliers.AsNoTracking().Select(x => (object)x).ToList(),
            DbfImportTable.Stock => _db.StockMovements.AsNoTracking().Select(x => (object)x).ToList(),
            DbfImportTable.Sales => _db.Sales.AsNoTracking().Select(x => (object)x).ToList(),
            DbfImportTable.Payable => _db.AccountsPayable.AsNoTracking().Select(x => (object)x).ToList(),
            DbfImportTable.Receivable => _db.Duplicates.AsNoTracking().Select(x => (object)x).ToList(),
            DbfImportTable.Duplicates => _db.Duplicates.AsNoTracking().Select(x => (object)x).ToList(),
            _ => new List<object>()
        };
        File.WriteAllText(file, JsonSerializer.Serialize(payload, options));
    }

    private static string Value(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static string Default(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static decimal DecimalValue(IReadOnlyDictionary<string, string> row, string key)
    {
        var value = Value(row, key).Replace(",", ".");
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    }

    private static DateTime? DateTimeValue(IReadOnlyDictionary<string, string> row, string key)
    {
        var value = Value(row, key);
        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var date))
        {
            return date;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date;
        }

        return null;
    }

    private enum ImportStatus
    {
        Imported,
        Ignored
    }
}
