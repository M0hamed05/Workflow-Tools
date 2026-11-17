using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ContextMenuManager.Dialogs;
using ContextMenuManager.Models;
using ContextMenuManager.Services;
using Microsoft.Win32;

namespace ContextMenuManager
{
    public partial class MainWindow : Window
    {
        private readonly RegistryService _registryService;
        private readonly CustomMenuService _customMenuService;
        private readonly RestorePointService _restorePointService;

        private CustomMenuConfig _customConfig = new();
        private ObservableCollection<CustomToolItem> _customToolsObservable = new();

        private bool _firstActivation = true;

        public MainWindow()
        {
            InitializeComponent();

            _registryService = new RegistryService();
            _customMenuService = new CustomMenuService();
            _restorePointService = new RestorePointService(_registryService);

            // Ensure window position is visible on screen (fix off-screen issues with multi-monitor setups)
            Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - Width) / 2);
            Top = Math.Max(0, (SystemParameters.PrimaryScreenHeight - Height) / 2);

            Loaded += MainWindow_Loaded;
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (_firstActivation)
            {
                _firstActivation = false;
                // Briefly set Topmost to bring window to front, then release it
                Topmost = true;
                Activate();
                Topmost = false;
                Focus();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initial load of custom tools config
                _customConfig = _customMenuService.LoadConfig();
                if (TxtCustomMenuTitle != null) TxtCustomMenuTitle.Text = _customConfig.MenuTitle;

                _customToolsObservable = new ObservableCollection<CustomToolItem>(_customConfig.Tools);
                if (GridCustomTools != null) GridCustomTools.ItemsSource = _customToolsObservable;
                if (ListPreviewChildren != null) ListPreviewChildren.ItemsSource = _customToolsObservable;
                if (TxtPreviewMenuName != null) TxtPreviewMenuName.Text = _customConfig.MenuTitle;

                // Load restore points
                RefreshRestorePoints();

                // Update Entire Menu Toggle Button State
                UpdateMenuToggleButtonState(_customMenuService.IsMenuDisabled());

                // Auto-create initial baseline snapshot if no restore points exist yet
                var existingPoints = _restorePointService.GetAllRestorePoints();
                if (existingPoints.Count == 0)
                {
                    try
                    {
                        _restorePointService.CreateRestorePoint("Initial Baseline Context Menu", "Initial baseline snapshot created automatically on first run.");
                        RefreshRestorePoints();
                    }
                    catch { }
                }
            }
            catch { }
        }

        #region Custom Tools Cascading Menu

        private void BtnDeployCascade_Click(object sender, RoutedEventArgs e)
        {
            if (_customToolsObservable.Count == 0)
            {
                MessageBox.Show("Please add at least one tool command before deploying.", "No Tools", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string title = TxtCustomMenuTitle.Text.Trim();
            if (string.IsNullOrWhiteSpace(title)) title = "Custom Tools";

            _customConfig.MenuTitle = title;
            _customConfig.MenuIcon = "cmd.exe";
            _customConfig.Tools = _customToolsObservable.ToList();

            // Create backup snapshot ONLY if the user explicitly checked the option
            if (ChkCreateBackupOnDeploy?.IsChecked == true)
            {
                _restorePointService.CreateRestorePoint($"Backup before deploying '{title}'", $"Configured {title} with {_customConfig.Tools.Count} commands.");
                RefreshRestorePoints();
            }

            bool success = _customMenuService.DeployToRegistry(_customConfig);
            if (success)
            {
                RefreshRestorePoints();
                UpdateMenuToggleButtonState(false);

                int activeCount = _customConfig.Tools.Count(t => t.IsEnabled);
                MessageBox.Show(
                    $"Custom menu '{title}' with {activeCount} active tool(s) is deployed successfully!",
                    "Deploy Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Failed to deploy cascading menu to registry.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRemoveCascade_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show("Are you sure you want to remove the custom submenu from Windows context menu?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                bool success = _customMenuService.RemoveFromRegistry();
                if (success)
                {
                    RefreshRestorePoints();
                    MessageBox.Show("Custom cascading menu removed from context menu.", "Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnToggleEntireMenu_Click(object sender, RoutedEventArgs e)
        {
            bool toggled = _customMenuService.ToggleMenuDisabled(out bool isNowDisabled);
            if (toggled)
            {
                UpdateMenuToggleButtonState(isNowDisabled);
                string menuTitle = TxtCustomMenuTitle?.Text.Trim() ?? "Custom Tools";
                string stateDesc = isNowDisabled ? "Disabled (Hidden)" : "Enabled (Visible)";
                MessageBox.Show($"Custom menu '{menuTitle}' is now {stateDesc} in Windows context menu.", 
                    "Menu State Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Please deploy the menu first using 'Apply & Deploy to Windows' before toggling its Windows state.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UpdateMenuToggleButtonState(bool isNowDisabled)
        {
            if (BtnToggleEntireMenu == null) return;
            if (isNowDisabled)
            {
                BtnToggleEntireMenu.Content = "⚡ Enable Menu";
                BtnToggleEntireMenu.ToolTip = "The Custom Tools menu is currently DISABLED in Windows. Click to enable.";
            }
            else
            {
                BtnToggleEntireMenu.Content = "⚡ Disable Menu";
                BtnToggleEntireMenu.ToolTip = "The Custom Tools menu is currently ACTIVE in Windows. Click to disable.";
            }
        }

        private void BtnToggleTool_Click(object sender, RoutedEventArgs e)
        {
            var selectedTools = GridCustomTools.SelectedItems.Cast<CustomToolItem>().ToList();
            if (selectedTools.Count == 0)
            {
                MessageBox.Show("Please select tool(s) from the table to toggle their Active/Disabled state.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var tool in selectedTools)
            {
                tool.IsEnabled = !tool.IsEnabled;
            }
            SaveAndRefreshCustomTools();
            GridCustomTools.Items.Refresh();

            string statusMsg = selectedTools.Count == 1 
                ? $"Tool '{selectedTools[0].Title}' is now {(selectedTools[0].IsEnabled ? "Active" : "Disabled")}."
                : $"Toggled {selectedTools.Count} tools.";
            MessageBox.Show(statusMsg + "\n\nClick 'Apply & Deploy to Windows' to update Explorer.", "Tool State Toggled", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRefreshCustomTools_Click(object sender, RoutedEventArgs e)
        {
            _customConfig = _customMenuService.LoadConfig();
            if (TxtCustomMenuTitle != null) TxtCustomMenuTitle.Text = _customConfig.MenuTitle;
            _customToolsObservable = new ObservableCollection<CustomToolItem>(_customConfig.Tools);
            if (GridCustomTools != null) GridCustomTools.ItemsSource = _customToolsObservable;
            if (ListPreviewChildren != null) ListPreviewChildren.ItemsSource = _customToolsObservable;
            if (TxtPreviewMenuName != null) TxtPreviewMenuName.Text = _customConfig.MenuTitle;
            UpdateMenuToggleButtonState(_customMenuService.IsMenuDisabled());
        }

        private void BtnAddCustomTool_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddEditToolDialog();
            dlg.Owner = this;

            if (dlg.ShowDialog() == true)
            {
                _customToolsObservable.Add(dlg.Tool);
                SaveAndRefreshCustomTools();
            }
        }

        private void BtnEditCustomTool_Click(object sender, RoutedEventArgs e)
        {
            if (GridCustomTools.SelectedItem is not CustomToolItem selected)
            {
                MessageBox.Show("Please select a tool from the list to edit.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new AddEditToolDialog(selected);
            dlg.Owner = this;

            if (dlg.ShowDialog() == true)
            {
                int index = _customToolsObservable.IndexOf(selected);
                if (index >= 0)
                {
                    _customToolsObservable[index] = dlg.Tool;
                    SaveAndRefreshCustomTools();
                }
            }
        }

        private void BtnDeleteCustomTool_Click(object sender, RoutedEventArgs e)
        {
            var selectedTools = GridCustomTools.SelectedItems.Cast<CustomToolItem>().ToList();
            if (selectedTools.Count == 0)
            {
                MessageBox.Show("Please select tool(s) to delete.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to delete {selectedTools.Count} tool command(s)?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                foreach (var tool in selectedTools)
                {
                    _customToolsObservable.Remove(tool);
                }
                SaveAndRefreshCustomTools();
            }
        }

        private void BtnMoveToolUp_Click(object sender, RoutedEventArgs e)
        {
            int index = GridCustomTools.SelectedIndex;
            if (index > 0)
            {
                var item = _customToolsObservable[index];
                _customToolsObservable.RemoveAt(index);
                _customToolsObservable.Insert(index - 1, item);
                GridCustomTools.SelectedIndex = index - 1;
                SaveAndRefreshCustomTools();
            }
        }

        private void BtnMoveToolDown_Click(object sender, RoutedEventArgs e)
        {
            int index = GridCustomTools.SelectedIndex;
            if (index >= 0 && index < _customToolsObservable.Count - 1)
            {
                var item = _customToolsObservable[index];
                _customToolsObservable.RemoveAt(index);
                _customToolsObservable.Insert(index + 1, item);
                GridCustomTools.SelectedIndex = index + 1;
                SaveAndRefreshCustomTools();
            }
        }

        private void SaveAndRefreshCustomTools()
        {
            if (_customToolsObservable == null || TxtCustomMenuTitle == null || _customConfig == null || ListPreviewChildren == null)
                return;

            _customConfig.Tools = _customToolsObservable.ToList();
            _customConfig.MenuTitle = TxtCustomMenuTitle.Text.Trim();
            _customConfig.MenuIcon = "cmd.exe";
            _customMenuService.SaveConfig(_customConfig);

            if (TxtPreviewMenuName != null)
            {
                TxtPreviewMenuName.Text = string.IsNullOrWhiteSpace(_customConfig.MenuTitle) ? "Custom Tools" : _customConfig.MenuTitle;
            }
            ListPreviewChildren.ItemsSource = null;
            ListPreviewChildren.ItemsSource = _customToolsObservable;
        }

        private void GridCustomTools_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        #endregion

        #region Restore Points & Backups

        private void RefreshRestorePoints()
        {
            if (_restorePointService == null) return;
            var points = _restorePointService.GetAllRestorePoints();
            if (GridRestorePoints != null) GridRestorePoints.ItemsSource = points;
        }

        private void BtnRefreshRestorePoints_Click(object sender, RoutedEventArgs e)
        {
            RefreshRestorePoints();
        }

        private void BtnTogglePin_Click(object sender, RoutedEventArgs e)
        {
            var selectedPoints = GridRestorePoints.SelectedItems.Cast<RestorePointModel>().ToList();
            if (selectedPoints.Count == 0)
            {
                MessageBox.Show("Please select restore point(s) from the table to pin or unpin.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var pt in selectedPoints)
            {
                _restorePointService.TogglePin(pt.Id);
            }
            RefreshRestorePoints();
        }

        private void BtnCreateRestorePoint_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CreateRestorePointDialog();
            dlg.Owner = this;

            if (dlg.ShowDialog() == true)
            {
                var point = _restorePointService.CreateRestorePoint(dlg.SnapshotName, dlg.SnapshotDescription);
                RefreshRestorePoints();
                MessageBox.Show($"Restore point '{point.Name}' created successfully!\nBacked up {point.ItemCount} items.", "Snapshot Created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnRestoreSelected_Click(object sender, RoutedEventArgs e)
        {
            if (GridRestorePoints.SelectedItem is not RestorePointModel selected)
            {
                MessageBox.Show("Please select a restore point from the table to restore.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to restore your context menu to:\n\n'{selected.Name}' ({selected.FormattedDate})?\n\nBefore restoring, an automatic safety backup will be created.",
                "Confirm Context Menu Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                bool success = _restorePointService.Restore(selected, out var err);
                if (success)
                {
                    RefreshRestorePoints();

                    MessageBox.Show(
                        $"Context menu was restored successfully to '{selected.Name}'.",
                        "Restore Succeeded", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Failed to restore snapshot:\n{err}", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnExportReg_Click(object sender, RoutedEventArgs e)
        {
            if (GridRestorePoints.SelectedItem is not RestorePointModel selected)
            {
                MessageBox.Show("Please select a restore point to export.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Registry Registration Files (*.reg)|*.reg",
                FileName = $"ContextMenuBackup_{DateTime.Now:yyyyMMdd_HHmm}.reg",
                Title = "Export Restore Point as .REG"
            };

            if (dlg.ShowDialog() == true)
            {
                bool success = _restorePointService.ExportRegToUserLocation(selected, dlg.FileName);
                if (success)
                {
                    MessageBox.Show($"Exported backup successfully to:\n{dlg.FileName}", "Exported", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to export .reg file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDeleteRestorePoint_Click(object sender, RoutedEventArgs e)
        {
            var selectedPoints = GridRestorePoints.SelectedItems.Cast<RestorePointModel>().ToList();
            if (selectedPoints.Count == 0)
            {
                MessageBox.Show("Please select snapshot(s) to delete.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to delete {selectedPoints.Count} snapshot(s)?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                foreach (var pt in selectedPoints)
                {
                    _restorePointService.DeleteRestorePoint(pt);
                }
                RefreshRestorePoints();
            }
        }

        private void GridRestorePoints_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        #endregion

        #region Header Actions & Helpers

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnCreateSnapshotHeader_Click(object sender, RoutedEventArgs e)
        {
            BtnCreateRestorePoint_Click(sender, e);
        }

        private void SetStatus(string text)
        {
            // Status bar removed per user request
        }

        #endregion
    }
}
