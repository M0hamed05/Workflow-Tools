using System;
using System.Runtime.InteropServices;

namespace MillerColumns.Services
{
    public static class DarkModeHelper
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int SetPreferredAppMode(int mode);

        [DllImport("uxtheme.dll", EntryPoint = "#136", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern void FlushMenuThemes();

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string? pszSubIdList);

        public static void SetDarkMode(IntPtr hwnd, bool isDark)
        {
            try
            {
                // ForceDark = 2, ForceLight = 3, Default = 0
                SetPreferredAppMode(isDark ? 2 : 3);
                FlushMenuThemes();

                if (hwnd != IntPtr.Zero)
                {
                    int darkMode = isDark ? 1 : 0;
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkMode, sizeof(int));
                    SetWindowTheme(hwnd, "Explorer", null);
                }
            }
            catch
            {
                // Graceful fallback on older Windows builds
            }
        }

        public static void EnableDarkMode(IntPtr hwnd)
        {
            SetDarkMode(hwnd, true);
        }
    }
}
