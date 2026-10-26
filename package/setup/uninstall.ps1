# Removes "Grand Theft Barfenders". The shared fix plugin and MelonLoader stay as long as other mods
# (e.g. Barfender Staff Roles) are installed. Game files are never touched.
# -Yes answers every question with yes (for tests).
param([string]$GameDir, [switch]$Yes)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
if ($Yes) { function Ask-YesNo([string]$question) { Write-Host "$question (y/n) y"; return $true } }

$ModDll = 'GrandTheftBarfenders.dll'

Write-Title 'Grand Theft Barfenders - Uninstall'

$game = Resolve-GameDir $GameDir
Assert-GameClosed $game

$path = Join-Path $game "Mods\$ModDll"
if (Test-Path $path) { Remove-Item $path -Force; Write-Step "Removed Mods\$ModDll." }

$others = Get-OtherMods $game $ModDll
if ($others.Count -gt 0) {
    Write-Step ("Other mods still installed: " + (($others | ForEach-Object Name) -join ', '))
    Write-Step 'MelonLoader and the fix plugin stay, they still need them.'
} else {
    $fix = Join-Path $game 'Plugins\Il2CppInteropUnity6000Fix.dll'
    if (Test-Path $fix) { Remove-Item $fix -Force; Write-Step 'Removed Plugins\Il2CppInteropUnity6000Fix.dll.' }

    if (Ask-YesNo 'No other mods left. Remove MelonLoader completely?') {
        $proxy = Join-Path $game 'version.dll'
        if (Test-Path $proxy) { Remove-Item $proxy -Force }
        $disabled = Join-Path $game 'version.dll.disabled'
        if (Test-Path $disabled) { Remove-Item $disabled -Force }

        $ml = Join-Path $game 'MelonLoader'
        if (Test-Path $ml) { Remove-Item $ml -Recurse -Force }

        # Only remove empty mod folders; UserData only holds MelonLoader/mod settings.
        foreach ($folder in 'Mods', 'Plugins', 'UserLibs') {
            $p = Join-Path $game $folder
            if ((Test-Path $p) -and -not (Get-ChildItem $p -Force)) { Remove-Item $p -Force }
        }
        $userData = Join-Path $game 'UserData'
        if (Test-Path $userData) { Remove-Item $userData -Recurse -Force }

        # Folder link Barfender_Data: ONLY release the link (rmdir), never delete its contents -
        # they belong to the game.
        $link = Join-Path $game 'Barfender_Data'
        if (Test-Path $link) {
            $item = Get-Item $link -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                cmd /c rmdir "$link" | Out-Null
                Write-Step 'Released the folder link Barfender_Data.'
            } else {
                Write-Warn 'Barfender_Data is a real folder (not a link) - left untouched.'
            }
        }
        Write-Step 'Removed MelonLoader.'
    }
}

Write-Host ''
Write-Host 'Done. Barfender now starts without Grand Theft Barfenders.' -ForegroundColor Green
Write-Host 'Note: Trevor Philips stays in savegames as a normal (idle) character.'
