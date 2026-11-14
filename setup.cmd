@echo off
rem Baut dist\Elas3DView-Setup.exe (inklusive frischem bin\Elas3DView.exe).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1"
echo.
pause
