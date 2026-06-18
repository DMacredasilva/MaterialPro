using System.Text.Json;

namespace MaterialPro.Infrastructure;

public sealed class MaterialProSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public FiscalSettings Fiscal { get; set; } = new();
}

public sealed class FiscalSettings
{
    public bool Enabled { get; set; }
    public string Environment { get; set; } = "homologacao";
    public string Cnpj { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public string CertificatePath { get; set; } = string.Empty;
    public string CertificatePassword { get; set; } = string.Empty;
    public string CscId { get; set; } = string.Empty;
    public string CscToken { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = "MOC 7.0";
    public string NfeServiceUrl { get; set; } = string.Empty;
    public string NfceServiceUrl { get; set; } = string.Empty;
    public string UpdateFeedUrl { get; set; } = string.Empty;
}

public static class MaterialProSettingsLoader
{
    public static MaterialProSettings Load(string basePath)
    {
        var settingsPath = ResolveSettingsPath(basePath);
        if (!File.Exists(settingsPath))
        {
            return new MaterialProSettings();
        }

        using var stream = File.OpenRead(settingsPath);
        using var document = JsonDocument.Parse(stream);

        var root = document.RootElement;
        var connectionString = root
            .GetProperty("ConnectionStrings")
            .GetProperty("MaterialPro")
            .GetString() ?? string.Empty;

        var settings = new MaterialProSettings { ConnectionString = connectionString };

        if (root.TryGetProperty("Fiscal", out var fiscal))
        {
            settings.Fiscal = new FiscalSettings
            {
                Enabled = fiscal.TryGetProperty("Enabled", out var enabled) && enabled.GetBoolean(),
                Environment = GetString(fiscal, "Environment", "homologacao"),
                Cnpj = GetString(fiscal, "Cnpj"),
                Uf = GetString(fiscal, "Uf"),
                CertificatePath = GetString(fiscal, "CertificatePath"),
                CertificatePassword = GetString(fiscal, "CertificatePassword"),
                CscId = GetString(fiscal, "CscId"),
                CscToken = GetString(fiscal, "CscToken"),
                SchemaVersion = GetString(fiscal, "SchemaVersion", "MOC 7.0"),
                NfeServiceUrl = GetString(fiscal, "NfeServiceUrl"),
                NfceServiceUrl = GetString(fiscal, "NfceServiceUrl"),
                UpdateFeedUrl = GetString(fiscal, "UpdateFeedUrl")
            };
        }

        return settings;
    }

    private static string GetString(JsonElement element, string name, string fallback = "")
    {
        return element.TryGetProperty(name, out var child) ? child.GetString() ?? fallback : fallback;
    }

    public static string WritableClientSettingsPath()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MaterialPro",
            "Client");
        Directory.CreateDirectory(root);
        return Path.Combine(root, "appsettings.json");
    }

    public static IReadOnlyList<string> CandidateSettingsPaths(string basePath)
    {
        var processName = Path.GetFileName(Environment.ProcessPath ?? string.Empty);
        var isClient = processName.Contains("MaterialPro.UI", StringComparison.OrdinalIgnoreCase)
            || basePath.Contains($"{Path.DirectorySeparatorChar}Client", StringComparison.OrdinalIgnoreCase)
            || basePath.Contains($"{Path.AltDirectorySeparatorChar}Client", StringComparison.OrdinalIgnoreCase);

        if (!isClient)
        {
            return new[] { Path.Combine(basePath, "appsettings.json") };
        }

        return new[]
        {
            WritableClientSettingsPath(),
            Path.Combine(basePath, "appsettings.json")
        };
    }

    private static string ResolveSettingsPath(string basePath)
    {
        return CandidateSettingsPaths(basePath).FirstOrDefault(File.Exists)
            ?? Path.Combine(basePath, "appsettings.json");
    }
}
