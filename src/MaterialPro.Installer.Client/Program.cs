using System.Diagnostics;
using System.Drawing;
using System.Security.Principal;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MaterialPro.Installer.Client;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var form = new InstallerProgressForm("MaterialPro - Instalação do Cliente");
        var installer = new ClientInstaller();
        form.Shown += (_, _) =>
        {
            var exitCode = installer.Run(args, form.Report);
            form.Finish(exitCode);
        };

        System.Windows.Forms.Application.Run(form);
        return form.ExitCode;
    }
}

internal sealed class ClientInstaller
{
    private static Action<string>? _progress;

    private static readonly string[] WingetPackages =
    [
        "Microsoft.DotNet.Runtime.8",
        "Microsoft.DotNet.DesktopRuntime.8",
        "Microsoft.VCRedist.2015+.x64",
        "Microsoft.EdgeWebView2Runtime"
    ];

    public int Run(string[] args, Action<string>? progress = null)
    {
        _progress = progress;
        if (!IsAdministrator())
        {
            const string message = "Execute o instalador como administrador.";
            _progress?.Invoke(message);
            MessageBox.Show(message, "MaterialPro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }

        var bundleRoot = AppContext.BaseDirectory;
        var installRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MaterialPro", "Client");
        var payloadRoot = Path.Combine(bundleRoot, "payload", "client");
        var scriptsRoot = Path.Combine(installRoot, "scripts");
        var logsRoot = Path.Combine(installRoot, "logs");
        var diagnosticsRoot = Path.Combine(installRoot, "diagnostics");
        var configRoot = Path.Combine(installRoot, "config");

        Directory.CreateDirectory(installRoot);
        Directory.CreateDirectory(scriptsRoot);
        Directory.CreateDirectory(logsRoot);
        Directory.CreateDirectory(diagnosticsRoot);
        Directory.CreateDirectory(configRoot);

        var logPath = Path.Combine(logsRoot, $"client-setup-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        using var log = new StreamWriter(logPath, append: true) { AutoFlush = true };

        Log(log, "Instalação do cliente iniciada.");
        TryInstallPrerequisites(log);
        StopExistingClient(log);
        CopyPayload(payloadRoot, installRoot, log);
        SaveServerConnection(installRoot, configRoot, args, log);
        WriteDiagnosticScript(scriptsRoot, installRoot, log);
        WriteUpdateScript(scriptsRoot, installRoot, log);
        WriteUninstallScript(scriptsRoot, installRoot, log);
        CreateDesktopShortcuts(installRoot, scriptsRoot, log);
        DetectPrinters(log);
        CreateScheduledTasks(scriptsRoot, log);
        SaveInstallReport(configRoot, log);
        RegisterUninstallEntry(installRoot, scriptsRoot, log);
        Log(log, "Instalação do cliente concluída.");
        return 0;
    }

    private static void StopExistingClient(StreamWriter log)
    {
        Log(log, "Fechando cliente MaterialPro em execução.");
        WaitForProcessExit("MaterialPro.UI", TimeSpan.FromSeconds(5));
        KillProcess("MaterialPro.UI", log);
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void TryInstallPrerequisites(StreamWriter log)
    {
        InstallDotNetRuntime(log);
        InstallDesktopRuntime(log);
        InstallVcruntime(log);
        InstallWebView2(log);
    }

    private static void InstallDotNetRuntime(StreamWriter log)
        => InstallPrerequisite(log, "Microsoft.DotNet.Runtime.8", IsDotNetRuntimeInstalled, "dotnet-runtime-8.0.422-win-x64.exe", "/install /quiet /norestart");

    private static void InstallDesktopRuntime(StreamWriter log)
        => InstallPrerequisite(log, "Microsoft.DotNet.DesktopRuntime.8", IsDesktopRuntimeInstalled, "windowsdesktop-runtime-8.0.28-win-x64.exe", "/install /quiet /norestart");

    private static void InstallVcruntime(StreamWriter log)
        => InstallPrerequisite(log, "Microsoft.VCRedist.2015+.x64", IsVcredistInstalled, "vc_redist.x64.exe", "/install /quiet /norestart");

    private static void InstallWebView2(StreamWriter log)
        => InstallPrerequisite(log, "Microsoft.EdgeWebView2Runtime", IsWebView2Installed, "MicrosoftEdgeWebView2RuntimeInstallerX64.exe", "/silent /install");

    private static void InstallPrerequisite(StreamWriter log, string wingetId, Func<bool> installedCheck, string localFile, string localArgs)
    {
        if (installedCheck())
        {
            Log(log, $"{wingetId} já instalado.");
            return;
        }

        if (TryRunWinget(log, wingetId))
        {
            return;
        }

        var prereqRoot = Path.Combine(AppContext.BaseDirectory, "prereqs");
        var localPath = Path.Combine(prereqRoot, localFile);
        if (File.Exists(localPath))
        {
            if (Path.GetExtension(localPath).Equals(".msi", StringComparison.OrdinalIgnoreCase))
            {
                RunCommand(log, "msiexec.exe", $"/i \"{localPath}\" /quiet /norestart");
            }
            else
            {
                RunCommand(log, localPath, localArgs);
            }
            return;
        }

        Log(log, $"Nenhum instalador local encontrado para {wingetId} em {prereqRoot}");
    }

    private static bool TryRunWinget(StreamWriter log, string wingetId)
    {
        if (!HasCommand("winget"))
        {
            Log(log, "winget não disponível.");
            return false;
        }

        RunCommand(log, "winget", $"install --id {wingetId} -e --silent --accept-package-agreements --accept-source-agreements");
        return true;
    }

    private static bool HasCommand(string name) => !string.IsNullOrWhiteSpace(ResolveCommand(name));

    private static string ResolveCommand(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var segment in path.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(segment.Trim(), $"{name}.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return string.Empty;
    }

    private static bool IsDotNetRuntimeInstalled() => RunAndCheck("dotnet", "--list-runtimes", "Microsoft.NETCore.App 8.");
    private static bool IsDesktopRuntimeInstalled() => RunAndCheck("dotnet", "--list-runtimes", "Microsoft.WindowsDesktop.App 8.");
    private static bool IsVcredistInstalled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
        return key?.GetValue("Installed") is int installed && installed == 1;
    }

    private static bool IsWebView2Installed()
    {
        var roots = new[]
        {
            @"C:\Program Files (x86)\Microsoft\EdgeWebView\Application",
            @"C:\Program Files\Microsoft\EdgeWebView\Application"
        };
        return roots.Any(Directory.Exists);
    }

    private static bool RunAndCheck(string fileName, string arguments, string contains)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.Contains(contains, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void CopyPayload(string source, string target, StreamWriter log)
    {
        if (!Directory.Exists(source))
        {
            Log(log, $"Payload do cliente não encontrado em {source}");
            return;
        }

        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, target));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = file.Replace(source, target);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }

        Log(log, $"Payload do cliente copiado para {target}");
    }

    private static void SaveServerConnection(string installRoot, string configRoot, string[] args, StreamWriter log)
    {
        var server = args.FirstOrDefault(x => x.StartsWith("--server=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1]
            ?? Environment.GetEnvironmentVariable("MATERIALPRO_SERVER")
            ?? PromptServerAddress();

        if (string.IsNullOrWhiteSpace(server))
        {
            server = "127.0.0.1";
        }

        server = server.Trim();

        var config = new
        {
            server,
            updatedAt = DateTimeOffset.Now
        };

        File.WriteAllText(
            Path.Combine(configRoot, "client-config.json"),
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        File.WriteAllText(Path.Combine(installRoot, "appsettings.json"), BuildAppSettings(server));
        Log(log, $"Conexão com servidor configurada para {server}");
    }

    private static string PromptServerAddress()
    {
        using var form = new Form
        {
            Text = "MaterialPro - IP do Servidor",
            StartPosition = FormStartPosition.CenterScreen,
            Width = 520,
            Height = 170,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var label = new Label { Text = "Informe o IP ou nome do servidor MaterialPro:", Left = 16, Top = 16, Width = 470, Height = 26 };
        var box = new TextBox { Left = 16, Top = 48, Width = 470, Text = "127.0.0.1" };
        var ok = new Button { Text = "OK", Left = 300, Top = 88, Width = 88, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancelar", Left = 398, Top = 88, Width = 88, DialogResult = DialogResult.Cancel };
        form.Controls.Add(label);
        form.Controls.Add(box);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? box.Text : "127.0.0.1";
    }

    private static string BuildAppSettings(string server)
    {
        return $$"""
{
  "ConnectionStrings": {
    "MaterialPro": "server={{server}};port=3306;database=materialpro;user=materialpro_system;password=MaterialPro@123!;Connection Timeout=5;Default Command Timeout=60;"
  },
  "Fiscal": {
    "Enabled": false,
    "Environment": "homologacao",
    "Cnpj": "",
    "Uf": "SP",
    "CertificatePath": "",
    "CertificatePassword": "",
    "CscId": "",
    "CscToken": "",
    "SchemaVersion": "MOC 7.0",
    "NfeServiceUrl": "",
    "NfceServiceUrl": "",
    "UpdateFeedUrl": ""
  }
}
""";
    }

    private static void WriteDiagnosticScript(string scriptsRoot, string installRoot, StreamWriter log)
    {
        var script = """
param()
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
  exit
}
$printers = Get-Printer -ErrorAction SilentlyContinue | Select-Object Name, DriverName, PortName
$result = [ordered]@{
  generatedAt = (Get-Date).ToString("s")
  dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue) -ne $null
  webview2 = Test-Path "C:\Program Files (x86)\Microsoft\EdgeWebView\Application"
  printers = $printers
}
$json = $result | ConvertTo-Json -Depth 6
$out = Join-Path $PSScriptRoot "..\diagnostics\client-diagnostic.json"
Set-Content -Path $out -Value $json -Encoding UTF8
Write-Host $json
""";
        File.WriteAllText(Path.Combine(scriptsRoot, "materialpro-client-diagnostics.ps1"), script);
        Log(log, "Script de diagnóstico do cliente gerado.");
    }

    private static void WriteUpdateScript(string scriptsRoot, string installRoot, StreamWriter log)
    {
        var script = """
param()
$updater = Join-Path $PSScriptRoot "..\MaterialPro.Updater.exe"
if (Test-Path $updater) {
  & $updater check
}
""";
        File.WriteAllText(Path.Combine(scriptsRoot, "materialpro-client-update.ps1"), script);
        Log(log, "Script de atualização do cliente gerado.");
    }

    private static void WriteUninstallScript(string scriptsRoot, string installRoot, StreamWriter log)
    {
        var script = """
param(
  [switch]$Quiet
)

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  $quietArg = if ($Quiet) { " -Quiet" } else { "" }
  Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"$quietArg"
  exit
}

$ErrorActionPreference = "SilentlyContinue"
$installRoot = Split-Path -Parent $PSScriptRoot
$expectedRoot = Join-Path $env:ProgramFiles "MaterialPro\Client"

if (([IO.Path]::GetFullPath($installRoot).TrimEnd('\')) -ne ([IO.Path]::GetFullPath($expectedRoot).TrimEnd('\'))) {
  Write-Host "Caminho de instalacao inesperado: $installRoot"
  if (-not $Quiet) { Read-Host "Pressione ENTER para sair" }
  exit 1
}

if (-not $Quiet) {
  $answer = Read-Host "Desinstalar MaterialPro Cliente? Isso remove atalhos e arquivos instalados. Digite SIM para continuar"
  if ($answer -ne "SIM") { exit 0 }
}

Write-Host "Fechando MaterialPro..."
Get-Process MaterialPro.UI -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "Removendo tarefas agendadas..."
schtasks /Delete /F /TN "MaterialPro Client Updater" | Out-Null
schtasks /Delete /F /TN "MaterialPro Client Diagnostics" | Out-Null

Write-Host "Removendo atalhos..."
$shortcutRoots = @(
  [Environment]::GetFolderPath("CommonDesktopDirectory"),
  [Environment]::GetFolderPath("DesktopDirectory"),
  (Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "MaterialPro"),
  (Join-Path ([Environment]::GetFolderPath("Programs")) "MaterialPro")
)
$shortcutNames = @("MaterialPro.lnk", "MaterialPro Desinstalar.lnk")
foreach ($root in $shortcutRoots) {
  foreach ($name in $shortcutNames) {
    Remove-Item -LiteralPath (Join-Path $root $name) -Force
  }
  if ((Split-Path $root -Leaf) -eq "MaterialPro") {
    Remove-Item -LiteralPath $root -Force -Recurse
  }
}

Write-Host "Removendo entrada do Windows..."
Remove-Item -LiteralPath "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MaterialProClient" -Recurse -Force

Write-Host "Removendo arquivos..."
$deleteScript = @"
Start-Sleep -Seconds 3
Remove-Item -LiteralPath "$installRoot" -Recurse -Force -ErrorAction SilentlyContinue
"@
$tempScript = Join-Path $env:TEMP "materialpro-client-remove-files.ps1"
Set-Content -Path $tempScript -Value $deleteScript -Encoding UTF8
Start-Process powershell.exe -WindowStyle Hidden -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$tempScript`""

Write-Host "MaterialPro Cliente desinstalado."
if (-not $Quiet) { Read-Host "Pressione ENTER para sair" }
""";
        File.WriteAllText(Path.Combine(scriptsRoot, "materialpro-uninstall-client.ps1"), script);
        Log(log, "Script de desinstalação do cliente gerado.");
    }

    private static void CreateDesktopShortcuts(string installRoot, string scriptsRoot, StreamWriter log)
    {
        var powershell = PowershellExePath();
        foreach (var root in ShortcutRoots("MaterialPro", log))
        {
            CreateShortcut(Path.Combine(root, "MaterialPro.lnk"), Path.Combine(installRoot, "MaterialPro.UI.exe"), "", installRoot, log);
            CreateShortcut(Path.Combine(root, "MaterialPro Desinstalar.lnk"), powershell, $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(scriptsRoot, "materialpro-uninstall-client.ps1")}\"", scriptsRoot, log);
        }
        Log(log, "Atalhos do cliente criados.");
    }

    private static void DetectPrinters(StreamWriter log)
    {
        RunCommand(log, "powershell", "-Command \"Get-Printer | Select-Object Name,DriverName,PortName\"");
    }

    private static void CreateScheduledTasks(string scriptsRoot, StreamWriter log)
    {
        var diagnosticScript = Path.Combine(scriptsRoot, "materialpro-client-diagnostics.ps1");
        var updateScript = Path.Combine(scriptsRoot, "materialpro-client-update.ps1");

        RunCommand(log, "schtasks", $"/Create /F /SC DAILY /TN \"MaterialPro Client Updater\" /TR \"powershell -ExecutionPolicy Bypass -File \\\"{updateScript}\\\"\" /ST 08:00");
        RunCommand(log, "schtasks", $"/Create /F /SC DAILY /TN \"MaterialPro Client Diagnostics\" /TR \"powershell -ExecutionPolicy Bypass -File \\\"{diagnosticScript}\\\"\" /ST 08:15");
    }

    private static void SaveInstallReport(string configRoot, StreamWriter log)
    {
        var report = new
        {
            installedAt = DateTimeOffset.Now,
            machine = Environment.MachineName,
            type = "client"
        };

        File.WriteAllText(
            Path.Combine(configRoot, "install-report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Log(log, "Relatório de instalação salvo.");
    }

    private static void RegisterUninstallEntry(string installRoot, string scriptsRoot, StreamWriter log)
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MaterialProClient");
        if (key is null)
        {
            Log(log, "Não foi possível criar entrada de desinstalação.");
            return;
        }

        var uninstallScript = Path.Combine(scriptsRoot, "materialpro-uninstall-client.ps1");
        key.SetValue("DisplayName", "MaterialPro Cliente");
        key.SetValue("DisplayVersion", "1.0.0");
        key.SetValue("Publisher", "MaterialPro");
        key.SetValue("InstallLocation", installRoot);
        key.SetValue("DisplayIcon", Path.Combine(installRoot, "MaterialPro.UI.exe"));
        var powershell = PowershellExePath();
        key.SetValue("UninstallString", $"\"{powershell}\" -NoProfile -ExecutionPolicy Bypass -File \"{uninstallScript}\" -Quiet");
        key.SetValue("QuietUninstallString", $"\"{powershell}\" -NoProfile -ExecutionPolicy Bypass -File \"{uninstallScript}\" -Quiet");
        key.SetValue("EstimatedSize", GetDirectorySizeKb(installRoot), RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        Log(log, "Entrada em Adicionar/Remover Programas registrada.");
    }

    private static void RunCommand(StreamWriter log, string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                Log(log, $"{fileName} não iniciou.");
                return;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (!string.IsNullOrWhiteSpace(output)) Log(log, output.Trim());
            if (!string.IsNullOrWhiteSpace(error)) Log(log, error.Trim());
        }
        catch (Exception ex)
        {
            Log(log, $"{fileName}: {ex.Message}");
        }
    }

    private static void WaitForProcessExit(string processName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Process.GetProcessesByName(processName).Length == 0)
            {
                return;
            }

            Thread.Sleep(500);
        }
    }

    private static void KillProcess(string processName, StreamWriter log)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                Log(log, $"Encerrando processo {process.ProcessName} ({process.Id}).");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                Log(log, $"Não foi possível encerrar {processName}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static string PowershellExePath()
    {
        var path = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
        return File.Exists(path) ? path : "powershell.exe";
    }

    private static IReadOnlyList<string> ShortcutRoots(string startMenuFolderName, StreamWriter log)
    {
        var roots = new List<string>();
        AddShortcutRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
        AddShortcutRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        AddShortcutRoot(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), startMenuFolderName));
        AddShortcutRoot(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), startMenuFolderName));

        foreach (var root in roots)
        {
            try
            {
                Directory.CreateDirectory(root);
            }
            catch (Exception ex)
            {
                Log(log, $"Não foi possível preparar pasta de atalhos {root}: {ex.Message}");
            }
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddShortcutRoot(List<string> roots, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            roots.Add(path);
        }
    }

    private static void CreateShortcut(string path, string targetPath, string arguments, string workingDirectory, StreamWriter log)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var shell = Type.GetTypeFromProgID("WScript.Shell");
            if (shell is null)
            {
                Log(log, "WScript.Shell não disponível para criar atalhos.");
                return;
            }

            dynamic instance = Activator.CreateInstance(shell)!;
            dynamic shortcut = instance.CreateShortcut(path);
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.Save();
            Log(log, $"Atalho criado: {path}");
        }
        catch (Exception ex)
        {
            Log(log, $"Falha ao criar atalho {path}: {ex.Message}");
        }
    }

    private static int GetDirectorySizeKb(string path)
    {
        try
        {
            var bytes = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
            return (int)Math.Min(int.MaxValue, Math.Max(1, bytes / 1024));
        }
        catch
        {
            return 1;
        }
    }

    private static void Log(StreamWriter log, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        Console.WriteLine(line);
        log.WriteLine(line);
        _progress?.Invoke(line);
    }
}

internal sealed class InstallerProgressForm : Form
{
    private readonly TextBox _logBox;
    private readonly ProgressBar _progressBar;
    private readonly Button _closeButton;

    public int ExitCode { get; private set; }

    public InstallerProgressForm(string title)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 520);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(16);

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold)
        };

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 22,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 32
        };

        _logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
            Font = new Font("Consolas", 9F)
        };

        _closeButton = new Button
        {
            Text = "Fechar",
            Dock = DockStyle.Right,
            Width = 110,
            Enabled = false
        };
        _closeButton.Click += (_, _) => Close();

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(0, 10, 0, 0) };
        bottom.Controls.Add(_closeButton);

        Controls.Add(_logBox);
        Controls.Add(bottom);
        Controls.Add(_progressBar);
        Controls.Add(titleLabel);
    }

    public void Report(string message)
    {
        _logBox.AppendText(message + Environment.NewLine);
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
        System.Windows.Forms.Application.DoEvents();
    }

    public void Finish(int exitCode)
    {
        ExitCode = exitCode;
        _progressBar.Style = ProgressBarStyle.Blocks;
        _progressBar.Value = exitCode == 0 ? 100 : 0;
        _closeButton.Enabled = true;
        Report(exitCode == 0 ? "Instalação concluída com sucesso." : "Instalação finalizada com erro.");
        MessageBox.Show(
            exitCode == 0 ? "Instalação concluída com sucesso." : "Instalação finalizada com erro. Veja o log na tela.",
            "MaterialPro",
            MessageBoxButtons.OK,
            exitCode == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }
}
