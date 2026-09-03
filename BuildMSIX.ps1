<#
.SYNOPSIS
    Microsoft Store 登録用 MSIX / MSIXBundle パッケージ生成スクリプト
.DESCRIPTION
    WiFiChecker の x64 および Arm64 向け MSIX パッケージ、
    ならびにストア提出用の MSIXBundle を生成します。
#>

[CmdletBinding()]
param(
    [string]$PackageIdentityName,
    [string]$PublisherId,
    [string]$PublisherDisplayName,
    [string]$DisplayName,
    [string[]]$Architectures = @("x64", "arm64"),
    [switch]$SkipBundle
)

$ErrorActionPreference = "Stop"

$rootDir = $PSScriptRoot
$msixOutDir = Join-Path $rootDir "MSIX"
$assetsSourceDir = Join-Path $rootDir "Assets"
$csprojPath = Join-Path $rootDir "WiFiChecker.csproj"
$configPath = Join-Path $rootDir "StoreConfig.json"
$manifestTemplatePath = Join-Path $rootDir "Package.appxmanifest.template"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " WiFiChecker - Microsoft Store (MSIX) Package Build" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. StoreConfig.json の読み込み
$config = $null
if (Test-Path $configPath) {
    try {
        $configJson = [System.IO.File]::ReadAllText($configPath, [System.Text.Encoding]::UTF8)
        $config = ConvertFrom-Json $configJson
    } catch {
        Write-Warning "StoreConfig.json の読み込みに失敗しました: $_"
    }
}

if (-not $PackageIdentityName) {
    if ($config -and $config.PackageIdentityName) { $PackageIdentityName = $config.PackageIdentityName } else { $PackageIdentityName = "WiFiChecker" }
}
if (-not $PublisherId) {
    if ($config -and $config.PublisherId) { $PublisherId = $config.PublisherId } else { $PublisherId = "CN=00000000-0000-0000-0000-000000000000" }
}
if (-not $PublisherDisplayName) {
    if ($config -and $config.PublisherDisplayName) { $PublisherDisplayName = $config.PublisherDisplayName } else { $PublisherDisplayName = "WiFiChecker Publisher" }
}
if (-not $DisplayName) {
    if ($config -and $config.DisplayName) { $DisplayName = $config.DisplayName } else { $DisplayName = "Wi-Fi Checker" }
}
$Description = if ($config -and $config.Description) { $config.Description } else { "Wi-Fi 接続状況・電波強度・リンク速度チェッカー" }

# 2. csproj からバージョン番号を取得
[xml]$projXml = Get-Content $csprojPath
$version = $projXml.Project.PropertyGroup.AssemblyVersion
if (-not $version) {
    $version = $projXml.Project.PropertyGroup.FileVersion
}
if (-not $version) {
    $version = "1.0.0.0"
}
# 4桁形式を保証
while (($version.Split('.')).Count -lt 4) {
    $version = $version + ".0"
}

Write-Host "Package Information:" -ForegroundColor Green
Write-Host ("  Name:          {0}" -f $PackageIdentityName)
Write-Host ("  Version:       {0}" -f $version)
Write-Host ("  Publisher:     {0}" -f $PublisherId)
Write-Host ("  PublisherName: {0}" -f $PublisherDisplayName)
Write-Host ("  DisplayName:   {0}" -f $DisplayName)
Write-Host ("  Targets:       {0}" -f ($Architectures -join ', '))
Write-Host ""

# 3. MakeAppx.exe の検出
$makeAppxExe = $null
$candidates = @()

$nugetTools = Get-ChildItem -Path "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools\*\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue
if ($nugetTools) { $candidates += $nugetTools }

$programFilesX86 = [System.Environment]::GetEnvironmentVariable("ProgramFiles(x86)")
if ($programFilesX86) {
    $kitTools = Get-ChildItem -Path "$programFilesX86\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue
    if ($kitTools) { $candidates += $kitTools }
}

$pathTool = Get-Command makeappx.exe -ErrorAction SilentlyContinue
if ($pathTool) { $candidates += $pathTool.Source }

if ($candidates.Count -gt 0) {
    $sorted = $candidates | Sort-Object LastWriteTime -Descending
    $makeAppxExe = ($sorted | Select-Object -First 1).FullName
    if (-not $makeAppxExe) {
        $makeAppxExe = ($sorted | Select-Object -First 1)
    }
}

if (-not $makeAppxExe -or -not (Test-Path $makeAppxExe)) {
    throw "makeappx.exe が見つかりませんでした。Windows SDK または Microsoft.Windows.SDK.BuildTools を確認してください。"
}
Write-Host ("Found MakeAppx: {0}" -f $makeAppxExe) -ForegroundColor DarkGray
Write-Host ""

# 4. 出力フォルダ準備
if (-not (Test-Path $msixOutDir)) {
    New-Item -ItemType Directory -Path $msixOutDir -Force | Out-Null
}

$stageDir = Join-Path $msixOutDir "_staging"
if (Test-Path $stageDir) {
    Remove-Item $stageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null

$createdPackages = @()

# 5. 各アーキテクチャのビルド & パッケージング
foreach ($arch in $Architectures) {
    Write-Host "--------------------------------------------------" -ForegroundColor Cyan
    Write-Host ("Building MSIX for [{0}]..." -f $arch) -ForegroundColor Cyan
    Write-Host "--------------------------------------------------" -ForegroundColor Cyan

    $archStageDir = Join-Path $stageDir $arch
    $msixFileName = ("WiFiChecker_{0}_{1}.msix" -f $version, $arch)
    $msixFilePath = Join-Path $msixOutDir $msixFileName

    if (Test-Path $msixFilePath) {
        Remove-Item $msixFilePath -Force
    }

    # dotnet publish (self-contained)
    Write-Host "Publishing self-contained .NET binary..." -ForegroundColor Gray
    $publishArgs = @(
        "publish",
        $csprojPath,
        "-c", "Release",
        "-r", "win-$arch",
        "--self-contained", "true",
        "-o", $archStageDir
    )
    & dotnet $publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw ("dotnet publish failed for {0}." -f $arch)
    }

    # デバッグシンボルのクリーンアップ
    Get-ChildItem -Path $archStageDir -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

    # Assets コピー
    $destAssetsDir = Join-Path $archStageDir "Assets"
    if (Test-Path $destAssetsDir) { Remove-Item $destAssetsDir -Recurse -Force }
    Copy-Item -Path $assetsSourceDir -Destination $destAssetsDir -Recurse -Force

    # AppxManifest.xml の生成
    $manifestContent = [System.IO.File]::ReadAllText($manifestTemplatePath, [System.Text.Encoding]::UTF8)
    $manifestContent = $manifestContent.Replace("__PACKAGE_IDENTITY_NAME__", $PackageIdentityName)
    $manifestContent = $manifestContent.Replace("__PUBLISHER_ID__", $PublisherId)
    $manifestContent = $manifestContent.Replace("__PACKAGE_VERSION__", $version)
    $manifestContent = $manifestContent.Replace("__PROCESSOR_ARCHITECTURE__", $arch)
    $manifestContent = $manifestContent.Replace("__DISPLAY_NAME__", $DisplayName)
    $manifestContent = $manifestContent.Replace("__PUBLISHER_DISPLAY_NAME__", $PublisherDisplayName)
    $manifestContent = $manifestContent.Replace("__DESCRIPTION__", $Description)

    $manifestDestPath = Join-Path $archStageDir "AppxManifest.xml"
    [System.IO.File]::WriteAllText($manifestDestPath, $manifestContent, [System.Text.Encoding]::UTF8)

    # makeappx pack
    Write-Host "Packaging with MakeAppx..." -ForegroundColor Gray
    & $makeAppxExe pack /v /h SHA256 /d $archStageDir /p $msixFilePath /o
    if ($LASTEXITCODE -ne 0) {
        throw ("makeappx pack failed for {0}." -f $arch)
    }

    $fileItem = Get-Item $msixFilePath
    $sizeMb = [math]::Round($fileItem.Length / 1MB, 2)
    Write-Host ("Successfully generated: {0} ({1} MB)" -f $msixFileName, $sizeMb) -ForegroundColor Green
    $createdPackages += $msixFilePath
}

# 6. MSIXBundle の作成
if (-not $SkipBundle -and ($createdPackages.Count -ge 2)) {
    Write-Host ""
    Write-Host "--------------------------------------------------" -ForegroundColor Cyan
    Write-Host "Creating MSIXBundle (x64 + Arm64)..." -ForegroundColor Cyan
    Write-Host "--------------------------------------------------" -ForegroundColor Cyan

    $bundleFileName = ("WiFiChecker_{0}_x64_arm64.msixbundle" -f $version)
    $bundleFilePath = Join-Path $msixOutDir $bundleFileName

    if (Test-Path $bundleFilePath) {
        Remove-Item $bundleFilePath -Force
    }

    $bundleStageDir = Join-Path $stageDir "bundle"
    if (Test-Path $bundleStageDir) { Remove-Item $bundleStageDir -Recurse -Force }
    New-Item -ItemType Directory -Path $bundleStageDir -Force | Out-Null

    foreach ($pkg in $createdPackages) {
        Copy-Item -Path $pkg -Destination $bundleStageDir -Force
    }

    & $makeAppxExe bundle /v /o /bv $version /d $bundleStageDir /p $bundleFilePath
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "MSIXBundle の生成で警告またはエラーが発生しました。"
    } else {
        $bundleItem = Get-Item $bundleFilePath
        $bundleMb = [math]::Round($bundleItem.Length / 1MB, 2)
        Write-Host ("Successfully generated: {0} ({1} MB)" -f $bundleFileName, $bundleMb) -ForegroundColor Green
    }
}

# 7. クリーンアップ
if (Test-Path $stageDir) {
    Remove-Item $stageDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host " Build Completed Successfully!" -ForegroundColor Green
Write-Host (" Output Directory: {0}" -f $msixOutDir) -ForegroundColor Green
Write-Host " Files:" -ForegroundColor Green
Get-ChildItem -Path $msixOutDir -Filter "*.msix*" | ForEach-Object {
    $sizeMb = [math]::Round($_.Length / 1MB, 2)
    Write-Host ("  - {0} ({1} MB)" -f $_.Name, $sizeMb) -ForegroundColor White
}
Write-Host "==================================================" -ForegroundColor Green

