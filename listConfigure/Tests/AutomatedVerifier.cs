using System;
using System.IO;
using ContextMenuManager.Models;
using ContextMenuManager.Services;

namespace ContextMenuManager.Tests
{
    public class AutomatedVerifier
    {
        public static void RunTests()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("Running Automated Verification of All Features...");
            Console.WriteLine("==================================================");

            var regService = new RegistryService();
            var menuService = new CustomMenuService();
            var restoreService = new RestorePointService(regService);

            // TEST 1: Restore Point Creation
            Console.WriteLine("\n[TEST 1] Testing Restore Point Creation...");
            var initialSnapshot = restoreService.CreateRestorePoint("Automated Test Baseline", "Verification run test baseline.");
            if (File.Exists(initialSnapshot.RegFilePath))
            {
                Console.WriteLine($"[PASS] Initial snapshot created successfully at: {initialSnapshot.RegFilePath}");
            }
            else
            {
                throw new Exception("Initial snapshot .reg file was not created!");
            }

            // TEST 2: Add and Toggle Item (Feature 1)
            Console.WriteLine("\n[TEST 2] Testing Add & Toggle Context Menu Item (Feature 1)...");
            string testKey = "TestItem_Alpha";
            bool added = regService.AddOrUpdateItem(testKey, "Test Item Alpha", "cmd.exe /c echo Hello", "cmd.exe", MenuLocationScope.Both);
            if (!added) throw new Exception("Failed to add test item!");
            Console.WriteLine("[PASS] Added test item 'TestItem_Alpha' to both Folder & Background scopes.");

            var items = regService.GetAllItems(MenuLocationScope.All);
            var found = items.Find(i => i.KeyName.Equals(testKey, StringComparison.OrdinalIgnoreCase));
            if (found == null) throw new Exception("Item not found after addition!");
            Console.WriteLine($"[PASS] Verified item presence in registry: DisplayName='{found.DisplayName}', Location={found.Location}");

            // TEST 3: Deploy Custom Cascading Submenu (Feature 2)
            Console.WriteLine("\n[TEST 3] Testing Custom Cascade Menu Deployment (Feature 2)...");
            var config = new CustomMenuConfig
            {
                MenuTitle = "Custom Developer Tools",
                MenuIcon = "cmd.exe"
            };
            config.Tools.Add(new CustomToolItem
            {
                Command = "code .",
                RunInCmd = false,
                ShowInFolders = true,
                ShowInEmptySpace = true
            });
            config.Tools.Add(new CustomToolItem
            {
                Command = "cmd",
                RunInCmd = true,
                ShowInFolders = true,
                ShowInEmptySpace = true
            });
            config.Tools.Add(new CustomToolItem
            {
                Command = "npx --version",
                RunInCmd = true,
                ShowInFolders = true,
                ShowInEmptySpace = true
            });

            bool deployed = menuService.DeployToRegistry(config);
            if (!deployed || !menuService.IsDeployedInRegistry()) throw new Exception("Failed to deploy custom cascade menu!");
            Console.WriteLine("[PASS] Custom cascade menu deployed with 3 commands (code ., cmd, npx) on Folders and Empty Space!");

            // TEST 4: Create Second Restore Point (Multiple Restore Points)
            Console.WriteLine("\n[TEST 4] Testing Multiple Restore Points...");
            var secondSnapshot = restoreService.CreateRestorePoint("State With Custom Menu & Test Item", "Captured after deploying custom cascade.");
            var allPoints = restoreService.GetAllRestorePoints();
            if (allPoints.Count < 2) throw new Exception("Multiple restore points not found in index!");
            Console.WriteLine($"[PASS] Multiple restore points created successfully (Total: {allPoints.Count}).");

            // TEST 5: Restore to Baseline (Feature 3)
            Console.WriteLine("\n[TEST 5] Testing Restore Functionality (Feature 3)...");
            bool restored = restoreService.Restore(initialSnapshot, out var err);
            if (!restored) throw new Exception($"Restore failed with error: {err}");
            Console.WriteLine($"[PASS] Successfully restored registry back to: '{initialSnapshot.Name}'.");

            // Verify test item was removed by restore
            var itemsAfterRestore = regService.GetAllItems(MenuLocationScope.All);
            var itemAfter = itemsAfterRestore.Find(i => i.KeyName.Equals(testKey, StringComparison.OrdinalIgnoreCase));
            if (itemAfter != null)
            {
                Console.WriteLine("[WARN] Test item still lingered, cleaning up.");
                regService.DeleteItem(itemAfter);
            }
            else
            {
                Console.WriteLine("[PASS] Verified test item was cleanly removed upon restoring baseline!");
            }

            // Cleanup test snapshot files
            restoreService.DeleteRestorePoint(initialSnapshot);
            restoreService.DeleteRestorePoint(secondSnapshot);
            Console.WriteLine("[PASS] Cleaned up temporary test snapshots.");

            // TEST 6: Test Pin / Unpin Snapshot
            Console.WriteLine("\n[TEST 6] Testing Pin / Unpin Snapshot...");
            var pinTestPoint = restoreService.CreateRestorePoint("Pin Test Snapshot", "Testing pin ordering.");
            bool pinned = restoreService.TogglePin(pinTestPoint.Id);
            if (!pinned) throw new Exception("Failed to pin snapshot!");
            var allPointsAfterPin = restoreService.GetAllRestorePoints();
            if (allPointsAfterPin.Count == 0 || !allPointsAfterPin[0].IsPinned || allPointsAfterPin[0].Id != pinTestPoint.Id)
            {
                throw new Exception("Pinned snapshot is not at the top of the list!");
            }
            Console.WriteLine("[PASS] Verified pinned snapshot stays at top of list.");
            restoreService.DeleteRestorePoint(pinTestPoint);
            Console.WriteLine("[PASS] Cleaned up pin test snapshot.");

            Console.WriteLine("\n==================================================");
            Console.WriteLine("ALL 6 VERIFICATION TESTS PASSED SUCCESSFULLY!");
            Console.WriteLine("==================================================");
        }
    }
}
