using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SysMeter
{
    public class MidnightReportForm : Form
    {
        private DailySystemRecord record;
        private Action onOpenHistory;
        private Timer autoCloseTimer;
        private int secondsRemaining;
        private float uiScale = 1.0f;

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

        public MidnightReportForm(DailySystemRecord dailyRecord, Action openHistoryAction, int autoCloseSeconds = 0)
        {
            record = dailyRecord;
            onOpenHistory = openHistoryAction;
            secondsRemaining = autoCloseSeconds;

            InitializeComponent();
        }

        private int S(float basePx)
        {
            return (int)Math.Round(basePx * uiScale);
        }

        private void InitializeComponent()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(24, 24, 28);
            DoubleBuffered = true;

            IntPtr unused = Handle;
            uiScale = Math.Max(1.0f, NativeMethods.GetWindowDpiScale(Handle));

            Size = new Size(S(430), S(268));
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - S(16), wa.Bottom - Height - S(16));

            Button btnClose = new Button();
            btnClose.Text = "X";
            btnClose.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnClose.ForeColor = Color.FromArgb(160, 160, 175);
            btnClose.BackColor = Color.Transparent;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Size = new Size(S(28), S(28));
            btnClose.Location = new Point(Width - S(36), S(12));
            btnClose.Cursor = Cursors.Hand;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 45, 55);
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);

            Button btnHistory = new Button();
            btnHistory.Text = "View Full History";
            btnHistory.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnHistory.ForeColor = Color.White;
            btnHistory.BackColor = Color.FromArgb(36, 99, 235);
            btnHistory.FlatStyle = FlatStyle.Flat;
            btnHistory.FlatAppearance.BorderSize = 0;
            btnHistory.Size = new Size(S(155), S(34));
            btnHistory.Location = new Point(S(22), Height - S(48));
            btnHistory.Cursor = Cursors.Hand;
            btnHistory.Click += (s, e) =>
            {
                if (onOpenHistory != null) onOpenHistory();
                Close();
            };
            Controls.Add(btnHistory);

            Button btnDismiss = new Button();
            btnDismiss.Text = "Dismiss";
            btnDismiss.Font = new Font("Segoe UI", 9.5f);
            btnDismiss.ForeColor = Color.FromArgb(200, 200, 210);
            btnDismiss.BackColor = Color.FromArgb(42, 42, 50);
            btnDismiss.FlatStyle = FlatStyle.Flat;
            btnDismiss.FlatAppearance.BorderSize = 0;
            btnDismiss.Size = new Size(S(100), S(34));
            btnDismiss.Location = new Point(Width - S(122), Height - S(48));
            btnDismiss.Cursor = Cursors.Hand;
            btnDismiss.Click += (s, e) => Close();
            Controls.Add(btnDismiss);

            if (secondsRemaining > 0)
            {
                autoCloseTimer = new Timer();
                autoCloseTimer.Interval = 1000;
                autoCloseTimer.Tick += (s, e) =>
                {
                    secondsRemaining--;
                    if (secondsRemaining <= 0)
                    {
                        autoCloseTimer.Stop();
                        Close();
                    }
                };
                autoCloseTimer.Start();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen borderPen = new Pen(Color.FromArgb(62, 62, 74), Math.Max(1f, uiScale)))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }

            using (SolidBrush accent = new SolidBrush(Color.FromArgb(34, 197, 94)))
            {
                g.FillRectangle(accent, 0, 0, Width, S(4));
            }

            using (Font fTitle = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (Font fSub = new Font("Segoe UI", 9.5f))
            using (Font fLabel = new Font("Segoe UI", 9.5f))
            using (Font fVal = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (SolidBrush bWhite = new SolidBrush(Color.White))
            using (SolidBrush bMuted = new SolidBrush(Color.FromArgb(160, 160, 175)))
            using (SolidBrush bGreen = new SolidBrush(Color.FromArgb(34, 197, 94)))
            using (SolidBrush bAmber = new SolidBrush(Color.FromArgb(245, 158, 11)))
            using (SolidBrush bCyan = new SolidBrush(Color.FromArgb(56, 189, 248)))
            {
                g.DrawString("SysMeter Daily Summary", fTitle, bWhite, S(22), S(16));
                g.DrawString("Report for " + record.DateString, fSub, bMuted, S(22), S(42));

                int y = S(76);
                int rowH = S(32);

                g.DrawString("Peak CPU Usage:", fLabel, bMuted, S(22), y);
                g.DrawString(record.PeakCpuPercent.ToString("0.0") + "% (Avg " + record.AvgCpuPercent.ToString("0.0") + "%)", fVal, bGreen, S(180), y - S(2));

                y += rowH;
                g.DrawString("Peak RAM Usage:", fLabel, bMuted, S(22), y);
                g.DrawString(record.PeakRamPercent.ToString("0.0") + "% (Avg " + record.AvgRamPercent.ToString("0.0") + "%)", fVal, bAmber, S(180), y - S(2));

                y += rowH;
                g.DrawString("Peak Disk Usage:", fLabel, bMuted, S(22), y);
                g.DrawString(record.PeakDiskPercent.ToString("0.0") + "% (Avg " + record.AvgDiskPercent.ToString("0.0") + "%)", fVal, bCyan, S(180), y - S(2));

                y += rowH;
                g.DrawString("Samples Recorded:", fLabel, bMuted, S(22), y);
                g.DrawString(record.SampleCount.ToString(), fVal, bWhite, S(180), y - S(2));
            }
        }
    }
}
