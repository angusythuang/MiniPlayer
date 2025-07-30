using System.Diagnostics;             // For System.Diagnostics.Debug.WriteLine
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;         // For Imaging.CreateBitmapSourceFromHIcon
using System.Windows.Media.Imaging;   // For BitmapSource

namespace MiniPlayer
{
    public static class IconHelper
    {
        // 儲存 SHGetFileInfo 函數返回的資訊的結構
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;        // 處理圖示
            public int iIcon;           // 圖示索引
            public uint dwAttributes;   // 檔案屬性
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName; // 顯示名稱
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;    // 類型名稱
        }

        // SHGetFileInfo 函數的旗標
        public const uint SHGFI_ICON = 0x100;              // 獲取圖示
        public const uint SHGFI_SMALLICON = 0x1;           // 獲取小圖示 (16x16)
        public const uint SHGFI_LARGEICON = 0x0;           // 獲取大圖示 (32x32)
        public const uint SHGFI_USEFILEATTRIBUTES = 0x10;  // 即使檔案不存在，也使用檔案屬性來獲取圖示
        public const uint SHGFI_OPENICON = 0x00000002;     // 獲取打開狀態的圖示 (用於資料夾)        
        public const uint SHGFI_SYSICONINDEX = 0x4000;     // 獲取系統圖示索引

        // 引入 shell32.dll 中的 SHGetFileInfo 函數
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeStruct, uint uFlags);

        // 引入 user32.dll 中的 DestroyIcon 函數 (用於釋放圖示資源)
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        // 引入 comctl32.dll 中的 ImageList_GetIcon 函數，用於從系統圖示表提取圖示
        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

        // 引入 shell32.dll 中的 SHGetImageList 函數，用於獲取系統圖示表
        [DllImport("shell32.dll", SetLastError = true)]
        public static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

        // 定義系統圖示表類型
        public const int SHIL_JUMBO = 0x4; // 大圖示 (256x256)
        public const int SHIL_EXTRALARGE = 0x2; // 超大圖示 (48x48)
        public const int SHIL_LARGE = 0x0; // 大圖示 (32x32)
        public const int SHIL_SMALL = 0x1; // 小圖示 (16x16)

        // static readonly，因為 IID_IImageList 不應改變
        private static readonly Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

        // 從系統圖示表提取圖示
        private static BitmapSource? GetIconFromSystemImageList(int iIcon, bool smallIcon, string filePath)
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to retrieve icon from system image list for iIcon {iIcon}.");
            IntPtr hImageList = IntPtr.Zero;
            try
            {
                int imageListType = smallIcon ? SHIL_SMALL : SHIL_LARGE;
                IntPtr ppv;
                // 使用臨時 Guid 變數，避免傳遞 readonly 欄位作為 ref
                Guid riid = IID_IImageList;
                int result = SHGetImageList(imageListType, ref riid, out ppv);
                if (result == 0 && ppv != IntPtr.Zero)
                {
                    hImageList = ppv;
                    IntPtr hIcon = ImageList_GetIcon(hImageList, iIcon, 0);
                    if (hIcon != IntPtr.Zero)
                    {
                        try
                        {
                            BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                                hIcon,
                                System.Windows.Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions()
                            );
                            bs.Freeze();
                            return bs;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error converting HICON to BitmapSource for {filePath}: {ex.Message}");
                            return null;
                        }
                        finally
                        {
                            DestroyIcon(hIcon);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"ImageList_GetIcon failed for iIcon {iIcon}, error code: {Marshal.GetLastWin32Error()}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"SHGetImageList failed for {filePath}, HRESULT: {result}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error retrieving icon from system image list for {filePath}: {ex.Message}");
            }
            finally
            {
                // ADDED: 釋放圖示表資源
                if (hImageList != IntPtr.Zero)
                {
                    Marshal.Release(hImageList);
                }
            }
            return null;
        }

        /// <summary>
        /// 根據檔案或資料夾路徑獲取其系統圖示。
        /// </summary>
        /// <param name="filePath">檔案或資料夾的完整路徑。</param>
        /// <param name="smallIcon">如果為 true，返回 16x16 的小圖示；否則返回 32x32 的大圖示。</param>
        /// <returns>轉換為 BitmapSource 的圖示，如果獲取失敗則為 null。</returns>
        public static BitmapSource? GetFileIcon(string filePath, bool smallIcon = true)
        {
            // 驗證輸入路徑是否有效
            if (string.IsNullOrWhiteSpace(filePath))
            {
                System.Diagnostics.Debug.WriteLine("GetFileIcon: filePath is null or empty.");
                return null;
            }

            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SYSICONINDEX; // 總是獲取圖示
            if (smallIcon)
                flags |= SHGFI_SMALLICON;
            else
                flags |= SHGFI_LARGEICON;

            uint fileAttributes = 0;

            // 判斷路徑類型，並設定相應的旗標和屬性
            if (Directory.Exists(filePath))
            {
                fileAttributes = (uint)FileAttributes.Directory; // 資料夾屬性
                flags |= SHGFI_OPENICON; // 資料夾獲取打開狀態的圖示
            }
            else if (File.Exists(filePath))
            {
                fileAttributes = (uint)FileAttributes.Normal; // 普通檔案屬性
            }
            else
            {
                // 對於不存在的路徑，我們需要使用 USEFILEATTRIBUTES 旗標
                flags |= SHGFI_USEFILEATTRIBUTES;
                // 並且將 fileAttributes 設定為 Normal，讓 Shell 自己判斷其圖示。
                // 例如，對於 ".txt" 的擴展名，即使檔案不存在，也能獲取到文本文檔圖示。
                // 對於磁碟機根路徑 "C:\", Windows Shell 會正確識別並返回磁碟機圖示。
                fileAttributes = (uint)FileAttributes.Normal;
            }

            // 呼叫 SHGetFileInfo 獲取圖示 Handle
            IntPtr hIcon = IntPtr.Zero;
            try
            {
                hIcon = SHGetFileInfo(
                    filePath,
                    fileAttributes,
                    ref shfi,
                    (uint)Marshal.SizeOf(shfi),
                    flags
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calling SHGetFileInfo for {filePath}: {ex.Message}");
                return null;
            }


            if (hIcon != IntPtr.Zero) // 如果成功獲取到圖示 Handle
            {
                try
                {
                    BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                        shfi.hIcon,
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    );
                    bs.Freeze();
                    return bs;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error converting HICON to BitmapSource for {filePath}: {ex.Message}");

                    if (shfi.hIcon == IntPtr.Zero)
                    {
                        // 當 hIcon 為 0 時，檢查 iIcon 是否有值（例如 iIcon == 3）
                        System.Diagnostics.Debug.WriteLine($"SHGetFileInfo failed for {filePath}, hIcon={shfi.hIcon}, iIcon={shfi.iIcon}.");
                        if (shfi.iIcon != 0)
                        {
                            // 從系統圖示表提取圖示
                            return GetIconFromSystemImageList(shfi.iIcon, smallIcon, filePath);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"iIcon is 0 for {filePath}, no icon available.");
                        }
                        return null;
                    }
                }
                finally
                {
                    DestroyIcon(shfi.hIcon);
                }
            }
            else
            {
                // SHGetFileInfo 失敗通常是因為路徑無效、權限問題或找不到圖示
                Debug.WriteLine($"Failed to get icon for {filePath}. SHGetFileInfo returned IntPtr.Zero.");
            }
            return null; // 獲取失敗
        }
    }
}
