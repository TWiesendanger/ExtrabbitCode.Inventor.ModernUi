@echo off
REM Regenerates the per-control documentation screenshots (light + dark) from the Gallery.
REM Run from anywhere; paths are resolved relative to this script.
setlocal
set SCRIPT_DIR=%~dp0
dotnet run --project "%SCRIPT_DIR%..\..\ExtrabbitCode.Inventor.ModernUi.Gallery\ExtrabbitCode.Inventor.ModernUi.Gallery.csproj" -c Release -- --shoot-docs "%SCRIPT_DIR%..\public\images\controls"
endlocal
