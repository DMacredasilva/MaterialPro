using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialPro.Infrastructure.Migrations;

public partial class ProductModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Barcode",
            table: "Products",
            type: "varchar(80)",
            maxLength: 80,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Products",
            type: "varchar(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Category",
            table: "Products",
            type: "varchar(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Brand",
            table: "Products",
            type: "varchar(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Ncm",
            table: "Products",
            type: "varchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Location",
            table: "Products",
            type: "varchar(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_Products_Barcode",
            table: "Products",
            column: "Barcode");

        migrationBuilder.CreateIndex(
            name: "IX_Products_Category",
            table: "Products",
            column: "Category");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Products_Barcode", table: "Products");
        migrationBuilder.DropIndex(name: "IX_Products_Category", table: "Products");
        migrationBuilder.DropColumn(name: "Description", table: "Products");
        migrationBuilder.DropColumn(name: "Category", table: "Products");
        migrationBuilder.DropColumn(name: "Brand", table: "Products");
        migrationBuilder.DropColumn(name: "Barcode", table: "Products");
        migrationBuilder.DropColumn(name: "Ncm", table: "Products");
        migrationBuilder.DropColumn(name: "Location", table: "Products");
    }
}
