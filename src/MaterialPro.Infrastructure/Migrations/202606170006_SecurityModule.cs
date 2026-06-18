using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialPro.Infrastructure.Migrations;

public partial class SecurityModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FailedLoginCount",
            table: "Users",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LockedUntilUtc",
            table: "Users",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PasswordChangedAtUtc",
            table: "Users",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "SecurityAudits",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UserId = table.Column<Guid>(type: "char(36)", nullable: true),
                EventType = table.Column<int>(type: "int", nullable: false),
                Area = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                Action = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                EntityName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                EntityId = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                Details = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                MachineName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                IpAddress = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_SecurityAudits", x => x.Id); });

        migrationBuilder.CreateTable(
            name: "SecurityLoginAttempts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Username = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                UserId = table.Column<Guid>(type: "char(36)", nullable: true),
                Success = table.Column<bool>(type: "tinyint(1)", nullable: false),
                FailureReason = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                MachineName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                IpAddress = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                AttemptedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_SecurityLoginAttempts", x => x.Id); });

        migrationBuilder.CreateTable(
            name: "SecuritySessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                SessionKey = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                MachineName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                IpAddress = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                LastSeenAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                EndedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsClosed = table.Column<bool>(type: "tinyint(1)", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_SecuritySessions", x => x.Id); });

        migrationBuilder.CreateIndex(
            name: "IX_SecurityAudits_CreatedAtUtc",
            table: "SecurityAudits",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_SecurityLoginAttempts_AttemptedAtUtc",
            table: "SecurityLoginAttempts",
            column: "AttemptedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_SecuritySessions_SessionKey",
            table: "SecuritySessions",
            column: "SessionKey",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SecuritySessions");
        migrationBuilder.DropTable(name: "SecurityLoginAttempts");
        migrationBuilder.DropTable(name: "SecurityAudits");

        migrationBuilder.DropColumn(name: "FailedLoginCount", table: "Users");
        migrationBuilder.DropColumn(name: "LockedUntilUtc", table: "Users");
        migrationBuilder.DropColumn(name: "PasswordChangedAtUtc", table: "Users");
    }
}
