using System;

namespace ContextMenuManager.Models
{
    public class CustomToolItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        private string _command = string.Empty;
        public string Command
        {
            get => _command;
            set => _command = value ?? string.Empty;
        }

        // Logic Change: The tool name is ALWAYS identical to the command itself!
        public string Title
        {
            get => string.IsNullOrWhiteSpace(_command) ? "Custom Command" : _command.Trim();
            set { /* read-only computed from command */ }
        }

        public bool RunInCmd { get; set; } = false;
        public bool ShowInFolders { get; set; } = true;
        public bool ShowInEmptySpace { get; set; } = true;
        public bool IsEnabled { get; set; } = true;

        public string StatusBadge => IsEnabled ? "Active" : "Disabled";

        // Display string for the grid
        public string TargetDisplay => (ShowInFolders && ShowInEmptySpace) 
            ? "Folders & Empty Space" 
            : ShowInFolders ? "Folders Only" : "Empty Space Only";

        // Read-only getter with empty setter to prevent TwoWay binding exception when clicking grid cells
        public string ExecutionModeDisplay
        {
            get => RunInCmd ? "Runs in CMD (Keeps Open)" : "Quiet Background (No CMD Window)";
            set { }
        }

        public string BuildRegistryCommand()
        {
            var rawCmd = Command.Trim();
            if (string.IsNullOrWhiteSpace(rawCmd))
            {
                return "cmd.exe /k cd /d \"%V\"";
            }

            // If user explicitly entered 'cmd' or 'cmd.exe', open interactive cmd
            if (rawCmd.Equals("cmd", StringComparison.OrdinalIgnoreCase) || 
                rawCmd.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase))
            {
                return "cmd.exe /k cd /d \"%V\"";
            }

            // If user typed 'powershell' or 'pwsh'
            if (rawCmd.Equals("powershell", StringComparison.OrdinalIgnoreCase) || 
                rawCmd.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
            {
                return "powershell.exe -NoExit -Command \"Set-Location -LiteralPath '%V'\"";
            }

            // When checked: Run inside CMD and keep terminal open for logs/output/interaction
            // NOTE: No outer quotes around the whole command — cmd.exe /k takes the rest of the
            // line as the command directly. %V is quoted individually to handle spaces in paths.
            if (RunInCmd)
            {
                return $"cmd.exe /k cd /d \"%V\" && {rawCmd}";
            }
            else
            {
                // When unchecked: Run smoothly with ZERO console window flash or flicker!
                // Using our GUI binary launcher with --run-silent executes via cmd /c with CREATE_NO_WINDOW
                // avoiding the brief black PowerShell console window flash.
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                {
                    return $"\"{exePath}\" --run-silent \"%V\" \"{rawCmd.Replace("\"", "\\\"")}\"";
                }

                // Clean fallback
                return $"powershell.exe -WindowStyle Hidden -NoProfile -Command \"Set-Location -LiteralPath '%V'; & {rawCmd}\"";
            }
        }
    }
}
