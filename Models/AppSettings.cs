using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WiFiChecker.Models
{
    /// <summary>
    /// アプリケーション設定クラス
    /// AppData\Local\WiFiChecker\config.json に保存されます
    /// </summary>
    public class AppSettings
    {
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        public int WindowWidth { get; set; } = 920;
        public int WindowHeight { get; set; } = 680;
        public bool IsMaximized { get; set; } = false;

        public int AutoRefreshIntervalSeconds { get; set; } = 5;
        public string Language { get; set; } = "auto"; // "auto", "ja-JP", "en-US"
        public string Theme { get; set; } = "Default";  // "Default", "Dark", "Light"

        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WiFiChecker"
        );

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "config.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 日本語をそのままUTF-8テキストとして保持
        };

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定ファイルの読み込み失敗: {ex.Message}");
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                string json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定ファイルの保存失敗: {ex.Message}");
            }
        }
    }
}
