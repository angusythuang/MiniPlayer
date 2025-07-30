using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using static MiniPlayer.CustomFileSystemItemComparer;

namespace MiniPlayer
{
    public partial class MainWindow
    {

        // 這個集合將作為 TreeView 的頂層數據源 (也就是所有的磁碟機)
        public ObservableCollection<FileSystemItem> TreeViewRootItems { get; set; } = new ObservableCollection<FileSystemItem>();
        public required ICollectionView TreeViewRootItemsView { get; set; }


        private void InitializeTreeView()
        {
            // Treeview 集合
            TreeViewRootItemsView = CollectionViewSource.GetDefaultView(TreeViewRootItems);

            // 清除舊的排序描述
            TreeViewRootItemsView.SortDescriptions.Clear();
            // 設定磁碟機字母順序排序器
            if (TreeViewRootItemsView is ListCollectionView treeCollectionView)
            {
                treeCollectionView.CustomSort = new DriveLetterComparer();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("TreeViewRootItemsView is not a ListCollectionView. CustomSort cannot be applied.");
            }

            // 載入磁碟機到 TreeView
            LoadDrivesIntoTreeView();
        }

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
                    System.Diagnostics.Debug.WriteLine($"無法載入磁碟機 {drive.Name}: {ex.Message}");
                    // 你也可以選擇在這裡添加一個表示錯誤的 FileSystemItem
                    // TreeViewRootItems.Add(new FileSystemItem(drive.Name, false) { Name = $"{drive.Name} (不可用)" });
                }
            }

            TreeViewRootItemsView.Refresh();
        }

        /// <summary>
        /// 新增磁碟機到 TreeView。
        /// </summary>
        private void AddDriveToTreeView(string drivePath)
        {
            try
            {
                var drive = new DriveInfo(drivePath);
                // 檢查磁碟機是否已存在且已就緒
                if (drive.IsReady && !TreeViewRootItems.Any(item => item.FullPath.Equals(drive.RootDirectory.FullName, StringComparison.OrdinalIgnoreCase)))
                {
                    var driveItem = new FileSystemItem(drive.RootDirectory.FullName, true, true);
                    TreeViewRootItems.Add(driveItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"新增磁碟機 {drivePath} 失敗: {ex.Message}");
                MessageBox.Show($"{DebugInfo.Current()} 新增磁碟機 {drivePath} 失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 從 TreeView 移除磁碟機。
        /// </summary>
        private void RemoveDriveFromTreeView(string drivePath)
        {
            string driveLetter = drivePath.Length >= 2 ? drivePath.Substring(0, 2) : string.Empty;
            if (string.IsNullOrEmpty(driveLetter)) return;

            // 使用 StartsWith 進行比對，更加直觀。
            // item.FullPath (例如 "C:\") 的開頭只要與 driveLetter (例如 "C:") 匹配即可。
            var itemToRemove = TreeViewRootItems.FirstOrDefault(item =>
                item.IsDrive && item.FullPath.StartsWith(driveLetter, StringComparison.OrdinalIgnoreCase));

            if (itemToRemove != null)
            {
                TreeViewRootItems.Remove(itemToRemove);
            }
        }

        /// <summary>
        /// 將目前的 FileSystemItem 設定為選中項目。此方法只操作資料模型。
        /// UI 滾動行為將由 TreeViewItemHelper 自動處理。
        /// </summary>
        private void SelectTreeViewItemByPath(FileSystemItem targetItem)
        {
            if (targetItem == null || string.IsNullOrEmpty(targetItem.FullPath))
            {
                return;
            }

            // 遞迴展開所有父節點
            var parent = targetItem.Parent;
            while (parent != null)
            {
                parent.IsExpanded = true;
                parent = parent.Parent;
            }

            // 設定當前項目為選取狀態。這將觸發附加屬性中的邏輯。
            targetItem.IsSelected = true;

            
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
        /// 處理 Enter 鍵，僅當按下 Enter 時更新 CurrentDir
        /// </summary>
        private void tvNVPane_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (tvNVPane.SelectedItem is FileSystemItem selectedItem &&
                    (selectedItem.IsDirectory || selectedItem.IsDrive))
                {
                    CurrentDir.CurrentItem = selectedItem; // 更新 CurrentDir，觸發 PropertyChanged
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// 處理 TreeView 的鼠標左鍵按下事件
        /// </summary>
        private void tvNVPane_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 檢查是否點擊在展開/收合按鈕 (ToggleButton) 上
            if (e.OriginalSource is DependencyObject source && FindAncestor<System.Windows.Controls.Primitives.ToggleButton>(source) != null)
            {
                return;
            }

            // 獲取點擊到的 TreeViewItem
            var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
            if (treeViewItem != null)
            {
                if (treeViewItem.DataContext is FileSystemItem clickedItem)
                {
                    // 僅當點擊的是目錄或磁碟機時更新 CurrentDir
                    if (clickedItem.IsDirectory || clickedItem.IsDrive)
                    {
                        CurrentDir.CurrentItem = clickedItem; // 更新 CurrentDir，觸發 PropertyChanged
                        clickedItem.IsSelected = true; // 設定視覺選中
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
    }
}
