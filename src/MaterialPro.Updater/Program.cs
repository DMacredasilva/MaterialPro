using System.Text.Json;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Diagnostics;

namespace MaterialPro.Updater;

internal static class Program
{
    private static int Main(string[] args)
    {
        var root = AppContext.BaseDirectory;
        var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "apply";
        var logPath = Path.Combine(root, "materialpro-update.log");

        try
        {
            WaitForParentExit(logPath);
            var exitCode = command switch
            {
                "check" => Check(root, logPath),
                "apply" => Apply(root, args.Skip(1).FirstOrDefault() ?? "update-package.zip", logPath),
                _ => WriteUsage()
            };
            RestartApplicationIfRequested(exitCode, logPath);
            if (args.Length == 0)
            {
                ShowMessage(exitCode == 0 ? "Atualizacao concluida. Veja materialpro-update.log para detalhes." : "Atualizacao falhou. Veja materialpro-update.log.");
            }

            return exitCode;
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} ERRO: {ex}{Environment.NewLine}");
            Console.Error.WriteLine($"Updater falhou: {ex.Message}");
            if (args.Length == 0)
            {
                ShowMessage($"Updater falhou: {ex.Message}");
            }

            return 1;
        }
    }

    private static void WaitForParentExit(string logPath)
    {
        var value = Environment.GetEnvironmentVariable("MATERIALPRO_PARENT_PID");
        if (!int.TryParse(value, out var pid) || pid <= 0)
        {
            return;
        }

        try
        {
            var parent = Process.GetProcessById(pid);
            Log(logPath, $"Aguardando fechamento do processo {pid}.");
            parent.WaitForExit(30000);
            Thread.Sleep(1500);
        }
        catch (Exception ex)
        {
            Log(logPath, $"Nao foi necessario aguardar processo pai: {ex.Message}");
        }
    }

    private static void RestartApplicationIfRequested(int exitCode, string logPath)
    {
        if (exitCode != 0)
        {
            return;
        }

        var restartExe = Environment.GetEnvironmentVariable("MATERIALPRO_RESTART_EXE");
        if (string.IsNullOrWhiteSpace(restartExe) || !File.Exists(restartExe))
        {
            return;
        }

        try
        {
            Log(logPath, $"Reabrindo MaterialPro: {restartExe}");
            Process.Start(new ProcessStartInfo { FileName = restartExe, WorkingDirectory = Path.GetDirectoryName(restartExe) ?? AppContext.BaseDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log(logPath, $"Nao foi possivel reabrir o MaterialPro: {ex.Message}");
        }
    }

    private static int Check(string root, string logPath)
    {
        var manifestPath = Path.Combine(root, "update-manifest.json");
        if (!File.Exists(manifestPath))
        {
            var channel = DetectChannel();
            var manifestUrl = RemoteUrl(channel, "update-manifest.json");
            Log(logPath, $"Manifesto local nao encontrado. Consultando {manifestUrl}");
            var manifestText = DownloadString(manifestUrl, logPath);
            File.WriteAllText(manifestPath, manifestText);
        }

        var manifest = ParseManifest(File.ReadAllText(manifestPath));
        Log(logPath, $"Versao disponivel: {manifest.Version}");
        Log(logPath, $"Pacote: {manifest.PackagePath}");
        if (!string.IsNullOrWhiteSpace(manifest.PackageUrl))
        {
            Log(logPath, $"URL: {manifest.PackageUrl}");
        }
        return 0;
    }

    private static int Apply(string root, string? packagePath, string logPath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            Log(logPath, "Informe o caminho do pacote para aplicar.");
            return 1;
        }

        var installRoot = root;
        var backupRoot = Path.Combine(installRoot, "backups", $"update-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(backupRoot);
        Log(logPath, $"Iniciando atualizacao em {installRoot}");

        foreach (var file in Directory.GetFiles(installRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(installRoot, file);
            if (ShouldSkip(relative))
            {
                continue;
            }

            var target = Path.Combine(backupRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }

        Log(logPath, $"Backup pre-atualizacao salvo em {backupRoot}");
        var resolvedPackage = ResolveOrDownloadPackage(root, packagePath, logPath);
        if (!File.Exists(resolvedPackage))
        {
            Log(logPath, $"Pacote nao encontrado: {resolvedPackage}");
            return 1;
        }

        var extractRoot = Path.Combine(Path.GetTempPath(), $"materialpro-update-{Guid.NewGuid():N}");
        ZipFile.ExtractToDirectory(resolvedPackage, extractRoot, true);
        foreach (var file in Directory.GetFiles(extractRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(extractRoot, file);
            if (relative.StartsWith("backups", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = Path.Combine(installRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            try
            {
                File.Copy(file, target, true);
                Log(logPath, $"Atualizado: {relative}");
            }
            catch (IOException ex)
            {
                Log(logPath, $"Nao foi possivel atualizar {relative}: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Log(logPath, $"Sem permissao para atualizar {relative}: {ex.Message}");
            }
        }

        Directory.Delete(extractRoot, true);
        Log(logPath, $"Atualizacao aplicada a partir de {resolvedPackage}");
        return 0;
    }

    private static string ResolveOrDownloadPackage(string root, string packagePath, string logPath)
    {
        var resolvedPackage = Path.GetFullPath(Path.IsPathRooted(packagePath) ? packagePath : Path.Combine(root, packagePath));
        if (File.Exists(resolvedPackage))
        {
            Log(logPath, $"Usando pacote local: {resolvedPackage}");
            return resolvedPackage;
        }

        var channel = DetectChannel();
        var manifestPath = Path.Combine(root, "update-manifest.json");
        UpdateManifest manifest = new();
        if (File.Exists(manifestPath))
        {
            manifest = ParseManifest(File.ReadAllText(manifestPath));
        }
        else
        {
            var manifestUrl = RemoteUrl(channel, "update-manifest.json");
            Log(logPath, $"Baixando manifesto: {manifestUrl}");
            var manifestText = DownloadString(manifestUrl, logPath);
            File.WriteAllText(manifestPath, manifestText);
            manifest = ParseManifest(manifestText);
        }

        var packageUrl = string.IsNullOrWhiteSpace(manifest.PackageUrl)
            ? RemoteUrl(channel, "update-package.zip")
            : manifest.PackageUrl;

        Log(logPath, $"Baixando pacote de atualizacao: {packageUrl}");
        DownloadFile(packageUrl, resolvedPackage, logPath);
        return resolvedPackage;
    }

    private static string DetectChannel()
    {
        var processName = Path.GetFileName(Environment.ProcessPath ?? string.Empty);
        return processName.Contains("server", StringComparison.OrdinalIgnoreCase) ? "server" : "client";
    }

    private static UpdateManifest ParseManifest(string json)
    {
        return JsonSerializer.Deserialize<UpdateManifest>(json.TrimStart('\uFEFF')) ?? new UpdateManifest();
    }

    private static string RemoteUrl(string channel, string fileName)
    {
        var configured = ConfiguredBaseUrl();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return $"{configured.TrimEnd('/')}/{channel}/{fileName}";
        }

        return $"https://raw.githubusercontent.com/DMacredasilva/MaterialPro/main/updates/{channel}/{fileName}";
    }

    private static string ConfiguredBaseUrl()
    {
        var root = AppContext.BaseDirectory;
        var file = Path.Combine(root, "update-source.txt");
        if (File.Exists(file))
        {
            return File.ReadAllText(file).Trim();
        }

        return Environment.GetEnvironmentVariable("MATERIALPRO_UPDATE_URL")?.Trim() ?? string.Empty;
    }

    private static string DownloadString(string url, string logPath)
    {
        using var client = NewHttpClient();
        try
        {
            return client.GetStringAsync(url).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log(logPath, $"Falha ao baixar texto: {ex.Message}");
            Log(logPath, "Se estiver usando GitHub privado, torne os arquivos de update publicos ou informe uma URL publica em update-source.txt.");
            throw;
        }
    }

    private static void DownloadFile(string url, string destination, string logPath)
    {
        using var client = NewHttpClient();
        using var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            Log(logPath, $"Falha ao baixar pacote: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            Log(logPath, "Se estiver usando GitHub privado, torne os arquivos de update publicos ou informe uma URL publica em update-source.txt.");
            response.EnsureSuccessStatusCode();
        }

        var total = response.Content.Headers.ContentLength;
        using var source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var target = File.Create(destination);
        var buffer = new byte[1024 * 128];
        long copied = 0;
        var nextLog = 10L;
        while (true)
        {
            var read = source.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                break;
            }

            target.Write(buffer, 0, read);
            copied += read;
            if (total.HasValue)
            {
                var percent = copied * 100 / Math.Max(1, total.Value);
                if (percent >= nextLog)
                {
                    Log(logPath, $"Download {percent}%");
                    nextLog += 10;
                }
            }
        }

        Log(logPath, $"Pacote baixado: {destination}");
    }

    private static HttpClient NewHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MaterialPro-Updater/1.0");
        return client;
    }

    private static bool ShouldSkip(string relative)
    {
        return relative.StartsWith("backups", StringComparison.OrdinalIgnoreCase)
            || relative.Equals("update-package.zip", StringComparison.OrdinalIgnoreCase)
            || relative.Equals("materialpro-update.log", StringComparison.OrdinalIgnoreCase)
            || relative.EndsWith("Update.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static void Log(string logPath, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
        Console.WriteLine(line);
        File.AppendAllText(logPath, line + Environment.NewLine);
    }

    private static void ShowMessage(string message)
    {
        if (OperatingSystem.IsWindows())
        {
            MessageBox(IntPtr.Zero, message, "MaterialPro Updater", 0);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private static int WriteUsage()
    {
        Console.WriteLine("Uso: MaterialPro.Updater [check|apply <pacote>]");
        return 1;
    }
}

internal sealed class UpdateManifest
{
    public string Version { get; set; } = "0.0.0";
    public string PackagePath { get; set; } = string.Empty;
    public string PackageUrl { get; set; } = string.Empty;
}
