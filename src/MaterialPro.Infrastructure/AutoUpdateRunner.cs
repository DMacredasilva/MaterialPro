using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MaterialPro.Infrastructure;

public static class AutoUpdateRunner
{
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

    private static UpdateManifest? ReadLocalManifest(string root)
    {
        var path = Path.Combine(root, "update-manifest.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<UpdateManifest>(File.ReadAllText(path));
    }

    private static UpdateManifest? DownloadManifest(string channel)
    {
        var url = $"https://api.github.com/repos/DMacredasilva/MaterialPro/contents/updates/{channel}/update-manifest.json?ref=main&ts={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MaterialPro-AutoUpdate/1.0");
        var text = client.GetStringAsync(url).GetAwaiter().GetResult();
        var response = JsonSerializer.Deserialize<GitHubContentResponse>(text);
        if (response is null || string.IsNullOrWhiteSpace(response.Content))
        {
            return null;
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(response.Content.Replace("\n", string.Empty)));
        return JsonSerializer.Deserialize<UpdateManifest>(json);
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
