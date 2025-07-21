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
        private string _fullPath;
        private string _manualName = "";
        private bool _isSelected;
        private ObservableCollection<FileSystemItem> _children;
        private readonly ICollectionView _childrenView;
        private BitmapSource? _icon;

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
                if (_icon == null)
                {
                    _icon = IconHelper.GetFileIcon(FullPath, smallIcon: false);
                    if (_icon == null && IsDirectory)
                    {
                        _icon = IconHelper.GetFileIcon(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), smallIcon: false);
                    }
                }
                return _icon;
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

        public bool IsDirectory { get; set; }
        public bool IsDrive { get; set; }

        public ObservableCollection<FileSystemItem> Children
        {
            get => _children;
            set
            {
                _children = value;
                OnPropertyChanged(nameof(Children));
            }
        }

        public ICollectionView ChildrenView => _childrenView;

        public FileSystemItem(string path, bool isDirectory, bool isDrive = false)
        {
            _fullPath = path;
            IsDirectory = isDirectory;
            IsDrive = isDrive;
            _children = new ObservableCollection<FileSystemItem>();
            _childrenView = CollectionViewSource.GetDefaultView(_children);

            if (IsDirectory || IsDrive)
            {
                try
                {
                    // 只檢查非隱藏的子目錄
                    if (Directory.EnumerateDirectories(FullPath)
                        .Any(dir => (new DirectoryInfo(dir).Attributes & FileAttributes.Hidden) == 0))
                    {
                        _children.Add(new FileSystemItem("DummyChild", false) { Name = "Loading..." });
                    }
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking for subdirectories in {FullPath}: {ex.Message}");
                }
            }
        }

        public void LoadChildren()
        {
            if (_children.Count == 1 && _children[0].FullPath == "DummyChild")
            {
                _children.Clear();
                try
                {
                    foreach (var dir in Directory.GetDirectories(FullPath))
                    {
                        // 檢查是否為隱藏目錄，排除隱藏目錄
                        DirectoryInfo dirInfo = new DirectoryInfo(dir);
                        if ((dirInfo.Attributes & FileAttributes.Hidden) == 0)
                        {
                            _children.Add(new FileSystemItem(dir, true));
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    _children.Add(new FileSystemItem("Access Denied", false) { Name = "無權限訪問" });
                }
                catch (Exception ex)
                {
                    _children.Add(new FileSystemItem("Error", false) { Name = $"載入錯誤: {ex.Message}" });
                }
                _childrenView.Refresh();
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}