using System.Text;
using ClosedXML.Excel;
using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaterialPro.Tests;

public sealed class SupplierModuleTests
{
    [Fact]
    public void SupplierService_Creates_And_Searches_Supplier()
    {
        using var db = CreateDbContext();
        var service = new SupplierService(db);

        var supplier = service.Create(Request("Cimento Forte", "11222333000144", "11911112222", city: "Sao Paulo", state: "SP"));
        var found = service.FindByCodeOrCnpj("11222333000144");
        var search = service.Search(new SupplierSearchRequest("cimento"));

        Assert.Equal(supplier.Id, found?.Id);
        Assert.Single(search);
        Assert.Equal("SP", search[0].State);
    }

    [Fact]
    public void SupplierService_Rejects_Duplicate_Cnpj()
    {
        using var db = CreateDbContext();
        var service = new SupplierService(db);
        service.Create(Request("Fornecedor A", "00111222000133"));

        Assert.Throws<InvalidOperationException>(() => service.Create(Request("Fornecedor B", "00111222000133")));
    }

    [Fact]
    public void SupplierService_Updates_And_Inactivates()
    {
        using var db = CreateDbContext();
        var service = new SupplierService(db);
        var supplier = service.Create(Request("Fornecedor Original", "22233344000155"));

        service.Update(supplier.Id, Request("Fornecedor Alterado", "22233344000155", email: "alterado@test.local"));
        service.Inactivate(supplier.Id);

        var updated = service.FindById(supplier.Id);
        Assert.Equal("Fornecedor Alterado", updated?.FantasyName);
        Assert.Equal("alterado@test.local", updated?.Email);
        Assert.False(updated?.IsActive);
    }

    [Fact]
    public void SupplierService_Links_Product_And_Creates_Payable()
    {
        using var db = CreateDbContext();
        var suppliers = new SupplierService(db);
        var products = new ProductService(db);
        var supplier = suppliers.Create(Request("Madeireira Norte", "33344455000166"));
        var product = products.Create(new ProductUpsertRequest("MAD-1", "Tabua Pinus", "UN", 30m, 18m, 2m, "", SupplierId: supplier.Id));
        suppliers.LinkProduct(supplier.Id, product.Id);
        suppliers.CreatePayable(new SupplierPayableRequest(supplier.Id, "CP-1", "Compra madeira", 500m, DateTime.UtcNow.AddDays(5), "Boleto"));

        var linked = suppliers.Products(supplier.Id);
        var financial = suppliers.FinancialSummary(supplier.Id);

        Assert.Single(linked);
        Assert.Equal(500m, financial.OpenAmount);
    }

    [Fact]
    public void SupplierReportService_Exports_Pdf_Excel_And_Ficha()
    {
        using var db = CreateDbContext();
        var suppliers = new SupplierService(db);
        var supplier = suppliers.Create(Request("Fornecedor Relatorio", "44455566000177"));
        var reports = new SupplierReportService(db);

        Assert.True(reports.ExportPdf(new SupplierReportRequest()).Length > 100);
        Assert.True(reports.ExportExcel(new SupplierReportRequest()).Length > 100);
        Assert.True(reports.SupplierFichaPdf(supplier.Id).Length > 100);
    }

    [Fact]
    public void SupplierImportService_Imports_Csv()
    {
        using var db = CreateDbContext();
        var importer = new SupplierImportService(db);
        var file = Path.Combine(Path.GetTempPath(), $"fornecedores-{Guid.NewGuid():N}.csv");
        File.WriteAllText(file, "codigo;nome_fantasia;razao_social;cnpj;telefone;whatsapp;email;cidade;estado\r\nF001;Telhas Sul;Telhas Sul Ltda;55566677000188;1133334444;11999998888;telhas@test.local;Curitiba;PR", Encoding.UTF8);

        var result = importer.ImportCsv(file, new SupplierImportOptions(UpdateExisting: true, IgnoreDuplicates: true));

        Assert.Equal(1, result.ImportedRows);
        Assert.Equal("Telhas Sul", db.Suppliers.Single().FantasyName);
    }

    [Fact]
    public void SupplierImportService_Imports_Excel()
    {
        using var db = CreateDbContext();
        var importer = new SupplierImportService(db);
        var file = Path.Combine(Path.GetTempPath(), $"fornecedores-{Guid.NewGuid():N}.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Fornecedores");
            sheet.Cell(1, 1).Value = "codigo";
            sheet.Cell(1, 2).Value = "nome_fantasia";
            sheet.Cell(1, 3).Value = "cnpj";
            sheet.Cell(2, 1).Value = "F002";
            sheet.Cell(2, 2).Value = "Areia Centro";
            sheet.Cell(2, 3).Value = "66677788000199";
            workbook.SaveAs(file);
        }

        var result = importer.ImportExcel(file, new SupplierImportOptions(UpdateExisting: true, IgnoreDuplicates: true));

        Assert.Equal(1, result.ImportedRows);
        Assert.Equal("Areia Centro", db.Suppliers.Single().FantasyName);
    }

    [Fact]
    public void SupplierImportService_Imports_Dbf()
    {
        using var db = CreateDbContext();
        var importer = new SupplierImportService(db);
        var file = Path.Combine(Path.GetTempPath(), $"fornecedores-{Guid.NewGuid():N}.dbf");
        WriteDbf(file, new[] { ("CODIGO", 10), ("FANTASIA", 40), ("CNPJ", 20) }, new[] { new[] { "F003", "Tijolos Oeste", "77788899000100" } });

        var result = importer.ImportDbf(file, new SupplierImportOptions(UpdateExisting: true, IgnoreDuplicates: true));

        Assert.Equal(1, result.ImportedRows);
        Assert.Equal("Tijolos Oeste", db.Suppliers.Single().FantasyName);
    }

    private static SupplierUpsertRequest Request(string name, string cnpj, string phone = "1133334444", string email = "fornecedor@test.local", string city = "Cidade", string state = "SP")
    {
        return new SupplierUpsertRequest(name, cnpj, phone, email, "Rua A", FantasyName: name, LegalName: $"{name} Ltda", WhatsApp: "11999999999", City: city, State: state);
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
