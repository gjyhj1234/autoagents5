namespace AutoAgents5.Core.Models;

/// <summary>
/// All states in the task orchestration state machine (R-State-Machine).
/// </summary>
public enum OrchestratorState
{
    Idle,
    AwaitQueued,
    AwaitInProgress,
    AwaitIdle,
    InspectResult,
    EndMarkerCheck,
    SendContinue,
    HandleFailed,
    HandleTimedOut,
    HandleWaitingUser,
    HandleCancelled,
    CreatePR,
    MarkReady,
    Merge,
    Done,
    Halt
}
