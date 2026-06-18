using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialPro.Infrastructure.Migrations;

public partial class FinancialModules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccountsPayable",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Number = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                SupplierName = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                BalanceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                DueDateUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                PaidAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                PaymentMethod = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_AccountsPayable", x => x.Id); });

        migrationBuilder.CreateTable(
            name: "Duplicates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Number = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                SaleId = table.Column<Guid>(type: "char(36)", nullable: true),
                BudgetId = table.Column<Guid>(type: "char(36)", nullable: true),
                Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                BalanceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                DueDateUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                PaidAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_Duplicates", x => x.Id); });

        migrationBuilder.CreateTable(
            name: "FinancialMovements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Number = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                MovementAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Reference = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_FinancialMovements", x => x.Id); });

        migrationBuilder.CreateTable(
            name: "SaleCancellations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                SaleId = table.Column<Guid>(type: "char(36)", nullable: false),
                Reason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                CancelledBy = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                CancelledAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_SaleCancellations", x => x.Id); });

        migrationBuilder.CreateTable(
            name: "SaleReturns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                SaleId = table.Column<Guid>(type: "char(36)", nullable: false),
                Reason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                TotalReturnedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                ProcessedBy = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_SaleReturns", x => x.Id); });

        migrationBuilder.CreateIndex(name: "IX_AccountsPayable_Number", table: "AccountsPayable", column: "Number", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Duplicates_Number", table: "Duplicates", column: "Number", unique: true);
        migrationBuilder.CreateIndex(name: "IX_FinancialMovements_Number", table: "FinancialMovements", column: "Number");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SaleReturns");
        migrationBuilder.DropTable(name: "SaleCancellations");
        migrationBuilder.DropTable(name: "FinancialMovements");
        migrationBuilder.DropTable(name: "Duplicates");
        migrationBuilder.DropTable(name: "AccountsPayable");
    }
}
