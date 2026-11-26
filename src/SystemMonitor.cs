using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SysMeter
{
    public class SystemStatsEventArgs : EventArgs
    {
        public double CpuPercent { get; set; }
        public double RamPercent { get; set; }
        public double DiskPercent { get; set; }
        public long UsedRamBytes { get; set; }
        public long TotalRamBytes { get; set; }
        public DailySystemRecord TodayRecord { get; set; }
    }

    public class SystemMonitor
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        private AppSettings settings;
        private Timer pollTimer;
        private DailySystemRecord todayRecord;
        private DateTime lastSaveTime;

        private ulong prevIdleTime;
        private ulong prevKernelTime;
        private ulong prevUserTime;
        private bool hasCpuBaseline;

        private PerformanceCounter diskCounter;
        private bool diskCounterInitialized;

        public event EventHandler<SystemStatsEventArgs> StatsUpdated;
        public event EventHandler<DailySystemRecord> MidnightRollover;

        public double CurrentCpuPercent { get; private set; }
        public double CurrentRamPercent { get; private set; }
        public double CurrentDiskPercent { get; private set; }
        public long UsedRamBytes { get; private set; }
        public long TotalRamBytes { get; private set; }

        public SystemMonitor(AppSettings appSettings)
        {
            settings = appSettings;
            lastSaveTime = DateTime.Now;
            InitializeTodayRecord();
            InitializeCounters();
        }

        private void InitializeCounters()
        {
            try
            {
                FILETIME idle, kernel, user;
                if (GetSystemTimes(out idle, out kernel, out user))
                {
                    prevIdleTime = FileTimeToUInt64(idle);
                    prevKernelTime = FileTimeToUInt64(kernel);
                    prevUserTime = FileTimeToUInt64(user);
                    hasCpuBaseline = true;
                }
            }
            catch { }

            try
            {
                diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
                diskCounter.NextValue();
                diskCounterInitialized = true;
            }
            catch
            {
                diskCounterInitialized = false;
            }
        }

        private static ulong FileTimeToUInt64(FILETIME ft)
        {
            return ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
        }

        private void InitializeTodayRecord()
        {
            string todayStr = DateTime.Now.ToString("yyyy-MM-dd");
            DailySystemRecord saved = DataManager.LoadCurrentDay();

            if (saved != null && saved.DateString == todayStr)
            {
                todayRecord = saved;
            }
            else
            {
                if (saved != null && !string.IsNullOrEmpty(saved.DateString))
                {
                    ArchiveRecord(saved);
                }
                todayRecord = new DailySystemRecord
                {
                    DateString = todayStr
                };
                DataManager.SaveCurrentDay(todayRecord);
            }
        }

        public DailySystemRecord CheckPreviousDayUnreported()
        {
            List<DailySystemRecord> history = DataManager.LoadHistory();
            string todayStr = DateTime.Now.ToString("yyyy-MM-dd");
            DailySystemRecord candidate = null;

            for (int i = 0; i < history.Count; i++)
            {
                if (history[i].DateString != todayStr && !history[i].MidnightReported && history[i].SampleCount > 0)
                {
                    if (candidate == null || string.Compare(history[i].DateString, candidate.DateString, StringComparison.Ordinal) > 0)
                    {
                        candidate = history[i];
                    }
                }
            }

            if (candidate != null)
            {
                candidate.MidnightReported = true;
                DataManager.SaveHistory(history);
            }

            return candidate;
        }

        private void ArchiveRecord(DailySystemRecord rec)
        {
            if (rec == null || string.IsNullOrEmpty(rec.DateString)) return;

            List<DailySystemRecord> history = DataManager.LoadHistory();
            int existingIndex = history.FindIndex(r => r.DateString == rec.DateString);
            if (existingIndex >= 0)
            {
                history[existingIndex] = rec;
            }
            else
            {
                history.Insert(0, rec);
            }

            history.Sort((a, b) => string.Compare(b.DateString, a.DateString, StringComparison.Ordinal));
            DataManager.SaveHistory(history);
        }

        public DailySystemRecord GetTodayRecord()
        {
            return todayRecord;
        }

        public void Start()
        {
            if (pollTimer == null)
            {
                pollTimer = new Timer();
                pollTimer.Interval = 1000;
                pollTimer.Tick += PollTimer_Tick;
            }
            pollTimer.Start();
            PollOnce();
        }

        public void Stop()
        {
            if (pollTimer != null)
            {
                pollTimer.Stop();
            }
            DataManager.SaveCurrentDay(todayRecord);
            if (diskCounter != null)
            {
                try { diskCounter.Dispose(); } catch { }
            }
        }

        public void ResetBaseline()
        {
            hasCpuBaseline = false;
            FILETIME idle, kernel, user;
            if (GetSystemTimes(out idle, out kernel, out user))
            {
                prevIdleTime = FileTimeToUInt64(idle);
                prevKernelTime = FileTimeToUInt64(kernel);
                prevUserTime = FileTimeToUInt64(user);
                hasCpuBaseline = true;
            }
        }

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            PollOnce();
        }

        public void PollOnce()
        {
            try
            {
                string nowStr = DateTime.Now.ToString("yyyy-MM-dd");
                if (todayRecord.DateString != nowStr)
                {
                    DailySystemRecord completedDay = todayRecord;
                    completedDay.MidnightReported = true;
                    ArchiveRecord(completedDay);

                    todayRecord = new DailySystemRecord
                    {
                        DateString = nowStr
                    };
                    DataManager.SaveCurrentDay(todayRecord);

                    if (MidnightRollover != null)
                    {
                        MidnightRollover(this, completedDay);
                    }
                }

                double cpu = ReadCpuPercent();
                long usedRam, totalRam;
                double ram = ReadRamPercent(out usedRam, out totalRam);
                double disk = ReadDiskPercent();

                CurrentCpuPercent = cpu;
                CurrentRamPercent = ram;
                CurrentDiskPercent = disk;
                UsedRamBytes = usedRam;
                TotalRamBytes = totalRam;

                if (!settings.IsPaused)
                {
                    if (cpu > todayRecord.PeakCpuPercent) todayRecord.PeakCpuPercent = cpu;
                    if (ram > todayRecord.PeakRamPercent) todayRecord.PeakRamPercent = ram;
                    if (disk > todayRecord.PeakDiskPercent) todayRecord.PeakDiskPercent = disk;

                    todayRecord.SumCpuPercent += cpu;
                    todayRecord.SumRamPercent += ram;
                    todayRecord.SumDiskPercent += disk;
                    todayRecord.SampleCount++;
                    if (totalRam > 0) todayRecord.TotalRamBytes = totalRam;
                }

                if ((DateTime.Now - lastSaveTime).TotalSeconds >= 30)
                {
                    DataManager.SaveCurrentDay(todayRecord);
                    lastSaveTime = DateTime.Now;
                }

                if (StatsUpdated != null)
                {
                    StatsUpdated(this, new SystemStatsEventArgs
                    {
                        CpuPercent = cpu,
                        RamPercent = ram,
                        DiskPercent = disk,
                        UsedRamBytes = usedRam,
                        TotalRamBytes = totalRam,
                        TodayRecord = todayRecord
                    });
                }
            }
            catch { }
        }

        private double ReadCpuPercent()
        {
            try
            {
                FILETIME idle, kernel, user;
                if (GetSystemTimes(out idle, out kernel, out user))
                {
                    ulong idleVal = FileTimeToUInt64(idle);
                    ulong kernelVal = FileTimeToUInt64(kernel);
                    ulong userVal = FileTimeToUInt64(user);

                    if (hasCpuBaseline)
                    {
                        ulong idleDelta = idleVal >= prevIdleTime ? idleVal - prevIdleTime : 0;
                        ulong kernelDelta = kernelVal >= prevKernelTime ? kernelVal - prevKernelTime : 0;
                        ulong userDelta = userVal >= prevUserTime ? userVal - prevUserTime : 0;

                        prevIdleTime = idleVal;
                        prevKernelTime = kernelVal;
                        prevUserTime = userVal;

                        ulong total = kernelDelta + userDelta;
                        if (total > 0 && total >= idleDelta)
                        {
                            double pct = ((total - idleDelta) * 100.0) / total;
                            return ClampPercent(pct);
                        }
                    }
                    else
                    {
                        prevIdleTime = idleVal;
                        prevKernelTime = kernelVal;
                        prevUserTime = userVal;
                        hasCpuBaseline = true;
                    }
                }
            }
            catch { }
            return CurrentCpuPercent;
        }

        private double ReadRamPercent(out long usedBytes, out long totalBytes)
        {
            usedBytes = 0;
            totalBytes = 0;
            try
            {
                NativeMethods.MEMORYSTATUSEX mem = new NativeMethods.MEMORYSTATUSEX();
                if (NativeMethods.GlobalMemoryStatusEx(mem) && mem.ullTotalPhys > 0)
                {
                    totalBytes = (long)mem.ullTotalPhys;
                    ulong used = mem.ullTotalPhys >= mem.ullAvailPhys ? (mem.ullTotalPhys - mem.ullAvailPhys) : 0;
                    usedBytes = (long)used;
                    double pct = (used * 100.0) / mem.ullTotalPhys;
                    return ClampPercent(pct);
                }
            }
            catch { }
            return 0;
        }

        private double ReadDiskPercent()
        {
            if (!diskCounterInitialized || diskCounter == null) return 0;
            try
            {
                return ClampPercent(diskCounter.NextValue());
            }
            catch
            {
                return 0;
            }
        }

        public static double ClampPercent(double val)
        {
            if (double.IsNaN(val) || double.IsInfinity(val) || val < 0) return 0;
            if (val > 100) return 100;
            return val;
        }

        public static string FormatGb(long bytes)
        {
            if (bytes <= 0) return "0 GB";
            double gb = bytes / (1024.0 * 1024.0 * 1024.0);
            return gb.ToString("0.0") + " GB";
        }
    }
}
