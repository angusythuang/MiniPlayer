using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace MiniPlayer
{
    public static class TreeViewItemHelper
    {
        public static readonly DependencyProperty AutoScrollOnSelectProperty =
            DependencyProperty.RegisterAttached(
                "AutoScrollOnSelect",
                typeof(bool),
                typeof(TreeViewItemHelper),
                new UIPropertyMetadata(false, OnAutoScrollOnSelectChanged));

        public static bool GetAutoScrollOnSelect(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollOnSelectProperty);
        }

        public static void SetAutoScrollOnSelect(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollOnSelectProperty, value);
        }

        private static void OnAutoScrollOnSelectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeViewItem tvi)
            {
                if ((bool)e.NewValue)
                {
                    // === 核心改變：我們訂閱的是 IsSelected 屬性的值變更，而非 Selected 事件 ===
                    // 這能避免虛擬化造成的事件冒泡或重複呼叫問題。
                    DependencyPropertyDescriptor.FromProperty(TreeViewItem.IsSelectedProperty, typeof(TreeViewItem))
                                                ?.AddValueChanged(tvi, OnIsSelectedChanged);
                }
                else
                {
                    // 當屬性被移除時，取消訂閱以避免記憶體洩漏
                    DependencyPropertyDescriptor.FromProperty(TreeViewItem.IsSelectedProperty, typeof(TreeViewItem))
                                                ?.RemoveValueChanged(tvi, OnIsSelectedChanged);
                }
            }
        }

        private static void OnIsSelectedChanged(object? sender, EventArgs e)
        {
            if (sender is TreeViewItem tvi && tvi.DataContext is FileSystemItem item)
            {
                if (!item.IsSelected) return; // 只處理選中事件

                Debug.WriteLine($"[TreeViewItemHelper] IsSelected changed for: {item.FullPath}");
                Debug.WriteLine($"[TreeViewItemHelper] Initial state - IsLoaded: {tvi.IsLoaded}, IsMeasureValid: {tvi.IsMeasureValid}, IsArrangeValid: {tvi.IsArrangeValid}");

                // 使用 InvokeAsync 確保在 UI 執行緒上執行，且在 Render 優先級下給予佈局機會
                tvi.Dispatcher.InvokeAsync(() =>
                {
                    // 確保所有父節點都已展開。這是確保 TreeViewItem 容器被生成的前提。
                    item.ExpandAllParents();

                    // 關鍵點：在項目完全佈局完成後再呼叫 BringIntoView()
                    // 由於沒有虛擬化，這裡不需要複雜的 ItemContainerGenerator 檢查，
                    // 但我們需要等待佈局更新。
                    // 使用 Dispatcher.InvokeAsync 再次排隊，並檢查佈局狀態。
                    tvi.Dispatcher.InvokeAsync(() =>
                    {
                        Debug.WriteLine($"[TreeViewItemHelper] After parent expand InvokeAsync - IsLoaded: {tvi.IsLoaded}, IsMeasureValid: {tvi.IsMeasureValid}, IsArrangeValid: {tvi.IsArrangeValid} for {item.FullPath}");

                        // 判斷是否已經準備好捲動
                        if (tvi.IsLoaded && tvi.IsMeasureValid && tvi.IsArrangeValid)
                        {
                            tvi.BringIntoView();
                            Debug.WriteLine($"[TreeViewItemHelper] Successfully scrolled to: {item.FullPath}");
                        }
                        else
                        {
                            // 如果還沒準備好，嘗試訂閱 LayoutUpdated 事件
                            // 使用一個局部變數來儲存事件處理器，方便在觸發後移除
                            EventHandler? layoutUpdatedHandler = null;
                            layoutUpdatedHandler = (s, args) =>
                            {
                                // 確保只執行一次
                                tvi.LayoutUpdated -= layoutUpdatedHandler;
                                Debug.WriteLine($"[TreeViewItemHelper] LayoutUpdated triggered for: {item.FullPath}, IsLoaded: {tvi.IsLoaded}, IsMeasureValid: {tvi.IsMeasureValid}, IsArrangeValid: {tvi.IsArrangeValid}");

                                if (tvi.IsLoaded && tvi.IsMeasureValid && tvi.IsArrangeValid)
                                {
                                    tvi.BringIntoView();
                                    Debug.WriteLine($"[TreeViewItemHelper] Scrolled via LayoutUpdated to: {item.FullPath}");
                                }
                                else
                                {
                                    // 仍然無法捲動，可能存在更深層次的佈局問題或時機問題
                                    Debug.WriteLine($"[TreeViewItemHelper] Still not ready after LayoutUpdated for: {item.FullPath}");
                                }
                            };
                            tvi.LayoutUpdated += layoutUpdatedHandler;
                            Debug.WriteLine($"[TreeViewItemHelper] Waiting for LayoutUpdated for: {item.FullPath}");
                        }
                    }, DispatcherPriority.Render); // 仍然使用 Render 優先級
                }, DispatcherPriority.Render); // 第一次 InvokeAsync
            }
        }

#region Debug用
#if DEBUG
        /// <summary>
        /// 只搜尋目前已產生的 TreeViewItem，不強制展開。
        /// </summary>
        public static TreeViewItem? FindTreeViewItem(TreeView treeView, FileSystemItem target)
        {
            foreach (var item in treeView.Items)
            {
                var tvi = treeView.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                var found = FindTreeViewItemRecursive(tvi, target);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static TreeViewItem? FindTreeViewItemRecursive(TreeViewItem? tvi, FileSystemItem target)
        {
            if (tvi == null) return null;

            if (tvi.DataContext is FileSystemItem fsItem &&
                ReferenceEquals(fsItem, target))
            {
                return tvi;
            }

            // 只搜尋已產生的子 TreeViewItem，不展開
            foreach (var child in tvi.Items)
            {
                var childTvi = tvi.ItemContainerGenerator.ContainerFromItem(child) as TreeViewItem;
                var found = FindTreeViewItemRecursive(childTvi, target);
                if (found != null)
                    return found;
            }
            return null;
        }
#endif
#endregion


    }
}