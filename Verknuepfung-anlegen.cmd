@echo off
rem Legt Verknuepfungen auf dem Desktop und im Startmenue an.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0create-shortcut.ps1"
echo.
pause
