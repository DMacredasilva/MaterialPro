using System.Text;
using MaterialPro.Application;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaterialPro.Tests;

public sealed class ProductModuleTests
{
    [Fact]
    public void ProductService_Creates_And_Searches_Product()
    {
        using var db = CreateDbContext();
        var service = new ProductService(db);

        var product = service.Create(new ProductUpsertRequest(
            "SKU-1",
            "Cimento CP II",
            "SC",
            39.90m,
            30m,
            5m,
            "789000000001",
            Category: "Construcao",
            Brand: "MaterialPro"));

        var found = service.FindBySkuOrBarcode("789000000001");
        var search = service.Search(new ProductSearchRequest("cimento"));

        Assert.Equal(product.Id, found?.Id);
        Assert.Single(search);
        Assert.Equal("Construcao", search[0].Category);
    }

    [Fact]
    public void ProductSearch_Returns_Low_Stock()
    {
        using var db = CreateDbContext();
        var products = new ProductService(db);
        var inventory = new InventoryService(db);
        var product = products.Create(new ProductUpsertRequest("SKU-2", "Argamassa", "UN", 20m, 12m, 10m, ""));

        inventory.Move(product.Id, 3m, "Entrada", "TESTE");

        var lowStock = products.Search(new ProductSearchRequest(OnlyLowStock: true));

        Assert.Single(lowStock);
        Assert.Equal("SKU-2", lowStock[0].Sku);
    }

    [Fact]
    public void ProductImportService_Imports_Csv()
    {
        using var db = CreateDbContext();
        var importer = new ProductImportService(db);
        var file = Path.Combine(Path.GetTempPath(), $"produtos-{Guid.NewGuid():N}.csv");
        File.WriteAllText(file, "Sku;Name;Unit;SalePrice;CostPrice;MinimumStock;Barcode;Category;Brand\r\nCSV-1;Tinta Latex;GL;120,50;80;2;7899;Pintura;Marca A", Encoding.UTF8);

        var result = importer.ImportCsv(file, new ProductImportOptions(UpdateExisting: true, IgnoreDuplicates: true));

        Assert.Equal(1, result.ImportedRows);
        Assert.Equal("Tinta Latex", db.Products.Single().Name);
        Assert.Equal(120.50m, db.Products.Single().SalePrice);
    }

    [Fact]
    public void ProductReportService_Exports_Pdf_And_Excel()
    {
        using var db = CreateDbContext();
        var products = new ProductService(db);
        products.Create(new ProductUpsertRequest("SKU-3", "Brita", "M3", 90m, 60m, 1m, "", Category: "Agregados"));
        var reports = new ProductReportService(db);

        var pdf = reports.ExportPdf(new ProductReportRequest());
        var excel = reports.ExportExcel(new ProductReportRequest());

        Assert.True(pdf.Length > 100);
        Assert.True(excel.Length > 100);
    }

    private static MaterialProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MaterialProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MaterialProDbContext(options);
    }
}
