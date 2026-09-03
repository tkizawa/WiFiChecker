using System;
using System.Text;
using System.Windows;
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

            // Shift_JIS (CP932) や OEM コードページを扱えるようプロバイダーを登録
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Settings = AppSettings.Load();
            LocalizationService.Instance.SetLanguage(Settings.Language);
        }
    }
}

