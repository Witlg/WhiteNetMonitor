using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WhiteNet
{
    public partial class MainWindow : Window
    {
        private readonly TrayIconManager _tray;
        private readonly DispatcherTimer _uiTimer;
        private CancellationTokenSource _netLoopCts;

        private bool _online;
        private bool _langSyncing;
        private bool _busyNet;

        private bool _pingBusy;
        private long? _pingMs;

        public MainWindow(TrayIconManager tray)
        {
            InitializeComponent();
            _tray = tray;

            try
            {
                var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(
                    new Uri("pack://application:,,,/app.ico"),
                    System.Windows.Media.Imaging.BitmapCreateOptions.None,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                Icon = decoder.Frames[decoder.Frames.Count - 1];
            }
            catch { }

            TabNet.Checked += (_, __) => SwitchTab(true, false, false);
            TabSys.Checked += (_, __) => SwitchTab(false, true, false);
            TabSet.Checked += (_, __) => SwitchTab(false, false, true);

            LangRu.Checked += (_, __) => { if (!_langSyncing) Loc.Set("ru"); };
            LangEn.Checked += (_, __) => { if (!_langSyncing) Loc.Set("en"); };

            Loaded += (_, __) =>
            {
                OsValue.Text = SystemMetrics.OsString();
                InitSpeedBaseline();
                StartNetLoop();
            };

            Closing += (_, e) => { e.Cancel = true; HideToTray(); };

            StartupCheck.IsChecked = StartupHelper.IsRegistered();

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uiTimer.Tick += (_, __) => UpdateLive();
            _uiTimer.Start();

            ApplyLang();
            Loc.Changed += ApplyLang;
            UpdateLive();
        }

        private void ApplyLang()
        {
            TabNet.Content = Loc.T("tab.net");
            TabSys.Content = Loc.T("tab.sys");
            TabSet.Content = Loc.T("tab.set");

            UpdateHint();
            IpCaption.Text  = Loc.T("ip.cap");
            DlCaption.Text  = Loc.T("dl.cap");
            UlCaption.Text  = Loc.T("ul.cap");
            DlSub.Text      = Loc.T("sp.sub");
            UlSub.Text      = Loc.T("sp.sub");
            PingCaption.Text = Loc.T("p.cap");
            PingBtn.Content = Loc.T("btn.check");

            CpuCaption.Text  = Loc.T("cpu.cap");
            RamCaption.Text  = Loc.T("ram.cap");
            DiskCaption.Text = Loc.T("disk.cap");
            OsCaption.Text   = Loc.T("os.cap");
            UpCaption.Text   = Loc.T("up.cap");

            StartupCheck.Content = Loc.T("start.chk");

            LangCaption.Text = Loc.T("set.lang");
            LangNote.Text    = Loc.T("set.note");
            LangRuSub.Text   = Loc.T("lang.ru.sub");
            LangEnSub.Text   = Loc.T("lang.en.sub");
            AboutCaption.Text = Loc.T("about.cap");
            AboutLine2.Text   = Loc.T("about.l2");
            DonateBtn.Content = Loc.T("donate.btn");

            RefreshStatusText();
            RenderPing();

            _langSyncing = true;
            LangRu.IsChecked = Loc.Lang == "ru";
            LangEn.IsChecked = Loc.Lang == "en";
            _langSyncing = false;

            UpdateLive();
        }

        private void UpdateHint()
        {
            string hint = Loc.T(_online ? "net.hint.on" : "net.hint.off");
            StatusSub.Text = hint;
            DotWrap.ToolTip = hint;
        }

        private void RefreshStatusText()
        {
            StatusBig.Text = _online ? Loc.T("st.ok")
                            : StatusBig.Text == "…" ? Loc.T("st.check")
                            : Loc.T("st.fail");
            UpdateHint();
        }

        private void RenderPing()
        {
            if (_pingBusy) PingValue.Text = Loc.T("p.busy");
            else if (_pingMs == null) PingValue.Text = Loc.T("p.hint");
            else if (_pingMs < 0) PingValue.Text = Loc.T("p.fail");
            else PingValue.Text = Loc.F("p.val", _pingMs.Value);
        }

        public void ShowFromTray()
        {
            Show();
            Opacity = 0;
            BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            Activate();
        }

        private void HideToTray() => Hide();

        private void TitleBar_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnMin_Click(object sender, RoutedEventArgs e) => HideToTray();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => HideToTray();

        private void SwitchTab(bool net, bool sys, bool set)
        {
            PanelNet.Visibility = net ? Visibility.Visible : Visibility.Collapsed;
            PanelSys.Visibility = sys ? Visibility.Visible : Visibility.Collapsed;
            PanelSet.Visibility = set ? Visibility.Visible : Visibility.Collapsed;
        }

        private void StartNetLoop()
        {
            _netLoopCts?.Cancel();
            _netLoopCts = new CancellationTokenSource();
            var ct = _netLoopCts.Token;

            Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    bool online = ProbeInternet(ct);
                    string ip = GetLocalIpSafe();

                    await Dispatcher.InvokeAsync(() => ApplyNetState(online, ip));
                    _tray.SetOnline(online);

                    try { await Task.Delay(5000, ct); }
                    catch (TaskCanceledException) { break; }
                }
            }, ct);
        }

        public async void ManualRefreshAsync()
        {
            var ct = CancellationToken.None;
            bool online = await Task.Run(() => ProbeInternet(ct));
            string ip = await Task.Run(() => GetLocalIpSafe());
            ApplyNetState(online, ip);
            _tray.SetOnline(online);
            RunPing();
        }

        private static bool ProbeInternet(CancellationToken ct)
        {
            try
            {
                using (var cl = new TcpClient())
                {
                    var ar = cl.BeginConnect("8.8.8.8", 53, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(1500)) return false;
                    cl.EndConnect(ar);
                    return true;
                }
            }
            catch { return false; }
        }

        private static string GetLocalIpSafe()
        {
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork,
                                          SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Connect("8.8.8.8", 65530);
                    return (s.LocalEndPoint as System.Net.IPEndPoint)?.Address.ToString() ?? "—";
                }
            }
            catch { return "—"; }
        }

        private void ApplyNetState(bool online, string ip)
        {
            _online = online;
            DotOk.Visibility = online ? Visibility.Visible : Visibility.Hidden;
            DotFail.Visibility = online ? Visibility.Hidden : Visibility.Visible;
            StatusBig.Text = online ? Loc.T("st.ok") : Loc.T("st.fail");
            IpValue.Text = ip;
            UpdateHint();
        }

        private async void Dot_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_busyNet) return;
            bool turningOff = _online;

            if (turningOff)
            {
                var r = MessageBox.Show(Loc.T("msg.confirm"), "White Team",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }

            _busyNet = true;
            StatusBig.Text = Loc.T("st.busy");

            await Task.Run(() => ToggleInternet(turningOff));

            bool online = ProbeInternet(CancellationToken.None);
            ApplyNetState(online, GetLocalIpSafe());
            _tray.SetOnline(online);
            _busyNet = false;
        }

        private static void ToggleInternet(bool off)
        {
            try
            {
                string script = off
                    ? "Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Disable-NetAdapter -Confirm:$false"
                    : "Get-NetAdapter | Where-Object { $_.AdminStatus -eq 'Down' } | Enable-NetAdapter -Confirm:$false";
                string b64 = Convert.ToBase64String(
                    System.Text.Encoding.Unicode.GetBytes(script));

                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + b64)
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                    p?.WaitForExit(20000);
            }
            catch { }
        }

        private async void PingBtn_Click(object sender, RoutedEventArgs e) => RunPing();

        private async void RunPing()
        {
            if (_pingBusy) return;
            _pingBusy = true;
            _pingMs = null;
            RenderPing();
            PingBtn.IsEnabled = false;

            long ms = await Task.Run(() =>
            {
                try
                {
                    using (var p = new Ping())
                        return p.Send("8.8.8.8", 2000).RoundtripTime;
                }
                catch { return -1L; }
            });

            _pingBusy = false;
            _pingMs = ms <= 0 ? -1 : ms;
            RenderPing();
            PingBtn.IsEnabled = true;
        }

        private void InitSpeedBaseline() => NetSampler.Sample();

        private void UpdateLive()
        {
            NetSampler.Sample();
            SpeedDown.Text = Fmt.Speed(NetSampler.LastDownBps);
            SpeedUp.Text   = Fmt.Speed(NetSampler.LastUpBps);

            double cpu = SystemMetrics.CpuPercent();
            CpuBar.Value = cpu;
            CpuPct.Text = $"{cpu:0}%";

            var ram = SystemMetrics.Ram();
            RamBar.Value = ram.PercentUsed;
            RamPct.Text = $"{ram.PercentUsed:0}%";
            RamDetail.Text = Loc.F("ram.det", Fmt.Gb(ram.UsedGb), Fmt.Gb(ram.TotalGb));

            var disk = SystemMetrics.DiskC();
            DiskBar.Value = disk.PercentUsed;
            DiskPct.Text = $"{disk.PercentUsed:0}%";
            DiskDetail.Text = Loc.F("disk.det", Fmt.Gb(disk.UsedGb), Fmt.Gb(disk.FreeGb));

            UptimeValue.Text = SystemMetrics.UptimeString();
        }

        private void Startup_Changed(object sender, RoutedEventArgs e)
        {
            if (StartupCheck.IsChecked == null) return;
            StartupHelper.SetRegistered(StartupCheck.IsChecked.Value);
        }

        private void LangRu_Checked(object sender, RoutedEventArgs e)
        {
            if (!_langSyncing) Loc.Set("ru");
        }

        private void LangEn_Checked(object sender, RoutedEventArgs e)
        {
            if (!_langSyncing) Loc.Set("en");
        }

        private void DonateBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://yoomoney.ru/to/4100119622192067")
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    internal static class SystemMetrics
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern ulong GetTickCount64();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buf);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME { public uint DwLowDateTime; public uint DwHighDateTime; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint DwLength;
            public uint DwMemoryLoad;
            public ulong UllTotalPhys;
            public ulong UllAvailPhys;
            public ulong UllTotalPageFile;
            public ulong UllAvailPageFile;
            public ulong UllTotalVirtual;
            public ulong UllAvailVirtual;
            public ulong UllAvailExtendedVirtual;
        }

        private static ulong _prevIdle, _prevTotal;

        public static double CpuPercent()
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user))
                return 0;

            ulong i = ((ulong)idle.DwHighDateTime << 32) | idle.DwLowDateTime;
            ulong k = ((ulong)kernel.DwHighDateTime << 32) | kernel.DwLowDateTime;
            ulong u = ((ulong)user.DwHighDateTime << 32) | user.DwLowDateTime;
            ulong total = k + u;

            if (_prevTotal != 0)
            {
                ulong dTotal = total - _prevTotal;
                ulong dIdle = i - _prevIdle;
                if (dTotal > 0)
                {
                    double usage = 100.0 * (dTotal - dIdle) / dTotal;
                    _prevIdle = i; _prevTotal = total;
                    return Math.Max(0, Math.Min(100, usage));
                }
            }
            _prevIdle = i; _prevTotal = total;
            return 0;
        }

        public static (double UsedGb, double TotalGb, double PercentUsed) Ram()
        {
            var st = new MEMORYSTATUSEX();
            st.DwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (!GlobalMemoryStatusEx(ref st)) return (0, 0, 0);
            double total = st.UllTotalPhys / 1073741824.0;
            double used = (st.UllTotalPhys - st.UllAvailPhys) / 1073741824.0;
            return (used, total, total > 0 ? used / total * 100.0 : 0);
        }

        public static (double UsedGb, double FreeGb, double PercentUsed) DiskC()
        {
            try
            {
                var d = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(
                    Environment.SystemDirectory));
                double total = d.TotalSize / 1073741824.0;
                double free = d.AvailableFreeSpace / 1073741824.0;
                double used = total - free;
                return (used, free, total > 0 ? used / total * 100.0 : 0);
            }
            catch { return (0, 0, 0); }
        }

        public static string UptimeString()
        {
            var t = TimeSpan.FromMilliseconds(GetTickCount64());
            return t.TotalDays >= 1
                ? $"{t.Days} {Loc.T("u.d")} {t.Hours} {Loc.T("u.h")} {t.Minutes} {Loc.T("u.m")}"
                : $"{t.Hours} {Loc.T("u.h")} {t.Minutes} {Loc.T("u.m")}";
        }

        public static string OsString()
        {
            var v = Environment.OSVersion.Version;
            string name = v.Major >= 10 ? "Windows 10/11" : "Windows";
            return $"{name} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")} ({v})";
        }
    }

    internal static class NetSampler
    {
        private static long _prevRx, _prevTx;
        private static DateTime _prevTime = DateTime.MinValue;

        public static double LastDownBps { get; private set; }
        public static double LastUpBps { get; private set; }

        public static void Sample()
        {
            long rx = 0, tx = 0;
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    var st = nic.GetIPv4Statistics();
                    rx += st.BytesReceived;
                    tx += st.BytesSent;
                }
            }
            catch { return; }

            var now = DateTime.UtcNow;
            if (_prevTime != DateTime.MinValue)
            {
                var dt = (now - _prevTime).TotalSeconds;
                if (dt > 0.2)
                {
                    LastDownBps = Math.Max(0, rx - _prevRx) / dt;
                    LastUpBps = Math.Max(0, tx - _prevTx) / dt;
                }
            }
            _prevRx = rx; _prevTx = tx; _prevTime = now;
        }
    }

    internal static class Fmt
    {
        public static string Speed(double bps)
        {
            if (bps < 1024) return $"{bps:0} {Loc.T("sp.b")}";
            if (bps < 1024 * 1024) return $"{bps / 1024:0.#} {Loc.T("sp.k")}";
            return $"{bps / (1024 * 1024):0.##} {Loc.T("sp.m")}";
        }

        public static string Gb(double gb)
            => gb >= 1 ? $"{gb:0.#} {Loc.T("u.gb")}" : $"{gb * 1024:0} {Loc.T("u.mb")}";
    }

    internal static class StartupHelper
    {
        private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "WhiteNetMonitor";

        private static string ExePath()
        {
            using (var p = Process.GetCurrentProcess())
                return p.MainModule.FileName;
        }

        public static bool IsRegistered()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(KeyPath))
                    return k?.GetValue(ValueName) != null;
            }
            catch { return false; }
        }

        public static void SetRegistered(bool enable)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (enable) k.SetValue(ValueName, "\"" + ExePath() + "\"");
                    else if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName);
                }
            }
            catch { }
        }
    }
}
