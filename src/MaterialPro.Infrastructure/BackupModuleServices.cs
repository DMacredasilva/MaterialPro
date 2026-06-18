using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using MySqlConnector;

namespace MaterialPro.Infrastructure;

public sealed record BackupRequest(string InstallPath, string DestinationFolder, string ConnectionString, bool IncludeFiles, bool IncludeDatabase);

public sealed record BackupResult(bool Success, bool DatabaseIncluded, bool FilesIncluded, string BackupPath, string LogPath, string Message);

public sealed class BackupModuleService
{
    public BackupResult CreateBackup(BackupRequest request)
    {
        Directory.CreateDirectory(request.DestinationFolder);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var workRoot = Path.Combine(request.DestinationFolder, $"MaterialPro-backup-{stamp}");
        var filesRoot = Path.Combine(workRoot, "arquivos");
        var databaseRoot = Path.Combine(workRoot, "banco");
        var log = new StringBuilder();

        Directory.CreateDirectory(workRoot);
        log.AppendLine($"Backup iniciado em {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        log.AppendLine($"Instalacao: {request.InstallPath}");
        log.AppendLine($"Destino: {request.DestinationFolder}");

        var filesOk = false;
        var dbOk = false;

        try
        {
            if (request.IncludeFiles)
            {
                Directory.CreateDirectory(filesRoot);
                CopyDirectory(request.InstallPath, filesRoot, workRoot, log);
                CopyClientSettings(filesRoot, log);
                filesOk = true;
                log.AppendLine("Arquivos do sistema copiados.");
            }

            if (request.IncludeDatabase)
            {
                Directory.CreateDirectory(databaseRoot);
                var dumpPath = Path.Combine(databaseRoot, "materialpro.sql");
                dbOk = DumpDatabase(request.ConnectionString, dumpPath, log);
            }
        }
        catch (Exception ex)
        {
            log.AppendLine($"Falha durante backup: {ex.Message}");
        }

        var logPath = Path.Combine(workRoot, "relatorio-backup.txt");
        File.WriteAllText(logPath, log.ToString(), Encoding.UTF8);

        var zipPath = Path.Combine(request.DestinationFolder, $"MaterialPro-backup-{stamp}.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(workRoot, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        Directory.Delete(workRoot, recursive: true);

        var success = (!request.IncludeFiles || filesOk) && (!request.IncludeDatabase || dbOk);
        var message = success
            ? "Backup completo criado com sucesso."
            : "Backup parcial criado. Verifique o relatorio dentro do arquivo ZIP.";

        return new BackupResult(success, dbOk, filesOk, zipPath, "relatorio-backup.txt", message);
    }

    public string DefaultDestinationFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, "MaterialPro", "Backups");
    }

    private static void CopyClientSettings(string filesRoot, StringBuilder log)
    {
        var settingsPath = MaterialProSettingsLoader.WritableClientSettingsPath();
        if (!File.Exists(settingsPath))
        {
            return;
        }

        var target = Path.Combine(filesRoot, "ProgramData", "Client", "appsettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(settingsPath, target, overwrite: true);
        log.AppendLine($"Configuracao do cliente copiada: {settingsPath}");
    }

    private static void CopyDirectory(string source, string destination, string workRoot, StringBuilder log)
    {
        if (!Directory.Exists(source))
        {
            log.AppendLine("Pasta de instalacao nao encontrada.");
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            if (ShouldSkipPath(directory, workRoot))
            {
                continue;
            }

            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (ShouldSkipPath(file, workRoot))
            {
                continue;
            }

            try
            {
                var relative = Path.GetRelativePath(source, file);
                var target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
            catch (Exception ex)
            {
                log.AppendLine($"Arquivo ignorado: {file} | {ex.Message}");
            }
        }
    }

    private static bool ShouldSkipPath(string path, string workRoot)
    {
        return path.StartsWith(workRoot, StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}Backups{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.AltDirectorySeparatorChar}Backups{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DumpDatabase(string connectionString, string dumpPath, StringBuilder log)
    {
        try
        {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            var mysqldump = FindMySqlDump();
            if (string.IsNullOrWhiteSpace(mysqldump))
            {
                log.AppendLine("mysqldump.exe nao encontrado. Instale MySQL Server/Tools ou coloque mysqldump no PATH.");
                return false;
            }

            var arguments = new StringBuilder();
            arguments.Append($"--host={Quote(builder.Server)} ");
            arguments.Append($"--port={builder.Port} ");
            arguments.Append($"--user={Quote(builder.UserID)} ");
            arguments.Append($"--password={Quote(builder.Password)} ");
            arguments.Append("--single-transaction --routines --events --default-character-set=utf8mb4 ");
            arguments.Append($"--result-file={Quote(dumpPath)} ");
            arguments.Append(Quote(builder.Database));

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = mysqldump,
                Arguments = arguments.ToString(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

            if (process is null)
            {
                log.AppendLine("Nao foi possivel iniciar mysqldump.exe.");
                return false;
            }

            process.WaitForExit(120_000);
            var error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(error))
            {
                log.AppendLine(error.Trim());
            }

            if (process.ExitCode != 0 || !File.Exists(dumpPath) || new FileInfo(dumpPath).Length == 0)
            {
                log.AppendLine($"Dump do banco falhou. Codigo: {process.ExitCode}");
                return false;
            }

            log.AppendLine("Banco de dados exportado com sucesso em banco/materialpro.sql.");
            return true;
        }
        catch (Exception ex)
        {
            log.AppendLine($"Falha ao exportar banco: {ex.Message}");
            return false;
        }
    }

    private static string? FindMySqlDump()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), "mysqldump.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var candidates = Directory.Exists(programFiles)
            ? Directory.EnumerateFiles(programFiles, "mysqldump.exe", SearchOption.AllDirectories)
            : [];
        var x86Candidates = Directory.Exists(programFilesX86)
            ? Directory.EnumerateFiles(programFilesX86, "mysqldump.exe", SearchOption.AllDirectories)
            : [];

        return candidates.Concat(x86Candidates).FirstOrDefault();
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
