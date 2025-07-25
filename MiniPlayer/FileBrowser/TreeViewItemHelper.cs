using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using System.ComponentModel;

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

        // 監聽 IsSelected 屬性變更的核心方法
        private static void OnIsSelectedChanged(object? sender, EventArgs e)
        {
            if (sender is TreeViewItem tvi && tvi.IsSelected)
            {
                // 使用 Dispatcher.BeginInvoke 延遲執行，讓 UI 執行緒有機會處理其他更高優先級的任務
                tvi.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // === 關鍵的優化 ===
                    // 只有當 TreeViewItem 已被載入且有佈局時才訂閱 LayoutUpdated 事件
                    // 這能確保我們只處理有效的 UI 元素
                    if (tvi.IsLoaded && tvi.IsArrangeValid) // 檢查 IsArrangeValid 確保佈局已完成
                    {
                        // 確保事件只訂閱一次
                        tvi.LayoutUpdated -= Tvi_LayoutUpdated;
                        tvi.LayoutUpdated += Tvi_LayoutUpdated;

                        // 將 tvi 儲存在事件處理器中，以便後續使用 (這是 C# 9+ 的局部函數閉包特性)
                        void Tvi_LayoutUpdated(object? s, EventArgs ea)
                        {
                            // 立即取消訂閱，以避免重複觸發
                            tvi.LayoutUpdated -= Tvi_LayoutUpdated;
                            tvi.BringIntoView();
                            System.Diagnostics.Debug.WriteLine($"[TreeViewItemHelper] LayoutUpdated and scrolled to: {(tvi.DataContext as FileSystemItem)?.FullPath}");
                        }
                    }
                    else if (tvi.IsLoaded)
                    {
                        // 如果尚未完成佈局，但已載入，直接滾動，這在某些情況下仍然有效
                        tvi.BringIntoView();
                        System.Diagnostics.Debug.WriteLine($"[TreeViewItemHelper] Direct scroll for loaded item: {(tvi.DataContext as FileSystemItem)?.FullPath}");
                    }
                    else
                    {
                        // 如果既未載入也未佈局，可能在更早的階段被觸發，記錄下來
                        System.Diagnostics.Debug.WriteLine($"[TreeViewItemHelper] Item not loaded/arranged yet: {(tvi.DataContext as FileSystemItem)?.FullPath}");
                    }
                }), DispatcherPriority.Background);


                //tvi.Dispatcher.InvokeAsync(() =>
                //{
                //    // 再次檢查 IsLoaded，確保 TreeViewItem 仍是 UI 樹的一部分
                //    if (tvi.IsLoaded)
                //    {
                //        // 呼叫 BringIntoView() 滾動到可視範圍
                //        tvi.BringIntoView();                        
                //        System.Diagnostics.Debug.WriteLine($"[TreeViewItemHelper] Scrolled to selected item: {(tvi.DataContext as FileSystemItem)?.FullPath}");
                //    }
                //}, DispatcherPriority.Render); // Background 優先級給予 UI 更多時間更新
            }
        }
    }
}