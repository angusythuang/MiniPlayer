using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MiniPlayer
{
    public class ClipboardManager : INotifyPropertyChanged
    {
        // 確保它是單例模式
        private static ClipboardManager? _instance;
        public static ClipboardManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ClipboardManager();
                }
                return _instance;
            }
        }

        private bool _hasContentToPaste;
        public bool HasContentToPaste
        {
            get => _hasContentToPaste;
            private set
            {
                if (_hasContentToPaste != value)
                {
                    _hasContentToPaste = value;
                    OnPropertyChanged();
                }
            }
        }

        // 判斷是否可以貼上的方法
        public void SetClipboardState(bool canPaste)
        {
            HasContentToPaste = canPaste;
        }

        // 觸發屬性變更通知的方法
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}