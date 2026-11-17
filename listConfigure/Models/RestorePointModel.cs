using System;

namespace ContextMenuManager.Models
{
    public class RestorePointModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Description { get; set; } = string.Empty;
        public string RegFilePath { get; set; } = string.Empty;
        public string JsonSnapshotPath { get; set; } = string.Empty;
        public int ItemCount { get; set; } = 0;
        public bool IsPinned { get; set; } = false;

        public string PinBadge => IsPinned ? "📌 Pinned" : "";
        public string FormattedDate => CreatedAt.ToString("MMM dd, yyyy - hh:mm:ss tt");
    }

    public class RegistryKeySnapshot
    {
        public string KeyPath { get; set; } = string.Empty;
        public Dictionary<string, string> Values { get; set; } = new();
        public List<RegistryKeySnapshot> SubKeys { get; set; } = new();
    }

    public class ContextMenuSnapshotData
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Description { get; set; } = string.Empty;
        public List<RegistryKeySnapshot> Snapshots { get; set; } = new();
    }
}
