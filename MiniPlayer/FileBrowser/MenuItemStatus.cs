using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MiniPlayer
{
    public class MenuItemStatus : INotifyPropertyChanged
    {
        // 供剪下複製貼上使用
        public List<FileSystemItem>? SrcItems { get; set; } = null;
        public FileSystemItem? DestItem { get; set; } = null;

        // 當前操作狀態
        public ActionStatus CurrentAction { get; set; } = ActionStatus.None;
        public enum ActionStatus
        {
            None,
            Cut,    
            Copy,
        }

        // 實作每個屬性，在 set 中呼叫 OnPropertyChanged
        private Visibility _openVisiblity = Visibility.Collapsed;
        public Visibility OpenVisiblity
        {
            get => _openVisiblity;
            set
            {
                if (_openVisiblity != value)
                {
                    _openVisiblity = value;
                    OnPropertyChanged();
                }
            }
        }

        private Visibility _removeVisiblity = Visibility.Collapsed;
        public Visibility RemoveVisiblity
        {
            get => _removeVisiblity;
            set
            {
                if (_removeVisiblity != value)
                {
                    _removeVisiblity = value;
                    OnPropertyChanged();
                }
            }
        }

        private Visibility _cutVisiblity = Visibility.Collapsed;
        public Visibility CutVisiblity
        {
            get => _cutVisiblity;
            set
            {
                if (_cutVisiblity != value)
                {
                    _cutVisiblity = value;
                    OnPropertyChanged();
                }
            }
        }

        private Visibility _copyVisiblity = Visibility.Collapsed;
        public Visibility CopyVisiblity
        {
            get => _copyVisiblity;
            set
            {
                if (_copyVisiblity != value)
                {
                    _copyVisiblity = value;
                    OnPropertyChanged();
                }
            }
        }

        private Visibility _pasteVisiblity = Visibility.Collapsed;
        public Visibility PasteVisiblity
        {
            get => _pasteVisiblity;
            set
            {
                if (_pasteVisiblity != value)
                {
                    _pasteVisiblity = value;
                    OnPropertyChanged();
                }
            }
        }

        private Visibility _deleteVisiblity = Visibility.Collapsed;
        public Visibility DeleteVisiblity
        {
            get => _deleteVisiblity;
            set
            {
                if (_deleteVisiblity != value)
                {
                    _deleteVisiblity = value;
                    OnPropertyChanged();
                }
            }
        }

        // 屬性變更事件
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}