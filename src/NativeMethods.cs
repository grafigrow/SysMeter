using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SysMeter
{
    public static class NativeMethods
    {
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int HT_CAPTION = 0x2;
        public const int WM_DPICHANGED = 0x02E0;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOREDRAW = 0x0008;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_HIDEWINDOW = 0x0080;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;

        public const int GWL_STYLE = -16;
        public const int WS_CAPTION = 0x00C00000;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        public const uint ABM_GETTASKBARPOS = 0x00000005;
        public const uint ABE_LEFT = 0;
        public const uint ABE_TOP = 1;
        public const uint ABE_RIGHT = 2;
        public const uint ABE_BOTTOM = 3;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("shell32.dll")]
        public static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        public static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        public static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public static int GetWindowLong(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return (int)GetWindowLongPtr64(hWnd, nIndex).ToInt64();
            else
                return GetWindowLong32(hWnd, nIndex);
        }

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        public static extern int SetProcessDpiAwareness(int awareness);

        public static void EnableHighDpi()
        {
            try
            {
                SetProcessDpiAwareness(2);
            }
            catch
            {
                try { SetProcessDPIAware(); } catch { }
            }
        }

        public static float GetWindowDpiScale(IntPtr hwnd)
        {
            try
            {
                if (hwnd != IntPtr.Zero)
                {
                    uint dpi = GetDpiForWindow(hwnd);
                    if (dpi >= 96) return dpi / 96f;
                }
            }
            catch { }

            try
            {
                using (Graphics g = Graphics.FromHwnd(hwnd))
                {
                    if (g.DpiX >= 96f) return g.DpiX / 96f;
                }
            }
            catch { }

            return 1.0f;
        }

        public static bool GetTaskbarPosition(out RECT rect, out uint edge)
        {
            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            IntPtr ret = SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);
            if (ret != IntPtr.Zero && abd.rc.Width > 0 && abd.rc.Height > 0)
            {
                rect = abd.rc;
                edge = abd.uEdge;
                return true;
            }

            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            if (tray != IntPtr.Zero && GetWindowRect(tray, out rect))
            {
                edge = ABE_BOTTOM;
                return true;
            }

            rect = new RECT();
            edge = ABE_BOTTOM;
            return false;
        }

        public static bool IsForegroundWindowFullscreen()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero || fg == GetDesktopWindow() || fg == GetShellWindow())
                    return false;

                StringBuilder sb = new StringBuilder(256);
                GetClassName(fg, sb, 256);
                string cls = sb.ToString();

                if (cls == "Shell_TrayWnd" || cls == "Progman" || cls == "WorkerW")
                    return false;

                int style = GetWindowLong(fg, GWL_STYLE);
                if ((style & WS_CAPTION) == WS_CAPTION)
                    return false;

                RECT fgRect;
                if (!GetWindowRect(fg, out fgRect))
                    return false;

                Screen screen = Screen.FromHandle(fg);
                Rectangle sBounds = screen.Bounds;

                bool coversMonitor = (fgRect.Left <= sBounds.Left && fgRect.Top <= sBounds.Top &&
                                     fgRect.Right >= sBounds.Right && fgRect.Bottom >= sBounds.Bottom);

                if (!coversMonitor) return false;

                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                if (tray != IntPtr.Zero && IsWindowVisible(tray))
                {
                    RECT trayRect;
                    if (GetWindowRect(tray, out trayRect))
                    {
                        int trayMidX = (trayRect.Left + trayRect.Right) / 2;
                        int trayMidY = (trayRect.Top + trayRect.Bottom) / 2;

                        if (trayMidX >= fgRect.Left && trayMidX <= fgRect.Right &&
                            trayMidY >= fgRect.Top && trayMidY <= fgRect.Bottom)
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
