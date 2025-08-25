using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MiniPlayer
{
    class FileOperationHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public FileFuncFlags wFunc;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pTo;
            public FileOpFlags fFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszProgressTitle;
        }

        private enum FileFuncFlags : uint
        {
            FO_MOVE = 0x0001,
            FO_COPY = 0x0002,
            FO_DELETE = 0x0003,
            FO_RENAME = 0x0004,
        }

        [Flags]
        private enum FileOpFlags : ushort
        {
            FOF_MULTIDESTFILES = 0x0001,
            FOF_CONFIRMMOUSE = 0x0002,
            FOF_SILENT = 0x0004,
            FOF_RENAMEONCOLLISION = 0x0008,
            FOF_NOCONFIRMATION = 0x0010,
            FOF_ALLOWUNDO = 0x0040,
            FOF_SIMPLEPROGRESS = 0x0100,
            FOF_NOCONFIRMMKDIR = 0x0200,
            FOF_NOERRORUI = 0x0400,
            FOF_WANTNUKEWARNING = 0x4000,
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        // 把多個路徑組合成 SHFileOperation 需要的字串（\0 分隔，最後再加 \0\0）
        private static string BuildMultiPath(IEnumerable<string> paths)
        {
            return string.Join("\0", paths) + "\0\0";
        }

        // --- 刪除 (多檔案) ---
        public static void Delete(IEnumerable<string> paths)
        {
            var shfo = new SHFILEOPSTRUCT
            {
                wFunc = FileFuncFlags.FO_DELETE,
                pFrom = BuildMultiPath(paths),
                fFlags = 0 // 會詢問、直接刪除、不進資源回收桶
            };

            int result = SHFileOperation(ref shfo);
            Console.WriteLine(shfo.fAnyOperationsAborted ? "刪除被取消。" :
                              result == 0 ? "刪除完成。" : $"刪除失敗，錯誤碼 {result}");
        }

        // --- 複製 (多檔案) ---
        public static void Copy(IEnumerable<string> sources, string destinationFolder)
        {
            var shfo = new SHFILEOPSTRUCT
            {
                wFunc = FileFuncFlags.FO_COPY,
                pFrom = BuildMultiPath(sources),
                pTo = destinationFolder + "\0\0",
                fFlags = FileOpFlags.FOF_NOCONFIRMMKDIR // 自動建資料夾，有重複檔名跳出詢問視窗
            };

            int result = SHFileOperation(ref shfo);
            Console.WriteLine(shfo.fAnyOperationsAborted ? "複製被取消。" :
                              result == 0 ? "複製完成。" : $"複製失敗，錯誤碼 {result}");
        }

        // --- 搬移 (多檔案) ---
        public static void Move(IEnumerable<string> sources, string destinationFolder)
        {
            var shfo = new SHFILEOPSTRUCT
            {
                wFunc = FileFuncFlags.FO_MOVE,
                pFrom = BuildMultiPath(sources),
                pTo = destinationFolder + "\0\0",
                fFlags = FileOpFlags.FOF_NOCONFIRMMKDIR // 自動建資料夾，有重複檔名跳出詢問視窗
            };

            int result = SHFileOperation(ref shfo);
            Console.WriteLine(shfo.fAnyOperationsAborted ? "搬移被取消。" :
                              result == 0 ? "搬移完成。" : $"搬移失敗，錯誤碼 {result}");
        }
    }
}
