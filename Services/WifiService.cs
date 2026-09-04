using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    /// Windows Native Wifi API (wlanapi.dll) を第一優先で使用し、
    /// フォールバックとして netsh wlan show interfaces (文字コード自動判別) を利用
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
                // 1. Native Wifi API (wlanapi.dll) を最優先で試行
                // 言語・コードページ・コマンド出力差異に左右されずミリ秒で完全取得可能
                var nativeInfo = NativeWifiService.GetCurrentWifiInfo();
                if (nativeInfo != null)
                {
                    CopyProperties(nativeInfo, info);
                }
                else
                {
                    // 2. フォールバック: netsh wlan show interfaces から取得
                    ParseNetshWlanInterfaces(info);
                }

                // 3. System.Net.NetworkInformation から IP / Subnet / Gateway / DNS / MAC を紐付け
                EnrichNetworkStackInfo(info);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Wi-Fi情報取得エラー: {ex.Message}");
            }

            return info;
        }

        private void ParseNetshWlanInterfaces(WifiInfo targetInfo)
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
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return;

                // バイト列として標準出力を読み取り、適切なエンコーディングでデコード
                using var memoryStream = new MemoryStream();
                process.StandardOutput.BaseStream.CopyTo(memoryStream);
                process.WaitForExit(3000);

                byte[] rawBytes = memoryStream.ToArray();
                if (rawBytes.Length == 0) return;

                string output = DecodeProcessOutput(rawBytes);
                if (string.IsNullOrWhiteSpace(output)) return;

                var interfaceList = new List<WifiInfo>();
                WifiInfo? current = null;

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    int colonIdx = line.IndexOf(':');
                    if (colonIdx <= 0) continue;

                    string key = line.Substring(0, colonIdx).Trim();
                    string val = line.Substring(colonIdx + 1).Trim();

                    // 新しいインターフェイスの開始判定（名前 / Name）
                    if (key.Equals("名前", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        current = new WifiInfo
                        {
                            LastRefreshed = targetInfo.LastRefreshed,
                            InterfaceName = val
                        };
                        interfaceList.Add(current);
                        continue;
                    }

                    if (current == null)
                    {
                        current = new WifiInfo
                        {
                            LastRefreshed = targetInfo.LastRefreshed
                        };
                        interfaceList.Add(current);
                    }

                    // 状態 / State
                    if (key.Equals("状態", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("State", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isConn = (val.Contains("接続", StringComparison.OrdinalIgnoreCase) ||
                                       val.Contains("connected", StringComparison.OrdinalIgnoreCase))
                                      && !val.Contains("切断", StringComparison.OrdinalIgnoreCase)
                                      && !val.Contains("disconnected", StringComparison.OrdinalIgnoreCase);
                        current.IsConnected = isConn;
                    }
                    // SSID (BSSID を除外)
                    else if (key.Equals("SSID", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(val)) current.Ssid = val;
                    }
                    // AP BSSID / BSSID
                    else if (key.Contains("BSSID", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(val)) current.Bssid = val.ToUpperInvariant();
                    }
                    // シグナル / Signal
                    else if (key.Equals("シグナル", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("Signal", StringComparison.OrdinalIgnoreCase))
                    {
                        var matchSig = Regex.Match(val, @"\d+");
                        if (matchSig.Success && int.TryParse(matchSig.Value, out int qual))
                        {
                            current.SignalQuality = qual;
                            if (current.SignalDbm == -100)
                            {
                                current.SignalDbm = (qual / 2) - 100;
                            }
                        }
                    }
                    // RSSI
                    else if (key.Equals("Rssi", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int rssi))
                        {
                            current.SignalDbm = rssi;
                        }
                    }
                    // 無線の種類 / Radio type
                    else if ((key.Contains("無線", StringComparison.OrdinalIgnoreCase) && key.Contains("種類", StringComparison.OrdinalIgnoreCase)) ||
                             key.Equals("Radio type", StringComparison.OrdinalIgnoreCase) ||
                             key.Contains("802.11", StringComparison.OrdinalIgnoreCase))
                    {
                        current.PhyType = ConvertRadioTypeToStandardName(val);
                    }
                    // 認証 / Authentication
                    else if (key.Equals("認証", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("Authentication", StringComparison.OrdinalIgnoreCase))
                    {
                        current.Authentication = val;
                    }
                    // 暗号 / Cipher
                    else if (key.Equals("暗号", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("Cipher", StringComparison.OrdinalIgnoreCase))
                    {
                        current.Cipher = val;
                    }
                    // チャネル / Channel
                    else if (key.Contains("チャネル", StringComparison.OrdinalIgnoreCase) ||
                             key.Contains("Channel", StringComparison.OrdinalIgnoreCase))
                    {
                        var matchCh = Regex.Match(val, @"\d+");
                        if (matchCh.Success && int.TryParse(matchCh.Value, out int ch))
                        {
                            current.Channel = ch;
                            DetermineBandAndFrequency(current, ch);
                        }
                    }
                    // バンド / Band
                    else if (key.Contains("バンド", StringComparison.OrdinalIgnoreCase) ||
                             key.Contains("Band", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(val)) current.Band = val;
                    }
                    // 受信レート / Receive rate
                    else if (key.Contains("受信", StringComparison.OrdinalIgnoreCase) ||
                             key.Contains("Receive", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(val, @"[\d\.]+");
                        if (match.Success && double.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rx))
                        {
                            current.LinkSpeedRxMbps = (long)Math.Round(rx);
                        }
                    }
                    // 送信レート / Transmit rate
                    else if (key.Contains("送信", StringComparison.OrdinalIgnoreCase) ||
                             key.Contains("Transmit", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(val, @"[\d\.]+");
                        if (match.Success && double.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double tx))
                        {
                            current.LinkSpeedTxMbps = (long)Math.Round(tx);
                        }
                    }
                    // 物理アドレス / Physical address
                    else if (key.Equals("物理アドレス", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("Physical address", StringComparison.OrdinalIgnoreCase))
                    {
                        current.MacAddress = val.ToUpperInvariant();
                    }
                }

                // 接続されているインターフェイスを優先して選択（なければ先頭）
                var best = interfaceList.FirstOrDefault(i => i.IsConnected) ?? interfaceList.FirstOrDefault();
                if (best != null)
                {
                    CopyProperties(best, targetInfo);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"netsh パースエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// コマンド出力を適切な文字コード（UTF-8, OEM CP932等）で自動判別デコード
        /// </summary>
        private string DecodeProcessOutput(byte[] bytes)
        {
            try
            {
                // 1. まず OEM コードページ（日本語 Windows の場合は CP932 / Shift_JIS）を試行
                int oemCodePage = CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
                if (oemCodePage > 0)
                {
                    try
                    {
                        var oemEncoding = Encoding.GetEncoding(oemCodePage);
                        string oemDecoded = oemEncoding.GetString(bytes);
                        // 日本語または英語の主要キーワードが含まれているかチェック
                        if (oemDecoded.Contains("状態") || oemDecoded.Contains("State") ||
                            oemDecoded.Contains("名前") || oemDecoded.Contains("Name"))
                        {
                            return oemDecoded;
                        }
                    }
                    catch { }
                }

                // 2. CP932 (Shift_JIS)
                try
                {
                    var sjisEncoding = Encoding.GetEncoding(932);
                    string sjisDecoded = sjisEncoding.GetString(bytes);
                    if (sjisDecoded.Contains("状態") || sjisDecoded.Contains("名前"))
                    {
                        return sjisDecoded;
                    }
                }
                catch { }

                // 3. UTF-8
                string utf8Decoded = Encoding.UTF8.GetString(bytes);
                return utf8Decoded;
            }
            catch
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        private void CopyProperties(WifiInfo source, WifiInfo target)
        {
            target.IsConnected = source.IsConnected;
            target.Ssid = source.Ssid;
            target.Bssid = source.Bssid;
            target.SignalQuality = source.SignalQuality;
            target.SignalDbm = source.SignalDbm;
            target.FrequencyGhz = source.FrequencyGhz;
            target.Channel = source.Channel;
            target.Band = source.Band;
            target.PhyType = source.PhyType;
            target.Authentication = source.Authentication;
            target.Cipher = source.Cipher;
            target.LinkSpeedRxMbps = source.LinkSpeedRxMbps;
            target.LinkSpeedTxMbps = source.LinkSpeedTxMbps;
            target.InterfaceName = source.InterfaceName;
            target.InterfaceGuid = source.InterfaceGuid;
            target.MacAddress = source.MacAddress;
            target.NetworkCategory = source.NetworkCategory;
        }

        private string ConvertRadioTypeToStandardName(string radioType)
        {
            if (string.IsNullOrEmpty(radioType)) return "Unknown";

            if (radioType.Contains("802.11be", StringComparison.OrdinalIgnoreCase)) return $"{radioType} (Wi-Fi 7)";
            if (radioType.Contains("802.11ax", StringComparison.OrdinalIgnoreCase)) return $"{radioType} (Wi-Fi 6 / 6E)";
            if (radioType.Contains("802.11ac", StringComparison.OrdinalIgnoreCase)) return $"{radioType} (Wi-Fi 5)";
            if (radioType.Contains("802.11n", StringComparison.OrdinalIgnoreCase)) return $"{radioType} (Wi-Fi 4)";
            if (radioType.Contains("802.11g", StringComparison.OrdinalIgnoreCase)) return $"{radioType} (Wi-Fi 3)";
            if (radioType.Contains("802.11a", StringComparison.OrdinalIgnoreCase) || radioType.Contains("802.11b", StringComparison.OrdinalIgnoreCase)) return radioType;

            return radioType;
        }

        private void DetermineBandAndFrequency(WifiInfo info, int channel)
        {
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
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();

                NetworkInterface? targetNic = null;

                // 1. GUID での完全一致を最優先（Native Wifi API で判明している場合）
                if (info.InterfaceGuid != Guid.Empty)
                {
                    string guidBraced = info.InterfaceGuid.ToString("B");
                    string guidHyphen = info.InterfaceGuid.ToString("D");
                    targetNic = interfaces.FirstOrDefault(nic =>
                        string.Equals(nic.Id, guidBraced, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(nic.Id, guidHyphen, StringComparison.OrdinalIgnoreCase));
                }

                // 2. MACアドレスでの一致を試みる
                if (targetNic == null && !string.IsNullOrEmpty(info.MacAddress) && info.MacAddress != "00:00:00:00:00:00")
                {
                    string macClean = info.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
                    targetNic = interfaces.FirstOrDefault(nic =>
                        nic.GetPhysicalAddress().ToString().ToUpperInvariant() == macClean &&
                        nic.OperationalStatus == OperationalStatus.Up);
                }

                // 3. OperationalStatus.Up かつ Wireless80211 で、仮想フィルタを除外したアクティブなWi-Fi
                if (targetNic == null)
                {
                    var wirelessNics = interfaces
                        .Where(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                                      nic.OperationalStatus == OperationalStatus.Up &&
                                      !nic.Name.Contains("Filter") && !nic.Description.Contains("Filter") &&
                                      !nic.Name.Contains("Packet Scheduler") && !nic.Description.Contains("Packet Scheduler"))
                        .ToList();

                    // デフォルトゲートウェイを持つものを最優先
                    targetNic = wirelessNics.FirstOrDefault(nic =>
                        nic.GetIPProperties().GatewayAddresses.Any(gw => gw.Address.AddressFamily == AddressFamily.InterNetwork))
                        ?? wirelessNics.FirstOrDefault();
                }

                // 4. フォールバック: インターフェイス名または説明での一致
                if (targetNic == null && !string.IsNullOrEmpty(info.InterfaceName) && info.InterfaceName != "Unknown")
                {
                    targetNic = interfaces.FirstOrDefault(nic =>
                        nic.OperationalStatus == OperationalStatus.Up &&
                        (string.Equals(nic.Name, info.InterfaceName, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(nic.Description, info.InterfaceName, StringComparison.OrdinalIgnoreCase)));
                }

                // 5. 最後のフォールバック
                targetNic ??= interfaces.FirstOrDefault(nic =>
                    nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                    nic.OperationalStatus == OperationalStatus.Up);

                if (targetNic != null)
                {
                    // アダプター名（OS上のフレンドリー名: 例: "Wi-Fi"）
                    if (!string.IsNullOrEmpty(targetNic.Name))
                    {
                        info.InterfaceName = targetNic.Name;
                    }

                    // PC MAC アドレス
                    var bytes = targetNic.GetPhysicalAddress().GetAddressBytes();
                    if (bytes.Length == 6)
                    {
                        info.MacAddress = string.Join(":", bytes.Select(b => b.ToString("X2")));
                    }

                    var ipProps = targetNic.GetIPProperties();

                    // IPv4: APIPA (169.254.x.x) を除外した正規のIPアドレスを最優先
                    var validIpv4 = ipProps.UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                                              !ip.Address.ToString().StartsWith("169.254."));
                    validIpv4 ??= ipProps.UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork);

                    if (validIpv4 != null)
                    {
                        info.Ipv4Address = validIpv4.Address.ToString();
                        info.SubnetMask = validIpv4.IPv4Mask?.ToString() ?? "-";
                    }

                    // IPv6: リンクローカル以外のグローバル/一時アドレスを優先
                    var ipv6Global = ipProps.UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetworkV6 &&
                                              !ip.Address.IsIPv6LinkLocal &&
                                              !ip.Address.IsIPv6SiteLocal);
                    ipv6Global ??= ipProps.UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetworkV6);

                    if (ipv6Global != null)
                    {
                        info.Ipv6Address = ipv6Global.Address.ToString();
                    }

                    // デフォルトゲートウェイ: IPv4 を最優先
                    var ipv4Gw = ipProps.GatewayAddresses
                        .FirstOrDefault(gw => gw.Address.AddressFamily == AddressFamily.InterNetwork &&
                                              !string.IsNullOrEmpty(gw.Address.ToString()) &&
                                              gw.Address.ToString() != "0.0.0.0");
                    var anyGw = ipv4Gw ?? ipProps.GatewayAddresses.FirstOrDefault();
                    if (anyGw != null)
                    {
                        info.GatewayAddress = anyGw.Address.ToString();
                    }

                    // DNS サーバー
                    var dnsList = ipProps.DnsAddresses
                        .Where(d => d.AddressFamily == AddressFamily.InterNetwork ||
                                   (d.AddressFamily == AddressFamily.InterNetworkV6 && !d.IsIPv6LinkLocal))
                        .Select(d => d.ToString())
                        .ToList();

                    if (!dnsList.Any())
                    {
                        dnsList = ipProps.DnsAddresses.Select(d => d.ToString()).ToList();
                    }

                    if (dnsList.Any())
                    {
                        info.DnsServers = string.Join(", ", dnsList);
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

