using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaterialPro.Tests;

public sealed class CustomerModuleTests
{
    [Fact]
    public void CustomerService_Creates_And_Searches_By_Document_And_WhatsApp()
    {
        using var db = CreateDbContext();
        var service = new CustomerService(db);

        var customer = service.Create(new CustomerUpsertRequest(
            "Cliente Teste",
            "12345678900",
            "1133334444",
            "cliente@test.local",
            "Rua A",
            "Sao Paulo",
            WhatsApp: "11999998888",
            CreditLimit: 500m));

        var found = service.FindByCodeOrDocument("12345678900");
        var search = service.Search(new CustomerSearchRequest("99999"));

        Assert.Equal(customer.Id, found?.Id);
        Assert.Single(search);
        Assert.Equal(500m, search[0].CreditLimit);
    }

    [Fact]
    public void CustomerService_Blocks_And_Inactivates()
    {
        using var db = CreateDbContext();
        var service = new CustomerService(db);
        var customer = service.Create(new CustomerUpsertRequest("Cliente Bloqueio", "", "", "", "", ""));

        service.Block(customer.Id, "Inadimplencia");
        service.Inactivate(customer.Id);

        var updated = service.FindById(customer.Id);
        Assert.True(updated?.IsBlocked);
        Assert.False(updated?.IsActive);
    }

    [Fact]
    public void CustomerService_Returns_History_And_Credit_Summary()
    {
        using var db = CreateDbContext();
        var customers = new CustomerService(db);
        var products = new ProductService(db);
        var inventory = new InventoryService(db);
        var sales = new SalesService(db);

        var customer = customers.Create(new CustomerUpsertRequest("Cliente Historico", "", "", "", "", "", CreditLimit: 1000m));
        var product = products.Create(new ProductUpsertRequest("H-1", "Produto H", "UN", 100m, 60m, 0m, ""));
        inventory.Move(product.Id, 3m, "Entrada", "TESTE");
        var sale = sales.CreateSale(new SaleCreateRequest(customer.Id, "Prazo", 0m, 0m, "V-H-1"), new[] { new SaleItemRequest(product.Id, 1m, 100m, 0m) });
        db.AccountsReceivable.Add(new AccountReceivable
        {
            Number = "AR-1",
            SaleId = sale.Id,
            CustomerName = customer.FullName,
            Description = "Venda a prazo",
            OriginalAmount = 100m,
            BalanceAmount = 100m,
            DueDateUtc = DateTime.UtcNow.AddDays(10),
            Status = FinancialStatus.Open
        });
        db.SaveChanges();

        var purchases = customers.PurchaseHistory(customer.Id);
        var financial = customers.FinancialHistory(customer.Id);
        var credit = customers.CreditSummary(customer.Id);

        Assert.Single(purchases);
        Assert.Single(financial);
        Assert.Equal(900m, credit.AvailableCredit);
    }

    [Fact]
    public void CustomerReportService_Exports_Pdf_Excel_Csv_And_Ficha()
    {
        using var db = CreateDbContext();
        var customers = new CustomerService(db);
        var customer = customers.Create(new CustomerUpsertRequest("Cliente Relatorio", "123", "11", "r@test.local", "Rua", "Cidade", WhatsApp: "22"));
        var reports = new CustomerReportService(db);

        Assert.True(reports.ExportPdf(new CustomerReportRequest()).Length > 100);
        Assert.True(reports.ExportExcel(new CustomerReportRequest()).Length > 100);
        Assert.True(reports.ExportCsv(new CustomerReportRequest()).Length > 30);
        Assert.True(reports.CustomerFichaPdf(customer.Id).Length > 100);
    }

    private static MaterialProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MaterialProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MaterialProDbContext(options);
    }
}
