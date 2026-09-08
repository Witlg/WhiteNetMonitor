using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace WhiteNetUninstall
{
    internal static class Program
    {
        private const string AppName = "White Network Monitor";

        [STAThread]
        private static int Main(string[] args)
        {
            bool silent = args != null && Array.IndexOf(args, "/SILENT") >= 0;

            try { KillApps(); } catch { }
            try { RemoveShortcuts(); } catch { }
            bool restoredIcon = false;
            try { restoredIcon = RemoveRegistry(); } catch { }
            SelfDelete();

            if (!silent)
            {
                string msg = AppName + " удалён с компьютера.\n\n" +
                    (restoredIcon
                        ? "Стандартная сетевая иконка вернётся после перезапуска Проводника."
                        : "Все файлы и ярлыки очищены.");
                try
                {
                    System.Windows.Forms.MessageBox.Show(
                        msg, "White Team", System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                }
                catch { }
            }
            return 0;
        }

        private static void KillApps()
        {
            foreach (string name in new[] { "WhiteNetMonitor", "WhiteClient", "WhiteNetUninstall" })
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    if (p.Id == Process.GetCurrentProcess().Id) continue;
                    try { p.Kill(); p.WaitForExit(2000); } catch { }
                }
            }
        }

        private static void RemoveShortcuts()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

            foreach (string lnk in new[]
            {
                Path.Combine(desktop, AppName + ".lnk"),
                Path.Combine(programs, AppName + ".lnk"),
                Path.Combine(programs, "Uninstall " + AppName + ".lnk")
            })
            {
                try { if (File.Exists(lnk)) File.Delete(lnk); } catch { }
            }
        }

        private static bool RemoveRegistry()
        {
            using (var un = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
            {
                if (un != null) un.DeleteSubKeyTree("WhiteNetMonitor", false);
            }

            using (var run = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (run != null && run.GetValue("WhiteNetMonitor") != null)
                    run.DeleteValue("WhiteNetMonitor");
            }

            bool hideSet = false;
            using (var own = Registry.CurrentUser.OpenSubKey(@"Software\WhiteNetMonitor"))
                hideSet = own?.GetValue("HideIconSet") as string == "1";

            if (hideSet)
            {
                using (var pol = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                    pol.SetValue("HideSCANetwork", 0, RegistryValueKind.DWord);
            }

            using (var root = Registry.CurrentUser.OpenSubKey("Software", true))
            {
                if (root != null) root.DeleteSubKeyTree("WhiteNetMonitor", false);
            }
            return hideSet;
        }

        private static void SelfDelete()
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                var psi = new ProcessStartInfo("cmd.exe",
                    "/C ping -n 2 127.0.0.1 > nul & rmdir /s /q \"" + dir + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch { }
        }
    }
}
