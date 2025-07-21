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
            public required string Path { get; set; }
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
                string? currentPath = tbPath.Text;
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
                string targetPath = _navigationHistory[_currentHistoryIndex].Path;
                System.Diagnostics.Debug.WriteLine($"Navigating back to: {targetPath}, HistoryIndex: {_currentHistoryIndex}");
                LoadItemsForListView(targetPath);
                // Optionally, update the TreeView selection
                // You would need to implement a method to select the item in TreeView
                // based on the path, which might involve expanding nodes.
            }
        }

        /// <summary>
        /// 處理 Next 按鈕點擊事件，導航到下一個目錄。
        /// </summary>
        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHistoryIndex < _navigationHistory.Count - 1)
            {
                _currentHistoryIndex++;
                string targetPath = _navigationHistory[_currentHistoryIndex].Path;
                System.Diagnostics.Debug.WriteLine($"Navigating next to: {targetPath}, HistoryIndex: {_currentHistoryIndex}");
                LoadItemsForListView(targetPath);
                // Optionally, update the TreeView selection
            }
        }

        /// <summary>
        /// 處理 Up 按鈕點擊事件，導航到父目錄。
        /// </summary>
        private void btnUp_Click(object sender, RoutedEventArgs e)
        {
            string currentPath = tbPath.Text;
            if (!string.IsNullOrEmpty(currentPath))
            {
                try
                {
                    DirectoryInfo? parent = Directory.GetParent(currentPath);
                    if (parent != null)
                    {
                        string parentPath = parent.FullName;
                        LoadItemsForListView(parentPath); // tbPath.Text 會在 LoadItemsForListView 內部更新
                        // Optionally, update the TreeView selection
                    }
                    else
                    {
                        // Already at the root of a drive, disable Up button (handled by UpdateNavigationButtonStates)
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"無法導航到上一級目錄：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            UpdateNavigationButtonStates();
        }

        /// <summary>
        /// 清除無效歷史紀錄。
        /// </summary>
        private void CleanInvalidHistoryEntries()
        {
            for (int i = _navigationHistory.Count - 1; i >= 0; i--)
            {
                string historyPath = _navigationHistory[i].Path;
                bool isValid = !string.IsNullOrEmpty(historyPath) && Directory.Exists(historyPath);
                if (isValid)
                {
                    try
                    {
                        Directory.GetDirectories(historyPath); // 檢查權限
                    }
                    catch
                    {
                        isValid = false;
                    }
                }

                if (!isValid && i != 0) // 保護索引 0
                {
                    if (i <= _currentHistoryIndex)
                    {
                        _navigationHistory.RemoveAt(i);
                        _currentHistoryIndex--;
                        System.Diagnostics.Debug.WriteLine($"Removed invalid history entry (index <= _currentHistoryIndex): {historyPath}, New Index: {_currentHistoryIndex}");
                    }
                    else
                    {
                        _navigationHistory.RemoveAt(i);
                        System.Diagnostics.Debug.WriteLine($"Removed invalid history entry (index > _currentHistoryIndex): {historyPath}");
                    }
                }
            }
            _currentHistoryIndex = Math.Max(-1, _currentHistoryIndex);
        }
    }
}
