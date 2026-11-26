using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SysMeter
{
    public static class DataManager
    {
        private static readonly string AppDataFolder;
        private static readonly string SettingsFile;
        private static readonly string HistoryFile;
        private static readonly string CurrentDayFile;
        private static readonly JavaScriptSerializer Json;
        private static readonly object FileLock = new object();

        static DataManager()
        {
            AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysMeter");
            SettingsFile = Path.Combine(AppDataFolder, "settings.json");
            HistoryFile = Path.Combine(AppDataFolder, "history.json");
            CurrentDayFile = Path.Combine(AppDataFolder, "current_day.json");
            Json = new JavaScriptSerializer();

            EnsureDataDirectory();
        }

        private static void EnsureDataDirectory()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }
            }
            catch { }
        }

        private static void WriteAtomicUtf8(string path, string content)
        {
            EnsureDataDirectory();
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, content, Encoding.UTF8);
            if (File.Exists(path))
            {
                File.Copy(tempPath, path, true);
                try { File.Delete(tempPath); } catch { }
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        public static string GetDataDirectory()
        {
            return AppDataFolder;
        }

        public static AppSettings LoadSettings()
        {
            lock (FileLock)
            {
                try
                {
                    if (File.Exists(SettingsFile))
                    {
                        string text = File.ReadAllText(SettingsFile, Encoding.UTF8);
                        AppSettings s = Json.Deserialize<AppSettings>(text);
                        if (s != null)
                        {
                            s.Normalize();
                            return s;
                        }
                    }
                }
                catch { }
                return new AppSettings();
            }
        }

        public static void SaveSettings(AppSettings settings)
        {
            if (settings == null) return;
            lock (FileLock)
            {
                try
                {
                    settings.Normalize();
                    string text = Json.Serialize(settings);
                    WriteAtomicUtf8(SettingsFile, text);
                }
                catch { }
            }
        }

        public static List<DailySystemRecord> LoadHistory()
        {
            lock (FileLock)
            {
                try
                {
                    if (File.Exists(HistoryFile))
                    {
                        string text = File.ReadAllText(HistoryFile, Encoding.UTF8);
                        List<DailySystemRecord> list = Json.Deserialize<List<DailySystemRecord>>(text);
                        if (list != null) return list;
                    }
                }
                catch { }
                return new List<DailySystemRecord>();
            }
        }

        public static void SaveHistory(List<DailySystemRecord> history)
        {
            if (history == null) return;
            lock (FileLock)
            {
                try
                {
                    string text = Json.Serialize(history);
                    WriteAtomicUtf8(HistoryFile, text);
                }
                catch { }
            }
        }

        public static List<DailySystemRecord> GetMergedHistory(DailySystemRecord liveTodayRecord)
        {
            List<DailySystemRecord> history = LoadHistory();
            string todayStr = DateTime.Now.ToString("yyyy-MM-dd");
            DailySystemRecord todayRec = liveTodayRecord ?? LoadCurrentDay();

            if (todayRec != null && !string.IsNullOrEmpty(todayRec.DateString))
            {
                int existingIdx = history.FindIndex(r => r.DateString == todayRec.DateString);
                if (existingIdx >= 0)
                {
                    history[existingIdx] = todayRec;
                }
                else if (todayRec.DateString == todayStr || todayRec.SampleCount > 0)
                {
                    history.Insert(0, todayRec);
                }
            }

            history.Sort((a, b) => string.Compare(b.DateString, a.DateString, StringComparison.Ordinal));
            return history;
        }

        public static DailySystemRecord LoadCurrentDay()
        {
            lock (FileLock)
            {
                try
                {
                    if (File.Exists(CurrentDayFile))
                    {
                        string text = File.ReadAllText(CurrentDayFile, Encoding.UTF8);
                        DailySystemRecord rec = Json.Deserialize<DailySystemRecord>(text);
                        if (rec != null) return rec;
                    }
                }
                catch { }
                return null;
            }
        }

        public static void SaveCurrentDay(DailySystemRecord record)
        {
            if (record == null) return;
            lock (FileLock)
            {
                try
                {
                    string text = Json.Serialize(record);
                    WriteAtomicUtf8(CurrentDayFile, text);
                }
                catch { }
            }
        }
    }
}
