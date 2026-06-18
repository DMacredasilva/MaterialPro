using MaterialPro.Infrastructure;
using MaterialPro.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace MaterialPro.ServerHost;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (!args.Any(x => x.Equals("--prepare-database", StringComparison.OrdinalIgnoreCase))
            && AutoUpdateRunner.StartUpdateAndExitIfAvailable("server"))
        {
            return;
        }

        if (args.Any(x => x.Equals("--prepare-database", StringComparison.OrdinalIgnoreCase)))
        {
            PrepareDatabase();
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddWindowsService();
        builder.Services.AddHostedService<ServerHeartbeatService>();
        builder.Services.AddSingleton(CreateDbContext());

        using var host = builder.Build();
        await host.RunAsync();
    }

    private static MaterialProDbContext CreateDbContext()
    {
        var settings = MaterialProSettingsLoader.Load(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException("ConnectionString do MySQL não configurada.");
        }

        return MaterialProDbContextFactory.CreateMySql(settings.ConnectionString);
    }

    private static void PrepareDatabase()
    {
        using var db = CreateDbContext();
        db.Database.Migrate();

        if (!TableExists(db, "Users"))
        {
            var tables = GetTableNames(db);
            if (tables.Count == 0 || tables.All(x => x.Equals("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase)))
            {
                db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS `__EFMigrationsHistory`;");
                db.GetService<IRelationalDatabaseCreator>().CreateTables();
            }
            else
            {
                throw new InvalidOperationException("Tabela Users ausente, mas o banco possui outras tabelas. Verifique o banco antes de recriar o schema.");
            }
        }

        if (!db.Users.Any())
        {
            var hasher = new Sha256PasswordHasher();
            var salt = hasher.CreateSalt();
            db.Users.Add(new AppUser
            {
                FullName = "Administrador",
                Username = "admin",
                Email = "admin@materialpro.local",
                PasswordSalt = salt,
                PasswordHash = hasher.Hash("Admin@123", salt),
                Role = UserRole.Admin,
                MustChangePassword = true,
                PasswordChangedAtUtc = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }

    private static bool TableExists(MaterialProDbContext db, string tableName)
        => GetTableNames(db).Any(x => x.Equals(tableName, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> GetTableNames(MaterialProDbContext db)
    {
        var names = new List<string>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE();";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
