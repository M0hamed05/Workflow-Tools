using System;
using System.IO;
using System.Windows.Media;
using MillerColumns.Services;

namespace MillerColumns.ViewModels
{
    public class FileItemViewModel : ViewModelBase
    {
        private ImageSource _icon;
        private bool _isSelected;

        public string Name { get; }
        public string FullPath { get; }
        public bool IsDirectory { get; }
        public bool NeedsCustomIcon { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public ImageSource Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public FileItemViewModel(string fullPath, bool isDirectory)
        {
            FullPath = fullPath;
            IsDirectory = isDirectory;
            Name = isDirectory 
                ? Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) 
                : Path.GetFileName(fullPath);

            if (string.IsNullOrEmpty(Name))
            {
                Name = fullPath;
            }

            if (isDirectory)
            {
                _icon = IconHelper.GetDefaultFolderIcon();
                NeedsCustomIcon = false;
            }
            else
            {
                _icon = IconHelper.GetFastFileIcon(fullPath, out bool needsCustom);
                NeedsCustomIcon = needsCustom;
            }
        }
    }
}
