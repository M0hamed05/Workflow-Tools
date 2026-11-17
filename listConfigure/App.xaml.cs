using System;
using System.IO;
using System.Linq;
using System.Windows;
using ContextMenuManager.Tests;

namespace ContextMenuManager
{
    public partial class App : Application
    {
        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "context_menu_studio_startup.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] App OnStartup triggered. Args: {string.Join(" ", e.Args)}\n");
            }
            catch { }

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                string msg = $"FATAL UNHANDLED EXCEPTION:\n{ex?.Message}\n\nStack:\n{ex?.StackTrace}";
                try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
                MessageBox.Show(msg, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                string msg = $"DISPATCHER EXCEPTION:\n{args.Exception.Message}\n\nStack:\n{args.Exception.StackTrace}";
                try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
                MessageBox.Show(msg, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // Silent command runner for background commands (smooth, zero console window flash)
            if (e.Args.Length >= 2 && e.Args[0].Equals("--run-silent", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string targetDir = e.Args[1];
                    string rawCmd = e.Args.Length > 2 ? string.Join(" ", e.Args.Skip(2)) : string.Empty;

                    if (!string.IsNullOrWhiteSpace(rawCmd))
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c {rawCmd}",
                            WorkingDirectory = Directory.Exists(targetDir) ? targetDir : Environment.CurrentDirectory,
                            CreateNoWindow = true,
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                            UseShellExecute = false
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                }
                catch { }

                Environment.Exit(0);
                return;
            }

            // Test execution mode
            if (e.Args.Contains("--test", StringComparer.OrdinalIgnoreCase))
            {
                var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_results.log");
                using var writer = new StreamWriter(logFile, false);
                Console.SetOut(writer);
                try
                {
                    AutomatedVerifier.RunTests();
                    writer.Flush();
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"\n[TEST ERROR]: {ex.Message}");
                    writer.WriteLine(ex.StackTrace);
                    writer.Flush();
                    Environment.Exit(1);
                }
                return;
            }

            base.OnStartup(e);
        }
    }
}
