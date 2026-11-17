using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MillerColumns.Services;
using MillerColumns.ViewModels;

namespace MillerColumns
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        private Point _dragStartPoint;
        private FileItemViewModel? _draggedItem;
        private bool _isDragging;
        private bool _isWindowReady;

        // Rubber-band marquee selection state
        private bool _isRubberBandSelecting;
        private Point _rubberBandStartPoint;
        private ListBox? _rubberBandListBox;
        private Border? _rubberBandBox;
        private readonly System.Collections.Generic.HashSet<FileItemViewModel> _preRubberBandSelected = new();

        // State for potential rubber-band vs single-click on row whitespace
        private Point _whitespaceClickStart;
        private FileItemViewModel? _whitespaceClickItem;
        private ListBox? _whitespaceClickListBox;

        // State for multi-item drag vs single-click reset
        private FileItemViewModel? _pendingSelectionResetItem;

        public static readonly DependencyProperty AnimatedScrollOffsetProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedScrollOffset",
                typeof(double),
                typeof(MainWindow),
                new PropertyMetadata(0.0, (d, e) =>
                {
                    if (d is ScrollViewer sv)
                    {
                        sv.ScrollToHorizontalOffset((double)e.NewValue);
                    }
                }));

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.ColumnNavigationRequested += OnColumnNavigationRequested;
            _viewModel.ScrollToEndRequested += OnScrollToEndRequested;
            _viewModel.ScrollToStartRequested += OnScrollToStartRequested;
            Closing += OnMainWindowClosing;

            SidebarBorder.Width = _viewModel.IsSidebarMinimized ? 50.0 : 220.0;
            Loaded += (_, _) => { _isWindowReady = true; };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsSidebarMinimized))
            {
                AnimateSidebar(_viewModel.IsSidebarMinimized);
            }
        }

        private void AnimateSidebar(bool isMinimized)
        {
            double targetWidth = isMinimized ? 50.0 : 220.0;

            if (!_isWindowReady)
            {
                SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                SidebarBorder.Width = targetWidth;
                return;
            }

            double currentWidth = SidebarBorder.ActualWidth > 0 ? SidebarBorder.ActualWidth : SidebarBorder.Width;

            if (Math.Abs(targetWidth - currentWidth) > 0.5)
            {
                var anim = new DoubleAnimation
                {
                    From = currentWidth,
                    To = targetWidth,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                anim.Completed += (_, _) =>
                {
                    SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                    SidebarBorder.Width = targetWidth;
                };
                SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, anim);
            }
            else
            {
                SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                SidebarBorder.Width = targetWidth;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            ThemeService.ApplyTheme(_viewModel.IsDarkTheme, hwnd);
        }

        private void OnMainWindowClosing(object? sender, CancelEventArgs e)
        {
            _viewModel.SaveSession();
        }

        private void OnColumnNavigationRequested(int activeColIndex, bool hasChild, Action cleanupAction)
        {
            Dispatcher.InvokeAsync(() =>
            {
                double currentOffset = ColumnsScrollViewer.HorizontalOffset;
                double targetOffset = CalculateTargetOffset(activeColIndex, hasChild);

                if (Math.Abs(targetOffset - currentOffset) > 1.0)
                {
                    var anim = new DoubleAnimation
                    {
                        From = currentOffset,
                        To = targetOffset,
                        Duration = TimeSpan.FromMilliseconds(240),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    // Run cleanup AFTER animation — for backward nav the old columns stay
                    // visible during the slide, then are removed when it completes.
                    anim.Completed += (_, _) => cleanupAction();
                    ColumnsScrollViewer.BeginAnimation(AnimatedScrollOffsetProperty, anim);
                }
                else
                {
                    // No animation needed — clean up immediately.
                    cleanupAction();
                }
            }, DispatcherPriority.Loaded);
        }

        private double CalculateTargetOffset(int activeColIndex, bool hasChild)
        {
            const double colWidth = 260.0;
            int rightColIndex = hasChild ? activeColIndex + 1 : activeColIndex;
            double leftEdge = activeColIndex * colWidth;
            double rightEdge = (rightColIndex + 1) * colWidth;
            double viewportWidth = ColumnsScrollViewer.ViewportWidth;
            double currentOffset = ColumnsScrollViewer.HorizontalOffset;

            if (viewportWidth <= 0) return 0;

            // If the total remaining content fits within viewport from the left, scroll all the way to 0
            if (rightEdge <= viewportWidth)
            {
                return 0.0;
            }

            // Expanding forward to the right (right edge is past current viewport)
            if (rightEdge > currentOffset + viewportWidth)
            {
                return Math.Max(0.0, rightEdge - viewportWidth);
            }

            // Moving in opposite direction to the left (left edge is before current viewport)
            if (leftEdge < currentOffset)
            {
                return Math.Max(0.0, leftEdge);
            }

            // Align within bounds
            double maxOffset = Math.Max(0.0, rightEdge - viewportWidth);
            return Math.Min(currentOffset, maxOffset);
        }

        private void OnScrollToEndRequested()
        {
            Dispatcher.InvokeAsync(() =>
            {
                double currentOffset = ColumnsScrollViewer.HorizontalOffset;
                double targetOffset = ColumnsScrollViewer.ScrollableWidth;

                if (Math.Abs(targetOffset - currentOffset) > 1.0)
                {
                    var anim = new DoubleAnimation
                    {
                        From = currentOffset,
                        To = targetOffset,
                        Duration = TimeSpan.FromMilliseconds(240),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    ColumnsScrollViewer.BeginAnimation(AnimatedScrollOffsetProperty, anim);
                }
            }, DispatcherPriority.Loaded);
        }

        private void OnScrollToStartRequested()
        {
            Dispatcher.InvokeAsync(() =>
            {
                double currentOffset = ColumnsScrollViewer.HorizontalOffset;
                if (currentOffset > 1.0)
                {
                    var anim = new DoubleAnimation
                    {
                        From = currentOffset,
                        To = 0.0,
                        Duration = TimeSpan.FromMilliseconds(240),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    ColumnsScrollViewer.BeginAnimation(AnimatedScrollOffsetProperty, anim);
                }
            }, DispatcherPriority.Loaded);
        }

        #region Drag and Drop (Cross-Column and Windows Explorer)

        private void OnListBoxItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _pendingSelectionResetItem = null;
            _whitespaceClickItem = null;
            _whitespaceClickListBox = null;

            if (sender is ListBoxItem lbi && lbi.DataContext is FileItemViewModel item)
            {
                var parentListBox = FindVisualParent<ListBox>(lbi);
                var col = parentListBox?.DataContext as ColumnViewModel;
                if (col != null)
                {
                    _viewModel.ActiveColumn = col;
                }

                bool isDirectFileHit = e.OriginalSource is Image || e.OriginalSource is TextBlock;
                bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                if (!isDirectFileHit && parentListBox != null)
                {
                    // Clicked on row whitespace: record for potential marquee or single selection
                    _whitespaceClickStart = e.GetPosition(parentListBox);
                    _whitespaceClickItem = item;
                    _whitespaceClickListBox = parentListBox;
                    _draggedItem = item;
                    e.Handled = true;
                    return;
                }

                // Clicked directly on file (Icon or TextBlock)
                _draggedItem = item;

                if (!isCtrl && !isShift)
                {
                    if (item.IsSelected)
                    {
                        // CRITICAL WINDOWS EXPLORER BEHAVIOR:
                        // Item is already selected among potential multi-selection.
                        // Keep selection intact on MouseDown so user can drag all selected items!
                        // Only reset selection on MouseUp if user does not drag.
                        _pendingSelectionResetItem = item;
                        e.Handled = true;
                        lbi.Focus();
                        return;
                    }
                    else
                    {
                        if (col != null)
                        {
                            foreach (var it in col.Items) it.IsSelected = (it == item);
                            col.NotifySelectionChanged();
                        }
                    }
                }
                else if (isCtrl)
                {
                    item.IsSelected = !item.IsSelected;
                    col?.NotifySelectionChanged();
                    e.Handled = true;
                }
                else if (isShift && col != null)
                {
                    int anchorIndex = col.Items.IndexOf(col.SelectedItem ?? item);
                    int targetIndex = col.Items.IndexOf(item);
                    if (anchorIndex != -1 && targetIndex != -1)
                    {
                        int min = Math.Min(anchorIndex, targetIndex);
                        int max = Math.Max(anchorIndex, targetIndex);
                        for (int i = 0; i < col.Items.Count; i++)
                        {
                            col.Items[i].IsSelected = (i >= min && i <= max);
                        }
                        col.NotifySelectionChanged();
                        e.Handled = true;
                    }
                }
            }
            else
            {
                _draggedItem = null;
            }
        }

        private void OnListBoxItemPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isRubberBandSelecting) return;

            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null && !_isDragging)
            {
                Point currentPoint = e.GetPosition(this);
                Vector diff = _dragStartPoint - currentPoint;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Drag initiated! Cancel pending selection resets
                    _pendingSelectionResetItem = null;
                    _whitespaceClickItem = null;
                    _whitespaceClickListBox = null;

                    if (File.Exists(_draggedItem.FullPath) || Directory.Exists(_draggedItem.FullPath))
                    {
                        _isDragging = true;
                        try
                        {
                            var lbi = sender as ListBoxItem ?? FindVisualParent<ListBoxItem>(sender as DependencyObject);
                            var parentListBox = FindVisualParent<ListBox>(lbi);
                            var col = parentListBox?.DataContext as ColumnViewModel;
                            string[] filesToDrag;
                            if (col != null && col.SelectedItems.Count > 1 && col.SelectedItems.Contains(_draggedItem))
                            {
                                filesToDrag = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(col.SelectedItems, x => x.FullPath));
                            }
                            else
                            {
                                filesToDrag = new string[] { _draggedItem.FullPath };
                            }

                            var data = new DataObject(DataFormats.FileDrop, filesToDrag);
                            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy | DragDropEffects.Move);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"DragDrop exception: {ex.Message}");
                        }
                        finally
                        {
                            _isDragging = false;
                            _draggedItem = null;
                        }
                    }
                }
            }
        }

        private void OnListBoxItemPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_pendingSelectionResetItem != null)
            {
                // User clicked on an already-selected item without dragging it: collapse selection to just this item
                if (sender is ListBoxItem lbi && lbi.DataContext is FileItemViewModel item && item == _pendingSelectionResetItem)
                {
                    var parentListBox = FindVisualParent<ListBox>(lbi);
                    if (parentListBox?.DataContext is ColumnViewModel col)
                    {
                        foreach (var it in col.Items) it.IsSelected = (it == item);
                        col.NotifySelectionChanged();
                    }
                }
                _pendingSelectionResetItem = null;
            }
        }

        private void OnColumnListBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox listBox || listBox.DataContext is not ColumnViewModel col) return;

            _viewModel.ActiveColumn = col;

            var lbi = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (lbi == null)
            {
                // Clicked in empty space of column (below items)
                bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                if (!isCtrl && !isShift)
                {
                    foreach (var it in col.Items) it.IsSelected = false;
                    col.NotifySelectionChanged();
                }

                StartRubberBandSelection(listBox, e.GetPosition(listBox));
                e.Handled = true;
            }
        }

        private void OnColumnListBoxPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not ListBox listBox) return;

            Point currentPoint = e.GetPosition(listBox);

            if (_isRubberBandSelecting)
            {
                UpdateRubberBandSelection(listBox, currentPoint);
                e.Handled = true;
                return;
            }

            if (_whitespaceClickListBox == listBox && e.LeftButton == MouseButtonState.Pressed)
            {
                Vector diff = currentPoint - _whitespaceClickStart;
                if (Math.Abs(diff.X) > 4 || Math.Abs(diff.Y) > 4)
                {
                    // User dragged from item whitespace -> start rubber-band marquee selection!
                    var startPt = _whitespaceClickStart;
                    _whitespaceClickListBox = null;
                    _whitespaceClickItem = null;
                    _pendingSelectionResetItem = null;

                    StartRubberBandSelection(listBox, startPt);
                    UpdateRubberBandSelection(listBox, currentPoint);
                    e.Handled = true;
                }
            }
        }

        private void OnColumnListBoxPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isRubberBandSelecting)
            {
                EndRubberBandSelection();
                e.Handled = true;
                return;
            }

            if (_whitespaceClickItem != null && _whitespaceClickListBox != null)
            {
                // Clicked on row whitespace without dragging: select that item
                if (_whitespaceClickListBox.DataContext is ColumnViewModel col)
                {
                    bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                    bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                    if (!isCtrl && !isShift)
                    {
                        foreach (var it in col.Items) it.IsSelected = (it == _whitespaceClickItem);
                    }
                    else if (isCtrl)
                    {
                        _whitespaceClickItem.IsSelected = !_whitespaceClickItem.IsSelected;
                    }
                    col.NotifySelectionChanged();
                }
                _whitespaceClickItem = null;
                _whitespaceClickListBox = null;
                e.Handled = true;
            }
        }

        #region Marquee / Rubberband Selection Methods

        private void StartRubberBandSelection(ListBox listBox, Point startPoint)
        {
            if (listBox.DataContext is not ColumnViewModel col) return;

            var parentGrid = listBox.Parent as Grid;
            var canvas = parentGrid?.Children.OfType<Canvas>().FirstOrDefault();
            var box = canvas?.Children.OfType<Border>().FirstOrDefault();
            if (box == null) return;

            _isRubberBandSelecting = true;
            _rubberBandStartPoint = startPoint;
            _rubberBandListBox = listBox;
            _rubberBandBox = box;

            _preRubberBandSelected.Clear();
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (isCtrl)
            {
                foreach (var it in col.SelectedItems)
                {
                    _preRubberBandSelected.Add(it);
                }
            }

            Canvas.SetLeft(box, startPoint.X);
            Canvas.SetTop(box, startPoint.Y);
            box.Width = 0;
            box.Height = 0;
            box.Visibility = Visibility.Visible;

            listBox.CaptureMouse();
        }

        private void UpdateRubberBandSelection(ListBox listBox, Point currentPoint)
        {
            if (!_isRubberBandSelecting || _rubberBandBox == null || listBox.DataContext is not ColumnViewModel col) return;

            double left = Math.Min(_rubberBandStartPoint.X, currentPoint.X);
            double top = Math.Min(_rubberBandStartPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - _rubberBandStartPoint.X);
            double height = Math.Abs(currentPoint.Y - _rubberBandStartPoint.Y);

            Canvas.SetLeft(_rubberBandBox, left);
            Canvas.SetTop(_rubberBandBox, top);
            _rubberBandBox.Width = width;
            _rubberBandBox.Height = height;

            Rect rubberBandRect = new Rect(left, top, width, height);
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            for (int i = 0; i < listBox.Items.Count; i++)
            {
                if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem lbi && lbi.IsVisible)
                {
                    try
                    {
                        var transform = lbi.TransformToAncestor(listBox);
                        Rect itemBounds = transform.TransformBounds(new Rect(0, 0, lbi.ActualWidth, lbi.ActualHeight));
                        bool intersects = rubberBandRect.IntersectsWith(itemBounds);

                        if (lbi.DataContext is FileItemViewModel item)
                        {
                            if (isCtrl)
                            {
                                item.IsSelected = intersects ? !_preRubberBandSelected.Contains(item) : _preRubberBandSelected.Contains(item);
                            }
                            else
                            {
                                item.IsSelected = intersects;
                            }
                        }
                    }
                    catch { }
                }
            }

            // Auto-scroll when dragging near top or bottom
            var sv = FindVisualChild<ScrollViewer>(listBox);
            if (sv != null)
            {
                const double edgeThreshold = 25.0;
                if (currentPoint.Y < edgeThreshold && sv.VerticalOffset > 0)
                {
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - 15);
                }
                else if (currentPoint.Y > listBox.ActualHeight - edgeThreshold && sv.VerticalOffset < sv.ScrollableHeight)
                {
                    sv.ScrollToVerticalOffset(sv.VerticalOffset + 15);
                }
            }
        }

        private void EndRubberBandSelection()
        {
            if (!_isRubberBandSelecting) return;

            _isRubberBandSelecting = false;
            if (_rubberBandBox != null)
            {
                _rubberBandBox.Visibility = Visibility.Collapsed;
                _rubberBandBox = null;
            }

            if (_rubberBandListBox != null)
            {
                _rubberBandListBox.ReleaseMouseCapture();
                if (_rubberBandListBox.DataContext is ColumnViewModel col)
                {
                    col.NotifySelectionChanged();
                }
                _rubberBandListBox = null;
            }

            _preRubberBandSelected.Clear();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key == Key.Escape && _isRubberBandSelecting)
            {
                if (_rubberBandListBox?.DataContext is ColumnViewModel col)
                {
                    foreach (var it in col.Items)
                    {
                        it.IsSelected = _preRubberBandSelected.Contains(it);
                    }
                    col.NotifySelectionChanged();
                }
                EndRubberBandSelection();
                e.Handled = true;
            }
        }

        #endregion

        private void OnColumnPreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                bool isCtrl = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                e.Effects = isCtrl ? DragDropEffects.Copy : DragDropEffects.Move;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void OnColumnPreviewDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                string? targetDir = null;
                var lbi = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
                if (lbi?.DataContext is FileItemViewModel targetItem && targetItem.IsDirectory)
                {
                    targetDir = targetItem.FullPath;
                }
                else if (sender is FrameworkElement fe && fe.DataContext is ColumnViewModel col)
                {
                    targetDir = col.FolderPath;
                }

                if (!string.IsNullOrEmpty(targetDir) && Directory.Exists(targetDir))
                {
                    e.Handled = true;
                    bool isCtrl = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                    bool isMove = !isCtrl;
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    ShellContextMenu.ExecuteFileOperation(hwnd, files, targetDir, isMove);
                }
            }
        }

        private void OnSidebarPreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var lbi = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
                if (lbi?.DataContext is SidebarItemViewModel sidebarItem && !sidebarItem.IsHeader && Directory.Exists(sidebarItem.Path))
                {
                    bool isCtrl = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                    e.Effects = isCtrl ? DragDropEffects.Copy : DragDropEffects.Move;
                    e.Handled = true;
                    return;
                }
            }
            e.Effects = DragDropEffects.None;
        }

        private void OnSidebarPreviewDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var lbi = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (lbi?.DataContext is SidebarItemViewModel sidebarItem && !sidebarItem.IsHeader && Directory.Exists(sidebarItem.Path))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    e.Handled = true;
                    bool isCtrl = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                    bool isMove = !isCtrl;
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    ShellContextMenu.ExecuteFileOperation(hwnd, files, sidebarItem.Path, isMove);
                }
            }
        }

        private void OnColumnListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.DataContext is ColumnViewModel col)
            {
                _viewModel.ActiveColumn = col;
                col.NotifySelectionChanged();
            }
        }

        #endregion

        #region Context Menu Handling

        private void OnListBoxItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem lbi && lbi.DataContext is FileItemViewModel item)
            {
                var parentListBox = FindVisualParent<ListBox>(lbi);
                if (parentListBox != null && parentListBox.DataContext is ColumnViewModel col)
                {
                    _viewModel.ActiveColumn = col;
                    // If right-clicked item is NOT already selected, clear other selections and select it
                    if (!item.IsSelected)
                    {
                        foreach (var it in col.Items)
                        {
                            it.IsSelected = (it == item);
                        }
                        col.NotifySelectionChanged();
                    }
                }
            }

            // CRITICAL: Block ListBox default right-click selection behavior which can clear multi-selection
            e.Handled = true;
        }

        private void OnListBoxItemPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (sender is ListBoxItem lbi && lbi.DataContext is FileItemViewModel item)
            {
                var parentListBox = FindVisualParent<ListBox>(lbi);
                var col = parentListBox?.DataContext as ColumnViewModel;

                System.Collections.Generic.List<string> paths;
                if (col != null && col.SelectedItems.Count > 1 && col.SelectedItems.Contains(item))
                {
                    paths = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(col.SelectedItems, x => x.FullPath));
                }
                else
                {
                    paths = new System.Collections.Generic.List<string> { item.FullPath };
                }

                Point screenPt = lbi.PointToScreen(e.GetPosition(lbi));
                Dispatcher.BeginInvoke(() =>
                {
                    ShellContextMenu.ShowContextMenu(this, paths, (int)screenPt.X, (int)screenPt.Y);
                }, DispatcherPriority.Input);
            }
        }

        private void OnColumnListBoxPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject) == null)
            {
                e.Handled = true;
            }
        }

        private void OnColumnListBoxPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject) == null)
            {
                e.Handled = true;

                if (sender is FrameworkElement fe && fe.DataContext is ColumnViewModel col)
                {
                    Point screenPt = fe.PointToScreen(e.GetPosition(fe));
                    Dispatcher.BeginInvoke(() =>
                    {
                        ShellContextMenu.ShowFolderBackgroundMenu(this, col.FolderPath, (int)screenPt.X, (int)screenPt.Y);
                    }, DispatcherPriority.Input);
                }
            }
        }

        #endregion

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
