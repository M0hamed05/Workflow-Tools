using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MillerColumns.Services;

namespace MillerColumns.ViewModels
{
    public class ColumnViewModel : ViewModelBase, IDisposable
    {
        private FileItemViewModel? _selectedItem;
        private bool _isLoading;
        private string? _errorMessage;
        private CancellationTokenSource? _loadCts;
        private FileSystemWatcher? _watcher;

        public string FolderPath { get; }
        public string HeaderName { get; }
        public ObservableCollection<FileItemViewModel> Items { get; } = new();

        public IReadOnlyList<FileItemViewModel> SelectedItems => Items.Where(i => i.IsSelected).ToList();

        public event Action<ColumnViewModel, IReadOnlyList<FileItemViewModel>>? SelectionChanged;

        public FileItemViewModel? SelectedItem
        {
            get => _selectedItem ?? SelectedItems.LastOrDefault();
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    if (value != null && !value.IsSelected)
                    {
                        foreach (var it in Items) it.IsSelected = (it == value);
                    }
                    NotifySelectionChanged();
                }
            }
        }

        public void NotifySelectionChanged()
        {
            var selected = SelectedItems;
            _selectedItem = selected.LastOrDefault();
            OnPropertyChanged(nameof(SelectedItem));
            SelectionChanged?.Invoke(this, selected);
        }

        public void SetSelectedItemSilently(FileItemViewModel? item)
        {
            foreach (var it in Items)
            {
                it.IsSelected = (it == item);
            }
            SetProperty(ref _selectedItem, item, nameof(SelectedItem));
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public ColumnViewModel(string folderPath)
        {
            FolderPath = folderPath;
            string trimmed = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            HeaderName = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(HeaderName))
            {
                HeaderName = folderPath;
            }
        }

        public async Task LoadAsync()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            CancellationToken token = _loadCts.Token;

            StopWatcher();
            IsLoading = true;
            ErrorMessage = null;
            Items.Clear();

            try
            {
                // STEP 1: Fast asynchronous enumeration of names and directories
                var loadedItems = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    var dirInfo = new DirectoryInfo(FolderPath);
                    if (!dirInfo.Exists)
                    {
                        return new List<FileItemViewModel>();
                    }

                    var folderList = new List<FileItemViewModel>();
                    var fileList = new List<FileItemViewModel>();

                    // Enumerate directories safely without slow I/O
                    try
                    {
                        foreach (var dir in dirInfo.EnumerateDirectories())
                        {
                            if (token.IsCancellationRequested) return new List<FileItemViewModel>();
                            if ((dir.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                            {
                                folderList.Add(new FileItemViewModel(dir.FullName, true));
                            }
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }

                    // Enumerate files safely with instant placeholder icons
                    try
                    {
                        foreach (var file in dirInfo.EnumerateFiles())
                        {
                            if (token.IsCancellationRequested) return new List<FileItemViewModel>();
                            if ((file.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                            {
                                fileList.Add(new FileItemViewModel(file.FullName, false));
                            }
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }

                    // Natural / alphabetical sort: folders first, then files
                    folderList.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
                    fileList.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

                    var result = new List<FileItemViewModel>(folderList.Count + fileList.Count);
                    result.AddRange(folderList);
                    result.AddRange(fileList);

                    return result;
                }, token);

                if (!token.IsCancellationRequested)
                {
                    // STEP 2: Show files IMMEDIATELY on the UI
                    foreach (var item in loadedItems)
                    {
                        Items.Add(item);
                    }

                    IsLoading = false;

                    // STEP 3: Start live monitoring for created, deleted, and renamed files/folders
                    StartWatcher();

                    // STEP 4: Asynchronously load accurate icons for apps/executables in the background
                    _ = LoadDeferredIconsAsync(loadedItems, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Task was canceled
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsLoading = false;
                }
            }
        }

        private async Task LoadDeferredIconsAsync(List<FileItemViewModel> items, CancellationToken token)
        {
            var itemsNeedingIcons = items.Where(i => i.NeedsCustomIcon).ToList();
            if (itemsNeedingIcons.Count == 0) return;

            await Task.Run(() =>
            {
                foreach (var item in itemsNeedingIcons)
                {
                    if (token.IsCancellationRequested) return;

                    var accurateIcon = IconHelper.GetAccurateFileIcon(item.FullPath);
                    if (accurateIcon != null && !token.IsCancellationRequested)
                    {
                        item.Icon = accurateIcon;
                        item.NeedsCustomIcon = false;
                    }
                }
            }, token);
        }

        #region Live FileSystemWatcher

        private void StartWatcher()
        {
            StopWatcher();
            if (!Directory.Exists(FolderPath)) return;

            try
            {
                _watcher = new FileSystemWatcher(FolderPath)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                _watcher.Created += OnFileSystemCreated;
                _watcher.Deleted += OnFileSystemDeleted;
                _watcher.Renamed += OnFileSystemRenamed;
            }
            catch
            {
                // Graceful fallback for non-watchable network/virtual paths
            }
        }

        private void StopWatcher()
        {
            if (_watcher != null)
            {
                try
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Created -= OnFileSystemCreated;
                    _watcher.Deleted -= OnFileSystemDeleted;
                    _watcher.Renamed -= OnFileSystemRenamed;
                    _watcher.Dispose();
                }
                catch { }
                _watcher = null;
            }
        }

        private void OnFileSystemCreated(object sender, FileSystemEventArgs e)
        {
            string fullPath = e.FullPath;

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                bool isDirectory = Directory.Exists(fullPath);
                if (!isDirectory && !File.Exists(fullPath)) return;

                try
                {
                    var attr = File.GetAttributes(fullPath);
                    if ((attr & (FileAttributes.Hidden | FileAttributes.System)) != 0) return;
                }
                catch { }

                if (Items.Any(i => i.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))) return;

                var newItem = new FileItemViewModel(fullPath, isDirectory);

                // Insert in sorted order: folders first, then alphabetical
                int insertIndex = -1;
                for (int i = 0; i < Items.Count; i++)
                {
                    var cur = Items[i];
                    if (newItem.IsDirectory && !cur.IsDirectory)
                    {
                        insertIndex = i;
                        break;
                    }
                    if (newItem.IsDirectory == cur.IsDirectory)
                    {
                        if (string.Compare(newItem.Name, cur.Name, StringComparison.CurrentCultureIgnoreCase) < 0)
                        {
                            insertIndex = i;
                            break;
                        }
                    }
                }

                if (insertIndex >= 0)
                {
                    Items.Insert(insertIndex, newItem);
                }
                else
                {
                    Items.Add(newItem);
                }

                if (newItem.NeedsCustomIcon)
                {
                    _ = Task.Run(() =>
                    {
                        var icon = IconHelper.GetAccurateFileIcon(newItem.FullPath);
                        if (icon != null)
                        {
                            newItem.Icon = icon;
                            newItem.NeedsCustomIcon = false;
                        }
                    });
                }
            });
        }

        private void OnFileSystemDeleted(object sender, FileSystemEventArgs e)
        {
            string fullPath = e.FullPath;

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var existing = Items.FirstOrDefault(i => i.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (SelectedItem == existing)
                    {
                        SelectedItem = null;
                    }
                    Items.Remove(existing);
                }
            });
        }

        private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
        {
            string oldPath = e.OldFullPath;
            string newPath = e.FullPath;

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var existing = Items.FirstOrDefault(i => i.FullPath.Equals(oldPath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    bool wasSelected = SelectedItem == existing;
                    Items.Remove(existing);

                    bool isDirectory = Directory.Exists(newPath);
                    var renamedItem = new FileItemViewModel(newPath, isDirectory);

                    int insertIndex = -1;
                    for (int i = 0; i < Items.Count; i++)
                    {
                        var cur = Items[i];
                        if (renamedItem.IsDirectory && !cur.IsDirectory)
                        {
                            insertIndex = i;
                            break;
                        }
                        if (renamedItem.IsDirectory == cur.IsDirectory)
                        {
                            if (string.Compare(renamedItem.Name, cur.Name, StringComparison.CurrentCultureIgnoreCase) < 0)
                            {
                                insertIndex = i;
                                break;
                            }
                        }
                    }

                    if (insertIndex >= 0) Items.Insert(insertIndex, renamedItem);
                    else Items.Add(renamedItem);

                    if (wasSelected) SelectedItem = renamedItem;

                    if (renamedItem.NeedsCustomIcon)
                    {
                        _ = Task.Run(() =>
                        {
                            var icon = IconHelper.GetAccurateFileIcon(renamedItem.FullPath);
                            if (icon != null)
                            {
                                renamedItem.Icon = icon;
                                renamedItem.NeedsCustomIcon = false;
                            }
                        });
                    }
                }
            });
        }

        #endregion

        public void CancelLoading()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;
            IsLoading = false;
        }

        public void Dispose()
        {
            StopWatcher();
            CancelLoading();
        }
    }
}
