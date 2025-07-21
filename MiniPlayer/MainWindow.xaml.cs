using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using static MiniPlayer.CustomFileSystemItemComparer;
using SWF = System.Windows.Forms;

namespace MiniPlayer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
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

            // Treeview 集合
            TreeViewRootItems = new ObservableCollection<FileSystemItem>();
            TreeViewRootItemsView = CollectionViewSource.GetDefaultView(TreeViewRootItems);

            // 清除舊的排序描述
            TreeViewRootItemsView.SortDescriptions.Clear();
            // 設置磁碟機字母順序排序器
            if (TreeViewRootItemsView is ListCollectionView treeCollectionView)
            {
                treeCollectionView.CustomSort = new DriveLetterComparer();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("TreeViewRootItemsView is not a ListCollectionView. CustomSort cannot be applied.");
            }

            // ListView 集合
            CurrentDirectoryItems = new ObservableCollection<FileSystemItem>();
            CurrentDirectoryItemsView = CollectionViewSource.GetDefaultView(CurrentDirectoryItems);

            // 清除舊的排序描述
            CurrentDirectoryItemsView.SortDescriptions.Clear();

            // 將 ICollectionView 轉換為 ListCollectionView 以設定 CustomSort
            if (CurrentDirectoryItemsView is ListCollectionView listCollectionView)
            {
                listCollectionView.CustomSort = new CustomFileSystemItemComparer(); // 設定自訂排序器
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("CurrentDirectoryItemsView is not a ListCollectionView. CustomSort cannot be applied.");
            }

            // 將視窗的 DataContext 設定為自身，這樣 XAML 就能直接綁定到這裡的屬性
            this.DataContext = this;

            // 載入磁碟機到 TreeView
            LoadDrivesIntoTreeView();

            // 嘗試找到第一個可用的磁碟機 (通常為 C 槽)
            string? initialPath = null;
            var firstDrive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady);
            if (firstDrive != null)
            {
                initialPath = firstDrive.RootDirectory.FullName;
                // >>> 在找到初始路徑後，立即載入 ListView 的內容 <<<
                LoadItemsForListView(initialPath); // tbPath.Text 會在 LoadItemsForListView 內部更新
            }

            // 延遲選中第一個磁碟機，直到 TreeView 確定已載入完畢
            if (firstDrive != null)
            {
                // 使用 Dispatcher.BeginInvoke，讓這段程式碼在 UI 準備好後才執行
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var driveItemToSelect = TreeViewRootItems.FirstOrDefault(item => item.IsDrive && item.FullPath == firstDrive.RootDirectory.FullName);
                    if (driveItemToSelect != null)
                    {
                        // 設定 IsSelected 為 true，讓 TreeViewItem 被選中
                        driveItemToSelect.IsSelected = true;

                        // 確保選中的項目在可視範圍內 (如果需要滾動)
                        // 這裡需要確保 TreeViewItem 的容器已生成
                        TreeViewItem? treeViewItem = tvNVPane.ItemContainerGenerator.ContainerFromItem(driveItemToSelect) as TreeViewItem;
                        if (treeViewItem != null)
                        {
                            treeViewItem.BringIntoView();
                        }
                    }
                }), DispatcherPriority.Loaded); // 使用 DispatcherPriority.Loaded 表示在 UI 載入完成後執行
            }


            // 啟用時鐘
            _clockHandler = new ClockHandler(PowerPanelClock);

            // 初始化並啟動磁碟機監聽器
            InitializeDriveWatcher();
            // 視窗關閉時停止監聽
            this.Closed += MainWindow_Closed;

            // 初始化按鈕狀態
            UpdateNavigationButtonStates();
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
                Console.WriteLine("磁碟機變更監聽器已停止。");
            }

            // 時鐘停止
            _clockHandler?.StopClock();
        }
    }
}