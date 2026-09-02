using System;
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
            Settings = AppSettings.Load();
            LocalizationService.Instance.SetLanguage(Settings.Language);
        }
    }
}
