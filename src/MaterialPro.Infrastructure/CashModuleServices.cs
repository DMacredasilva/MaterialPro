using System.Globalization;
using ClosedXML.Excel;
using MaterialPro.Application;
using MaterialPro.Domain;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MaterialPro.Infrastructure;

public sealed class CashReportService : ICashReportService
{
    private readonly MaterialProDbContext _db;

    public CashReportService(MaterialProDbContext db) => _db = db;

    public byte[] ExportPdf(CashReportRequest request)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var sessions = QuerySessions(request).ToList();
        var movements = QueryMovements(request).ToList();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.Header().Text("Relatorio de Caixa").SemiBold().FontSize(18);
                page.Content().Column(column =>
                {
                    column.Item().Text($"Caixas: {sessions.Count} | Movimentos: {movements.Count}").SemiBold();
                    column.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });
                        Header(table, "Caixa");
                        Header(table, "Abertura");
                        Header(table, "Status");
                        Header(table, "Diferença");
                        foreach (var cash in sessions)
                        {
                            table.Cell().Text(cash.Code);
                            table.Cell().Text(cash.OpenedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                            table.Cell().Text(cash.Status.ToString());
                            table.Cell().AlignRight().Text(cash.DifferenceAmount.ToString("C", CultureInfo.GetCultureInfo("pt-BR")));
                        }
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportExcel(CashReportRequest request)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Caixas");
        var headers = new[] { "Caixa", "Abertura", "Fechamento", "Operador", "Status", "Inicial", "Dinheiro", "PIX", "Débito", "Crédito", "Prazo", "Suprimento", "Sangria", "Vendas", "Informado", "Diferença" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var cash in QuerySessions(request))
        {
            sheet.Cell(row, 1).Value = cash.Code;
            sheet.Cell(row, 2).Value = cash.OpenedAtUtc;
            sheet.Cell(row, 3).Value = cash.ClosedAtUtc;
            sheet.Cell(row, 4).Value = cash.OpenedByUserId.ToString();
            sheet.Cell(row, 5).Value = cash.Status.ToString();
            sheet.Cell(row, 6).Value = cash.OpeningAmount;
            sheet.Cell(row, 7).Value = cash.CashAmount;
            sheet.Cell(row, 8).Value = cash.PixAmount;
            sheet.Cell(row, 9).Value = cash.DebitCardAmount;
            sheet.Cell(row, 10).Value = cash.CreditCardAmount;
            sheet.Cell(row, 11).Value = cash.CreditSaleAmount;
            sheet.Cell(row, 12).Value = cash.SupplyAmount;
            sheet.Cell(row, 13).Value = cash.WithdrawalAmount;
            sheet.Cell(row, 14).Value = cash.TotalSalesAmount;
            sheet.Cell(row, 15).Value = cash.ReportedAmount;
            sheet.Cell(row, 16).Value = cash.DifferenceAmount;
            row++;
        }

        var movementsSheet = workbook.AddWorksheet("Movimentos");
        var movementHeaders = new[] { "CaixaId", "Tipo", "Origem", "Forma", "Valor", "Data", "VendaId", "DuplicataId", "Observação" };
        for (var i = 0; i < movementHeaders.Length; i++)
        {
            movementsSheet.Cell(1, i + 1).Value = movementHeaders[i];
        }
        row = 2;
        foreach (var movement in QueryMovements(request))
        {
            movementsSheet.Cell(row, 1).Value = movement.CashSessionId.ToString();
            movementsSheet.Cell(row, 2).Value = movement.Type.ToString();
            movementsSheet.Cell(row, 3).Value = movement.Origin;
            movementsSheet.Cell(row, 4).Value = movement.PaymentMethod;
            movementsSheet.Cell(row, 5).Value = movement.Amount;
            movementsSheet.Cell(row, 6).Value = movement.MovementAtUtc;
            movementsSheet.Cell(row, 7).Value = movement.SaleId?.ToString() ?? string.Empty;
            movementsSheet.Cell(row, 8).Value = movement.DuplicateId?.ToString() ?? string.Empty;
            movementsSheet.Cell(row, 9).Value = movement.Observation;
            row++;
        }

        sheet.Columns().AdjustToContents();
        movementsSheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private IQueryable<CashSession> QuerySessions(CashReportRequest request)
    {
        var query = _db.CashSessions.AsNoTracking().AsQueryable();
        if (request.FromUtc.HasValue) query = query.Where(x => x.OpenedAtUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(x => x.OpenedAtUtc <= request.ToUtc.Value);
        if (request.OperatorId.HasValue) query = query.Where(x => x.OpenedByUserId == request.OperatorId.Value);
        if (request.OnlyDifferences) query = query.Where(x => x.DifferenceAmount != 0);
        return query.OrderByDescending(x => x.OpenedAtUtc);
    }

    private IQueryable<CashMovement> QueryMovements(CashReportRequest request)
    {
        var sessionIds = QuerySessions(request).Select(x => x.Id);
        var query = _db.CashMovements.AsNoTracking().Where(x => sessionIds.Contains(x.CashSessionId));
        if (request.OnlyWithdrawals) query = query.Where(x => x.Type == CashMovementType.Withdrawal);
        if (request.OnlySupplies) query = query.Where(x => x.Type == CashMovementType.Supply);
        return query.OrderByDescending(x => x.MovementAtUtc);
    }

    private static void Header(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(text).SemiBold();
    }
}
