# Installs "Grand Theft Barfenders": checks whether MelonLoader is already set up (e.g. by
# "Barfender Staff Roles") and only installs it when it is missing; then the folder link, the shared
# fix plugin (only if missing or older) and the mod. Other mods in the game folder are left alone.
# Run via Install.bat; for tests: install.ps1 -GameDir "D:\...\Barfender"
param([string]$GameDir)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$MelonLoaderUrl = 'https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip'
$MelonLoaderSha256 = '5B2B2F3D1CD42B59EC886C5BDC2663EDAE87A0097A4F4A8F58C0965A99DDA416'
$MelonLoaderVersion = '0.7.3'
$ModDll = 'GrandTheftBarfenders.dll'
$FixDll = 'Il2CppInteropUnity6000Fix.dll'
$PackageRoot = Split-Path $PSScriptRoot -Parent

Write-Title 'Grand Theft Barfenders - Installation'

$game = Resolve-GameDir $GameDir
Assert-GameClosed $game

# 1. MelonLoader: check first, install only when needed.
$mlDll = Join-Path $game 'MelonLoader\net6\MelonLoader.dll'
$disabled = Join-Path $game 'version.dll.disabled'
$proxy = Join-Path $game 'version.dll'
if (-not (Test-Path $proxy) -and (Test-Path $disabled)) {
    Rename-Item $disabled 'version.dll'
    Write-Step 'MelonLoader was disabled - turned it back on.'
}

$installed = if (Test-Path $mlDll) { (Get-Item $mlDll).VersionInfo.ProductVersion } else { $null }
if ((Test-Path $proxy) -and $installed -and $installed.StartsWith($MelonLoaderVersion)) {
    Write-Step "MelonLoader $MelonLoaderVersion is already installed - nothing to do."
} else {
    if ($installed) {
        Write-Warn "MelonLoader $installed found, this mod needs $MelonLoaderVersion - updating it."
    } else {
        Write-Step 'MelonLoader is not installed yet - setting it up.'
    }
    Write-Step "Downloading MelonLoader $MelonLoaderVersion from GitHub ..."
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('GrandTheftBarfenders-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temp | Out-Null
    try {
        $zip = Join-Path $temp 'MelonLoader.x64.zip'
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $MelonLoaderUrl -OutFile $zip -UseBasicParsing
        $hash = (Get-FileHash $zip -Algorithm SHA256).Hash
        if ($hash -ne $MelonLoaderSha256) { throw "The download is not the expected file (checksum $hash). Aborted, nothing was changed." }
        Write-Step 'Download verified (checksum matches).'

        $extract = Join-Path $temp 'x'
        Expand-Archive -Path $zip -DestinationPath $extract
        Copy-Item -Path (Join-Path $extract '*') -Destination $game -Recurse -Force
        Write-Step "MelonLoader $MelonLoaderVersion installed."
    } finally {
        Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 2. Folder link: MelonLoader looks for "Barfender_Data", the game uses "Data".
$link = Join-Path $game 'Barfender_Data'
$data = Join-Path $game 'Data'
if (Test-Path $link) {
    Write-Step 'Folder link Barfender_Data already exists.'
} elseif (Test-Path $data) {
    New-Item -ItemType Junction -Path $link -Target $data | Out-Null
    Write-Step 'Created folder link Barfender_Data -> Data.'
} else {
    Write-Warn 'No "Data" folder found in the game folder - skipped the folder link.'
}

# 3. Fix plugin: shared with other Barfender mods. Only copy it if it's missing or ours is newer.
$plugins = Join-Path $game 'Plugins'
New-Item -ItemType Directory -Force -Path $plugins | Out-Null
$ourFix = Join-Path $PackageRoot "files\Plugins\$FixDll"
$theirFix = Join-Path $plugins $FixDll
if (-not (Test-Path $theirFix)) {
    Copy-Item $ourFix $theirFix
    Write-Step 'Installed the fix plugin.'
} elseif ((Get-DllVersion $ourFix) -gt (Get-DllVersion $theirFix)) {
    Copy-Item $ourFix $theirFix -Force
    Write-Step 'Updated the fix plugin.'
} else {
    Write-Step 'Fix plugin is already installed (shared with your other mods).'
}

# 4. The mod itself.
$mods = Join-Path $game 'Mods'
New-Item -ItemType Directory -Force -Path $mods | Out-Null
Copy-Item (Join-Path $PackageRoot "files\Mods\$ModDll") $mods -Force
Write-Step 'Installed Grand Theft Barfenders.'

$others = Get-OtherMods $game $ModDll
if ($others.Count -gt 0) { Write-Step ("Other mods kept: " + (($others | ForEach-Object Name) -join ', ')) }

Write-Host ''
Write-Host 'Done!' -ForegroundColor Green
Write-Host ''
Write-Host 'On the FIRST start of Barfender with MelonLoader:'
Write-Host ' - An extra black console window (MelonLoader) opens. That is normal.'
Write-Host ' - The first start takes 1-3 minutes while MelonLoader prepares the game.'
Write-Host ''
Write-Host 'In game: Trevor Philips stands behind your bar. Look at him and press E to buy.'
Write-Host 'Draw: 1 = pistol, 2 = bat. Left mouse = shoot / hit.'
