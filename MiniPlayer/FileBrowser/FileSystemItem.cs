using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MiniPlayer
{
    public class FileSystemItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public bool IsDirectory { get; set; }
        public bool IsDrive { get; set; }

        private string _fullPath;
        public string FullPath
        {
            get { return _fullPath; }
            set
            {
                if (_fullPath != value)
                {
                    _fullPath = value;
                    OnPropertyChanged(nameof(FullPath));

                    if (string.IsNullOrEmpty(_manualName))
                    {
                        OnPropertyChanged(nameof(Name));
                    }
                    OnPropertyChanged(nameof(Icon));
                }
            }
        }

        private string _manualName = "";

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }


        // 父目錄；當前項目是磁碟機時，父目錄為 null
        private FileSystemItem? _parent;

        // 用於存儲子目錄的集合(排除隱藏目錄、無權存取目錄與檔案；檔案在 lvFileList 裡面動態載入)
        private ObservableCollection<FileSystemItem> _children;
        private readonly ICollectionView _childrenView;

        // 用於存儲圖示的 BitmapSource；如果是目錄則直接使用 IconHelper 的 DirectoryIcon
        // 磁碟機則使用 IconHelper 的 GetItemIcon 方法，且不存入 IconHelper 的 cache
        private BitmapSource? _icon;
        private bool _isUseIconMember;

        // 特殊副檔名集合，這些副檔名會抓取各自的 icon，且不存入 IconHelper 的 cache；使用 HashSet 加速查詢
        private static readonly HashSet<string> SpecialExtensions = new HashSet<string>
        {
            ".scr", ".exe", ".ico", ".lnk"
        };

        public ICollectionView ChildrenView => _childrenView;

        public FileSystemItem(string path, bool isDirectory = false, bool isDrive = false, FileSystemItem? parent = null)
        {
            _fullPath = path;
            IsDirectory = isDirectory;
            IsDrive = isDrive;
            _parent = parent; // 設定父項目
            _children = new ObservableCollection<FileSystemItem>();
            _childrenView = CollectionViewSource.GetDefaultView(_children);
            if (_childrenView is ListCollectionView childrenCollectionView)
            {
                childrenCollectionView.CustomSort = CustomFileSystemItemComparer.Instance;
            }

            _isUseIconMember = false;

            if (isDirectory || isDrive)
            {
                try
                {
                    // 如果有任何一個非隱藏的子目錄，就加一個 DummyChild ，實現 lazy loading
                    if (Directory.EnumerateDirectories(FullPath)
                        .Any(dir => (new DirectoryInfo(dir).Attributes & FileAttributes.Hidden) == 0))
                    {
                        _children.Add(new FileSystemItem("DummyChild", isDirectory: false, isDrive: false, this) { Name = "載入中..." });
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking for subdirectories in {FullPath}: {ex.Message}");
                }

                if (IsDirectory)
                {
                    // 目錄直接使用 IconHelper 的 DirectoryIcon
                    _icon = IconHelper.DirectoryIcon;
                    _isUseIconMember = true;
                }
                else if (IsDrive)
                {
                    // 如果是磁碟機，使用 IconHelper 的 GetItemIcon 方法
                    // 且不存入 IconHelper 的 cache
                    _icon = IconHelper.GetItemIcon(_fullPath, false);
                    _isUseIconMember = true;
                }
            }
            else if (SpecialExtensions.Contains(Path.GetExtension(_fullPath).ToLowerInvariant()))
            {
                // 如果是特殊副檔名，使用 IconHelper 的 GetItemIcon 方法
                // 且不存入 IconHelper 的 cache
                _icon = IconHelper.GetItemIcon(_fullPath, false);
                _isUseIconMember = true;
            }
            else
            {
                // 其他檔案類型，在呼叫 Icon 屬性時才會載入
                _icon = null;
                _isUseIconMember = false;
            }
        }

        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(_manualName))
                {
                    return _manualName;
                }

                if (IsDrive)
                {
                    try
                    {
                        DriveInfo drive = new DriveInfo(FullPath);
                        return $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})";
                    }
                    catch (Exception)
                    {
                        return FullPath;
                    }
                }

                return Path.GetFileName(_fullPath) ?? _fullPath;
            }
            private set
            {
                if (_manualName != value)
                {
                    _manualName = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public BitmapSource? Icon
        {
            get
            {
                if (_isUseIconMember)
                {
                    return _icon;
                }
                else
                {
                    // 如果不是使用 IconMember，則在需要時才載入圖示，
                    // 並且存入 IconHelper 的 cache
                    return IconHelper.GetItemIcon(_fullPath, true);
                }
            }
            set
            {
                if (_icon != value)
                {
                    _icon = value;
                    OnPropertyChanged(nameof(Icon));
                }
            }
        }

        public ObservableCollection<FileSystemItem> Children
        {
            get => _children;
            set
            {
                _children = value;
                OnPropertyChanged(nameof(Children));
            }
        }
        public FileSystemItem? Parent
        {
            get => _parent;
            set
            {
                _parent = value;
                OnPropertyChanged(nameof(Parent));
            }
        }

        public void LoadChildren()
        {
            if (_children.Count == 1 && _children[0].FullPath == "DummyChild")
            {
                // 如果只有一個 DummyChild，表示需要載入子目錄
                _children.Clear();
                try
                {
                    // 只載入子目錄
                    foreach (var dir in Directory.GetDirectories(FullPath))
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(dir);
                        if ((dirInfo.Attributes & FileAttributes.Hidden) == 0)
                        {                            
                            if (HasReadAccess(dir)) // 檢查是否有讀取權限，有才會加入
                                _children.Add(new FileSystemItem(dir, isDirectory: true, isDrive: false, this)); // 設定 Parent 為當前項目
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    //_children.Add(new FileSystemItem("Access Denied", false, false, this) { Name = "無權限訪問" });
                }
                catch (Exception)
                {
                    //_children.Add(new FileSystemItem("Error", false, false, this) { Name = $"載入錯誤: {ex.Message}" });
                }
                _childrenView.Refresh();
            }
        }

        private bool HasReadAccess(string path)
        {
            try
            {
                // 嘗試取得一個目錄資訊
                using (var enumerator = Directory.EnumerateFileSystemEntries(path).GetEnumerator())
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static FileSystemItem? FindItemByPath(IEnumerable<FileSystemItem> rootItems, string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return null;
            }

            // 將路徑標準化，並拆解成各個部分
            // 例如 "C:\Users\UserA" -> ["C:", "Users", "aman"]
            // Path.GetFullPath 能夠處理如 ".." 等相對路徑
            string standardizedPath = Path.GetFullPath(fullPath);
            string[] pathParts = standardizedPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            // 處理磁碟機名稱，例如 "C:"
            string? driveName = Path.GetPathRoot(standardizedPath)?.ToUpperInvariant().TrimEnd(Path.DirectorySeparatorChar);
            if (string.IsNullOrEmpty(driveName))
            {
                return null; // 無法識別根路徑
            }

            // 尋找根項目（磁碟機）
            FileSystemItem? currentItem = rootItems.FirstOrDefault(item =>
                string.Equals(item.FullPath.TrimEnd(Path.DirectorySeparatorChar), driveName, StringComparison.OrdinalIgnoreCase)
            );

            if (currentItem == null)
            {
                return null; // 找不到磁碟機
            }

            // 開始找子目錄
            for (int i = 1; i < pathParts.Length; i++)
            {
                string partToFind = pathParts[i];

                // 確保當前項目已經載入子項目
                currentItem.LoadChildren();                

                // 在子項目中尋找匹配的項目
                FileSystemItem? nextItem = currentItem.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, partToFind, StringComparison.OrdinalIgnoreCase)
                );

                if (nextItem == null)
                {
                    return null; // 找不到路徑中的下一個部分
                }

                currentItem = nextItem;
            }

            return currentItem;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}