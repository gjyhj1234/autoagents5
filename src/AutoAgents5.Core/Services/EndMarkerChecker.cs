using AutoAgents5.Core.Models;

namespace AutoAgents5.Core.Services;

/// <summary>
/// Evaluates the end-marker in the last 3 non-empty lines (R-EndMarker).
/// </summary>
public static class EndMarkerChecker
{
    private const string AllDoneMarker = "===任务全部完成===";
    private const string PartialMarker = "===部分任务未完成===";
    private const string ErrorMarker = "===任务执行出现错误===";

    /// <summary>
    /// Evaluates a list of the last N lines (typically 3) from the last assistant message.
    /// Returns the result based on the LAST matching line (to resolve conflicts).
    /// </summary>
    public static EndMarkerResult Evaluate(IReadOnlyList<string> tailLines)
    {
        EndMarkerResult result = EndMarkerResult.NotFound;
        foreach (var line in tailLines)
        {
            if (line.Contains(AllDoneMarker)) result = EndMarkerResult.AllDone;
            else if (line.Contains(PartialMarker) || line.Contains(ErrorMarker))
                result = EndMarkerResult.Continue;
        }
        return result;
    }
}
