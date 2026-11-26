using System;
using System.Threading;
using System.Windows.Forms;

namespace SysMeter
{
    static class Program
    {
        private const string MutexName = "Local\\SysMeter_SingleInstance_Mutex_742910";

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                bool resetPos = false;
                if (args != null)
                {
                    foreach (string arg in args)
                    {
                        if (string.Equals(arg, "--reset", StringComparison.OrdinalIgnoreCase))
                        {
                            resetPos = true;
                        }
                    }
                }

                NativeMethods.EnableHighDpi();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool createdNew = false;
                Mutex mutex = null;
                try
                {
                    mutex = new Mutex(true, MutexName, out createdNew);
                }
                catch
                {
                    createdNew = true;
                }

                if (!createdNew && mutex != null)
                {
                    return;
                }

                AppSettings settings = DataManager.LoadSettings();
                if (resetPos)
                {
                    settings.WidgetX = -1;
                    settings.WidgetY = -1;
                    settings.IsWidgetVisible = true;
                    DataManager.SaveSettings(settings);
                }

                if (settings.StartWithWindows)
                {
                    StartupHelper.EnsureStartupSynced(Application.ExecutablePath);
                }

                SystemMonitor monitor = new SystemMonitor(settings);
                SysMeterWidgetForm widget = new SysMeterWidgetForm(monitor, settings);

                DailySystemRecord unreported = monitor.CheckPreviousDayUnreported();
                if (unreported != null && settings.ShowMidnightPopup)
                {
                    widget.Shown += (s, e) =>
                    {
                        try
                        {
                            MidnightReportForm popup = new MidnightReportForm(unreported, widget.OpenDailyReport, settings.AutoClosePopupSeconds);
                            popup.Show();
                        }
                        catch { }
                    };
                }

                monitor.Start();

                if (settings.IsWidgetVisible)
                {
                    widget.Show();
                }

                Application.Run();

                monitor.Stop();

                if (mutex != null)
                {
                    try { mutex.ReleaseMutex(); } catch { }
                    mutex.Dispose();
                }
            }
            catch { }
        }
    }
}
