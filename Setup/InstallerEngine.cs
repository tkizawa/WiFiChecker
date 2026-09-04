using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace WiFiChecker.Setup
{
    public class InstallerEngine
    {
        public const string AppName = "WiFiChecker";
        public const string DisplayName = "Wi-Fi アクセスポイント チェッカー";
        public const string DisplayNameEn = "Wi-Fi Access Point Checker";
        public const string Publisher = "tkizawa";
        public const string Version = "1.0.1.0";
        public const string MainExeName = "WiFiChecker.exe";
        public const string UninstallExeName = "Uninstall.exe";

        public static string GetDefaultInstallDir()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Programs", AppName);
        }

        public static async Task InstallAsync(
            string targetDir,
            bool createDesktopShortcut,
            bool createStartMenuShortcut,
            IProgress<string> progress)
        {
            await Task.Run(() =>
            {
                // 1. ディレクトリ作成
                progress.Report("インストール先フォルダーを準備しています...");
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // 2. 埋め込みリソースから payload.zip を抽出・展開
                progress.Report("アプリケーションファイルを展開しています...");
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException("インストール用リソース (payload.zip) が見つかりません。");

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) throw new InvalidOperationException("リソースの読み込みに失敗しました。");
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                    archive.ExtractToDirectory(targetDir, overwriteFiles: true);
                }

                // 3. 自分自身を Uninstall.exe として配置
                progress.Report("アンインストーラーを配置しています...");
                string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string targetUninstallPath = Path.Combine(targetDir, UninstallExeName);
                if (File.Exists(currentExePath))
                {
                    try
                    {
                        File.Copy(currentExePath, targetUninstallPath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Uninstall.exe コピー失敗: {ex.Message}");
                    }
                }

                string mainExePath = Path.Combine(targetDir, MainExeName);

                // 4. ショートカットの作成
                if (createDesktopShortcut)
                {
                    progress.Report("デスクトップショートカットを作成しています...");
                    string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string shortcutPath = Path.Combine(desktopDir, $"{DisplayName}.lnk");
                    CreateShortcut(shortcutPath, mainExePath, targetDir, DisplayName);
                }

                if (createStartMenuShortcut)
                {
                    progress.Report("スタートメニューショートカットを作成しています...");
                    string startMenuPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                    string shortcutPath = Path.Combine(startMenuPrograms, $"{DisplayName}.lnk");
                    CreateShortcut(shortcutPath, mainExePath, targetDir, DisplayName);
                }

                // 5. Windows の「プログラムと機能」アンインストール登録
                progress.Report("Windows にアプリケーション情報を登録しています...");
                RegisterUninstall(targetDir, mainExePath, targetUninstallPath);

                progress.Report("完了");
            });
        }

        public static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.Description = description;
                    shortcut.IconLocation = targetPath + ",0";
                    shortcut.Save();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ショートカット作成失敗: {ex.Message}");
            }
        }

        public static void RegisterUninstall(string targetDir, string mainExePath, string targetUninstallPath)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}");
                if (key != null)
                {
                    key.SetValue("DisplayName", DisplayName);
                    key.SetValue("DisplayVersion", Version);
                    key.SetValue("Publisher", Publisher);
                    key.SetValue("DisplayIcon", $"{mainExePath},0");
                    key.SetValue("InstallLocation", targetDir);
                    key.SetValue("UninstallString", $"\"{targetUninstallPath}\" --uninstall");
                    key.SetValue("QuietUninstallString", $"\"{targetUninstallPath}\" --uninstall --quiet");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"レジストリ登録失敗: {ex.Message}");
            }
        }

        public static async Task UninstallAsync(IProgress<string> progress)
        {
            await Task.Run(() =>
            {
                progress.Report("実行中のプロセスを終了しています...");
                foreach (var p in Process.GetProcessesByName(AppName))
                {
                    try { p.Kill(); p.WaitForExit(2000); } catch { }
                }

                // 1. ショートカット削除
                progress.Report("ショートカットを削除しています...");
                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string desktopShortcut = Path.Combine(desktopDir, $"{DisplayName}.lnk");
                if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

                string startMenuPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string startMenuShortcut = Path.Combine(startMenuPrograms, $"{DisplayName}.lnk");
                if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);

                // 2. レジストリ登録解除
                progress.Report("アンインストール情報を削除しています...");
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}", throwOnMissingSubKey: false);
                }
                catch { }

                // 3. インストールフォルダの自己削除バッチ起動
                progress.Report("インストールフォルダーをクリーンアップしています...");
                string currentDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                string tempBatch = Path.Combine(Path.GetTempPath(), $"uninstall_{Guid.NewGuid():N}.bat");
                
                // バッチファイルでプロセス終了を待ってからディレクトリ削除
                string batchScript = $@"@echo off
timeout /t 2 /nobreak > nul
rmdir /s /q ""{currentDir}""
del ""{tempBatch}""
";
                File.WriteAllText(tempBatch, batchScript);

                var psi = new ProcessStartInfo
                {
                    FileName = tempBatch,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            });
        }
    }
}
