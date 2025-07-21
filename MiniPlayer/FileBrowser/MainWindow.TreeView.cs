using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MiniPlayer
{
    public partial class MainWindow
    {

        // 這個集合將作為 TreeView 的頂層數據源 (也就是所有的磁碟機)
        public ObservableCollection<FileSystemItem> TreeViewRootItems { get; set; }
        public ICollectionView TreeViewRootItemsView { get; set; }

        /// <summary>
        /// 獲取系統上的所有準備好的磁碟機，並添加到 TreeView 的根項目中。
        /// </summary>
        private void LoadDrivesIntoTreeView()
        {
            TreeViewRootItems.Clear(); // 清空現有項目
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    // 只有當磁碟機準備好時才顯示 (例如，排除沒有插入光碟的光碟機)
                    if (drive.IsReady)
                    {
                        var driveItem = new FileSystemItem(drive.RootDirectory.FullName, true, true);
                        TreeViewRootItems.Add(driveItem);
                    }
                }
                catch (Exception ex)
                {
                    // 捕獲並處理無法訪問的磁碟機錯誤 (例如光碟機沒有光碟時)
                    Console.WriteLine($"無法載入磁碟機 {drive.Name}: {ex.Message}");
                    // 你也可以選擇在這裡添加一個表示錯誤的 FileSystemItem
                    // TreeViewRootItems.Add(new FileSystemItem(drive.Name, false) { Name = $"{drive.Name} (不可用)" });
                }
            }

            TreeViewRootItemsView.Refresh();
        }

        /// <summary>
        /// 當 TreeView 中的項目被展開時觸發，用於懶加載子項目。
        /// </summary>
        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            // 阻止事件冒泡，確保只有實際被展開的 TreeViewItem 觸發加載。
            e.Handled = true;

            if (sender is TreeViewItem treeViewItem)
            {
                if (treeViewItem.DataContext is FileSystemItem fileSystemItem &&
                  (fileSystemItem.IsDirectory || fileSystemItem.IsDrive))
                {
                    // 使用 Dispatcher.BeginInvoke 將加載操作排入 UI 執行緒的背景優先級，
                    // 這樣 UI 就不會被長時間的檔案系統讀取操作卡住。
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        fileSystemItem.LoadChildren(); // 調用 FileSystemItem 中的加載子項目方法
                    }), DispatcherPriority.Background); // Background 優先級確保 UI 保持響應
                }
            }
        }

        /// <summary>
        /// 當 TreeView 中選擇的項目改變時觸發，用於更新 ListView 和地址列。
        /// </summary>
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (tvNVPane.SelectedItem is FileSystemItem selectedItem)
            {
                // 如果選定的是目錄，則載入其內容到 ListView
                if (selectedItem.IsDirectory || selectedItem.IsDrive)
                {
                    LoadItemsForListView(selectedItem.FullPath); // tbPath.Text 會在 LoadItemsForListView 內部更新
                }
                else
                {
                    // 如果選定的是檔案，則清空 ListView 或顯示檔案資訊
                    CurrentDirectoryItems.Clear();
                    // (可選) 這裡可以添加邏輯來顯示單個檔案的詳細資訊
                }
            }
        }

        /// <summary>
        /// 處理 TreeView 的鼠標左鍵按下事件，用於偵測點擊已相同的項目。
        /// </summary>
        private void tvNVPane_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 獲取點擊到的 TreeViewItem
            var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

            if (treeViewItem != null)
            {
                if (treeViewItem.DataContext is FileSystemItem clickedItem)
                {
                    // 檢查點擊的項目是否與當前已選中的項目相同
                    if (tvNVPane.SelectedItem == clickedItem)
                    {
                        // 如果是相同項目被重複點擊，手動觸發 ListView 的內容載入
                        if (clickedItem.IsDirectory || clickedItem.IsDrive)
                        {
                            LoadItemsForListView(clickedItem.FullPath); // tbPath.Text 會在 LoadItemsForListView 內部更新
                        }
                    }
                }
            }
        }

        private T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private FileSystemItem? FindTreeViewItem(FileSystemItem parent, string path)
        {
            if (string.Equals(parent.FullPath, path, StringComparison.OrdinalIgnoreCase)) return parent;

            parent.LoadChildren();
            foreach (var child in parent.Children)
            {
                var found = FindTreeViewItem(child, path);
                if (found != null) return found;
            }
            return null;
        }
    }
}
