using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialPro.Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                FullName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                DocumentNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                Phone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                Email = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Sku = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                Name = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false),
                Unit = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                SalePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CostPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                StockQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                MinimumStock = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Suppliers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                Cnpj = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                Phone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                Email = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Suppliers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                FullName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                Username = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                Email = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false),
                PasswordHash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                PasswordSalt = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                Role = table.Column<int>(type: "int", nullable: false),
                LastLoginAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                MustChangePassword = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Sales",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CustomerId = table.Column<Guid>(type: "char(36)", nullable: false),
                TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                ChangeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PaymentMethod = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                ReceiptNumber = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                SoldAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sales", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "StockMovements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                Reason = table.Column<string>(type: "longtext", nullable: false),
                Reference = table.Column<string>(type: "longtext", nullable: false),
                MovementAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockMovements", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SaleItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                SaleId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SaleItems", x => x.Id);
            });

        migrationBuilder.CreateIndex(name: "IX_Customers_DocumentNumber", table: "Customers", column: "DocumentNumber");
        migrationBuilder.CreateIndex(name: "IX_Products_Sku", table: "Products", column: "Sku", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Suppliers_Cnpj", table: "Suppliers", column: "Cnpj");
        migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", column: "Email", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Users_Username", table: "Users", column: "Username", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SaleItems");
        migrationBuilder.DropTable(name: "Sales");
        migrationBuilder.DropTable(name: "StockMovements");
        migrationBuilder.DropTable(name: "Users");
        migrationBuilder.DropTable(name: "Suppliers");
        migrationBuilder.DropTable(name: "Products");
        migrationBuilder.DropTable(name: "Customers");
    }
}
