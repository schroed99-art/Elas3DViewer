@echo off
rem Baut bin\Elas3DView.exe mit dem C#-Compiler, der zu Windows gehoert.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
echo.
pause
