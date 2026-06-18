using System.Globalization;
using ClosedXML.Excel;
using MaterialPro.Application;
using MaterialPro.Domain;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MaterialPro.Infrastructure;

public sealed class PdvService : IPdvService
{
    private const decimal MaxDiscountWithoutManager = 50m;
    private readonly MaterialProDbContext _db;
    private readonly InventoryService _inventory;
    private readonly IPasswordHasher _hasher;
    private readonly ISecurityService? _security;

    public PdvService(MaterialProDbContext db, IPasswordHasher? hasher = null, ISecurityService? security = null)
    {
        _db = db;
        _inventory = new InventoryService(db, security);
        _hasher = hasher ?? new Sha256PasswordHasher();
        _security = security;
    }

    public Sale CreateSale(PdvCreateSaleRequest request)
    {
        var activeCash = ActiveCash(request.CashSessionId);
        var sale = new Sale
        {
            ReceiptNumber = NextNumber(),
            CustomerId = request.CustomerId ?? Guid.Empty,
            UserId = request.UserId,
            CashSessionId = activeCash.Id,
            SoldAtUtc = DateTime.UtcNow,
            Status = SaleStatus.Aberta,
            PaymentMethod = "ABERTA",
            Observation = request.Observation.Trim()
        };
        _db.Sales.Add(sale);
        AddLog(sale.Id, request.UserId, "ABERTURA", "Venda aberta");
        _db.SaveChanges();
        return sale;
    }

    public SaleItem AddItem(Guid saleId, PdvSaleItemRequest request)
    {
        var sale = OpenSale(saleId);
        var product = _db.Products.FirstOrDefault(x => x.Id == request.ProductId) ?? throw new InvalidOperationException("Produto nao encontrado.");
        if (!product.IsActive)
        {
            throw new InvalidOperationException("Produto inativo nao pode ser vendido.");
        }
        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Quantidade deve ser maior que zero.");
        }

        var item = new SaleItem
        {
            SaleId = sale.Id,
            ProductId = product.Id,
            ProductCode = product.Sku,
            ProductDescription = product.Name,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            DiscountAmount = request.DiscountAmount,
            SurchargeAmount = request.SurchargeAmount,
            TotalItem = (request.Quantity * request.UnitPrice) - request.DiscountAmount + request.SurchargeAmount
        };
        _db.SaleItems.Add(item);
        Recalculate(sale);
        AddLog(sale.Id, sale.UserId, "ITEM", $"{product.Sku} x {request.Quantity}");
        _db.SaveChanges();
        return item;
    }

    public Sale RemoveItem(Guid saleItemId)
    {
        var item = _db.SaleItems.FirstOrDefault(x => x.Id == saleItemId) ?? throw new InvalidOperationException("Item nao encontrado.");
        var sale = OpenSale(item.SaleId);
        _db.SaleItems.Remove(item);
        Recalculate(sale);
        AddLog(sale.Id, sale.UserId, "CANCELA_ITEM", item.ProductCode);
        _db.SaveChanges();
        return sale;
    }

    public Sale ApplyDiscount(Guid saleId, decimal discountAmount, bool managerAuthorized = false)
    {
        var sale = OpenSale(saleId);
        if (discountAmount < 0)
        {
            throw new InvalidOperationException("Desconto nao pode ser negativo.");
        }
        if (discountAmount > MaxDiscountWithoutManager && !managerAuthorized)
        {
            throw new InvalidOperationException("Desconto exige autorizacao de gerente.");
        }

        sale.DiscountAmount = discountAmount;
        Recalculate(sale);
        AddLog(sale.Id, sale.UserId, "DESCONTO", discountAmount.ToString("N2", CultureInfo.InvariantCulture));
        _db.SaveChanges();
        return sale;
    }

    public Sale FinalizeSale(PdvFinalizeRequest request)
    {
        var sale = OpenSale(request.SaleId);
        ActiveCash(sale.CashSessionId);
        var items = _db.SaleItems.Where(x => x.SaleId == sale.Id).ToList();
        if (items.Count == 0)
        {
            throw new InvalidOperationException("Venda sem produtos.");
        }

        sale.DiscountAmount = request.DiscountAmount;
        sale.SurchargeAmount = request.SurchargeAmount;
        Recalculate(sale);
        if (sale.TotalAmount <= 0)
        {
            throw new InvalidOperationException("Venda com total zero nao permitida.");
        }

        var payments = request.Payments.Where(x => x.Amount > 0).ToList();
        if (payments.Count == 0)
        {
            throw new InvalidOperationException("Informe pagamento.");
        }
        if (payments.Sum(x => x.Amount) < sale.TotalAmount)
        {
            throw new InvalidOperationException("Pagamento menor que total da venda.");
        }
        if (sale.DiscountAmount > MaxDiscountWithoutManager && !request.ManagerAuthorized)
        {
            throw new InvalidOperationException("Desconto exige autorizacao de gerente.");
        }

        var hasCredit = payments.Any(x => IsCredit(x.PaymentMethod));
        if (hasCredit)
        {
            var customer = sale.CustomerId == Guid.Empty ? null : _db.Customers.FirstOrDefault(x => x.Id == sale.CustomerId);
            if (customer is null)
            {
                throw new InvalidOperationException("Venda a prazo exige cliente.");
            }
            if (customer.IsBlocked)
            {
                throw new InvalidOperationException("Cliente bloqueado para venda a prazo.");
            }
        }

        foreach (var item in items)
        {
            var product = _db.Products.First(x => x.Id == item.ProductId);
            if (!request.AllowNegativeStock && product.StockQuantity < item.Quantity)
            {
                throw new InvalidOperationException($"Estoque insuficiente: {product.Name}.");
            }
        }

        foreach (var item in items.Where(x => !x.StockDeducted))
        {
            _inventory.ExitStock(new StockMoveRequest(item.ProductId, item.Quantity, StockMovementType.SaleExit, "Venda PDV", sale.ReceiptNumber, sale.UserId, AllowNegative: request.AllowNegativeStock || request.ManagerAuthorized));
            item.StockDeducted = true;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }

        foreach (var payment in payments)
        {
            _db.SalePayments.Add(new SalePayment
            {
                SaleId = sale.Id,
                PaymentMethod = NormalizePayment(payment.PaymentMethod),
                Amount = payment.Amount,
                Installments = Math.Max(1, payment.Installments),
                FirstDueDateUtc = payment.FirstDueDateUtc,
                Observation = payment.Observation.Trim()
            });
        }

        sale.PaidAmount = payments.Sum(x => x.Amount);
        sale.ChangeAmount = Math.Max(0, sale.PaidAmount - sale.TotalAmount);
        sale.PaymentMethod = payments.Count == 1 ? NormalizePayment(payments[0].PaymentMethod) : "MISTO";
        sale.Status = SaleStatus.Finalizada;
        sale.UpdatedAtUtc = DateTime.UtcNow;

        RegisterCash(sale, payments);
        GenerateCreditDocuments(sale, payments);
        AddLog(sale.Id, sale.UserId, "FINALIZACAO", $"Total {sale.TotalAmount:N2}");
        _db.SaveChanges();
        return sale;
    }

    public SaleCancellation CancelSale(SaleCancellationRequest request)
    {
        var cancellation = new SaleCancellationService(_db, _hasher, _security).CancelSale(request);
        AddLog(request.SaleId, request.UserId, "CANCELAMENTO", request.Reason);
        _db.SaveChanges();
        return cancellation;
    }

    public IReadOnlyList<Sale> Search(PdvSaleSearchRequest request)
    {
        var query = _db.Sales.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Number))
        {
            query = query.Where(x => x.ReceiptNumber.Contains(request.Number));
        }
        if (request.FromUtc.HasValue)
        {
            query = query.Where(x => x.SoldAtUtc >= request.FromUtc.Value);
        }
        if (request.ToUtc.HasValue)
        {
            query = query.Where(x => x.SoldAtUtc <= request.ToUtc.Value);
        }
        if (request.Amount.HasValue)
        {
            query = query.Where(x => x.TotalAmount == request.Amount.Value);
        }
        if (request.UserId.HasValue)
        {
            query = query.Where(x => x.UserId == request.UserId);
        }
        if (!string.IsNullOrWhiteSpace(request.Customer))
        {
            var term = request.Customer.Trim().ToLower();
            var ids = _db.Customers.Where(x => x.FullName.ToLower().Contains(term) || x.DocumentNumber.ToLower().Contains(term)).Select(x => x.Id);
            query = query.Where(x => ids.Contains(x.CustomerId));
        }

        return query.OrderByDescending(x => x.SoldAtUtc).ToList();
    }

    public IReadOnlyList<SaleItem> Items(Guid saleId) => _db.SaleItems.AsNoTracking().Where(x => x.SaleId == saleId).OrderBy(x => x.CreatedAtUtc).ToList();

    public IReadOnlyList<SalePayment> Payments(Guid saleId) => _db.SalePayments.AsNoTracking().Where(x => x.SaleId == saleId).OrderBy(x => x.CreatedAtUtc).ToList();

    public byte[] GenerateReceiptPdf(PdvReceiptRequest request)
    {
        var sale = _db.Sales.AsNoTracking().First(x => x.Id == request.SaleId);
        var customer = sale.CustomerId == Guid.Empty ? null : _db.Customers.AsNoTracking().FirstOrDefault(x => x.Id == sale.CustomerId);
        var lines = Items(sale.Id).Select(x => $"{x.Quantity:N3} x {x.ProductDescription} | {x.UnitPrice:C} | {x.TotalItem:C}").ToList();
        lines.Add($"Subtotal: {sale.SubtotalAmount:C}");
        lines.Add($"Desconto: {sale.DiscountAmount:C}");
        lines.Add($"Acrescimo: {sale.SurchargeAmount:C}");
        lines.Add($"Total: {sale.TotalAmount:C}");
        foreach (var payment in Payments(sale.Id))
        {
            lines.Add($"{payment.PaymentMethod}: {payment.Amount:C}");
        }
        if (sale.ChangeAmount > 0)
        {
            lines.Add($"Troco: {sale.ChangeAmount:C}");
        }
        lines.Add("OBRIGADO PELA PREFERENCIA");

        return new InternalDocumentService().GeneratePdf(new InternalDocumentRequest(
            InternalDocumentKind.SaleCoupon,
            request.Format,
            sale.ReceiptNumber,
            customer?.FullName ?? "Consumidor final",
            "Cupom interno sem valor fiscal",
            sale.TotalAmount,
            sale.PaymentMethod,
            "Nao e documento fiscal",
            lines));
    }

    private CashSession ActiveCash(Guid? preferredId)
    {
        var query = _db.CashSessions.Where(x => x.ClosedAtUtc == null);
        var active = preferredId.HasValue ? query.FirstOrDefault(x => x.Id == preferredId.Value) : query.FirstOrDefault();
        return active ?? throw new InvalidOperationException("Nao e permitido vender sem caixa aberto.");
    }

    private Sale OpenSale(Guid saleId)
    {
        var sale = _db.Sales.FirstOrDefault(x => x.Id == saleId) ?? throw new InvalidOperationException("Venda nao encontrada.");
        if (sale.Status != SaleStatus.Aberta)
        {
            throw new InvalidOperationException("Venda nao esta aberta.");
        }
        return sale;
    }

    private void Recalculate(Sale sale)
    {
        var deletedIds = _db.ChangeTracker.Entries<SaleItem>()
            .Where(x => x.State == EntityState.Deleted && x.Entity.SaleId == sale.Id)
            .Select(x => x.Entity.Id)
            .ToHashSet();
        var tracked = _db.ChangeTracker.Entries<SaleItem>()
            .Where(x => x.State != EntityState.Deleted && x.Entity.SaleId == sale.Id)
            .Select(x => x.Entity);
        var persisted = _db.SaleItems.Where(x => x.SaleId == sale.Id && !deletedIds.Contains(x.Id));
        var items = tracked.Concat(persisted).DistinctBy(x => x.Id).ToList();
        foreach (var item in items)
        {
            item.TotalItem = (item.Quantity * item.UnitPrice) - item.DiscountAmount + item.SurchargeAmount;
        }
        sale.SubtotalAmount = items.Sum(x => x.Quantity * x.UnitPrice);
        var itemDiscounts = items.Sum(x => x.DiscountAmount);
        var itemSurcharges = items.Sum(x => x.SurchargeAmount);
        sale.TotalAmount = Math.Max(0, sale.SubtotalAmount - itemDiscounts - sale.DiscountAmount + itemSurcharges + sale.SurchargeAmount);
        sale.UpdatedAtUtc = DateTime.UtcNow;
    }

    private void RegisterCash(Sale sale, IReadOnlyList<PdvPaymentRequest> payments)
    {
        var session = _db.CashSessions.First(x => x.Id == sale.CashSessionId);
        foreach (var payment in payments.Where(x => !IsCredit(x.PaymentMethod)))
        {
            var method = NormalizePayment(payment.PaymentMethod);
            _db.CashMovements.Add(new CashMovement
            {
                CashSessionId = sale.CashSessionId!.Value,
                Type = CashMovementType.Sale,
                Origin = "PDV",
                Amount = payment.Amount,
                Description = $"Venda {sale.ReceiptNumber} - {method}",
                SaleId = sale.Id,
                UserId = sale.UserId,
                PaymentMethod = method,
                MovementAtUtc = DateTime.UtcNow
            });
            ApplyCashTotals(session, payment.Amount, method);
        }
        foreach (var payment in payments.Where(x => IsCredit(x.PaymentMethod)))
        {
            session.TotalSalesAmount += payment.Amount;
            session.CreditSaleAmount += payment.Amount;
        }
        session.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void ApplyCashTotals(CashSession session, decimal amount, string method)
    {
        session.TotalSalesAmount += amount;
        switch (method)
        {
            case "DINHEIRO":
                session.CashAmount += amount;
                session.CurrentAmount = session.CashAmount;
                break;
            case "PIX":
                session.PixAmount += amount;
                break;
            case "CARTAO_DEBITO":
                session.DebitCardAmount += amount;
                break;
            case "CARTAO_CREDITO":
                session.CreditCardAmount += amount;
                break;
        }
    }

    private void GenerateCreditDocuments(Sale sale, IReadOnlyList<PdvPaymentRequest> payments)
    {
        foreach (var payment in payments.Where(x => IsCredit(x.PaymentMethod)))
        {
            var installments = Math.Max(1, payment.Installments);
            var installmentAmount = Math.Round(payment.Amount / installments, 2);
            var firstDue = payment.FirstDueDateUtc ?? DateTime.UtcNow.Date.AddDays(30);
            var customerName = sale.CustomerId == Guid.Empty ? "Cliente" : _db.Customers.Where(x => x.Id == sale.CustomerId).Select(x => x.FullName).FirstOrDefault() ?? "Cliente";
            for (var i = 1; i <= installments; i++)
            {
                var amount = i == installments ? payment.Amount - (installmentAmount * (installments - 1)) : installmentAmount;
                var number = $"{sale.ReceiptNumber}-{i:D2}";
                var due = firstDue.AddDays(30 * (i - 1));
                _db.Duplicates.Add(new Duplicate
                {
                    Number = number,
                    Type = FinancialType.Receivable,
                    SaleId = sale.Id,
                    Description = $"Venda a prazo {sale.ReceiptNumber}",
                    Amount = amount,
                    BalanceAmount = amount,
                    DueDateUtc = due,
                    Status = FinancialStatus.Open
                });
                _db.AccountsReceivable.Add(new AccountReceivable
                {
                    Number = number,
                    SaleId = sale.Id,
                    CustomerName = customerName,
                    Description = $"Venda a prazo {sale.ReceiptNumber}",
                    OriginalAmount = amount,
                    BalanceAmount = amount,
                    DueDateUtc = due,
                    Status = FinancialStatus.Open,
                    PaymentMethod = "PRAZO"
                });
            }
        }
    }

    private void AddLog(Guid saleId, Guid? userId, string action, string description)
    {
        _db.SaleLogs.Add(new SaleLog
        {
            SaleId = saleId,
            UserId = userId,
            Action = action,
            Description = description,
            LogAtUtc = DateTime.UtcNow,
            MachineIp = Environment.MachineName
        });
    }

    private string NextNumber() => $"V{(_db.Sales.Count() + 1):D6}";
    private static string NormalizePayment(string value) => string.IsNullOrWhiteSpace(value) ? "DINHEIRO" : value.Trim().ToUpperInvariant();
    private static bool IsCredit(string value) => NormalizePayment(value) == "PRAZO";
}

public sealed class SalesReportService : ISalesReportService
{
    private readonly MaterialProDbContext _db;

    public SalesReportService(MaterialProDbContext db) => _db = db;

    public byte[] ExportPdf(SalesReportRequest request)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var sales = Query(request).ToList();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.Header().Text("Relatorio de Vendas").SemiBold().FontSize(18);
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });
                    Header(table, "Venda");
                    Header(table, "Data");
                    Header(table, "Pagamento");
                    Header(table, "Total");
                    foreach (var sale in sales)
                    {
                        table.Cell().Text(sale.ReceiptNumber);
                        table.Cell().Text(sale.SoldAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                        table.Cell().Text(sale.PaymentMethod);
                        table.Cell().AlignRight().Text(sale.TotalAmount.ToString("C", CultureInfo.GetCultureInfo("pt-BR")));
                    }
                });
            });
        });
        return document.GeneratePdf();
    }

    public byte[] ExportExcel(SalesReportRequest request)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Vendas");
        var headers = new[] { "Venda", "Data", "ClienteId", "OperadorId", "Pagamento", "Status", "Subtotal", "Desconto", "Acrescimo", "Total", "LucroBruto" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }
        var row = 2;
        foreach (var sale in Query(request))
        {
            sheet.Cell(row, 1).Value = sale.ReceiptNumber;
            sheet.Cell(row, 2).Value = sale.SoldAtUtc;
            sheet.Cell(row, 3).Value = sale.CustomerId.ToString();
            sheet.Cell(row, 4).Value = sale.UserId?.ToString() ?? string.Empty;
            sheet.Cell(row, 5).Value = sale.PaymentMethod;
            sheet.Cell(row, 6).Value = sale.Status.ToString();
            sheet.Cell(row, 7).Value = sale.SubtotalAmount;
            sheet.Cell(row, 8).Value = sale.DiscountAmount;
            sheet.Cell(row, 9).Value = sale.SurchargeAmount;
            sheet.Cell(row, 10).Value = sale.TotalAmount;
            sheet.Cell(row, 11).Value = GrossProfit(sale.Id);
            row++;
        }
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public IReadOnlyList<ProductSalesSummary> BestSellingProducts(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var sales = _db.Sales.AsNoTracking().Where(x => x.Status == SaleStatus.Finalizada);
        if (fromUtc.HasValue) sales = sales.Where(x => x.SoldAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) sales = sales.Where(x => x.SoldAtUtc <= toUtc.Value);
        var saleIds = sales.Select(x => x.Id);
        return (from item in _db.SaleItems.AsNoTracking()
                join product in _db.Products.AsNoTracking() on item.ProductId equals product.Id
                where saleIds.Contains(item.SaleId)
                group new { item, product } by new { product.Id, product.Sku, product.Name } into g
                orderby g.Sum(x => x.item.Quantity) descending
                select new ProductSalesSummary(g.Key.Id, g.Key.Sku, g.Key.Name, g.Sum(x => x.item.Quantity), g.Sum(x => x.item.TotalItem)))
            .Take(50)
            .ToList();
    }

    private IQueryable<Sale> Query(SalesReportRequest request)
    {
        var query = _db.Sales.AsNoTracking().AsQueryable();
        if (request.FromUtc.HasValue) query = query.Where(x => x.SoldAtUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(x => x.SoldAtUtc <= request.ToUtc.Value);
        if (request.CustomerId.HasValue) query = query.Where(x => x.CustomerId == request.CustomerId.Value);
        if (request.UserId.HasValue) query = query.Where(x => x.UserId == request.UserId.Value);
        if (request.OnlyCancelled) query = query.Where(x => x.Status == SaleStatus.Cancelada);
        if (request.OnlyCredit) query = query.Where(x => x.PaymentMethod == "PRAZO" || _db.SalePayments.Any(p => p.SaleId == x.Id && p.PaymentMethod == "PRAZO"));
        if (!string.IsNullOrWhiteSpace(request.PaymentMethod)) query = query.Where(x => x.PaymentMethod == request.PaymentMethod.Trim().ToUpperInvariant() || _db.SalePayments.Any(p => p.SaleId == x.Id && p.PaymentMethod == request.PaymentMethod.Trim().ToUpperInvariant()));
        if (request.ProductId.HasValue)
        {
            var ids = _db.SaleItems.Where(x => x.ProductId == request.ProductId.Value).Select(x => x.SaleId);
            query = query.Where(x => ids.Contains(x.Id));
        }
        return query.OrderByDescending(x => x.SoldAtUtc);
    }

    private decimal GrossProfit(Guid saleId)
    {
        return (from item in _db.SaleItems.AsNoTracking().Where(x => x.SaleId == saleId)
                join product in _db.Products.AsNoTracking() on item.ProductId equals product.Id
                select item.TotalItem - (item.Quantity * product.CostPrice)).Sum();
    }

    private static void Header(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(text).SemiBold();
    }
}
