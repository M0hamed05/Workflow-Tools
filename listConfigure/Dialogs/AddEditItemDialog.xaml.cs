using System;
using System.Windows;
using ContextMenuManager.Models;
using Microsoft.Win32;

namespace ContextMenuManager.Dialogs
{
    public partial class AddEditItemDialog : Window
    {
        public string KeyName => TxtKeyName.Text.Trim();
        public string DisplayName => TxtDisplayName.Text.Trim();
        public string Command => TxtCommand.Text.Trim();
        public string ItemIcon => TxtIcon.Text.Trim();
        public MenuLocationScope Scope
        {
            get
            {
                return CmbScope.SelectedIndex switch
                {
                    1 => MenuLocationScope.Folders,
                    2 => MenuLocationScope.EmptySpace,
                    _ => MenuLocationScope.Both
                };
            }
        }

        public AddEditItemDialog(ContextMenuItemModel? existing = null)
        {
            InitializeComponent();

            if (existing != null)
            {
                TxtDialogTitle.Text = "Edit Context Menu Option";
                TxtKeyName.Text = existing.KeyName;
                TxtKeyName.IsEnabled = false; // Do not rename primary key while editing
                TxtDisplayName.Text = existing.DisplayName;
                TxtCommand.Text = existing.Command;
                TxtIcon.Text = existing.Icon;

                CmbScope.SelectedIndex = existing.Location switch
                {
                    MenuLocationScope.Folders => 1,
                    MenuLocationScope.EmptySpace => 2,
                    _ => 0
                };
            }
            else
            {
                TxtKeyName.Text = "QuickTool";
                TxtDisplayName.Text = "My Context Tool";
                TxtCommand.Text = "cmd.exe /k \"cd /d \\\"%V\\\"\"";
                CmbScope.SelectedIndex = 0;
            }
        }

        private void BrowseIcon_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Icon & Executable Files (*.ico;*.exe;*.dll)|*.ico;*.exe;*.dll|All Files (*.*)|*.*",
                Title = "Select Icon"
            };

            if (dlg.ShowDialog() == true)
            {
                TxtIcon.Text = dlg.FileName;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtKeyName.Text))
            {
                MessageBox.Show("Please enter a key identifier.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtDisplayName.Text))
            {
                MessageBox.Show("Please enter a display name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtCommand.Text))
            {
                MessageBox.Show("Please enter a command.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
