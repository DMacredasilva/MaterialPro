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
