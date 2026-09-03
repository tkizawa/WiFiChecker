using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WiFiChecker.Models;

namespace WiFiChecker.Services
{
    /// <summary>
    /// Wi-Fi情報をCSV形式でファイルにロギングするサービス
    /// 保存先: %LocalAppData%\WiFiChecker\Logs\wifi_log_yyyy-MM-dd.csv
    /// </summary>
    public static class CsvLoggerService
    {
        private static readonly object _lock = new object();

        public static string LogDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WiFiChecker",
            "Logs"
        );

        private static readonly string CsvHeader =
            "Timestamp,IsConnected,SSID,BSSID,SignalQuality(%),Signal(dBm),PHY,Band,Channel,Frequency(GHz),Authentication,Cipher,RxSpeed(Mbps),TxSpeed(Mbps),IPv4,SubnetMask,IPv6,Gateway,DNS,AdapterName,PcMacAddress";

        public static async Task LogWifiInfoAsync(WifiInfo info)
        {
            await Task.Run(() => LogWifiInfo(info));
        }

        public static void LogWifiInfo(WifiInfo info)
        {
            if (!App.Settings.EnableCsvLogging) return;

            try
            {
                lock (_lock)
                {
                    if (!Directory.Exists(LogDirectory))
                    {
                        Directory.CreateDirectory(LogDirectory);
                    }

                    string fileName = $"wifi_log_{DateTime.Now:yyyy-MM-dd}.csv";
                    string filePath = Path.Combine(LogDirectory, fileName);

                    bool fileExists = File.Exists(filePath);

                    var sb = new StringBuilder();
                    if (!fileExists)
                    {
                        sb.AppendLine(CsvHeader);
                    }

                    string line = string.Join(",",
                        EscapeCsv(info.LastRefreshed.ToString("yyyy-MM-dd HH:mm:ss")),
                        info.IsConnected ? "Connected" : "Disconnected",
                        EscapeCsv(info.Ssid),
                        EscapeCsv(info.Bssid),
                        info.SignalQuality,
                        info.SignalDbm,
                        EscapeCsv(info.PhyType),
                        EscapeCsv(info.Band),
                        info.Channel,
                        info.FrequencyGhz.ToString("F3"),
                        EscapeCsv(info.Authentication),
                        EscapeCsv(info.Cipher),
                        info.LinkSpeedRxMbps,
                        info.LinkSpeedTxMbps,
                        EscapeCsv(info.Ipv4Address),
                        EscapeCsv(info.SubnetMask),
                        EscapeCsv(info.Ipv6Address),
                        EscapeCsv(info.GatewayAddress),
                        EscapeCsv(info.DnsServers),
                        EscapeCsv(info.InterfaceName),
                        EscapeCsv(info.MacAddress)
                    );

                    sb.AppendLine(line);

                    // Excel 等で直接開いても文字化けしないよう UTF-8 with BOM で保存
                    var encoding = new UTF8Encoding(true);
                    File.AppendAllText(filePath, sb.ToString(), encoding);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CSVロギング失敗: {ex.Message}");
            }
        }

        public static void OpenLogFolder()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = LogDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ログフォルダを開けませんでした: {ex.Message}");
            }
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\r") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return $"\"{value}\"";
        }
    }
}
