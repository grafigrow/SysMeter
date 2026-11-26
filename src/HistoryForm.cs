using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SysMeter
{
    public class HistoryForm : Form
    {
        private DataGridView grid;
        private Label lblPeakCpu;
        private Label lblPeakRam;
        private Label lblPeakDisk;
        private Label lblDaysCount;
        private SystemMonitor monitor;

        public HistoryForm(SystemMonitor sysMonitor)
        {
            monitor = sysMonitor;
            InitializeComponent();
            LoadData();
        }

        private int S(float basePx, float scale)
        {
            return (int)Math.Round(basePx * scale);
        }

        private void InitializeComponent()
        {
            IntPtr unused = Handle;
            float scale = Math.Max(1.0f, NativeMethods.GetWindowDpiScale(Handle));

            Text = "SysMeter - Daily System Usage History & Reports";
            Font = new Font("Segoe UI", 9f);
            Size = new Size(S(840, scale), S(540, scale));
            MinimumSize = new Size(S(720, scale), S(440, scale));
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(20, 20, 24);
            ForeColor = Color.FromArgb(235, 235, 240);
            Icon = SysMeterWidgetForm.LoadAppIcon();

            Panel topPanel = new Panel();
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = S(104, scale);
            topPanel.BackColor = Color.FromArgb(26, 26, 32);
            Controls.Add(topPanel);

            Label lblTitle = new Label();
            lblTitle.Text = "Daily System Resource Usage History";
            lblTitle.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(S(18, scale), S(14, scale));
            topPanel.Controls.Add(lblTitle);

            FlowLayoutPanel badgeFlow = new FlowLayoutPanel();
            badgeFlow.Location = new Point(S(18, scale), S(50, scale));
            badgeFlow.Size = new Size(S(800, scale), S(42, scale));
            badgeFlow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            badgeFlow.BackColor = Color.Transparent;
            badgeFlow.WrapContents = false;
            topPanel.Controls.Add(badgeFlow);

            lblPeakCpu = CreateStatBadge(badgeFlow, "Peak CPU: 0%", Color.FromArgb(34, 197, 94), scale);
            lblPeakRam = CreateStatBadge(badgeFlow, "Peak RAM: 0%", Color.FromArgb(245, 158, 11), scale);
            lblPeakDisk = CreateStatBadge(badgeFlow, "Peak Disk: 0%", Color.FromArgb(56, 189, 248), scale);
            lblDaysCount = CreateStatBadge(badgeFlow, "Days: 0", Color.FromArgb(168, 85, 247), scale);

            Panel bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = S(56, scale);
            bottomPanel.BackColor = Color.FromArgb(26, 26, 32);
            Controls.Add(bottomPanel);

            Button btnExport = new Button();
            btnExport.Text = "Export to CSV...";
            btnExport.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnExport.ForeColor = Color.White;
            btnExport.BackColor = Color.FromArgb(36, 99, 235);
            btnExport.FlatStyle = FlatStyle.Flat;
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Size = new Size(S(140, scale), S(34, scale));
            btnExport.Location = new Point(S(18, scale), S(11, scale));
            btnExport.Cursor = Cursors.Hand;
            btnExport.Click += BtnExport_Click;
            bottomPanel.Controls.Add(btnExport);

            Button btnRefresh = new Button();
            btnRefresh.Text = "Refresh";
            btnRefresh.Font = new Font("Segoe UI", 9.5f);
            btnRefresh.ForeColor = Color.FromArgb(220, 220, 230);
            btnRefresh.BackColor = Color.FromArgb(45, 45, 55);
            btnRefresh.FlatStyle = FlatStyle.Flat;
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Size = new Size(S(100, scale), S(34, scale));
            btnRefresh.Location = new Point(S(170, scale), S(11, scale));
            btnRefresh.Cursor = Cursors.Hand;
            btnRefresh.Click += (s, e) => LoadData();
            bottomPanel.Controls.Add(btnRefresh);

            Button btnClose = new Button();
            btnClose.Text = "Close";
            btnClose.Font = new Font("Segoe UI", 9.5f);
            btnClose.ForeColor = Color.FromArgb(220, 220, 230);
            btnClose.BackColor = Color.FromArgb(45, 45, 55);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Size = new Size(S(100, scale), S(34, scale));
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Location = new Point(ClientSize.Width - S(118, scale), S(11, scale));
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => Close();
            bottomPanel.Controls.Add(btnClose);

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.FromArgb(20, 20, 24);
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(40, 40, 48);
            grid.EnableHeadersVisualStyles = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeight = S(36, scale);
            grid.RowTemplate.Height = S(32, scale);

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 38);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(200, 200, 215);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(S(8, scale), 0, 0, 0);

            grid.DefaultCellStyle.BackColor = Color.FromArgb(22, 22, 27);
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(230, 230, 238);
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 65, 110);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Padding = new Padding(S(8, scale), 0, 0, 0);

            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(26, 26, 32);

            grid.Columns.Add("Date", "Date");
            grid.Columns.Add("PeakCpu", "Peak CPU");
            grid.Columns.Add("AvgCpu", "Avg CPU");
            grid.Columns.Add("PeakRam", "Peak RAM");
            grid.Columns.Add("AvgRam", "Avg RAM");
            grid.Columns.Add("PeakDisk", "Peak Disk");
            grid.Columns.Add("Samples", "Samples");

            Controls.Add(grid);
            grid.BringToFront();
        }

        private Label CreateStatBadge(FlowLayoutPanel parent, string text, Color accent, float scale)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lbl.ForeColor = accent;
            lbl.BackColor = Color.FromArgb(36, 36, 44);
            lbl.AutoSize = true;
            lbl.Padding = new Padding(S(10, scale), S(6, scale), S(10, scale), S(6, scale));
            lbl.Margin = new Padding(0, 0, S(10, scale), 0);
            parent.Controls.Add(lbl);
            return lbl;
        }

        public void LoadData()
        {
            grid.Rows.Clear();
            DailySystemRecord liveToday = monitor != null ? monitor.GetTodayRecord() : null;
            List<DailySystemRecord> history = DataManager.GetMergedHistory(liveToday);

            double maxCpu = 0;
            double maxRam = 0;
            double maxDisk = 0;
            string todayStr = DateTime.Now.ToString("yyyy-MM-dd");

            foreach (DailySystemRecord rec in history)
            {
                if (rec.PeakCpuPercent > maxCpu) maxCpu = rec.PeakCpuPercent;
                if (rec.PeakRamPercent > maxRam) maxRam = rec.PeakRamPercent;
                if (rec.PeakDiskPercent > maxDisk) maxDisk = rec.PeakDiskPercent;

                string dateLabel = rec.DateString == todayStr ? rec.DateString + " (Today)" : rec.DateString;
                grid.Rows.Add(
                    dateLabel,
                    rec.PeakCpuPercent.ToString("0.0") + "%",
                    rec.AvgCpuPercent.ToString("0.0") + "%",
                    rec.PeakRamPercent.ToString("0.0") + "%",
                    rec.AvgRamPercent.ToString("0.0") + "%",
                    rec.PeakDiskPercent.ToString("0.0") + "%",
                    rec.SampleCount.ToString()
                );
            }

            lblPeakCpu.Text = "Peak CPU: " + maxCpu.ToString("0.0") + "%";
            lblPeakRam.Text = "Peak RAM: " + maxRam.ToString("0.0") + "%";
            lblPeakDisk.Text = "Peak Disk: " + maxDisk.ToString("0.0") + "%";
            lblDaysCount.Text = "Days Tracked: " + history.Count;
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                sfd.FileName = "SysMeter_Report_" + DateTime.Now.ToString("yyyy-MM-dd") + ".csv";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        DailySystemRecord liveToday = monitor != null ? monitor.GetTodayRecord() : null;
                        List<DailySystemRecord> history = DataManager.GetMergedHistory(liveToday);

                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("Date,Peak CPU (%),Avg CPU (%),Peak RAM (%),Avg RAM (%),Peak Disk (%),Avg Disk (%),Samples");
                        foreach (DailySystemRecord rec in history)
                        {
                            sb.AppendLine(string.Format("{0},{1:0.0},{2:0.0},{3:0.0},{4:0.0},{5:0.0},{6:0.0},{7}",
                                rec.DateString,
                                rec.PeakCpuPercent,
                                rec.AvgCpuPercent,
                                rec.PeakRamPercent,
                                rec.AvgRamPercent,
                                rec.PeakDiskPercent,
                                rec.AvgDiskPercent,
                                rec.SampleCount));
                        }
                        File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        MessageBox.Show(this, "CSV report exported successfully!", "SysMeter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Export failed: " + ex.Message, "SysMeter", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}
