using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace MiniPlayer
{
    /// <summary>
    /// 協助從 Windows Shell 取得檔案與資料夾圖示的靜態工具類別。
    /// 包含高效的快取機制，可減少對系統資源的重複請求。
    /// </summary>
    public static class IconHelper
    {
        #region Windows API 結構與常數
        // -----------------------------------------------------------
        // Windows API 結構與常數
        // -----------------------------------------------------------

        // SHFILEINFO 結構，用於接收 SHGetFileInfo 函式的回傳資訊。
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        // SHGetFileInfo 函式所需的旗標。
        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_SYSICONINDEX = 0x4000;
        public const uint SHGFI_LARGEICON = 0x0;
        //public const uint SHGFI_SMALLICON = 0x1; 
        public const uint SHGFI_OPENICON = 0x2; // 開啟狀態的資料夾圖示
        public const uint SHGFI_USEFILEATTRIBUTES = 0x10; // 使用傳入的 dwFileAttributes 而非檢查檔案路徑
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

        // ImageList 相關常數
        public const int SHIL_LARGE = 0x0; // 32x32 圖示
        public const uint ILD_TRANSPARENT = 0x00000001; // 透明圖示

        #endregion

        #region COM 介面與 P/Invoke

        // 導入 Windows API 函式
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

        [DllImport("comctl32.dll")]
        private static extern int ImageList_GetImageCount(IntPtr himl);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

        [ComImport]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IImageList
        {
            [PreserveSig]
            int GetIcon(int i, int flags, out IntPtr phicon);
        }        

        #endregion

        #region 快取與靜態成員

        // 根據您提供的邏輯
        // 1. 增加兩種快取
        // 快取 a: 以系統圖示索引 (iIcon) 為鍵，儲存 BitmapSource 實例。
        private static readonly Dictionary<int, BitmapSource> _iconCacheByIIcon = new Dictionary<int, BitmapSource>();

        // 快取 b: 以標準化副檔名為鍵，儲存 BitmapSource 實例。
        private static readonly Dictionary<string, BitmapSource> _iconCacheByExtension = new Dictionary<string, BitmapSource>();

        // 2. 增加靜態成員
        private static BitmapSource? _directoryIcon;
        private static int _unknownTypeIIcon;

        private static readonly Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

        #endregion

        #region 靜態建構式與屬性

        // 3. 建構式預先載入常用圖示
        static IconHelper()
        {

            // 預抓 Directory Icon (資料夾圖示)
            // 由於檔案系統虛擬化，傳入 "dummy_folder" 即可獲取資料夾圖示。
            // 這裡使用了 GetFileIconInternal 方法來處理
            (_directoryIcon, _) = GetIconInternal("dummy_folder", true);

            // 預抓 Unknown Type Icon (未知檔案類型圖示)
            // 這裡傳入一個沒有副檔名的路徑來模擬未知類型檔案。            
            BitmapSource? unknownIcon;
            (unknownIcon, _unknownTypeIIcon) = GetIconInternal("dummy_file", false);
            if (unknownIcon != null)
            {
                _iconCacheByIIcon[_unknownTypeIIcon] = unknownIcon;
            }
        }

        // 外部可直接使用的 DirectoryIcon 屬性
        public static BitmapSource DirectoryIcon => _directoryIcon;

        #endregion

        #region 公有方法 (Public Methods)

        /// <summary>
        /// 獲取圖示並由呼叫方決定是否加入快取b。
        /// </summary>
        /// <param name="filePath">檔案路徑。</param>
        /// <param name="useCache">是否加入快取b。</param>
        /// <returns>檔案圖示的 BitmapSource，如果抓不到，則回傳 unknown Type Icon 。</returns>
        public static BitmapSource GetItemIcon(string filePath, bool useCache)
        {
            BitmapSource? icon;

            if (useCache)
            {
                // 抓取 icon 時，根據副檔名先確認快取b
                string extension = Path.GetExtension(filePath).ToLowerInvariant();

                // 無副檔名
                if (string.IsNullOrEmpty(extension))
                {
                    // 直接從快取a 回傳靜態成員的 Unknown type icon
                    return _iconCacheByIIcon[_unknownTypeIIcon];
                }

                // 檢查快取b
                if (_iconCacheByExtension.TryGetValue(extension, out BitmapSource? cachedIcon))
                {
                    // hit
                    return cachedIcon;
                }

                // Missing，呼叫 GetAndCacheIcon 處理
                icon = GetAndCacheIcon(filePath, extension);
            }
            else
            {
                // 不使用快取，直接呼叫 GetIconInternal
                (icon, _) = GetIconInternal(filePath, false);
                
            }

            return icon == null ? _iconCacheByIIcon[_unknownTypeIIcon] : icon;
        }

        /// <summary>
        /// 清除全部副檔名快取。
        /// </summary>
        public static void ClearExtensionCache()
        {
            _iconCacheByExtension.Clear();
        }

        /// <summary>
        /// 清除特定副檔名快取。
        /// </summary>
        public static void ClearExtensionCache(string ext)
        {
            _iconCacheByExtension.Remove(ext);
        }

        #endregion

        #region 內部實作 (Private Implementation)

        /// <summary>
        /// 獲取圖示並進行快取處理。
        /// </summary>
        private static BitmapSource? GetAndCacheIcon(string filePath, string extension)
        {
            //取得 iIcon
            int iIcon = GetIIconFromPath(filePath);

            BitmapSource? bs;

            // 檢查快取a 
            if (_iconCacheByIIcon.TryGetValue(iIcon, out BitmapSource? existingIcon))
            {
                bs = existingIcon;
            }
            else
            {
                // 快取a沒命中，根據 iIcon 抓取 bs
                bs = GetIconFromSystemImageList(iIcon);
            }

            if (bs != null)
            {
                // 加入快取a與快取b
                _iconCacheByIIcon[iIcon] = bs;
                _iconCacheByExtension[extension] = bs;
            }
            return bs;

        }

        /// <summary>
        /// 獲取圖示。
        /// </summary>
        private static (BitmapSource?, int) GetIconInternal(string filePath, bool isDirectory)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SYSICONINDEX | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES;
            uint fileAttributes = FILE_ATTRIBUTE_NORMAL;

            if (isDirectory)
            {
                fileAttributes = FILE_ATTRIBUTE_DIRECTORY;
                // 目錄開啟狀態
                flags |= SHGFI_OPENICON;
            }

            IntPtr result = SHGetFileInfo(filePath, fileAttributes, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

            if (shfi.hIcon != IntPtr.Zero)
            {
                try
                {
                    BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                        shfi.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    );
                    bs.Freeze(); // 凍結後，指標的內容永遠指向當前的物件，不可修改；可跨執行緒安全使用。
                    return (bs, shfi.iIcon);
                }
                finally
                {
                    DestroyIcon(shfi.hIcon);
                }
            }
            return (null, 0);
        }

        /// <summary>
        /// 獲取路徑對應的系統圖示索引 (iIcon)。
        /// </summary>
        private static int GetIIconFromPath(string filePath)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_SYSICONINDEX | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES;
            uint fileAttributes = FILE_ATTRIBUTE_NORMAL;
            // 雖然這個方法不返回圖示，但為了獲取 iIcon，仍需呼叫 SHGetFileInfo。
            // 這裡不需要處理 hIcon，因為它會被忽略。
            SHGetFileInfo(filePath, fileAttributes, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
            return shfi.iIcon;
        }

        // 用 iIcon 從系統圖示列表中獲取 BitmapSource。
        public static BitmapSource? GetIconFromSystemImageList(int iIcon)
        {
            IntPtr ppv;
            // 用區域變數存 GUID
            Guid iid = IID_IImageList;
            int hr = SHGetImageList(SHIL_LARGE, ref iid, out ppv);
            if (hr != 0 || ppv == IntPtr.Zero)
                return null;


            int total = ImageList_GetImageCount(ppv);
            if (iIcon < 0 || iIcon >= total)
                return null;

            try
            {
                IImageList imageList = (IImageList)Marshal.GetObjectForIUnknown(ppv);

                int ret = imageList.GetIcon(iIcon, (int)ILD_TRANSPARENT, out IntPtr hIcon);
                if (ret != 0 || hIcon == IntPtr.Zero)
                    return null;

                try
                {
                    BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                        hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bs.Freeze();
                    return bs;
                }
                catch(Exception ex)
                {
                    // 處理可能的例外情況，例如無法創建 BitmapSource
                    System.Diagnostics.Debug.WriteLine($"Error creating BitmapSource from icon: {ex.Message}");
                    return null;
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
            finally
            {
                Marshal.Release(ppv);
            }
        }

        #endregion
    }
}