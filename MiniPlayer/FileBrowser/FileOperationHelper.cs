using System;
using System.Collections.Generic;
using System.ComponentModel; // 引入此命名空間
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPlayer
{
    public class FileOperationHelper
    {
        // 確保 SHFileOperation 每次只被一個執行緒呼叫
        private static readonly SemaphoreSlim _shFileOperationSemaphore = new SemaphoreSlim(1, 1);

        // P/Invoke 宣告：從 shell32.dll 匯入 SHFileOperation 函數
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);

        private enum FileOperationType : uint
        {
            FO_COPY = 0x0001,
            FO_MOVE = 0x0002,
            FO_DELETE = 0x0003,
            FO_RENAME = 0x0004,
        }

        [Flags]
        private enum FileOperationFlags : ushort
        {
            FOF_ALLOWUNDO = 0x0040,
            FOF_WANTNUKEWARNING = 0x4000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public FileOperationType wFunc;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pTo;
            public FileOperationFlags fFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszProgressTitle;
        }

        private static string FormatPaths(List<FileSystemItem> items)
        {
            var sb = new StringBuilder();
            foreach (var i in items)
            {
                string path = i.FullPath;
                sb.Append(path.TrimEnd('\\', '/'));
                sb.Append('\0');
            }
            sb.Append('\0');
            return sb.ToString();
        }

        /// <summary>
        /// 私有方法，負責執行緒同步和實際的 SHFileOperation 呼叫，並檢查回傳值。
        /// </summary>
        /// <param name="fileOp">已建立好的 SHFILEOPSTRUCT 結構</param>
        private static async Task ExecuteFileOperationAsync(SHFILEOPSTRUCT fileOp)
        {
            await _shFileOperationSemaphore.WaitAsync();
            try
            {
                int result = SHFileOperation(ref fileOp);

                if (fileOp.fAnyOperationsAborted)
                {
                    // 如果使用者取消了操作
                    throw new OperationCanceledException("檔案操作被使用者取消。");
                }

                if (result != 0)
                {
                    // 使用 Win32Exception 類別將錯誤碼轉換為有意義的錯誤訊息
                    throw new Win32Exception(result, $"檔案操作失敗，Win32 錯誤碼: {result}");
                }
            }
            finally
            {
                _shFileOperationSemaphore.Release();
            }
        }

        /// <summary>
        /// 複製檔案或資料夾。如果目標存在，會自動彈出對話框詢問。
        /// </summary>
        public static async Task Copy(List<FileSystemItem> sources, FileSystemItem dest)
        {
            var formattedSources = FormatPaths(sources);
            var destination = dest.FullPath;
            var fileOp = new SHFILEOPSTRUCT
            {
                hwnd = IntPtr.Zero,
                wFunc = FileOperationType.FO_COPY,
                pFrom = formattedSources,
                pTo = destination.TrimEnd('\\', '/') + '\0',
                fFlags = 0
            };
            await ExecuteFileOperationAsync(fileOp);
        }

        /// <summary>
        /// 移動檔案或資料夾。如果目標存在，會自動彈出對話框詢問。
        /// </summary>
        public static async Task Move(List<FileSystemItem> sources, FileSystemItem dest)
        {
            var formattedSources = FormatPaths(sources);
            var destination = dest.FullPath;
            var fileOp = new SHFILEOPSTRUCT
            {
                hwnd = IntPtr.Zero,
                wFunc = FileOperationType.FO_MOVE,
                pFrom = formattedSources,
                pTo = destination.TrimEnd('\\', '/') + '\0',
                fFlags = 0
            };
            await ExecuteFileOperationAsync(fileOp);
        }

        /// <summary>
        /// 刪除檔案或資料夾。不使用資源回收筒，但會顯示確認視窗。
        /// </summary>
        public static async Task Delete(List<FileSystemItem> sources)
        {
            var formattedSources = FormatPaths(sources);
            var fileOp = new SHFILEOPSTRUCT
            {
                hwnd = IntPtr.Zero,
                wFunc = FileOperationType.FO_DELETE,
                pFrom = formattedSources,
                fFlags = FileOperationFlags.FOF_WANTNUKEWARNING
            };
            await ExecuteFileOperationAsync(fileOp);
        }
    }
}