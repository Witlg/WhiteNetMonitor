using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace WhiteNet
{
    public partial class App : Application
    {
        private static Mutex _mutex;
        public TrayIconManager Tray { get; private set; }
        public MainWindow Shell { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool createdNew;
            _mutex = new Mutex(true, "WhiteNetMonitor_SingleInstance", out createdNew);
            if (!createdNew)
            {
                Shutdown();
                return;
            }

            Tray = new TrayIconManager();
            Tray.Init();
            Tray.OnOpen += () => Shell?.ShowFromTray();
            Tray.OnRefresh += () => Shell?.ManualRefreshAsync();
            Tray.OnExit += () =>
            {
                Tray.Dispose();
                Current.Shutdown();
            };

            Shell = new MainWindow(Tray);

            LaunchCompanion();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("WhiteClient"))
                    p.Kill();
            }
            catch { }
            base.OnExit(e);
        }

        private static void LaunchCompanion()
        {
            try
            {
                if (Process.GetProcessesByName("WhiteClient").Length > 0) return;

                string exeDir = System.IO.Path.GetDirectoryName(
                    Process.GetCurrentProcess().MainModule.FileName);
                string[] candidates =
                {
                    System.IO.Path.Combine(exeDir ?? "", "WhiteClient.exe"),
                    @"C:\Users\White\Desktop\2\dist\WhiteClient.exe"
                };

                string client = null;
                foreach (var c in candidates)
                {
                    if (!string.IsNullOrEmpty(c) && System.IO.File.Exists(c))
                    {
                        client = c;
                        break;
                    }
                }
                if (client == null) return;

                var psi = new ProcessStartInfo(client)
                {
                    UseShellExecute = true,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(client),
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
            }
            catch { }
        }
    }
}
