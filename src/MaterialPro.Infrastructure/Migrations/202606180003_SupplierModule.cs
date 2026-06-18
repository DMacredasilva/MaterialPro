using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialPro.Infrastructure.Migrations;

public partial class SupplierModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Code", table: "Suppliers", type: "varchar(40)", maxLength: 40, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<int>(name: "PersonType", table: "Suppliers", type: "int", nullable: false, defaultValue: 2);
        migrationBuilder.AddColumn<string>(name: "FantasyName", table: "Suppliers", type: "varchar(150)", maxLength: 150, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "LegalName", table: "Suppliers", type: "varchar(180)", maxLength: 180, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "StateRegistration", table: "Suppliers", type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "MunicipalRegistration", table: "Suppliers", type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "MobilePhone", table: "Suppliers", type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "WhatsApp", table: "Suppliers", type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "Website", table: "Suppliers", type: "varchar(180)", maxLength: 180, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ZipCode", table: "Suppliers", type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "AddressNumber", table: "Suppliers", type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "Complement", table: "Suppliers", type: "varchar(120)", maxLength: 120, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "District", table: "Suppliers", type: "varchar(120)", maxLength: 120, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "City", table: "Suppliers", type: "varchar(120)", maxLength: 120, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "State", table: "Suppliers", type: "varchar(2)", maxLength: 2, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ContactName", table: "Suppliers", type: "varchar(150)", maxLength: 150, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ContactRole", table: "Suppliers", type: "varchar(120)", maxLength: 120, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<int>(name: "DefaultPaymentTermDays", table: "Suppliers", type: "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<decimal>(name: "PurchaseLimit", table: "Suppliers", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>(name: "Notes", table: "Suppliers", type: "varchar(500)", maxLength: 500, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<Guid>(name: "SupplierId", table: "Products", type: "char(36)", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "SupplierId", table: "AccountsPayable", type: "char(36)", nullable: true);

        migrationBuilder.CreateTable(
            name: "Purchases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                SupplierId = table.Column<Guid>(type: "char(36)", nullable: false),
                Number = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                PurchasedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Purchases", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PurchaseItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                PurchaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PurchaseItems", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_Suppliers_Code", table: "Suppliers", column: "Code");
        migrationBuilder.CreateIndex(name: "IX_Suppliers_FantasyName", table: "Suppliers", column: "FantasyName");
        migrationBuilder.CreateIndex(name: "IX_Suppliers_LegalName", table: "Suppliers", column: "LegalName");
        migrationBuilder.CreateIndex(name: "IX_Suppliers_Phone", table: "Suppliers", column: "Phone");
        migrationBuilder.CreateIndex(name: "IX_Suppliers_WhatsApp", table: "Suppliers", column: "WhatsApp");
        migrationBuilder.CreateIndex(name: "IX_Products_SupplierId", table: "Products", column: "SupplierId");
        migrationBuilder.CreateIndex(name: "IX_AccountsPayable_SupplierId", table: "AccountsPayable", column: "SupplierId");
        migrationBuilder.CreateIndex(name: "IX_Purchases_SupplierId", table: "Purchases", column: "SupplierId");
        migrationBuilder.CreateIndex(name: "IX_PurchaseItems_ProductId", table: "PurchaseItems", column: "ProductId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PurchaseItems");
        migrationBuilder.DropTable(name: "Purchases");
        migrationBuilder.DropIndex(name: "IX_Suppliers_Code", table: "Suppliers");
        migrationBuilder.DropIndex(name: "IX_Suppliers_FantasyName", table: "Suppliers");
        migrationBuilder.DropIndex(name: "IX_Suppliers_LegalName", table: "Suppliers");
        migrationBuilder.DropIndex(name: "IX_Suppliers_Phone", table: "Suppliers");
        migrationBuilder.DropIndex(name: "IX_Suppliers_WhatsApp", table: "Suppliers");
        migrationBuilder.DropIndex(name: "IX_Products_SupplierId", table: "Products");
        migrationBuilder.DropIndex(name: "IX_AccountsPayable_SupplierId", table: "AccountsPayable");
        migrationBuilder.DropColumn(name: "SupplierId", table: "Products");
        migrationBuilder.DropColumn(name: "SupplierId", table: "AccountsPayable");
        foreach (var column in new[] { "Code", "PersonType", "FantasyName", "LegalName", "StateRegistration", "MunicipalRegistration", "MobilePhone", "WhatsApp", "Website", "ZipCode", "AddressNumber", "Complement", "District", "City", "State", "ContactName", "ContactRole", "DefaultPaymentTermDays", "PurchaseLimit", "Notes" })
        {
            migrationBuilder.DropColumn(name: column, table: "Suppliers");
        }
    }
}
