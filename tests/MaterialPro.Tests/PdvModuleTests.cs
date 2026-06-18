using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaterialPro.Tests;

public sealed class PdvModuleTests
{
    [Fact]
    public void Pdv_Creates_Sale_Adds_Removes_Item_And_Calculates_Total()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var pdv = new PdvService(db);

        var sale = pdv.CreateSale(new PdvCreateSaleRequest(ctx.Customer.Id, ctx.UserId, ctx.Cash.Id));
        var item = pdv.AddItem(sale.Id, new PdvSaleItemRequest(ctx.Product.Id, 2m, 10m, 1m));
        sale = pdv.ApplyDiscount(sale.Id, 2m);

        Assert.Equal(17m, db.Sales.Single().TotalAmount);

        pdv.RemoveItem(item.Id);

        Assert.Empty(db.SaleItems);
        Assert.Equal(0m, db.Sales.Single().TotalAmount);
    }

    [Theory]
    [InlineData("DINHEIRO")]
    [InlineData("PIX")]
    [InlineData("CARTAO_DEBITO")]
    [InlineData("CARTAO_CREDITO")]
    public void Pdv_Finalizes_Sale_With_Immediate_Payment_And_Deducts_Stock(string method)
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var pdv = new PdvService(db);

        var sale = pdv.CreateSale(new PdvCreateSaleRequest(ctx.Customer.Id, ctx.UserId, ctx.Cash.Id));
        pdv.AddItem(sale.Id, new PdvSaleItemRequest(ctx.Product.Id, 2m, 10m));

        var finalized = pdv.FinalizeSale(new PdvFinalizeRequest(sale.Id, [new PdvPaymentRequest(method, 20m)]));

        Assert.Equal(SaleStatus.Finalizada, finalized.Status);
        Assert.Equal(8m, db.Products.Single().StockQuantity);
        Assert.Single(db.SalePayments);
        Assert.Single(db.CashMovements.Where(x => x.Type == CashMovementType.Sale));
        Assert.Contains(db.StockMovements, x => x.Type == StockMovementType.SaleExit);
    }

    [Fact]
    public void Pdv_Finalizes_Credit_Sale_And_Generates_Duplicates()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var pdv = new PdvService(db);

        var sale = pdv.CreateSale(new PdvCreateSaleRequest(ctx.Customer.Id, ctx.UserId, ctx.Cash.Id));
        pdv.AddItem(sale.Id, new PdvSaleItemRequest(ctx.Product.Id, 3m, 10m));
        pdv.FinalizeSale(new PdvFinalizeRequest(sale.Id, [new PdvPaymentRequest("PRAZO", 30m, 3, DateTime.UtcNow.AddDays(30))]));

        Assert.Equal(3, db.Duplicates.Count());
        Assert.Equal(3, db.AccountsReceivable.Count());
        Assert.Empty(db.CashMovements.Where(x => x.Type == CashMovementType.Sale));
    }

    [Fact]
    public void Pdv_Cancels_Sale_And_Restores_Stock()
    {
        using var db = CreateDbContext();
        var hasher = new Sha256PasswordHasher();
        var auth = new AuthService(new EfUserRepository(db), hasher);
        var manager = auth.CreateAdmin("Gerente", "gerente", "gerente@test.local", "Senha@123");
        var ctx = Seed(db, manager.Id);
        var pdv = new PdvService(db, hasher);

        var sale = pdv.CreateSale(new PdvCreateSaleRequest(ctx.Customer.Id, manager.Id, ctx.Cash.Id));
        pdv.AddItem(sale.Id, new PdvSaleItemRequest(ctx.Product.Id, 2m, 10m));
        pdv.FinalizeSale(new PdvFinalizeRequest(sale.Id, [new PdvPaymentRequest("PIX", 20m)]));

        pdv.CancelSale(new SaleCancellationRequest(sale.Id, "Erro na venda", manager.Id, "Senha@123", ""));

        Assert.Equal(SaleStatus.Cancelada, db.Sales.Single().Status);
        Assert.Equal(10m, db.Products.Single().StockQuantity);
        Assert.Single(db.SaleCancellations);
    }

    [Fact]
    public void Pdv_Generates_Receipt_And_Blocks_Sale_Without_Open_Cash()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var pdv = new PdvService(db);
        var sale = pdv.CreateSale(new PdvCreateSaleRequest(ctx.Customer.Id, ctx.UserId, ctx.Cash.Id));
        pdv.AddItem(sale.Id, new PdvSaleItemRequest(ctx.Product.Id, 1m, 10m));
        pdv.FinalizeSale(new PdvFinalizeRequest(sale.Id, [new PdvPaymentRequest("DINHEIRO", 10m)]));

        Assert.True(pdv.GenerateReceiptPdf(new PdvReceiptRequest(sale.Id)).Length > 100);

        using var db2 = CreateDbContext();
        var pdv2 = new PdvService(db2);
        Assert.Throws<InvalidOperationException>(() => pdv2.CreateSale(new PdvCreateSaleRequest(null, Guid.NewGuid(), null)));
    }

    private static SeedContext Seed(MaterialProDbContext db, Guid? userId = null)
    {
        var user = userId ?? Guid.NewGuid();
        var customer = new Customer { FullName = "Cliente PDV", DocumentNumber = "123" };
        var product = new Product { Sku = "PDV-1", Name = "Cimento", Unit = "SC", SalePrice = 10m, CostPrice = 6m, StockQuantity = 10m };
        var cash = new CashSession { Code = "CX-TESTE", OpeningAmount = 100m, CurrentAmount = 100m, OpenedByUserId = user };
        db.Customers.Add(customer);
        db.Products.Add(product);
        db.CashSessions.Add(cash);
        db.SaveChanges();
        return new SeedContext(customer, product, cash, user);
    }

    private static MaterialProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MaterialProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MaterialProDbContext(options);
    }

    private sealed record SeedContext(Customer Customer, Product Product, CashSession Cash, Guid UserId);
}
