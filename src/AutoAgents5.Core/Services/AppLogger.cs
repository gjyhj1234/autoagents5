using System.Text;
using AutoAgents5.Core.Models;

namespace AutoAgents5.Core.Services;

public enum LogLevel { Trace, Debug, Info, Warn, Error }

/// <summary>
/// Structured logger following R-Logging rules.
/// Format: [yyyy-MM-dd HH:mm:ss.fff] [LEVEL] [STATE] message
/// </summary>
public class AppLogger
{
    private readonly string _logDirectory;
    private readonly object _fileLock = new();

    /// <summary>Raised on the calling thread; consumer should marshal to UI thread.</summary>
    public event Action<string, LogLevel>? OnLog;

    public AppLogger(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public void Trace(OrchestratorState state, string message) =>
        Log(LogLevel.Trace, state, message);

    public void Debug(OrchestratorState state, string message) =>
        Log(LogLevel.Debug, state, message);

    public void Info(OrchestratorState state, string message) =>
        Log(LogLevel.Info, state, message);

    public void Warn(OrchestratorState state, string message, Exception? ex = null) =>
        Log(LogLevel.Warn, state, message, ex);

    public void Error(OrchestratorState state, string message, Exception? ex = null, string? currentUrl = null) =>
        Log(LogLevel.Error, state, message, ex, currentUrl);

    private void Log(LogLevel level, OrchestratorState state, string message,
                     Exception? ex = null, string? currentUrl = null)
    {
        var now = DateTime.Now;
        var sb = new StringBuilder();
        sb.Append($"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToString().ToUpper()}] [{state}] {message}");

        if (level == LogLevel.Error)
        {
            if (currentUrl != null)
            {
                sb.AppendLine();
                sb.Append($"  URL: {currentUrl}");
            }
            if (ex != null)
            {
                sb.AppendLine();
                sb.Append($"  {ex}");
            }
        }
        else if (ex != null && level == LogLevel.Warn)
        {
            sb.AppendLine();
            sb.Append($"  {ex.Message}");
        }

        var line = sb.ToString();
        WriteToFile(now, line);
        OnLog?.Invoke(line, level);
    }

    private void WriteToFile(DateTime timestamp, string line)
    {
        var filePath = Path.Combine(_logDirectory, $"run-{timestamp:yyyyMMdd}.log");
        lock (_fileLock)
        {
            try
            {
                File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Best-effort file logging; do not crash the app on log failure.
            }
        }
    }
}
