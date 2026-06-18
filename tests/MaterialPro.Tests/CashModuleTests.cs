using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaterialPro.Tests;

public sealed class CashModuleTests
{
    [Fact]
    public void CashService_Opens_Cash_And_Blocks_Duplicate_For_User()
    {
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var cash = new CashService(db);

        var session = cash.Open(new CashOpenRequest(100m, userId));

        Assert.Equal(CashSessionStatus.Aberto, session.Status);
        Assert.Single(db.CashMovements.Where(x => x.Type == CashMovementType.Opening));
        Assert.Throws<InvalidOperationException>(() => cash.Open(new CashOpenRequest(50m, userId)));
    }

    [Fact]
    public void Pdv_Blocks_Sale_Without_Open_Cash()
    {
        using var db = CreateDbContext();
        var pdv = new PdvService(db);

        Assert.Throws<InvalidOperationException>(() => pdv.CreateSale(new PdvCreateSaleRequest(null, Guid.NewGuid(), null)));
    }

    [Fact]
    public void Pdv_Registers_Sale_Amounts_In_Cash()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var pdv = new PdvService(db);
        var sale = pdv.CreateSale(new PdvCreateSaleRequest(ctx.Customer.Id, ctx.User.Id, ctx.Cash.Id));
        pdv.AddItem(sale.Id, new PdvSaleItemRequest(ctx.Product.Id, 2m, 10m));

        pdv.FinalizeSale(new PdvFinalizeRequest(sale.Id, [new PdvPaymentRequest("PIX", 20m)]));

        var cash = db.CashSessions.Single();
        Assert.Equal(20m, cash.TotalSalesAmount);
        Assert.Equal(20m, cash.PixAmount);
        Assert.Single(db.CashMovements.Where(x => x.Type == CashMovementType.Sale));
    }

    [Fact]
    public void CashService_Supplies_And_Withdraws_With_Manager()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var cash = new CashService(db, ctx.Hasher);

        cash.Supply(new CashSupplyRequest(ctx.Cash.Id, 50m, "Troco", ctx.User.Id));
        cash.Withdraw(new CashWithdrawalRequest(ctx.Cash.Id, 30m, "Deposito", ctx.User.Id, "Senha@123"));

        var session = db.CashSessions.Single();
        Assert.Equal(50m, session.SupplyAmount);
        Assert.Equal(30m, session.WithdrawalAmount);
        Assert.Equal(120m, session.CashAmount);
    }

    [Fact]
    public void CashService_Blocks_Withdrawal_Without_Manager()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var cash = new CashService(db, ctx.Hasher);

        Assert.Throws<InvalidOperationException>(() => cash.Withdraw(new CashWithdrawalRequest(ctx.Cash.Id, 10m, "Teste", ctx.User.Id, "")));
    }

    [Fact]
    public void CashService_Closes_Cash_And_Calculates_Difference()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var cash = new CashService(db, ctx.Hasher);
        cash.Supply(new CashSupplyRequest(ctx.Cash.Id, 20m, "Troco", ctx.User.Id));

        var closed = cash.Close(new CashCloseRequest(ctx.Cash.Id, ctx.User.Id, 110m, 0m, 0m, 0m, "Conferencia"));

        Assert.Equal(CashSessionStatus.Fechado, closed.Status);
        Assert.Equal(-10m, closed.DifferenceAmount);
        Assert.Throws<InvalidOperationException>(() => cash.Supply(new CashSupplyRequest(ctx.Cash.Id, 1m, "Depois", ctx.User.Id)));
    }

    [Fact]
    public void CashService_Prints_And_Queries_History()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var cash = new CashService(db, ctx.Hasher);
        var reports = new CashReportService(db);

        Assert.True(cash.PrintOpening(ctx.Cash.Id).Length > 100);
        Assert.NotEmpty(cash.History(new CashHistoryRequest(Code: "CX")));
        Assert.True(reports.ExportPdf(new CashReportRequest()).Length > 100);
        Assert.True(reports.ExportExcel(new CashReportRequest()).Length > 100);
    }

    private static SeedContext Seed(MaterialProDbContext db)
    {
        var hasher = new Sha256PasswordHasher();
        var auth = new AuthService(new EfUserRepository(db), hasher);
        var user = auth.CreateAdmin("Gerente", "gerente", "gerente@test.local", "Senha@123");
        var customer = new Customer { FullName = "Cliente Caixa", DocumentNumber = "123" };
        var product = new Product { Sku = "CX-1", Name = "Produto", Unit = "UN", SalePrice = 10m, CostPrice = 5m, StockQuantity = 10m };
        var cash = new CashSession { Code = "CX-TESTE", OpeningAmount = 100m, CurrentAmount = 100m, CashAmount = 100m, OpenedByUserId = user.Id, Status = CashSessionStatus.Aberto };
        db.Customers.Add(customer);
        db.Products.Add(product);
        db.CashSessions.Add(cash);
        db.SaveChanges();
        return new SeedContext(user, customer, product, cash, hasher);
    }

    private static MaterialProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MaterialProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new MaterialProDbContext(options);
    }

    private sealed record SeedContext(AppUser User, Customer Customer, Product Product, CashSession Cash, IPasswordHasher Hasher);
}
