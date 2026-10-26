@echo off
rem Removes "Grand Theft Barfenders" (other Barfender mods stay installed).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup\uninstall.ps1"
if errorlevel 1 echo. & echo Uninstalling failed - see the message above.
echo.
pause
