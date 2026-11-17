using System.Windows.Media;

namespace MillerColumns.ViewModels
{
    public class SidebarItemViewModel : ViewModelBase
    {
        public string Name { get; }
        public string Path { get; }
        public ImageSource? Icon { get; }
        public bool IsHeader { get; }
        public string HeaderType { get; } // "FAVORITES" or "DRIVES"

        public SidebarItemViewModel(string name, string path, ImageSource? icon, bool isHeader = false, string headerType = "")
        {
            Name = name;
            Path = path;
            Icon = icon;
            IsHeader = isHeader;
            HeaderType = headerType;
        }

        public static SidebarItemViewModel CreateHeader(string header)
        {
            return new SidebarItemViewModel(header, string.Empty, null, isHeader: true, headerType: header.ToUpperInvariant());
        }
    }
}
