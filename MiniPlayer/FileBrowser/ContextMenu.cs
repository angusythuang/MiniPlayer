using System.Diagnostics;
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
            _menuItemStatus.PasteVisiblity = _menuItemStatus.SourceItems is null ? Visibility.Collapsed : Visibility.Visible;

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

            bool anyVisible = _menuItemStatus.OpenVisiblity == Visibility.Visible ||
                              _menuItemStatus.RemoveVisiblity == Visibility.Visible ||
                              _menuItemStatus.CutVisiblity == Visibility.Visible ||
                              _menuItemStatus.CopyVisiblity == Visibility.Visible ||
                              _menuItemStatus.PasteVisiblity == Visibility.Visible ||
                              _menuItemStatus.DeleteVisiblity == Visibility.Visible;

            // 如果所有 MenuItem 都是隱藏的，就阻止 ContextMenu 顯示
            if (!anyVisible)
            {
                e.Handled = true;
            }
        }

        // ListViewItem_MouseRightButtonDown 右鍵點擊刪除
        private async void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (lvFileList.SelectedItems.Count == 0)
            {
                MessageBox.Show("請先選擇要刪除的項目。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var selectedItems = lvFileList.SelectedItems.Cast<FileSystemItem>().ToList();

            this.Cursor = Cursors.Wait;
            try
            {
                await FileOperationHelper.Delete(selectedItems);                               
            }
            catch (OperationCanceledException ex)
            {
                MessageBox.Show(ex.Message, "操作取消", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刪除失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private async void MenuItem_Paste_Click(object sender, RoutedEventArgs e)
        {
            if (_menuItemStatus.SourceItems == null || !_menuItemStatus.SourceItems.Any() || CurrentDir.CurrentItem == null)
            {
                MessageBox.Show("無法貼上，來源或目的項目無效。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.Cursor = Cursors.Wait;
            try
            {
                if (_menuItemStatus.Action == MenuItemStatus.ActionStatus.Cut)
                {
                    await FileOperationHelper.Move(_menuItemStatus.SourceItems, CurrentDir.CurrentItem);
                }
                else if (_menuItemStatus.Action == MenuItemStatus.ActionStatus.Copy)
                {
                    await FileOperationHelper.Copy(_menuItemStatus.SourceItems, CurrentDir.CurrentItem);
                }

                _menuItemStatus.SourceItems = null;
                _menuItemStatus.Action = MenuItemStatus.ActionStatus.None;
            }
            catch (OperationCanceledException ex)
            {
                MessageBox.Show(ex.Message, "操作取消", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"貼上失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void MenuItem_Cut_Click(object sender, RoutedEventArgs e)
        {
            if (lvFileList.SelectedItems.Count == 0) return;

            _menuItemStatus.SourceItems = lvFileList.SelectedItems.Cast<FileSystemItem>().ToList();
            _menuItemStatus.Action = MenuItemStatus.ActionStatus.Cut;

        }

        private void MenuItem_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (lvFileList.SelectedItems.Count == 0) return;

            _menuItemStatus.SourceItems = lvFileList.SelectedItems.Cast<FileSystemItem>().ToList();
            _menuItemStatus.Action = MenuItemStatus.ActionStatus.Copy;

        }

        private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
        {
            List<FileSystemItem> selectedItems = lvFileList.SelectedItems.Cast<FileSystemItem>().ToList();

            if (selectedItems.Count > 0)
            {
                this.Cursor = Cursors.Wait;
                foreach (FileSystemItem item in selectedItems)
                {
                    DebugInfo.PrintDebugMsg($"開啟：{item.FullPath}");
                    // Launch_FileSystemItem 方法來處理開啟檔案或資料夾
                    Launch_FileSystemItem(item);
                }
                
                this.Cursor = Cursors.Arrow;
                
            }
        }

        private void MenuItem_Remove_Click(object sender, RoutedEventArgs e)
        {
            //if (sender is MenuItem menuItem && menuItem.DataContext is FileSystemItem selectedItem)
            //{
            //    this.Cursor = Cursors.Wait;
            //    //Launch_FileSystemItem(selectedItem);
            //    this.Cursor = Cursors.Arrow;
            //    DebugInfo.PrintDebugMsg($"退出：{selectedItem.FullPath}");
            //}
        }
    }
}
