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

public sealed class SupplierImportService : ISupplierImportService
{
    private readonly MaterialProDbContext _db;
    private readonly SupplierService _service;

    public SupplierImportService(MaterialProDbContext db, ISecurityService? security = null)
    {
        _db = db;
        _service = new SupplierService(db, security: security);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public SupplierImportResult ImportCsv(string filePath, SupplierImportOptions options)
    {
        var rows = File.ReadAllLines(filePath, Encoding.UTF8).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (rows.Count == 0)
        {
            return new SupplierImportResult(0, 0, 0, 0, 0, Array.Empty<SupplierImportIssue>());
        }

        var separator = rows[0].Contains(';') ? ';' : ',';
        var headers = SplitCsv(rows[0], separator);
        return ImportRows(rows.Skip(1).Select((line, index) => ToDictionary(headers, SplitCsv(line, separator), index + 2)), options);
    }

    public SupplierImportResult ImportExcel(string filePath, SupplierImportOptions options)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed();
        if (used is null)
        {
            return new SupplierImportResult(0, 0, 0, 0, 0, Array.Empty<SupplierImportIssue>());
        }

        var headers = used.FirstRowUsed().Cells().Select(x => x.GetString().Trim()).ToList();
        var rows = used.RowsUsed().Skip(1).Select(row => ToDictionary(headers, row.Cells(1, headers.Count).Select(x => x.GetString()).ToList(), row.RowNumber()));
        return ImportRows(rows, options);
    }

    public SupplierImportResult ImportDbf(string filePath, SupplierImportOptions options)
    {
        var rows = new List<SupplierImportRow>();
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

            rows.Add(new SupplierImportRow(rowNumber, row));
        }

        return ImportRows(rows, options);
    }

    private SupplierImportResult ImportRows(IEnumerable<SupplierImportRow> inputRows, SupplierImportOptions options)
    {
        var total = 0;
        var imported = 0;
        var updated = 0;
        var ignored = 0;
        var errors = 0;
        var issues = new List<SupplierImportIssue>();

        foreach (var input in inputRows)
        {
            total++;
            try
            {
                var request = BuildRequest(input.Values);
                var key = string.IsNullOrWhiteSpace(request.Cnpj) ? request.Code : request.Cnpj;
                var existing = string.IsNullOrWhiteSpace(key) ? null : _service.FindByCodeOrCnpj(key);
                if (existing is not null)
                {
                    if (!options.UpdateExisting)
                    {
                        ignored++;
                        if (!options.IgnoreDuplicates)
                        {
                            issues.Add(new SupplierImportIssue(input.RowNumber, "Aviso", $"Fornecedor duplicado: {key}."));
                        }

                        continue;
                    }

                    _service.Update(existing.Id, request);
                    updated++;
                    continue;
                }

                _service.Create(request);
                imported++;
            }
            catch (Exception ex)
            {
                errors++;
                issues.Add(new SupplierImportIssue(input.RowNumber, "Erro", ex.Message));
            }
        }

        _db.SaveChanges();
        return new SupplierImportResult(total, imported, updated, ignored, errors, issues);
    }

    private static SupplierUpsertRequest BuildRequest(IReadOnlyDictionary<string, string> row)
    {
        var tipo = Value(row, "tipo_pessoa", "TipoPessoa", "TIPO", "Pessoa");
        var personType = tipo.StartsWith("F", StringComparison.OrdinalIgnoreCase) ? PersonType.Fisica : PersonType.Juridica;
        return new SupplierUpsertRequest(
            Default(Value(row, "nome_fantasia", "NomeFantasia", "Nome", "NAME", "FANTASIA"), Value(row, "razao_social", "RazaoSocial", "RAZAO")),
            Value(row, "cnpj", "CNPJ", "Documento"),
            Value(row, "telefone", "Telefone", "FONE", "Phone"),
            Value(row, "email", "E-mail", "EMAIL"),
            Value(row, "endereco", "Endereco", "Rua"),
            Code: Value(row, "codigo", "Codigo", "CODE"),
            PersonType: personType,
            FantasyName: Value(row, "nome_fantasia", "NomeFantasia", "FANTASIA"),
            LegalName: Value(row, "razao_social", "RazaoSocial", "RAZAO"),
            StateRegistration: Value(row, "inscricao_estadual", "IE", "InscricaoEstadual"),
            MunicipalRegistration: Value(row, "inscricao_municipal", "IM", "InscricaoMunicipal"),
            MobilePhone: Value(row, "celular", "Celular"),
            WhatsApp: Value(row, "whatsapp", "WhatsApp"),
            Website: Value(row, "site", "Site"),
            ZipCode: Value(row, "cep", "CEP"),
            AddressNumber: Value(row, "numero", "Numero"),
            Complement: Value(row, "complemento", "Complemento"),
            District: Value(row, "bairro", "Bairro"),
            City: Value(row, "cidade", "Cidade"),
            State: Value(row, "estado", "Estado", "UF"),
            ContactName: Value(row, "contato_responsavel", "Contato", "Responsavel"),
            ContactRole: Value(row, "cargo_contato", "Cargo"),
            DefaultPaymentTermDays: IntValue(row, "prazo_pagamento_padrao", "PrazoPagamento", "Prazo"),
            PurchaseLimit: DecimalValue(row, "limite_compra", "LimiteCompra", "Limite"),
            Notes: Value(row, "observacoes", "Observacoes", "Obs"));
    }

    private static SupplierImportRow ToDictionary(IReadOnlyList<string> headers, IReadOnlyList<string> values, int rowNumber)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(headers[i]))
            {
                row[headers[i]] = i < values.Count ? values[i].Trim() : string.Empty;
            }
        }

        return new SupplierImportRow(rowNumber, row);
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

    private static int IntValue(IReadOnlyDictionary<string, string> row, params string[] keys)
        => int.TryParse(Value(row, keys), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static decimal DecimalValue(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        var value = Value(row, keys).Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase).Trim().Replace(",", ".");
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    }

    private sealed record SupplierImportRow(int RowNumber, IReadOnlyDictionary<string, string> Values);
}

public sealed class SupplierReportService : ISupplierReportService
{
    private readonly MaterialProDbContext _db;

    public SupplierReportService(MaterialProDbContext db) => _db = db;

    public byte[] ExportPdf(SupplierReportRequest request)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var suppliers = Query(request).ToList();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.Header().Text("Relatorio de Fornecedores").SemiBold().FontSize(18);
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });

                    Header(table, "Codigo");
                    Header(table, "Fornecedor");
                    Header(table, "CNPJ");
                    Header(table, "Telefone");
                    Header(table, "UF");

                    foreach (var supplier in suppliers)
                    {
                        table.Cell().Text(supplier.Code);
                        table.Cell().Text(DisplayName(supplier));
                        table.Cell().Text(supplier.Cnpj);
                        table.Cell().Text(supplier.Phone);
                        table.Cell().Text(supplier.State);
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportExcel(SupplierReportRequest request)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Fornecedores");
        var headers = Headers();
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var supplier in Query(request))
        {
            var values = Values(supplier);
            for (var i = 0; i < values.Length; i++)
            {
                sheet.Cell(row, i + 1).Value = values[i];
            }

            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] SupplierFichaPdf(Guid supplierId)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var supplier = _db.Suppliers.AsNoTracking().First(x => x.Id == supplierId);
        var products = _db.Products.AsNoTracking().Where(x => x.SupplierId == supplierId).OrderBy(x => x.Name).Take(20).ToList();
        var payables = _db.AccountsPayable.AsNoTracking().Where(x => x.SupplierId == supplierId).OrderByDescending(x => x.DueDateUtc).Take(12).ToList();
        var totalOpen = payables.Where(x => x.Status is FinancialStatus.Open or FinancialStatus.Overdue).Sum(x => x.BalanceAmount);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.Header().Text("Ficha de Fornecedor").SemiBold().FontSize(20);
                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text($"{supplier.Code} - {DisplayName(supplier)}").SemiBold().FontSize(14);
                    column.Item().Text($"Razao social: {supplier.LegalName}");
                    column.Item().Text($"CNPJ: {supplier.Cnpj} | IE: {supplier.StateRegistration} | IM: {supplier.MunicipalRegistration}");
                    column.Item().Text($"Telefone: {supplier.Phone} | Celular: {supplier.MobilePhone} | WhatsApp: {supplier.WhatsApp}");
                    column.Item().Text($"E-mail: {supplier.Email} | Site: {supplier.Website}");
                    column.Item().Text($"Endereco: {supplier.Address}, {supplier.AddressNumber} {supplier.Complement} - {supplier.District}");
                    column.Item().Text($"{supplier.City}/{supplier.State} | CEP {supplier.ZipCode}");
                    column.Item().Text($"Contato: {supplier.ContactName} | Cargo: {supplier.ContactRole}");
                    column.Item().Text($"Prazo: {supplier.DefaultPaymentTermDays} dias | Limite: {supplier.PurchaseLimit.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))} | Aberto: {totalOpen.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))}");
                    column.Item().Text($"Status: {(supplier.IsActive ? "Ativo" : "Inativo")} | Obs: {supplier.Notes}");
                    column.Item().PaddingTop(12).Text("Produtos fornecidos").SemiBold();
                    foreach (var product in products)
                    {
                        column.Item().Text($"{product.Sku} | {product.Name} | Custo {product.CostPrice.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))}");
                    }

                    column.Item().PaddingTop(12).Text("Contas a pagar").SemiBold();
                    foreach (var payable in payables)
                    {
                        column.Item().Text($"{payable.DueDateUtc:dd/MM/yyyy} | {payable.Number} | {payable.BalanceAmount.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))} | {payable.Status}");
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private IQueryable<Supplier> Query(SupplierReportRequest request)
    {
        var term = request.Term.Trim().ToLower();
        var city = request.City.Trim().ToLower();
        var state = request.State.Trim().ToUpperInvariant();
        var query = _db.Suppliers.AsNoTracking().AsQueryable();
        if (request.OnlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(x => x.City.ToLower().Contains(city));
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            query = query.Where(x => x.State == state);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x => x.Code.ToLower().Contains(term) || x.FantasyName.ToLower().Contains(term) || x.LegalName.ToLower().Contains(term) || x.Cnpj.ToLower().Contains(term) || x.Phone.ToLower().Contains(term) || x.WhatsApp.ToLower().Contains(term));
        }

        var now = DateTime.UtcNow;
        if (request.OnlyWithOpenPayables)
        {
            var ids = _db.AccountsPayable.Where(x => x.SupplierId.HasValue && x.Status == FinancialStatus.Open).Select(x => x.SupplierId!.Value);
            query = query.Where(x => ids.Contains(x.Id));
        }

        if (request.OnlyWithOverduePayables)
        {
            var ids = _db.AccountsPayable.Where(x => x.SupplierId.HasValue && (x.Status == FinancialStatus.Overdue || (x.Status == FinancialStatus.Open && x.DueDateUtc < now))).Select(x => x.SupplierId!.Value);
            query = query.Where(x => ids.Contains(x.Id));
        }

        return query.OrderBy(x => x.FantasyName).ThenBy(x => x.LegalName);
    }

    private static string[] Headers()
    {
        return new[]
        {
            "Codigo", "TipoPessoa", "NomeFantasia", "RazaoSocial", "CNPJ", "IE", "IM", "Telefone", "Celular", "WhatsApp",
            "Email", "Site", "CEP", "Endereco", "Numero", "Complemento", "Bairro", "Cidade", "Estado", "Contato",
            "Cargo", "PrazoPagamento", "LimiteCompra", "Observacoes", "Ativo"
        };
    }

    private static string[] Values(Supplier supplier)
    {
        return new[]
        {
            supplier.Code,
            supplier.PersonType.ToString(),
            supplier.FantasyName,
            supplier.LegalName,
            supplier.Cnpj,
            supplier.StateRegistration,
            supplier.MunicipalRegistration,
            supplier.Phone,
            supplier.MobilePhone,
            supplier.WhatsApp,
            supplier.Email,
            supplier.Website,
            supplier.ZipCode,
            supplier.Address,
            supplier.AddressNumber,
            supplier.Complement,
            supplier.District,
            supplier.City,
            supplier.State,
            supplier.ContactName,
            supplier.ContactRole,
            supplier.DefaultPaymentTermDays.ToString(CultureInfo.InvariantCulture),
            supplier.PurchaseLimit.ToString(CultureInfo.InvariantCulture),
            supplier.Notes,
            supplier.IsActive ? "Sim" : "Nao"
        };
    }

    private static string DisplayName(Supplier supplier)
        => string.IsNullOrWhiteSpace(supplier.FantasyName) ? (string.IsNullOrWhiteSpace(supplier.Name) ? supplier.LegalName : supplier.Name) : supplier.FantasyName;

    private static void Header(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(text).SemiBold();
    }
}
