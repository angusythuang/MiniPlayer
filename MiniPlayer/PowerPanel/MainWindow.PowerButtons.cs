using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MiniPlayer
{
    public partial class MainWindow
    {
        //--------------------------- 休眠 -----------------------------------
        // 導入 powrprof.dll 中的 SetSuspendState 函數 
        // force: true = 強制關閉所有程式並進入睡眠/休眠，false = 嘗試正常關閉
        // hibernate: true = 進入休眠，false = 進入睡眠
        // wakeLock: true = 阻止電腦因某些事件自動喚醒 (通常設為 false)
        [DllImport("powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] // 函數返回一個布林值
        static extern bool SetSuspendState(bool force, bool hibernate, bool wakeLock);
        //--------------------------- 休眠 End-----------------------------------       

        private void btnSleep_Click(object sender, RoutedEventArgs e)
        {
            bool result = SetSuspendState(false, false, false);

            if (!result)
            {
                // 獲取錯誤碼以進行更詳細的診斷
                int errorCode = Marshal.GetLastWin32Error();
                MessageBox.Show($"無法進入睡眠。錯誤碼: {errorCode}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnReboot_Click(object sender, RoutedEventArgs e)
        {
            // 呼叫系統命令
            try
            {
                Process.Start("shutdown.exe", "/r /t 0"); // /r = 重啟, /t 0 = 立即
            }
            catch (Exception ex)
            {
                MessageBox.Show($"無法重啟電腦：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnShutdown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("shutdown.exe", "/s /t 0"); // /s = 關機, /t 0 = 立即
            }
            catch (Exception ex)
            {
                MessageBox.Show($"無法關機電腦：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
