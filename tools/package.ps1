# Baut zwei Weitergabe-Pakete:
# - dist\GrandTheftBarfenders-<Version>.zip: mit Installer (Install.bat, Uninstall.bat, README.txt,
#   setup\*.ps1, files\Mods + files\Plugins) – für Freunde.
# - dist\GrandTheftBarfenders-<Version>-Nexus.zip: ohne ausführbare Dateien (Nexus Mods lehnt .bat/.ps1 ab),
#   nur Mods\ + Plugins\ wie im Spielordner und README.txt mit Anleitung zum Selbst-Installieren.
# Aufruf aus dem Projektordner: powershell -ExecutionPolicy Bypass -File tools\package.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:DOTNET_NOLOGO = 1

$version = ([xml](Get-Content (Join-Path $root 'GrandTheftBarfenders\GrandTheftBarfenders.csproj'))).Project.PropertyGroup.Version | Select-Object -First 1
$name = "GrandTheftBarfenders-$version"
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist $name

# Beide Projekte aus diesem Repository bauen (alles im Paket stammt aus dem veröffentlichten Quellcode),
# ohne in den eigenen Spielordner zu kopieren und ohne Compiler-Server (sperrt sonst Spiel-DLLs).
foreach ($project in 'GrandTheftBarfenders', 'Il2CppInteropUnity6000Fix') {
    & $dotnet build (Join-Path $root $project) -c Release -p:CopyToMods=false -p:CopyToPlugins=false -nodeReuse:false -v:q -clp:NoSummary
    if ($LASTEXITCODE -ne 0) { throw "Build von $project fehlgeschlagen" }
}
& $dotnet build-server shutdown | Out-Null
$fix = Join-Path $root 'Il2CppInteropUnity6000Fix\bin\Release\net6.0\Il2CppInteropUnity6000Fix.dll'

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'files\Mods'), (Join-Path $stage 'files\Plugins') | Out-Null

$modDll = Join-Path $root 'GrandTheftBarfenders\bin\Release\net6.0\GrandTheftBarfenders.dll'
Copy-Item (Join-Path $root 'package\*') $stage -Recurse -Force -Exclude 'README-Nexus.txt'
Copy-Item $modDll (Join-Path $stage 'files\Mods')
Copy-Item $fix (Join-Path $stage 'files\Plugins')

# Nexus-Paket: nur DLLs + Anleitung.
$nexusStage = Join-Path $dist "$name-Nexus"
if (Test-Path $nexusStage) { Remove-Item $nexusStage -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $nexusStage 'Mods'), (Join-Path $nexusStage 'Plugins') | Out-Null
Copy-Item $modDll (Join-Path $nexusStage 'Mods')
Copy-Item $fix (Join-Path $nexusStage 'Plugins')
Copy-Item (Join-Path $root 'package\README-Nexus.txt') (Join-Path $nexusStage 'README.txt')

foreach ($s in $stage, $nexusStage) {
    $zip = "$s.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $s '*') -DestinationPath $zip
    Write-Output "Paket: $zip ($([math]::Round((Get-Item $zip).Length / 1KB)) KB)"
    Get-ChildItem $s -Recurse -File | ForEach-Object { '  ' + $_.FullName.Substring($s.Length + 1) }
}
