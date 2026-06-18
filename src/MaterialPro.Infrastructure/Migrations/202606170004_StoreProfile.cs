using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialPro.Infrastructure.Migrations;

public partial class StoreProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StoreProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                ProgramName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                StoreName = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false),
                Cnpj = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                Address = table.Column<string>(type: "varchar(220)", maxLength: 220, nullable: false),
                Phone = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                LogoPath = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreProfiles", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StoreProfiles");
    }
}
