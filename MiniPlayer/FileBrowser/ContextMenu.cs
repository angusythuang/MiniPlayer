using System.Windows;

namespace MiniPlayer
{
    public partial class MainWindow
    {
        // ListViewItem_MouseRightButtonDown 右鍵點擊刪除
        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (lvFileList.SelectedItem == null)
            {
                MessageBox.Show($"{DebugInfo.Current()} 請先選擇要刪除的項目。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 直接拿 lvFileList 的選中項目
            FileSystemItem f = (FileSystemItem)lvFileList.SelectedItem;

            DebugInfo.PrintDebugMsg($"刪除：{f.FullPath}");
        }

        private void MenuItem_Paste_Click(object sender, RoutedEventArgs e)
        {
            if (lvFileList.SelectedItem == null)
            {
                MessageBox.Show($"{DebugInfo.Current()} 請先選擇要刪除的項目。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 直接拿 lvFileList 的選中項目
            FileSystemItem f = (FileSystemItem)lvFileList.SelectedItem;

            DebugInfo.PrintDebugMsg($"貼上：{f.FullPath}");
        }
        
    }
}
