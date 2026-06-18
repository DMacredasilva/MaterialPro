using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaterialPro.Tests;

public sealed class PrintingModuleTests
{
    [Fact]
    public void Discovery_Returns_Printer_Data()
    {
        var discovery = new FakeDiscovery();

        var printers = discovery.Detect();

        Assert.Single(printers);
        Assert.Equal("Elgin i9 80", printers[0].Name);
        Assert.Equal(PrinterKind.Thermal80, printers[0].Kind);
        Assert.True(printers[0].IsWindowsDefault);
    }

    [Fact]
    public void Sync_And_Set_Default_Printer()
    {
        using var db = CreateDbContext();
        var service = new PrinterManagementService(db, new FakeDiscovery(), new ReceiptTemplateService());

        var printers = service.SyncInstalledPrinters();
        var selected = service.SetDefault(printers.Single().Id);

        Assert.True(selected.IsWindowsDefault);
        Assert.Single(db.PrintLogs);
    }

    [Theory]
    [InlineData(PrintDocumentType.SaleReceipt, "CUPOM SIMPLES DE VENDA")]
    [InlineData(PrintDocumentType.Budget, "ORCAMENTO")]
    [InlineData(PrintDocumentType.PaymentReceipt, "RECIBO DE PAGAMENTO")]
    [InlineData(PrintDocumentType.CashClosing, "FECHAMENTO DE CAIXA")]
    public void Receipt_Template_Generates_Document_Text(PrintDocumentType type, string expectedTitle)
    {
        var template = new ReceiptTemplateService();

        var content = template.BuildReceipt(new PrintDocumentRequest(type, Guid.NewGuid(), "Teste", [
            "Cliente: Cliente Teste",
            "Item: Cimento 2 x 10,00",
            "Total: R$ 20,00"
        ]));

        Assert.Contains(expectedTitle, content);
        Assert.Contains("Documento interno sem valor fiscal", content);
    }

    [Fact]
    public void Product_Label_Generates_Code128_And_Ean13_Validation()
    {
        var barcode = new BarcodeService();
        var label = new ReceiptTemplateService().BuildProductLabel(new ProductLabelRequest("Cimento CP-II", "CIM-01", "7891234567895", 39.90m, "SC"));

        Assert.Contains("Cimento CP-II", label);
        Assert.Contains("|", label);
        Assert.True(barcode.IsValidEan13("7891234567895"));
    }

    [Fact]
    public void Print_Failure_Saves_Queue_And_Log()
    {
        using var db = CreateDbContext();
        var service = SeedService(db);

        var item = service.Print(new PrintDocumentRequest(PrintDocumentType.SaleReceipt, Guid.NewGuid(), "Cupom", ["Venda 1"], ForceFail: true));

        Assert.Equal(PrintQueueStatus.Error, item.Status);
        Assert.Equal(1, item.Attempts);
        Assert.Contains("Falha simulada", item.Error);
        Assert.Single(service.Queue(PrintQueueStatus.Error));
        Assert.Single(service.Logs(), x => x.Status == PrintQueueStatus.Error);
    }

    [Fact]
    public void Reprint_From_Queue_Registers_New_Attempt()
    {
        using var db = CreateDbContext();
        var service = SeedService(db);
        var item = service.Print(new PrintDocumentRequest(PrintDocumentType.Budget, Guid.NewGuid(), "Orcamento", ["Produto A"], ForceFail: true));

        var reprinted = service.Reprint(item.Id);

        Assert.True(reprinted.Attempts >= 2);
        Assert.Contains(reprinted.Status, new[] { PrintQueueStatus.Printed, PrintQueueStatus.Error });
        Assert.True(service.Logs().Count >= 2);
    }

    private static PrinterManagementService SeedService(MaterialProDbContext db)
    {
        var service = new PrinterManagementService(db, new FakeDiscovery(), new ReceiptTemplateService());
        service.SyncInstalledPrinters();
        return service;
    }

    private static MaterialProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MaterialProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MaterialProDbContext(options);
    }

    private sealed class FakeDiscovery : IPrinterDiscoveryService
    {
        public IReadOnlyList<PrinterDiscoveryItem> Detect()
        {
            return [new PrinterDiscoveryItem("Elgin i9 80", "Elgin", "USB001", PrinterKind.Thermal80, PrinterDeviceStatus.Online, true)];
        }
    }
}
