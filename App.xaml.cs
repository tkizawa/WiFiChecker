using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WiFiChecker.Models;
using WiFiChecker.Services;

namespace WiFiChecker
{
    public partial class App : Application
    {
        public static AppSettings Settings { get; private set; } = new AppSettings();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 未処理例外の捕捉とクラッシュロギングを設定
            SetupExceptionHandling();

            // Shift_JIS (CP932) や OEM コードページを扱えるようプロバイダーを登録
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Settings = AppSettings.Load();
            LocalizationService.Instance.SetLanguage(Settings.Language);
        }

        private void SetupExceptionHandling()
        {
            // 1. WPF UIスレッドでの未処理例外
            this.DispatcherUnhandledException += (sender, args) =>
            {
                LogCrash("DispatcherUnhandledException", args.Exception);
                // 可能であればアプリケーションの突然死を防止
                args.Handled = true;
            };

            // 2. バックグラウンドスレッド等 AppDomain 全体の未処理例外
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    LogCrash("AppDomain.UnhandledException", ex, args.IsTerminating);
                }
                else
                {
                    LogCrash("AppDomain.UnhandledException", new Exception($"Non-exception object: {args.ExceptionObject}"), args.IsTerminating);
                }
            };

            // 3. 非同期タスク (Task) の未監視例外
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                LogCrash("TaskScheduler.UnobservedTaskException", args.Exception);
                args.SetObserved();
            };
        }

        public static void LogCrash(string source, Exception ex, bool isTerminating = false)
        {
            try
            {
                string logDir = CsvLoggerService.LogDirectory;
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string logFilePath = Path.Combine(logDir, $"crash_{DateTime.Now:yyyy-MM-dd}.log");
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"[CRASH REPORT] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"Source: {source} (IsTerminating: {isTerminating})");
                sb.AppendLine($"OS Architecture: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}, Process: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}");
                sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"Stack Trace:\n{ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    sb.AppendLine($"Inner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                    sb.AppendLine($"Inner Stack Trace:\n{ex.InnerException.StackTrace}");
                }
                sb.AppendLine("================================================================================\n");

                File.AppendAllText(logFilePath, sb.ToString(), Encoding.UTF8);
                Debug.WriteLine(sb.ToString());
            }
            catch (Exception writeEx)
            {
                Debug.WriteLine($"クラッシュログ保存失敗: {writeEx.Message}");
            }
        }
    }
}

