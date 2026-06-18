using MaterialPro.Application;
using MaterialPro.Domain;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MaterialPro.Infrastructure;

public sealed class ReportsCenterService : IReportsCenterService
{
    private readonly MaterialProDbContext _db;

    private static readonly IReadOnlyList<ReportDefinition> Definitions =
    [
        new("sales.period", "Vendas por periodo", ReportGroup.Sales, false, true),
        new("sales.product", "Vendas por produto", ReportGroup.Sales, false, false),
        new("sales.cancelled", "Vendas canceladas", ReportGroup.Sales, false, false),
        new("cash.daily", "Caixa diario", ReportGroup.Cash, false, true),
        new("cash.movements", "Movimentos de caixa", ReportGroup.Cash, false, true),
        new("stock.current", "Estoque atual", ReportGroup.Stock, false, false),
        new("stock.low", "Produtos com estoque baixo", ReportGroup.Stock, false, false),
        new("stock.movements", "Movimentacoes de estoque", ReportGroup.Stock, false, false),
        new("financial.payables", "Contas a pagar", ReportGroup.Financial, true, false),
        new("financial.receivables", "Contas a receber", ReportGroup.Financial, true, false),
        new("financial.duplicates", "Duplicatas", ReportGroup.Financial, true, false),
        new("financial.settlements", "Baixas financeiras", ReportGroup.Financial, true, false),
        new("customers.list", "Lista de clientes", ReportGroup.Customers, false, false),
        new("customers.delinquent", "Clientes inadimplentes", ReportGroup.Customers, true, false),
        new("products.list", "Lista de produtos", ReportGroup.Products, false, false),
        new("suppliers.list", "Lista de fornecedores", ReportGroup.Suppliers, false, false),
        new("returns.period", "Devolucoes por periodo", ReportGroup.Returns, false, false),
        new("cancellations.period", "Cancelamentos por periodo", ReportGroup.Cancellations, false, false),
        new("system.logs", "Logs do sistema", ReportGroup.System, false, false),
        new("system.users", "Usuarios ativos", ReportGroup.System, false, false)
    ];

    public ReportsCenterService(MaterialProDbContext db) => _db = db;

    public IReadOnlyList<ReportDefinition> Catalog() => Definitions;

    public ReportResult Generate(ReportFilterRequest request, AppUser user)
    {
        var definition = Definition(request.ReportKey);
        EnsurePermission(definition, user);
        var rows = definition.Key switch
        {
            "sales.period" => SalesRows(request),
            "sales.product" => ProductSalesRows(request),
            "sales.cancelled" => CancelledSalesRows(request),
            "cash.daily" => CashRows(request),
            "cash.movements" => CashMovementRows(request),
            "stock.current" => StockRows(request, false),
            "stock.low" => StockRows(request, true),
            "stock.movements" => StockMovementRows(request),
            "financial.payables" => PayableRows(request),
            "financial.receivables" => ReceivableRows(request),
            "financial.duplicates" => DuplicateRows(request),
            "financial.settlements" => SettlementRows(request),
            "customers.list" => CustomerRows(request, false),
            "customers.delinquent" => CustomerRows(request, true),
            "products.list" => ProductRows(request),
            "suppliers.list" => SupplierRows(request),
            "returns.period" => ReturnRows(request),
            "cancellations.period" => CancellationRows(request),
            "system.logs" => SystemLogRows(request),
            "system.users" => UserRows(request),
            _ => SalesRows(request)
        };
        var columns = rows.SelectMany(x => x.Values.Keys).Distinct().ToList();
        var totals = BuildTotals(rows);
        Log(definition, request, user, ReportOutputFormat.Preview);
        return new ReportResult(definition, request, columns, rows, totals);
    }

    public ReportsDashboardSummary Dashboard(ReportFilterRequest request, AppUser user)
    {
        EnsurePermission(new ReportDefinition("dashboard", "Dashboard de relatorios", ReportGroup.Sales, false, false), user);
        var from = request.FromUtc ?? DateTime.UtcNow.Date;
        var to = request.ToUtc ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
        var sales = _db.Sales.AsNoTracking().Where(x => x.SoldAtUtc >= from && x.SoldAtUtc <= to && x.Status == SaleStatus.Finalizada);
        var totalSales = sales.Sum(x => x.TotalAmount);
        var received = _db.FinancialSettlements.AsNoTracking().Where(x => x.Type == FinancialType.Receivable && x.SettledAtUtc >= from && x.SettledAtUtc <= to).Sum(x => x.TotalAmount);
        var receivable = _db.AccountsReceivable.AsNoTracking().Where(x => x.BalanceAmount > 0 && x.Status != FinancialStatus.Cancelled).Sum(x => x.BalanceAmount);
        var payable = _db.AccountsPayable.AsNoTracking().Where(x => x.BalanceAmount > 0 && x.Status != FinancialStatus.Cancelled).Sum(x => x.BalanceAmount);
        var stockValue = _db.Products.AsNoTracking().Where(x => x.IsActive).Sum(x => x.StockQuantity * x.CostPrice);
        var cost = from item in _db.SaleItems.AsNoTracking()
                   join sale in sales on item.SaleId equals sale.Id
                   join product in _db.Products.AsNoTracking() on item.ProductId equals product.Id
                   select item.Quantity * product.CostPrice;
        var bestProducts = BestSellingProducts(from, to);
        var bestCustomers = BestCustomers(from, to);
        return new ReportsDashboardSummary(totalSales, received, receivable, payable, totalSales - cost.Sum(), stockValue, bestProducts, bestCustomers);
    }

    public byte[] ExportPdf(ReportFilterRequest request, AppUser user)
    {
        var result = Generate(request, user);
        Log(result.Definition, request, user, ReportOutputFormat.Pdf);
        QuestPDF.Settings.License = LicenseType.Community;
        var profile = _db.StoreProfiles.AsNoTracking().FirstOrDefault() ?? new StoreProfile();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.Header().Column(column =>
                {
                    column.Item().Text(profile.StoreName).SemiBold().FontSize(16);
                    column.Item().Text($"{profile.Cnpj} | {result.Definition.Title}");
                    column.Item().Text($"Emitido em {DateTime.Now:dd/MM/yyyy HH:mm} por {user.FullName}");
                });
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in result.Columns.Take(8)) columns.RelativeColumn();
                    });
                    foreach (var col in result.Columns.Take(8)) table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(col).SemiBold();
                    foreach (var row in result.Rows)
                    {
                        foreach (var col in result.Columns.Take(8)) table.Cell().Padding(3).Text(Value(row, col));
                    }
                });
                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Pagina ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });
        return document.GeneratePdf();
    }

    public byte[] ExportExcel(ReportFilterRequest request, AppUser user)
    {
        var result = Generate(request, user);
        Log(result.Definition, request, user, ReportOutputFormat.Excel);
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.AddWorksheet(SafeSheetName(result.Definition.Title));
        sheet.Cell(1, 1).Value = result.Definition.Title;
        sheet.Cell(2, 1).Value = $"Emitido em {DateTime.Now:dd/MM/yyyy HH:mm} por {user.FullName}";
        for (var i = 0; i < result.Columns.Count; i++) sheet.Cell(4, i + 1).Value = result.Columns[i];
        var rowIndex = 5;
        foreach (var row in result.Rows)
        {
            for (var i = 0; i < result.Columns.Count; i++)
            {
                var value = row.Values.TryGetValue(result.Columns[i], out var v) ? v : null;
                sheet.Cell(rowIndex, i + 1).Value = value switch
                {
                    DateTime d => d,
                    decimal m => m,
                    int n => n,
                    null => string.Empty,
                    _ => value.ToString()
                };
            }
            rowIndex++;
        }
        sheet.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] PrintSummary(ReportFilterRequest request, AppUser user, InternalPaperFormat format = InternalPaperFormat.A4)
    {
        var result = Generate(request, user);
        Log(result.Definition, request, user, ReportOutputFormat.Print);
        var lines = result.Rows.Take(30).Select(row => string.Join(" | ", result.Columns.Take(4).Select(c => Value(row, c))));
        return new InternalDocumentService().GeneratePdf(new InternalDocumentRequest(InternalDocumentKind.PaymentProof, format, result.Definition.Key, string.Empty, result.Definition.Title, result.Totals.Values.Sum(), "RELATORIO", "Resumo de impressao", lines));
    }

    public IReadOnlyList<ReportAuditLog> Logs() => _db.ReportAuditLogs.AsNoTracking().OrderByDescending(x => x.GeneratedAtUtc).ToList();

    public ReportSchedule Schedule(string reportKey, string frequency, string outputFolder)
    {
        _ = Definition(reportKey);
        var schedule = new ReportSchedule { ReportKey = reportKey, Frequency = frequency.Trim(), OutputFolder = outputFolder.Trim() };
        _db.ReportSchedules.Add(schedule);
        _db.SaveChanges();
        return schedule;
    }

    private List<ReportRow> SalesRows(ReportFilterRequest request)
    {
        var query = FilterSales(request);
        return (from sale in query.AsEnumerable()
                join customer in _db.Customers.AsNoTracking().AsEnumerable() on sale.CustomerId equals customer.Id into customers
                from customer in customers.DefaultIfEmpty()
                join user in _db.Users.AsNoTracking().AsEnumerable() on sale.UserId equals user.Id into users
                from user in users.DefaultIfEmpty()
                select Row(("Numero", sale.ReceiptNumber), ("Data", sale.SoldAtUtc), ("Cliente", customer == null ? "Consumidor" : customer.FullName), ("Operador", user == null ? "" : user.FullName), ("Pagamento", sale.PaymentMethod), ("Subtotal", sale.SubtotalAmount), ("Desconto", sale.DiscountAmount), ("Total", sale.TotalAmount), ("Status", sale.Status.ToString()))).ToList();
    }

    private List<ReportRow> ProductSalesRows(ReportFilterRequest request)
    {
        var sales = FilterSales(request);
        return (from item in _db.SaleItems.AsNoTracking().AsEnumerable()
                join sale in sales.AsEnumerable() on item.SaleId equals sale.Id
                group item by new { item.ProductId, item.ProductCode, item.ProductDescription } into g
                select Row(("Codigo", g.Key.ProductCode), ("Produto", g.Key.ProductDescription), ("Quantidade", g.Sum(x => x.Quantity)), ("Total", g.Sum(x => x.TotalItem)))).ToList();
    }

    private List<ReportRow> CancelledSalesRows(ReportFilterRequest request) => FilterSales(request).Where(x => x.Status == SaleStatus.Cancelada).AsEnumerable().Select(x => Row(("Venda", x.ReceiptNumber), ("Data", x.SoldAtUtc), ("Valor", x.TotalAmount), ("Status", x.Status.ToString()))).ToList();
    private List<ReportRow> CashRows(ReportFilterRequest request) => FilterCash(request).AsEnumerable().Select(x => Row(("Caixa", x.Code), ("Abertura", x.OpenedAtUtc), ("Fechamento", x.ClosedAtUtc), ("Inicial", x.OpeningAmount), ("Dinheiro", x.CashAmount), ("PIX", x.PixAmount), ("Cartao", x.DebitCardAmount + x.CreditCardAmount), ("Sangria", x.WithdrawalAmount), ("Suprimento", x.SupplyAmount), ("Diferenca", x.DifferenceAmount), ("Status", x.Status.ToString()))).ToList();
    private List<ReportRow> CashMovementRows(ReportFilterRequest request) => FilterCashMovements(request).AsEnumerable().Select(x => Row(("Data", x.MovementAtUtc), ("Tipo", x.Type.ToString()), ("Origem", x.Origin), ("Descricao", x.Description), ("Forma", x.PaymentMethod), ("Valor", x.Amount))).ToList();
    private List<ReportRow> StockRows(ReportFilterRequest request, bool lowOnly) => _db.Products.AsNoTracking().Where(x => !lowOnly || x.StockQuantity <= x.MinimumStock).AsEnumerable().Select(x => Row(("Codigo", x.Sku), ("Produto", x.Name), ("Categoria", x.Category), ("Marca", x.Brand), ("Estoque", x.StockQuantity), ("Minimo", x.MinimumStock), ("Custo", x.CostPrice), ("Venda", x.SalePrice), ("Valor total", x.StockQuantity * x.CostPrice))).ToList();
    private List<ReportRow> StockMovementRows(ReportFilterRequest request) => _db.StockMovements.AsNoTracking().Where(x => (!request.FromUtc.HasValue || x.MovementAtUtc >= request.FromUtc) && (!request.ToUtc.HasValue || x.MovementAtUtc <= request.ToUtc)).AsEnumerable().Select(x => Row(("Data", x.MovementAtUtc), ("ProdutoId", x.ProductId), ("Tipo", x.Type.ToString()), ("Quantidade", x.Quantity), ("Anterior", x.PreviousStock), ("Atual", x.CurrentStock), ("Observacao", x.Observation))).ToList();
    private List<ReportRow> PayableRows(ReportFilterRequest request) => _db.AccountsPayable.AsNoTracking().Where(x => (!request.FromUtc.HasValue || x.DueDateUtc >= request.FromUtc) && (!request.ToUtc.HasValue || x.DueDateUtc <= request.ToUtc)).AsEnumerable().Select(x => Row(("Documento", x.Number), ("Fornecedor", x.SupplierName), ("Emissao", x.IssueDateUtc), ("Vencimento", x.DueDateUtc), ("Original", x.OriginalAmount), ("Juros", x.InterestAmount), ("Multa", x.FineAmount), ("Desconto", x.DiscountAmount), ("Pago", x.PaidAmount), ("Saldo", x.BalanceAmount), ("Status", x.Status.ToString()))).ToList();
    private List<ReportRow> ReceivableRows(ReportFilterRequest request) => _db.AccountsReceivable.AsNoTracking().Where(x => (!request.FromUtc.HasValue || x.DueDateUtc >= request.FromUtc) && (!request.ToUtc.HasValue || x.DueDateUtc <= request.ToUtc)).AsEnumerable().Select(x => Row(("Documento", x.Number), ("Cliente", x.CustomerName), ("Emissao", x.IssueDateUtc), ("Vencimento", x.DueDateUtc), ("Original", x.OriginalAmount), ("Juros", x.InterestAmount), ("Multa", x.FineAmount), ("Desconto", x.DiscountAmount), ("Recebido", x.PaidAmount), ("Saldo", x.BalanceAmount), ("Status", x.Status.ToString()))).ToList();
    private List<ReportRow> DuplicateRows(ReportFilterRequest request) => _db.Duplicates.AsNoTracking().Where(x => (!request.FromUtc.HasValue || x.DueDateUtc >= request.FromUtc) && (!request.ToUtc.HasValue || x.DueDateUtc <= request.ToUtc)).AsEnumerable().Select(x => Row(("Documento", x.Number), ("Tipo", x.Type.ToString()), ("Emissao", x.IssueDateUtc), ("Vencimento", x.DueDateUtc), ("Original", x.Amount), ("Pago", x.PaidAmount), ("Saldo", x.BalanceAmount), ("Status", x.Status.ToString()))).ToList();
    private List<ReportRow> SettlementRows(ReportFilterRequest request) => _db.FinancialSettlements.AsNoTracking().Where(x => (!request.FromUtc.HasValue || x.SettledAtUtc >= request.FromUtc) && (!request.ToUtc.HasValue || x.SettledAtUtc <= request.ToUtc)).AsEnumerable().Select(x => Row(("Data", x.SettledAtUtc), ("Tipo", x.Type.ToString()), ("Baixa", x.Amount), ("Juros", x.InterestAmount), ("Multa", x.FineAmount), ("Desconto", x.DiscountAmount), ("Total", x.TotalAmount), ("Forma", x.PaymentMethod))).ToList();
    private List<ReportRow> CustomerRows(ReportFilterRequest request, bool delinquent) => _db.Customers.AsNoTracking().Where(c => !delinquent || _db.AccountsReceivable.Any(r => r.CustomerId == c.Id && r.BalanceAmount > 0 && r.DueDateUtc < DateTime.UtcNow)).AsEnumerable().Select(c => Row(("Codigo", c.Code), ("Nome", c.FullName), ("Documento", c.DocumentNumber), ("Telefone", c.Phone), ("WhatsApp", c.WhatsApp), ("Cidade", c.City), ("Status", c.IsBlocked ? "Bloqueado" : c.IsActive ? "Ativo" : "Inativo"))).ToList();
    private List<ReportRow> ProductRows(ReportFilterRequest request) => _db.Products.AsNoTracking().AsEnumerable().Select(p => Row(("Codigo", p.Sku), ("Barras", p.Barcode), ("Descricao", p.Name), ("Categoria", p.Category), ("Marca", p.Brand), ("Custo", p.CostPrice), ("Venda", p.SalePrice), ("Margem", p.SalePrice - p.CostPrice), ("Estoque", p.StockQuantity), ("Status", p.IsActive ? "Ativo" : "Inativo"))).ToList();
    private List<ReportRow> SupplierRows(ReportFilterRequest request) => _db.Suppliers.AsNoTracking().AsEnumerable().Select(s => Row(("Codigo", s.Code), ("Nome fantasia", s.FantasyName), ("Razao social", s.LegalName), ("CNPJ", s.Cnpj), ("Telefone", s.Phone), ("Cidade", s.City), ("Status", s.IsActive ? "Ativo" : "Inativo"))).ToList();
    private List<ReportRow> ReturnRows(ReportFilterRequest request) => _db.SaleReturns.AsNoTracking().Where(x => (!request.FromUtc.HasValue || x.ProcessedAtUtc >= request.FromUtc) && (!request.ToUtc.HasValue || x.ProcessedAtUtc <= request.ToUtc)).AsEnumerable().Select(x => Row(("Devolucao", x.Id), ("Venda", x.SaleId), ("Data", x.ProcessedAtUtc), ("Valor", x.TotalReturnedAmount), ("Motivo", x.Reason), ("Operador", x.ProcessedBy))).ToList();
    private List<ReportRow> CancellationRows(ReportFilterRequest request) => _db.SaleCancellations.AsNoTracking().Where(x => (!request.FromUtc.HasValue || x.CancelledAtUtc >= request.FromUtc) && (!request.ToUtc.HasValue || x.CancelledAtUtc <= request.ToUtc)).AsEnumerable().Select(x => Row(("Venda", x.SaleId), ("Data", x.CancelledAtUtc), ("Valor", x.TotalAmount), ("Motivo", x.Reason), ("Usuario", x.UserId))).ToList();
    private List<ReportRow> SystemLogRows(ReportFilterRequest request) => _db.SecurityAudits.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(500).AsEnumerable().Select(x => Row(("Data", x.CreatedAtUtc), ("Area", x.Area), ("Acao", x.Action), ("Entidade", x.EntityName), ("Detalhes", x.Details), ("Maquina", x.MachineName))).ToList();
    private List<ReportRow> UserRows(ReportFilterRequest request) => _db.Users.AsNoTracking().AsEnumerable().Select(x => Row(("Nome", x.FullName), ("Usuario", x.Username), ("Email", x.Email), ("Perfil", x.Role.ToString()), ("Ativo", x.IsActive), ("Ultimo login", x.LastLoginAtUtc))).ToList();

    private IQueryable<Sale> FilterSales(ReportFilterRequest request)
    {
        var query = _db.Sales.AsNoTracking().AsQueryable();
        if (request.FromUtc.HasValue) query = query.Where(x => x.SoldAtUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(x => x.SoldAtUtc <= request.ToUtc.Value);
        if (request.CustomerId.HasValue) query = query.Where(x => x.CustomerId == request.CustomerId.Value);
        if (request.UserId.HasValue) query = query.Where(x => x.UserId == request.UserId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<SaleStatus>(request.Status, true, out var status)) query = query.Where(x => x.Status == status);
        return query;
    }

    private IQueryable<CashSession> FilterCash(ReportFilterRequest request)
    {
        var query = _db.CashSessions.AsNoTracking().AsQueryable();
        if (request.FromUtc.HasValue) query = query.Where(x => x.OpenedAtUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(x => x.OpenedAtUtc <= request.ToUtc.Value);
        if (request.UserId.HasValue) query = query.Where(x => x.OpenedByUserId == request.UserId.Value);
        return query;
    }

    private IQueryable<CashMovement> FilterCashMovements(ReportFilterRequest request)
    {
        var query = _db.CashMovements.AsNoTracking().AsQueryable();
        if (request.FromUtc.HasValue) query = query.Where(x => x.MovementAtUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(x => x.MovementAtUtc <= request.ToUtc.Value);
        if (request.UserId.HasValue) query = query.Where(x => x.UserId == request.UserId.Value);
        return query;
    }

    private IReadOnlyList<ProductSalesSummary> BestSellingProducts(DateTime fromUtc, DateTime toUtc)
        => (from item in _db.SaleItems.AsNoTracking()
            join sale in _db.Sales.AsNoTracking().Where(x => x.SoldAtUtc >= fromUtc && x.SoldAtUtc <= toUtc && x.Status == SaleStatus.Finalizada) on item.SaleId equals sale.Id
            group item by new { item.ProductId, item.ProductCode, item.ProductDescription } into g
            orderby g.Sum(x => x.TotalItem) descending
            select new ProductSalesSummary(g.Key.ProductId, g.Key.ProductCode, g.Key.ProductDescription, g.Sum(x => x.Quantity), g.Sum(x => x.TotalItem))).Take(10).ToList();

    private IReadOnlyList<CustomerPurchaseSummary> BestCustomers(DateTime fromUtc, DateTime toUtc)
        => (from sale in _db.Sales.AsNoTracking().Where(x => x.SoldAtUtc >= fromUtc && x.SoldAtUtc <= toUtc && x.Status == SaleStatus.Finalizada)
            join customer in _db.Customers.AsNoTracking() on sale.CustomerId equals customer.Id
            group sale by new { customer.Id, customer.FullName } into g
            orderby g.Sum(x => x.TotalAmount) descending
            select new CustomerPurchaseSummary(g.Key.Id, g.Key.FullName, g.Sum(x => x.TotalAmount), g.Count())).Take(10).ToList();

    private void EnsurePermission(ReportDefinition definition, AppUser user)
    {
        if (definition.FinancialRestricted && user.Role is not (UserRole.Admin or UserRole.Manager))
        {
            throw new UnauthorizedAccessException("Relatorios financeiros exigem administrador ou gerente.");
        }
    }

    private ReportDefinition Definition(string key) => Definitions.FirstOrDefault(x => x.Key == key) ?? throw new InvalidOperationException("Relatorio nao encontrado.");

    private void Log(ReportDefinition definition, ReportFilterRequest request, AppUser user, ReportOutputFormat format)
    {
        _db.ReportAuditLogs.Add(new ReportAuditLog
        {
            UserId = user.Id,
            ReportKey = definition.Key,
            ReportTitle = definition.Title,
            Filters = $"{request.FromUtc:O}|{request.ToUtc:O}|{request.Term}|{request.Status}",
            Format = format,
            GeneratedAtUtc = DateTime.UtcNow,
            MachineName = Environment.MachineName
        });
        _db.SaveChanges();
    }

    private static ReportRow Row(params (string Key, object? Value)[] values) => new(values.ToDictionary(x => x.Key, x => x.Value));
    private static string Value(ReportRow row, string column) => row.Values.TryGetValue(column, out var value) ? Format(value) : string.Empty;
    private static string Format(object? value) => value switch { null => "", DateTime d => d.ToString("dd/MM/yyyy HH:mm"), decimal m => m.ToString("N2"), _ => value.ToString() ?? "" };
    private static string SafeSheetName(string value) => new(value.Take(28).Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
    private static Dictionary<string, decimal> BuildTotals(IEnumerable<ReportRow> rows)
    {
        var totals = new Dictionary<string, decimal>();
        foreach (var row in rows)
        {
            foreach (var pair in row.Values)
            {
                if (pair.Value is decimal value) totals[pair.Key] = totals.GetValueOrDefault(pair.Key) + value;
            }
        }
        return totals;
    }
}

