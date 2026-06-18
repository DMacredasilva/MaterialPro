using MaterialPro.Infrastructure;
using MaterialPro.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text.Json;

namespace MaterialPro.ServerHost;

public sealed class ServerHeartbeatService : BackgroundService
{
    private readonly ILogger<ServerHeartbeatService> _logger;
    private readonly MaterialProDbContext _db;

    public ServerHeartbeatService(ILogger<ServerHeartbeatService> logger, MaterialProDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TryPrepareDatabase();
        _logger.LogInformation("MaterialPro server host iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Heartbeat MaterialPro {Time}", DateTimeOffset.Now);
            WriteDiagnostics();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private void WriteDiagnostics()
    {
        var canConnect = false;
        var error = string.Empty;
        var users = 0;
        var products = 0;
        var sales = 0;

        try
        {
            canConnect = _db.Database.CanConnect();
            if (canConnect)
            {
                if (!TableExists("Users"))
                {
                    TryPrepareDatabase();
                }

                users = _db.Users.Count();
                products = _db.Products.Count();
                sales = _db.Sales.Count();
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _logger.LogWarning("Falha ao consultar diagnostico do banco MaterialPro: {Message}", ex.Message);
        }

        var diagnostics = new
        {
            generatedAt = DateTimeOffset.Now,
            canConnect,
            users,
            products,
            sales,
            error
        };

        var path = Path.Combine(AppContext.BaseDirectory, "diagnostics");
        Directory.CreateDirectory(path);
        File.WriteAllText(
            Path.Combine(path, "server-status.json"),
            JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void TryPrepareDatabase()
    {
        try
        {
            _db.Database.Migrate();
            EnsureCurrentSchemaExists();
            SeedAdminUser();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Banco MaterialPro indisponivel. O servidor continuara aguardando a configuracao do MySQL: {Message}", ex.Message);
            WriteStartupError(ex);
        }
    }

    private void EnsureCurrentSchemaExists()
    {
        if (TableExists("Users"))
        {
            return;
        }

        if (!DatabaseIsEmptyOrOnlyMigrationHistory())
        {
            _logger.LogWarning("Tabela Users nao encontrada, mas o banco possui outras tabelas. Migração automática foi preservada para evitar perda de dados.");
            return;
        }

        _logger.LogWarning("Schema MaterialPro incompleto. Criando tabelas pelo modelo atual.");
        _db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS `__EFMigrationsHistory`;");
        var creator = _db.GetService<IRelationalDatabaseCreator>();
        creator.CreateTables();
    }

    private void SeedAdminUser()
    {
        if (!TableExists("Users") || _db.Users.Any())
        {
            return;
        }

        var hasher = new Sha256PasswordHasher();
        var salt = hasher.CreateSalt();
        _db.Users.Add(new AppUser
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
        _db.SaveChanges();
        _logger.LogInformation("Usuário administrador padrão criado: admin / Admin@123");
    }

    private bool DatabaseIsEmptyOrOnlyMigrationHistory()
    {
        var tableNames = GetTableNames();
        return tableNames.Count == 0
            || tableNames.All(x => x.Equals("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase));
    }

    private bool TableExists(string tableName)
    {
        return GetTableNames().Any(x => x.Equals(tableName, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<string> GetTableNames()
    {
        var names = new List<string>();
        try
        {
            var connection = _db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
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
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Nao foi possivel listar tabelas do banco MaterialPro: {Message}", ex.Message);
        }

        return names;
    }

    private static void WriteStartupError(Exception ex)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "diagnostics");
        Directory.CreateDirectory(path);
        File.WriteAllText(
            Path.Combine(path, "server-startup-error.txt"),
            $"{DateTimeOffset.Now:O}{Environment.NewLine}{ex}");
    }
}
