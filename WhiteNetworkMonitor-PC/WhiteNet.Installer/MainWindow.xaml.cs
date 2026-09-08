using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace WhiteNetInstaller
{
    public partial class MainWindow : Window
    {
        private const string AppName = "White Network Monitor";
        private static readonly string DefaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "WhiteNetMonitor");

        private int _page;
        private bool _busy;

        public MainWindow()
        {
            InitializeComponent();
            DirBox.Text = DefaultDir;

            var args = Environment.GetCommandLineArgs();
            if (args != null && Array.IndexOf(args, "/SILENT") >= 0)
            {
                Visibility = Visibility.Hidden;
                RunSilentAsync();
            }
        }

        private void ShowPage(int p)
        {
            if (p == 2) return;

            _page = p;
            PageWelcome.Visibility = p == 0 ? Visibility.Visible : Visibility.Collapsed;
            PageOptions.Visibility = p == 1 ? Visibility.Visible : Visibility.Collapsed;
            PageFinish.Visibility = p == 3 ? Visibility.Visible : Visibility.Collapsed;

            BtnBack.Visibility = p == 0 ? Visibility.Collapsed : Visibility.Visible;
            BtnBack.IsEnabled = p == 1;
            BtnNext.Content = p switch
            {
                0 => "Далее →",
                1 => "Установить",
                _ => "Готово"
            };

            string[] titles = { "Добро пожаловать!", "Параметры установки",
                                "Установка…", "Всё готово" };
            PageTitle.Text = titles[p];

            for (int i = 0; i < 4; i++)
            {
                var dot = (System.Windows.Shapes.Ellipse)FindName($"Step{i}D");
                var txt = (System.Windows.Controls.TextBlock)FindName($"Step{i}T");
                bool active = i == p || (p == 3 && i < 3);
                bool done = p == 3 && i <= 2;
                dot.Fill = active || done
                    ? (System.Windows.Media.Brush)FindResource("BrushAccent")
                    : new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                            .ConvertFromString("#3E2F5C"));
                txt.FontWeight = i == p ? FontWeights.SemiBold : FontWeights.Normal;
                txt.Foreground = i == p
                    ? (System.Windows.Media.Brush)FindResource("BrushText")
                    : (System.Windows.Media.Brush)FindResource("BrushDim");
            }
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;

            switch (_page)
            {
                case 0:
                    ShowPage(1);
                    break;

                case 1:
                    await InstallFlowAsync();
                    break;

                case 3:
                    try
                    {
                        if (OptRunNow.IsChecked == true)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = Path.Combine(InstallDir(), "WhiteNetMonitor.exe"),
                                UseShellExecute = true,
                                WorkingDirectory = InstallDir()
                            });
                        }
                    }
                    catch { }
                    Application.Current.Shutdown();
                    break;
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_page > 0 && !_busy) ShowPage(_page - 1);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_busy) Application.Current.Shutdown();
        }

        private void Root_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed &&
                e.OriginalSource is DependencyObject d && !IsInsideInteractive(d))
                DragMove();
        }

        private static bool IsInsideInteractive(DependencyObject d)
        {
            while (d != null)
            {
                if (d is System.Windows.Controls.Button ||
                    d is System.Windows.Controls.TextBox ||
                    d is System.Windows.Controls.CheckBox)
                    return true;
                d = System.Windows.Media.VisualTreeHelper.GetParent(d);
            }
            return false;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = DirBox.Text,
                Description = "Выбери папку для White Network Monitor"
            })
            {
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    DirBox.Text = dlg.SelectedPath;
            }
        }

        private string InstallDir() =>
            string.IsNullOrWhiteSpace(DirBox.Text) ? DefaultDir : DirBox.Text.Trim();

        private async Task InstallFlowAsync()
        {
            if (!ValidateDir()) return;
            _busy = true;
            BtnNext.IsEnabled = false;
            BtnBack.IsEnabled = false;

            PageWelcome.Visibility = Visibility.Collapsed;
            PageOptions.Visibility = Visibility.Collapsed;
            PageProgress.Visibility = Visibility.Visible;
            PageFinish.Visibility = Visibility.Collapsed;
            PageTitle.Text = "Установка…";
            _page = 2;

            var progress = new Progress<(int pct, string stage, string detail)>(v =>
            {
                Bar.Value = v.pct;
                PctLabel.Text = v.pct + "%";
                StageLabel.Text = v.stage;
                DetailLabel.Text = v.detail ?? "";
            });

            string dir = InstallDir();
            Exception error = null;
            await Task.Run(() =>
            {
                try { DoInstall(dir, progress); }
                catch (Exception ex) { error = ex; }
            });

            if (error != null)
            {
                MessageBox.Show("Ошибка установки:\n" + error.Message,
                    "White Team", MessageBoxButton.OK, MessageBoxImage.Error);
                _busy = false;
                BtnNext.IsEnabled = true;
                ShowPage(1);
                return;
            }

            PageProgress.Visibility = Visibility.Collapsed;
            PageFinish.Visibility = Visibility.Visible;
            PageTitle.Text = "Всё готово";
            FinishNote.Text = OptHideIcon.IsChecked == true
                ? "Стандартная сетевая иконка скроется после перезапуска Проводника."
                : "";
            _page = 3;
            SetStepDots(3);
            BtnBack.Visibility = Visibility.Collapsed;
            BtnNext.Content = "Готово";
            BtnNext.IsEnabled = true;
            _busy = false;
        }

        private bool ValidateDir()
        {
            string dir = InstallDir();
            if (dir.Length < 4 || !Path.IsPathRooted(dir) || dir.Contains("*"))
            {
                MessageBox.Show("Похоже, это не папка. Выбери нормальный путь.",
                    "White Team", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void DoInstall(string dir,
            IProgress<(int, string, string)> progress)
        {
            Directory.CreateDirectory(dir);

            void Report(int pct, string stage, string detail) =>
                progress.Report((pct, stage, detail));

            Report(5, "Останавливаю запущенные копии…", dir);
            foreach (string name in new[] { "WhiteNetMonitor", "WhiteClient" })
                foreach (var p in Process.GetProcessesByName(name))
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
            System.Threading.Thread.Sleep(300);

            Report(20, "Извлекаю файлы…", "WhiteNetMonitor.exe");
            Extract("payload.monitor.exe", Path.Combine(dir, "WhiteNetMonitor.exe"));

            Report(40, "Извлекаю файлы…", "Uninstall.exe");
            Extract("payload.uninstall.exe", Path.Combine(dir, "Uninstall.exe"));

            Report(50, "Извлекаю файлы…", "WhiteClient.exe");
            Extract("payload.client.exe", Path.Combine(dir, "WhiteClient.exe"));

            Report(60, "Создаю ярлыки…", "");
            CreateShortcut(
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"),
                Path.Combine(dir, "WhiteNetMonitor.exe"), dir, AppName);
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            CreateShortcut(Path.Combine(programs, AppName + ".lnk"),
                Path.Combine(dir, "WhiteNetMonitor.exe"), dir, AppName);
            CreateShortcut(Path.Combine(programs, "Uninstall " + AppName + ".lnk"),
                Path.Combine(dir, "Uninstall.exe"), dir, "Удалить " + AppName);

            Report(80, "Регистрирую программу…", "");
            using (var key = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WhiteNetMonitor"))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", "2.0.0");
                key.SetValue("Publisher", "White Team");
                key.SetValue("DisplayIcon", Path.Combine(dir, "WhiteNetMonitor.exe"));
                key.SetValue("UninstallString", "\"" + Path.Combine(dir, "Uninstall.exe") + "\"");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                long size = 0;
                try { size = new FileInfo(Path.Combine(dir, "WhiteNetMonitor.exe")).Length / 1024; } catch { }
                key.SetValue("EstimatedSize", (int)size, RegistryValueKind.DWord);
            }

            if (OptAutostart.IsChecked == true)
            {
                using (var run = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run"))
                    run.SetValue("WhiteNetMonitor", "\"" +
                        Path.Combine(dir, "WhiteNetMonitor.exe") + "\"");
            }
            else
            {
                using (var run = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    if (run?.GetValue("WhiteNetMonitor") != null)
                        run.DeleteValue("WhiteNetMonitor");
            }

            if (OptHideIcon.IsChecked == true)
            {
                using (var pol = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                    pol.SetValue("HideSCANetwork", 1, RegistryValueKind.DWord);
                using (var own = Registry.CurrentUser.CreateSubKey(@"Software\WhiteNetMonitor"))
                    own.SetValue("HideIconSet", "1");
            }

            System.Threading.Thread.Sleep(400);
            Report(100, "Готово!", dir);
        }

        private static void Extract(string logicalName, string targetPath)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var src = asm.GetManifestResourceStream(logicalName))
            {
                if (src == null)
                    throw new InvalidOperationException("payload not found: " + logicalName);
                using (var dst = File.Create(targetPath))
                    src.CopyTo(dst);
            }
        }

        private static void CreateShortcut(string lnkPath, string target,
            string workDir, string description)
        {
            try
            {
                object shell = Activator.CreateInstance(
                    Type.GetTypeFromProgID("WScript.Shell"));
                dynamic lnk = ((dynamic)shell).CreateShortcut(lnkPath);
                lnk.TargetPath = target;
                lnk.WorkingDirectory = workDir;
                lnk.Description = description;
                lnk.IconLocation = target + ",0";
                lnk.Save();
            }
            catch { }
        }

        private void SetStepDots(int current)
        {
            for (int i = 0; i < 4; i++)
            {
                var dot = (System.Windows.Shapes.Ellipse)FindName($"Step{i}D");
                var txt = (System.Windows.Controls.TextBlock)FindName($"Step{i}T");
                if (i <= current)
                {
                    dot.Fill = (System.Windows.Media.Brush)FindResource("BrushAccent");
                    txt.Foreground = i == current
                        ? System.Windows.Media.Brushes.White
                        : (System.Windows.Media.Brush)FindResource("BrushText");
                }
            }
        }

        private async void RunSilentAsync()
        {
            var progress = new Progress<(int, string, string)>(_ => { });
            await Task.Run(() => DoInstall(DefaultDir, progress));
            Application.Current.Shutdown();
        }
    }
}
