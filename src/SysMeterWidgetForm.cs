using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SysMeter
{
    public class SysMeterWidgetForm : Form
    {
        private const string GitHubUrl = "https://github.com/grafigrow/SysMeter";
        private const string NetSpeedMeterUrl = "https://github.com/grafigrow/NetSpeedMeter";

        private SystemMonitor monitor;
        private AppSettings settings;
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private ToolTip toolTip;

        private double currentCpu;
        private double currentRam;
        private double currentDisk;
        private long usedRamBytes;
        private long totalRamBytes;

        private Point dragStartCursor;
        private Point dragStartLocation;
        private bool isDragging;

        private HistoryForm historyFormInstance;
        private MidnightReportForm activeMidnightPopup;
        private uint taskbarCreatedMessage;
        private Timer fullscreenCheckTimer;
        private bool isHiddenByFullscreen;
        private Icon currentDynamicTrayIcon;

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        public SysMeterWidgetForm(SystemMonitor sysMonitor, AppSettings appSettings)
        {
            monitor = sysMonitor;
            settings = appSettings;

            InitializeWidget();
            SetupTrayIcon();

            monitor.StatsUpdated += OnStatsUpdated;
            monitor.MidnightRollover += OnMidnightRollover;

            taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");

            fullscreenCheckTimer = new Timer();
            fullscreenCheckTimer.Interval = 1000;
            fullscreenCheckTimer.Tick += (s, e) => CheckFullscreen();
            fullscreenCheckTimer.Start();

            try
            {
                SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
                SystemEvents.SessionSwitch += OnSessionSwitch;
                SystemEvents.PowerModeChanged += OnPowerModeChanged;
            }
            catch { }
        }

        public static Icon LoadAppIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
            }
            catch { }

            try
            {
                Icon embedded = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (embedded != null) return embedded;
            }
            catch { }

            return SystemIcons.Application;
        }

        private float GetDpiScale()
        {
            IntPtr hwnd = IsHandleCreated ? Handle : IntPtr.Zero;
            return Math.Max(1.0f, NativeMethods.GetWindowDpiScale(hwnd));
        }

        private int S(float basePx, float scale)
        {
            return (int)Math.Round(basePx * scale);
        }

        private void InitializeWidget()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(24, 24, 28);
            Opacity = settings.WidgetOpacity >= 0.25 ? settings.WidgetOpacity : 0.95;
            DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            IntPtr unused = Handle;
            UpdateDimensions();

            if (settings.WidgetX >= 0 && settings.WidgetY >= 0)
            {
                Location = new Point(settings.WidgetX, settings.WidgetY);
            }
            else
            {
                ResetTaskbarPosition();
            }

            EnsureVisibleOnScreen();

            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 500;
            toolTip.ReshowDelay = 200;
            toolTip.BackColor = Color.FromArgb(28, 28, 34);
            toolTip.ForeColor = Color.White;
            toolTip.SetToolTip(this, "SysMeter (Taskbar System Monitor)\n• Drag: Move anywhere or snap to taskbar\n• Right-Click: Options & Metrics\n• Double-Click: Daily Usage Report");

            MouseDown += Widget_MouseDown;
            MouseMove += Widget_MouseMove;
            MouseUp += Widget_MouseUp;
            MouseEnter += (s, e) => EnsureTopmostZOrder();
            MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) OpenDailyReport();
            };

            BuildContextMenu();
        }

        private int GetVisibleMetricCount()
        {
            int c = 0;
            if (settings.ShowCpu) c++;
            if (settings.ShowRam) c++;
            if (settings.ShowDisk) c++;
            return Math.Max(1, c);
        }

        private void UpdateDimensions()
        {
            float scale = GetDpiScale();
            int count = GetVisibleMetricCount();
            if (settings.IsHorizontalLayout)
            {
                int widthBase = 16 + count * 68;
                Size = new Size(S(widthBase, scale), S(28, scale));
            }
            else
            {
                int widthBase = count >= 2 ? 148 : 88;
                int heightBase = count == 3 ? 38 : (count == 2 ? 36 : 26);
                Size = new Size(S(widthBase, scale), S(heightBase, scale));
            }
            Invalidate();
        }

        public void ResetTaskbarPosition()
        {
            float scale = GetDpiScale();
            NativeMethods.RECT tbRect;
            uint edge;
            if (NativeMethods.GetTaskbarPosition(out tbRect, out edge))
            {
                if (edge == NativeMethods.ABE_BOTTOM || edge == NativeMethods.ABE_TOP)
                {
                    int targetY = tbRect.Top + (tbRect.Height - Height) / 2;
                    int targetX = tbRect.Right - S(260, scale) - Width;
                    if (targetX < tbRect.Left + S(120, scale)) targetX = tbRect.Left + S(120, scale);
                    Location = new Point(targetX, targetY);
                }
                else
                {
                    int targetX = tbRect.Left + (tbRect.Width - Width) / 2;
                    int targetY = tbRect.Bottom - S(160, scale) - Height;
                    Location = new Point(targetX, targetY);
                }
            }
            else
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(wa.Right - Width - S(20, scale), wa.Bottom - Height - S(12, scale));
            }

            settings.WidgetX = Location.X;
            settings.WidgetY = Location.Y;
            DataManager.SaveSettings(settings);
            EnsureTopmostZOrder();
        }

        private void EnsureVisibleOnScreen()
        {
            Rectangle bounds = Bounds;
            bool visibleOnAny = false;
            foreach (Screen scr in Screen.AllScreens)
            {
                if (scr.Bounds.IntersectsWith(bounds))
                {
                    visibleOnAny = true;
                    break;
                }
            }

            if (!visibleOnAny)
            {
                ResetTaskbarPosition();
            }
        }

        private void EnsureTopmostZOrder()
        {
            if (!settings.IsTopMost || !Visible || isHiddenByFullscreen) return;
            try
            {
                NativeMethods.SetWindowPos(
                    Handle,
                    NativeMethods.HWND_TOPMOST,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE
                );
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (taskbarCreatedMessage != 0 && m.Msg == (int)taskbarCreatedMessage)
            {
                if (settings.SnapToTaskbar) ResetTaskbarPosition();
                EnsureTopmostZOrder();
            }
            else if (m.Msg == NativeMethods.WM_DPICHANGED)
            {
                UpdateDimensions();
                if (settings.SnapToTaskbar) ResetTaskbarPosition();
                Invalidate();
            }
            base.WndProc(ref m);
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => OnDisplaySettingsChanged(sender, e)));
                return;
            }
            UpdateDimensions();
            EnsureVisibleOnScreen();
            if (settings.SnapToTaskbar) ResetTaskbarPosition();
            EnsureTopmostZOrder();
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                monitor.ResetBaseline();
                BeginInvoke((Action)(() => EnsureTopmostZOrder()));
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                monitor.ResetBaseline();
                BeginInvoke((Action)(() => EnsureTopmostZOrder()));
            }
        }

        private void CheckFullscreen()
        {
            if (!settings.AutoHideInFullscreen)
            {
                if (isHiddenByFullscreen)
                {
                    isHiddenByFullscreen = false;
                    if (settings.IsWidgetVisible && !Visible) Show();
                }
                return;
            }

            bool full = NativeMethods.IsForegroundWindowFullscreen();
            if (full && !isHiddenByFullscreen)
            {
                isHiddenByFullscreen = true;
                if (Visible) Hide();
            }
            else if (!full && isHiddenByFullscreen)
            {
                isHiddenByFullscreen = false;
                if (settings.IsWidgetVisible && !Visible)
                {
                    Show();
                    EnsureTopmostZOrder();
                }
            }
        }

        private void OnStatsUpdated(object sender, SystemStatsEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => OnStatsUpdated(sender, e)));
                return;
            }

            currentCpu = e.CpuPercent;
            currentRam = e.RamPercent;
            currentDisk = e.DiskPercent;
            usedRamBytes = e.UsedRamBytes;
            totalRamBytes = e.TotalRamBytes;

            UpdateTrayStatus();
            Invalidate();
        }

        private void OnMidnightRollover(object sender, DailySystemRecord completedDay)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => OnMidnightRollover(sender, completedDay)));
                return;
            }

            if (settings.ShowMidnightPopup)
            {
                try
                {
                    if (activeMidnightPopup != null && !activeMidnightPopup.IsDisposed)
                    {
                        activeMidnightPopup.Close();
                    }
                    activeMidnightPopup = new MidnightReportForm(completedDay, OpenDailyReport, settings.AutoClosePopupSeconds);
                    activeMidnightPopup.Show();
                }
                catch { }
            }

            if (historyFormInstance != null && !historyFormInstance.IsDisposed)
            {
                historyFormInstance.LoadData();
            }
        }

        private static Color GetMetricColor(double pct)
        {
            if (pct >= 80.0) return Color.FromArgb(239, 68, 68);   // Red
            if (pct >= 50.0) return Color.FromArgb(245, 158, 11);  // Amber
            return Color.FromArgb(34, 197, 94);                    // Green
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float scale = GetDpiScale();

            using (Pen borderPen = new Pen(Color.FromArgb(58, 58, 70), Math.Max(1f, scale)))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }

            List<KeyValuePair<string, double>> items = new List<KeyValuePair<string, double>>();
            if (settings.ShowCpu) items.Add(new KeyValuePair<string, double>("CPU", currentCpu));
            if (settings.ShowRam) items.Add(new KeyValuePair<string, double>("RAM", currentRam));
            if (settings.ShowDisk) items.Add(new KeyValuePair<string, double>("DSK", currentDisk));

            if (settings.IsPaused)
            {
                using (Font f = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                using (SolidBrush bMuted = new SolidBrush(Color.FromArgb(160, 160, 175)))
                {
                    g.DrawString("PAUSED", f, bMuted, S(10, scale), (Height - S(16, scale)) / 2f);
                }
                return;
            }

            using (Font fLabel = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (Font fVal = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (SolidBrush bMuted = new SolidBrush(Color.FromArgb(168, 172, 184)))
            {
                if (settings.IsHorizontalLayout)
                {
                    float x = S(8, scale);
                    float y = (Height - S(15, scale)) / 2f;
                    for (int i = 0; i < items.Count; i++)
                    {
                        string lbl = items[i].Key;
                        double val = items[i].Value;
                        string valStr = Math.Round(val).ToString("0") + "%";

                        using (SolidBrush bVal = new SolidBrush(GetMetricColor(val)))
                        {
                            g.DrawString(lbl, fLabel, bMuted, x, y + S(1, scale));
                            g.DrawString(valStr, fVal, bVal, x + S(28, scale), y);
                        }
                        x += S(66, scale);
                    }
                }
                else
                {
                    if (items.Count == 1)
                    {
                        string lbl = items[0].Key;
                        double val = items[0].Value;
                        using (SolidBrush bVal = new SolidBrush(GetMetricColor(val)))
                        {
                            g.DrawString(lbl, fLabel, bMuted, S(8, scale), S(6, scale));
                            g.DrawString(Math.Round(val).ToString("0") + "%", fVal, bVal, S(40, scale), S(5, scale));
                        }
                    }
                    else if (items.Count == 2)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            float y = i == 0 ? S(4, scale) : S(19, scale);
                            using (SolidBrush bVal = new SolidBrush(GetMetricColor(items[i].Value)))
                            {
                                g.DrawString(items[i].Key, fLabel, bMuted, S(8, scale), y + S(1, scale));
                                g.DrawString(Math.Round(items[i].Value).ToString("0") + "%", fVal, bVal, S(42, scale), y);
                            }
                        }
                    }
                    else
                    {
                        // 3 metrics in 2 clean rows:
                        // Row 1: CPU 34%   RAM 67%
                        // Row 2: DSK 12%   8.4/16G
                        float row1Y = S(4, scale);
                        float row2Y = S(20, scale);

                        using (SolidBrush bCpu = new SolidBrush(GetMetricColor(currentCpu)))
                        using (SolidBrush bRam = new SolidBrush(GetMetricColor(currentRam)))
                        using (SolidBrush bDsk = new SolidBrush(GetMetricColor(currentDisk)))
                        {
                            g.DrawString("CPU", fLabel, bMuted, S(7, scale), row1Y);
                            g.DrawString(Math.Round(currentCpu).ToString("0") + "%", fVal, bCpu, S(36, scale), row1Y - S(1, scale));

                            g.DrawString("RAM", fLabel, bMuted, S(76, scale), row1Y);
                            g.DrawString(Math.Round(currentRam).ToString("0") + "%", fVal, bRam, S(106, scale), row1Y - S(1, scale));

                            g.DrawString("DSK", fLabel, bMuted, S(7, scale), row2Y);
                            g.DrawString(Math.Round(currentDisk).ToString("0") + "%", fVal, bDsk, S(36, scale), row2Y - S(1, scale));

                            if (totalRamBytes > 0)
                            {
                                string ramSub = (usedRamBytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.0") + "/" +
                                                (totalRamBytes / (1024.0 * 1024.0 * 1024.0)).ToString("0") + "G";
                                using (Font fSmall = new Font("Segoe UI", 7.3f))
                                {
                                    g.DrawString(ramSub, fSmall, bMuted, S(76, scale), row2Y + S(1, scale));
                                }
                            }
                        }
                    }
                }
            }
        }

        private void Widget_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !settings.IsLocked)
            {
                isDragging = true;
                dragStartCursor = Cursor.Position;
                dragStartLocation = Location;
            }
        }

        private void Widget_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point diff = new Point(Cursor.Position.X - dragStartCursor.X, Cursor.Position.Y - dragStartCursor.Y);
                Location = new Point(dragStartLocation.X + diff.X, dragStartLocation.Y + diff.Y);
            }
        }

        private void Widget_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                if (settings.SnapToTaskbar)
                {
                    NativeMethods.RECT tbRect;
                    uint edge;
                    if (NativeMethods.GetTaskbarPosition(out tbRect, out edge))
                    {
                        if (Math.Abs((Location.Y + Height / 2) - (tbRect.Top + tbRect.Height / 2)) < 36)
                        {
                            Location = new Point(Location.X, tbRect.Top + (tbRect.Height - Height) / 2);
                        }
                    }
                }
                settings.WidgetX = Location.X;
                settings.WidgetY = Location.Y;
                DataManager.SaveSettings(settings);
                EnsureTopmostZOrder();
            }
            else if (e.Button == MouseButtons.Right)
            {
                RefreshContextMenuState();
                contextMenu.Show(Cursor.Position);
            }
        }

        private void SetupTrayIcon()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = LoadAppIcon();
            notifyIcon.Text = "SysMeter";
            notifyIcon.Visible = true;
            notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    RefreshContextMenuState();
                    contextMenu.Show(Cursor.Position);
                }
            };
            notifyIcon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) OpenDailyReport();
            };
        }

        private void UpdateTrayStatus()
        {
            if (notifyIcon == null) return;
            try
            {
                string tip = string.Format("SysMeter — CPU: {0:0}% | RAM: {1:0}% | Disk: {2:0}%",
                    currentCpu, currentRam, currentDisk);
                if (tip.Length > 63) tip = tip.Substring(0, 63);
                notifyIcon.Text = tip;

                // Render live CPU % in tray icon
                using (Bitmap bmp = new Bitmap(16, 16))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(24, 24, 28));
                    Color c = GetMetricColor(currentCpu);
                    using (Pen p = new Pen(c, 1f))
                    {
                        g.DrawRectangle(p, 0, 0, 15, 15);
                    }
                    int val = (int)Math.Round(currentCpu);
                    string txt = val >= 100 ? "99" : val.ToString();
                    using (Font f = new Font("Segoe UI", txt.Length > 1 ? 7f : 8f, FontStyle.Bold))
                    using (SolidBrush b = new SolidBrush(c))
                    {
                        StringFormat sf = new StringFormat();
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(txt, f, b, new RectangleF(0, 0, 16, 16), sf);
                    }

                    IntPtr hIcon = bmp.GetHicon();
                    Icon newIcon = Icon.FromHandle(hIcon);
                    Icon oldIcon = currentDynamicTrayIcon;
                    currentDynamicTrayIcon = newIcon;
                    notifyIcon.Icon = newIcon;
                    if (oldIcon != null)
                    {
                        try
                        {
                            NativeMethods.DestroyIcon(oldIcon.Handle);
                            oldIcon.Dispose();
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private ToolStripMenuItem miShowWidget;
        private ToolStripMenuItem miHorizontalLayout;
        private ToolStripMenuItem miShowCpu;
        private ToolStripMenuItem miShowRam;
        private ToolStripMenuItem miShowDisk;
        private ToolStripMenuItem miLockPos;
        private ToolStripMenuItem miTopMost;
        private ToolStripMenuItem miSnapTaskbar;
        private ToolStripMenuItem miAutoHideFullscreen;
        private ToolStripMenuItem miPause;
        private ToolStripMenuItem miStartup;
        private ToolStripMenuItem miMidnightPopup;

        private void BuildContextMenu()
        {
            contextMenu = new ContextMenuStrip();

            miShowWidget = new ToolStripMenuItem("Show Floating Widget");
            miShowWidget.Click += (s, e) =>
            {
                settings.IsWidgetVisible = !settings.IsWidgetVisible;
                DataManager.SaveSettings(settings);
                if (settings.IsWidgetVisible)
                {
                    Show();
                    EnsureTopmostZOrder();
                }
                else
                {
                    Hide();
                }
            };
            contextMenu.Items.Add(miShowWidget);

            miHorizontalLayout = new ToolStripMenuItem("Single-Line Horizontal Layout");
            miHorizontalLayout.Click += (s, e) =>
            {
                settings.IsHorizontalLayout = !settings.IsHorizontalLayout;
                DataManager.SaveSettings(settings);
                UpdateDimensions();
                if (settings.SnapToTaskbar) ResetTaskbarPosition();
            };
            contextMenu.Items.Add(miHorizontalLayout);

            ToolStripMenuItem miMetrics = new ToolStripMenuItem("Visible Metrics");
            miShowCpu = new ToolStripMenuItem("Show CPU %");
            miShowCpu.Click += (s, e) => ToggleMetric("cpu");
            miShowRam = new ToolStripMenuItem("Show RAM %");
            miShowRam.Click += (s, e) => ToggleMetric("ram");
            miShowDisk = new ToolStripMenuItem("Show Disk %");
            miShowDisk.Click += (s, e) => ToggleMetric("disk");
            miMetrics.DropDownItems.AddRange(new ToolStripItem[] { miShowCpu, miShowRam, miShowDisk });
            contextMenu.Items.Add(miMetrics);

            ToolStripMenuItem miOpacity = new ToolStripMenuItem("Widget Opacity");
            double[] opacities = new double[] { 1.0, 0.95, 0.85, 0.70, 0.50 };
            foreach (double op in opacities)
            {
                double captured = op;
                ToolStripMenuItem item = new ToolStripMenuItem((int)(op * 100) + "%");
                item.Click += (s, e) =>
                {
                    settings.WidgetOpacity = captured;
                    Opacity = captured;
                    DataManager.SaveSettings(settings);
                };
                miOpacity.DropDownItems.Add(item);
            }
            contextMenu.Items.Add(miOpacity);

            contextMenu.Items.Add(new ToolStripSeparator());

            miLockPos = new ToolStripMenuItem("Lock Widget Position");
            miLockPos.Click += (s, e) => { settings.IsLocked = !settings.IsLocked; DataManager.SaveSettings(settings); };
            contextMenu.Items.Add(miLockPos);

            miTopMost = new ToolStripMenuItem("Always on Top");
            miTopMost.Click += (s, e) => { settings.IsTopMost = !settings.IsTopMost; TopMost = settings.IsTopMost; DataManager.SaveSettings(settings); EnsureTopmostZOrder(); };
            contextMenu.Items.Add(miTopMost);

            miSnapTaskbar = new ToolStripMenuItem("Snap to Taskbar");
            miSnapTaskbar.Click += (s, e) => { settings.SnapToTaskbar = !settings.SnapToTaskbar; DataManager.SaveSettings(settings); if (settings.SnapToTaskbar) ResetTaskbarPosition(); };
            contextMenu.Items.Add(miSnapTaskbar);

            miAutoHideFullscreen = new ToolStripMenuItem("Auto-Hide in Fullscreen");
            miAutoHideFullscreen.Click += (s, e) => { settings.AutoHideInFullscreen = !settings.AutoHideInFullscreen; DataManager.SaveSettings(settings); };
            contextMenu.Items.Add(miAutoHideFullscreen);

            contextMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem miHistory = new ToolStripMenuItem("Daily Usage History & CSV Export...");
            miHistory.Click += (s, e) => OpenDailyReport();
            contextMenu.Items.Add(miHistory);

            miMidnightPopup = new ToolStripMenuItem("Show Midnight Summary Popup");
            miMidnightPopup.Click += (s, e) => { settings.ShowMidnightPopup = !settings.ShowMidnightPopup; DataManager.SaveSettings(settings); };
            contextMenu.Items.Add(miMidnightPopup);

            miPause = new ToolStripMenuItem("Pause Monitoring");
            miPause.Click += (s, e) => { settings.IsPaused = !settings.IsPaused; DataManager.SaveSettings(settings); Invalidate(); };
            contextMenu.Items.Add(miPause);

            contextMenu.Items.Add(new ToolStripSeparator());

            miStartup = new ToolStripMenuItem("Start with Windows");
            miStartup.Click += (s, e) =>
            {
                settings.StartWithWindows = !settings.StartWithWindows;
                StartupHelper.SetStartup(settings.StartWithWindows, Application.ExecutablePath);
                DataManager.SaveSettings(settings);
            };
            contextMenu.Items.Add(miStartup);

            ToolStripMenuItem miResetPos = new ToolStripMenuItem("Reset Position to Taskbar");
            miResetPos.Click += (s, e) => ResetTaskbarPosition();
            contextMenu.Items.Add(miResetPos);

            ToolStripMenuItem miAbout = new ToolStripMenuItem("About SysMeter (GrafiGrow)");
            miAbout.Click += (s, e) =>
            {
                DialogResult res = MessageBox.Show(
                    "SysMeter v1.0.0 — Taskbar CPU, RAM & Disk Monitor\nBuilt by GrafiGrow (MIT License)\n\nCompanion tool: NetSpeedMeter\n\nOpen GitHub repository?",
                    "About SysMeter",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );
                if (res == DialogResult.Yes)
                {
                    try { Process.Start(GitHubUrl); } catch { }
                }
            };
            contextMenu.Items.Add(miAbout);

            ToolStripMenuItem miCompanion = new ToolStripMenuItem("Get NetSpeedMeter (Companion Tool)");
            miCompanion.Click += (s, e) => { try { Process.Start(NetSpeedMeterUrl); } catch { } };
            contextMenu.Items.Add(miCompanion);

            contextMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem miExit = new ToolStripMenuItem("Exit SysMeter");
            miExit.Click += (s, e) =>
            {
                if (notifyIcon != null) notifyIcon.Visible = false;
                Application.Exit();
            };
            contextMenu.Items.Add(miExit);
        }

        private void ToggleMetric(string metric)
        {
            int enabledCount = (settings.ShowCpu ? 1 : 0) + (settings.ShowRam ? 1 : 0) + (settings.ShowDisk ? 1 : 0);
            if (metric == "cpu")
            {
                if (settings.ShowCpu && enabledCount <= 1) return;
                settings.ShowCpu = !settings.ShowCpu;
            }
            else if (metric == "ram")
            {
                if (settings.ShowRam && enabledCount <= 1) return;
                settings.ShowRam = !settings.ShowRam;
            }
            else if (metric == "disk")
            {
                if (settings.ShowDisk && enabledCount <= 1) return;
                settings.ShowDisk = !settings.ShowDisk;
            }
            DataManager.SaveSettings(settings);
            UpdateDimensions();
            if (settings.SnapToTaskbar) ResetTaskbarPosition();
        }

        private void RefreshContextMenuState()
        {
            miShowWidget.Checked = settings.IsWidgetVisible;
            miHorizontalLayout.Checked = settings.IsHorizontalLayout;
            miShowCpu.Checked = settings.ShowCpu;
            miShowRam.Checked = settings.ShowRam;
            miShowDisk.Checked = settings.ShowDisk;
            miLockPos.Checked = settings.IsLocked;
            miTopMost.Checked = settings.IsTopMost;
            miSnapTaskbar.Checked = settings.SnapToTaskbar;
            miAutoHideFullscreen.Checked = settings.AutoHideInFullscreen;
            miMidnightPopup.Checked = settings.ShowMidnightPopup;
            miPause.Checked = settings.IsPaused;
            miStartup.Checked = settings.StartWithWindows;
        }

        public void OpenDailyReport()
        {
            try
            {
                if (historyFormInstance == null || historyFormInstance.IsDisposed)
                {
                    historyFormInstance = new HistoryForm(monitor);
                    historyFormInstance.Show();
                }
                else
                {
                    historyFormInstance.LoadData();
                    if (historyFormInstance.WindowState == FormWindowState.Minimized)
                        historyFormInstance.WindowState = FormWindowState.Normal;
                    historyFormInstance.Activate();
                }
            }
            catch { }
        }
    }
}
