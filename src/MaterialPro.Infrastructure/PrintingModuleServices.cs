using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using MaterialPro.Application;
using MaterialPro.Domain;
using Microsoft.EntityFrameworkCore;

namespace MaterialPro.Infrastructure;

public sealed class PrinterDiscoveryService : IPrinterDiscoveryService
{
    public IReadOnlyList<PrinterDiscoveryItem> Detect()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [new PrinterDiscoveryItem("Arquivo de impressao", "Fallback multiplataforma", Path.GetTempPath(), PrinterKind.A4, PrinterDeviceStatus.Offline, true)];
        }

        return DetectWindowsPrinters();
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<PrinterDiscoveryItem> DetectWindowsPrinters()
    {
        var printers = new List<PrinterDiscoveryItem>();
        var defaultPrinter = SafeDefaultPrinter();
        foreach (string name in PrinterSettings.InstalledPrinters)
        {
            printers.Add(new PrinterDiscoveryItem(
                name,
                GuessDriver(name),
                GuessPort(name),
                GuessKind(name),
                PrinterDeviceStatus.Online,
                string.Equals(name, defaultPrinter, StringComparison.OrdinalIgnoreCase)));
        }

        if (printers.Count == 0)
        {
            printers.Add(new PrinterDiscoveryItem("Impressora virtual PDF", "Microsoft Print to PDF", "PORTPROMPT:", PrinterKind.A4, PrinterDeviceStatus.Offline, true));
        }
        return printers;
    }

    [SupportedOSPlatform("windows")]
    private static string SafeDefaultPrinter()
    {
        try
        {
            return new PrinterSettings().PrinterName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GuessDriver(string name)
    {
        var lower = name.ToLowerInvariant();
        foreach (var brand in new[] { "Elgin", "Epson", "Bematech", "Daruma", "Tanca", "Control ID", "HP", "Canon", "Brother", "Xerox" })
        {
            if (lower.Contains(brand.ToLowerInvariant())) return brand;
        }
        return "Generica Windows";
    }

    private static string GuessPort(string name) => name.Contains("\\\\", StringComparison.Ordinal) ? "Rede/Compartilhada" : "USB/Windows";
    private static PrinterKind GuessKind(string name)
    {
        var value = name.ToLowerInvariant();
        if (value.Contains("58")) return PrinterKind.Thermal58;
        if (value.Contains("80") || value.Contains("elgin") || value.Contains("epson") || value.Contains("bematech") || value.Contains("daruma") || value.Contains("tanca")) return PrinterKind.Thermal80;
        if (value.Contains("label") || value.Contains("etiqueta") || value.Contains("zebra")) return PrinterKind.Label;
        return PrinterKind.A4;
    }
}

public sealed class ReceiptTemplateService : IReceiptTemplateService
{
    public string BuildReceipt(PrintDocumentRequest request, InternalPaperFormat format = InternalPaperFormat.Thermal80)
    {
        var width = format == InternalPaperFormat.Thermal58 ? 32 : format == InternalPaperFormat.Thermal80 || format == InternalPaperFormat.Label ? 42 : 80;
        var line = new string('-', width);
        var builder = new StringBuilder();
        builder.AppendLine(Center("MATERIALPRO", width));
        builder.AppendLine(Center(DocumentTitle(request.DocumentType, request.Title), width));
        builder.AppendLine(line);
        builder.AppendLine($"Data: {DateTime.Now:dd/MM/yyyy HH:mm}");
        if (request.ReferenceId.HasValue)
        {
            builder.AppendLine($"Referencia: {request.ReferenceId.Value:N}"[..Math.Min(width, 43)]);
        }
        builder.AppendLine(line);
        foreach (var item in request.Lines)
        {
            foreach (var wrapped in Wrap(item, width))
            {
                builder.AppendLine(wrapped);
            }
        }
        builder.AppendLine(line);
        builder.AppendLine(Center(Footer(request.DocumentType), width));
        return builder.ToString();
    }

    public string BuildProductLabel(ProductLabelRequest request)
    {
        var barcode = new BarcodeService().Code128(request.Barcode);
        var builder = new StringBuilder();
        builder.AppendLine(Center(request.Name, 42));
        builder.AppendLine($"Cod: {request.Code}  Und: {request.Unit}");
        builder.AppendLine($"Preco: {request.Price:C}");
        builder.AppendLine(barcode);
        return builder.ToString();
    }

    public static string BuildInternalDocument(InternalDocumentRequest request)
    {
        var type = request.Kind switch
        {
            InternalDocumentKind.SaleCoupon => PrintDocumentType.SaleReceipt,
            InternalDocumentKind.SaleReceipt => PrintDocumentType.PaymentReceipt,
            InternalDocumentKind.Budget => PrintDocumentType.Budget,
            InternalDocumentKind.PaymentProof => PrintDocumentType.PaymentReceipt,
            InternalDocumentKind.SaleSecondCopy => PrintDocumentType.SecondCopy,
            InternalDocumentKind.ReturnProof => PrintDocumentType.ReturnProof,
            InternalDocumentKind.CancellationProof => PrintDocumentType.CancellationProof,
            InternalDocumentKind.CashOpening => PrintDocumentType.CashOpening,
            InternalDocumentKind.CashWithdrawal => PrintDocumentType.CashWithdrawal,
            InternalDocumentKind.CashSupply => PrintDocumentType.CashSupply,
            InternalDocumentKind.CashClosing => PrintDocumentType.CashClosing,
            InternalDocumentKind.A4Report => PrintDocumentType.A4Report,
            InternalDocumentKind.ProductLabel => PrintDocumentType.ProductLabel,
            _ => PrintDocumentType.TestPage
        };

        var lines = new List<string>
        {
            $"Numero: {request.Number}",
            $"Cliente: {request.CustomerName}",
            $"Referencia: {request.Reference}"
        };
        lines.AddRange(request.Lines.Where(x => !string.IsNullOrWhiteSpace(x)));
        lines.Add($"Total: {request.TotalAmount:C}");
        lines.Add($"Pagamento: {request.PaymentMethod}");
        if (!string.IsNullOrWhiteSpace(request.Notes)) lines.Add($"Obs: {request.Notes}");

        return new ReceiptTemplateService().BuildReceipt(new PrintDocumentRequest(type, null, request.Kind.ToString(), lines), request.PaperFormat);
    }

    private static string Center(string value, int width)
    {
        value = Clip(value, width);
        var pad = Math.Max(0, (width - value.Length) / 2);
        return new string(' ', pad) + value;
    }

    private static string Clip(string value, int width) => value.Length <= width ? value : value[..width];

    private static IEnumerable<string> Wrap(string value, int width)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield return string.Empty;
            yield break;
        }

        var remaining = value.Trim();
        while (remaining.Length > width)
        {
            yield return remaining[..width];
            remaining = remaining[width..].TrimStart();
        }
        yield return remaining;
    }

    private static string DocumentTitle(PrintDocumentType type, string fallback) => type switch
    {
        PrintDocumentType.SaleReceipt => "CUPOM SIMPLES DE VENDA",
        PrintDocumentType.SecondCopy => "SEGUNDA VIA",
        PrintDocumentType.Budget => "ORCAMENTO",
        PrintDocumentType.PaymentReceipt => "RECIBO DE PAGAMENTO",
        PrintDocumentType.ReturnProof => "COMPROVANTE DE DEVOLUCAO",
        PrintDocumentType.CancellationProof => "COMPROVANTE DE CANCELAMENTO",
        PrintDocumentType.CashOpening => "ABERTURA DE CAIXA",
        PrintDocumentType.CashWithdrawal => "SANGRIA",
        PrintDocumentType.CashSupply => "SUPRIMENTO",
        PrintDocumentType.CashClosing => "FECHAMENTO DE CAIXA",
        PrintDocumentType.A4Report => "RELATORIO A4",
        PrintDocumentType.ProductLabel => "ETIQUETA DE PRODUTO",
        _ => string.IsNullOrWhiteSpace(fallback) ? "DOCUMENTO INTERNO" : fallback
    };

    private static string Footer(PrintDocumentType type) => type == PrintDocumentType.ProductLabel
        ? "CODE128 / EAN13"
        : "Documento interno sem valor fiscal";
}

public sealed class BarcodeService : IBarcodeService
{
    public string Code128(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "000000" : value.Trim();
        return $"|{string.Join("|", text.Select(ch => ((int)ch).ToString("X2")))}|";
    }

    public bool IsValidEan13(string value)
    {
        if (value.Length != 13 || !value.All(char.IsDigit)) return false;
        var sum = value.Take(12).Select((ch, i) => (ch - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        var check = (10 - (sum % 10)) % 10;
        return check == value[12] - '0';
    }
}

public sealed class PrinterStatusService : IPrinterStatusService
{
    public PrinterDeviceStatus GetStatus(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return PrinterDeviceStatus.Offline;
        }

        if (!OperatingSystem.IsWindows())
        {
            return PrinterDeviceStatus.Offline;
        }

        return PrinterDeviceStatus.Online;
    }
}

public sealed class PrintQueueService : IPrintQueueService
{
    private readonly IPrinterManagementService _printers;

    public PrintQueueService(IPrinterManagementService printers) => _printers = printers;

    public IReadOnlyList<PrintQueueItem> Queue(PrintQueueStatus? status = null) => _printers.Queue(status);
    public PrintQueueItem Reprint(Guid queueItemId, Guid? userId = null) => _printers.Reprint(queueItemId, userId);
    public PrintQueueItem Cancel(Guid queueItemId, Guid? userId = null) => _printers.CancelQueueItem(queueItemId, userId);
}

public sealed class PrinterManagementService : IPrinterManagementService
{
    private readonly MaterialProDbContext _db;
    private readonly IPrinterDiscoveryService _discovery;
    private readonly IReceiptTemplateService _templates;

    public PrinterManagementService(MaterialProDbContext db) : this(db, new PrinterDiscoveryService(), new ReceiptTemplateService())
    {
    }

    public PrinterManagementService(MaterialProDbContext db, IPrinterDiscoveryService discovery, IReceiptTemplateService templates)
    {
        _db = db;
        _discovery = discovery;
        _templates = templates;
    }

    public IReadOnlyList<PrinterDevice> SyncInstalledPrinters()
    {
        foreach (var detected in _discovery.Detect())
        {
            var printer = _db.PrinterDevices.FirstOrDefault(x => x.Name == detected.Name);
            if (printer is null)
            {
                printer = new PrinterDevice { Name = detected.Name };
                _db.PrinterDevices.Add(printer);
            }
            printer.Driver = detected.Driver;
            printer.Port = detected.Port;
            printer.Kind = detected.Kind;
            printer.Status = detected.Status;
            printer.IsWindowsDefault = detected.IsWindowsDefault;
            printer.IsActive = true;
            printer.UpdatedAtUtc = DateTime.UtcNow;
        }
        _db.SaveChanges();
        return Printers();
    }

    public IReadOnlyList<PrinterDevice> Printers() => _db.PrinterDevices.AsNoTracking().OrderByDescending(x => x.IsWindowsDefault).ThenBy(x => x.Name).ToList();

    public PrinterDevice SetDefault(Guid printerId)
    {
        foreach (var printer in _db.PrinterDevices)
        {
            printer.IsWindowsDefault = printer.Id == printerId;
            printer.UpdatedAtUtc = DateTime.UtcNow;
        }
        _db.SaveChanges();
        var selected = _db.PrinterDevices.First(x => x.Id == printerId);
        Log(null, PrintDocumentType.TestPage, null, selected.Name, PrintQueueStatus.Printed, "Impressora padrao configurada");
        _db.SaveChanges();
        return selected;
    }

    public PrintConfiguration SaveConfiguration(PrintConfigurationRequest request)
    {
        var config = _db.PrintConfigurations.FirstOrDefault(x => x.ComputerName == request.ComputerName && x.DocumentType == request.DocumentType);
        if (config is null)
        {
            config = new PrintConfiguration { ComputerName = request.ComputerName, DocumentType = request.DocumentType };
            _db.PrintConfigurations.Add(config);
        }
        config.PrinterId = request.PrinterId;
        config.PaperKind = request.PaperKind;
        config.LeftMargin = request.LeftMargin;
        config.RightMargin = request.RightMargin;
        config.TopMargin = request.TopMargin;
        config.BottomMargin = request.BottomMargin;
        config.CutPaper = request.CutPaper;
        config.OpenDrawer = request.OpenDrawer;
        config.PrintLogo = request.PrintLogo;
        config.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        return config;
    }

    public IReadOnlyList<PrintConfiguration> Configurations() => _db.PrintConfigurations.AsNoTracking().OrderBy(x => x.DocumentType).ToList();

    public PrintQueueItem Print(PrintDocumentRequest request)
    {
        var printer = ResolvePrinter(request);
        var content = _templates.BuildReceipt(request, ToFormat(printer?.Kind ?? PrinterKind.Thermal80));
        var item = new PrintQueueItem { DocumentType = request.DocumentType, ReferenceId = request.ReferenceId, PrinterId = printer?.Id, Content = content };
        _db.PrintQueueItems.Add(item);
        _db.SaveChanges();
        TryPrint(item, request.UserId, request.ForceFail);
        return item;
    }

    public PrintQueueItem Reprint(Guid queueItemId, Guid? userId = null)
    {
        var item = _db.PrintQueueItems.First(x => x.Id == queueItemId);
        item.Status = PrintQueueStatus.Pending;
        item.Error = string.Empty;
        TryPrint(item, userId, false);
        return item;
    }

    public PrintQueueItem CancelQueueItem(Guid queueItemId, Guid? userId = null)
    {
        var item = _db.PrintQueueItems.First(x => x.Id == queueItemId);
        item.Status = PrintQueueStatus.Cancelled;
        item.UpdatedAtUtc = DateTime.UtcNow;
        Log(userId, item.DocumentType, item.ReferenceId, PrinterName(item.PrinterId), PrintQueueStatus.Cancelled, "Item cancelado");
        _db.SaveChanges();
        return item;
    }

    public IReadOnlyList<PrintQueueItem> Queue(PrintQueueStatus? status = null)
    {
        var query = _db.PrintQueueItems.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return query.OrderByDescending(x => x.CreatedAtUtc).ToList();
    }

    public IReadOnlyList<PrintLog> Logs() => _db.PrintLogs.AsNoTracking().OrderByDescending(x => x.LoggedAtUtc).ToList();

    private PrinterDevice? ResolvePrinter(PrintDocumentRequest request)
    {
        if (request.PrinterId.HasValue) return _db.PrinterDevices.FirstOrDefault(x => x.Id == request.PrinterId.Value);
        return _db.PrinterDevices.FirstOrDefault(x => x.IsWindowsDefault) ?? _db.PrinterDevices.FirstOrDefault();
    }

    private void TryPrint(PrintQueueItem item, Guid? userId, bool forceFail)
    {
        item.Attempts++;
        try
        {
            if (forceFail) throw new InvalidOperationException("Falha simulada de impressao.");
            var printerName = PrinterName(item.PrinterId);
            if (string.IsNullOrWhiteSpace(printerName)) throw new InvalidOperationException("Nenhuma impressora configurada.");
            PrintContent(printerName, item.Content);
            item.Status = PrintQueueStatus.Printed;
            item.PrintedAtUtc = DateTime.UtcNow;
            Log(userId, item.DocumentType, item.ReferenceId, printerName, PrintQueueStatus.Printed, "Documento impresso");
        }
        catch (Exception ex)
        {
            item.Status = PrintQueueStatus.Error;
            item.Error = ex.Message;
            Log(userId, item.DocumentType, item.ReferenceId, PrinterName(item.PrinterId), PrintQueueStatus.Error, ex.Message);
        }
        item.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
    }

    private static void PrintContent(string printerName, string content)
    {
        if (OperatingSystem.IsWindows())
        {
            PrintTextToWindows(printerName, content);
            return;
        }

        var file = Path.Combine(Path.GetTempPath(), $"materialpro-print-{Guid.NewGuid():N}.txt");
        File.WriteAllText(file, content, Encoding.UTF8);
    }

    [SupportedOSPlatform("windows")]
    private static void PrintTextToWindows(string printerName, string content)
    {
        var document = new PrintDocument { PrinterSettings = { PrinterName = printerName } };
        document.PrintPage += (_, e) =>
        {
            using var font = new Font("Consolas", 9);
            e.Graphics!.DrawString(content, font, Brushes.Black, e.MarginBounds.Left, e.MarginBounds.Top);
        };
        document.Print();
    }

    private string PrinterName(Guid? printerId) => printerId.HasValue ? _db.PrinterDevices.Where(x => x.Id == printerId.Value).Select(x => x.Name).FirstOrDefault() ?? string.Empty : string.Empty;
    private static InternalPaperFormat ToFormat(PrinterKind kind) => kind switch { PrinterKind.Thermal58 => InternalPaperFormat.Thermal58, PrinterKind.A4 => InternalPaperFormat.A4, _ => InternalPaperFormat.Thermal80 };

    private void Log(Guid? userId, PrintDocumentType type, Guid? referenceId, string printer, PrintQueueStatus status, string message)
    {
        _db.PrintLogs.Add(new PrintLog { UserId = userId, DocumentType = type, ReferenceId = referenceId, PrinterName = printer, Status = status, Message = message, LoggedAtUtc = DateTime.UtcNow });
    }
}
