# WiFiChecker インストーラ / パッケージビルドスクリプト (Rules compliant)
$ErrorActionPreference = "Stop"

$version = "1.0.0.0"
$installerDir = Join-Path $PSScriptRoot "Installer"
if (-not (Test-Path $installerDir)) {
    New-Item -ItemType Directory -Path $installerDir | Out-Null
}

Write-Host "=== WiFiChecker スタンドアロンパッケージの作成開始 (v$version) ==="

# x64 ビルド
Write-Host "Building x64 Self-Contained Package..."
dotnet publish -c Release -r win-x64 --self-contained true /p:AssemblyVersion=$version /p:FileVersion=$version

$x64Source = Join-Path $PSScriptRoot "bin\Release\net10.0-windows\win-x64\publish"
$x64Target = Join-Path $installerDir "WiFiChecker_v${version}_x64"
if (Test-Path $x64Target) { Remove-Item $x64Target -Recurse -Force }
Copy-Item $x64Source $x64Target -Recurse -Force

$x64Zip = Join-Path $installerDir "WiFiChecker_Setup_1.0.0.0_x64.zip"
if (Test-Path $x64Zip) { Remove-Item $x64Zip -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$x64Target\*" -DestinationPath $x64Zip -Force
Write-Host "Created: $x64Zip"

# Arm64 ビルド
Write-Host "Building Arm64 Self-Contained Package..."
dotnet publish -c Release -r win-arm64 --self-contained true /p:AssemblyVersion=$version /p:FileVersion=$version

$arm64Source = Join-Path $PSScriptRoot "bin\Release\net10.0-windows\win-arm64\publish"
$arm64Target = Join-Path $installerDir "WiFiChecker_v${version}_Arm64"
if (Test-Path $arm64Target) { Remove-Item $arm64Target -Recurse -Force }
Copy-Item $arm64Source $arm64Target -Recurse -Force

$arm64Zip = Join-Path $installerDir "WiFiChecker_Setup_1.0.0.0_Arm64.zip"
if (Test-Path $arm64Zip) { Remove-Item $arm64Zip -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$arm64Target\*" -DestinationPath $arm64Zip -Force
Write-Host "Created: $arm64Zip"

# 代表 exe コピー
Copy-Item "$x64Target\WiFiChecker.exe" (Join-Path $installerDir "WiFiChecker_Setup_1.0.0.0.exe") -Force

Write-Host "=== インストーラ/スタンドアロンパッケージの作成が完了しました ==="
