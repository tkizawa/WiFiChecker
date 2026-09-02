using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace WiFiChecker.Setup
{
    public partial class SetupWindow : Window
    {
        private bool _isInstalled = false;

        public SetupWindow()
        {
            InitializeComponent();
            InstallPathBox.Text = InstallerEngine.GetDefaultInstallDir();
        }

        private void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "インストール先フォルダーを選択してください",
                InitialDirectory = InstallPathBox.Text
            };

            if (dialog.ShowDialog() == true)
            {
                InstallPathBox.Text = dialog.FolderName;
            }
        }

        private async void OnInstallClick(object sender, RoutedEventArgs e)
        {
            if (_isInstalled)
            {
                // 完了画面の [完了] ボタン押下時
                if (LaunchAfterCheck.IsChecked == true)
                {
                    string targetExe = Path.Combine(InstallPathBox.Text.Trim(), InstallerEngine.MainExeName);
                    if (File.Exists(targetExe))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = targetExe,
                            UseShellExecute = true
                        });
                    }
                }
                this.Close();
                return;
            }

            string targetDir = InstallPathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                MessageBox.Show("インストール先フォルダーを指定してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // UIをStep 2 (進行状況) に切り替え
            Step1Grid.Visibility = Visibility.Collapsed;
            Step2Grid.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = false;
            NextButton.IsEnabled = false;

            var progress = new Progress<string>(msg =>
            {
                StatusText.Text = msg;
            });

            try
            {
                await InstallerEngine.InstallAsync(
                    targetDir,
                    DesktopShortcutCheck.IsChecked == true,
                    StartMenuShortcutCheck.IsChecked == true,
                    progress
                );

                // UIをStep 3 (完了) に切り替え
                Step2Grid.Visibility = Visibility.Collapsed;
                Step3Grid.Visibility = Visibility.Visible;

                NextButton.Content = "完了";
                NextButton.IsEnabled = true;
                CancelButton.Visibility = Visibility.Collapsed;
                _isInstalled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"インストール中にエラーが発生しました:\n{ex.Message}", "インストールエラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Step2Grid.Visibility = Visibility.Collapsed;
                Step1Grid.Visibility = Visibility.Visible;
                CancelButton.IsEnabled = true;
                NextButton.IsEnabled = true;
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
