using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaterialPro.Tests;

public sealed class ReportsCenterServiceTests
{
    [Fact]
    public void ReportsCenter_Generates_Sales_Report_With_Totals()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var service = new ReportsCenterService(db);

        var result = service.Generate(new ReportFilterRequest("sales.period", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)), ctx.Admin);

        Assert.Single(result.Rows);
        Assert.Equal(100m, result.Totals["Total"]);
    }

    [Fact]
    public void ReportsCenter_Generates_Cash_Stock_And_Financial_Reports()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var service = new ReportsCenterService(db);

        Assert.NotEmpty(service.Generate(new ReportFilterRequest("cash.daily"), ctx.Admin).Rows);
        Assert.NotEmpty(service.Generate(new ReportFilterRequest("stock.current"), ctx.Admin).Rows);
        Assert.NotEmpty(service.Generate(new ReportFilterRequest("financial.receivables"), ctx.Admin).Rows);
    }

    [Fact]
    public void ReportsCenter_Exports_Pdf_Excel_And_Print_Summary()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var service = new ReportsCenterService(db);
        var request = new ReportFilterRequest("sales.period", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        Assert.True(service.ExportPdf(request, ctx.Admin).Length > 100);
        Assert.True(service.ExportExcel(request, ctx.Admin).Length > 100);
        Assert.True(service.PrintSummary(request, ctx.Admin).Length > 100);
        Assert.True(db.ReportAuditLogs.Count() >= 3);
    }

    [Fact]
    public void ReportsCenter_Validates_Filters_And_Dashboard()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var service = new ReportsCenterService(db);

        var empty = service.Generate(new ReportFilterRequest("sales.period", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-9)), ctx.Admin);
        var dash = service.Dashboard(new ReportFilterRequest("sales.period", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)), ctx.Admin);

        Assert.Empty(empty.Rows);
        Assert.Equal(100m, dash.TotalSales);
        Assert.True(dash.StockValue > 0);
    }

    [Fact]
    public void ReportsCenter_Blocks_Financial_Report_For_Cashier()
    {
        using var db = CreateDbContext();
        var ctx = Seed(db);
        var service = new ReportsCenterService(db);

        Assert.Throws<UnauthorizedAccessException>(() => service.Generate(new ReportFilterRequest("financial.payables"), ctx.Cashier));
    }

    [Fact]
    public void ReportsCenter_Creates_Schedule()
    {
        using var db = CreateDbContext();
        Seed(db);
        var service = new ReportsCenterService(db);

        var schedule = service.Schedule("sales.period", "Mensal", "C:\\Relatorios");

        Assert.Equal("sales.period", schedule.ReportKey);
        Assert.Single(db.ReportSchedules);
    }

    private static SeedContext Seed(MaterialProDbContext db)
    {
        var admin = new AppUser { FullName = "Admin", Username = "admin", Role = UserRole.Admin };
        var cashier = new AppUser { FullName = "Caixa", Username = "caixa", Role = UserRole.Cashier };
        var customer = new Customer { FullName = "Cliente Relatorio", Code = "C1", DocumentNumber = "123" };
        var product = new Product { Sku = "P1", Name = "Cimento", Category = "Basico", Brand = "Marca", StockQuantity = 10m, MinimumStock = 2m, CostPrice = 40m, SalePrice = 50m };
        var sale = new Sale { CustomerId = customer.Id, UserId = admin.Id, ReceiptNumber = "V001", SoldAtUtc = DateTime.UtcNow, SubtotalAmount = 100m, TotalAmount = 100m, PaymentMethod = "PIX", Status = SaleStatus.Finalizada };
        var item = new SaleItem { SaleId = sale.Id, ProductId = product.Id, ProductCode = product.Sku, ProductDescription = product.Name, Quantity = 2m, UnitPrice = 50m, TotalItem = 100m };
        var cash = new CashSession { Code = "CX1", OpenedByUserId = admin.Id, OpenedAtUtc = DateTime.UtcNow, OpeningAmount = 50m, CashAmount = 50m, PixAmount = 100m, TotalSalesAmount = 100m, Status = CashSessionStatus.Aberto };
        var receivable = new AccountReceivable { Number = "R1", CustomerId = customer.Id, CustomerName = customer.FullName, Description = "Prazo", OriginalAmount = 100m, BalanceAmount = 100m, DueDateUtc = DateTime.UtcNow.AddDays(10), Status = FinancialStatus.Open };
        var payable = new AccountPayable { Number = "P1", SupplierName = "Fornecedor", Description = "Compra", OriginalAmount = 60m, BalanceAmount = 60m, DueDateUtc = DateTime.UtcNow.AddDays(5), Status = FinancialStatus.Open };
        db.AddRange(admin, cashier, customer, product, sale, item, cash, receivable, payable);
        db.SaveChanges();
        return new SeedContext(admin, cashier);
    }

    private static MaterialProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MaterialProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new MaterialProDbContext(options);
    }

    private sealed record SeedContext(AppUser Admin, AppUser Cashier);
}
