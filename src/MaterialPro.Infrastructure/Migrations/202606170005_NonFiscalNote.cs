using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialPro.Infrastructure.Migrations;

public partial class NonFiscalNote : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NonFiscalNotes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Number = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                StoreName = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false),
                StoreDocument = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                CustomerName = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false),
                CustomerDocument = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                CustomerAddress = table.Column<string>(type: "varchar(220)", maxLength: 220, nullable: false),
                TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                IssuedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_NonFiscalNotes", x => x.Id); });

        migrationBuilder.CreateTable(
            name: "NonFiscalNoteItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                NonFiscalNoteId = table.Column<Guid>(type: "char(36)", nullable: false),
                Description = table.Column<string>(type: "varchar(220)", maxLength: 220, nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                TotalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_NonFiscalNoteItems", x => x.Id); });

        migrationBuilder.CreateIndex(
            name: "IX_NonFiscalNotes_Number",
            table: "NonFiscalNotes",
            column: "Number",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "NonFiscalNoteItems");
        migrationBuilder.DropTable(name: "NonFiscalNotes");
    }
}
