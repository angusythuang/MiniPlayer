using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MiniPlayer
{
    public partial class MainWindow
    {
        // 開啟 menu 前先設定各個 item 的顯示屬性
        private void lvFileList_ContextMenuOpening(object sender, RoutedEventArgs e)
        {
            // 預設所有選項都先隱藏
            _menuItemStatus.OpenVisiblity = Visibility.Collapsed;
            _menuItemStatus.RemoveVisiblity = Visibility.Collapsed; // 只有可移除磁碟機才會用上，tvNVPane 會有這個情況
            _menuItemStatus.CutVisiblity = Visibility.Collapsed;
            _menuItemStatus.CopyVisiblity = Visibility.Collapsed;
            _menuItemStatus.DeleteVisiblity = Visibility.Collapsed;

            // 判斷是否有內容可以貼上
            _menuItemStatus.PasteVisiblity = _menuItemStatus.SrcItem is null ? Visibility.Collapsed : Visibility.Visible;

            var originalSource = e.OriginalSource as FrameworkElement;

            // 從原始來源向上尋找 ListViewItem
            var clickedItem = FindAncestor<ListViewItem>(originalSource);

            if (clickedItem != null)
            {
                // 如果點擊的是一個 ListViewItem，表示點擊在檔案或資料夾上
                if (clickedItem.DataContext is FileSystemItem fileItem)
                {
                    _menuItemStatus.DeleteVisiblity = Visibility.Visible;
                    _menuItemStatus.OpenVisiblity = Visibility.Visible;
                    _menuItemStatus.CutVisiblity = Visibility.Visible;
                    _menuItemStatus.CopyVisiblity = Visibility.Visible;
                    
                }
            }

            var contextMenu = lvFileList.ContextMenu;
            if (contextMenu != null)
            {
                contextMenu.DataContext = _menuItemStatus;
            }

        }


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

        private void MenuItem_Remove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is FileSystemItem selectedItem)
            {
                this.Cursor = Cursors.Wait;
                //Launch_FileSystemItem(selectedItem);
                this.Cursor = Cursors.Arrow;
                DebugInfo.PrintDebugMsg($"退出：{selectedItem.FullPath}");
            }
        }
    }
}
