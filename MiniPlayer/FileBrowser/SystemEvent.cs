using System;
using System.Collections.Generic;
using System.IO;
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
                DebugInfo.PrintDebugMsg("磁碟機變更監聽器已啟動。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{DebugInfo.Current()} 無法啟動磁碟機監聽器: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
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

                if (e.NewEvent.Properties["DriveName"]?.Value != null)
                {
                    driveName = e.NewEvent.Properties["DriveName"].Value.ToString();
                }
                if (e.NewEvent.Properties["EventType"]?.Value != null)
                {
                    eventTypeValue = Convert.ToInt32(e.NewEvent.Properties["EventType"].Value);
                }

                if (string.IsNullOrEmpty(driveName) || eventTypeValue == null)
                {
                    MessageBox.Show($"{DebugInfo.Current()} 無法取得磁碟機名稱或事件類型。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                DebugInfo.PrintDebugMsg($"偵測到磁碟機事件：{eventType}, 磁碟機: {driveName}, EventType: {eventTypeValue}");

                if (eventType == "Win32_VolumeChangeEvent" && eventTypeValue.HasValue)
                {
                    if (eventTypeValue == 2) // 設備到達（插入）
                    {
                        DebugInfo.PrintDebugMsg("偵測到插入磁碟機 {driveName} 事件。嘗試選中新磁碟機。");
                        AddDriveToTreeView(driveName);

                        if (!string.IsNullOrEmpty(driveName))
                        {
                            FileSystemItem? newDriveItem = TreeViewRootItems.FirstOrDefault(item =>
                                item.IsDrive && item.FullPath.StartsWith(driveName, StringComparison.OrdinalIgnoreCase));
                            if (newDriveItem != null)
                            {
                                // 設定 CurrentDir.CurrentItem，觸發 PropertyChanged
                                CurrentDir.CurrentItem = newDriveItem;
                            }
                        }
                    }
                    else if (eventTypeValue == 3) // 設備移除（拔出）
                    {
                        DebugInfo.PrintDebugMsg("偵測到移除磁碟機 {driveName} 事件。回退到歷史記錄前一個項目。");
                        // 呼叫移除磁碟機，同時 TreeView 會自動更新顯示
                        RemoveDriveFromTreeView(driveName);

                        // 呼叫 ForceUpdate，將觸發 PropertyChanged 事件，
                        // 進而觸發 CleanInvalidHistoryEntries 來自動導航到最近一個有效的歷史路徑
                        CurrentDir.ForceUpdate();
                    }
                    else
                    {
                        DebugInfo.PrintDebugMsg($"未知的 EventType: {eventTypeValue}. 已重新載入磁碟機。");
                    }
                }
            });
        }
    }
}
