using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialPro.Infrastructure.Migrations;

public partial class CustomerModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Code", table: "Customers", type: "varchar(40)", maxLength: 40, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "StateRegistration", table: "Customers", type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "WhatsApp", table: "Customers", type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ZipCode", table: "Customers", type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "Address", table: "Customers", type: "varchar(220)", maxLength: 220, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "AddressNumber", table: "Customers", type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "Complement", table: "Customers", type: "varchar(120)", maxLength: 120, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "District", table: "Customers", type: "varchar(120)", maxLength: 120, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "City", table: "Customers", type: "varchar(120)", maxLength: 120, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "State", table: "Customers", type: "varchar(2)", maxLength: 2, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<decimal>(name: "CreditLimit", table: "Customers", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>(name: "Notes", table: "Customers", type: "varchar(500)", maxLength: 500, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<bool>(name: "IsBlocked", table: "Customers", type: "tinyint(1)", nullable: false, defaultValue: false);

        migrationBuilder.CreateIndex(name: "IX_Customers_Code", table: "Customers", column: "Code");
        migrationBuilder.CreateIndex(name: "IX_Customers_FullName", table: "Customers", column: "FullName");
        migrationBuilder.CreateIndex(name: "IX_Customers_Phone", table: "Customers", column: "Phone");
        migrationBuilder.CreateIndex(name: "IX_Customers_WhatsApp", table: "Customers", column: "WhatsApp");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Customers_Code", table: "Customers");
        migrationBuilder.DropIndex(name: "IX_Customers_FullName", table: "Customers");
        migrationBuilder.DropIndex(name: "IX_Customers_Phone", table: "Customers");
        migrationBuilder.DropIndex(name: "IX_Customers_WhatsApp", table: "Customers");
        migrationBuilder.DropColumn(name: "Code", table: "Customers");
        migrationBuilder.DropColumn(name: "StateRegistration", table: "Customers");
        migrationBuilder.DropColumn(name: "WhatsApp", table: "Customers");
        migrationBuilder.DropColumn(name: "ZipCode", table: "Customers");
        migrationBuilder.DropColumn(name: "Address", table: "Customers");
        migrationBuilder.DropColumn(name: "AddressNumber", table: "Customers");
        migrationBuilder.DropColumn(name: "Complement", table: "Customers");
        migrationBuilder.DropColumn(name: "District", table: "Customers");
        migrationBuilder.DropColumn(name: "City", table: "Customers");
        migrationBuilder.DropColumn(name: "State", table: "Customers");
        migrationBuilder.DropColumn(name: "CreditLimit", table: "Customers");
        migrationBuilder.DropColumn(name: "Notes", table: "Customers");
        migrationBuilder.DropColumn(name: "IsBlocked", table: "Customers");
    }
}
