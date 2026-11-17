using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace MillerColumns.Services
{
    public static class ThemeService
    {
        /// <summary>
        /// Reads Windows Personalize registry to determine if system apps are in Dark theme.
        /// Defaults to Dark (true) if the key is absent or on error.
        /// </summary>
        public static bool GetSystemIsDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var val = key.GetValue("AppsUseLightTheme");
                    if (val is int intVal)
                    {
                        return intVal == 0; // 0 = Dark, 1 = Light
                    }
                }
            }
            catch
            {
                // Fallback default
            }
            return true;
        }

        /// <summary>
        /// Dynamically updates the Application resource palette and DWM window title bar attributes.
        /// </summary>
        public static void ApplyTheme(bool isDark, IntPtr hwnd = default)
        {
            var app = Application.Current;
            if (app == null) return;

            var res = app.Resources;

            if (isDark)
            {
                SetBrush(res, "WindowBackgroundBrush", Color.FromRgb(0x19, 0x19, 0x19));
                SetBrush(res, "SidebarBackgroundBrush", Color.FromRgb(0x14, 0x14, 0x14));
                SetBrush(res, "ColumnBackgroundBrush", Color.FromRgb(0x1E, 0x1E, 0x1E));
                SetBrush(res, "BorderBrush", Color.FromRgb(0x2A, 0x2A, 0x2D));
                SetBrush(res, "AccentBrush", Color.FromRgb(0x0A, 0x60, 0xFE));
                SetBrush(res, "HoverBrush", Color.FromRgb(0x2A, 0x2D, 0x30));
                SetBrush(res, "TextPrimaryBrush", Color.FromRgb(0xED, 0xED, 0xED));
                SetBrush(res, "TextSecondaryBrush", Color.FromRgb(0x8E, 0x8E, 0x93));

                SetBrush(res, "ScrollBarTrackBrush", Color.FromArgb(0x64, 0x1C, 0x1C, 0x1F));
                SetBrush(res, "ScrollBarThumbBrush", Color.FromRgb(0x4E, 0x4E, 0x54));
                SetBrush(res, "ScrollBarThumbHoverBrush", Color.FromRgb(0x6E, 0x6E, 0x78));
                SetBrush(res, "ScrollBarThumbDraggingBrush", Color.FromRgb(0x8E, 0x8E, 0x9A));
            }
            else
            {
                SetBrush(res, "WindowBackgroundBrush", Color.FromRgb(0xF5, 0xF5, 0xF7));
                SetBrush(res, "SidebarBackgroundBrush", Color.FromRgb(0xEB, 0xEB, 0xF0));
                SetBrush(res, "ColumnBackgroundBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
                SetBrush(res, "BorderBrush", Color.FromRgb(0xD2, 0xD2, 0xD7));
                SetBrush(res, "AccentBrush", Color.FromRgb(0x00, 0x7A, 0xFF));
                SetBrush(res, "HoverBrush", Color.FromRgb(0xE2, 0xE2, 0xE8));
                SetBrush(res, "TextPrimaryBrush", Color.FromRgb(0x1D, 0x1D, 0x1F));
                SetBrush(res, "TextSecondaryBrush", Color.FromRgb(0x6E, 0x6E, 0x73));

                SetBrush(res, "ScrollBarTrackBrush", Color.FromArgb(0x32, 0x00, 0x00, 0x00));
                SetBrush(res, "ScrollBarThumbBrush", Color.FromRgb(0xC1, 0xC1, 0xC6));
                SetBrush(res, "ScrollBarThumbHoverBrush", Color.FromRgb(0xA0, 0xA0, 0xA5));
                SetBrush(res, "ScrollBarThumbDraggingBrush", Color.FromRgb(0x80, 0x80, 0x85));
            }

            if (hwnd != IntPtr.Zero)
            {
                DarkModeHelper.SetDarkMode(hwnd, isDark);
            }
        }

        private static void SetBrush(ResourceDictionary res, string key, Color color)
        {
            if (res.Contains(key) && res[key] is SolidColorBrush brush && !brush.IsFrozen)
            {
                brush.Color = color;
            }
            else
            {
                res[key] = new SolidColorBrush(color);
            }
        }
    }
}
