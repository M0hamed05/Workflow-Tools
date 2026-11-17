using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ContextMenuManager.Models;
using Microsoft.Win32;

namespace ContextMenuManager.Services
{
    public class CustomMenuConfig
    {
        public string MenuTitle { get; set; } = "Custom Tools";
        public string MenuIcon { get; set; } = "cmd.exe";
        public List<CustomToolItem> Tools { get; set; } = new();
    }

    public class CustomMenuService
    {
        public const string CascadeMenuKey = "CustomTools";
        private readonly string _configFilePath;

        public CustomMenuService()
        {
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ContextMenuManager");
            Directory.CreateDirectory(appDataDir);
            _configFilePath = Path.Combine(appDataDir, "custom_tools.json");
        }

        public CustomMenuConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<CustomMenuConfig>(json);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch { }

            // Return default starter pack with the exact requested commands!
            var defaultConfig = new CustomMenuConfig();
            defaultConfig.Tools.Add(new CustomToolItem
            {
                Command = "code .",
                RunInCmd = false,
                ShowInFolders = true,
                ShowInEmptySpace = true
            });
            defaultConfig.Tools.Add(new CustomToolItem
            {
                Command = "cmd",
                RunInCmd = true,
                ShowInFolders = true,
                ShowInEmptySpace = true
            });
            defaultConfig.Tools.Add(new CustomToolItem
            {
                Command = "powershell",
                RunInCmd = true,
                ShowInFolders = true,
                ShowInEmptySpace = true
            });
            defaultConfig.Tools.Add(new CustomToolItem
            {
                Command = "npx --version",
                RunInCmd = true,
                ShowInFolders = true,
                ShowInEmptySpace = true
            });

            SaveConfig(defaultConfig);
            return defaultConfig;
        }

        public void SaveConfig(CustomMenuConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch { }
        }

        public bool DeployToRegistry(CustomMenuConfig config)
        {
            try
            {
                SaveConfig(config);

                // 1. Remove previous instance first to ensure clean state
                RemoveFromRegistry();

                if (config.Tools.Count == 0) return true;

                // 2. Deploy to Directory\Background\shell (Empty Space) - only active tools
                var bgTools = config.Tools.FindAll(t => t.ShowInEmptySpace && t.IsEnabled);
                if (bgTools.Count > 0)
                {
                    WriteCascadeKey(RegistryService.BackgroundShellUserPath, config.MenuTitle, config.MenuIcon, bgTools);
                }

                // 3. Deploy to Directory\shell (Folders) - only active tools
                var folderTools = config.Tools.FindAll(t => t.ShowInFolders && t.IsEnabled);
                if (folderTools.Count > 0)
                {
                    WriteCascadeKey(RegistryService.FolderShellUserPath, config.MenuTitle, config.MenuIcon, folderTools);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void WriteCascadeKey(string basePath, string menuTitle, string menuIcon, List<CustomToolItem> tools)
        {
            using var baseKey = Registry.CurrentUser.CreateSubKey(basePath, true);
            using var cascadeKey = baseKey.CreateSubKey(CascadeMenuKey, true);

            // CRITICAL: (Default) value must NOT be set on the cascade parent container key!
            try { cascadeKey.DeleteValue("", false); } catch { }

            // CRITICAL: SubCommands must NOT be set when using ExtendedSubCommandsKey!
            // When SubCommands is set (even as an empty string), Windows Explorer executes BOTH
            // the SubCommands provider and the ExtendedSubCommandsKey provider, duplicating every item!
            try { cascadeKey.DeleteValue("SubCommands", false); } catch { }

            cascadeKey.SetValue("MUIVerb", menuTitle);

            string relativePath = basePath.Replace(@"Software\Classes\", "", StringComparison.OrdinalIgnoreCase);
            cascadeKey.SetValue("ExtendedSubCommandsKey", $@"{relativePath}\{CascadeMenuKey}");

            // Menu icon is always cmd.exe
            cascadeKey.SetValue("Icon", "cmd.exe");

            using var subShellKey = cascadeKey.CreateSubKey("shell", true);
            for (int i = 0; i < tools.Count; i++)
            {
                var tool = tools[i];
                string itemKeyName = $"Item_{i + 1}_{SanitizeKey(tool.Title)}";

                using var itemKey = subShellKey.CreateSubKey(itemKeyName, true);
                itemKey.SetValue("", tool.Title);
                itemKey.SetValue("MUIVerb", tool.Title);

                using var commandKey = itemKey.CreateSubKey("command", true);
                commandKey.SetValue("", tool.BuildRegistryCommand());
            }
        }

        public bool RemoveFromRegistry()
        {
            try
            {
                using (var bgKey = Registry.CurrentUser.OpenSubKey(RegistryService.BackgroundShellUserPath, true))
                {
                    if (bgKey != null)
                    {
                        try { bgKey.DeleteSubKeyTree(CascadeMenuKey); } catch { }
                    }
                }

                using (var dirKey = Registry.CurrentUser.OpenSubKey(RegistryService.FolderShellUserPath, true))
                {
                    if (dirKey != null)
                    {
                        try { dirKey.DeleteSubKeyTree(CascadeMenuKey); } catch { }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsDeployedInRegistry()
        {
            try
            {
                using var bgKey = Registry.CurrentUser.OpenSubKey($@"{RegistryService.BackgroundShellUserPath}\{CascadeMenuKey}");
                using var dirKey = Registry.CurrentUser.OpenSubKey($@"{RegistryService.FolderShellUserPath}\{CascadeMenuKey}");
                return bgKey != null || dirKey != null;
            }
            catch
            {
                return false;
            }
        }

        public bool IsMenuDisabled()
        {
            try
            {
                using var bgKey = Registry.CurrentUser.OpenSubKey($@"{RegistryService.BackgroundShellUserPath}\{CascadeMenuKey}");
                return bgKey?.GetValue("LegacyDisable") != null;
            }
            catch
            {
                return false;
            }
        }

        public bool ToggleMenuDisabled(out bool isNowDisabled)
        {
            isNowDisabled = false;
            try
            {
                using var bgKey = Registry.CurrentUser.OpenSubKey(RegistryService.BackgroundShellUserPath, true);
                using var dirKey = Registry.CurrentUser.OpenSubKey(RegistryService.FolderShellUserPath, true);

                using var subBg = bgKey?.OpenSubKey(CascadeMenuKey, true);
                using var subDir = dirKey?.OpenSubKey(CascadeMenuKey, true);

                if (subBg != null)
                {
                    var val = subBg.GetValue("LegacyDisable");
                    if (val != null)
                    {
                        subBg.DeleteValue("LegacyDisable", false);
                        subDir?.DeleteValue("LegacyDisable", false);
                        isNowDisabled = false;
                    }
                    else
                    {
                        subBg.SetValue("LegacyDisable", "");
                        subDir?.SetValue("LegacyDisable", "");
                        isNowDisabled = true;
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static string SanitizeKey(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "Tool";
            var chars = Path.GetInvalidFileNameChars();
            foreach (var c in chars)
            {
                title = title.Replace(c.ToString(), "");
            }
            return title.Replace(" ", "_").Replace(".", "_");
        }
    }
}
