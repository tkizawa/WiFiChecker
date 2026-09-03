using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace WiFiChecker.Services
{
    /// <summary>
    /// 日本語・英語 多言語管理サービス
    /// </summary>
    public class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _currentLanguage = "auto";
        private bool _isJapanese = true;

        public bool IsJapanese => _isJapanese;

        public LocalizationService()
        {
            SetLanguage("auto");
        }

        public void SetLanguage(string langCode)
        {
            _currentLanguage = langCode;
            if (langCode == "auto")
            {
                var uiCulture = CultureInfo.CurrentUICulture;
                _isJapanese = uiCulture.Name.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                _isJapanese = langCode.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
            }

            OnPropertyChanged(string.Empty); // 全バインディングプロパティ更新
        }

        public string AppTitle => _isJapanese ? "Wi-Fi アクセスポイント チェッカー" : "Wi-Fi Access Point Checker";
        public string HeaderSubTitle => _isJapanese ? "接続中のWi-Fi情報をリアルタイムモニタリング" : "Real-time Wi-Fi AP Monitoring";
        public string ConnectedStatus => _isJapanese ? "接続中" : "Connected";
        public string DisconnectedStatus => _isJapanese ? "未接続" : "Disconnected";

        public string RefreshText => _isJapanese ? "更新" : "Refresh";
        public string AutoRefreshText => _isJapanese ? "自動更新:" : "Auto Refresh:";
        public string CopyText => _isJapanese ? "コピー" : "Copy";
        public string CopyAllText => _isJapanese ? "全情報をコピー" : "Copy All Info";
        public string CopiedMessage => _isJapanese ? "クリップボードにコピーしました" : "Copied to clipboard";

        // セクションタイトル
        public string SectionAccessPoint => _isJapanese ? "アクセスポイント (AP) 情報" : "Access Point Info";
        public string SectionWirelessSpecs => _isJapanese ? "無線スペック & セキュリティ" : "Wireless Specs & Security";
        public string SectionNetwork => _isJapanese ? "ネットワーク & IP 設定" : "Network & IP Settings";
        public string SectionSettings => _isJapanese ? "設定" : "Settings";

        // フィールド名
        public string LabelSsid => "SSID (ネットワーク名)";
        public string LabelBssid => "BSSID (MACアドレス)";
        public string LabelSignal => _isJapanese ? "電波強度 (Signal)" : "Signal Strength";
        public string LabelPhyType => _isJapanese ? "Wi-Fi 規格 (PHY)" : "Wi-Fi Standard (PHY)";
        public string LabelBand => _isJapanese ? "周波数帯 (Band)" : "Frequency Band";
        public string LabelChannel => _isJapanese ? "チャンネル (Channel)" : "Channel";
        public string LabelAuth => _isJapanese ? "認証方式 (Authentication)" : "Authentication";
        public string LabelCipher => _isJapanese ? "暗号化方式 (Cipher)" : "Encryption Cipher";
        public string LabelLinkSpeed => _isJapanese ? "リンク速度 (Rx / Tx)" : "Link Speed (Rx / Tx)";
        public string LabelAdapter => _isJapanese ? "ネットワーク アダプター" : "Network Adapter";
        public string LabelPcMac => _isJapanese ? "PC MAC アドレス" : "PC MAC Address";
        public string LabelIpv4 => "IPv4 アドレス";
        public string LabelSubnet => _isJapanese ? "サブネット マスク" : "Subnet Mask";
        public string LabelIpv6 => "IPv6 アドレス";
        public string LabelGateway => _isJapanese ? "デフォルト ゲートウェイ" : "Default Gateway";
        public string LabelDns => "DNS サーバー";
        public string LabelLastUpdated => _isJapanese ? "最終更新時間" : "Last Refreshed";

        // 設定ダイアログ用
        public string LabelLanguageSetting => _isJapanese ? "表示言語 (Language)" : "Display Language";
        public string OptionLangAuto => _isJapanese ? "OSの設定に合わせる (Auto)" : "Follow OS Settings (Auto)";
        public string OptionLangJa => "日本語 (Japanese)";
        public string OptionLangEn => "English";
        public string LabelIntervalSetting => _isJapanese ? "自動更新間隔" : "Refresh Interval";
        public string UnitSeconds => _isJapanese ? "秒" : "sec";
        public string LabelCsvLoggingSetting => _isJapanese ? "更新時にCSVログを記録する" : "Enable CSV logging on refresh";
        public string BtnOpenLogFolder => _isJapanese ? "ログフォルダーを開く" : "Open Log Folder";
        public string BtnClose => _isJapanese ? "閉じる" : "Close";

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
