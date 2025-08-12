using System.Collections;     // 引用 System.Collections 以使用 IComparer
using System.Runtime.InteropServices; // **** 新增：引用 System.Runtime.InteropServices 以使用 DllImport ****

namespace MiniPlayer
{
    /// <summary>
    /// 為 FileSystemItem 集合提供自訂排序邏輯。
    /// 先按目錄/檔案排序 (目錄在前)，然後所有項目都依據 StrCmpLogicalW 函式進行自然排序。
    /// </summary>
    public class CustomFileSystemItemComparer : IComparer
    {
        // Singleton instance，便於在應用程式中重複使用。
        public static readonly CustomFileSystemItemComparer Instance = new CustomFileSystemItemComparer();

        /// <summary>
        /// 靜態類別，用於宣告從 Windows API 導入的函式。
        /// </summary>
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int StrCmpLogicalW(string psz1, string psz2);

        // 私有建構子，防止外部 new
        private CustomFileSystemItemComparer() { }

        public int Compare(object? x, object? y)
        {
            if (x is FileSystemItem item1 && y is FileSystemItem item2)
            {
                if (item1.IsDirectory != item2.IsDirectory)
                {
                    return item1.IsDirectory ? -1 : 1;
                }
                // 處理 null 名稱
                string name1 = item1.Name ?? string.Empty;
                string name2 = item2.Name ?? string.Empty;
                return StrCmpLogicalW(name1, name2);
            }
            return 0;
        }

        // <summary>
        /// 為頂層磁碟機提供按磁碟機字母順序的排序邏輯（例如，C:\, D:\, E:\）。
        /// </summary>
        public class DriveLetterComparer : IComparer
        {
            // Singleton instance，便於在應用程式中重複使用。
            public static readonly DriveLetterComparer Instance = new DriveLetterComparer();

            // 私有建構子，防止外部 new
            private DriveLetterComparer() { }

            public int Compare(object? x, object? y)
            {
                if (x is FileSystemItem item1 && y is FileSystemItem item2)
                {
                    // 按 FullPath 的磁碟機字母進行簡單字母順序比較
                    string drive1 = item1.FullPath ?? string.Empty;
                    string drive2 = item2.FullPath ?? string.Empty;
                    return string.Compare(drive1, drive2, StringComparison.OrdinalIgnoreCase);
                }
                return 0;
            }
        }
    }
}