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
using System.Windows.Media;
using System.Windows.Threading;
using SWF = System.Windows.Forms;

namespace MiniPlayer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // 當前目錄，設定為屬性，讓 XAML 可以綁定
        public CurrentDir CurrentDir { get; set; }= new CurrentDir();        

        // 用於監聽磁碟機變更的物件
        private ManagementEventWatcher? _driveWatcher = null;

        // 右鍵選單各項目的狀態
        private readonly MenuItemStatus _menuItemStatus;

        // 時鐘
        private ClockHandler? _clockHandler;

        private double _top, _left, _width, _height;

        public MainWindow()
        {
            InitializeComponent();

            _menuItemStatus = this.Resources["MenuItemStatus"] as MenuItemStatus 
                                ?? throw new InvalidOperationException("找不到 MenuItemStatus resource"); ;

            // 取得除了工作列以外的範圍
            System.Drawing.Rectangle workingArea = SWF.Screen.PrimaryScreen?.WorkingArea ?? throw new InvalidOperationException("No primary screen found.");

            // 設定視窗的尺寸和位置
            _left = workingArea.Left;
            _top = workingArea.Top;
            _width = workingArea.Width;
            _height = workingArea.Height;

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

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            // 檢查尺寸或位置是否被改變
            if (this.Top != _top || this.Left != _left || this.Width != _width || this.Height != _height)
            {
                // 如果有改變，就還原
                this.Top = _top;
                this.Left = _left;
                this.Width = _width;
                this.Height = _height;
            }            
        }

        // 接收 CurrentDir 的 PropertyChanged 事件，處理 tvNVPane、lvFileList、_navigationHistory
        private void CurrentDir_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CurrentDir.CurrentItem))
            {
                var currentItem = CurrentDir.CurrentItem;
                if (currentItem != null && (currentItem.IsDirectory || currentItem.IsDrive))  // 確保是目錄或磁碟機
                {
                    // HandleNavigationHistoryUpdate() 會檢查路徑是否有效，並更新歷史紀錄
                    var (isValid, targetItem) = HandleNavigationHistoryUpdate(currentItem);
                    if (!isValid && targetItem != null)
                    {
                        // 路徑無效
                        // 歷史紀錄中已清除無效的路徑
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

                    // 同步 UI：更新 lvFileList、tvNVPane                    
                    LoadItemsForListView(currentItem);
                    SelectTreeViewItem(currentItem);

#if DEBUG
                    //if (TreeViewItemHelper.FindTreeViewItem(tvNVPane, currentItem) is null)
                    //{
                    //    ;
                    //}
#endif
                }
                else
                {
                    // 處理無效 CurrentItem：清空 UI
                    MessageBox.Show($"{DebugInfo.Current()} 收到非檔案或磁碟機的參數", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
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
                DebugInfo.PrintDebugMsg("磁碟機變更監聽器已停止。");
            }

            // 時鐘停止
            _clockHandler?.StopClock();
        }

        /// <summary>
        /// 尋找指定類型的父級元素
        /// </summary>
        private T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
#if DEBUG
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
#endif

                while (current != null)
                {
                    if (current is T match) return match;
                    current = VisualTreeHelper.GetParent(current);
                }
                return null;
#if DEBUG
            }
            finally
            {
                sw.Stop();
                Debug.WriteLine($"[FindAncestor] 查找 {typeof(T).Name} 耗時: {sw.ElapsedMilliseconds} ms");
            }
#endif
        }
    }
}