using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WiFiChecker.Models;

namespace WiFiChecker.Services
{
    /// <summary>
    /// Wi-Fi接続・アクセスポイント情報取得サービス
    /// </summary>
    public class WifiService
    {
        public async Task<WifiInfo> GetCurrentWifiInfoAsync()
        {
            return await Task.Run(() => GetCurrentWifiInfo());
        }

        public WifiInfo GetCurrentWifiInfo()
        {
            var info = new WifiInfo
            {
                LastRefreshed = DateTime.Now
            };

            try
            {
                // 1. netsh wlan show interfaces から詳細なWi-Fi情報を取得
                ParseNetshWlanInterfaces(info);

                // 2. System.Net.NetworkInformation から IP / Subnet / Gateway / DNS を紐付け
                EnrichNetworkStackInfo(info);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Wi-Fi情報取得エラー: {ex.Message}");
            }

            return info;
        }

        private void ParseNetshWlanInterfaces(WifiInfo info)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.Default // OSの標準マルチバイトエンコーディング
                };

                using var process = Process.Start(startInfo);
                if (process == null) return;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000);

                if (string.IsNullOrWhiteSpace(output)) return;

                // 各行をパース
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    int colonIdx = line.IndexOf(':');
                    if (colonIdx <= 0) continue;

                    string key = line.Substring(0, colonIdx).Trim();
                    string val = line.Substring(colonIdx + 1).Trim();

                    // キーの正規化
                    if (key.Equals("状態", StringComparison.OrdinalIgnoreCase) || key.Equals("State", StringComparison.OrdinalIgnoreCase))
                    {
                        info.IsConnected = val.Contains("接続", StringComparison.OrdinalIgnoreCase) ||
                                           val.Contains("connected", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (key.Equals("SSID", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(val)) info.Ssid = val;
                    }
                    else if (key.Contains("BSSID", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(val)) info.Bssid = val.ToUpperInvariant();
                    }
                    else if (key.Equals("シグナル", StringComparison.OrdinalIgnoreCase) || key.Equals("Signal", StringComparison.OrdinalIgnoreCase))
                    {
                        var matchSig = Regex.Match(val, @"\d+");
                        if (matchSig.Success && int.TryParse(matchSig.Value, out int qual))
                        {
                            info.SignalQuality = qual;
                            if (info.SignalDbm == -100)
                            {
                                info.SignalDbm = (qual / 2) - 100;
                            }
                        }
                    }
                    else if (key.Equals("Rssi", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int rssi))
                        {
                            info.SignalDbm = rssi;
                        }
                    }
                    else if (key.Contains("無線", StringComparison.OrdinalIgnoreCase) || key.Contains("Radio", StringComparison.OrdinalIgnoreCase))
                    {
                        info.PhyType = ConvertRadioTypeToStandardName(val);
                    }
                    else if (key.Equals("認証", StringComparison.OrdinalIgnoreCase) || key.Equals("Authentication", StringComparison.OrdinalIgnoreCase))
                    {
                        info.Authentication = val;
                    }
                    else if (key.Equals("暗号", StringComparison.OrdinalIgnoreCase) || key.Equals("Cipher", StringComparison.OrdinalIgnoreCase))
                    {
                        info.Cipher = val;
                    }
                    else if (key.Contains("チャネル", StringComparison.OrdinalIgnoreCase) || key.Contains("Channel", StringComparison.OrdinalIgnoreCase))
                    {
                        var matchCh = Regex.Match(val, @"\d+");
                        if (matchCh.Success && int.TryParse(matchCh.Value, out int ch))
                        {
                            info.Channel = ch;
                            DetermineBandAndFrequency(info, ch);
                        }
                    }
                    else if (key.Contains("バンド", StringComparison.OrdinalIgnoreCase) || key.Contains("Band", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(val)) info.Band = val;
                    }
                    else if (key.Contains("受信速度", StringComparison.OrdinalIgnoreCase) || key.Contains("Receive rate", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(val, @"[\d\.]+");
                        if (match.Success && double.TryParse(match.Value, out double rx))
                        {
                            info.LinkSpeedRxMbps = (long)Math.Round(rx);
                        }
                    }
                    else if (key.Contains("送信速度", StringComparison.OrdinalIgnoreCase) || key.Contains("Transmit rate", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(val, @"[\d\.]+");
                        if (match.Success && double.TryParse(match.Value, out double tx))
                        {
                            info.LinkSpeedTxMbps = (long)Math.Round(tx);
                        }
                    }
                    else if (key.Equals("名前", StringComparison.OrdinalIgnoreCase) || key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        info.InterfaceName = val;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"netsh パースエラー: {ex.Message}");
            }
        }

        private string ConvertRadioTypeToStandardName(string radioType)
        {
            if (string.IsNullOrEmpty(radioType)) return "Unknown";

            if (radioType.Contains("802.11be")) return $"{radioType} (Wi-Fi 7)";
            if (radioType.Contains("802.11ax")) return $"{radioType} (Wi-Fi 6 / 6E)";
            if (radioType.Contains("802.11ac")) return $"{radioType} (Wi-Fi 5)";
            if (radioType.Contains("802.11n")) return $"{radioType} (Wi-Fi 4)";
            if (radioType.Contains("802.11g")) return $"{radioType} (Wi-Fi 3)";
            if (radioType.Contains("802.11a") || radioType.Contains("802.11b")) return radioType;

            return radioType;
        }

        private void DetermineBandAndFrequency(WifiInfo info, int channel)
        {
            if (info.Band != "Unknown" && !string.IsNullOrEmpty(info.Band))
            {
                // すでに netsh から "5 GHz" や "2.4 GHz" などが取得できている場合
            }

            // 2.4 GHz 帯: Ch 1 ~ 14
            if (channel >= 1 && channel <= 14)
            {
                if (string.IsNullOrEmpty(info.Band) || info.Band == "Unknown") info.Band = "2.4 GHz";
                info.FrequencyGhz = channel == 14 ? 2.484 : 2.407 + (channel * 0.005);
            }
            // 5 GHz 帯: Ch 32 ~ 177
            else if (channel >= 32 && channel <= 177)
            {
                if (string.IsNullOrEmpty(info.Band) || info.Band == "Unknown") info.Band = "5 GHz";
                info.FrequencyGhz = 5.000 + (channel * 0.005);
            }
            // 6 GHz 帯: Ch 1 ~ 233
            else if (channel >= 1 && channel <= 233 && (info.PhyType.Contains("Wi-Fi 6E") || info.PhyType.Contains("Wi-Fi 7") || info.PhyType.Contains("802.11ax") || info.PhyType.Contains("802.11be")))
            {
                if (string.IsNullOrEmpty(info.Band) || info.Band == "Unknown") info.Band = "6 GHz";
                info.FrequencyGhz = 5.950 + (channel * 0.005);
            }
        }

        private void EnrichNetworkStackInfo(WifiInfo info)
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                                  nic.OperationalStatus == OperationalStatus.Up)
                    .ToList();

                if (!interfaces.Any())
                {
                    interfaces = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                        .ToList();
                }

                var targetNic = interfaces.FirstOrDefault(nic =>
                    string.Equals(nic.Name, info.InterfaceName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(nic.Description, info.InterfaceName, StringComparison.OrdinalIgnoreCase)
                ) ?? interfaces.FirstOrDefault();

                if (targetNic != null)
                {
                    if (string.Equals(info.InterfaceName, "Unknown", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(info.InterfaceName))
                    {
                        info.InterfaceName = targetNic.Name;
                    }

                    info.MacAddress = string.Join(":", targetNic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));

                    var ipProps = targetNic.GetIPProperties();

                    // IPv4
                    var ipv4Unicast = ipProps.UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (ipv4Unicast != null)
                    {
                        info.Ipv4Address = ipv4Unicast.Address.ToString();
                        info.SubnetMask = ipv4Unicast.IPv4Mask?.ToString() ?? "-";
                    }

                    // IPv6
                    var ipv6Unicast = ipProps.UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetworkV6 && !ip.Address.IsIPv6LinkLocal);
                    if (ipv6Unicast != null)
                    {
                        info.Ipv6Address = ipv6Unicast.Address.ToString();
                    }

                    // Default Gateway
                    var gateway = ipProps.GatewayAddresses
                        .FirstOrDefault(gw => gw.Address.AddressFamily == AddressFamily.InterNetwork || gw.Address.AddressFamily == AddressFamily.InterNetworkV6);
                    if (gateway != null)
                    {
                        info.GatewayAddress = gateway.Address.ToString();
                    }

                    // DNS
                    var dns = ipProps.DnsAddresses
                        .Where(d => d.AddressFamily == AddressFamily.InterNetwork || d.AddressFamily == AddressFamily.InterNetworkV6)
                        .Select(d => d.ToString());
                    if (dns.Any())
                    {
                        info.DnsServers = string.Join(", ", dns);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NetworkInterface 情報取得エラー: {ex.Message}");
            }
        }
    }
}
