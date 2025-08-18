using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace MiniPlayer
{
    class FileOperationHelper
    {
        // --- IFileOperation COM 介面 ---
        [ComImport]
        [Guid("947AAB5F-0A5C-4C13-B4D6-4EB6103F4A1C")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileOperation
        {
            void Advise(); // 不用
            void Unadvise();
            void SetOperationFlags(FileOperationFlags operationFlags);
            void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
            void SetProgressDialog([MarshalAs(UnmanagedType.IUnknown)] object progressDialog);

            void DeleteItem(IShellItem psiItem, IntPtr punkProgressSink);
            void CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder,
                          [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr punkProgressSink);
            void MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder,
                          [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr punkProgressSink);

            void PerformOperations();
            void GetAnyOperationsAborted(out bool pfAnyOperationsAborted);
        }

        [Flags]
        enum FileOperationFlags : uint
        {
            FOF_ALLOWUNDO = 0x40,              // 資源回收桶
            FOFX_SHOWELEVATIONPROMPT = 0x20000000 // UAC 提示
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem { }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IBindCtx pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IShellItem ppv
        );

        // 建立 ShellItem
        private static IShellItem CreateShellItem(string path)
        {
            Guid shellItemGuid = typeof(IShellItem).GUID;
            SHCreateItemFromParsingName(path, null, shellItemGuid, out IShellItem shellItem);
            return shellItem;
        }

        // 建立 IFileOperation 實例
        private static IFileOperation CreateFileOperation()
        {
            Type type = Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09"));
            var fo = (IFileOperation)Activator.CreateInstance(type);

            // 設定成允許 UAC
            fo.SetOperationFlags(FileOperationFlags.FOFX_SHOWELEVATIONPROMPT);

            return fo;
        }

        // --- 刪除 ---
        public static void Delete(string path)
        {
            var fo = CreateFileOperation();
            IShellItem item = CreateShellItem(path);

            fo.DeleteItem(item, IntPtr.Zero);
            fo.PerformOperations();
            fo.GetAnyOperationsAborted(out bool aborted);

            Console.WriteLine(aborted ? "刪除被取消。" : "刪除完成。");
        }

        // --- 複製 ---
        public static void Copy(string source, string destinationFolder, string? newName = null)
        {
            var fo = CreateFileOperation();
            IShellItem srcItem = CreateShellItem(source);
            IShellItem destItem = CreateShellItem(destinationFolder);

            fo.CopyItem(srcItem, destItem, newName, IntPtr.Zero);
            fo.PerformOperations();
            fo.GetAnyOperationsAborted(out bool aborted);

            Console.WriteLine(aborted ? "複製被取消。" : "複製完成。");
        }

        // --- 移動 ---
        public static void Move(string source, string destinationFolder, string? newName = null)
        {
            var fo = CreateFileOperation();
            IShellItem srcItem = CreateShellItem(source);
            IShellItem destItem = CreateShellItem(destinationFolder);

            fo.MoveItem(srcItem, destItem, newName, IntPtr.Zero);
            fo.PerformOperations();
            fo.GetAnyOperationsAborted(out bool aborted);

            Console.WriteLine(aborted ? "搬移被取消。" : "搬移完成。");
        }
    }
}