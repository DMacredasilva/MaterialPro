using System.Diagnostics;
using System.Drawing;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text.Json;
using System.Windows.Forms;
using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Win32;

namespace MaterialPro.Installer.Server;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var form = new InstallerProgressForm("MaterialPro - Instalação do Servidor");
        var installer = new ServerInstaller();
        form.Shown += (_, _) =>
        {
            var exitCode = installer.Run(args, form.Report);
            form.Finish(exitCode);
        };

        System.Windows.Forms.Application.Run(form);
        return form.ExitCode;
    }
}

internal sealed class ServerInstaller
{
    private static Action<string>? _progress;

    private static readonly string[] WingetPackages =
    [
        "Microsoft.DotNet.Runtime.8",
        "Microsoft.DotNet.DesktopRuntime.8",
        "Microsoft.DotNet.AspNetCore.8",
        "Microsoft.VCRedist.2015+.x64",
        "Microsoft.EdgeWebView2Runtime",
        "Oracle.MySQL"
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
        var installRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MaterialPro", "Server");
        var localClientRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MaterialPro", "Client");
        var payloadRoot = Path.Combine(bundleRoot, "payload", "server");
        var localClientPayloadRoot = Path.Combine(bundleRoot, "payload", "client");
        var scriptsRoot = Path.Combine(installRoot, "scripts");
        var backupsRoot = Path.Combine(installRoot, "backups");
        var diagnosticsRoot = Path.Combine(installRoot, "diagnostics");
        var logsRoot = Path.Combine(installRoot, "logs");
        var configRoot = Path.Combine(installRoot, "config");

        Directory.CreateDirectory(installRoot);
        Directory.CreateDirectory(localClientRoot);
        Directory.CreateDirectory(scriptsRoot);
        Directory.CreateDirectory(backupsRoot);
        Directory.CreateDirectory(diagnosticsRoot);
        Directory.CreateDirectory(logsRoot);
        Directory.CreateDirectory(configRoot);

        var logPath = Path.Combine(logsRoot, $"server-setup-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        using var log = new StreamWriter(logPath, append: true) { AutoFlush = true };

        Log(log, "Instalação do servidor iniciada.");
        TryInstallPrerequisites(log);
        StopExistingServer(log);
        StopExistingClient(log);
        CopyPayload(payloadRoot, installRoot, log);
        CopyPayload(localClientPayloadRoot, localClientRoot, log);
        WriteServerConfig(configRoot, log);
        WriteLocalClientConfig(localClientRoot, log);
        WriteBackupScript(scriptsRoot, installRoot, log);
        WriteDiagnosticScript(scriptsRoot, installRoot, log);
        WriteUpdateScript(scriptsRoot, installRoot, log);
        WriteFirewallScript(scriptsRoot, installRoot, log);
        WriteControlPanelScript(scriptsRoot, log);
        WriteStartServerScript(scriptsRoot, log);
        WriteUninstallScript(scriptsRoot, installRoot, log);
        EnsureMySqlService(log);
        ConfigureDatabase(installRoot, log);
        CreateWindowsService(installRoot, log);
        ConfigureFirewall(installRoot, log);
        CreateScheduledTasks(scriptsRoot, log);
        CreateShortcuts(installRoot, scriptsRoot, localClientRoot, log);
        SaveInstallReport(configRoot, log);
        RegisterUninstallEntry(installRoot, scriptsRoot, log);
        Log(log, "Instalação do servidor concluída.");
        return 0;
    }

    private static void StopExistingServer(StreamWriter log)
    {
        Log(log, "Parando servidor MaterialPro em execução.");
        RunCommand(log, "sc.exe", "stop MaterialProServer");
        WaitForProcessExit("MaterialPro.ServerHost", TimeSpan.FromSeconds(10));
        KillProcess("MaterialPro.ServerHost", log);
    }

    private static void StopExistingClient(StreamWriter log)
    {
        Log(log, "Fechando cliente MaterialPro local em execução.");
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
        InstallAspNetRuntime(log);
        InstallVcruntime(log);
        InstallWebView2(log);
        InstallMySql(log);
    }

    private static void InstallDotNetRuntime(StreamWriter log)
        => InstallPrerequisite(log, "Microsoft.DotNet.Runtime.8", IsDotNetRuntimeInstalled, "dotnet-runtime-8.0.422-win-x64.exe", "/install /quiet /norestart");

    private static void InstallDesktopRuntime(StreamWriter log)
        => InstallPrerequisite(log, "Microsoft.DotNet.DesktopRuntime.8", IsDesktopRuntimeInstalled, "windowsdesktop-runtime-8.0.28-win-x64.exe", "/install /quiet /norestart");

    private static void InstallAspNetRuntime(StreamWriter log)
        => InstallPrerequisite(log, "Microsoft.DotNet.AspNetCore.8", IsAspNetRuntimeInstalled, "aspnetcore-runtime-8.0.28-win-x64.exe", "/install /quiet /norestart");

    private static void InstallVcruntime(StreamWriter log)
        => InstallPrerequisite(log, "Microsoft.VCRedist.2015+.x64", IsVcredistInstalled, "vc_redist.x64.exe", "/install /quiet /norestart");

    private static void InstallWebView2(StreamWriter log)
        => InstallPrerequisite(log, "Microsoft.EdgeWebView2Runtime", IsWebView2Installed, "MicrosoftEdgeWebView2RuntimeInstallerX64.exe", "/silent /install");

    private static void InstallMySql(StreamWriter log)
        => InstallPrerequisite(log, "Oracle.MySQL", IsMySqlInstalled, "mysql-installer-community-8.0.msi", "/i /quiet");

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
    private static bool IsAspNetRuntimeInstalled() => RunAndCheck("dotnet", "--list-runtimes", "Microsoft.AspNetCore.App 8.");
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

    private static bool IsMySqlInstalled() => HasCommand("mysqld") || HasCommand("mysql") || !string.IsNullOrWhiteSpace(FindMySqlDaemon());

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
            Log(log, $"Payload do servidor não encontrado em {source}");
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

        Log(log, $"Payload do servidor copiado para {target}");
    }

    private static void WriteServerConfig(string configRoot, StreamWriter log)
    {
        var config = new
        {
            connectionStrings = new
            {
                materialPro = "server=localhost;port=3306;database=materialpro;user=materialpro_system;password=MaterialPro@123!;Connection Timeout=5;Default Command Timeout=60;"
            },
            backup = new
            {
                hourly = false,
                dailyAt = "02:00"
            }
        };

        File.WriteAllText(
            Path.Combine(configRoot, "server-config.json"),
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        Log(log, "Configuração do servidor gerada.");
    }

    private static void WriteLocalClientConfig(string localClientRoot, StreamWriter log)
    {
        if (!Directory.Exists(localClientRoot))
        {
            return;
        }

        var configRoot = Path.Combine(localClientRoot, "config");
        Directory.CreateDirectory(configRoot);

        File.WriteAllText(Path.Combine(configRoot, "client-config.json"), JsonSerializer.Serialize(new
        {
            server = "127.0.0.1",
            updatedAt = DateTimeOffset.Now
        }, new JsonSerializerOptions { WriteIndented = true }));

        File.WriteAllText(Path.Combine(localClientRoot, "appsettings.json"), """
{
  "ConnectionStrings": {
    "MaterialPro": "server=127.0.0.1;port=3306;database=materialpro;user=materialpro_system;password=MaterialPro@123!;Connection Timeout=5;Default Command Timeout=60;"
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
""");
        Log(log, "Cliente local configurado para acessar o servidor em 127.0.0.1.");
    }

    private static void WriteBackupScript(string scriptsRoot, string installRoot, StreamWriter log)
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

function Find-MySqlExe($name) {
  $cmd = Get-Command $name -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }
  $found = Get-ChildItem "C:\Program Files\MySQL","C:\Program Files (x86)\MySQL" -Filter "$name.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($found) { return $found.FullName }
  return $null
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = Join-Path $PSScriptRoot "..\backups"
$target = Join-Path $backupRoot $timestamp
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item (Join-Path $PSScriptRoot "..\config") $target -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $PSScriptRoot "..\diagnostics") $target -Recurse -Force -ErrorAction SilentlyContinue

$dump = Find-MySqlExe "mysqldump"
if (-not $dump) {
  Write-Host "ERRO: mysqldump.exe nao encontrado. Instale/repare o MySQL e tente novamente."
  if (-not $Quiet) { Read-Host "Pressione ENTER para sair" }
  exit 1
}

$outFile = Join-Path $target "materialpro.sql"
$oldMysqlPwd = $env:MYSQL_PWD
try {
  $env:MYSQL_PWD = "MaterialPro@123!"
  & $dump --host=127.0.0.1 --port=3306 --user=materialpro_system --single-transaction --routines --events --databases materialpro --result-file="$outFile"
  if ($LASTEXITCODE -ne 0 -or -not (Test-Path $outFile)) {
    Write-Host "ERRO: backup do banco falhou. Codigo: $LASTEXITCODE"
    if (-not $Quiet) { Read-Host "Pressione ENTER para sair" }
    exit 1
  }
} finally {
  $env:MYSQL_PWD = $oldMysqlPwd
}

Write-Host "Backup concluido com sucesso:"
Write-Host $target
if (-not $Quiet) {
  Start-Process explorer.exe $target
  Read-Host "Pressione ENTER para sair"
}
""";
        File.WriteAllText(Path.Combine(scriptsRoot, "materialpro-backup.ps1"), script);
        Log(log, "Script de backup gerado.");
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
$result = [ordered]@{
  generatedAt = (Get-Date).ToString("s")
  dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue) -ne $null
  mysql = (Get-Command mysql -ErrorAction SilentlyContinue) -ne $null
  service = (Get-Service -Name MaterialProServer -ErrorAction SilentlyContinue).Status
  config = Test-Path (Join-Path $PSScriptRoot "..\config\server-config.json")
}
$json = $result | ConvertTo-Json -Depth 4
$out = Join-Path $PSScriptRoot "..\diagnostics\installer-diagnostic.json"
Set-Content -Path $out -Value $json -Encoding UTF8
Write-Host $json
""";
        File.WriteAllText(Path.Combine(scriptsRoot, "materialpro-diagnostics.ps1"), script);
        Log(log, "Script de diagnóstico gerado.");
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
        File.WriteAllText(Path.Combine(scriptsRoot, "materialpro-update.ps1"), script);
        Log(log, "Script de atualização gerado.");
    }

    private static void WriteFirewallScript(string scriptsRoot, string installRoot, StreamWriter log)
    {
        var script = $$"""
param(
  [switch]$Quiet
)

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  $quietArg = if ($Quiet) { " -Quiet" } else { "" }
  Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"$quietArg"
  exit
}

$serverExe = "{{Path.Combine(installRoot, "MaterialPro.ServerHost.exe")}}"
Write-Host "Liberando MaterialPro no Firewall do Windows..."
netsh advfirewall firewall delete rule name="MaterialPro Server" | Out-Null
netsh advfirewall firewall delete rule name="MaterialPro MySQL" | Out-Null
netsh advfirewall firewall delete rule name="MaterialPro MySQL Saida" | Out-Null
netsh advfirewall firewall add rule name="MaterialPro Server" dir=in action=allow program="$serverExe" enable=yes profile=any | Out-Host
netsh advfirewall firewall add rule name="MaterialPro MySQL" dir=in action=allow protocol=TCP localport=3306 enable=yes profile=any | Out-Host
netsh advfirewall firewall add rule name="MaterialPro MySQL Saida" dir=out action=allow protocol=TCP remoteport=3306 enable=yes profile=any | Out-Host
Write-Host "Firewall liberado."
if (-not $Quiet) { Read-Host "Pressione ENTER para sair" }
""";
        File.WriteAllText(Path.Combine(scriptsRoot, "materialpro-liberar-firewall.ps1"), script);
        Log(log, "Script de liberação do firewall gerado.");
    }

    private static void WriteControlPanelScript(string scriptsRoot, StreamWriter log)
    {
        var script = """
param()

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
  exit
}

function Run-Script($name) {
  $path = Join-Path $PSScriptRoot $name
  if (Test-Path $path) {
    & $path
  } else {
    Write-Host "Arquivo nao encontrado: $path"
    Read-Host "Pressione ENTER para voltar"
  }
}

while ($true) {
  Clear-Host
  Write-Host "MaterialPro - Painel do Servidor"
  Write-Host ""
  Write-Host "1 - Iniciar/reparar servidor"
  Write-Host "2 - Configurar/reparar banco"
  Write-Host "3 - Fazer backup agora"
  Write-Host "4 - Liberar firewall"
  Write-Host "5 - Diagnostico"
  Write-Host "6 - Desinstalar servidor"
  Write-Host "0 - Sair"
  Write-Host ""
  $opcao = Read-Host "Escolha uma opcao"
  switch ($opcao) {
    "1" { Run-Script "materialpro-start-server.ps1" }
    "2" { Run-Script "materialpro-configurar-banco.ps1" }
    "3" { Run-Script "materialpro-backup.ps1" }
    "4" { Run-Script "materialpro-liberar-firewall.ps1" }
    "5" { Run-Script "materialpro-diagnostics.ps1" }
    "6" { Run-Script "materialpro-uninstall-server.ps1"; return }
    "0" { return }
  }
}
""";
        File.WriteAllText(Path.Combine(scriptsRoot, "materialpro-painel-servidor.ps1"), script);
        Log(log, "Painel do servidor gerado.");
    }

    private static void WriteStartServerScript(string scriptsRoot, StreamWriter log)
    {
        var script = """
param()

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
  exit
}

Write-Host "Iniciando MaterialPro..."
$mysql = Get-Service -Name MaterialProMySQL -ErrorAction SilentlyContinue
if ($mysql) {
  if ($mysql.Status -ne "Running") {
    sc.exe start MaterialProMySQL | Out-Host
    Start-Sleep -Seconds 5
  }
} else {
  Write-Host "Servico MaterialProMySQL nao existe. Execute MaterialPro Configurar Banco primeiro."
}

$server = Get-Service -Name MaterialProServer -ErrorAction SilentlyContinue
if ($server) {
  if ($server.Status -ne "Running") {
    sc.exe start MaterialProServer | Out-Host
    Start-Sleep -Seconds 3
  }
} else {
  Write-Host "Servico MaterialProServer nao existe. Execute o instalador do servidor novamente."
}

Write-Host ""
Get-Service -Name MaterialProMySQL,MaterialProServer -ErrorAction SilentlyContinue | Format-Table Name,Status,StartType
Write-Host ""
$tcp = New-Object Net.Sockets.TcpClient
try {
  $connected = $tcp.ConnectAsync("127.0.0.1", 3306).Wait(1500) -and $tcp.Connected
} finally {
  $tcp.Dispose()
}
if ($connected) {
  Write-Host "MySQL respondendo na porta 3306."
} else {
  Write-Host "MySQL ainda nao esta respondendo na porta 3306. Use MaterialPro Configurar Banco para reparar."
}
Read-Host "Pressione ENTER para sair"
""";
        File.WriteAllText(Path.Combine(scriptsRoot, "materialpro-start-server.ps1"), script);
        Log(log, "Script de inicialização do servidor gerado.");
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
$expectedRoot = Join-Path $env:ProgramFiles "MaterialPro\Server"

if (([IO.Path]::GetFullPath($installRoot).TrimEnd('\')) -ne ([IO.Path]::GetFullPath($expectedRoot).TrimEnd('\'))) {
  Write-Host "Caminho de instalacao inesperado: $installRoot"
  if (-not $Quiet) { Read-Host "Pressione ENTER para sair" }
  exit 1
}

if (-not $Quiet) {
  $answer = Read-Host "Desinstalar MaterialPro Servidor? Isso remove servicos, atalhos e arquivos instalados. Digite SIM para continuar"
  if ($answer -ne "SIM") { exit 0 }
}

Write-Host "Parando servico MaterialPro..."
Get-Process MaterialPro.ServerHost -ErrorAction SilentlyContinue | Stop-Process -Force
sc.exe stop MaterialProServer | Out-Null
Start-Sleep -Seconds 2
sc.exe delete MaterialProServer | Out-Null

Write-Host "Removendo tarefas agendadas..."
schtasks /Delete /F /TN "MaterialPro Backup" | Out-Null
schtasks /Delete /F /TN "MaterialPro Diagnostics" | Out-Null
schtasks /Delete /F /TN "MaterialPro Updater" | Out-Null

Write-Host "Removendo regra de firewall..."
netsh advfirewall firewall delete rule name="MaterialPro Server" | Out-Null
netsh advfirewall firewall delete rule name="MaterialPro MySQL" | Out-Null

Write-Host "Removendo atalhos..."
$shortcutRoots = @(
  [Environment]::GetFolderPath("CommonDesktopDirectory"),
  [Environment]::GetFolderPath("DesktopDirectory"),
  (Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "MaterialPro"),
  (Join-Path ([Environment]::GetFolderPath("Programs")) "MaterialPro")
)
$shortcutNames = @(
  "MaterialPro Servidor.lnk",
  "MaterialPro Iniciar Servidor.lnk",
  "MaterialPro Painel do Servidor.lnk",
  "MaterialPro Configurar Banco.lnk",
  "MaterialPro Liberar Firewall.lnk",
  "MaterialPro Diagnostico.lnk",
  "MaterialPro Diagnóstico.lnk",
  "MaterialPro Backup.lnk",
  "MaterialPro Desinstalar Servidor.lnk",
  "MaterialPro Sistema.lnk"
)
foreach ($root in $shortcutRoots) {
  foreach ($name in $shortcutNames) {
    Remove-Item -LiteralPath (Join-Path $root $name) -Force
  }
  if ((Split-Path $root -Leaf) -eq "MaterialPro") {
    Remove-Item -LiteralPath $root -Force -Recurse
  }
}

Write-Host "Removendo entrada do Windows..."
Remove-Item -LiteralPath "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MaterialProServer" -Recurse -Force

Write-Host "Removendo arquivos..."
$deleteScript = @"
Start-Sleep -Seconds 3
Remove-Item -LiteralPath "$installRoot" -Recurse -Force -ErrorAction SilentlyContinue
"@
$tempScript = Join-Path $env:TEMP "materialpro-server-remove-files.ps1"
Set-Content -Path $tempScript -Value $deleteScript -Encoding UTF8
Start-Process powershell.exe -WindowStyle Hidden -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$tempScript`""

Write-Host "MaterialPro Servidor desinstalado."
if (-not $Quiet) { Read-Host "Pressione ENTER para sair" }
""";
        File.WriteAllText(Path.Combine(scriptsRoot, "materialpro-uninstall-server.ps1"), script);
        Log(log, "Script de desinstalação do servidor gerado.");
    }

    private static void ConfigureDatabase(string installRoot, StreamWriter log)
    {
        if (CanConnectMaterialPro(log))
        {
            if (EnsureMaterialProSchema(log))
            {
                Log(log, "Banco MaterialPro já configurado e com tabelas.");
                return;
            }

            Log(log, "Banco conecta, mas as tabelas não foram criadas. Tentando reparar com root.");
        }

        if (TryConfigureDatabaseWithRoot(string.Empty, log))
        {
            Log(log, "Banco MaterialPro configurado com root sem senha.");
            return;
        }

        var envPassword = Environment.GetEnvironmentVariable("MYSQL_ROOT_PASSWORD") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(envPassword) && TryConfigureDatabaseWithRoot(envPassword, log))
        {
            Log(log, "Banco MaterialPro configurado com MYSQL_ROOT_PASSWORD.");
            return;
        }

        var password = PromptPassword("Senha root do MySQL", "Informe a senha do usuário root do MySQL para criar o banco MaterialPro:");
        if (!string.IsNullOrWhiteSpace(password) && TryConfigureDatabaseWithRoot(password, log))
        {
            Log(log, "Banco MaterialPro configurado com senha informada.");
            return;
        }

        WriteDatabaseSetupScript(installRoot, log);
        Log(log, "Banco não configurado automaticamente. Execute o atalho MaterialPro Configurar Banco como administrador.");
    }

    private static void EnsureMySqlService(StreamWriter log)
    {
        if (IsTcpPortOpen("127.0.0.1", 3306, TimeSpan.FromSeconds(1)))
        {
            Log(log, "MySQL já está respondendo na porta 3306.");
            return;
        }

        Log(log, "MySQL não está respondendo na porta 3306. Tentando iniciar serviço existente.");
        var existingServiceFound = false;
        foreach (var serviceName in new[] { "MaterialProMySQL", "MySQL84", "MySQL80", "MySQL" })
        {
            if (!ServiceExists(serviceName))
            {
                continue;
            }

            existingServiceFound = true;
            RunCommand(log, "sc.exe", $"start {serviceName}");
            if (WaitForTcpPort(3306, TimeSpan.FromSeconds(15)))
            {
                Log(log, $"MySQL iniciado pelo serviço {serviceName}.");
                return;
            }
        }

        if (!existingServiceFound)
        {
            Log(log, "Nenhum serviço MySQL existente encontrado. Criando serviço MaterialProMySQL.");
        }

        var mysqldPath = FindMySqlDaemon();
        if (string.IsNullOrWhiteSpace(mysqldPath))
        {
            Log(log, "mysqld.exe não encontrado. Instale o MySQL Server 8 e execute novamente.");
            return;
        }

        var binDir = Path.GetDirectoryName(mysqldPath)!;
        var baseDir = Directory.GetParent(binDir)?.FullName ?? binDir;
        var materialProMySqlRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MaterialPro", "MySQL");
        var dataDir = Path.Combine(materialProMySqlRoot, "data");
        var iniPath = Path.Combine(materialProMySqlRoot, "my.ini");

        Directory.CreateDirectory(materialProMySqlRoot);
        Directory.CreateDirectory(dataDir);

        if (IsMySqlDataDirectoryIncomplete(dataDir))
        {
            Log(log, "Dados do MySQL estão incompletos. Recriando diretório de dados do MaterialPro.");
            RunCommand(log, "sc.exe", "stop MaterialProMySQL");
            RunCommand(log, "sc.exe", "delete MaterialProMySQL");
            TryDeleteDirectory(dataDir, log);
            Directory.CreateDirectory(dataDir);
        }

        File.WriteAllText(iniPath, $"""
[mysqld]
basedir={baseDir.Replace('\\', '/')}
datadir={dataDir.Replace('\\', '/')}
port=3306
bind-address=0.0.0.0
character-set-server=utf8mb4
collation-server=utf8mb4_unicode_ci

[client]
port=3306
default-character-set=utf8mb4
""");

        if (!Directory.Exists(Path.Combine(dataDir, "mysql")))
        {
            Log(log, "Inicializando dados do MySQL para o MaterialPro.");
            RunCommand(log, mysqldPath, $"--initialize-insecure --console --basedir=\"{baseDir}\" --datadir=\"{dataDir}\"");
            if (!IsMySqlDataDirectoryInitialized(dataDir))
            {
                Log(log, "Inicialização do MySQL não gerou os arquivos esperados. Veja o arquivo .err em C:\\ProgramData\\MaterialPro\\MySQL\\data.");
            }
        }

        Log(log, "Registrando serviço MaterialProMySQL.");
        RunCommand(log, mysqldPath, $"--install MaterialProMySQL --defaults-file=\"{iniPath}\"");
        RunCommand(log, "sc.exe", "config MaterialProMySQL start= auto");
        RunCommand(log, "sc.exe", "start MaterialProMySQL");

        if (WaitForTcpPort(3306, TimeSpan.FromSeconds(30)))
        {
            Log(log, "Serviço MaterialProMySQL iniciado na porta 3306.");
        }
        else
        {
            Log(log, "Não foi possível confirmar MySQL na porta 3306. Veja o log do instalador.");
        }
    }

    private static void CreateWindowsService(string installRoot, StreamWriter log)
    {
        var serverExe = Path.Combine(installRoot, "MaterialPro.ServerHost.exe");

        if (ServiceExists("MaterialProServer"))
        {
            Log(log, "Serviço MaterialProServer já existe. Atualizando caminho e configuração.");
            RunCommand(log, "sc.exe", "stop MaterialProServer");
            RunCommand(log, "sc.exe", $"config MaterialProServer binPath= \"{serverExe}\" start= auto");
        }
        else
        {
            RunCommand(log, "sc.exe", $"create MaterialProServer binPath= \"{serverExe}\" start= auto");
        }

        RunCommand(log, "sc.exe", "description MaterialProServer \"MaterialPro application server\"");
        RunCommand(log, "sc.exe", "start MaterialProServer");
    }

    private static bool ServiceExists(string serviceName)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("sc.exe", $"query {serviceName}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ConfigureFirewall(string installRoot, StreamWriter log)
    {
        var serverExe = Path.Combine(installRoot, "MaterialPro.ServerHost.exe");
        RunCommand(log, "netsh", "advfirewall firewall delete rule name=\"MaterialPro Server\"");
        RunCommand(log, "netsh", "advfirewall firewall delete rule name=\"MaterialPro MySQL\"");
        RunCommand(log, "netsh", "advfirewall firewall delete rule name=\"MaterialPro MySQL Saida\"");
        RunCommand(log, "netsh", $"advfirewall firewall add rule name=\"MaterialPro Server\" dir=in action=allow program=\"{serverExe}\" enable=yes profile=any");
        RunCommand(log, "netsh", "advfirewall firewall add rule name=\"MaterialPro MySQL\" dir=in action=allow protocol=TCP localport=3306 enable=yes profile=any");
        RunCommand(log, "netsh", "advfirewall firewall add rule name=\"MaterialPro MySQL Saida\" dir=out action=allow protocol=TCP remoteport=3306 enable=yes profile=any");
    }

    private static void CreateScheduledTasks(string scriptsRoot, StreamWriter log)
    {
        var backupScript = Path.Combine(scriptsRoot, "materialpro-backup.ps1");
        var diagnosticScript = Path.Combine(scriptsRoot, "materialpro-diagnostics.ps1");
        var updateScript = Path.Combine(scriptsRoot, "materialpro-update.ps1");

        RunCommand(log, "schtasks", $"/Create /F /RU SYSTEM /RL HIGHEST /SC DAILY /TN \"MaterialPro Backup\" /TR \"powershell -ExecutionPolicy Bypass -File \\\"{backupScript}\\\" -Quiet\" /ST 02:00");
        RunCommand(log, "schtasks", $"/Create /F /RU SYSTEM /RL HIGHEST /SC HOURLY /MO 4 /TN \"MaterialPro Diagnostics\" /TR \"powershell -ExecutionPolicy Bypass -File \\\"{diagnosticScript}\\\"\"");
        RunCommand(log, "schtasks", $"/Create /F /RU SYSTEM /RL HIGHEST /SC DAILY /TN \"MaterialPro Updater\" /TR \"powershell -ExecutionPolicy Bypass -File \\\"{updateScript}\\\"\" /ST 01:00");
    }

    private static void CreateShortcuts(string installRoot, string scriptsRoot, string localClientRoot, StreamWriter log)
    {
        var localClientExe = Path.Combine(localClientRoot, "MaterialPro.UI.exe");
        var powershell = PowershellExePath();
        foreach (var root in ShortcutRoots("MaterialPro", log))
        {
            if (File.Exists(localClientExe))
            {
                CreateShortcut(Path.Combine(root, "MaterialPro Sistema.lnk"), localClientExe, "", localClientRoot, log);
            }

            CreateShortcut(Path.Combine(root, "MaterialPro Painel do Servidor.lnk"), powershell, $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(scriptsRoot, "materialpro-painel-servidor.ps1")}\"", scriptsRoot, log);
            CreateShortcut(Path.Combine(root, "MaterialPro Iniciar Servidor.lnk"), powershell, $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(scriptsRoot, "materialpro-start-server.ps1")}\"", scriptsRoot, log);
            CreateShortcut(Path.Combine(root, "MaterialPro Configurar Banco.lnk"), powershell, $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(scriptsRoot, "materialpro-configurar-banco.ps1")}\"", scriptsRoot, log);
            CreateShortcut(Path.Combine(root, "MaterialPro Liberar Firewall.lnk"), powershell, $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(scriptsRoot, "materialpro-liberar-firewall.ps1")}\"", scriptsRoot, log);
            CreateShortcut(Path.Combine(root, "MaterialPro Backup.lnk"), powershell, $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(scriptsRoot, "materialpro-backup.ps1")}\"", scriptsRoot, log);
            CreateShortcut(Path.Combine(root, "MaterialPro Desinstalar Servidor.lnk"), powershell, $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(scriptsRoot, "materialpro-uninstall-server.ps1")}\"", scriptsRoot, log);
        }
        Log(log, "Atalhos do servidor criados.");
    }

    private static bool CanConnectMaterialPro(StreamWriter log)
    {
        try
        {
            using var connection = new MySqlConnector.MySqlConnection("server=localhost;port=3306;database=materialpro;user=materialpro_system;password=MaterialPro@123!;Connection Timeout=5;Default Command Timeout=60;");
            connection.Open();
            return true;
        }
        catch (Exception ex)
        {
            Log(log, $"Banco MaterialPro ainda não conecta: {ex.Message}");
            return false;
        }
    }

    private static bool TryConfigureDatabaseWithRoot(string password, StreamWriter log)
    {
        try
        {
            var passwordPart = string.IsNullOrEmpty(password) ? string.Empty : $"password={password};";
            using var connection = new MySqlConnector.MySqlConnection($"server=localhost;port=3306;user=root;{passwordPart}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
CREATE DATABASE IF NOT EXISTS materialpro CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'materialpro_system'@'%' IDENTIFIED BY 'MaterialPro@123!';
CREATE USER IF NOT EXISTS 'materialpro_system'@'localhost' IDENTIFIED BY 'MaterialPro@123!';
GRANT ALL PRIVILEGES ON materialpro.* TO 'materialpro_system'@'%';
GRANT ALL PRIVILEGES ON materialpro.* TO 'materialpro_system'@'localhost';
FLUSH PRIVILEGES;
""";
            command.ExecuteNonQuery();
            return CanConnectMaterialPro(log) && EnsureMaterialProSchema(log);
        }
        catch (Exception ex)
        {
            Log(log, $"Falha ao configurar banco com root: {ex.Message}");
            return false;
        }
    }

    private static bool EnsureMaterialProSchema(StreamWriter log)
    {
        const string connectionString = "server=localhost;port=3306;database=materialpro;user=materialpro_system;password=MaterialPro@123!;Connection Timeout=5;Default Command Timeout=60;";
        try
        {
            using var db = MaterialProDbContextFactory.CreateMySql(connectionString);
            db.Database.Migrate();
            EnsureCurrentTables(db, log);
            SeedAdminUser(db, log);
            return db.Users.Any();
        }
        catch (Exception ex)
        {
            Log(log, $"Falha ao criar/reparar tabelas do MaterialPro: {ex.Message}");
            return false;
        }
    }

    private static void EnsureCurrentTables(MaterialProDbContext db, StreamWriter log)
    {
        if (TableExists(db, "Users"))
        {
            return;
        }

        var tables = GetTableNames(db);
        if (tables.Count == 0 || tables.All(x => x.Equals("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase)))
        {
            Log(log, "Schema MaterialPro vazio ou incompleto. Criando tabelas do sistema.");
            db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS `__EFMigrationsHistory`;");
            db.GetService<IRelationalDatabaseCreator>().CreateTables();
            return;
        }

        Log(log, "Tabela Users ausente, mas existem outras tabelas no banco. Execute Configurar Banco para reparo assistido.");
    }

    private static void SeedAdminUser(MaterialProDbContext db, StreamWriter log)
    {
        if (!TableExists(db, "Users") || db.Users.Any())
        {
            return;
        }

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
        Log(log, "Usuário administrador padrão criado: admin / Admin@123");
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

    private static string PromptPassword(string title, string message)
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterScreen,
            Width = 520,
            Height = 170,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var label = new Label { Text = message, Left = 16, Top = 16, Width = 470, Height = 36 };
        var box = new TextBox { Left = 16, Top = 58, Width = 470, UseSystemPasswordChar = true };
        var ok = new Button { Text = "OK", Left = 300, Top = 96, Width = 88, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancelar", Left = 398, Top = 96, Width = 88, DialogResult = DialogResult.Cancel };
        form.Controls.Add(label);
        form.Controls.Add(box);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? box.Text : string.Empty;
    }

    private static void WriteDatabaseSetupScript(string installRoot, StreamWriter log)
    {
        var scriptsRoot = Path.Combine(installRoot, "scripts");
        var script = """
param()

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
  exit
}

function Find-MySqlExe($name) {
  $cmd = Get-Command $name -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }
  $found = Get-ChildItem "C:\Program Files\MySQL","C:\Program Files (x86)\MySQL" -Filter "$name.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($found) { return $found.FullName }
  return $null
}

function Test-Port3306 {
  $tcp = New-Object Net.Sockets.TcpClient
  try {
    return ($tcp.ConnectAsync("127.0.0.1", 3306).Wait(1500) -and $tcp.Connected)
  } catch {
    return $false
  } finally {
    $tcp.Dispose()
  }
}

function Ensure-MySqlService {
  if (Test-Port3306) { return $true }

  $mysqldPath = Find-MySqlExe "mysqld"
  if (-not $mysqldPath) {
    Write-Host "mysqld.exe nao encontrado. Instale o MySQL Server 8 e execute novamente."
    return $false
  }

  $baseDir = Split-Path (Split-Path $mysqldPath -Parent) -Parent
  $root = Join-Path $env:ProgramData "MaterialPro\MySQL"
  $dataDir = Join-Path $root "data"
  $iniPath = Join-Path $root "my.ini"
  New-Item -ItemType Directory -Path $root -Force | Out-Null
  New-Item -ItemType Directory -Path $dataDir -Force | Out-Null

  $mysqlFolder = Join-Path $dataDir "mysql"
  $ibdata = Join-Path $dataDir "ibdata1"
  $hasAnyData = (Get-ChildItem $dataDir -Force -ErrorAction SilentlyContinue | Select-Object -First 1) -ne $null
  if ($hasAnyData -and -not ((Test-Path $mysqlFolder) -and (Test-Path $ibdata))) {
    Write-Host "Dados MySQL incompletos. Recriando pasta de dados do MaterialPro..."
    sc.exe stop MaterialProMySQL | Out-Null
    sc.exe delete MaterialProMySQL | Out-Null
    Start-Sleep -Seconds 2
    Remove-Item -LiteralPath $dataDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
  }

@"
[mysqld]
basedir=$($baseDir.Replace('\','/'))
datadir=$($dataDir.Replace('\','/'))
port=3306
bind-address=0.0.0.0
character-set-server=utf8mb4
collation-server=utf8mb4_unicode_ci

[client]
port=3306
default-character-set=utf8mb4
"@ | Set-Content -Path $iniPath -Encoding ASCII

  if (-not (Test-Path $mysqlFolder)) {
    Write-Host "Inicializando MySQL do MaterialPro..."
    & $mysqldPath --initialize-insecure --console --basedir="$baseDir" --datadir="$dataDir"
  }

  if (-not (Get-Service MaterialProMySQL -ErrorAction SilentlyContinue)) {
    Write-Host "Criando servico MaterialProMySQL..."
    & $mysqldPath --install MaterialProMySQL --defaults-file="$iniPath"
    sc.exe config MaterialProMySQL start= auto | Out-Null
  }

  Write-Host "Iniciando MaterialProMySQL..."
  sc.exe start MaterialProMySQL | Out-Host
  Start-Sleep -Seconds 8
  return (Test-Port3306)
}

if (-not (Ensure-MySqlService)) {
  Write-Host "MySQL nao iniciou na porta 3306."
  Write-Host "Veja o log em C:\ProgramData\MaterialPro\MySQL\data\*.err"
  Read-Host "Pressione ENTER para sair"
  exit 1
}

$serverHost = Join-Path $PSScriptRoot "..\MaterialPro.ServerHost.exe"
$prepared = $false
if (Test-Path $serverHost) {
  Write-Host "Tentando reparar tabelas com o usuario MaterialPro..."
  & $serverHost --prepare-database
  $prepared = $LASTEXITCODE -eq 0
  if ($prepared) {
    Write-Host "Tabelas do MaterialPro prontas."
  }
}

if (-not $prepared) {
$password = Read-Host "Senha root do MySQL" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
$mysql = Get-Command mysql -ErrorAction SilentlyContinue
if (-not $mysql) {
  $mysql = Get-ChildItem "C:\Program Files\MySQL" -Filter mysql.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
}
if (-not $mysql) {
  Write-Host "mysql.exe nao encontrado."
  Read-Host "Pressione ENTER para sair"
  exit 1
}
$mysqlPath = if ($mysql.Source) { $mysql.Source } else { $mysql.FullName }
if (-not (Test-Port3306)) {
  Write-Host "MySQL nao esta rodando na porta 3306."
  Write-Host "Use este mesmo atalho para reparar o MySQL."
  Read-Host "Pressione ENTER para sair"
  exit 1
}
$sqlFile = Join-Path $env:TEMP "materialpro-configurar-banco.sql"
@"
CREATE DATABASE IF NOT EXISTS materialpro CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'materialpro_system'@'%' IDENTIFIED BY 'MaterialPro@123!';
CREATE USER IF NOT EXISTS 'materialpro_system'@'localhost' IDENTIFIED BY 'MaterialPro@123!';
GRANT ALL PRIVILEGES ON materialpro.* TO 'materialpro_system'@'%';
GRANT ALL PRIVILEGES ON materialpro.* TO 'materialpro_system'@'localhost';
FLUSH PRIVILEGES;
"@ | Set-Content -Path $sqlFile -Encoding UTF8

Write-Host "Configurando banco MaterialPro..."
$oldMysqlPwd = $env:MYSQL_PWD
try {
  $env:MYSQL_PWD = $plain
  & $mysqlPath -u root --execute "source $($sqlFile.Replace('\','/'))"
  if ($LASTEXITCODE -eq 0) {
    Write-Host "Banco MaterialPro configurado com sucesso."
  } else {
    Write-Host "Falha ao configurar banco. Codigo: $LASTEXITCODE"
  }
} finally {
  $env:MYSQL_PWD = $oldMysqlPwd
}

if (Test-Path $serverHost) {
  Write-Host "Criando/reparando tabelas do MaterialPro..."
  & $serverHost --prepare-database
  if ($LASTEXITCODE -ne 0) {
    Write-Host "Falha ao criar tabelas. Codigo: $LASTEXITCODE"
  } else {
    Write-Host "Tabelas do MaterialPro prontas."
  }
}
}

Write-Host "Liberando firewall..."
netsh advfirewall firewall delete rule name="MaterialPro Server" | Out-Null
netsh advfirewall firewall delete rule name="MaterialPro MySQL" | Out-Null
netsh advfirewall firewall delete rule name="MaterialPro MySQL Saida" | Out-Null
netsh advfirewall firewall add rule name="MaterialPro Server" dir=in action=allow program="$serverHost" enable=yes profile=any | Out-Host
netsh advfirewall firewall add rule name="MaterialPro MySQL" dir=in action=allow protocol=TCP localport=3306 enable=yes profile=any | Out-Host
netsh advfirewall firewall add rule name="MaterialPro MySQL Saida" dir=out action=allow protocol=TCP remoteport=3306 enable=yes profile=any | Out-Host

sc.exe stop MaterialProServer | Out-Null
Start-Sleep -Seconds 2
sc.exe start MaterialProServer | Out-Host
Read-Host "Pressione ENTER para sair"
""";
        File.WriteAllText(Path.Combine(scriptsRoot, "materialpro-configurar-banco.ps1"), script);
        Log(log, "Script de configuração do banco gerado.");
    }

    private static void SaveInstallReport(string configRoot, StreamWriter log)
    {
        var report = new
        {
            installedAt = DateTimeOffset.Now,
            machine = Environment.MachineName,
            type = "server"
        };

        File.WriteAllText(
            Path.Combine(configRoot, "install-report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Log(log, "Relatório de instalação salvo.");
    }

    private static void RegisterUninstallEntry(string installRoot, string scriptsRoot, StreamWriter log)
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MaterialProServer");
        if (key is null)
        {
            Log(log, "Não foi possível criar entrada de desinstalação.");
            return;
        }

        var uninstallScript = Path.Combine(scriptsRoot, "materialpro-uninstall-server.ps1");
        key.SetValue("DisplayName", "MaterialPro Servidor");
        key.SetValue("DisplayVersion", "1.0.0");
        key.SetValue("Publisher", "MaterialPro");
        key.SetValue("InstallLocation", installRoot);
        key.SetValue("DisplayIcon", Path.Combine(installRoot, "MaterialPro.ServerHost.exe"));
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

    private static string FindMySqlDaemon()
    {
        var fromPath = ResolveCommand("mysqld");
        if (!string.IsNullOrWhiteSpace(fromPath))
        {
            return fromPath;
        }

        foreach (var root in new[] { @"C:\Program Files\MySQL", @"C:\Program Files (x86)\MySQL" })
        {
            try
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                var candidate = Directory
                    .GetFiles(root, "mysqld.exe", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore protected folders while searching common MySQL locations.
            }
        }

        return string.Empty;
    }

    private static bool WaitForTcpPort(int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (IsTcpPortOpen("127.0.0.1", port, TimeSpan.FromMilliseconds(800)))
            {
                return true;
            }

            Thread.Sleep(500);
        }

        return false;
    }

    private static bool IsTcpPortOpen(string host, int port, TimeSpan timeout)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(host, port);
            return connect.Wait(timeout) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMySqlDataDirectoryInitialized(string dataDir)
    {
        return Directory.Exists(Path.Combine(dataDir, "mysql"))
            && File.Exists(Path.Combine(dataDir, "ibdata1"));
    }

    private static bool IsMySqlDataDirectoryIncomplete(string dataDir)
    {
        if (!Directory.Exists(dataDir))
        {
            return false;
        }

        var hasAnyFile = Directory.EnumerateFileSystemEntries(dataDir).Any();
        return hasAnyFile && !IsMySqlDataDirectoryInitialized(dataDir);
    }

    private static void TryDeleteDirectory(string path, StreamWriter log)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Log(log, $"Não foi possível limpar {path}: {ex.Message}");
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
