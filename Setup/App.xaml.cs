using System;
using System.Linq;
using System.Windows;

namespace WiFiChecker.Setup
{
    public partial class App : Application
    {
        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
            {
                bool isQuiet = e.Args.Contains("--quiet", StringComparer.OrdinalIgnoreCase);

                if (!isQuiet)
                {
                    var result = MessageBox.Show(
                        "Wi-Fi アクセスポイント チェッカー をコンピューターからアンインストールしますか？",
                        "アンインストールの確認",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (result != MessageBoxResult.Yes)
                    {
                        Shutdown();
                        return;
                    }
                }

                try
                {
                    var progress = new Progress<string>(msg => { });
                    await InstallerEngine.UninstallAsync(progress);

                    if (!isQuiet)
                    {
                        MessageBox.Show(
                            "Wi-Fi アクセスポイント チェッカー は正常にアンインストールされました。",
                            "アンインストール完了",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                    }
                }
                catch (Exception ex)
                {
                    if (!isQuiet)
                    {
                        MessageBox.Show(
                            $"アンインストール中にエラーが発生しました:\n{ex.Message}",
                            "アンインストールエラー",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }
                }

                Shutdown();
                return;
            }

            var setupWindow = new SetupWindow();
            setupWindow.Show();
        }
    }
}
