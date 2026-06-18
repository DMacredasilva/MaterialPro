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
        var updaterRoot = AppContext.BaseDirectory;
        var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "apply";
        var channel = DetectChannel();
        var installRoot = ResolveInstallRoot(channel, args);
        var logPath = Path.Combine(updaterRoot, "materialpro-update.log");

        try
        {
            WaitForParentExit(logPath);
            if (command == "apply" && NeedsElevation(installRoot) && TryRelaunchElevated(args, logPath))
            {
                return 0;
            }

            var exitCode = command switch
            {
                "check" => Check(updaterRoot, installRoot, channel, logPath),
                "apply" => Apply(updaterRoot, installRoot, channel, PackageArg(args) ?? "update-package.zip", logPath),
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

    private static int Check(string updaterRoot, string installRoot, string channel, string logPath)
    {
        var manifestPath = FirstExistingPath(
            Path.Combine(updaterRoot, "update-manifest.json"),
            Path.Combine(installRoot, "update-manifest.json"));

        if (!File.Exists(manifestPath))
        {
            var manifestUrl = RemoteUrl(channel, "update-manifest.json");
            Log(logPath, $"Manifesto local nao encontrado. Consultando {manifestUrl}");
            var manifestText = DownloadString(manifestUrl, logPath);
            manifestPath = Path.Combine(updaterRoot, "update-manifest.json");
            File.WriteAllText(manifestPath, manifestText);
        }

        var manifest = ParseManifest(File.ReadAllText(manifestPath));
        Log(logPath, $"Canal: {channel}");
        Log(logPath, $"Pasta do updater: {updaterRoot}");
        Log(logPath, $"Pasta alvo da instalacao: {installRoot}");
        Log(logPath, $"Versao disponivel: {manifest.Version}");
        Log(logPath, $"Pacote: {manifest.PackagePath}");
        if (!string.IsNullOrWhiteSpace(manifest.PackageUrl))
        {
            Log(logPath, $"URL: {manifest.PackageUrl}");
        }
        return 0;
    }

    private static int Apply(string updaterRoot, string installRoot, string channel, string? packagePath, string logPath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            Log(logPath, "Informe o caminho do pacote para aplicar.");
            return 1;
        }

        if (!Directory.Exists(installRoot))
        {
            Log(logPath, $"Pasta de instalacao nao encontrada: {installRoot}");
            Log(logPath, "Instale primeiro o MaterialPro ou informe o destino com: apply update-package.zip --target \"C:\\Program Files\\MaterialPro\\Client\"");
            return 1;
        }

        StopMaterialProProcesses(channel, logPath);
        var backupRoot = Path.Combine(installRoot, "backups", $"update-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(backupRoot);
        Log(logPath, $"Canal: {channel}");
        Log(logPath, $"Updater executado de: {updaterRoot}");
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
        var resolvedPackage = ResolveOrDownloadPackage(updaterRoot, installRoot, channel, packagePath, logPath);
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

    private static string ResolveOrDownloadPackage(string updaterRoot, string installRoot, string channel, string packagePath, string logPath)
    {
        var resolvedPackage = Path.GetFullPath(Path.IsPathRooted(packagePath) ? packagePath : Path.Combine(updaterRoot, packagePath));
        if (IsDefaultPackage(packagePath) && TryDownloadLatestPackage(updaterRoot, channel, resolvedPackage, logPath))
        {
            return resolvedPackage;
        }

        if (File.Exists(resolvedPackage))
        {
            Log(logPath, $"Usando pacote local: {resolvedPackage}");
            return resolvedPackage;
        }

        var installPackage = Path.Combine(installRoot, packagePath);
        if (!Path.IsPathRooted(packagePath) && File.Exists(installPackage))
        {
            Log(logPath, $"Usando pacote local da instalacao: {installPackage}");
            return installPackage;
        }

        var manifestPath = FirstExistingPath(
            Path.Combine(updaterRoot, "update-manifest.json"),
            Path.Combine(installRoot, "update-manifest.json"));
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
            manifestPath = Path.Combine(updaterRoot, "update-manifest.json");
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

    private static bool TryDownloadLatestPackage(string updaterRoot, string channel, string destination, string logPath)
    {
        try
        {
            var manifestUrl = RemoteUrl(channel, "update-manifest.json");
            Log(logPath, $"Consultando atualizacao no GitHub: {manifestUrl}");
            var manifestText = DownloadString(manifestUrl, logPath);
            var manifestPath = Path.Combine(updaterRoot, "update-manifest.json");
            File.WriteAllText(manifestPath, manifestText);

            var manifest = ParseManifest(manifestText);
            var packageUrl = string.IsNullOrWhiteSpace(manifest.PackageUrl)
                ? RemoteUrl(channel, "update-package.zip")
                : manifest.PackageUrl;

            Log(logPath, $"Baixando pacote mais recente ({manifest.Version}): {packageUrl}");
            DownloadFile(packageUrl, destination, logPath);
            return File.Exists(destination);
        }
        catch (Exception ex)
        {
            Log(logPath, $"Nao foi possivel baixar pacote do GitHub, tentando pacote local: {ex.Message}");
            return false;
        }
    }

    private static bool IsDefaultPackage(string packagePath)
        => !Path.IsPathRooted(packagePath)
            && packagePath.Equals("update-package.zip", StringComparison.OrdinalIgnoreCase);

    private static string DetectChannel()
    {
        var processName = Path.GetFileName(Environment.ProcessPath ?? string.Empty);
        return processName.Contains("server", StringComparison.OrdinalIgnoreCase) ? "server" : "client";
    }

    private static string ResolveInstallRoot(string channel, string[] args)
    {
        var explicitTarget = TargetArg(args);
        if (!string.IsNullOrWhiteSpace(explicitTarget))
        {
            return Path.GetFullPath(explicitTarget);
        }

        var envTarget = channel.Equals("server", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetEnvironmentVariable("MATERIALPRO_SERVER_ROOT")
            : Environment.GetEnvironmentVariable("MATERIALPRO_CLIENT_ROOT");
        if (!string.IsNullOrWhiteSpace(envTarget))
        {
            return Path.GetFullPath(envTarget);
        }

        var updaterRoot = Path.GetFullPath(AppContext.BaseDirectory);
        if (LooksLikeInstallRoot(updaterRoot, channel))
        {
            return updaterRoot;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var defaultRoot = Path.Combine(programFiles, "MaterialPro", channel.Equals("server", StringComparison.OrdinalIgnoreCase) ? "Server" : "Client");
        return Path.GetFullPath(defaultRoot);
    }

    private static bool LooksLikeInstallRoot(string path, string channel)
    {
        var exe = channel.Equals("server", StringComparison.OrdinalIgnoreCase)
            ? "MaterialPro.ServerHost.exe"
            : "MaterialPro.UI.exe";
        return File.Exists(Path.Combine(path, exe));
    }

    private static string? PackageArg(string[] args)
    {
        if (args.Length < 2)
        {
            return null;
        }

        var candidate = args[1];
        return candidate.StartsWith("--", StringComparison.OrdinalIgnoreCase) ? null : candidate;
    }

    private static string? TargetArg(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--target", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            if (arg.StartsWith("--target=", StringComparison.OrdinalIgnoreCase))
            {
                return arg["--target=".Length..].Trim('"');
            }
        }

        return null;
    }

    private static bool NeedsElevation(string installRoot)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return installRoot.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase)
            && !CanWriteToDirectory(installRoot);
    }

    private static bool TryRelaunchElevated(string[] args, string logPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            return false;
        }

        try
        {
            Log(logPath, "Permissao administrativa necessaria. Solicitando elevacao do Windows.");
            var arguments = args.Length == 0 ? string.Empty : string.Join(" ", args.Select(QuoteArgument));
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            });
            return true;
        }
        catch (Exception ex)
        {
            Log(logPath, $"Nao foi possivel solicitar permissao administrativa: {ex.Message}");
            return false;
        }
    }

    private static string QuoteArgument(string value)
    {
        return value.Contains(' ') ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
    }

    private static bool CanWriteToDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var testFile = Path.Combine(directory, $".materialpro-write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void StopMaterialProProcesses(string channel, string logPath)
    {
        if (channel.Equals("server", StringComparison.OrdinalIgnoreCase))
        {
            TryRun("sc.exe", "stop MaterialProServer", logPath);
            StopProcess("MaterialPro.ServerHost", logPath);
            StopProcess("MaterialPro.UI", logPath);
            return;
        }

        StopProcess("MaterialPro.UI", logPath);
    }

    private static void StopProcess(string processName, string logPath)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                Log(logPath, $"Fechando processo {processName} ({process.Id}).");
                process.Kill(true);
                process.WaitForExit(10000);
            }
            catch (Exception ex)
            {
                Log(logPath, $"Nao foi possivel fechar {processName}: {ex.Message}");
            }
        }
    }

    private static void TryRun(string fileName, string arguments, string logPath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(10000);
        }
        catch (Exception ex)
        {
            Log(logPath, $"Comando ignorado ({fileName} {arguments}): {ex.Message}");
        }
    }

    private static string FirstExistingPath(params string[] paths)
    {
        return paths.FirstOrDefault(File.Exists) ?? paths.First();
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
