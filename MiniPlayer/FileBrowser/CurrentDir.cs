using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MiniPlayer
{
    /// <summary>
    /// 儲存當前目錄的 FileSystemItem，當 CurrentItem 改變時發出 PropertyChanged 事件，
    /// 並自動調用 LoadChildren 以載入子目錄（排除隱藏和無權存取項目）。
    /// 通知 tvNVPane 和 lvFileList 更新顯示內容。
    /// </summary>
    public class CurrentDir : INotifyPropertyChanged
    {
        private FileSystemItem _currentItem = new FileSystemItem("Dummy_item");

        public event PropertyChangedEventHandler? PropertyChanged;

        public FileSystemItem CurrentItem
        {
            get => _currentItem;
            set
            {
                if (_currentItem != value) // 檢查是否真的改變了
                {
                    _currentItem = value;
                    if (_currentItem != null && (_currentItem.IsDirectory || _currentItem.IsDrive))
                    {
                        _currentItem.LoadChildren(); // 自動載入子目錄
                    }
                    OnPropertyChanged(nameof(CurrentItem));
                }
            }
        }

        /// <summary>
        /// 強制更新 CurrentItem，即使值沒有改變。
        /// </summary>
        public void ForceUpdate()
        {
            // 在此處呼叫 LoadChildren，以確保資料被重新載入
            if (_currentItem != null && (_currentItem.IsDirectory || _currentItem.IsDrive))
            {
                // 檢查路徑有效性
                if (System.IO.Directory.Exists(_currentItem.FullPath))
                {
                    _currentItem.LoadChildren(true);
                }
                else
                {
                    // 路徑無效
                    DebugInfo.PrintDebugMsg($"無效的路徑: {_currentItem.FullPath}");
                }
            }

            OnPropertyChanged(nameof(CurrentItem));
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}