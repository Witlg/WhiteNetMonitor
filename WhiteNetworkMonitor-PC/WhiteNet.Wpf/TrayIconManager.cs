using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WhiteNet
{
    public class TrayIconManager : IDisposable
    {
        private NotifyIcon _ni;
        private Icon _current;
        private bool _online;
        private bool _initialized;

        public event Action OnOpen;
        public event Action OnRefresh;
        public event Action OnExit;

        public void Init()
        {
            _ni = new NotifyIcon
            {
                Text = "White Network Monitor",
                Visible = true
            };
            _ni.Click += (s, e) =>
            {
                if (e is MouseEventArgs me && me.Button == MouseButtons.Left)
                    OnOpen?.Invoke();
            };

            RebuildMenu();
            Loc.Changed += RebuildMenu;

            _initialized = true;
            SetOnline(false);
        }

        private void RebuildMenu()
        {
            if (_ni == null) return;

            var menu = new ContextMenuStrip();
            menu.Items.Add(Loc.T("tray.open"), null, (_, __) => OnOpen?.Invoke());
            menu.Items.Add(Loc.T("tray.refresh"), null, (_, __) => OnRefresh?.Invoke());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Loc.T("tray.exit"), null, (_, __) => OnExit?.Invoke());
            _ni.ContextMenuStrip = menu;

            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            if (_ni == null) return;
            _ni.Text = $"White Network Monitor — {Loc.T(_online ? "tray.online" : "tray.offline")}";
        }

        public void SetOnline(bool online)
        {
            if (!_initialized) return;

            if (online != _online || _current == null)
            {
                _online = online;

                var fresh = Draw(online);
                if (_ni != null) _ni.Icon = fresh;

                if (_current != null)
                {
                    var h = _current.Handle;
                    _current.Dispose();
                    DestroyIcon(h);
                }
                _current = fresh;
            }

            UpdateTooltip();
        }

        private static Icon Draw(bool online)
        {
            using (var bmp = new Bitmap(64, 64))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                bool artDrawn = false;

                try
                {
                    var res = System.Windows.Application.GetResourceStream(
                        new Uri("pack://application:,,,/Assets/icon.png"));
                    if (res != null)
                    {
                        using (var ms = new System.IO.MemoryStream())
                        {
                            res.Stream.CopyTo(ms);
                            ms.Position = 0;
                            using (var src = new Bitmap(ms))
                            {
                                using (var path = new GraphicsPath())
                                {
                                    float r = 14f;
                                    path.AddArc(1, 1, r, r, 180, 90);
                                    path.AddArc(63 - r, 1, r, r, 270, 90);
                                    path.AddArc(63 - r, 63 - r, r, r, 0, 90);
                                    path.AddArc(1, 63 - r, r, r, 90, 90);
                                    path.CloseFigure();
                                    g.SetClip(path);
                                }
                                g.DrawImage(src, new Rectangle(1, 1, 62, 62));
                                g.ResetClip();
                                artDrawn = true;
                            }
                        }
                    }
                }
                catch { artDrawn = false; }

                if (!artDrawn)
                {
                    using (var bgBrush = new LinearGradientBrush(
                        new Rectangle(0, 0, 64, 64), Color.FromArgb(255, 157, 107, 255),
                        Color.FromArgb(255, 84, 33, 158), 45f))
                    {
                        g.FillEllipse(bgBrush, 1, 1, 62, 62);
                    }
                    using (var f = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("WT", f, Brushes.White, new RectangleF(0, 5, 64, 26), sf);
                    }
                    using (var pen = new Pen(Color.FromArgb(235, 240, 235, 255), 2.6f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    })
                    {
                        const float cx = 32f, cy = 46f;
                        foreach (float r in new[] { 6f, 11.5f, 17f })
                            g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, 215, 130);
                        g.FillEllipse(Brushes.White, cx - 3, cy - 3, 6, 6);
                    }
                }

                using (var ring = new Pen(Color.FromArgb(80, 255, 255, 255), 1.6f))
                {
                    g.DrawEllipse(ring, 2, 2, 60, 60);
                }

                var badge = online ? Color.FromArgb(255, 42, 160, 67)
                                   : Color.FromArgb(255, 218, 54, 51);
                using (var b = new SolidBrush(badge))
                {
                    g.FillEllipse(b, 44, 44, 18, 18);
                }
                using (var dark = new Pen(Color.FromArgb(120, 10, 8, 24), 2f))
                {
                    g.DrawEllipse(dark, 44, 44, 18, 18);
                }

                using (var w = new Pen(Color.White, 2.3f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                })
                {
                    if (online)
                    {
                        g.DrawLine(w, 48.4f, 53.4f, 51.6f, 56.6f);
                        g.DrawLine(w, 51.6f, 56.6f, 57.6f, 50.0f);
                    }
                    else
                    {
                        g.DrawLine(w, 48.6f, 48.6f, 57.4f, 57.4f);
                        g.DrawLine(w, 57.4f, 48.6f, 48.6f, 57.4f);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public void Dispose()
        {
            if (!_initialized) return;
            Loc.Changed -= RebuildMenu;
            _ni.Visible = false;
            _ni.Dispose();
            _ni = null;
            if (_current != null)
            {
                var h = _current.Handle;
                _current.Dispose();
                DestroyIcon(h);
                _current = null;
            }
            _initialized = false;
        }
    }
}
