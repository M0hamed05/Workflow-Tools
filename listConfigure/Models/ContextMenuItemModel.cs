using System;

namespace ContextMenuManager.Models
{
    public enum MenuLocationScope
    {
        All,
        Folders,
        EmptySpace,
        Both
    }

    public class ContextMenuItemModel
    {
        public string KeyName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public MenuLocationScope Location { get; set; }
        public string RegistryPath { get; set; } = string.Empty;
        public bool IsCurrentUser { get; set; } = true;
        public bool IsEnabled { get; set; } = true;
        public bool HasSubCommands { get; set; } = false;

        public string LocationDisplay => Location switch
        {
            MenuLocationScope.Folders => "Folder (Right-Click on Folder)",
            MenuLocationScope.EmptySpace => "Empty Space (Inside Folder)",
            MenuLocationScope.Both => "Folder & Empty Space",
            _ => "General"
        };

        public string StatusBadge => IsEnabled ? "Enabled" : "Disabled";
        public string SourceBadge => IsCurrentUser ? "Current User" : "System";
    }
}
