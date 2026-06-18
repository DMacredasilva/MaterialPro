using MaterialPro.Application;
using MaterialPro.Infrastructure;
using Xunit;

namespace MaterialPro.Tests;

public sealed class InternalDocumentServiceTests
{
    [Fact]
    public void GeneratePdf_Returns_Content_For_Internal_Document()
    {
        var service = new InternalDocumentService();

        var pdf = service.GeneratePdf(new InternalDocumentRequest(
            InternalDocumentKind.SaleCoupon,
            InternalPaperFormat.Thermal58,
            "DOC-1",
            "Cliente Teste",
            "PED-9",
            123.45m,
            "PIX",
            "Observacao",
            new[] { "Item 1", "Item 2" }));

        Assert.NotEmpty(pdf);
    }
}
