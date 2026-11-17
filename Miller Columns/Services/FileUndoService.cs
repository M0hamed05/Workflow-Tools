using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MillerColumns.Services
{
    public enum UndoOperationType
    {
        Move,
        Copy
    }

    public class UndoEntry
    {
        public UndoOperationType Type { get; }
        public List<string> SourcePaths { get; }
        public List<string> DestinationPaths { get; }
        public string TargetDirectory { get; }

        public UndoEntry(UndoOperationType type, IEnumerable<string> sourcePaths, string targetDirectory)
        {
            Type = type;
            TargetDirectory = targetDirectory;
            SourcePaths = sourcePaths.ToList();
            DestinationPaths = new List<string>();

            foreach (var src in SourcePaths)
            {
                string name = Path.GetFileName(src.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                DestinationPaths.Add(Path.Combine(targetDirectory, name));
            }
        }

        public bool Undo(IntPtr hwnd)
        {
            try
            {
                if (Type == UndoOperationType.Move)
                {
                    // Move the files back to their original directory
                    if (DestinationPaths.Count == 0 || SourcePaths.Count == 0) return false;
                    string? origDir = Path.GetDirectoryName(SourcePaths[0]);
                    if (string.IsNullOrEmpty(origDir) || !Directory.Exists(origDir)) return false;

                    var existingDest = DestinationPaths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
                    if (existingDest.Count == 0) return false;

                    return ShellContextMenu.ExecuteFileOperation(hwnd, existingDest, origDir, isMove: true, recordUndo: false);
                }
                else if (Type == UndoOperationType.Copy)
                {
                    // Delete the copied files (send to Recycle Bin)
                    var existingDest = DestinationPaths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
                    if (existingDest.Count == 0) return false;

                    return ShellContextMenu.SendToRecycleBin(hwnd, existingDest);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Undo operation failed: {ex.Message}");
            }
            return false;
        }
    }

    public static class FileUndoService
    {
        private static readonly Stack<UndoEntry> _undoStack = new();

        public static void RecordAction(UndoOperationType type, IEnumerable<string> sourcePaths, string targetDirectory)
        {
            try
            {
                _undoStack.Push(new UndoEntry(type, sourcePaths, targetDirectory));
            }
            catch { }
        }

        public static bool Undo(IntPtr hwnd)
        {
            try
            {
                while (_undoStack.Count > 0)
                {
                    var entry = _undoStack.Pop();
                    bool success = entry.Undo(hwnd);
                    if (success) return true;
                }

                // Fall back to native Windows Shell Undo
                PerformShellSystemUndo();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Undo failed: {ex.Message}");
                return false;
            }
        }

        private static void PerformShellSystemUndo()
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType != null)
                {
                    object? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        shellType.InvokeMember("Undo", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
                        Marshal.ReleaseComObject(shell);
                    }
                }
            }
            catch { }
        }
    }
}
