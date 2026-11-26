using System;
using System.IO;
using Microsoft.Win32;

namespace SysMeter
{
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "SysMeter";

        public static string GetRegisteredStartupPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue(AppName);
                        if (val != null)
                        {
                            return val.ToString().Trim().Trim('"');
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static bool IsStartupEnabled()
        {
            return !string.IsNullOrEmpty(GetRegisteredStartupPath());
        }

        public static void EnsureStartupSynced(string executablePath)
        {
            string existing = GetRegisteredStartupPath();
            if (string.IsNullOrEmpty(existing) || !File.Exists(existing))
            {
                SetStartup(true, executablePath);
            }
        }

        public static void SetStartup(bool enable, string executablePath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue(AppName, "\"" + executablePath + "\"");
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
