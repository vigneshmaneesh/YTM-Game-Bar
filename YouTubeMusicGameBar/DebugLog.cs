using System.Diagnostics;

namespace YouTubeMusicGameBar
{
    internal static class DebugLog
    {
        [Conditional("DEBUG")]
        internal static void Write(string message)
        {
            Debug.WriteLine("[YouTube Music Game Bar] " + message);
        }

        [Conditional("DEBUG")]
        internal static void Write(string format, params object[] args)
        {
            Debug.WriteLine("[YouTube Music Game Bar] " + string.Format(format, args));
        }
    }
}
