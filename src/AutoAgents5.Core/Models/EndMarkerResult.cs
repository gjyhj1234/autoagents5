namespace AutoAgents5.Core.Models;

/// <summary>
/// Result of checking the end-marker in the last assistant message (R-EndMarker).
/// </summary>
public enum EndMarkerResult
{
    /// <summary>===任务全部完成=== found → proceed to CreatePR.</summary>
    AllDone,

    /// <summary>===部分任务未完成=== or ===任务执行出现错误=== found → SendContinue.</summary>
    Continue,

    /// <summary>No recognized marker found → Halt for human intervention.</summary>
    NotFound
}
