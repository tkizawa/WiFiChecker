using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WiFiChecker.Models;
using WiFiChecker.Services;

namespace WiFiChecker
{
    public partial class MainWindow : Window
    {
        public LocalizationService Loc => LocalizationService.Instance;

        private readonly WifiService _wifiService = new WifiService();
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private WifiInfo? _currentInfo;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            InitializeTimer();
            _ = RefreshWifiInfoAsync();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RestoreWindowPosition();
            ApplyLocalization();
        }

        private void RestoreWindowPosition()
        {
            var settings = App.Settings;

            if (settings.WindowWidth > 200 && settings.WindowHeight > 200)
            {
                this.Width = settings.WindowWidth;
                this.Height = settings.WindowHeight;
            }

            if (settings.WindowX >= 0 && settings.WindowY >= 0)
            {
                // 画面外に出ていないか確認
                if (settings.WindowX < SystemParameters.VirtualScreenWidth - 100 &&
                    settings.WindowY < SystemParameters.VirtualScreenHeight - 100)
                {
                    this.Left = settings.WindowX;
                    this.Top = settings.WindowY;
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                }
            }

            if (settings.IsMaximized)
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowPosition();
        }

        private void SaveWindowPosition()
        {
            var settings = App.Settings;

            if (this.WindowState == WindowState.Maximized)
            {
                settings.IsMaximized = true;
            }
            else
            {
                settings.IsMaximized = false;
                settings.WindowX = (int)this.Left;
                settings.WindowY = (int)this.Top;
                settings.WindowWidth = (int)this.Width;
                settings.WindowHeight = (int)this.Height;
            }

            settings.Save();
        }

        private void InitializeTimer()
        {
            int interval = App.Settings.AutoRefreshIntervalSeconds;
            if (interval < 1) interval = 5;

            _timer.Interval = TimeSpan.FromSeconds(interval);
            _timer.Tick += (s, e) => _ = RefreshWifiInfoAsync();

            if (AutoRefreshCheck.IsChecked == true)
            {
                _timer.Start();
            }
        }

        private async Task RefreshWifiInfoAsync()
        {
            var info = await _wifiService.GetCurrentWifiInfoAsync();
            _currentInfo = info;
            UpdateUi(info);
        }

        private void UpdateUi(WifiInfo info)
        {
            if (info.IsConnected)
            {
                StatusText.Text = Loc.ConnectedStatus;
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                SsidText.Text = string.IsNullOrEmpty(info.Ssid) ? "(Hidden SSID)" : info.Ssid;
            }
            else
            {
                StatusText.Text = Loc.DisconnectedStatus;
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                SsidText.Text = Loc.DisconnectedStatus;
            }

            BssidText.Text = info.Bssid;
            BandText.Text = $"{info.Band} (Ch {info.Channel})";
            SignalPercentText.Text = info.SignalQuality.ToString();
            SignalProgressBar.Value = info.SignalQuality;
            SignalDbmText.Text = $"{info.SignalDbm} dBm";

            PhyTypeText.Text = info.PhyType;
            ChannelDetailText.Text = info.FrequencyGhz > 0
                ? $"Ch {info.Channel} ({info.FrequencyGhz:F3} GHz)"
                : $"Ch {info.Channel}";

            SecurityText.Text = $"{info.Authentication} / {info.Cipher}";
            RxSpeedText.Text = $"{info.LinkSpeedRxMbps} Mbps";
            TxSpeedText.Text = $"{info.LinkSpeedTxMbps} Mbps";

            Ipv4Text.Text = info.Ipv4Address;
            SubnetText.Text = info.SubnetMask;
            GatewayText.Text = info.GatewayAddress;
            DnsText.Text = info.DnsServers;
            Ipv6Text.Text = info.Ipv6Address;
            AdapterText.Text = info.InterfaceName;

            LastUpdatedText.Text = $"{Loc.LabelLastUpdated}: {info.LastRefreshed:yyyy-MM-dd HH:mm:ss}";
        }

        private void ApplyLocalization()
        {
            AppTitleText.Text = Loc.AppTitle;
            HeaderSubtitleText.Text = Loc.HeaderSubTitle;
            BtnRefreshText.Text = Loc.RefreshText;
            AutoRefreshLabelText.Text = Loc.AutoRefreshText;
            BtnCopyText.Text = Loc.CopyAllText;

            LabelSignalText.Text = Loc.LabelSignal;
            SectionWirelessText.Text = Loc.SectionWirelessSpecs;
            LabelPhyTypeText.Text = Loc.LabelPhyType;
            LabelChannelText.Text = Loc.LabelChannel;
            LabelSecurityText.Text = Loc.LabelAuth;
            LabelRxSpeedText.Text = Loc.IsJapanese ? "受信速度 (Rx)" : "Rx Speed";
            LabelTxSpeedText.Text = Loc.IsJapanese ? "送信速度 (Tx)" : "Tx Speed";

            SectionNetworkText.Text = Loc.SectionNetwork;
            LabelIpv4Text.Text = Loc.LabelIpv4;
            LabelSubnetText.Text = Loc.LabelSubnet;
            LabelGatewayText.Text = Loc.LabelGateway;
            LabelDnsText.Text = Loc.LabelDns;
            LabelIpv6Text.Text = Loc.LabelIpv6;
            LabelAdapterText.Text = Loc.LabelAdapter;

            SettingsModalTitle.Text = Loc.SectionSettings;
            LabelLanguageSettingText.Text = Loc.LabelLanguageSetting;
            BtnCloseText.Text = Loc.BtnClose;

            this.Title = Loc.AppTitle;

            if (_currentInfo != null)
            {
                UpdateUi(_currentInfo);
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await RefreshWifiInfoAsync();
        }

        private void OnAutoRefreshCheckChanged(object sender, RoutedEventArgs e)
        {
            if (AutoRefreshCheck.IsChecked == true)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        }

        private void OnIntervalChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IntervalCombo.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int sec))
            {
                App.Settings.AutoRefreshIntervalSeconds = sec;
                App.Settings.Save();
                _timer.Interval = TimeSpan.FromSeconds(sec);
            }
        }

        private void OnCopyAllClick(object sender, RoutedEventArgs e)
        {
            if (_currentInfo != null)
            {
                string text = _currentInfo.ToFormattedText(Loc.IsJapanese);
                Clipboard.SetText(text);

                NotificationText.Text = Loc.CopiedMessage;
                NotificationBorder.Visibility = Visibility.Visible;

                var toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                toastTimer.Tick += (s, args) =>
                {
                    NotificationBorder.Visibility = Visibility.Collapsed;
                    toastTimer.Stop();
                };
                toastTimer.Start();
            }
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            string currentLang = App.Settings.Language;
            foreach (ComboBoxItem item in LangSettingCombo.Items)
            {
                if (item.Tag?.ToString() == currentLang)
                {
                    LangSettingCombo.SelectedItem = item;
                    break;
                }
            }

            SettingsModal.Visibility = Visibility.Visible;
        }

        private void OnCloseSettingsClick(object sender, RoutedEventArgs e)
        {
            SettingsModal.Visibility = Visibility.Collapsed;
        }

        private void OnLangSettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LangSettingCombo.SelectedItem is ComboBoxItem item)
            {
                string langCode = item.Tag?.ToString() ?? "auto";
                if (App.Settings.Language != langCode)
                {
                    App.Settings.Language = langCode;
                    App.Settings.Save();
                    Loc.SetLanguage(langCode);
                    ApplyLocalization();
                }
            }
        }
    }
}
