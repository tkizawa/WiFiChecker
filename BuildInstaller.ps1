# WiFiChecker インストーラビルドスクリプト (Rules compliant)
$ErrorActionPreference = "Stop"

$version = "1.0.0.0"
$installerDir = Join-Path $PSScriptRoot "Installer"
$setupProj = Join-Path $PSScriptRoot "Setup\Setup.csproj"
$mainProj = Join-Path $PSScriptRoot "WiFiChecker.csproj"
$payloadZip = Join-Path $PSScriptRoot "Setup\payload.zip"

if (-not (Test-Path $installerDir)) {
    New-Item -ItemType Directory -Path $installerDir | Out-Null
} else {
    # 古いビルド成果物をクリーンアップ
    Get-ChildItem -Path $installerDir -File | Remove-Item -Force
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

# (2) インストーラ埋め込み用 payload.zip の作成
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$arm64MainPublish\*" -DestinationPath $payloadZip -Force

# (3) Setup (インストーラ本体) を単一 exe として publish
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

# (2) インストーラ埋め込み用 payload.zip の作成
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$x64MainPublish\*" -DestinationPath $payloadZip -Force

# (3) Setup (インストーラ本体) を単一 exe として publish
Write-Host "2-2. セットアップインストーラ (win-x64) を単一EXEとして発行中..."
dotnet publish $setupProj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:AssemblyVersion=$version /p:FileVersion=$version

$x64SetupPublish = Join-Path $PSScriptRoot "Setup\bin\Release\net10.0-windows\win-x64\publish"
$x64ExeTarget = Join-Path $installerDir "WiFiChecker_Setup_${version}_x64.exe"
Copy-Item (Join-Path $x64SetupPublish "WiFiCheckerSetup.exe") $x64ExeTarget -Force
Write-Host "  作成: $x64ExeTarget"

# 一時ファイルのクリーンアップ
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force -ErrorAction SilentlyContinue }

Write-Host "=== セットアップインストーラの作成が正常に完了しました ==="
