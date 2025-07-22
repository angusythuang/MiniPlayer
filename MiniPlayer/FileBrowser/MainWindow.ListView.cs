using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MiniPlayer
{
    public partial class MainWindow
    {

        // 這個集合將作為 ListView 的數據源 (當前選定資料夾的內容)
        public ObservableCollection<FileSystemItem> CurrentDirectoryItems { get; set; }
        public ICollectionView CurrentDirectoryItemsView { get; set; }

        /// <summary>
        /// 載入指定 FileSystemItem 的子目錄和檔案到 ListView。
        /// </summary>
        /// <param name="item">當前的 FileSystemItem，包含子目錄資訊。</param>
        private void LoadItemsForListView(FileSystemItem? item)
        {

            string path = item is null ? "" : item.FullPath;

            // 清除無效歷史紀錄
            CleanInvalidHistoryEntries();

            // 清空 ListView 選中項目並更新地址列
            CurrentDirectoryItems.Clear();
            lvFileList.SelectedItem = null;
            tbPath.Text = path;

            // 檢查路徑有效性
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                if (_currentHistoryIndex >= 0 && _navigationHistory.Count > 0)
                {
                    var historyItem = _navigationHistory[_currentHistoryIndex].Item;
                    historyItem.LoadChildren();
                    historyItem.IsSelected = true;
                    LoadItemsForListView(historyItem);
                }
                UpdateNavigationButtonStates();
                return;
            }

            try
            {
                // 判斷是否與當前歷史記錄中的路徑相同，避免重複加入歷史記錄
                bool isSameAsCurrentHistoryEntry = _currentHistoryIndex >= 0 &&
                                                  _currentHistoryIndex < _navigationHistory.Count &&
                                                  string.Equals(path, _navigationHistory[_currentHistoryIndex].Item.FullPath,
                                                              StringComparison.OrdinalIgnoreCase);

                if (!isSameAsCurrentHistoryEntry)
                {
                    if (_currentHistoryIndex < _navigationHistory.Count - 1)
                    {
                        _navigationHistory.RemoveRange(_currentHistoryIndex + 1,
                                                     _navigationHistory.Count - _currentHistoryIndex - 1);
                    }
                    _navigationHistory.Add(new HistoryEntry { Item = item });
                    _currentHistoryIndex = _navigationHistory.Count - 1;
                    System.Diagnostics.Debug.WriteLine($"Added history entry: {path}, Index: {_currentHistoryIndex}");
                }

                // 添加目錄，使用現有的 FileSystemItem 的 Children 屬性
                foreach (var child in item.Children)
                {
                    CurrentDirectoryItems.Add(child);
                }

                // 載入檔案(每次都會重新載入檔案)
                foreach (string file in Directory.GetFiles(path))
                {
                    FileInfo fileInfo = new FileInfo(file);
                    if ((fileInfo.Attributes & FileAttributes.Hidden) == 0)
                    {
                        CurrentDirectoryItems.Add(new FileSystemItem(file, false));
                    }
                }

                // 僅在有內容時刷新檢視
                if (CurrentDirectoryItems.Any())
                {
                    CurrentDirectoryItemsView.Refresh();
                }

                // 恢復選中項目
                if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count)
                {
                    if (_currentHistoryIndex + 1 < _navigationHistory.Count)
                    {
                        string? nextPath = _navigationHistory[_currentHistoryIndex + 1].Item.FullPath;
                        if (!string.IsNullOrEmpty(nextPath) && Directory.Exists(nextPath) &&
                            string.Equals(Path.GetDirectoryName(nextPath), path,
                                         StringComparison.OrdinalIgnoreCase))
                        {
                            var itemToSelect = CurrentDirectoryItems.FirstOrDefault(i =>
                                string.Equals(Path.GetFullPath(i.FullPath).Replace('/', '\\'),
                                             Path.GetFullPath(nextPath).Replace('/', '\\'),
                                             StringComparison.OrdinalIgnoreCase));
                            if (itemToSelect != null)
                            {
                                lvFileList.SelectedItem = itemToSelect;
                                lvFileList.ScrollIntoView(itemToSelect);
                                System.Diagnostics.Debug.WriteLine($"Selected item restored: {itemToSelect.FullPath}");
                            }
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"無權限訪問此資料夾: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count &&
                    string.Equals(path, _navigationHistory[_currentHistoryIndex].Item.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    _navigationHistory.RemoveAt(_currentHistoryIndex);
                    _currentHistoryIndex = Math.Max(-1, _currentHistoryIndex - 1);
                    System.Diagnostics.Debug.WriteLine($"Removed unauthorized history entry: {path}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count &&
                    string.Equals(path, _navigationHistory[_currentHistoryIndex].Item.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    _navigationHistory.RemoveAt(_currentHistoryIndex);
                    _currentHistoryIndex = Math.Max(-1, _currentHistoryIndex - 1);
                    System.Diagnostics.Debug.WriteLine($"Removed error history entry: {path}");
                }
            }
            finally
            {
                UpdateNavigationButtonStates();
            }
        }

        private void ListViewItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 確保是左鍵雙擊
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                // 獲取被雙擊的 ListViewItem
                if (sender is ListViewItem item && item.DataContext is FileSystemItem selectedLvItem)
                {
                    // 如果被雙擊的是目錄或磁碟機
                    if (selectedLvItem.IsDirectory || selectedLvItem.IsDrive)
                    {
                        try
                        {
                            // 僅更新 ListView 的內容
                            selectedLvItem.LoadChildren(); // 重新載入子目錄
                            LoadItemsForListView(selectedLvItem);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"無法打開目錄：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else // 如果被雙擊的是檔案
                    {
                        try
                        {
                            // 嘗試使用預設程式打開檔案
                            Process.Start(new ProcessStartInfo(selectedLvItem.FullPath) { UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"無法打開檔案：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

    }
}


