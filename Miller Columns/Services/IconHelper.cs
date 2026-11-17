using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MillerColumns.Services
{
    public static class IconHelper
    {
        private static readonly ConcurrentDictionary<string, ImageSource> IconCache = new(StringComparer.OrdinalIgnoreCase);

        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        private static readonly HashSet<string> CustomIconExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".lnk", ".ico", ".url", ".appx", ".msi"
        };

        private static readonly Lazy<ImageSource> DefaultFolder = new(() =>
            GetIconFromShell("dummyFolder", FILE_ATTRIBUTE_DIRECTORY, useAttributes: true) ?? CreateFallbackIcon());

        private static readonly Lazy<ImageSource> GenericExeIcon = new(() =>
            GetIconFromShell("dummy.exe", FILE_ATTRIBUTE_NORMAL, useAttributes: true) ?? CreateFallbackIcon());

        private static readonly Lazy<ImageSource> GenericFileIcon = new(() =>
            GetIconFromShell("dummyFile", FILE_ATTRIBUTE_NORMAL, useAttributes: true) ?? CreateFallbackIcon());

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            out SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static ImageSource GetDefaultFolderIcon() => DefaultFolder.Value;

        public static ImageSource GetFolderIcon(string? folderPath = null)
        {
            if (!string.IsNullOrEmpty(folderPath))
            {
                return IconCache.GetOrAdd("dir::" + folderPath, path =>
                {
                    string realPath = path.Substring(5);
                    return GetIconFromShell(realPath, 0, useAttributes: false) ?? DefaultFolder.Value;
                });
            }

            return DefaultFolder.Value;
        }

        public static ImageSource GetDriveIcon(string? drivePath = null)
        {
            if (!string.IsNullOrEmpty(drivePath))
            {
                return IconCache.GetOrAdd("drive::" + drivePath, path =>
                {
                    string realPath = path.Substring(7);
                    return GetIconFromShell(realPath, 0, useAttributes: false) ?? DefaultFolder.Value;
                });
            }
            return DefaultFolder.Value;
        }

        /// <summary>
        /// Fast instant icon resolution: Returns immediately without hitting disk for executables/shortcuts.
        /// </summary>
        public static ImageSource GetFastFileIcon(string filePath, out bool needsCustom)
        {
            string extension = Path.GetExtension(filePath);

            if (string.IsNullOrEmpty(extension) || CustomIconExtensions.Contains(extension))
            {
                // Check if already cached from a previous background load
                if (IconCache.TryGetValue("file::" + filePath, out var cached))
                {
                    needsCustom = false;
                    return cached;
                }

                // Needs custom background load, but show instant placeholder immediately
                needsCustom = true;
                return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) 
                    ? GenericExeIcon.Value 
                    : GenericFileIcon.Value;
            }

            needsCustom = false;
            // Standard extensions (.txt, .pdf, .cs, etc.) share icons and are cached immediately
            return IconCache.GetOrAdd(extension, ext =>
            {
                return GetIconFromShell($"dummy{ext}", FILE_ATTRIBUTE_NORMAL, useAttributes: true) ?? GenericFileIcon.Value;
            });
        }

        /// <summary>
        /// Background accurate icon loader for apps, executables, and shortcuts.
        /// </summary>
        public static ImageSource? GetAccurateFileIcon(string filePath)
        {
            return IconCache.GetOrAdd("file::" + filePath, key =>
            {
                string realPath = key.Substring(6);
                var icon = GetIconFromShell(realPath, 0, useAttributes: false);
                return icon ?? GenericFileIcon.Value;
            });
        }

        private static ImageSource? GetIconFromShell(string path, uint fileAttributes, bool useAttributes)
        {
            try
            {
                uint flags = SHGFI_ICON | SHGFI_SMALLICON;
                if (useAttributes)
                {
                    flags |= SHGFI_USEFILEATTRIBUTES;
                }

                IntPtr result = SHGetFileInfo(path, fileAttributes, out SHFILEINFO shfi, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);

                if (result != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(
                            shfi.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());

                        bitmap.Freeze(); // Enables cross-thread safe sharing
                        return bitmap;
                    }
                    finally
                    {
                        DestroyIcon(shfi.hIcon);
                    }
                }
            }
            catch
            {
                // Fallback
            }

            return null;
        }

        private static ImageSource CreateFallbackIcon()
        {
            var drawing = new DrawingGroup();
            using (var ctx = drawing.Open())
            {
                ctx.DrawRectangle(Brushes.Gray, null, new Rect(0, 0, 16, 16));
            }
            var drawingImage = new DrawingImage(drawing);
            drawingImage.Freeze();
            return drawingImage;
        }
    }
}
