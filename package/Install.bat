@echo off
rem Installs "Grand Theft Barfenders" (see README.txt).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup\install.ps1"
if errorlevel 1 echo. & echo The installation failed - see the message above.
echo.
pause
