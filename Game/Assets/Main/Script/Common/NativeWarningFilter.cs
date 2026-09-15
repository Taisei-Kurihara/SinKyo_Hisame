using UnityEngine;

namespace Common
{
    /// <summary>
    /// Unityネイティブ側から出る既知の無害な警告をコンソールに表示しないようフィルタする.
    /// RuntimeInitializeOnLoadMethod により、シーンロード前に自動で適用される.
    /// </summary>
    public static class NativeWarningFilter
    {
        // フィルタ対象の部分文字列リスト.
        private static readonly string[] suppressPatterns = new[]
        {
            "Color primaries",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            Debug.unityLogger.logHandler = new FilteredLogHandler(Debug.unityLogger.logHandler);
        }

        private class FilteredLogHandler : ILogHandler
        {
            private readonly ILogHandler defaultHandler;

            public FilteredLogHandler(ILogHandler defaultHandler)
            {
                this.defaultHandler = defaultHandler;
            }

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                // Warning のみフィルタ対象（Error / Log はそのまま通す）.
                if (logType == LogType.Warning)
                {
                    string message = (args != null && args.Length > 0)
                        ? string.Format(format, args)
                        : format;

                    for (int i = 0; i < suppressPatterns.Length; i++)
                    {
                        if (message.Contains(suppressPatterns[i]))
                        {
                            return; // 抑制.
                        }
                    }
                }

                defaultHandler.LogFormat(logType, context, format, args);
            }

            public void LogException(System.Exception exception, Object context)
            {
                defaultHandler.LogException(exception, context);
            }
        }
    }
}
