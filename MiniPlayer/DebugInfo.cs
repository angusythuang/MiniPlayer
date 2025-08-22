using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

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

        /// <summary>
        /// 只有 DEBUG 模式下才會被編譯，RELEASE版不會有這個呼叫。
        /// </summary>
        /// <param name="message"></param>
        [Conditional("DEBUG")]
        public static void PrintDebugMsg(string message)
        {
            // 使用 Debug.WriteLine 來輸出訊息
            System.Diagnostics.Debug.WriteLine($"{message}");
        }
    }
}
