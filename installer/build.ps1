<#
.SYNOPSIS
    Builds the Modern UI setup.exe (standalone Gallery app + Inventor add-in) with Inno Setup.

.DESCRIPTION
    1. Publishes the Gallery as a self-contained win-x64 app (no .NET prerequisite for end users).
    2. Builds the Inventor add-in payload (framework-dependent; Inventor hosts the runtime).
    3. Compiles installer\ModernUi.iss with Inno Setup -> installer\Output\*.exe.

    Requires Inno Setup 6 (https://jrsoftware.org/isinfo.php) and the .NET 8 SDK.

.EXAMPLE
    pwsh installer\build.ps1 -Version 1.2.3
#>
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$root = Split-Path $here -Parent
$publish = Join-Path $here "publish"

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

Write-Host "==> Publishing standalone Gallery (self-contained win-x64)..." -ForegroundColor Cyan
dotnet publish "$root\ExtrabbitCode.Inventor.ModernUi.Gallery\ExtrabbitCode.Inventor.ModernUi.Gallery.csproj" `
    -c Release -r win-x64 --self-contained true -p:DeployToInventor=false `
    -o "$publish\app"
if ($LASTEXITCODE -ne 0) { throw "Gallery publish failed." }

Write-Host "==> Building Inventor add-in payload..." -ForegroundColor Cyan
dotnet build "$root\ExtrabbitCode.Inventor.ModernUi.Demo.AddIn\ExtrabbitCode.Inventor.ModernUi.Demo.AddIn.csproj" `
    -c Release -p:DeployToInventor=false `
    -o "$publish\addin"
if ($LASTEXITCODE -ne 0) { throw "Add-in build failed." }

$iscc = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    throw "Inno Setup 6 not found at '$iscc'. Install it from https://jrsoftware.org/isinfo.php"
}

Write-Host "==> Compiling installer (v$Version)..." -ForegroundColor Cyan
& $iscc "/DAppVersion=$Version" "$here\ModernUi.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }

Write-Host "==> Done. Setup is in $here\Output" -ForegroundColor Green
