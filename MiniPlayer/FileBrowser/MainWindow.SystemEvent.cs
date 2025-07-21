using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MiniPlayer
{
    public partial class MainWindow
    {       
        // <-- 初始化磁碟機監聽器的方法
        private void InitializeDriveWatcher()
        {
            try
            {
                // WQL (WMI Query Language) 查詢，用於監聽設備變更事件
                // __InstanceOperationEvent 是所有事件的基底類別
                // Win32_DeviceChangeEvent 是設備變更事件
                string query = "SELECT * FROM Win32_VolumeChangeEvent";

                // 創建一個監聽器物件
                _driveWatcher = new ManagementEventWatcher(query);

                // 訂閱事件：當有設備變更時，呼叫 OnDriveChanged 方法
                _driveWatcher.EventArrived += OnDriveChanged;

                // 啟動監聽
                _driveWatcher.Start();
                Console.WriteLine("磁碟機變更監聽器已啟動。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"無法啟動磁碟機監聽器: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 當磁碟機變更事件發生時被呼叫
        private void OnDriveChanged(object sender, EventArrivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string eventType = e.NewEvent.ClassPath.ClassName;
                string? driveName = null;
                int? eventTypeValue = null;

                // 嘗試從事件中獲取磁碟機名稱和事件類型
                if (e.NewEvent.Properties["DriveName"]?.Value != null)
                {
                    driveName = e.NewEvent.Properties["DriveName"].Value.ToString();
                }
                if (e.NewEvent.Properties["EventType"]?.Value != null)
                {
                    eventTypeValue = Convert.ToInt32(e.NewEvent.Properties["EventType"].Value);
                }

                Console.WriteLine($"偵測到磁碟機事件：{eventType}, 磁碟機: {driveName}, EventType: {eventTypeValue}");

                // 只處理 Win32_VolumeChangeEvent
                if (eventType == "Win32_VolumeChangeEvent" && eventTypeValue.HasValue)
                {
                    if (eventTypeValue == 2) // 設備到達（插入）
                    {
                        Console.WriteLine("偵測到磁碟機插入事件。重新載入磁碟機並嘗試跳轉。");
                        LoadDrivesIntoTreeView();

                        // 嘗試找到新插入的磁碟機並選中
                        if (!string.IsNullOrEmpty(driveName))
                        {
                            FileSystemItem? newDriveItem = TreeViewRootItems.FirstOrDefault(item => item.IsDrive && item.FullPath.StartsWith(driveName, StringComparison.OrdinalIgnoreCase));

                            if (newDriveItem != null)
                            {
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    newDriveItem.IsSelected = true;
                                    if (tvNVPane.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                                    {
                                        TreeViewItem? treeViewItem = tvNVPane.ItemContainerGenerator.ContainerFromItem(newDriveItem) as TreeViewItem;
                                        if (treeViewItem != null)
                                        {
                                            treeViewItem.IsSelected = true;
                                            treeViewItem.BringIntoView();
                                        }
                                    }
                                    else
                                    {
                                        tvNVPane.ItemContainerGenerator.StatusChanged += (s, ev) =>
                                        {
                                            if (tvNVPane.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                                            {
                                                TreeViewItem? treeViewItem = tvNVPane.ItemContainerGenerator.ContainerFromItem(newDriveItem) as TreeViewItem;
                                                if (treeViewItem != null)
                                                {
                                                    treeViewItem.IsSelected = true;
                                                    treeViewItem.BringIntoView();
                                                }
                                            }
                                        };
                                    }
                                }), DispatcherPriority.Background);
                            }
                        }
                    }
                    else if (eventTypeValue == 3) // 設備移除（拔出）
                    {
                        Console.WriteLine("偵測到磁碟機移除事件。重新載入磁碟機。");
                        LoadDrivesIntoTreeView();

                        // 檢查當前選中的項目是否位於被移除的磁碟機
                        if (tvNVPane.SelectedItem == null)
                        {
                            CurrentDirectoryItems.Clear();
                            // tbPath.Text = string.Empty; // 由 LoadItemsForListView 統一處理
                            LoadItemsForListView(string.Empty); // 傳遞空字串或預設路徑
                        }
                    }
                    else
                    {
                        Console.WriteLine($"未知的 EventType: {eventTypeValue}. 重新載入磁碟機以確保一致性。");
                        LoadDrivesIntoTreeView();
                    }
                }
                UpdateNavigationButtonStates(); // Update button states after drive changes
            });
        }
    }
}
