using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MillerColumns.Services
{
    public class SessionData
    {
        public List<string> ColumnPaths { get; set; } = new();
        public List<string?> SelectedItemPaths { get; set; } = new();
        public string? Theme { get; set; }
        public bool IsSidebarMinimized { get; set; }
    }

    public static class SessionService
    {
        private static readonly string SessionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MillerColumns",
            "session.json");

        public static void SaveSession(SessionData session)
        {
            try
            {
                if (session == null || session.ColumnPaths.Count == 0) return;

                string? dir = Path.GetDirectoryName(SessionFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SessionFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save session: {ex.Message}");
            }
        }

        public static SessionData? LoadSession()
        {
            try
            {
                if (!File.Exists(SessionFilePath)) return null;

                string json = File.ReadAllText(SessionFilePath);
                return JsonSerializer.Deserialize<SessionData>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load session: {ex.Message}");
                return null;
            }
        }
    }
}
