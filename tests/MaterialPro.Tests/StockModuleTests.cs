using System.Text;
using ClosedXML.Excel;
using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaterialPro.Tests;

public sealed class StockModuleTests
{
    [Fact]
    public void InventoryService_Registers_Entry_And_Exit()
    {
        using var db = CreateDbContext();
        var product = Product(db);
        var inventory = new InventoryService(db);

        inventory.EnterStock(new StockMoveRequest(product.Id, 10m, StockMovementType.ManualEntry, "Compra"));
        inventory.ExitStock(new StockMoveRequest(product.Id, 3m, StockMovementType.SaleExit, "Venda"));

        Assert.Equal(7m, inventory.GetStock(product.Id));
        Assert.Equal(2, db.StockMovements.Count());
    }

    [Fact]
    public void InventoryService_Adjusts_Stock_With_Reason()
    {
        using var db = CreateDbContext();
        var product = Product(db);
        var inventory = new InventoryService(db);
        inventory.EnterStock(new StockMoveRequest(product.Id, 5m, StockMovementType.ManualEntry, "Entrada"));

        inventory.AdjustStock(new StockAdjustRequest(product.Id, 12m, "Contagem física"));

        Assert.Equal(12m, inventory.GetStock(product.Id));
        Assert.Contains(db.StockMovements, x => x.Type == StockMovementType.AdjustmentEntry);
    }

    [Fact]
    public void InventoryService_Does_Not_Allow_Negative_Without_Authorization()
    {
        using var db = CreateDbContext();
        var product = Product(db);
        var inventory = new InventoryService(db);

        Assert.Throws<InvalidOperationException>(() => inventory.ExitStock(new StockMoveRequest(product.Id, 1m, StockMovementType.SaleExit, "Venda")));
    }

    [Fact]
    public void InventoryService_Handles_Inventory_Transfer_And_Reservation()
    {
        using var db = CreateDbContext();
        var product = Product(db);
        var inventory = new InventoryService(db);
        inventory.EnterStock(new StockMoveRequest(product.Id, 20m, StockMovementType.ManualEntry, "Entrada"));

        var inv = inventory.StartInventory(new StockInventoryRequest(null, "Geral"));
        inventory.CountInventoryItem(inv.Id, new StockInventoryItemRequest(product.Id, 18m));
        inventory.CloseInventory(inv.Id, applyAdjustments: true);
        inventory.Transfer(new StockTransferRequest(product.Id, 2m, "Loja", "Depósito Principal", null));
        var reservation = inventory.Reserve(new StockReservationRequest(product.Id, 5m, "Pedido", "P001"));
        inventory.ReleaseReservation(reservation.Id);

        Assert.Equal(18m, inventory.GetStock(product.Id));
        Assert.Equal(StockInventoryStatus.Closed, db.StockInventories.Single().Status);
        Assert.Single(db.StockTransfers);
        Assert.Equal(StockReservationStatus.Released, db.StockReservations.Single().Status);
    }

    [Fact]
    public void StockReportService_Exports_Pdf_And_Excel()
    {
        using var db = CreateDbContext();
        var product = Product(db);
        var inventory = new InventoryService(db);
        inventory.EnterStock(new StockMoveRequest(product.Id, 10m, StockMovementType.ManualEntry, "Entrada"));
        var reports = new StockReportService(db);

        Assert.True(reports.ExportPdf(new StockReportRequest()).Length > 100);
        Assert.True(reports.ExportExcel(new StockReportRequest()).Length > 100);
    }

    [Fact]
    public void StockImportService_Imports_Csv_Excel_And_Dbf()
    {
        using var db = CreateDbContext();
        Product(db, "IMP-1");
        var importer = new StockImportService(db);

        var csv = Path.Combine(Path.GetTempPath(), $"estoque-{Guid.NewGuid():N}.csv");
        File.WriteAllText(csv, "Sku;Quantity;Reason\r\nIMP-1;4;CSV", Encoding.UTF8);
        var csvResult = importer.ImportCsv(csv, new StockImportOptions(true, false));

        var xlsx = Path.Combine(Path.GetTempPath(), $"estoque-{Guid.NewGuid():N}.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Estoque");
            sheet.Cell(1, 1).Value = "Sku";
            sheet.Cell(1, 2).Value = "Quantity";
            sheet.Cell(2, 1).Value = "IMP-1";
            sheet.Cell(2, 2).Value = 3;
            workbook.SaveAs(xlsx);
        }
        var excelResult = importer.ImportExcel(xlsx, new StockImportOptions(true, false));

        var dbf = Path.Combine(Path.GetTempPath(), $"estoque-{Guid.NewGuid():N}.dbf");
        WriteDbf(dbf, new[] { ("SKU", 12), ("QUANTITY", 10) }, new[] { new[] { "IMP-1", "2" } });
        var dbfResult = importer.ImportDbf(dbf, new StockImportOptions(true, false));

        Assert.Equal(1, csvResult.ImportedRows);
        Assert.Equal(1, excelResult.ImportedRows);
        Assert.Equal(1, dbfResult.ImportedRows);
        Assert.Equal(9m, db.Products.Single().StockQuantity);
    }

    private static Product Product(MaterialProDbContext db, string sku = "EST-1")
    {
        var product = new Product { Sku = sku, Name = "Produto Estoque", Unit = "UN", SalePrice = 10m, CostPrice = 5m, MinimumStock = 2m };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    private static MaterialProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MaterialProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MaterialProDbContext(options);
    }

    private static void WriteDbf(string path, (string Name, int Length)[] fields, string[][] rows)
    {
        using var stream = File.Create(path);
        var headerLength = (short)(32 + fields.Length * 32 + 1);
        var recordLength = (short)(1 + fields.Sum(x => x.Length));
        var header = new byte[32];
        header[0] = 0x03;
        var now = DateTime.Now;
        header[1] = (byte)(now.Year - 1900);
        header[2] = (byte)now.Month;
        header[3] = (byte)now.Day;
        BitConverter.GetBytes(rows.Length).CopyTo(header, 4);
        BitConverter.GetBytes(headerLength).CopyTo(header, 8);
        BitConverter.GetBytes(recordLength).CopyTo(header, 10);
        stream.Write(header);

        foreach (var field in fields)
        {
            var descriptor = new byte[32];
            Encoding.ASCII.GetBytes(field.Name.PadRight(11, '\0')[..11]).CopyTo(descriptor, 0);
            descriptor[11] = (byte)'C';
            descriptor[16] = (byte)field.Length;
            stream.Write(descriptor);
        }

        stream.WriteByte(0x0D);
        foreach (var row in rows)
        {
            stream.WriteByte((byte)' ');
            for (var i = 0; i < fields.Length; i++)
            {
                var value = Encoding.ASCII.GetBytes((i < row.Length ? row[i] : string.Empty).PadRight(fields[i].Length)[..fields[i].Length]);
                stream.Write(value);
            }
        }

        stream.WriteByte(0x1A);
    }
}
