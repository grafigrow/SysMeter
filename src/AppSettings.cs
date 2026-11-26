namespace SysMeter
{
    public class AppSettings
    {
        public bool IsLocked { get; set; }
        public bool IsTopMost { get; set; }
        public bool StartWithWindows { get; set; }
        public bool IsHorizontalLayout { get; set; }
        public bool IsWidgetVisible { get; set; }
        public bool IsPaused { get; set; }
        public int WidgetX { get; set; }
        public int WidgetY { get; set; }
        public double WidgetOpacity { get; set; }
        public bool ShowMidnightPopup { get; set; }
        public int AutoClosePopupSeconds { get; set; }
        public bool AutoHideInFullscreen { get; set; }
        public bool SnapToTaskbar { get; set; }
        public bool ShowCpu { get; set; }
        public bool ShowRam { get; set; }
        public bool ShowDisk { get; set; }

        public AppSettings()
        {
            IsLocked = false;
            IsTopMost = true;
            StartWithWindows = true;
            IsHorizontalLayout = false;
            IsWidgetVisible = true;
            IsPaused = false;
            WidgetX = -1;
            WidgetY = -1;
            WidgetOpacity = 0.95;
            ShowMidnightPopup = true;
            AutoClosePopupSeconds = 0;
            AutoHideInFullscreen = false;
            SnapToTaskbar = true;
            ShowCpu = true;
            ShowRam = true;
            ShowDisk = true;
        }

        public void Normalize()
        {
            if (WidgetOpacity < 0.25 || WidgetOpacity > 1.0)
            {
                WidgetOpacity = 0.95;
            }
            if (AutoClosePopupSeconds < 0)
            {
                AutoClosePopupSeconds = 0;
            }
            if (!ShowCpu && !ShowRam && !ShowDisk)
            {
                ShowCpu = true;
            }
        }
    }
}
