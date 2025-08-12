using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;


namespace MiniPlayer.FileBrowser
{
    public static class ShortcutHelper
    {
        public class ShortcutResult
        {
            public string? TargetPath { get; init; }
            public bool IsDirectory { get; init; }
            public bool Success { get; init; }
            public string? ErrorMessage { get; init; }
        }

        public static ShortcutResult GetShortcutInfo(string lnkPath)
        {
            try
            {
                var link = (IShellLinkW)new CShellLink();
                ((IPersistFile)link).Load(lnkPath, 0);

                var sb = new StringBuilder(32767);
                var data = new WIN32_FIND_DATAW();
                link.GetPath(sb, sb.Capacity, ref data, 0);

                string target = sb.ToString();
                if (string.IsNullOrWhiteSpace(target))
                    return new ShortcutResult
                    {
                        Success = false,
                        ErrorMessage = "目標路徑為空白"
                    };

                string normalized = EnsureLongPathPrefix(target);

                try
                {
                    var attr = File.GetAttributes(normalized);
                    bool isDir = (attr & FileAttributes.Directory) != 0;
                    return new ShortcutResult
                    {
                        Success = true,
                        TargetPath = target,
                        IsDirectory = isDir,
                        ErrorMessage = null
                    };
                }
                catch (FileNotFoundException)
                {
                    return new ShortcutResult
                    {
                        Success = false,
                        ErrorMessage = "目標檔案不存在"
                    };
                }
                catch (UnauthorizedAccessException)
                {
                    return new ShortcutResult
                    {
                        Success = false,
                        ErrorMessage = "權限不足，無法存取目標路徑"
                    };
                }
                catch (PathTooLongException)
                {
                    return new ShortcutResult
                    {
                        Success = false,
                        ErrorMessage = "目標路徑過長"
                    };
                }
                catch (Exception ex)
                {
                    return new ShortcutResult
                    {
                        Success = false,
                        ErrorMessage = $"其他錯誤: {ex.Message}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ShortcutResult
                {
                    Success = false,
                    ErrorMessage = $"無法解析 .lnk 檔案: {ex.Message}"
                };
            }
        }

        private static string EnsureLongPathPrefix(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (path.StartsWith(@"\\?\"))
                return path;

            if (path.StartsWith(@"\\"))
                return @"\\?\UNC\" + path.Substring(2);

            return @"\\?\" + path;
        }

        #region Interop 宣告
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_DATAW
        {
            public FileAttributes dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch,
                         ref WIN32_FIND_DATAW pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
                                 int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class CShellLink
        {
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }
        #endregion
    }
}
