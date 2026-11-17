using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace MillerColumns.Services
{
    public static class ShellContextMenu
    {
        private const uint CMF_NORMAL = 0x00000000;
        private const uint CMD_FIRST = 1;
        private const uint CMD_LAST = 0x7FFF;

        private const uint CMD_PASTE = 0x8001;
        private const uint CMD_OPEN_VSCODE = 0x8002;
        private const uint CMD_OPEN_CMD = 0x8003;
        private const uint CMD_COPY_PATH = 0x8004;

        private const uint MF_BYPOSITION = 0x00000400;
        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint MF_ENABLED = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint MF_DISABLED = 0x00000002;

        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_RIGHTBUTTON = 0x0002;

        private const int WM_INITMENUPOPUP = 0x0117;
        private const int WM_DRAWITEM = 0x002B;
        private const int WM_MEASUREITEM = 0x002C;
        private const int WM_MENUCHAR = 0x0120;
        private const uint WM_NULL = 0x0000;

        private const int SW_SHOWNORMAL = 1;
        private const uint CMIC_MASK_UNICODE = 0x00004000;
        private const uint CMIC_MASK_PTINVOKE = 0x20000000;

        private const uint FO_MOVE = 0x0001;
        private const uint FO_COPY = 0x0002;
        private const uint FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMMKDIR = 0x0200;

        private static readonly Guid IID_IShellFolder = new("000214E6-0000-0000-C000-000000000046");
        private static readonly Guid IID_IContextMenu = new("000214e4-0000-0000-c000-000000000046");

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
            public POINT(int x, int y) { this.x = x; this.y = y; }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CMINVOKECOMMANDINFOEX
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            public IntPtr lpVerb;
            public IntPtr lpParameters;
            public IntPtr lpDirectory;
            public int nShow;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr lpTitle;
            public IntPtr lpVerbW;
            public IntPtr lpParametersW;
            public IntPtr lpDirectoryW;
            public IntPtr lpTitleW;
            public POINT ptInvoke;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214e4-0000-0000-c000-000000000046")]
        public interface IContextMenu
        {
            [PreserveSig]
            int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

            [PreserveSig]
            int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);

            [PreserveSig]
            int GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, [MarshalAs(UnmanagedType.LPStr)] StringBuilder commandstring, int cch);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214f4-0000-0000-c000-000000000046")]
        public interface IContextMenu2 : IContextMenu
        {
            [PreserveSig]
            new int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

            [PreserveSig]
            new int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);

            [PreserveSig]
            new int GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, [MarshalAs(UnmanagedType.LPStr)] StringBuilder commandstring, int cch);

            [PreserveSig]
            int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcfce0a0-ec14-11d0-ac88-00c04fd918d0")]
        public interface IContextMenu3 : IContextMenu2
        {
            [PreserveSig]
            new int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

            [PreserveSig]
            new int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);

            [PreserveSig]
            new int GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, [MarshalAs(UnmanagedType.LPStr)] StringBuilder commandstring, int cch);

            [PreserveSig]
            new int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);

            [PreserveSig]
            int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214E6-0000-0000-C000-000000000046")]
        public interface IShellFolder
        {
            [PreserveSig]
            int ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);

            [PreserveSig]
            int EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);

            [PreserveSig]
            int BindToObject(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

            [PreserveSig]
            int BindToStorage(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

            [PreserveSig]
            int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

            [PreserveSig]
            int CreateViewObject(IntPtr hwndOwner, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

            [PreserveSig]
            int GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] apidl, ref uint rgfInOut);

            [PreserveSig]
            int GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] apidl, [In] ref Guid riid, IntPtr rgfReserved, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

        [DllImport("shell32.dll")]
        private static extern int SHBindToParent(IntPtr pidl, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv, out IntPtr ppidlLast);

        [DllImport("shell32.dll")]
        private static extern int SHGetDesktopFolder(out IShellFolder ppshf);

        [DllImport("shell32.dll")]
        private static extern void ILFree(IntPtr pidl);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hmenu);

        [DllImport("user32.dll")]
        private static extern int GetMenuItemCount(IntPtr hmenu);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Displays the native Windows context menu for a single file or folder item.
        /// </summary>
        public static void ShowContextMenu(Window ownerWindow, string itemPath, int screenX, int screenY)
        {
            if (string.IsNullOrWhiteSpace(itemPath)) return;
            ShowContextMenu(ownerWindow, new[] { itemPath }, screenX, screenY);
        }

        /// <summary>
        /// Displays the native Windows context menu for one or more selected files or folders.
        /// </summary>
        public static void ShowContextMenu(Window ownerWindow, System.Collections.Generic.IReadOnlyList<string> itemPaths, int screenX, int screenY)
        {
            if (itemPaths == null || itemPaths.Count == 0) return;

            IntPtr hwnd = new WindowInteropHelper(ownerWindow).Handle;
            var fullPidls = new System.Collections.Generic.List<IntPtr>();
            var childPidls = new System.Collections.Generic.List<IntPtr>();
            IShellFolder? parentFolder = null;

            try
            {
                Guid guidISF = IID_IShellFolder;

                foreach (var path in itemPaths)
                {
                    uint dummy = 0;
                    int hr = SHParseDisplayName(path, IntPtr.Zero, out IntPtr pidl, 0, out dummy);
                    if (hr != 0 || pidl == IntPtr.Zero) continue;

                    fullPidls.Add(pidl);

                    if (parentFolder == null)
                    {
                        hr = SHBindToParent(pidl, ref guidISF, out object parentFolderObj, out IntPtr pidlLast);
                        if (hr == 0 && parentFolderObj is IShellFolder sf && pidlLast != IntPtr.Zero)
                        {
                            parentFolder = sf;
                            childPidls.Add(pidlLast);
                        }
                    }
                    else
                    {
                        hr = SHBindToParent(pidl, ref guidISF, out _, out IntPtr pidlLast);
                        if (hr == 0 && pidlLast != IntPtr.Zero)
                        {
                            childPidls.Add(pidlLast);
                        }
                    }
                }

                if (parentFolder == null || childPidls.Count == 0) return;

                Guid guidCM = IID_IContextMenu;
                int hrCm = parentFolder.GetUIObjectOf(hwnd, (uint)childPidls.Count, childPidls.ToArray(), ref guidCM, IntPtr.Zero, out object cmObj);
                if (hrCm != 0 || cmObj is not IContextMenu contextMenu) return;

                TrackContextMenu(hwnd, contextMenu, screenX, screenY, targetFolderPath: null, itemPaths: itemPaths);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing item context menu: {ex.Message}");
            }
            finally
            {
                foreach (var p in fullPidls)
                {
                    if (p != IntPtr.Zero)
                    {
                        ILFree(p);
                    }
                }
            }
        }

        /// <summary>
        /// Displays the native Windows directory background context menu for empty column space with Paste support.
        /// </summary>
        public static void ShowFolderBackgroundMenu(Window ownerWindow, string folderPath, int screenX, int screenY)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;

            IntPtr hwnd = new WindowInteropHelper(ownerWindow).Handle;
            IntPtr pidl = IntPtr.Zero;

            try
            {
                uint dummy = 0;
                int hr = SHParseDisplayName(folderPath, IntPtr.Zero, out pidl, 0, out dummy);
                if (hr != 0 || pidl == IntPtr.Zero) return;

                hr = SHGetDesktopFolder(out IShellFolder desktop);
                if (hr != 0 || desktop == null) return;

                Guid guidISF = IID_IShellFolder;
                hr = desktop.BindToObject(pidl, IntPtr.Zero, ref guidISF, out object folderObj);
                if (hr != 0 || folderObj is not IShellFolder folder) return;

                Guid guidCM = IID_IContextMenu;
                hr = folder.CreateViewObject(hwnd, ref guidCM, out object cmObj);
                if (hr != 0 || cmObj is not IContextMenu contextMenu) return;

                TrackContextMenu(hwnd, contextMenu, screenX, screenY, folderPath, itemPaths: null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing folder background context menu: {ex.Message}");
            }
            finally
            {
                if (pidl != IntPtr.Zero)
                {
                    ILFree(pidl);
                }
            }
        }

        private static void TrackContextMenu(IntPtr hwnd, IContextMenu contextMenu, int x, int y, string? targetFolderPath, System.Collections.Generic.IReadOnlyList<string>? itemPaths = null)
        {
            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            var cm2 = contextMenu as IContextMenu2;
            var cm3 = contextMenu as IContextMenu3;

            HwndSource? hwndSource = HwndSource.FromHwnd(hwnd);
            HwndSourceHook hook = (IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (cm3 != null)
                {
                    if (msg == WM_INITMENUPOPUP || msg == WM_DRAWITEM || msg == WM_MEASUREITEM || msg == WM_MENUCHAR)
                    {
                        if (cm3.HandleMenuMsg2((uint)msg, wParam, lParam, out IntPtr lResult) == 0)
                        {
                            handled = true;
                            return lResult;
                        }
                    }
                }
                else if (cm2 != null)
                {
                    if (msg == WM_INITMENUPOPUP || msg == WM_DRAWITEM || msg == WM_MEASUREITEM)
                    {
                        if (cm2.HandleMenuMsg((uint)msg, wParam, lParam) == 0)
                        {
                            handled = true;
                            return IntPtr.Zero;
                        }
                    }
                }

                return IntPtr.Zero;
            };

            hwndSource?.AddHook(hook);

            try
            {
                contextMenu.QueryContextMenu(hMenu, 0, CMD_FIRST, CMD_LAST, CMF_NORMAL);

                // For folder background: Insert Copy Path, Paste, Open in VS Code, and Open in CMD options
                if (!string.IsNullOrEmpty(targetFolderPath))
                {
                    bool canPaste = false;
                    try
                    {
                        canPaste = Clipboard.ContainsFileDropList();
                    }
                    catch { }

                    uint pasteFlags = MF_BYPOSITION | MF_STRING | (canPaste ? MF_ENABLED : (MF_GRAYED | MF_DISABLED));
                    InsertMenu(hMenu, 0, MF_BYPOSITION | MF_STRING | MF_ENABLED, (UIntPtr)CMD_COPY_PATH, "Copy &Path");
                    InsertMenu(hMenu, 1, pasteFlags, (UIntPtr)CMD_PASTE, "&Paste\tCtrl+V");
                    InsertMenu(hMenu, 2, MF_BYPOSITION | MF_STRING | MF_ENABLED, (UIntPtr)CMD_OPEN_VSCODE, "Open with &Code");
                    InsertMenu(hMenu, 3, MF_BYPOSITION | MF_STRING | MF_ENABLED, (UIntPtr)CMD_OPEN_CMD, "Open in &Command Prompt");
                    InsertMenu(hMenu, 4, MF_BYPOSITION | MF_SEPARATOR, UIntPtr.Zero, string.Empty);
                }
                else if (itemPaths != null && itemPaths.Count > 0)
                {
                    // For file/folder items: Insert Copy Path second in order (index 1)
                    int count = GetMenuItemCount(hMenu);
                    uint insertIndex = count > 0 ? 1u : 0u;
                    string copyLabel = itemPaths.Count > 1 ? "Copy &Paths" : "Copy &Path";
                    InsertMenu(hMenu, insertIndex, MF_BYPOSITION | MF_STRING | MF_ENABLED, (UIntPtr)CMD_COPY_PATH, copyLabel);
                    InsertMenu(hMenu, insertIndex + 1, MF_BYPOSITION | MF_SEPARATOR, UIntPtr.Zero, string.Empty);
                }

                SetForegroundWindow(hwnd);
                uint cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_LEFTALIGN | TPM_RIGHTBUTTON, x, y, hwnd, IntPtr.Zero);
                PostMessage(hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

                if (cmd == CMD_COPY_PATH)
                {
                    if (itemPaths != null && itemPaths.Count > 0)
                    {
                        string text = string.Join(Environment.NewLine, itemPaths);
                        SafeSetClipboardText(text);
                    }
                    else if (!string.IsNullOrEmpty(targetFolderPath))
                    {
                        SafeSetClipboardText(targetFolderPath);
                    }
                }
                else if (cmd == CMD_PASTE && !string.IsNullOrEmpty(targetFolderPath))
                {
                    PerformPaste(hwnd, targetFolderPath);
                }
                else if (cmd == CMD_OPEN_VSCODE && !string.IsNullOrEmpty(targetFolderPath))
                {
                    OpenInVsCode(targetFolderPath);
                }
                else if (cmd == CMD_OPEN_CMD && !string.IsNullOrEmpty(targetFolderPath))
                {
                    OpenInCmd(targetFolderPath);
                }
                else if (cmd >= CMD_FIRST && cmd <= CMD_LAST)
                {
                    var pici = new CMINVOKECOMMANDINFOEX
                    {
                        cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                        fMask = CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE,
                        hwnd = hwnd,
                        lpVerb = (IntPtr)(cmd - CMD_FIRST),
                        lpVerbW = (IntPtr)(cmd - CMD_FIRST),
                        nShow = SW_SHOWNORMAL,
                        ptInvoke = new POINT(x, y)
                    };

                    contextMenu.InvokeCommand(ref pici);
                }
            }
            finally
            {
                hwndSource?.RemoveHook(hook);
                DestroyMenu(hMenu);
                Marshal.ReleaseComObject(contextMenu);
            }
        }

        /// <summary>
        /// Reliably copies text to the Windows Clipboard with retry logic.
        /// </summary>
        public static void SafeSetClipboardText(string text)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    return;
                }
                catch (COMException)
                {
                    System.Threading.Thread.Sleep(40);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to set clipboard: {ex.Message}");
                    return;
                }
            }
        }

        /// <summary>
        /// Executes a native Windows Shell file copy or move operation with standard shell progress and undo support.
        /// </summary>
        public static bool ExecuteFileOperation(IntPtr hwnd, System.Collections.Generic.IEnumerable<string> sourcePaths, string targetDirectory, bool isMove, bool recordUndo = true)
        {
            if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory)) return false;

            try
            {
                var sbFrom = new StringBuilder();
                var validList = new System.Collections.Generic.List<string>();
                string normalizedTarget = Path.GetFullPath(targetDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                foreach (var f in sourcePaths)
                {
                    if (File.Exists(f) || Directory.Exists(f))
                    {
                        string normalizedSource = Path.GetFullPath(f).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                        // If moving, avoid moving an item into its current parent folder
                        string? parent = Path.GetDirectoryName(normalizedSource);
                        if (isMove && string.Equals(parent, normalizedTarget, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Avoid moving or copying a directory into its own subfolder
                        if (Directory.Exists(f) && normalizedTarget.StartsWith(normalizedSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        sbFrom.Append(f).Append('\0');
                        validList.Add(f);
                    }
                }

                if (validList.Count == 0) return false;
                sbFrom.Append('\0');

                var op = new SHFILEOPSTRUCT
                {
                    hwnd = hwnd,
                    wFunc = isMove ? FO_MOVE : FO_COPY,
                    pFrom = sbFrom.ToString(),
                    pTo = targetDirectory + "\0\0",
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMMKDIR
                };

                int ret = SHFileOperation(ref op);
                bool success = ret == 0 && !op.fAnyOperationsAborted;

                if (success && recordUndo)
                {
                    FileUndoService.RecordAction(isMove ? UndoOperationType.Move : UndoOperationType.Copy, validList, targetDirectory);
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Shell file operation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sends one or more files or folders to the Windows Recycle Bin (supports Undo).
        /// </summary>
        public static bool SendToRecycleBin(IntPtr hwnd, System.Collections.Generic.IEnumerable<string> paths)
        {
            try
            {
                var sbFrom = new StringBuilder();
                int count = 0;
                foreach (var p in paths)
                {
                    if (File.Exists(p) || Directory.Exists(p))
                    {
                        sbFrom.Append(p).Append('\0');
                        count++;
                    }
                }
                if (count == 0) return false;
                sbFrom.Append('\0');

                var op = new SHFILEOPSTRUCT
                {
                    hwnd = hwnd,
                    wFunc = FO_DELETE,
                    pFrom = sbFrom.ToString(),
                    pTo = string.Empty,
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMMKDIR
                };

                int ret = SHFileOperation(ref op);
                return ret == 0 && !op.fAnyOperationsAborted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Recycle bin failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Puts the specified files onto the Windows Clipboard for standard Copy or Cut.
        /// </summary>
        public static void CopyFilesToClipboard(System.Collections.Generic.IEnumerable<string> paths, bool isCut)
        {
            try
            {
                var valid = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
                if (valid.Count == 0) return;

                var data = new DataObject();
                var sc = new System.Collections.Specialized.StringCollection();
                foreach (var p in valid)
                {
                    sc.Add(p);
                }
                data.SetFileDropList(sc);

                // Preferred DropEffect: 1 = DROPEFFECT_COPY, 2 = DROPEFFECT_MOVE (Cut)
                byte[] dropEffect = new byte[] { (byte)(isCut ? 2 : 1), 0, 0, 0 };
                var ms = new MemoryStream(dropEffect);
                data.SetData("Preferred DropEffect", ms);

                Clipboard.SetDataObject(data, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set clipboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Pastes files/folders currently on the Windows Clipboard into the destination directory.
        /// </summary>
        public static void PerformPaste(IntPtr hwnd, string targetDirectory)
        {
            if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory)) return;

            try
            {
                var data = Clipboard.GetDataObject();
                if (data == null || !data.GetDataPresent(DataFormats.FileDrop)) return;

                if (data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    bool isMove = false;
                    if (data.GetDataPresent("Preferred DropEffect"))
                    {
                        if (data.GetData("Preferred DropEffect") is MemoryStream ms)
                        {
                            byte[] bytes = ms.ToArray();
                            if (bytes.Length >= 4 && BitConverter.ToInt32(bytes, 0) == 2) // DROPEFFECT_MOVE
                            {
                                isMove = true;
                            }
                        }
                    }

                    bool success = ExecuteFileOperation(hwnd, files, targetDirectory, isMove);
                    if (success && isMove)
                    {
                        Clipboard.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Paste failed: {ex.Message}");
            }
        }

        private static void OpenInVsCode(string folderPath)
        {
            try
            {
                string codeExe = GetVsCodeExecutablePath();
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = codeExe,
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open VS Code: {ex.Message}");
            }
        }

        private static string GetVsCodeExecutablePath()
        {
            string localPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\Microsoft VS Code\Code.exe");
            if (File.Exists(localPath)) return localPath;

            string progFiles = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"Microsoft VS Code\Code.exe");
            if (File.Exists(progFiles)) return progFiles;

            string progFilesX86 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Microsoft VS Code\Code.exe");
            if (File.Exists(progFilesX86)) return progFilesX86;

            return "code";
        }

        private static void OpenInCmd(string folderPath)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = folderPath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open CMD: {ex.Message}");
            }
        }
    }
}
