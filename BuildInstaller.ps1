# WiFiChecker インストーラ / スタンドアロンパッケージビルドスクリプト (Rules compliant)
$ErrorActionPreference = "Stop"

$version = "1.0.0.0"
$installerDir = Join-Path $PSScriptRoot "Installer"
$setupProj = Join-Path $PSScriptRoot "Setup\Setup.csproj"
$mainProj = Join-Path $PSScriptRoot "WiFiChecker.csproj"
$payloadZip = Join-Path $PSScriptRoot "Setup\payload.zip"

if (-not (Test-Path $installerDir)) {
    New-Item -ItemType Directory -Path $installerDir | Out-Null
}

Write-Host "=== WiFiChecker セットアップインストーラのビルド開始 (v$version) ==="

# ==========================================
# 1. Arm64 ビルド
# ==========================================
Write-Host "--- [1/2] Arm64 インストーラ作成 ---"

# (1) メインアプリの publish
Write-Host "1-1. メインアプリケーション (win-arm64) を発行中..."
dotnet publish $mainProj -c Release -r win-arm64 --self-contained true /p:PublishSingleFile=false /p:AssemblyVersion=$version /p:FileVersion=$version

$arm64MainPublish = Join-Path $PSScriptRoot "bin\Release\net10.0-windows\win-arm64\publish"

# (2) zip パッケージ作成 (配布用zip)
$arm64Zip = Join-Path $installerDir "WiFiChecker_v${version}_Arm64.zip"
if (Test-Path $arm64Zip) { Remove-Item $arm64Zip -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$arm64MainPublish\*" -DestinationPath $arm64Zip -Force
Write-Host "  作成: $arm64Zip"

# (3) インストーラ埋め込み用 payload.zip の作成
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$arm64MainPublish\*" -DestinationPath $payloadZip -Force

# (4) Setup (インストーラ本体) を単一 exe として publish
Write-Host "1-2. セットアップインストーラ (win-arm64) を単一EXEとして発行中..."
dotnet publish $setupProj -c Release -r win-arm64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:AssemblyVersion=$version /p:FileVersion=$version

$arm64SetupPublish = Join-Path $PSScriptRoot "Setup\bin\Release\net10.0-windows\win-arm64\publish"
$arm64ExeTarget = Join-Path $installerDir "WiFiChecker_Setup_${version}_Arm64.exe"
Copy-Item (Join-Path $arm64SetupPublish "WiFiCheckerSetup.exe") $arm64ExeTarget -Force
Write-Host "  作成: $arm64ExeTarget"

# ==========================================
# 2. x64 ビルド
# ==========================================
Write-Host "--- [2/2] x64 インストーラ作成 ---"

# (1) メインアプリの publish
Write-Host "2-1. メインアプリケーション (win-x64) を発行中..."
dotnet publish $mainProj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false /p:AssemblyVersion=$version /p:FileVersion=$version

$x64MainPublish = Join-Path $PSScriptRoot "bin\Release\net10.0-windows\win-x64\publish"

# (2) zip パッケージ作成 (配布用zip)
$x64Zip = Join-Path $installerDir "WiFiChecker_v${version}_x64.zip"
if (Test-Path $x64Zip) { Remove-Item $x64Zip -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$x64MainPublish\*" -DestinationPath $x64Zip -Force
Write-Host "  作成: $x64Zip"

# (3) インストーラ埋め込み用 payload.zip の作成
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$x64MainPublish\*" -DestinationPath $payloadZip -Force

# (4) Setup (インストーラ本体) を単一 exe として publish
Write-Host "2-2. セットアップインストーラ (win-x64) を単一EXEとして発行中..."
dotnet publish $setupProj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:AssemblyVersion=$version /p:FileVersion=$version

$x64SetupPublish = Join-Path $PSScriptRoot "Setup\bin\Release\net10.0-windows\win-x64\publish"
$x64ExeTarget = Join-Path $installerDir "WiFiChecker_Setup_${version}_x64.exe"
Copy-Item (Join-Path $x64SetupPublish "WiFiCheckerSetup.exe") $x64ExeTarget -Force
Write-Host "  作成: $x64ExeTarget"

# ==========================================
# 3. 実行環境に合わせたデフォルトインストーラ (Setup.exe)
# ==========================================
$osArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
if ($osArch -eq "Arm64") {
    Copy-Item $arm64ExeTarget (Join-Path $installerDir "WiFiChecker_Setup_${version}.exe") -Force
} else {
    Copy-Item $x64ExeTarget (Join-Path $installerDir "WiFiChecker_Setup_${version}.exe") -Force
}

# 一時ファイルのクリーンアップ
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force -ErrorAction SilentlyContinue }

Write-Host "=== セットアップインストーラの作成が正常に完了しました ==="
