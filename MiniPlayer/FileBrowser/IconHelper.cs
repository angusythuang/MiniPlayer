// IconHelper.cs
using System.Diagnostics;             // For Debug.WriteLine
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;         // For Imaging.CreateBitmapSourceFromHIcon
using System.Windows.Media.Imaging;   // For BitmapSource

namespace MiniPlayer
{
    public static class IconHelper
    {
        // 儲存 SHGetFileInfo 函數返回的資訊的結構
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] // 確保 CharSet 為 Auto
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

        // 引入 shell32.dll 中的 SHGetFileInfo 函數
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeStruct, uint uFlags);

        // 引入 user32.dll 中的 DestroyIcon 函數 (用於釋放圖示資源)
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>
        /// 根據檔案或資料夾路徑獲取其系統圖示。
        /// </summary>
        /// <param name="filePath">檔案或資料夾的完整路徑。</param>
        /// <param name="smallIcon">如果為 true，返回 16x16 的小圖示；否則返回 32x32 的大圖示。</param>
        /// <returns>轉換為 BitmapSource 的圖示，如果獲取失敗則為 null。</returns>
        public static BitmapSource? GetFileIcon(string filePath, bool smallIcon = true)
        {
            // 由於 Icon 可能是延遲載入的，我們應盡量避免在 UI 線程中阻塞。
            // 這裡的 SHGetFileInfo 是同步的，所以會阻塞 UI。
            // 對於大量檔案，考慮異步載入圖示。
            // 但對於本次需求，先確保功能正確。

            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON; // 總是獲取圖示

            if (smallIcon)
                flags |= SHGFI_SMALLICON;
            else
                flags |= SHGFI_LARGEICON;

            uint fileAttributes = 0; // 預設屬性為 0

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
            else // 路徑不存在（例如磁碟機根目錄 "C:\"，或不存在的檔案來獲取擴展名圖示）
            {
                // 對於不存在的路徑，我們需要使用 USEFILEATTRIBUTES 旗標
                flags |= SHGFI_USEFILEATTRIBUTES;
                // 並且將 fileAttributes 設定為 Normal，讓 Shell 自己判斷其圖示。
                // 例如，對於 ".txt" 的擴展名，即使檔案不存在，也能獲取到文本文檔圖示。
                // 對於磁碟機根路徑 "C:\", Windows Shell 會正確識別並返回驅動器圖示。
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
                Debug.WriteLine($"Error calling SHGetFileInfo for {filePath}: {ex.Message}");
                return null;
            }


            if (hIcon != IntPtr.Zero) // 如果成功獲取到圖示 Handle
            {
                try
                {
                    // 將 Windows 圖示 Handle (HICON) 轉換為 WPF 的 BitmapSource
                    BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                        shfi.hIcon,
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    );
                    bs.Freeze(); // 凍結 BitmapSource 以便在多執行緒環境中安全使用
                    return bs;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error converting HICON to BitmapSource for {filePath}: {ex.Message}");
                    return null;
                }
                finally
                {
                    // 釋放原生圖示資源
                    // 這是非常重要的一步，否則會導致資源洩漏
                    DestroyIcon(hIcon);
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