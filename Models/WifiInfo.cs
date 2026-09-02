using System;

namespace WiFiChecker.Models
{
    /// <summary>
    /// Wi-Fi接続詳細情報モデル
    /// </summary>
    public class WifiInfo
    {
        public bool IsConnected { get; set; } = false;
        public string Ssid { get; set; } = "未接続";
        public string Bssid { get; set; } = "00:00:00:00:00:00";
        public int SignalQuality { get; set; } = 0; // 0 ~ 100%
        public int SignalDbm { get; set; } = -100; // dBm
        public double FrequencyGhz { get; set; } = 0.0;
        public int Channel { get; set; } = 0;
        public string Band { get; set; } = "Unknown";
        public string PhyType { get; set; } = "Unknown";
        public string Authentication { get; set; } = "Unknown";
        public string Cipher { get; set; } = "Unknown";
        public long LinkSpeedRxMbps { get; set; } = 0;
        public long LinkSpeedTxMbps { get; set; } = 0;

        public string InterfaceName { get; set; } = "Unknown";
        public string MacAddress { get; set; } = "00:00:00:00:00:00";
        public string Ipv4Address { get; set; } = "-";
        public string SubnetMask { get; set; } = "-";
        public string Ipv6Address { get; set; } = "-";
        public string GatewayAddress { get; set; } = "-";
        public string DnsServers { get; set; } = "-";
        public string NetworkCategory { get; set; } = "-";
        public DateTime LastRefreshed { get; set; } = DateTime.Now;

        /// <summary>
        /// 全情報をクリップボード用テキスト形式で出力
        /// </summary>
        public string ToFormattedText(bool isJapanese)
        {
            if (!IsConnected)
            {
                return isJapanese ? "Wi-Fiに接続されていません。" : "Not connected to Wi-Fi.";
            }

            if (isJapanese)
            {
                return $@"=== Wi-Fi 接続情報 ===
■ アクセスポイント情報
SSID: {Ssid}
BSSID (MAC): {Bssid}
電波強度: {SignalQuality}% ({SignalDbm} dBm)
規格: {PhyType}
周波数帯: {Band} (チャンネル {Channel} / {FrequencyGhz:F3} GHz)
セキュリティ認証: {Authentication}
暗号化方式: {Cipher}
リンク速度: 受信 {LinkSpeedRxMbps} Mbps / 送信 {LinkSpeedTxMbps} Mbps

■ ネットワーク・IP情報
アダプター名: {InterfaceName}
PC MACアドレス: {MacAddress}
IPv4 アドレス: {Ipv4Address}
サブネットマスク: {SubnetMask}
IPv6 アドレス: {Ipv6Address}
デフォルトゲートウェイ: {GatewayAddress}
DNS サーバー: {DnsServers}
ネットワーク種別: {NetworkCategory}
取得日時: {LastRefreshed:yyyy-MM-dd HH:mm:ss}
";
            }
            else
            {
                return $@"=== Wi-Fi Connection Info ===
■ Access Point Info
SSID: {Ssid}
BSSID (MAC): {Bssid}
Signal Strength: {SignalQuality}% ({SignalDbm} dBm)
PHY Standard: {PhyType}
Band: {Band} (Channel {Channel} / {FrequencyGhz:F3} GHz)
Authentication: {Authentication}
Cipher: {Cipher}
Link Speed: Rx {LinkSpeedRxMbps} Mbps / Tx {LinkSpeedTxMbps} Mbps

■ Network & IP Info
Adapter Name: {InterfaceName}
PC MAC Address: {MacAddress}
IPv4 Address: {Ipv4Address}
Subnet Mask: {SubnetMask}
IPv6 Address: {Ipv6Address}
Default Gateway: {GatewayAddress}
DNS Servers: {DnsServers}
Network Category: {NetworkCategory}
Refreshed At: {LastRefreshed:yyyy-MM-dd HH:mm:ss}
";
            }
        }
    }
}
