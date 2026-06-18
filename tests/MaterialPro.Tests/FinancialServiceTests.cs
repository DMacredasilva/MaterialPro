using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaterialPro.Tests;

public sealed class FinancialServiceTests
{
    [Fact]
    public void PayableBaixa_Updates_Balance_And_Status()
    {
        using var db = CreateDbContext();
        var service = new FinancialService(db);

        var payable = service.CreatePayable(new AccountPayableRequest(
            "AP-1",
            "Fornecedor Teste",
            "Compra de material",
            100m,
            DateTime.UtcNow.AddDays(10),
            "PIX"));

        var updated = service.PayableBaixa(payable.Id, 100m, "PIX");

        Assert.Equal(0m, updated.BalanceAmount);
        Assert.Equal(FinancialStatus.Paid, updated.Status);
    }

    [Fact]
    public void Financial_Creates_Receivable_And_Settles_Partial_Then_Total()
    {
        using var db = CreateDbContext();
        var service = new FinancialService(db);
        var receivable = service.CreateReceivable(new AccountReceivableRequest("AR-1", null, null, "Cliente", "Venda a prazo", 200m, DateTime.UtcNow.AddDays(5), "PRAZO"));

        var partial = service.SettleReceivable(new FinancialSettlementRequest(receivable.Id, FinancialType.Receivable, 80m, PaymentMethod: "PIX"));
        var partialStatus = partial.Status;
        var paid = service.SettleReceivable(new FinancialSettlementRequest(receivable.Id, FinancialType.Receivable, 120m, PaymentMethod: "PIX"));

        Assert.Equal(FinancialStatus.Partial, partialStatus);
        Assert.Equal(FinancialStatus.Paid, paid.Status);
        Assert.Equal(2, db.FinancialSettlements.Count());
    }

    [Fact]
    public void Financial_Blocks_Settlement_Greater_Than_Balance()
    {
        using var db = CreateDbContext();
        var service = new FinancialService(db);
        var payable = service.CreatePayable(new AccountPayableRequest("AP-2", "Fornecedor", "Compra", 50m, DateTime.UtcNow.AddDays(1), "PIX"));

        Assert.Throws<InvalidOperationException>(() => service.SettlePayable(new FinancialSettlementRequest(payable.Id, FinancialType.Payable, 51m)));
    }

    [Fact]
    public void Financial_Applies_Interest_Fine_Discount()
    {
        using var db = CreateDbContext();
        var service = new FinancialService(db);
        var payable = service.CreatePayable(new AccountPayableRequest("AP-3", "Fornecedor", "Compra", 100m, DateTime.UtcNow.AddDays(1), "PIX"));

        var updated = service.SettlePayable(new FinancialSettlementRequest(payable.Id, FinancialType.Payable, 95m, Interest: 10m, Fine: 5m, Discount: 20m, PaymentMethod: "PIX"));

        Assert.Equal(FinancialStatus.Paid, updated.Status);
        Assert.Equal(10m, updated.InterestAmount);
        Assert.Equal(5m, updated.FineAmount);
        Assert.Equal(20m, updated.DiscountAmount);
    }

    [Fact]
    public void Financial_Duplicate_Settlement_And_Cancellation_Work()
    {
        using var db = CreateDbContext();
        var service = new FinancialService(db);
        var duplicate = service.CreateDuplicate(new DuplicateRequest("DUP-1", FinancialType.Receivable, null, null, 90m, DateTime.UtcNow.AddDays(10)));

        service.SettleDuplicate(new FinancialSettlementRequest(duplicate.Id, FinancialType.Receivable, 40m));
        var cancelled = service.CancelDuplicate(duplicate.Id, "Acordo substituido");

        Assert.Equal(FinancialStatus.Cancelled, cancelled.Status);
        Assert.NotEmpty(db.FinancialLogs);
    }

    [Fact]
    public void Financial_Receivable_Settlement_Registers_Cash_Movement()
    {
        using var db = CreateDbContext();
        var user = Guid.NewGuid();
        var cash = new CashSession { Code = "CX-FIN", OpeningAmount = 10m, CashAmount = 10m, CurrentAmount = 10m, OpenedByUserId = user, Status = CashSessionStatus.Aberto };
        db.CashSessions.Add(cash);
        db.SaveChanges();
        var service = new FinancialService(db);
        var receivable = service.CreateReceivable(new AccountReceivableRequest("AR-2", null, null, "Cliente", "Receber", 100m, DateTime.UtcNow, "PRAZO"));

        service.SettleReceivable(new FinancialSettlementRequest(receivable.Id, FinancialType.Receivable, 100m, PaymentMethod: "DINHEIRO", UserId: user, CashSessionId: cash.Id));

        Assert.Single(db.CashMovements.Where(x => x.Type == CashMovementType.DuplicateReceipt));
        Assert.Equal(110m, db.CashSessions.Single().CashAmount);
    }

    [Fact]
    public void Financial_Dashboard_Flow_And_Exports_Work()
    {
        using var db = CreateDbContext();
        var service = new FinancialService(db);
        service.CreatePayable(new AccountPayableRequest("AP-4", "Fornecedor", "Conta", 70m, DateTime.UtcNow.Date, "PIX"));
        service.CreateReceivable(new AccountReceivableRequest("AR-4", null, null, "Cliente", "Conta", 120m, DateTime.UtcNow.Date, "PIX"));

        var dashboard = service.Dashboard(DateTime.UtcNow);
        var flow = service.CashFlow(DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(2));

        Assert.Equal(70m, dashboard.PayableToday);
        Assert.Equal(120m, dashboard.ReceivableToday);
        Assert.NotEmpty(flow);
        Assert.True(service.ExportPdf(new FinancialSearchRequest()).Length > 100);
        Assert.True(service.ExportExcel(new FinancialSearchRequest()).Length > 100);
    }

    [Fact]
    public void CancelSale_Marks_Sale_Cancels_Duplicate_And_Restores_Stock()
    {
        using var db = CreateDbContext();
        var hasher = new Sha256PasswordHasher();
        var auth = new AuthService(new EfUserRepository(db), hasher);
        var manager = auth.CreateAdmin("Gerente", "gerente", "gerente@test.local", "Senha@123");
        var productService = new ProductService(db);
        var inventory = new InventoryService(db);
        var sales = new SalesService(db);
        var financial = new FinancialService(db);
        var cancellations = new SaleCancellationService(db, hasher);

        var product = productService.Create(new ProductUpsertRequest("P-1", "Produto", "UN", 10m, 5m, 0m, ""));
        inventory.Move(product.Id, 5m, "Entrada", "TESTE");
        var sale = sales.CreateSale(
            new SaleCreateRequest(null, "PIX", 0m, 10m, "V-1"),
            new[] { new SaleItemRequest(product.Id, 1m, 10m, 0m) });
        financial.CreateDuplicate(new DuplicateRequest("D-1", FinancialType.Receivable, sale.Id, null, 10m, DateTime.UtcNow.AddDays(1)));

        cancellations.CancelSale(new SaleCancellationRequest(sale.Id, "Cliente desistiu", manager.Id, "Senha@123", "Teste"));

        Assert.Equal(SaleStatus.Cancelada, db.Sales.Single().Status);
        Assert.False(db.Sales.Single().IsActive);
        Assert.Equal(5m, db.Products.Single().StockQuantity);
        Assert.Equal(FinancialStatus.Cancelled, db.Duplicates.Single().Status);
        Assert.Single(db.SaleCancellations);
    }

    private static MaterialProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MaterialProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MaterialProDbContext(options);
    }
}
