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
        /// <summary>
        /// 靜態類別，用於宣告從 Windows API 導入的函式。
        /// </summary>
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int StrCmpLogicalW(string psz1, string psz2);

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
    }
}