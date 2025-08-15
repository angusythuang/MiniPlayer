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
        //public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_SYSICONINDEX = 0x4000;
        public const uint SHGFI_LARGEICON = 0x0;
        public const uint SHGFI_ATTRIBUTES = 0x000000800;
        //public const uint SHGFI_SMALLICON = 0x1; 
        public const uint SHGFI_OPENICON = 0x2; // 開啟狀態的資料夾圖示
        public const uint SHGFI_USEFILEATTRIBUTES = 0x10; // 使用傳入的 dwFileAttributes 而非檢查檔案路徑

        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

        // 常見的 SFGAO_* 屬性旗標
        public const uint SFGAO_LINK = 0x00010000; // 捷徑
        public const uint ILD_NORMAL = 0x00000000; // 正常圖示
        //public const uint SFGAO_SHARE = 0x00020000;      // 共享
        //public const uint SFGAO_ENCRYPTED = 0x00002000;  // 加密

        // 計算 overlay mask
        private static uint INDEXTOOVERLAYMASK(uint i) => i << 8;

        // ImageList 相關常數
        public const int SHIL_LARGE = 0x0; // 32x32 圖示
        public const uint ILD_TRANSPARENT = 0x00000001; // 透明圖示

        #endregion

        #region P/Invoke

        // 導入 Windows API 函式
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

        [DllImport("comctl32.dll", CharSet = CharSet.Unicode)]
        public static extern bool ImageList_DrawEx(IntPtr himl, int i, IntPtr hdcDst, int x, int y, int cx, int cy, int rgbBk, int rgbFg, int fStyle);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

        #endregion

        #region 快取與靜態成員

        // 根據您提供的邏輯
        // 兩種快取
        // iIcon快取: 以系統圖示索引 (iIcon) 為鍵，儲存 BitmapSource 實例。
        private static readonly Dictionary<int, BitmapSource> _iconCacheByIIcon = new Dictionary<int, BitmapSource>();

        private static BitmapSource? _directoryIcon;  // 目錄用的 Icon
        public static BitmapSource DirectoryIcon => _directoryIcon;         // 外部可直接使用的 DirectoryIcon 屬性

        private static BitmapSource? _unknownTypeIcon;  // 未知類型檔案的 Icon
        private static int _unknownTypeIIcon;         // 未知類型檔案的 iIcon
        public static BitmapSource? UnknownTypeIcon => _unknownTypeIcon;

        private static readonly Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
        #endregion

        #region 靜態建構式與屬性

        // 建構式預先載入常用圖示
        static IconHelper()
        {
            // 預抓 Directory Icon (資料夾圖示)
            // 傳入 "dummy_folder" 獲取資料夾圖示。
            //(iIcon, _) = GetIIconAndOverlayFromPath("dummy_folder", true);
            _directoryIcon = GetIconFromSystemImageList(GetIIconAndOverlayFromPath("dummy_folder", true).Item1, ILD_NORMAL); // 抓取目錄圖示

            // 預抓 Unknown Type Icon (未知檔案類型圖示)
            // 傳入一個沒有副檔名的路徑來模擬未知類型檔案。
            (_unknownTypeIIcon, _) = GetIIconAndOverlayFromPath("dummy_file");
            _unknownTypeIcon = GetIconFromSystemImageList(_unknownTypeIIcon, ILD_NORMAL); // 抓取未知類型檔案圖示
            if (_unknownTypeIcon != null)
            {
                // 加入 iIcon快取，後續如果有未知類型檔案的請求，直接從快取中取得。
                _iconCacheByIIcon[_unknownTypeIIcon] = _unknownTypeIcon;
            }
        }

        #endregion

        #region 公有方法 (Public Methods)

        /// <summary>
        /// 獲取圖示並由呼叫方決定是否加入快取b。
        /// </summary>
        /// <param name="filePath">檔案路徑。</param>
        /// <param name="isUseExtension">是否只檢查副檔名。</param>
        /// <returns>檔案圖示的 BitmapSource，如果抓不到，則回傳 unknown Type Icon 。</returns>
        public static BitmapSource GetItemIcon(string filePath, bool isUseExtension = false)
        {
            BitmapSource? icon;

            if (isUseExtension)
            {
                // 抓取 icon 時，使用副檔名
                string extension = Path.GetExtension(filePath).ToLowerInvariant();

                // 無副檔名
                if (string.IsNullOrEmpty(extension))
                    icon = _unknownTypeIcon;

                // Missing，呼叫 GetIconByExt 取得該副檔名的 icon
                icon = GetIconByExt(extension);
            }
            else
            {
                // 不使用副檔名，直接從路徑取得圖示
                var (iIcon, attr) = GetIIconAndOverlayFromPath(filePath);
                icon = GetIconFromSystemImageList(iIcon, attr);

            }

            // 如果 icon 為 null，則回傳未知類型的圖示
            return icon ?? _unknownTypeIcon;
        }

        #endregion

        #region 內部實作 (Private Implementation)

        /// <summary>
        /// 獲取圖示並進行快取處理。
        /// </summary>
        private static BitmapSource? GetIconByExt(string extension)
        {
            // 用假的副檔名取得 iIcon
            int iIcon;
            (iIcon, _) = GetIIconAndOverlayFromPath($"dummy_file.{extension}");

            BitmapSource? bs;

            // 檢查iIcon快取 
            if (_iconCacheByIIcon.TryGetValue(iIcon, out BitmapSource? existingIcon))
            {
                // iIcon快取命中，直接返回 icon
                return existingIcon;
            }
            else
            {
                // iIcon快取沒命中，根據 iIcon 抓取 bs
                bs = GetIconFromSystemImageList(iIcon, ILD_NORMAL); // 抓正常圖示，不須加任 overlay
            }

            if (bs != null)
            {
                // 加入iIcon快取
                _iconCacheByIIcon[iIcon] = bs;
            }

            return bs;
        }

        /// <summary>
        /// overlay 判斷
        /// </summary>
        public static uint GetOverlayMask(uint attr)
        {
            if ((attr & SFGAO_LINK) != 0)
                return INDEXTOOVERLAYMASK(2); // 捷徑藍色小箭頭
            //if ((attr & SFGAO_SHARE) != 0)
            //    return INDEXTOOVERLAYMASK(2); // 共享小手
            //if ((attr & SFGAO_ENCRYPTED) != 0)
            //    return INDEXTOOVERLAYMASK(4); // 加密鎖頭

            return 0; // 無 overlay
        }

        /// <summary>
        /// 取得檔案的圖示索引與 overlay mask
        /// </summary>
        private static (int, uint) GetIIconAndOverlayFromPath(string path, bool isDirectory = false)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_SYSICONINDEX | SHGFI_ATTRIBUTES | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES;
            uint fileAttributes = FILE_ATTRIBUTE_NORMAL; // 預設為檔案屬性

            if (isDirectory)
            {
                fileAttributes = FILE_ATTRIBUTE_DIRECTORY; // 如果是目錄，則使用目錄屬性
                // 目錄開啟狀態
                flags |= SHGFI_OPENICON;
            }

            SHGetFileInfo(path, fileAttributes, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

            return (shfi.iIcon, GetOverlayMask(shfi.dwAttributes));
        }

        /// <summary>
        /// 由 iIcon 與 overlayMask 取得 BitmapSource
        /// </summary>
        private static BitmapSource? GetIconFromSystemImageList(int iIcon, uint overlayMask)
        {
            IntPtr hImageList = IntPtr.Zero;
            try
            {
                Guid riid = IID_IImageList;
                int result = SHGetImageList(SHIL_LARGE, ref riid, out IntPtr ppv);
                if (result == 0 && ppv != IntPtr.Zero)
                {
                    hImageList = ppv;

                    IntPtr hIcon = ImageList_GetIcon(hImageList, iIcon, overlayMask);

                    if (hIcon != IntPtr.Zero)
                    {
                        try
                        {
                            BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                                hIcon,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions()
                            );
                            bs.Freeze();
                            return bs;
                        }
                        finally
                        {
                            DestroyIcon(hIcon);
                        }
                    }
                }
            }
            finally
            {
                if (hImageList != IntPtr.Zero)
                {
                    Marshal.Release(hImageList);
                }
            }
            return null;
        }

        #endregion
    }
}