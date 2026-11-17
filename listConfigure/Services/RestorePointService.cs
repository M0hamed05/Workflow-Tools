using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using ContextMenuManager.Models;
using Microsoft.Win32;

namespace ContextMenuManager.Services
{
    public class RestorePointService
    {
        private readonly string _storageDir;
        private readonly string _indexFile;
        private readonly RegistryService _registryService;

        public RestorePointService(RegistryService registryService)
        {
            _registryService = registryService;
            _storageDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ContextMenuManager", "RestorePoints");
            Directory.CreateDirectory(_storageDir);
            _indexFile = Path.Combine(_storageDir, "restore_index.json");
        }

        public List<RestorePointModel> GetAllRestorePoints()
        {
            var list = new List<RestorePointModel>();
            try
            {
                if (File.Exists(_indexFile))
                {
                    var json = File.ReadAllText(_indexFile);
                    var deserialized = JsonSerializer.Deserialize<List<RestorePointModel>>(json);
                    if (deserialized != null)
                    {
                        list = deserialized;
                    }
                }
            }
            catch { }

            // Ensure pinned snapshots are always at the top, then sorted by newest first
            list.Sort((a, b) =>
            {
                if (a.IsPinned != b.IsPinned)
                    return b.IsPinned.CompareTo(a.IsPinned);
                return b.CreatedAt.CompareTo(a.CreatedAt);
            });
            return list;
        }

        public bool TogglePin(string id)
        {
            try
            {
                var list = GetAllRestorePoints();
                var point = list.Find(p => p.Id == id);
                if (point != null)
                {
                    point.IsPinned = !point.IsPinned;
                    SaveIndex(list);
                    return true;
                }
            }
            catch { }
            return false;
        }

        public RestorePointModel CreateRestorePoint(string name, string description = "")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Snapshot {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            }

            string id = $"RP_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            string pointDir = Path.Combine(_storageDir, id);
            Directory.CreateDirectory(pointDir);

            string bgRegPath = Path.Combine(pointDir, "background_shell.reg");
            string dirRegPath = Path.Combine(pointDir, "directory_shell.reg");
            string combinedRegPath = Path.Combine(pointDir, "full_restore.reg");
            string jsonSnapshotPath = Path.Combine(pointDir, "items_snapshot.json");

            // 1. Export using reg.exe for Directory\Background\shell
            ExportKeyWithRegExe(@"HKEY_CURRENT_USER\Software\Classes\Directory\Background\shell", bgRegPath);

            // 2. Export using reg.exe for Directory\shell
            ExportKeyWithRegExe(@"HKEY_CURRENT_USER\Software\Classes\Directory\shell", dirRegPath);

            // 3. Create combined .reg file
            CombineRegFiles(combinedRegPath, bgRegPath, dirRegPath);

            // 4. Capture structured items list
            var currentItems = _registryService.GetAllItems(MenuLocationScope.All);
            try
            {
                var itemsJson = JsonSerializer.Serialize(currentItems, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonSnapshotPath, itemsJson);
            }
            catch { }

            var point = new RestorePointModel
            {
                Id = id,
                Name = name,
                CreatedAt = DateTime.Now,
                Description = string.IsNullOrWhiteSpace(description) ? $"Backed up {currentItems.Count} context menu items." : description,
                RegFilePath = combinedRegPath,
                JsonSnapshotPath = jsonSnapshotPath,
                ItemCount = currentItems.Count
            };

            // Save to index
            var allPoints = GetAllRestorePoints();
            allPoints.Insert(0, point);
            SaveIndex(allPoints);

            return point;
        }

        public bool Restore(RestorePointModel point, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                if (!File.Exists(point.RegFilePath))
                {
                    errorMessage = $"Backup file not found at: {point.RegFilePath}";
                    return false;
                }

                // 1. Create a safety auto-backup of the current state before restoring!
                try
                {
                    CreateRestorePoint($"Pre-Restore Safety Backup ({DateTime.Now:HH:mm:ss})", $"Automatic backup created right before restoring '{point.Name}'");
                }
                catch { }

                // 2. Wipe current HKCU keys to prevent zombie entries that were added after snapshot
                ClearCurrentUserShellKeys();

                // 3. Import .reg file using reg.exe import
                bool imported = ImportRegFile(point.RegFilePath, out var importErr);
                if (!imported)
                {
                    errorMessage = importErr;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public bool DeleteRestorePoint(RestorePointModel point)
        {
            try
            {
                string pointDir = Path.Combine(_storageDir, point.Id);
                if (Directory.Exists(pointDir))
                {
                    Directory.Delete(pointDir, true);
                }

                var list = GetAllRestorePoints();
                list.RemoveAll(p => p.Id == point.Id);
                SaveIndex(list);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ExportRegToUserLocation(RestorePointModel point, string targetFilePath)
        {
            try
            {
                if (File.Exists(point.RegFilePath))
                {
                    File.Copy(point.RegFilePath, targetFilePath, true);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void ClearCurrentUserShellKeys()
        {
            try
            {
                using var bgKey = Registry.CurrentUser.OpenSubKey(RegistryService.BackgroundShellUserPath, true);
                if (bgKey != null)
                {
                    foreach (var sub in bgKey.GetSubKeyNames())
                    {
                        try { bgKey.DeleteSubKeyTree(sub); } catch { }
                    }
                }
            }
            catch { }

            try
            {
                using var dirKey = Registry.CurrentUser.OpenSubKey(RegistryService.FolderShellUserPath, true);
                if (dirKey != null)
                {
                    foreach (var sub in dirKey.GetSubKeyNames())
                    {
                        try { dirKey.DeleteSubKeyTree(sub); } catch { }
                    }
                }
            }
            catch { }
        }

        private static bool ExportKeyWithRegExe(string keyPath, string outputFilePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"export \"{keyPath}\" \"{outputFilePath}\" /y",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool ImportRegFile(string regFilePath, out string error)
        {
            error = string.Empty;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"import \"{regFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                error = proc?.StandardError.ReadToEnd() ?? string.Empty;
                proc?.WaitForExit(5000);

                return proc?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void CombineRegFiles(string combinedPath, string file1, string file2)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Windows Registry Editor Version 5.00");
                sb.AppendLine();

                void AppendContent(string path)
                {
                    if (File.Exists(path))
                    {
                        var lines = File.ReadAllLines(path);
                        foreach (var line in lines)
                        {
                            if (line.Trim().StartsWith("Windows Registry Editor Version", StringComparison.OrdinalIgnoreCase))
                                continue;
                            sb.AppendLine(line);
                        }
                    }
                }

                AppendContent(file1);
                sb.AppendLine();
                AppendContent(file2);

                File.WriteAllText(combinedPath, sb.ToString(), Encoding.Unicode);
            }
            catch { }
        }

        private void SaveIndex(List<RestorePointModel> points)
        {
            try
            {
                var json = JsonSerializer.Serialize(points, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_indexFile, json);
            }
            catch { }
        }
    }
}
