using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MillerColumns.Services;

namespace MillerColumns.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private SidebarItemViewModel? _selectedSidebarItem;
        private string _currentPath = string.Empty;
        private bool _isRestoringSession;
        private double _scrollPadWidth;
        private bool _isDarkTheme = true;
        private bool _isSidebarMinimized;

        public ObservableCollection<ColumnViewModel> Columns { get; } = new();
        public ObservableCollection<SidebarItemViewModel> SidebarItems { get; } = new();

        public bool IsSidebarMinimized
        {
            get => _isSidebarMinimized;
            set
            {
                if (SetProperty(ref _isSidebarMinimized, value))
                {
                    OnPropertyChanged(nameof(SidebarWidthGridLength));
                    OnPropertyChanged(nameof(SidebarToggleButtonIcon));
                    OnPropertyChanged(nameof(SidebarToggleButtonToolTip));
                    OnPropertyChanged(nameof(SidebarToggleAlignment));
                }
            }
        }

        public System.Windows.GridLength SidebarWidthGridLength => IsSidebarMinimized ? new System.Windows.GridLength(50) : new System.Windows.GridLength(220);
        public string SidebarToggleButtonIcon => IsSidebarMinimized ? "»" : "«";
        public string SidebarToggleButtonToolTip => IsSidebarMinimized ? "Expand Sidebar" : "Collapse Sidebar";
        public System.Windows.HorizontalAlignment SidebarToggleAlignment => IsSidebarMinimized ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Right;

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (SetProperty(ref _isDarkTheme, value))
                {
                    OnPropertyChanged(nameof(ThemeButtonText));
                    OnPropertyChanged(nameof(ThemeButtonIcon));
                    OnPropertyChanged(nameof(ThemeButtonToolTip));
                    ThemeService.ApplyTheme(_isDarkTheme, GetMainWindowHandle());
                }
            }
        }

        public string ThemeButtonText => IsDarkTheme ? "Dark Mode" : "Light Mode";
        public string ThemeButtonIcon => IsDarkTheme ? "🌙" : "☀️";
        public string ThemeButtonToolTip => IsDarkTheme ? "Switch to Light Mode" : "Switch to Dark Mode";

        public string WindowTitle => string.IsNullOrEmpty(CurrentPath)
            ? "Miller Columns Explorer"
            : $"{Path.GetFileName(CurrentPath.TrimEnd('\\', '/'))} — Miller Columns Explorer";

        /// <summary>
        /// Bound to a transparent spacer Border after the columns ItemsControl.
        /// Set before removing columns so ScrollableWidth stays high enough for the
        /// backward scroll animation; cleared in the animation Completed callback.
        /// </summary>
        public double ScrollPadWidth
        {
            get => _scrollPadWidth;
            set => SetProperty(ref _scrollPadWidth, value);
        }

        public event Action? ScrollToEndRequested;
        public event Action? ScrollToStartRequested;
        public event Action<int, bool, Action>? ColumnNavigationRequested;

        public string CurrentPath
        {
            get => _currentPath;
            private set
            {
                if (SetProperty(ref _currentPath, value))
                {
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

        public SidebarItemViewModel? SelectedSidebarItem
        {
            get => _selectedSidebarItem;
            set
            {
                if (SetProperty(ref _selectedSidebarItem, value) && value != null && !value.IsHeader)
                {
                    NavigateTo(value.Path);
                }
            }
        }

        private ColumnViewModel? _activeColumn;
        public ColumnViewModel? ActiveColumn
        {
            get => _activeColumn ?? Columns.LastOrDefault();
            set => SetProperty(ref _activeColumn, value);
        }

        public ICommand OpenFileCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand CutCommand { get; }
        public ICommand PasteCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand ToggleSidebarCommand { get; }

        public MainViewModel()
        {
            OpenFileCommand = new RelayCommand(param =>
            {
                if (param is FileItemViewModel fileItem && !fileItem.IsDirectory)
                {
                    OpenFileWithDefaultApp(fileItem.FullPath);
                }
            });

            CopyCommand = new RelayCommand(_ => CopySelectedItems(isCut: false));
            CutCommand = new RelayCommand(_ => CopySelectedItems(isCut: true));
            PasteCommand = new RelayCommand(_ => PasteIntoCurrentPath());
            UndoCommand = new RelayCommand(_ => PerformUndo());
            ToggleThemeCommand = new RelayCommand(_ => IsDarkTheme = !IsDarkTheme);
            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarMinimized = !IsSidebarMinimized);

            InitializeSidebar();
            _ = InitializeInitialViewAsync();
        }

        private static IntPtr GetMainWindowHandle()
        {
            if (System.Windows.Application.Current?.MainWindow != null)
            {
                return new System.Windows.Interop.WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
            }
            return IntPtr.Zero;
        }

        public void CopySelectedItems(bool isCut)
        {
            var items = ActiveColumn?.SelectedItems;
            if (items != null && items.Count > 0)
            {
                var paths = items.Select(x => x.FullPath).ToList();
                ShellContextMenu.CopyFilesToClipboard(paths, isCut);
            }
        }

        public void PasteIntoCurrentPath()
        {
            string targetDir = string.Empty;
            var col = ActiveColumn ?? Columns.LastOrDefault();
            if (col != null)
            {
                // If exactly 1 directory is selected in active column, paste into that directory
                if (col.SelectedItems.Count == 1 && col.SelectedItems[0].IsDirectory)
                {
                    targetDir = col.SelectedItems[0].FullPath;
                }
                else
                {
                    targetDir = col.FolderPath;
                }
            }
            else if (!string.IsNullOrEmpty(CurrentPath))
            {
                targetDir = Directory.Exists(CurrentPath) ? CurrentPath : Path.GetDirectoryName(CurrentPath) ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(targetDir) && Directory.Exists(targetDir))
            {
                IntPtr hwnd = GetMainWindowHandle();
                ShellContextMenu.PerformPaste(hwnd, targetDir);
            }
        }

        public void PerformUndo()
        {
            IntPtr hwnd = GetMainWindowHandle();
            FileUndoService.Undo(hwnd);
        }

        private async Task InitializeInitialViewAsync()
        {
            var session = SessionService.LoadSession();

            // Theme preference: saved session, or fallback to Windows system theme
            if (!string.IsNullOrEmpty(session?.Theme))
            {
                _isDarkTheme = string.Equals(session.Theme, "Dark", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                _isDarkTheme = ThemeService.GetSystemIsDarkTheme();
            }
            if (session != null)
            {
                IsSidebarMinimized = session.IsSidebarMinimized;
            }
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(ThemeButtonText));
            OnPropertyChanged(nameof(ThemeButtonIcon));
            OnPropertyChanged(nameof(ThemeButtonToolTip));
            ThemeService.ApplyTheme(_isDarkTheme, GetMainWindowHandle());

            if (session != null && session.ColumnPaths.Count > 0)
            {
                bool restored = await RestoreSessionAsync(session);
                if (restored) return;
            }

            // Fallback default path: User Profile or C:\
            string defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!Directory.Exists(defaultPath))
            {
                defaultPath = @"C:\";
            }

            NavigateTo(defaultPath);
        }

        private void InitializeSidebar()
        {
            SidebarItems.Add(SidebarItemViewModel.CreateHeader("FAVORITES"));

            AddSidebarItemIfExists("Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            AddSidebarItemIfExists("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            AddSidebarItemIfExists("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            AddSidebarItemIfExists("Downloads", downloads);

            string projectsPath = @"d:\Projects";
            if (Directory.Exists(projectsPath))
            {
                AddSidebarItemIfExists("Projects", projectsPath);
            }

            SidebarItems.Add(SidebarItemViewModel.CreateHeader("DRIVES"));
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    string driveLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? drive.Name : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})";
                    SidebarItems.Add(new SidebarItemViewModel(driveLabel, drive.RootDirectory.FullName, IconHelper.GetDriveIcon(drive.RootDirectory.FullName)));
                }
            }
        }

        private void AddSidebarItemIfExists(string name, string path)
        {
            if (Directory.Exists(path))
            {
                SidebarItems.Add(new SidebarItemViewModel(name, path, IconHelper.GetFolderIcon(path)));
            }
        }

        public void NavigateTo(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            ScrollToStartRequested?.Invoke();

            // Cancel and clear existing columns
            foreach (var col in Columns)
            {
                col.SelectionChanged -= OnColumnSelectionChanged;
                col.Dispose();
            }
            Columns.Clear();

            CurrentPath = folderPath;

            var firstCol = new ColumnViewModel(folderPath);
            firstCol.SelectionChanged += OnColumnSelectionChanged;
            Columns.Add(firstCol);

            _ = firstCol.LoadAsync();
        }

        private void OnColumnSelectionChanged(ColumnViewModel senderColumn, IReadOnlyList<FileItemViewModel> selectedItems)
        {
            if (_isRestoringSession) return;

            ActiveColumn = senderColumn;
            int colIndex = Columns.IndexOf(senderColumn);
            if (colIndex == -1) return;

            if (selectedItems == null || selectedItems.Count == 0)
            {
                CurrentPath = senderColumn.FolderPath;
                return;
            }

            if (selectedItems.Count == 1)
            {
                var selectedItem = selectedItems[0];
                // When clicking a file, do NOT close any columns to the right!
                // Files don't have children to expand, and keeping subsequent columns open
                // enables the user to drag and drop files into any already-opened folder to the right.
                if (!selectedItem.IsDirectory)
                {
                    CurrentPath = selectedItem.FullPath;
                    return;
                }

                // === A FOLDER WAS SELECTED ===
                CurrentPath = selectedItem.FullPath;
                bool hasChild = true;

                int afterCount = colIndex + 2; // sender column + new child column
                int removedCount = Columns.Count - afterCount;
                ScrollPadWidth = removedCount > 0 ? removedCount * 260.0 : 0.0;

                // Remove all columns to the right of colIndex
                while (Columns.Count > colIndex + 1)
                {
                    int last = Columns.Count - 1;
                    Columns[last].SelectionChanged -= OnColumnSelectionChanged;
                    Columns[last].Dispose();
                    Columns.RemoveAt(last);
                }

                var nextCol = new ColumnViewModel(selectedItem.FullPath);
                nextCol.SelectionChanged += OnColumnSelectionChanged;
                Columns.Add(nextCol);
                _ = nextCol.LoadAsync();

                // Fire scroll animation; clean up the spacer after it completes.
                ColumnNavigationRequested?.Invoke(colIndex, hasChild, () =>
                {
                    ScrollPadWidth = 0.0;
                });
            }
            else
            {
                // === MULTIPLE ITEMS SELECTED ===
                if (!selectedItems.Any(x => x.IsDirectory))
                {
                    // All selected items are files!
                    // Do NOT close columns to the right, allowing files to be moved to subsequent columns.
                    CurrentPath = selectedItems.Last().FullPath;
                    return;
                }

                CurrentPath = senderColumn.FolderPath;

                // At least one directory was selected among multiple items; close subsequent columns
                while (Columns.Count > colIndex + 1)
                {
                    int last = Columns.Count - 1;
                    Columns[last].SelectionChanged -= OnColumnSelectionChanged;
                    Columns[last].Dispose();
                    Columns.RemoveAt(last);
                }
                ScrollPadWidth = 0.0;
            }
        }

        public async Task<bool> RestoreSessionAsync(SessionData session)
        {
            var validPaths = new List<string>();
            foreach (var path in session.ColumnPaths)
            {
                if (Directory.Exists(path))
                {
                    validPaths.Add(path);
                }
                else
                {
                    break;
                }
            }

            if (validPaths.Count == 0) return false;

            _isRestoringSession = true;
            try
            {
                foreach (var col in Columns)
                {
                    col.SelectionChanged -= OnColumnSelectionChanged;
                    col.Dispose();
                }
                Columns.Clear();

                var newColumns = new List<ColumnViewModel>();
                foreach (var path in validPaths)
                {
                    var col = new ColumnViewModel(path);
                    col.SelectionChanged += OnColumnSelectionChanged;
                    newColumns.Add(col);
                    Columns.Add(col);
                }

                CurrentPath = validPaths.Last();

                // Load all columns asynchronously
                var loadTasks = new List<Task>();
                foreach (var col in newColumns)
                {
                    loadTasks.Add(col.LoadAsync());
                }

                await Task.WhenAll(loadTasks);

                // Highlight corresponding selected items
                for (int i = 0; i < newColumns.Count; i++)
                {
                    string? targetSelect = (i < session.SelectedItemPaths.Count) ? session.SelectedItemPaths[i] : null;
                    if (!string.IsNullOrEmpty(targetSelect))
                    {
                        var match = newColumns[i].Items.FirstOrDefault(it => it.FullPath.Equals(targetSelect, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            newColumns[i].SetSelectedItemSilently(match);
                        }
                    }
                }

                ScrollToEndRequested?.Invoke();
                return true;
            }
            finally
            {
                _isRestoringSession = false;
            }
        }

        public SessionData GetSessionData()
        {
            var session = new SessionData
            {
                Theme = IsDarkTheme ? "Dark" : "Light",
                IsSidebarMinimized = IsSidebarMinimized
            };
            foreach (var col in Columns)
            {
                session.ColumnPaths.Add(col.FolderPath);
                session.SelectedItemPaths.Add(col.SelectedItem?.FullPath);
            }
            return session;
        }

        public void SaveSession()
        {
            SessionService.SaveSession(GetSessionData());
        }

        private static void OpenFileWithDefaultApp(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open file: {ex.Message}");
            }
        }
    }
}
