using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MiniPlayer
{
    public partial class MainWindow
    {
        // 導航歷史
        private class HistoryEntry // 或者 private class HistoryEntry
        {
            public required FileSystemItem Item { get; set; }
            public FileSystemItem? SelectedItem { get; set; }
        }
        private List<HistoryEntry> _navigationHistory = new List<HistoryEntry>();
        private int _currentHistoryIndex = -1;

        /// <summary>
        /// 更新導航按鈕 (Back, Next, Up) 的啟用狀態。
        /// </summary>
        private void UpdateNavigationButtonStates()
        {
            // Assuming btnBack, btnNext, and btnUp are defined in XAML and accessible by name.
            // If not, you might need to find them using FindName or pass them as parameters.

            // Back button
            if (btnBack != null)
            {
                btnBack.IsEnabled = _currentHistoryIndex > 0;
            }

            // Next button
            if (btnNext != null)
            {
                btnNext.IsEnabled = _currentHistoryIndex < _navigationHistory.Count - 1;
            }

            // Up button
            if (btnUp != null)
            {
                string currentPath = CurrentDir.CurrentItem.FullPath;
                bool canGoUp = false; // 直接判斷是否可以向上導航

                if (!string.IsNullOrEmpty(currentPath))
                {
                    // 如果當前路徑是磁碟機根目錄 (例如 "C:\")，則不能向上
                    // 判斷方式：3個字元長度，第二個字元是冒號，第三個字元是路徑分隔符號
                    bool isCurrentPathDriveRoot = (currentPath.Length == 3 &&
                                                   currentPath[1] == ':' &&
                                                   currentPath[2] == Path.DirectorySeparatorChar);

                    // 只有當路徑不是空的且不是磁碟機根目錄時，才能向上導航
                    canGoUp = !isCurrentPathDriveRoot;
                }

                btnUp.IsEnabled = canGoUp;
            }
        }

        /// <summary>
        /// 處理 Back 按鈕點擊事件，導航到上一個目錄。
        /// </summary>
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHistoryIndex > 0)
            {
                _currentHistoryIndex--;
                var targetItem = _navigationHistory[_currentHistoryIndex].Item;
                DebugInfo.PrintDebugMsg($"Navigating back to: {targetItem.FullPath}, HistoryIndex: {_currentHistoryIndex}");
                CurrentDir.CurrentItem = targetItem; // 修改 CurrentDir
                targetItem.IsSelected = true;
                // Optionally, update the TreeView selection
                // You would need to implement a method to select the item in TreeView
                // based on the path, which might involve expanding nodes.
            }
            //lvFileList.Focus();
        }

        /// <summary>
        /// 處理 Next 按鈕點擊事件，導航到下一個目錄。
        /// </summary>
        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHistoryIndex < _navigationHistory.Count - 1)
            {
                _currentHistoryIndex++;
                var targetItem = _navigationHistory[_currentHistoryIndex].Item;
                DebugInfo.PrintDebugMsg($"Navigating next to: {targetItem.FullPath}, HistoryIndex: {_currentHistoryIndex}");
                CurrentDir.CurrentItem = targetItem; // 修改 CurrentDir
                targetItem.IsSelected = true;
                // Optionally, update the TreeView selection
                // You would need to implement a method to select the item in TreeView
                // based on the path, which might involve expanding nodes.
            }
            //lvFileList.Focus();
        }

        /// <summary>
        /// 處理 Up 按鈕點擊事件，導航到父目錄。
        /// </summary>
        private void btnUp_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentDir.CurrentItem != null && CurrentDir.CurrentItem.Parent != null)
            {
                try
                {
                    var parentItem = CurrentDir.CurrentItem.Parent;
                    CurrentDir.CurrentItem = parentItem; // 更新 CurrentDir，觸發 PropertyChanged
                    parentItem.IsSelected = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{DebugInfo.Current()} 無法導航到上一級目錄：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            //lvFileList.Focus();
        }

        /// <summary>
        /// 獲取當前的歷史記錄項目。
        /// </summary>
        /// <returns>當前的 HistoryEntry，如果無有效項目則返回 null。通常一定會有第一個 C:\ 存在。 </returns>
        private HistoryEntry? GetCurrentHistoryEntry()
        {
            if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count)
            {
                return _navigationHistory[_currentHistoryIndex];
            }
            return null;
        }

        /// <summary>
        /// 處理導航歷史的更新，當 CurrentDir.CurrentItem 改變時調用。
        /// </summary>
        /// <param name="currentItem">當前選擇的 FileSystemItem</param>
        /// <returns>元組 (isValid, targetItem, historyIndex)：
        /// - isValid: 表示當前路徑是否有效
        /// - targetItem: 有效的 FileSystemItem（當前項目或回退項目）
        /// </returns>
        private (bool isValid, FileSystemItem? targetItem) HandleNavigationHistoryUpdate(FileSystemItem currentItem)
        {
            string path = currentItem.FullPath;

            try
            {
                // 步驟 1：驗證路徑有效性
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    CleanInvalidHistoryEntries();

                    if (_currentHistoryIndex >= 0 && _navigationHistory.Count > 0)
                    {
                        // 回退到歷史記錄中的有效項目
                        return (false, _navigationHistory[_currentHistoryIndex].Item);
                    }
                    else
                    {
                        // 回退到第一個可用磁碟機
                        var fallbackItem = TreeViewRootItems.FirstOrDefault(item => item.IsDrive && Directory.Exists(item.FullPath));
                        return (false, fallbackItem);
                    }
                }

                // 步驟 2：判斷是否要更新歷史記錄
                bool isSameAsCurrentHistoryEntry = _currentHistoryIndex >= 0 &&
                                                  _currentHistoryIndex < _navigationHistory.Count &&
                                                  string.Equals(path, _navigationHistory[_currentHistoryIndex].Item.FullPath,
                                                              StringComparison.OrdinalIgnoreCase);

                if (!isSameAsCurrentHistoryEntry)
                {
                    // 準備新的歷史記錄
                    var newEntry = new HistoryEntry { Item = currentItem };

                    // 處理 SelectedItem 邏輯（父子目錄）
                    if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count)
                    {
                        var previousEntry = _navigationHistory[_currentHistoryIndex];

                        // 子目錄：當前項目的 Parent 等於前一個歷史項目的 Item
                        if (currentItem.Parent == previousEntry.Item)
                        {
                            previousEntry.SelectedItem = currentItem;
                            DebugInfo.PrintDebugMsg($"Set previous entry's SelectedItem to subfolder: {currentItem.FullPath}");
                        }
                        // 父目錄：前一個歷史項目的 Parent 等於當前項目
                        else if (previousEntry.Item.Parent == currentItem)
                        {
                            newEntry.SelectedItem = previousEntry.Item;
                            DebugInfo.PrintDebugMsg($"Set current entry's SelectedItem to subfolder: {previousEntry.Item.FullPath}");
                        }
                    }

                    if (_currentHistoryIndex < _navigationHistory.Count - 1)
                    {
                        _navigationHistory.RemoveRange(_currentHistoryIndex + 1, _navigationHistory.Count - _currentHistoryIndex - 1);
                    }
                    _navigationHistory.Add(newEntry);
                    _currentHistoryIndex = _navigationHistory.Count - 1;
                    DebugInfo.PrintDebugMsg($"Added history entry: {path}, Index: {_currentHistoryIndex}");
                }

                return (true, currentItem);
            }
            finally
            {
                // try 區塊裡面的 return 返回前，都會先呼叫這個函式
                UpdateNavigationButtonStates();
            } 
        }

        /// <summary>
        /// 清除無效歷史紀錄。
        /// </summary>
        private void CleanInvalidHistoryEntries()
        {
            for (int i = _navigationHistory.Count - 1; i >= 0; i--)
            {
                string historyPath = _navigationHistory[i].Item.FullPath;
                bool isValid = !string.IsNullOrEmpty(historyPath) && Directory.Exists(historyPath);
                if (isValid)
                {
                    try
                    {
                        Directory.GetDirectories(historyPath);
                    }
                    catch
                    {
                        isValid = false;
                    }
                }

                if (!isValid && i != 0)
                {
                    if (i <= _currentHistoryIndex)
                    {
                        _navigationHistory.RemoveAt(i);
                        _currentHistoryIndex--;
                        DebugInfo.PrintDebugMsg($"Removed invalid history entry (index <= _currentHistoryIndex): {historyPath}, New Index: {_currentHistoryIndex}");
                    }
                    else
                    {
                        _navigationHistory.RemoveAt(i);
                        DebugInfo.PrintDebugMsg($"Removed invalid history entry (index > _currentHistoryIndex): {historyPath}");
                    }
                }
            }
            _currentHistoryIndex = Math.Max(-1, _currentHistoryIndex);
        }
    }
}
