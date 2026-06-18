using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using MaterialPro.Application;
using MaterialPro.Domain;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MaterialPro.Infrastructure;

public sealed class CustomerReportService : ICustomerReportService
{
    private readonly MaterialProDbContext _db;

    public CustomerReportService(MaterialProDbContext db) => _db = db;

    public byte[] ExportPdf(CustomerReportRequest request)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var customers = Query(request).ToList();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.Header().Text("Relatorio de Clientes").SemiBold().FontSize(18);
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    Header(table, "Codigo");
                    Header(table, "Nome");
                    Header(table, "CPF/CNPJ");
                    Header(table, "Telefone");
                    Header(table, "WhatsApp");

                    foreach (var customer in customers)
                    {
                        table.Cell().Text(customer.Code);
                        table.Cell().Text(customer.FullName);
                        table.Cell().Text(customer.DocumentNumber);
                        table.Cell().Text(customer.Phone);
                        table.Cell().Text(customer.WhatsApp);
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportExcel(CustomerReportRequest request)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Clientes");
        var headers = Headers();
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var customer in Query(request))
        {
            WriteRow(sheet, row++, customer);
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportCsv(CustomerReportRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(";", Headers()));
        foreach (var customer in Query(request))
        {
            builder.AppendLine(string.Join(";", Values(customer).Select(Escape)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    public byte[] CustomerFichaPdf(Guid customerId)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var customer = _db.Customers.AsNoTracking().First(x => x.Id == customerId);
        var sales = _db.Sales.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.SoldAtUtc)
            .Take(10)
            .ToList();
        var openBalance = _db.AccountsReceivable.AsNoTracking()
            .Where(x => x.CustomerName == customer.FullName && x.Status == FinancialStatus.Open)
            .Sum(x => x.BalanceAmount);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.Header().Text("Ficha de Cliente").SemiBold().FontSize(20);
                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text($"{customer.Code} - {customer.FullName}").SemiBold().FontSize(14);
                    column.Item().Text($"CPF/CNPJ: {customer.DocumentNumber} | RG/IE: {customer.StateRegistration}");
                    column.Item().Text($"Telefone: {customer.Phone} | WhatsApp: {customer.WhatsApp} | E-mail: {customer.Email}");
                    column.Item().Text($"Endereco: {customer.Address}, {customer.AddressNumber} {customer.Complement} - {customer.District}");
                    column.Item().Text($"{customer.City}/{customer.State} | CEP {customer.ZipCode}");
                    column.Item().Text($"Limite: {customer.CreditLimit.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))} | Aberto: {openBalance.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))}");
                    column.Item().Text($"Status: {(customer.IsActive ? "Ativo" : "Inativo")} {(customer.IsBlocked ? "| Bloqueado" : string.Empty)}");
                    column.Item().Text($"Observacoes: {customer.Notes}");
                    column.Item().PaddingTop(12).Text("Ultimas compras").SemiBold();
                    foreach (var sale in sales)
                    {
                        column.Item().Text($"{sale.SoldAtUtc:dd/MM/yyyy} | {sale.ReceiptNumber} | {sale.TotalAmount.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))} | {sale.PaymentMethod}");
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private IQueryable<Customer> Query(CustomerReportRequest request)
    {
        var term = request.Term.Trim().ToLower();
        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (request.OnlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!request.IncludeBlocked)
        {
            query = query.Where(x => !x.IsBlocked);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x =>
                x.Code.ToLower().Contains(term) ||
                x.FullName.ToLower().Contains(term) ||
                x.DocumentNumber.ToLower().Contains(term) ||
                x.Phone.ToLower().Contains(term) ||
                x.WhatsApp.ToLower().Contains(term));
        }

        return query.OrderBy(x => x.FullName);
    }

    private static string[] Headers()
    {
        return new[]
        {
            "Codigo", "Nome", "CPF/CNPJ", "RG/IE", "Telefone", "WhatsApp", "E-mail", "CEP",
            "Endereco", "Numero", "Complemento", "Bairro", "Cidade", "Estado",
            "LimiteCredito", "Observacoes", "Ativo", "Bloqueado"
        };
    }

    private static string[] Values(Customer customer)
    {
        return new[]
        {
            customer.Code,
            customer.FullName,
            customer.DocumentNumber,
            customer.StateRegistration,
            customer.Phone,
            customer.WhatsApp,
            customer.Email,
            customer.ZipCode,
            customer.Address,
            customer.AddressNumber,
            customer.Complement,
            customer.District,
            customer.City,
            customer.State,
            customer.CreditLimit.ToString(CultureInfo.InvariantCulture),
            customer.Notes,
            customer.IsActive ? "Sim" : "Nao",
            customer.IsBlocked ? "Sim" : "Nao"
        };
    }

    private static void WriteRow(IXLWorksheet sheet, int row, Customer customer)
    {
        var values = Values(customer);
        for (var i = 0; i < values.Length; i++)
        {
            sheet.Cell(row, i + 1).Value = values[i];
        }
    }

    private static string Escape(string value)
    {
        return value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static void Header(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(text).SemiBold();
    }
}
