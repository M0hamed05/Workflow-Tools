using System;
using System.Windows;
using ContextMenuManager.Models;

namespace ContextMenuManager.Dialogs
{
    public partial class AddEditToolDialog : Window
    {
        public CustomToolItem Tool { get; private set; }

        public AddEditToolDialog(CustomToolItem? existingTool = null)
        {
            InitializeComponent();

            if (existingTool != null)
            {
                TxtDialogTitle.Text = "Edit Custom Command";
                Tool = new CustomToolItem
                {
                    Id = existingTool.Id,
                    Command = existingTool.Command,
                    RunInCmd = existingTool.RunInCmd,
                    ShowInFolders = existingTool.ShowInFolders,
                    ShowInEmptySpace = existingTool.ShowInEmptySpace
                };

                TxtCommand.Text = Tool.Command;
                ChkRunInCmd.IsChecked = Tool.RunInCmd;
                ChkFolder.IsChecked = Tool.ShowInFolders;
                ChkEmptySpace.IsChecked = Tool.ShowInEmptySpace;
            }
            else
            {
                Tool = new CustomToolItem();
                TxtCommand.Text = "code .";
                ChkRunInCmd.IsChecked = false;
            }
        }

        private void PresetCode_Click(object sender, RoutedEventArgs e)
        {
            TxtCommand.Text = "code .";
            ChkRunInCmd.IsChecked = false;
        }

        private void PresetCmd_Click(object sender, RoutedEventArgs e)
        {
            TxtCommand.Text = "cmd";
            ChkRunInCmd.IsChecked = true;
        }

        private void PresetPowerShell_Click(object sender, RoutedEventArgs e)
        {
            TxtCommand.Text = "powershell";
            ChkRunInCmd.IsChecked = true;
        }

        private void PresetNpx_Click(object sender, RoutedEventArgs e)
        {
            TxtCommand.Text = "npx create-next-app@latest";
            ChkRunInCmd.IsChecked = true;
        }

        private void PresetNpmDev_Click(object sender, RoutedEventArgs e)
        {
            TxtCommand.Text = "npm run dev";
            ChkRunInCmd.IsChecked = true;
        }

        private void PresetGitStatus_Click(object sender, RoutedEventArgs e)
        {
            TxtCommand.Text = "git status";
            ChkRunInCmd.IsChecked = true;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtCommand.Text))
            {
                MessageBox.Show("Please enter a command to execute.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ChkFolder.IsChecked != true && ChkEmptySpace.IsChecked != true)
            {
                MessageBox.Show("Please check at least one location (Folders or Empty Space).", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Tool.Command = TxtCommand.Text.Trim();
            Tool.RunInCmd = ChkRunInCmd.IsChecked == true;
            Tool.ShowInFolders = ChkFolder.IsChecked == true;
            Tool.ShowInEmptySpace = ChkEmptySpace.IsChecked == true;

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
