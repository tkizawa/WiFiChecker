# WiFiChecker インストーラ / スタンドアロンパッケージビルドスクリプト (Rules compliant)
$ErrorActionPreference = "Stop"

$version = "1.0.0.0"
$installerDir = Join-Path $PSScriptRoot "Installer"
if (-not (Test-Path $installerDir)) {
    New-Item -ItemType Directory -Path $installerDir | Out-Null
}

Write-Host "=== WiFiChecker スタンドアロンインストーラ/パッケージの作成開始 (v$version) ==="

# 1. Arm64 スタンドアロン exe ビルド
Write-Host "Building Arm64 Self-Contained Standalone Executable..."
dotnet publish -c Release -r win-arm64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:AssemblyVersion=$version /p:FileVersion=$version

$arm64PublishDir = Join-Path $PSScriptRoot "bin\Release\net10.0-windows\win-arm64\publish"
$arm64ExeTarget = Join-Path $installerDir "WiFiChecker_Setup_${version}_Arm64.exe"
Copy-Item (Join-Path $arm64PublishDir "WiFiChecker.exe") $arm64ExeTarget -Force
Write-Host "Created: $arm64ExeTarget"

$arm64Zip = Join-Path $installerDir "WiFiChecker_v${version}_Arm64.zip"
if (Test-Path $arm64Zip) { Remove-Item $arm64Zip -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$arm64PublishDir\*" -DestinationPath $arm64Zip -Force
Write-Host "Created: $arm64Zip"

# 2. x64 スタンドアロン exe ビルド
Write-Host "Building x64 Self-Contained Standalone Executable..."
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:AssemblyVersion=$version /p:FileVersion=$version

$x64PublishDir = Join-Path $PSScriptRoot "bin\Release\net10.0-windows\win-x64\publish"
$x64ExeTarget = Join-Path $installerDir "WiFiChecker_Setup_${version}_x64.exe"
Copy-Item (Join-Path $x64PublishDir "WiFiChecker.exe") $x64ExeTarget -Force
Write-Host "Created: $x64ExeTarget"

$x64Zip = Join-Path $installerDir "WiFiChecker_v${version}_x64.zip"
if (Test-Path $x64Zip) { Remove-Item $x64Zip -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$x64PublishDir\*" -DestinationPath $x64Zip -Force
Write-Host "Created: $x64Zip"

# 3. 実行環境に合わせたデフォルトインストーラ (Setup.exe)
$osArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
if ($osArch -eq "Arm64") {
    Copy-Item $arm64ExeTarget (Join-Path $installerDir "WiFiChecker_Setup_${version}.exe") -Force
} else {
    Copy-Item $x64ExeTarget (Join-Path $installerDir "WiFiChecker_Setup_${version}.exe") -Force
}

Write-Host "=== インストーラ/スタンドアロンパッケージの作成が完了しました ==="
