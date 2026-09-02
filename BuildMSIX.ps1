# BuildMSIX.ps1
$ErrorActionPreference = "Stop"

$outMsixDir = "c:\Dev\WiFiChecker\MSIX"

if (-not (Test-Path $outMsixDir)) {
    New-Item -ItemType Directory -Path $outMsixDir -Force | Out-Null
}

Write-Host "Building x64 MSIX package..."
dotnet publish -c Release -r win-x64 --self-contained true

$x64Src = "c:\Dev\WiFiChecker\bin\Release\net10.0-windows\win-x64\publish"
$x64Dst = "c:\Dev\WiFiChecker\MSIX\WiFiChecker_x64"
if (Test-Path $x64Dst) { Remove-Item $x64Dst -Recurse -Force }
Copy-Item $x64Src $x64Dst -Recurse -Force

$x64ZipTemp = "c:\Dev\WiFiChecker\MSIX\WiFiChecker_1.0.0.0_x64.zip"
$x64MsixFile = "c:\Dev\WiFiChecker\MSIX\WiFiChecker_1.0.0.0_x64.msix"
if (Test-Path $x64ZipTemp) { Remove-Item $x64ZipTemp -Force -ErrorAction SilentlyContinue }
if (Test-Path $x64MsixFile) { Remove-Item $x64MsixFile -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$x64Dst\*" -DestinationPath $x64ZipTemp -Force
Rename-Item -Path $x64ZipTemp -NewName "WiFiChecker_1.0.0.0_x64.msix" -Force
Write-Host "Created: $x64MsixFile"

Write-Host "Building Arm64 MSIX package..."
dotnet publish -c Release -r win-arm64 --self-contained true

$arm64Src = "c:\Dev\WiFiChecker\bin\Release\net10.0-windows\win-arm64\publish"
$arm64Dst = "c:\Dev\WiFiChecker\MSIX\WiFiChecker_Arm64"
if (Test-Path $arm64Dst) { Remove-Item $arm64Dst -Recurse -Force }
Copy-Item $arm64Src $arm64Dst -Recurse -Force

$arm64ZipTemp = "c:\Dev\WiFiChecker\MSIX\WiFiChecker_1.0.0.0_Arm64.zip"
$arm64MsixFile = "c:\Dev\WiFiChecker\MSIX\WiFiChecker_1.0.0.0_Arm64.msix"
if (Test-Path $arm64ZipTemp) { Remove-Item $arm64ZipTemp -Force -ErrorAction SilentlyContinue }
if (Test-Path $arm64MsixFile) { Remove-Item $arm64MsixFile -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$arm64Dst\*" -DestinationPath $arm64ZipTemp -Force
Rename-Item -Path $arm64ZipTemp -NewName "WiFiChecker_1.0.0.0_Arm64.msix" -Force
Write-Host "Created: $arm64File"

Write-Host "=== MSIX Build Completed ==="
