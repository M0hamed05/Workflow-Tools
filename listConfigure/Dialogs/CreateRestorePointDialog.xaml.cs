using System;
using System.Windows;

namespace ContextMenuManager.Dialogs
{
    public partial class CreateRestorePointDialog : Window
    {
        public string SnapshotName => TxtName.Text.Trim();
        public string SnapshotDescription => TxtDescription.Text.Trim();

        public CreateRestorePointDialog(string defaultName = "")
        {
            InitializeComponent();
            TxtName.Text = string.IsNullOrWhiteSpace(defaultName) 
                ? $"Manual Snapshot {DateTime.Now:yyyy-MM-dd HH:mm}" 
                : defaultName;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Please enter a name for the restore point.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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
