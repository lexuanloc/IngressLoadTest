using System.Text;

namespace IngressLoadTest;

public static class ErrorLogger
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, ErrorState> ErrorStates = new();

    private static readonly TimeSpan DuplicateInterval = TimeSpan.FromSeconds(10);
    private static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "log.txt");

    public static string FilePath => LogFilePath;

    public static void Initialize()
    {
        try
        {
            WriteRaw(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                $"IngressLoadTest started. PID={Environment.ProcessId}{Environment.NewLine}",
                flushToDisk: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Không thể ghi log.txt: {ex.Message}");
        }
    }

    public static void LogException(string context, Exception exception, bool force = false)
    {
        try
        {
            DateTime now = DateTime.Now;
            string key = BuildKey(context, exception);

            lock (SyncRoot)
            {
                int suppressedCount = 0;

                if (!force && ErrorStates.TryGetValue(key, out ErrorState? state))
                {
                    if (now - state.LastWritten < DuplicateInterval)
                    {
                        state.SuppressedCount++;
                        return;
                    }

                    suppressedCount = state.SuppressedCount;
                    state.LastWritten = now;
                    state.SuppressedCount = 0;
                }
                else
                {
                    ErrorStates[key] = new ErrorState { LastWritten = now };
                }

                var sb = new StringBuilder();

                sb.AppendLine("============================================================");
                sb.AppendLine($"Time       : {now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"PID        : {Environment.ProcessId}");
                sb.AppendLine($"Context    : {context}");

                if (suppressedCount > 0)
                {
                    sb.AppendLine($"Suppressed : {suppressedCount:N0} similar errors");
                }

                AppendException(sb, exception, 0);
                sb.AppendLine();

                WriteRaw(sb.ToString(), flushToDisk: force);
            }
        }
        catch
        {
            // Logger không được làm crash chương trình.
        }
    }

    public static void LogFatal(string context, Exception exception, bool isTerminating)
    {
        try
        {
            var sb = new StringBuilder();

            sb.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!! FATAL !!!!!!!!!!!!!!!!!!!!!!!!");
            sb.AppendLine($"Time        : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"PID         : {Environment.ProcessId}");
            sb.AppendLine($"Context     : {context}");
            sb.AppendLine($"Terminating : {isTerminating}");

            AppendException(sb, exception, 0);

            sb.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            sb.AppendLine();

            lock (SyncRoot)
            {
                WriteRaw(sb.ToString(), flushToDisk: true);
            }
        }
        catch
        {
        }
    }

    public static void LogMessage(string message, bool flushToDisk = false)
    {
        try
        {
            string text =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                $"{message}{Environment.NewLine}";

            lock (SyncRoot)
            {
                WriteRaw(text, flushToDisk);
            }
        }
        catch
        {
        }
    }

    public static void LogRateLimited(string key, string message)
    {
        try
        {
            DateTime now = DateTime.Now;

            lock (SyncRoot)
            {
                int suppressedCount = 0;

                if (ErrorStates.TryGetValue(key, out ErrorState? state))
                {
                    if (now - state.LastWritten < DuplicateInterval)
                    {
                        state.SuppressedCount++;
                        return;
                    }

                    suppressedCount = state.SuppressedCount;
                    state.LastWritten = now;
                    state.SuppressedCount = 0;
                }
                else
                {
                    ErrorStates[key] = new ErrorState { LastWritten = now };
                }

                string suffix =
                    suppressedCount > 0
                        ? $" (suppressed {suppressedCount:N0} similar events)"
                        : string.Empty;

                string text =
                    $"[{now:yyyy-MM-dd HH:mm:ss.fff}] " +
                    $"{message}{suffix}{Environment.NewLine}";

                WriteRaw(text, flushToDisk: false);
            }
        }
        catch
        {
        }
    }

    private static string BuildKey(string context, Exception exception)
    {
        return $"{context}|{exception.GetType().FullName}|{exception.Message}";
    }

    private static void AppendException(StringBuilder sb, Exception exception, int level)
    {
        string prefix = level == 0 ? string.Empty : $"Inner[{level}] ";

        sb.AppendLine($"{prefix}Type    : {exception.GetType().FullName}");
        sb.AppendLine($"{prefix}Message : {exception.Message}");

        if (!string.IsNullOrWhiteSpace(exception.StackTrace))
        {
            sb.AppendLine($"{prefix}Stack:");
            sb.AppendLine(exception.StackTrace);
        }

        if (exception.InnerException != null)
        {
            AppendException(sb, exception.InnerException, level + 1);
        }
    }

    private static void WriteRaw(string text, bool flushToDisk)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);

        using var stream = new FileStream(
            LogFilePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite,
            4096,
            FileOptions.SequentialScan);

        stream.Write(data, 0, data.Length);
        stream.Flush(flushToDisk);
    }

    private sealed class ErrorState
    {
        public DateTime LastWritten { get; set; }
        public int SuppressedCount { get; set; }
    }
}
