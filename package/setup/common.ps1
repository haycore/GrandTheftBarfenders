# Shared helpers for install.ps1 and uninstall.ps1.

function Write-Title([string]$text) {
    Write-Host ''
    Write-Host "=== $text ===" -ForegroundColor Cyan
    Write-Host ''
}

function Write-Step([string]$text) { Write-Host " * $text" }
function Write-Warn([string]$text) { Write-Host " ! $text" -ForegroundColor Yellow }

function Ask-YesNo([string]$question) {
    $answer = Read-Host "$question (y/n)"
    return $answer -match '^(y|yes|j|ja)$'
}

function Test-GameDir([string]$dir) {
    return $dir -and (Test-Path (Join-Path $dir 'Barfender.exe'))
}

# Find Barfender through the Steam libraries; otherwise let the user pick the folder.
function Resolve-GameDir([string]$given) {
    if ($given) {
        if (Test-GameDir $given) { return (Resolve-Path $given).Path }
        throw "There is no Barfender.exe in '$given'."
    }

    $libraries = @()
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        try {
            $props = Get-ItemProperty $key -ErrorAction Stop
            foreach ($name in 'SteamPath', 'InstallPath') { if ($props.$name) { $libraries += ($props.$name -replace '/', '\') } }
        } catch { }
    }
    foreach ($steam in @($libraries)) {
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($match in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $libraries += ($match.Groups[1].Value -replace '\\\\', '\')
            }
        }
    }
    foreach ($library in ($libraries | Select-Object -Unique)) {
        $candidate = Join-Path $library 'steamapps\common\Barfender'
        if (Test-GameDir $candidate) {
            Write-Step "Found Barfender: $candidate"
            return $candidate
        }
    }

    Write-Warn 'Could not find Barfender automatically.'
    Write-Host '   In Steam: right-click Barfender > Manage > Browse local files.'
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Select the Barfender game folder (the one containing Barfender.exe)'
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK -and (Test-GameDir $dialog.SelectedPath)) {
        return $dialog.SelectedPath
    }
    throw 'No valid Barfender folder selected.'
}

function Assert-GameClosed([string]$game) {
    $exe = (Join-Path $game 'Barfender.exe').ToLowerInvariant()
    $running = Get-Process Barfender -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.ToLowerInvariant() -eq $exe }
    if ($running) { throw 'Barfender is still running. Please close the game and start the installer again.' }
}

# File version of a DLL; 0.0 if it has none.
function Get-DllVersion([string]$path) {
    try { return [version](Get-Item $path).VersionInfo.FileVersion } catch { return [version]'0.0' }
}

# Other MelonLoader mods in the game folder (e.g. Barfender Staff Roles), without the given one.
function Get-OtherMods([string]$game, [string]$ownDll) {
    $mods = Join-Path $game 'Mods'
    if (-not (Test-Path $mods)) { return @() }
    return @(Get-ChildItem $mods -Filter '*.dll' -File | Where-Object { $_.Name -ne $ownDll })
}
