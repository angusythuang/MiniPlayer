using System.Runtime.CompilerServices;
using System.IO;

namespace MiniPlayer
{
    public static class DebugInfo
    {
        public static string Current(
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string func = "")
        {
            return $"[{Path.GetFileName(file)}:{line} - {func}]";
        }

        public static void PrintDebugMsg(string message)
        {
#if DEBUG
            // 使用 Debug.WriteLine 來輸出訊息
            System.Diagnostics.Debug.WriteLine($"{message}");
#endif
        }
    }
}
