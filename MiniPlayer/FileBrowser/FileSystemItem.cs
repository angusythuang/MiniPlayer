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
        private BitmapSource? _icon;

        public ICollectionView ChildrenView => _childrenView;

        public FileSystemItem(string path, bool isDirectory, bool isDrive = false, FileSystemItem? parent = null)
        {
            _fullPath = path;
            IsDirectory = isDirectory;
            IsDrive = isDrive;
            _parent = parent; // 設定父項目
            _children = new ObservableCollection<FileSystemItem>();
            _childrenView = CollectionViewSource.GetDefaultView(_children);
            if (_childrenView is ListCollectionView childrenCollectionView)
            {
                childrenCollectionView.CustomSort = new CustomFileSystemItemComparer();
            }

            if (isDirectory || isDrive)
            {
                try
                {
                    if (Directory.EnumerateDirectories(FullPath)
                        .Any(dir => (new DirectoryInfo(dir).Attributes & FileAttributes.Hidden) == 0))
                    {
                        _children.Add(new FileSystemItem("DummyChild", false, false, this) { Name = "Loading..." });
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
                _children.Clear();
                try
                {
                    foreach (var dir in Directory.GetDirectories(FullPath))
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(dir);
                        if ((dirInfo.Attributes & FileAttributes.Hidden) == 0)
                        {
                            // 檢查是否有讀取權限
                            if (HasReadAccess(dir))
                                _children.Add(new FileSystemItem(dir, true, false, this)); // 設定 Parent 為當前項目
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

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}