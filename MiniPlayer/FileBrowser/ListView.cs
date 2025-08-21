using MiniPlayer.FileBrowser;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MiniPlayer
{
    public partial class MainWindow
    {

        // 這個集合將作為 ListView 的數據源 (當前選定資料夾的內容)
        public ObservableCollection<FileSystemItem> CurrentDirectoryItems { get; set; } = new ObservableCollection<FileSystemItem>();
        public required ICollectionView CurrentDirectoryItemsView { get; set; }

        private void InitializeListView()
        {
            // ListView View
            CurrentDirectoryItemsView = CollectionViewSource.GetDefaultView(CurrentDirectoryItems);

            // 清除舊的排序描述
            CurrentDirectoryItemsView.SortDescriptions.Clear();

            // 將 ICollectionView 轉換為 ListCollectionView 以設定 CustomSort
            if (CurrentDirectoryItemsView is ListCollectionView listCollectionView)
            {
                listCollectionView.CustomSort = CustomFileSystemItemComparer.Instance; // 設定自訂排序器
            }
            else
            {
                DebugInfo.PrintDebugMsg("CurrentDirectoryItemsView is not a ListCollectionView. CustomSort cannot be applied.");
            }
        }

        /// <summary>
        /// 載入指定 FileSystemItem 的子目錄和檔案到 ListView。
        /// </summary>
        /// <param name="item">當前的 FileSystemItem，包含子目錄資訊。</param>
        private void LoadItemsForListView(FileSystemItem item)
        {
            string path = item.FullPath;

            // 清空 ListView 選中項目並更新地址列
            CurrentDirectoryItems.Clear();
            lvFileList.SelectedItem = null;

            // 檢查路徑有效性
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                this.Cursor = Cursors.Wait;
                // 添加目錄，使用現有的 FileSystemItem 的 Children 屬性
                foreach (var child in item.Children)
                {
                    CurrentDirectoryItems.Add(child);
                }

                // 載入檔案
                foreach (string file in Directory.GetFiles(path))
                {
                    FileInfo fileInfo = new FileInfo(file);
                    if ((fileInfo.Attributes & FileAttributes.Hidden) == 0)
                    {
                        CurrentDirectoryItems.Add(new FileSystemItem(file));
                    }
                }

                // 有東西才更新畫面
                if (CurrentDirectoryItems.Count > 0)
                {
                    CurrentDirectoryItemsView.Refresh();
                }

                // 設定 ListView 的選擇狀態
                var currentHistoryEntry = GetCurrentHistoryEntry();
                if (currentHistoryEntry?.SelectedItem != null)
                {
                    var itemToSelect = CurrentDirectoryItems.FirstOrDefault(i =>
                        string.Equals(i.Name, currentHistoryEntry.SelectedItem.Name, StringComparison.OrdinalIgnoreCase));
                    if (itemToSelect != null)
                    {
                        lvFileList.SelectedItem = itemToSelect;
                        lvFileList.ScrollIntoView(itemToSelect);
                        DebugInfo.PrintDebugMsg($"Selected ListView item: {itemToSelect.FullPath}");
                    }
                }
                else
                {
                    if (lvFileList.Items.Count > 0)
                    {
                        lvFileList.ScrollIntoView(lvFileList.Items[0]);
                    }
                }

                this.Cursor = Cursors.Arrow;
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"{DebugInfo.Current()} 無權限訪問此資料夾: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count &&
                    string.Equals(path, _navigationHistory[_currentHistoryIndex].Item.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    _navigationHistory.RemoveAt(_currentHistoryIndex);
                    _currentHistoryIndex = Math.Max(-1, _currentHistoryIndex - 1);
                    DebugInfo.PrintDebugMsg($"Removed unauthorized history entry: {path}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{DebugInfo.Current()} 載入錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count &&
                    string.Equals(path, _navigationHistory[_currentHistoryIndex].Item.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    _navigationHistory.RemoveAt(_currentHistoryIndex);
                    _currentHistoryIndex = Math.Max(-1, _currentHistoryIndex - 1);
                    DebugInfo.PrintDebugMsg($"Removed error history entry: {path}");
                }
            }
        }

        /// <summary>
        /// ListViewItem_MouseDoubleClick 更新 CurrentDir.CurrentItem
        /// </summary>
        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (sender is ListViewItem item && item.DataContext is FileSystemItem selectedLvItem)
                {
                    Launch_FileSystemItem(selectedLvItem);
                }
            }
        }

        /// <summary>
        /// Launch_FileSystemItem 開啟檔案或目錄。
        /// </summary>
        private void Launch_FileSystemItem(FileSystemItem item)
        {
            if (item.IsDirectory || item.IsDrive)
            {
                // 如果是目錄或磁碟機，則設定 CurrentDir.CurrentItem
                try
                {
                    CurrentDir.CurrentItem = item;
                    return; // 直接返回，因為 CurrentDir.CurrentItem 的變更會自動處理子目錄載入
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{DebugInfo.Current()} 無法打開目錄：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // 如果是檔案

                if (".lnk" == Path.GetExtension(item.FullPath).ToLowerInvariant())
                {
                    // 如果是捷徑，解析目標路徑
                    var result = ShortcutHelper.GetShortcutInfo(item.FullPath);
                    if (result.Success)
                    {
                        if (result.IsDirectory)
                        {
                            try
                            {
                                CurrentDir.CurrentItem = FileSystemItem.FindItemByPath(TreeViewRootItems, result.TargetPath);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"無法打開捷徑：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                            }

                            // 直接返回，後面為檔案處理。
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show($"解析 .lnk 失敗: {result.ErrorMessage}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        return; // 解析失敗，不繼續打開檔案
                    }
                }

                try
                {
                    // SelectedItem，處理 HistoryEntry 的 SelectedItem
                    var currentHistoryEntry = GetCurrentHistoryEntry();
                    if (currentHistoryEntry != null && CurrentDir.CurrentItem != null)
                    {
                        currentHistoryEntry.SelectedItem = item;
                        DebugInfo.PrintDebugMsg($"Set SelectedItem to file: {item.FullPath}");
                    }

                    // 使用系統預設程式打開檔案
                    Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{DebugInfo.Current()} 無法打開檔案：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }

        // 用於拖曳選取的起點
        private Point? _startPoint = null;

        /// <summary>
        /// lvFileList_PreviewMouseLeftButtonDown 按下滑鼠左鍵。
        /// </summary>
        private void lvFileList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 取得滑鼠按下的起點
            _startPoint = e.GetPosition(lvFileList);
        }

        /// <summary>
        /// lvFileList_PreviewMouseMove 滑鼠移動。
        /// </summary>
        private void lvFileList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _startPoint.HasValue)
            {
                // 如果滑鼠正在按住且有起點，則計算拖曳範圍
                Point currentPoint = e.GetPosition(lvFileList);

                // 判斷是否為有效拖曳（移動距離超過一定值）
                if (Math.Abs(currentPoint.X - _startPoint.Value.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPoint.Y - _startPoint.Value.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // 清空當前選取，準備重新選取
                    lvFileList.SelectedItems.Clear();

                    // 取得拖曳範圍的矩形
                    Rect selectionRect = new Rect(_startPoint.Value, currentPoint);

                    // 迭代 ListView 中的所有項目
                    foreach (var item in lvFileList.Items)
                    {
                        // 獲取項目的視覺樹容器
                        var container = lvFileList.ItemContainerGenerator.ContainerFromItem(item) as UIElement;
                        if (container != null)
                        {
                            // 將容器的邊界轉換到 ListView 的座標系統
                            Rect containerBounds = new Rect(container.TranslatePoint(new Point(0, 0), lvFileList), container.RenderSize);

                            // 檢查項目邊界是否與拖曳矩形相交
                            if (selectionRect.IntersectsWith(containerBounds))
                            {
                                // 如果相交，就選取這個項目
                                lvFileList.SelectedItems.Add(item);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// lvFileList_PreviewMouseMove 放開滑鼠左鍵。
        /// </summary>
        private void lvFileList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 拖曳結束，清除起點
            _startPoint = null;
        }
    }
}


