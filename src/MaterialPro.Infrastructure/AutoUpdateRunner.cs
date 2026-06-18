using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MaterialPro.Infrastructure;

public static class AutoUpdateRunner
{
    public static UpdateStatusSnapshot GetStatus(string channel)
    {
        var root = ResolveInstallRoot(channel);
        var updater = FindUpdater(root, channel) ?? FindUpdater(AppContext.BaseDirectory, channel);
        var local = ReadLocalManifest(root) ?? ReadLocalManifest(AppContext.BaseDirectory);
        UpdateManifest? remote = null;
        string message;

        try
        {
            remote = DownloadManifest(channel);
            message = remote is null
                ? "Nao foi possivel consultar o servidor de atualizacao."
                : string.Equals(local?.Version, remote.Version, StringComparison.OrdinalIgnoreCase)
                    ? "Instalacao correta. Este computador esta atualizado."
                    : "Existe atualizacao disponivel para este computador.";
        }
        catch (Exception ex)
        {
            message = $"Sem conexao com o servidor de atualizacao: {ex.Message}";
        }

        var package = Path.Combine(root, "update-package.zip");
        var hasPackage = File.Exists(package);
        if (updater is null)
        {
            message = "Updater nao encontrado nesta instalacao. Reinstale pelo setup mais recente.";
        }

        return new UpdateStatusSnapshot(
            channel,
            root,
            local?.Version ?? "desconhecida",
            remote?.Version ?? "indisponivel",
            updater ?? string.Empty,
            hasPackage,
            updater is not null,
            message);
    }

    public static bool StartForcedUpdate(string channel, string? packagePath = null)
    {
        try
        {
            var installRoot = ResolveInstallRoot(channel);
            var updater = FindUpdater(installRoot, channel) ?? FindUpdater(AppContext.BaseDirectory, channel);
            if (updater is null)
            {
                return false;
            }

            var root = Path.GetDirectoryName(updater) ?? installRoot;
            var package = string.IsNullOrWhiteSpace(packagePath) ? DownloadLatestPackage(channel) : packagePath;
            var process = Process.GetCurrentProcess();
            var startInfo = new ProcessStartInfo
            {
                FileName = updater,
                Arguments = $"apply \"{package}\" --target \"{installRoot}\"",
                UseShellExecute = false,
                WorkingDirectory = root
            };
            startInfo.Environment["MATERIALPRO_PARENT_PID"] = process.Id.ToString();
            startInfo.Environment["MATERIALPRO_RESTART_EXE"] = Environment.ProcessPath ?? string.Empty;

            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string DownloadLatestPackage(string channel)
    {
        var manifest = DownloadManifest(channel);
        var packageUrl = !string.IsNullOrWhiteSpace(manifest?.PackageUrl)
            ? manifest.PackageUrl
            : $"https://raw.githubusercontent.com/DMacredasilva/MaterialPro/main/updates/{channel}/update-package.zip?ts={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var packagePath = Path.Combine(Path.GetTempPath(), $"materialpro-{channel}-forced-update-{DateTime.Now:yyyyMMddHHmmss}.zip");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MaterialPro-AutoUpdate/1.0");
        using var response = client.GetAsync(packageUrl).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        using var input = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var output = File.Create(packagePath);
        input.CopyTo(output);
        return packagePath;
    }

    public static bool StartUpdateAndExitIfAvailable(string channel)
    {
        try
        {
            var root = AppContext.BaseDirectory;
            var updater = FindUpdater(root, channel);
            if (updater is null)
            {
                return false;
            }

            var remote = DownloadManifest(channel);
            if (remote is null || string.IsNullOrWhiteSpace(remote.Version))
            {
                return false;
            }

            var local = ReadLocalManifest(root);
            if (string.Equals(local?.Version, remote.Version, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var remotePath = Path.Combine(Path.GetTempPath(), $"materialpro-remote-manifest-{channel}.json");
            File.WriteAllText(remotePath, JsonSerializer.Serialize(remote, new JsonSerializerOptions { WriteIndented = true }));
            File.Copy(remotePath, Path.Combine(root, "update-manifest.json"), true);

            var process = Process.GetCurrentProcess();
            var startInfo = new ProcessStartInfo
            {
                FileName = updater,
                Arguments = "apply update-package.zip",
                UseShellExecute = false,
                WorkingDirectory = root
            };
            startInfo.Environment["MATERIALPRO_PARENT_PID"] = process.Id.ToString();
            startInfo.Environment["MATERIALPRO_RESTART_EXE"] = Environment.ProcessPath ?? string.Empty;
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindUpdater(string root, string channel)
    {
        var names = channel.Equals("server", StringComparison.OrdinalIgnoreCase)
            ? new[] { "MaterialProServerUpdate.exe", "MaterialPro.Updater.exe" }
            : new[] { "MaterialProClientUpdate.exe", "MaterialPro.Updater.exe" };

        return names.Select(name => Path.Combine(root, name)).FirstOrDefault(File.Exists);
    }

    private static string ResolveInstallRoot(string channel)
    {
        var current = AppContext.BaseDirectory;
        if (LooksLikeInstallRoot(current, channel))
        {
            return current;
        }

        var env = channel.Equals("server", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetEnvironmentVariable("MATERIALPRO_SERVER_ROOT")
            : Environment.GetEnvironmentVariable("MATERIALPRO_CLIENT_ROOT");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(programFiles, "MaterialPro", channel.Equals("server", StringComparison.OrdinalIgnoreCase) ? "Server" : "Client");
    }

    private static bool LooksLikeInstallRoot(string root, string channel)
    {
        var exe = channel.Equals("server", StringComparison.OrdinalIgnoreCase)
            ? "MaterialPro.ServerHost.exe"
            : "MaterialPro.UI.exe";
        return File.Exists(Path.Combine(root, exe));
    }

    private static UpdateManifest? ReadLocalManifest(string root)
    {
        var path = Path.Combine(root, "update-manifest.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return ParseManifest(File.ReadAllText(path));
    }

    private static UpdateManifest? DownloadManifest(string channel)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MaterialPro-AutoUpdate/1.0");

        try
        {
            var url = $"https://api.github.com/repos/DMacredasilva/MaterialPro/contents/updates/{channel}/update-manifest.json?ref=main&ts={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var text = client.GetStringAsync(url).GetAwaiter().GetResult();
            var response = JsonSerializer.Deserialize<GitHubContentResponse>(text);
            if (response is not null && !string.IsNullOrWhiteSpace(response.Content))
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(response.Content.Replace("\n", string.Empty)));
                return ParseManifest(json);
            }
        }
        catch
        {
            // Fallback abaixo usa o raw do GitHub, que costuma funcionar melhor em redes simples de loja.
        }

        var rawUrl = $"https://raw.githubusercontent.com/DMacredasilva/MaterialPro/main/updates/{channel}/update-manifest.json?ts={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var raw = client.GetStringAsync(rawUrl).GetAwaiter().GetResult();
        return ParseManifest(raw);
    }

    private static UpdateManifest? ParseManifest(string json)
    {
        return JsonSerializer.Deserialize<UpdateManifest>(json.TrimStart('\uFEFF'));
    }

    private sealed class UpdateManifest
    {
        public string Version { get; set; } = string.Empty;
        public string PackagePath { get; set; } = string.Empty;
        public string PackageUrl { get; set; } = string.Empty;
    }

    private sealed class GitHubContentResponse
    {
        public string Content { get; set; } = string.Empty;
    }
}

public sealed record UpdateStatusSnapshot(
    string Channel,
    string InstallPath,
    string LocalVersion,
    string RemoteVersion,
    string UpdaterPath,
    bool HasLocalPackage,
    bool HasUpdater,
    string Message);
