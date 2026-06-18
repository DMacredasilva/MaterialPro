param(
    [string]$Configuration = "Release",
    [string]$Version = (Get-Date -Format "yyyy.MM.dd.HHmm")
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet -and (Test-Path "C:\Program Files\dotnet\dotnet.exe")) {
    $dotnet = "C:\Program Files\dotnet\dotnet.exe"
}
if (-not $dotnet) {
    throw "dotnet SDK nao encontrado."
}
$distRoot = Join-Path $root "dist"
$serverPackage = Join-Path $distRoot "server"
$clientPackage = Join-Path $distRoot "client"
$serverPayload = Join-Path $serverPackage "payload\server"
$clientPayload = Join-Path $clientPackage "payload\client"

Remove-Item $distRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $serverPayload -Force | Out-Null
New-Item -ItemType Directory -Path $clientPayload -Force | Out-Null

Write-Host "Publicando payload do servidor..."
& $dotnet publish "$root\src\MaterialPro.ServerHost\MaterialPro.ServerHost.csproj" `
    -c $Configuration -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:PublishTrimmed=false `
    -o $serverPayload

Write-Host "Publicando payload do cliente..."
& $dotnet publish "$root\src\MaterialPro.UI\MaterialPro.UI.csproj" `
    -c $Configuration -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:PublishTrimmed=false `
    -o $clientPayload

Write-Host "Publicando updater..."
& $dotnet publish "$root\src\MaterialPro.Updater\MaterialPro.Updater.csproj" `
    -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o "$distRoot\updater"

Copy-Item "$distRoot\updater\MaterialPro.Updater.exe" $serverPayload -Force
Copy-Item "$distRoot\updater\MaterialPro.Updater.exe" $clientPayload -Force
Copy-Item "$root\src\MaterialPro.Database\materialpro.mysql.sql" $serverPayload -Force
Copy-Item "$root\src\MaterialPro.ServerHost\appsettings.json" $serverPayload -Force
Copy-Item "$root\src\MaterialPro.UI\appsettings.json" $clientPayload -Force

$serverManifest = @{
    Version = $Version
    PackagePath = ".\update-package.zip"
    PackageUrl = "https://raw.githubusercontent.com/DMacredasilva/MaterialPro/main/updates/server/update-package.zip?v=$Version"
} | ConvertTo-Json -Depth 3

$clientManifest = @{
    Version = $Version
    PackagePath = ".\update-package.zip"
    PackageUrl = "https://raw.githubusercontent.com/DMacredasilva/MaterialPro/main/updates/client/update-package.zip?v=$Version"
} | ConvertTo-Json -Depth 3

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $serverPayload "update-manifest.json"), $serverManifest, $utf8NoBom)
[System.IO.File]::WriteAllText((Join-Path $clientPayload "update-manifest.json"), $clientManifest, $utf8NoBom)

$serverUpdateZip = Join-Path $serverPackage "update-package.zip"
$clientUpdateZip = Join-Path $clientPackage "update-package.zip"
Remove-Item $serverUpdateZip, $clientUpdateZip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $serverPayload "*") -DestinationPath $serverUpdateZip -Force
Compress-Archive -Path (Join-Path $clientPayload "*") -DestinationPath $clientUpdateZip -Force

Write-Host "Publicando instalador do servidor..."
& $dotnet publish "$root\src\MaterialPro.Installer.Server\MaterialPro.Installer.Server.csproj" `
    -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true `
    -o $serverPackage

Write-Host "Publicando instalador do cliente..."
& $dotnet publish "$root\src\MaterialPro.Installer.Client\MaterialPro.Installer.Client.csproj" `
    -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true `
    -o $clientPackage

Write-Host "Pacotes gerados:"
Write-Host " - $serverPackage\MaterialProServerSetup.exe"
Write-Host " - $clientPackage\MaterialProClientSetup.exe"
Write-Host " - $serverUpdateZip"
Write-Host " - $clientUpdateZip"
Write-Host " - $distRoot\updater\MaterialPro.Updater.exe"
