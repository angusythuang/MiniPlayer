using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using SWF = System.Windows.Forms;

namespace MiniPlayer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // 當前目錄
        public CurrentDir CurrentDir { get; } = new CurrentDir();

        private ManagementEventWatcher? _driveWatcher = null; // 用於監聽磁碟機變更的物件

        // 時鐘
        private ClockHandler? _clockHandler;

        public MainWindow()
        {
            InitializeComponent();

            // 取得除了工作列以外的範圍
            System.Drawing.Rectangle workingArea = SWF.Screen.PrimaryScreen?.WorkingArea ?? throw new InvalidOperationException("No primary screen found.");

            // 設定視窗的尺寸和位置
            this.Left = workingArea.Left;
            this.Top = workingArea.Top;
            this.Width = workingArea.Width;
            this.Height = workingArea.Height;

            // 將視窗的 DataContext 設定為自身，這樣 XAML 就能直接綁定到這裡的屬性
            this.DataContext = this;
            // 訂閱 CurrentDir 的 PropertyChanged 事件
            CurrentDir.PropertyChanged += CurrentDir_PropertyChanged;

            InitializeTreeView(); // 初始化 TreeView 集合
            InitializeListView(); // 初始化 ListView 集合            
            
            // 初始化時設定 CurrentDir.CurrentItem 為第一個可用的磁碟機(通常為 C:\ )
            if (DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady) != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var driveItemToSelect = TreeViewRootItems.FirstOrDefault(item => item.IsDrive);
                    if (driveItemToSelect != null)
                    {
                        CurrentDir.CurrentItem = driveItemToSelect; // 設定 CurrentDir
                        driveItemToSelect.IsSelected = true;
                        TreeViewItem? treeViewItem = tvNVPane.ItemContainerGenerator.ContainerFromItem(driveItemToSelect) as TreeViewItem;
                        if (treeViewItem != null)
                        {
                            treeViewItem.BringIntoView();
                        }
                        // 初始化按鈕狀態
                        UpdateNavigationButtonStates();
                    }
                }), DispatcherPriority.Loaded);
            }

            // 啟用時鐘
            _clockHandler = new ClockHandler(PowerPanelClock);

            // 初始化並啟動磁碟機監聽器
            InitializeDriveWatcher();

            // 視窗關閉時執行相關的清理工作
            this.Closed += MainWindow_Closed;
        }

        // 接收 CurrentDir 的 PropertyChanged 事件，處理 tvNVPane、lvFileList、_navigationHistory
        private void CurrentDir_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CurrentDir.CurrentItem))
            {
                var currentItem = CurrentDir.CurrentItem;
                if (currentItem != null && (currentItem.IsDirectory || currentItem.IsDrive))  // 確保是目錄或磁碟機
                {
                    // 確認路徑有效
                    var (isValid, targetItem) = HandleNavigationHistoryUpdate(currentItem);
                    if (!isValid && targetItem != null)
                    {
                        // 路徑無效
                        // 已歷史中清除無效的路徑
                        // 並退回到前一個有效的路徑
                        CurrentDir.CurrentItem = targetItem;
                        return; // 等待下一次 PropertyChanged
                    }
                    else if (!isValid)
                    {
                        // 無有效回退項目，清空 UI 
                        // 應該不可能發生，因為一定有一個初始的路徑 (通常是 C:\ )
                        // 但為了保險起見，這裡要印出錯誤訊息
                        CurrentDirectoryItems.Clear();
                        MessageBox.Show($"{DebugInfo.Current()} 無有效的退回路徑", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 更新導航按鈕狀態
                    UpdateNavigationButtonStates();

                    // 同步 UI：更新 lvFileList、tvNVPane
                    LoadItemsForListView(currentItem);
                    SelectTreeViewItemByPath(currentItem);
                }
                else
                {
                    // 處理無效 CurrentItem：清空 UI
                    MessageBox.Show($"{DebugInfo.Current()} 收到非檔案或磁碟機的參數", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }       

        // 使用 FullPath 拆分層層查找
        private void SelectTreeViewItemByPath(FileSystemItem targetItem)
        {
            if (targetItem == null || string.IsNullOrEmpty(targetItem.FullPath)) return;

            // 拆分 FullPath 為層次
            string[] pathParts = targetItem.FullPath.TrimEnd(Path.DirectorySeparatorChar)
                                            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length == 0) return;

            // 從頂層磁碟機開始
            string drivePath = pathParts[0].EndsWith(":") ? pathParts[0] + @"\" : pathParts[0];
            FileSystemItem? currentItem = TreeViewRootItems.FirstOrDefault(item =>
                string.Equals(item.FullPath, drivePath, StringComparison.OrdinalIgnoreCase));

            if (currentItem == null) return;

            // 逐層查找
            for (int i = 1; i < pathParts.Length; i++)
            {
                currentItem.LoadChildren(); // 確保子目錄已載入
                string currentPathPart = pathParts[i];
                currentItem = currentItem.Children.FirstOrDefault(child =>
                    string.Equals(Path.GetFileName(child.FullPath), currentPathPart, StringComparison.OrdinalIgnoreCase));

                if (currentItem == null) return; // 路徑無效，停止查找

                // 展開父節點
                TreeViewItem? parentTreeViewItem = tvNVPane.ItemContainerGenerator.ContainerFromItem(currentItem) as TreeViewItem;
                if (parentTreeViewItem != null)
                {
                    parentTreeViewItem.IsExpanded = true;
                }
            }

            // 選擇目標節點
            if (currentItem != null)
            {
                TreeViewItem? treeViewItem = tvNVPane.ItemContainerGenerator.ContainerFromItem(currentItem) as TreeViewItem;
                if (treeViewItem != null)
                {
                    treeViewItem.IsSelected = true;
                    treeViewItem.BringIntoView();
                }
            }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnMini_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // 視窗關閉時停止監聽，釋放資源
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            if (_driveWatcher != null)
            {
                _driveWatcher.Stop();
                _driveWatcher.Dispose(); // 釋放資源
                System.Diagnostics.Debug.WriteLine("磁碟機變更監聽器已停止。");
            }

            // 時鐘停止
            _clockHandler?.StopClock();
        }

        public static class DebugInfo
        {
            public static string Current(
                [CallerFilePath] string file = "",
                [CallerLineNumber] int line = 0,
                [CallerMemberName] string func = "")
            {
                return $"[{Path.GetFileName(file)}:{line} - {func}]";
            }
        }
    }
}