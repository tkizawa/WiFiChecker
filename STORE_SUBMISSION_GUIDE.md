# Microsoft Store 登録・申請ガイド (WiFiChecker)

本ドキュメントは、WiFiChecker を Microsoft Store（Windows アプリストア）へ登録・申請するための手順と設定方法をまとめたガイドです。

---

## 1. 全体の流れ

1. **Microsoft パートナーセンター** にて開発者アカウントでログイン
2. **新しいアプリの予約**（アプリ名の登録）
3. パートナーセンターから **製品ID / 発行者情報** を取得
4. `StoreConfig.json` に取得した情報を設定
5. `BuildMSIX.ps1` を実行して **MSIX / MSIXBundle** を生成
6. パートナーセンターにパッケージをアップロードし、ストア登録情報を入力して審査へ提出

---

## 2. パートナーセンターでの事前準備

### 2.1 アプリ名の予約
1. [Microsoft パートナーセンター (Partner Center)](https://partner.microsoft.com/dashboard) にアクセスしてログインします。
2. 左メニューの **「アプリとゲーム」 (Apps and games)** → **「新しいアプリの作成」** をクリックします。
3. アプリ名（例: `WiFiChecker` または希望の名称）を入力し、予約を完了します。

### 2.2 パッケージID・発行者情報の確認
予約完了後、対象アプリの管理画面から：
1. **「製品の管理」 (Product management)** → **「製品の登録情報」 (Product Identity)** を開きます。
2. 以下の4つの項目を確認・コピーします：
   - **パッケージ/ID/名前 (Package/Identity/Name)**：例 `51978YourName.WiFiChecker`
   - **パッケージ/ID/発行者 (Package/Identity/Publisher)**：例 `CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX`
   - **発行者表示名 (Publisher display name)**：例 `Your Name` または会社名
   - **パッケージ表示名 (Package display name)**：例 `Wi-Fi Checker`

---

## 3. プロジェクト設定の反映

取得した情報を、プロジェクト直下の `StoreConfig.json` に反映します。

```json
{
  "PackageIdentityName": "51978YourName.WiFiChecker",
  "PublisherId": "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX",
  "PublisherDisplayName": "Your Publisher Name",
  "DisplayName": "Wi-Fi Checker",
  "Description": "Wi-Fi接続状況、電波強度、リンク速度、周波数帯などをリアルタイムに確認できるツールです。"
}
```

> **注意:**
> - `PublisherId` は必ずパートナーセンターに表示されている `CN=` で始まる完全な文字列を指定してください。
> - アプリバージョンは `WiFiChecker.csproj` の `<AssemblyVersion>` および `<FileVersion>`（例: `1.0.0.0`）から自動取得されます。バージョンアップ時は `c` の桁を1つ上げてください（例: `1.0.1.0`）。

---

## 4. MSIX パッケージのビルド

PowerShell（PowerShell 7 推奨）を開き、プロジェクトルートで以下のスクリプトを実行します。

```powershell
.\BuildMSIX.ps1
```

### 生成される成果物（`.\MSIX` フォルダ）
- `WiFiChecker_1.0.0.0_x64.msix`（x64 アーキテクチャ用）
- `WiFiChecker_1.0.0.0_Arm64.msix`（Arm64 アーキテクチャ用）
- `WiFiChecker_1.0.0.0_x64_arm64.msixbundle`（**ストア提出用バンドル**）

> **ヒント:**
> Microsoft パートナーセンターには、x64 と Arm64 の両方を1つに内包した **`.msixbundle`** をアップロードするのが最も推奨されます（ユーザーのPC環境に合わせて最適なアーキテクチャが自動配信されます）。

---

## 5. パートナーセンターでの申請手順

アプリの管理画面で **「提出を開始」 (Start submission)** をクリックし、各項目を入力します。

### 5.1 パッケージ (Packages)
- 作成された `.\MSIX\WiFiChecker_1.0.0.0_x64_arm64.msixbundle`（または個別の `_x64.msix`, `_Arm64.msix`）をドラッグ＆ドロップしてアップロードします。
- 検証が正常に完了することを確認します。

### 5.2 プロパティ (Properties)
- **カテゴリ**: ユーティリティ & ツール (Utilities & tools) / ネットワーク (Networking)
- **プライバシーポリシー URL**: プライバシーポリシーのウェブページURLを入力（GitHubリポジトリのREADMEやWiki等のリンクでも可）。
  - *WiFiChecker はローカルネットワーク接続情報の取得のみを行い、個人情報の外部送信は行わない旨を明記すると審査がスムーズです。*

### 5.3 年齢区分 (Age ratings)
- 質問に回答します（暴力表現、性的表現、オンライン交流などはいずれも「いいえ」）。
- 通常「全年齢 (All Ages / 3歳以上)」のレーティングが自動付与されます。

### 5.4 ストア掲載情報 (Store listings)
言語ごと（日本語 `ja-JP`、英語 `en-US`）に以下のテキストをコピー＆ペーストして使用できます。

#### 🇯🇵 日本語 (ja-JP)
- **説明 (Description)**:
  ```text
  WiFiChecker は、Windows PC が接続している Wi-Fi アクセスポイントの電波状況や詳細情報をリアルタイムに確認・モニタリングできる軽量でモダンなユーティリティツールです。

  テレワークやオンライン会議、ゲームプレイ中に「Wi-Fiが遅い」「接続が途切れる」と感じた際、現在の電波強度やリンク速度、接続している周波数帯（2.4GHz / 5GHz / 6GHz）を素早くチェックできます。

  【主な機能】
  ■ 電波強度（RSSI）のリアルタイムモニタリング
  現在の信号強度をパーセンテージとカラープログレスバーで直感的に可視化します。

  ■ リンク速度の表示
  アクセスポイントとPC間の受信速度（Rx）および送信速度（Tx）のリンク速度（Mbps）をリアルタイムに確認できます。

  ■ 接続中の Wi-Fi 詳細情報
  SSID（ネットワーク名）、BSSID（MACアドレス）、Wi-Fi規格（Wi-Fi 6 / 802.11ax、Wi-Fi 5 / 802.11ac など）、周波数帯、チャンネル番号を一目で把握できます。

  ■ 柔軟な更新設定
  1秒〜10秒間隔での自動更新に対応。もちろんワンクリックでの即時手動更新も可能です。

  ■ 多言語対応 & 設定保存
  Windows のシステム言語に応じて日本語と英語を自動切り替え。ウィンドウの位置やサイズ、更新間隔などの設定は自動で保存・復元されます。
  ```
- **製品の特長 (Product features)** (1行ずつ追加):
  - `Wi-Fi 電波強度（RSSI）のリアルタイム可視化`
  - `送受信リンク速度（Rx/Tx Mbps）のモニタリング`
  - `接続中アクセスポイントの詳細情報（SSID、BSSID、周波数帯、チャンネル、Wi-Fi規格）の表示`
  - `1秒〜10秒の自動更新間隔の調整および手動更新機能`
  - `日本語・英語のバイリンガル対応`
  - `ウィンドウ位置・サイズの自動保存・復元`
- **今回のリリースで新しくなった点 (What's new in this version)**:
  ```text
  初回リリース
  - Wi-Fi 接続状況・電波強度・リンク速度チェッカー
  - x64 および Arm64 ネイティブ対応
  ```

#### 🇺🇸 英語 (en-US)
- **Description**:
  ```text
  WiFiChecker is a lightweight, modern Windows desktop utility designed to monitor and visualize your Wi-Fi connection status, signal strength, and network details in real time.

  When experiencing slow connections or unstable network during remote work, video calls, or online gaming, WiFiChecker helps you instantly diagnose your Wi-Fi link speed, signal quality, and connected frequency band (2.4 GHz / 5 GHz / 6 GHz).

  [Key Features]
  ■ Real-Time Signal Strength (RSSI) Monitoring
  Visualizes current Wi-Fi signal strength with percentage and dynamic colored progress indicators.

  ■ Link Speed Monitoring
  Monitors real-time receive (Rx) and transmit (Tx) link speeds (Mbps) between your PC and the Wi-Fi access point.

  ■ Detailed Network Information
  Displays SSID, BSSID (MAC address), Wi-Fi standard (Wi-Fi 6 / 802.11ax, Wi-Fi 5 / 802.11ac, etc.), frequency band, and channel number at a glance.

  ■ Flexible Auto-Refresh
  Supports automatic background refreshes from 1 to 10 seconds, plus instant manual refresh on demand.

  ■ Multilingual & State Persistence
  Automatically adapts between English and Japanese based on Windows display settings. Saves and restores your window size, position, and preferences automatically.
  ```
- **Product features**:
  - `Real-time Wi-Fi signal strength (RSSI) monitoring with visual indicators`
  - `Live receive/transmit link speed (Rx/Tx Mbps) diagnostics`
  - `Comprehensive AP details: SSID, BSSID, Wi-Fi protocol, band, and channel`
  - `Adjustable auto-refresh interval (1–10s) and manual refresh`
  - `Bilingual support (English & Japanese)`
  - `Native performance for both x64 and Arm64 architectures`
- **What's new in this version**:
  ```text
  Initial release
  - Real-time Wi-Fi connection, signal quality, and link speed monitor
  - Native support for x64 and Arm64 devices
  ```

- **スクリーンショット**:
  - アプリ画面のスクリーンショット（PNG形式、1366x768 または 1920x1080 推奨）を1枚以上添付

### 5.5 価格と提供状況 (Pricing and availability)
- **価格**: 無料（または有料）
- **市場**: 全世界、または日本などの対象地域を選択

---

## 6. 提出と審査

すべての項目にチェックが入ったら、**「審査に提出」 (Submit to the Store)** をクリックします。
通常、数時間〜数営業日で審査が完了し、Microsoft Store に公開されます。
