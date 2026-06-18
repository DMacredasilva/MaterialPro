using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialPro.Infrastructure.Migrations;

public partial class SaleCancellationModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Status",
            table: "Sales",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.CreateTable(
            name: "AccountsReceivable",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Number = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                SaleId = table.Column<Guid>(type: "char(36)", nullable: true),
                CustomerName = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                BalanceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                DueDateUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                PaidAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                PaymentMethod = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_AccountsReceivable", x => x.Id); });

        migrationBuilder.CreateTable(
            name: "vendas_canceladas",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                venda_id = table.Column<Guid>(type: "char(36)", nullable: false),
                motivo = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                usuario_id = table.Column<Guid>(type: "char(36)", nullable: false),
                data_cancelamento = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                valor_total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                observacao = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_vendas_canceladas", x => x.id); });

        migrationBuilder.CreateIndex(
            name: "IX_AccountsReceivable_Number",
            table: "AccountsReceivable",
            column: "Number",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "vendas_canceladas");
        migrationBuilder.DropTable(name: "AccountsReceivable");
        migrationBuilder.DropColumn(name: "Status", table: "Sales");
    }
}
