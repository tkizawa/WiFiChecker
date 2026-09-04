# Wi-Fi Checker (WiFiChecker)

![Wi-Fi Checker](Assets/Wide310x150Logo.png)

Windows PC 上で接続中の Wi-Fi アクセスポイント情報（電波強度・リンク速度・周波数帯・チャンネルなど）をリアルタイムにモニタリング・可視化するモダンなデスクトップアプリケーションです。

📖 **詳しい使い方・用語解説は [MANUAL.md (操作説明書)](MANUAL.md) をご覧ください。**

---

## 主な機能

- **電波強度（RSSI）のリアルタイム表示**
  - 現在の電波強度をパーセンテージおよびカラープログレスバーで視覚的に表示します。
- **リンク速度の監視**
  - 受信速度（Rx）および送信速度（Tx）のリンク速度（Mbps）を表示します。
- **詳細な Wi-Fi 接続情報**
  - SSID、BSSID（MACアドレス）、ネットワーク規格（802.11ax/ac/n 等）、帯域幅（2.4GHz / 5GHz / 6GHz）、チャンネル番号を表示。
- **CSV ロギング機能**
  - 電波強度や通信速度の推移をタイムスタンプ付きの CSV ファイルに自動記録。
- **自動更新機能**
  - 1秒〜10秒間隔での定期自動リフレッシュ、またはワンクリックでの即時手動更新。
- **多言語対応 (Multilingual)**
  - 日本語（ja-JP）および英語（en-US）に対応。Windows の表示言語設定に応じて自動切替され、UIから手動で切り替えることも可能です。
- **ウィンドウ状態・設定の自動保存**
  - 終了時のウィンドウ位置・サイズ、自動更新間隔、選択言語を保存し、次回起動時に自動復元します。
- **アーキテクチャ最適化**
  - 64ビット（x64）および ARM64（Surface Pro 等）ネイティブバイナリに対応。

---

## 動作環境

- **OS**: Windows 10 バージョン 1809 (Build 17763) 以降 / Windows 11
- **フレームワーク**: .NET 10.0 (自己完結型パッケージのためランタイムの事前インストールは不要)
- **アーキテクチャ**: x64 / Arm64

---

## 開発・ローカル実行

### 前提条件
- .NET 10 SDK
- PowerShell 7 または Windows PowerShell 5.1

### ビルドと実行
```powershell
# プロジェクトのビルド
dotnet build -c Release

# アプリケーションの実行
dotnet run --project WiFiChecker.csproj -c Release
```

---

## パッケージ作成

### 1. スタンドアロンインストーラの作成 (`BuildInstaller.ps1`)
インストーラ形式（`.exe`）の配布パッケージを作成します。

```powershell
.\BuildInstaller.ps1
```

- **出力先**: `.\Installer`
- **生成ファイル**:
  - `WiFiChecker_Setup_<version>_x64.exe`
  - `WiFiChecker_Setup_<version>_Arm64.exe`

---

### 2. Microsoft Store 向けパッケージの作成 (`BuildMSIX.ps1`)
Microsoft Store（パートナーセンター）への登録・提出用パッケージ（MSIX / MSIXBundle）を作成します。

```powershell
.\BuildMSIX.ps1
```

- **出力先**: `.\MSIX`
- **生成ファイル**:
  - `WiFiChecker_<version>_x64.msix`
  - `WiFiChecker_<version>_Arm64.msix`
  - `WiFiChecker_<version>_x64_arm64.msixbundle`（**ストア提出用推奨バンドル**）

> **詳細な提出手順・設定方法:**
> パートナーセンターでのアプリ予約や設定ファイルの書き方については、[STORE_SUBMISSION_GUIDE.md](STORE_SUBMISSION_GUIDE.md) を参照してください。

---

## プロジェクト構成

```
WiFiChecker/
├── Assets/                        # ストアおよび Windows 規格の画像アセット
├── Models/                        # データモデル（WiFiInfo, AppSettings など）
├── Resources/                     # アプリアイコンリソース (app.ico)
├── Services/                      # Wi-Fi 情報取得サービス、多言語管理サービス
├── Setup/                         # 単一 EXE インストーラプロジェクト
├── App.xaml / App.xaml.cs         # アプリケーションエントリポイント
├── MainWindow.xaml / .cs          # メインウィンドウ UI・ロジック
├── WiFiChecker.csproj             # メインプロジェクトファイル (.NET 10 WPF)
├── Package.appxmanifest.template  # MSIX パッケージマニフェストテンプレート
├── StoreConfig.json               # Microsoft Store 登録情報設定ファイル
├── BuildInstaller.ps1             # スタンドアロンインストーラ作成スクリプト
├── BuildMSIX.ps1                  # MSIX / MSIXBundle 作成スクリプト
├── STORE_SUBMISSION_GUIDE.md      # Microsoft Store 申請手順書
└── README.md                      # 本ドキュメント
```

---

## 設定ファイルの保存場所

ユーザー設定（ウィンドウ位置・サイズ、言語設定、自動更新間隔等）は以下の場所に UTF-8 形式の JSON として保存されます：

- `%LOCALAPPDATA%\WiFiChecker\settings.json`  
  （例: `C:\Users\<ユーザー名>\AppData\Local\WiFiChecker\settings.json`）
