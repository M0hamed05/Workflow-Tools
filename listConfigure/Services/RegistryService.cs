using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ContextMenuManager.Models;
using Microsoft.Win32;

namespace ContextMenuManager.Services
{
    public class RegistryService
    {
        public const string FolderShellUserPath = @"Software\Classes\Directory\shell";
        public const string BackgroundShellUserPath = @"Software\Classes\Directory\Background\shell";

        public const string FolderShellRootPath = @"Directory\shell";
        public const string BackgroundShellRootPath = @"Directory\Background\shell";

        public const string CascadeMenuKeyName = "CustomActionsMenu";

        public List<ContextMenuItemModel> GetAllItems(MenuLocationScope filter = MenuLocationScope.All)
        {
            var results = new Dictionary<string, ContextMenuItemModel>(StringComparer.OrdinalIgnoreCase);

            // 1. Read HKCU Folder items
            ReadItemsFromKey(Registry.CurrentUser, FolderShellUserPath, MenuLocationScope.Folders, true, results);

            // 2. Read HKCU Background items
            ReadItemsFromKey(Registry.CurrentUser, BackgroundShellUserPath, MenuLocationScope.EmptySpace, true, results);

            // 3. Read HKCR Folder items (System/All users)
            ReadItemsFromKey(Registry.ClassesRoot, FolderShellRootPath, MenuLocationScope.Folders, false, results);

            // 4. Read HKCR Background items (System/All users)
            ReadItemsFromKey(Registry.ClassesRoot, BackgroundShellRootPath, MenuLocationScope.EmptySpace, false, results);

            var list = new List<ContextMenuItemModel>(results.Values);

            // Apply filter
            if (filter == MenuLocationScope.Folders)
            {
                list = list.FindAll(i => i.Location == MenuLocationScope.Folders || i.Location == MenuLocationScope.Both);
            }
            else if (filter == MenuLocationScope.EmptySpace)
            {
                list = list.FindAll(i => i.Location == MenuLocationScope.EmptySpace || i.Location == MenuLocationScope.Both);
            }

            return list;
        }

        private void ReadItemsFromKey(RegistryKey root, string subPath, MenuLocationScope scope, bool isCurrentUser, Dictionary<string, ContextMenuItemModel> targetDict)
        {
            try
            {
                using var key = root.OpenSubKey(subPath);
                if (key == null) return;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var itemKey = key.OpenSubKey(subKeyName);
                        if (itemKey == null) continue;

                        // De-duplication: If item already exists, merge location and do not re-add
                        if (targetDict.TryGetValue(subKeyName, out var existing))
                        {
                            if (existing.Location != scope && existing.Location != MenuLocationScope.Both)
                            {
                                existing.Location = MenuLocationScope.Both;
                            }
                            continue;
                        }

                        string displayName = itemKey.GetValue("MUIVerb") as string
                            ?? itemKey.GetValue("") as string
                            ?? subKeyName;

                        string icon = itemKey.GetValue("Icon") as string ?? string.Empty;

                        string command = string.Empty;
                        using var cmdKey = itemKey.OpenSubKey("command");
                        if (cmdKey != null)
                        {
                            command = cmdKey.GetValue("") as string ?? string.Empty;
                        }

                        bool isLegacyDisabled = itemKey.GetValue("LegacyDisable") != null;
                        bool isProgrammaticAccessOnly = itemKey.GetValue("ProgrammaticAccessOnly") != null;
                        bool isEnabled = !isLegacyDisabled && !isProgrammaticAccessOnly;

                        bool hasSubCommands = itemKey.GetValue("SubCommands") != null || 
                                              itemKey.GetValue("ExtendedSubCommandsKey") != null ||
                                              itemKey.OpenSubKey("shell") != null;

                        var model = new ContextMenuItemModel
                        {
                            KeyName = subKeyName,
                            DisplayName = displayName,
                            Command = command,
                            Icon = icon,
                            Location = scope,
                            RegistryPath = $@"{root.Name}\{subPath}\{subKeyName}",
                            IsCurrentUser = isCurrentUser,
                            IsEnabled = isEnabled,
                            HasSubCommands = hasSubCommands
                        };

                        targetDict[subKeyName] = model;
                    }
                    catch
                    {
                        // Ignore unreadable individual subkeys
                    }
                }
            }
            catch
            {
                // Root key cannot be opened or does not exist
            }
        }

        public bool AddOrUpdateItem(string keyName, string displayName, string command, string icon, MenuLocationScope scope)
        {
            keyName = SanitizeKeyName(keyName);
            if (string.IsNullOrWhiteSpace(keyName)) return false;

            if (scope == MenuLocationScope.Folders || scope == MenuLocationScope.Both)
            {
                WriteSingleKey(FolderShellUserPath, keyName, displayName, command, icon);
            }

            if (scope == MenuLocationScope.EmptySpace || scope == MenuLocationScope.Both)
            {
                WriteSingleKey(BackgroundShellUserPath, keyName, displayName, command, icon);
            }

            return true;
        }

        private void WriteSingleKey(string basePath, string keyName, string displayName, string command, string icon)
        {
            using var baseKey = Registry.CurrentUser.CreateSubKey(basePath, true);
            using var itemKey = baseKey.CreateSubKey(keyName, true);

            itemKey.SetValue("", displayName);
            itemKey.SetValue("MUIVerb", displayName);

            if (!string.IsNullOrWhiteSpace(icon))
            {
                itemKey.SetValue("Icon", icon);
            }
            else
            {
                // Remove icon if cleared
                try { itemKey.DeleteValue("Icon"); } catch { }
            }

            // Remove any disabled flags
            try { itemKey.DeleteValue("LegacyDisable"); } catch { }
            try { itemKey.DeleteValue("ProgrammaticAccessOnly"); } catch { }

            using var cmdKey = itemKey.CreateSubKey("command", true);
            cmdKey.SetValue("", command);
        }

        public bool DeleteItem(ContextMenuItemModel item)
        {
            try
            {
                bool deletedAny = false;

                if (item.Location == MenuLocationScope.Folders || item.Location == MenuLocationScope.Both)
                {
                    using var baseKey = Registry.CurrentUser.OpenSubKey(FolderShellUserPath, true);
                    if (baseKey != null)
                    {
                        try
                        {
                            baseKey.DeleteSubKeyTree(item.KeyName);
                            deletedAny = true;
                        }
                        catch { }
                    }
                }

                if (item.Location == MenuLocationScope.EmptySpace || item.Location == MenuLocationScope.Both)
                {
                    using var baseKey = Registry.CurrentUser.OpenSubKey(BackgroundShellUserPath, true);
                    if (baseKey != null)
                    {
                        try
                        {
                            baseKey.DeleteSubKeyTree(item.KeyName);
                            deletedAny = true;
                        }
                        catch { }
                    }
                }

                // If it was a system key, we can disable it via HKCU or attempt HKLM deletion if elevated
                if (!item.IsCurrentUser)
                {
                    // Disable it for current user via LegacyDisable
                    ToggleItem(item, false);
                    deletedAny = true;
                }

                return deletedAny;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete item: {ex.Message}");
                return false;
            }
        }

        public bool ToggleItem(ContextMenuItemModel item, bool enable)
        {
            try
            {
                void TogglePath(string basePath)
                {
                    using var baseKey = Registry.CurrentUser.CreateSubKey(basePath, true);
                    using var itemKey = baseKey.CreateSubKey(item.KeyName, true);
                    if (enable)
                    {
                        try { itemKey.DeleteValue("LegacyDisable"); } catch { }
                        try { itemKey.DeleteValue("ProgrammaticAccessOnly"); } catch { }
                    }
                    else
                    {
                        itemKey.SetValue("LegacyDisable", "");
                    }
                }

                if (item.Location == MenuLocationScope.Folders || item.Location == MenuLocationScope.Both)
                {
                    TogglePath(FolderShellUserPath);
                }

                if (item.Location == MenuLocationScope.EmptySpace || item.Location == MenuLocationScope.Both)
                {
                    TogglePath(BackgroundShellUserPath);
                }

                item.IsEnabled = enable;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool RestartExplorer()
        {
            try
            {
                var killProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = "/f /im explorer.exe",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };
                killProcess.Start();
                killProcess.WaitForExit(3000);

                var startProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        UseShellExecute = true
                    }
                };
                startProcess.Start();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string SanitizeKeyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "CustomItem";
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var ch in invalidChars)
            {
                name = name.Replace(ch.ToString(), "");
            }
            return name.Trim().Replace(" ", "_");
        }
    }
}
