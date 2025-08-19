using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            // 直接拿 lvFileList 的選中項目
            FileSystemItem f = (FileSystemItem)lvFileList.SelectedItem;

            DebugInfo.PrintDebugMsg($"貼上：{f.FullPath}");
        }

        private void MenuItem_Cut_Click(object sender, RoutedEventArgs e)
        {
            // 直接拿 lvFileList 的選中項目
            FileSystemItem f = (FileSystemItem)lvFileList.SelectedItem;

            DebugInfo.PrintDebugMsg($"剪下：{f.FullPath}");
        }

        private void MenuItem_Copy_Click(object sender, RoutedEventArgs e)
        {
            // 直接拿 lvFileList 的選中項目
            FileSystemItem f = (FileSystemItem)lvFileList.SelectedItem;

            DebugInfo.PrintDebugMsg($"複製：{f.FullPath}");
        }

        private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is FileSystemItem selectedItem)
            {
                this.Cursor = Cursors.Wait;
                Launch_FileSystemItem(selectedItem);
                this.Cursor = Cursors.Arrow;
                DebugInfo.PrintDebugMsg($"開啟：{selectedItem.FullPath}");
            }
        }        
    }
}
