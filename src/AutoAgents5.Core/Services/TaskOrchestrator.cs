using System.Text.Json;
using AutoAgents5.Core.Models;

namespace AutoAgents5.Core.Services;

/// <summary>
/// Single-threaded task orchestrator implementing R-State-Machine + R-Polling.
/// Must be driven from the UI thread (all async continuations are marshalled via SynchronizationContext).
/// </summary>
public class TaskOrchestrator
{
    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly IBrowserBridge _browser;
    private readonly AppLogger _logger;
    private readonly AppSettings _settings;

    // ── State ─────────────────────────────────────────────────────────────────
    private OrchestratorState _state = OrchestratorState.Idle;
    private CancellationTokenSource? _cts;

    // Current task context
    private TaskInfo? _currentTask;
    private string _currentTaskFile = string.Empty;
    private int _continueCount = 0;
    private bool _diffHasSessionFile = false;

    // XHR capture channels (set by state handlers)
    private TaskCompletionSource<List<TaskInfo>>? _pendingTasksXhr;
    private TaskCompletionSource<DiffResult?>? _pendingDiffXhr;
    private string _expectedTaskId = string.Empty;

    // URL matcher (re-created when settings change)
    private UrlMatcher? _urlMatcher;

    public event Action<OrchestratorState>? StateChanged;
    public event Action? HaltRequested;

    // ── Constants (R-Polling) ─────────────────────────────────────────────────
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan QueuedIdleTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan InspectTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan EndMarkerTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan CreatePrTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CreatePrReloadAfter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MergeButtonTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MergePollInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(5);

    public TaskOrchestrator(IBrowserBridge browser, AppLogger logger, AppSettings settings)
    {
        _browser = browser;
        _logger = logger;
        _settings = settings;

        _browser.XhrReceived += OnXhrReceived;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public OrchestratorState CurrentState => _state;

    /// <summary>
    /// Start orchestration for the given task file.
    /// Resets state machine and begins the loop.
    /// </summary>
    public async Task StartTaskAsync(string taskFileName, CancellationToken externalCt)
    {
        _urlMatcher = new UrlMatcher(_settings.Owner, _settings.Repo);
        _currentTaskFile = taskFileName;
        _continueCount = 0;
        _diffHasSessionFile = false;
        _currentTask = null;
        _expectedTaskId = string.Empty;

        await TransitionTo(OrchestratorState.Idle);

        // Navigate to agents page
        var agentsUrl = $"https://github.com/copilot/agents?author={_settings.Owner}";
        _logger.Info(OrchestratorState.Idle, $"[TASK-START] 文件={taskFileName}，role={_settings.Role}");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        _cts = linked;

        try
        {
            await RunMainLoopAsync(agentsUrl, linked.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.Info(_state, "任务被用户取消");
        }
        catch (Exception ex)
        {
            await Halt($"未预期异常: {ex.Message}", ex);
        }
    }

    /// <summary>Stop orchestration immediately.</summary>
    public void Stop()
    {
        _cts?.Cancel();
    }

    // ── Main Loop ─────────────────────────────────────────────────────────────

    private async Task RunMainLoopAsync(string agentsUrl, CancellationToken ct)
    {
        await _browser.NavigateAsync(agentsUrl, ct);

        // Check for active tasks first
        var existingTasks = await WaitForTasksXhrAsync(ct, timeoutSeconds: 30);
        if (existingTasks != null)
        {
            var active = existingTasks.Where(t =>
                t.State is "queued" or "in_progress" or "idle" or "waiting_for_user").ToList();
            if (active.Count > 0)
            {
                await Halt($"已有进行中的任务 (state={active[0].State})，无法开始新任务，请等待完成后再试");
                return;
            }
        }

        // Submit task
        if (!await SubmitTaskAsync(ct)) return;

        // Wait for new task to appear in XHR and enter state machine
        var newTasks = await WaitForTasksXhrAsync(ct, timeoutSeconds: 30);
        var task = newTasks?.FirstOrDefault(t => t.State is "queued" or "in_progress");
        if (task == null)
        {
            await Halt("提交任务后未能在 XHR 中找到新任务");
            return;
        }

        _currentTask = task;
        _expectedTaskId = task.TaskId;
        await DispatchTaskState(task.State, ct);
    }

    // ── Task Submission ───────────────────────────────────────────────────────

    private async Task<bool> SubmitTaskAsync(CancellationToken ct)
    {
        // Select model
        var model = _settings.Role.ToLower() == "pm" ? "Claude Opus 4.7" : "Claude Sonnet 4.6";
        _logger.Info(OrchestratorState.Idle, $"选择模型: {model}");
        var modelScript = BrowserScripts.ClickButton(model);
        var modelResult = await _browser.ExecuteScriptAsync(modelScript);
        if (modelResult != "true")
        {
            await Halt($"找不到模型选项 '{model}'，请检查页面是否有该模型");
            return false;
        }

        // Type task description
        var taskText = $"执行.ai/tasks/{_settings.Role}/ready/{_currentTaskFile}任务";
        _logger.Info(OrchestratorState.Idle, $"输入任务: {taskText}");
        var typeResult = await _browser.ExecuteScriptAsync(
            BrowserScripts.TypeAndSubmitChatForm(taskText));

        // For initial submission, find textarea outside the task form
        if (typeResult != "true")
        {
            // Try generic session textarea
            var serializedText = JsonSerializer.Serialize(taskText);
            var genericType = "(function() {" +
                "const ta = document.querySelector('textarea');" +
                "if (!ta || ta.disabled) return false;" +
                "const desc = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value');" +
                $"if (desc && desc.set) {{ desc.set.call(ta, {serializedText}); }} else {{ ta.value = {serializedText}; }}" +
                "ta.dispatchEvent(new Event('input', { bubbles: true }));" +
                "ta.dispatchEvent(new Event('change', { bubbles: true }));" +
                "return true;" +
                "})();";
            await _browser.ExecuteScriptAsync(genericType);
        }

        // Click Start task
        _logger.Info(OrchestratorState.Idle, "[CLICK] Start task");
        var startResult = await _browser.ExecuteScriptAsync(BrowserScripts.ClickButton("Start task"));
        if (startResult != "true")
        {
            await Halt("未找到 'Start task' 按钮，请检查页面状态");
            return false;
        }
        return true;
    }

    // ── State Dispatch ────────────────────────────────────────────────────────

    private async Task DispatchTaskState(string stateStr, CancellationToken ct)
    {
        switch (stateStr.ToLower())
        {
            case "queued": await RunAwaitQueued(ct); break;
            case "in_progress": await RunAwaitInProgress(ct); break;
            case "completed": await RunInspectResult(ct); break;
            case "failed": await RunHandleFailed(); break;
            case "idle": await RunAwaitIdle(ct); break;
            case "waiting_for_user": await RunHandleWaitingUser(); break;
            case "timed_out": await RunHandleTimedOut(ct); break;
            case "cancelled": await RunHandleCancelled(); break;
            default:
                _logger.Warn(_state, $"未知 state: {stateStr}");
                await RunAwaitInProgress(ct);
                break;
        }
    }

    // ── State Handlers ────────────────────────────────────────────────────────

    private async Task RunAwaitQueued(CancellationToken ct)
    {
        await TransitionTo(OrchestratorState.AwaitQueued);
        await PollUntilStateChanges(ct, QueuedIdleTimeout, "queued");
    }

    private async Task RunAwaitIdle(CancellationToken ct)
    {
        await TransitionTo(OrchestratorState.AwaitIdle);
        await PollUntilStateChanges(ct, QueuedIdleTimeout, "idle");
    }

    private async Task RunAwaitInProgress(CancellationToken ct)
    {
        await TransitionTo(OrchestratorState.AwaitInProgress);
        var started = DateTime.UtcNow;
        var lastHeartbeat = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            // Heartbeat
            if (DateTime.UtcNow - lastHeartbeat >= HeartbeatInterval)
            {
                var elapsed = (int)(DateTime.UtcNow - started).TotalMinutes;
                _logger.Info(OrchestratorState.AwaitInProgress,
                    $"[HEARTBEAT] AwaitInProgress 已运行 {elapsed} min，taskId={_currentTask?.TaskId}");
                lastHeartbeat = DateTime.UtcNow;
            }

            // Reload and check
            await ReloadAndWaitAsync(ct);
            var tasks = await WaitForTasksXhrAsync(ct, timeoutSeconds: 35);
            if (tasks == null) continue;

            var current = tasks.FirstOrDefault(t => t.TaskId == _expectedTaskId)
                          ?? tasks.FirstOrDefault();
            if (current == null) continue;

            _currentTask = current;
            if (current.State != "in_progress")
            {
                _logger.Info(OrchestratorState.AwaitInProgress,
                    $"[STATE→DispatchTaskState] state 变为 {current.State}");
                await DispatchTaskState(current.State, ct);
                return;
            }

            await Task.Delay(PollInterval, ct);
        }
    }

    private async Task PollUntilStateChanges(CancellationToken ct, TimeSpan timeout, string currentStateStr)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            await ReloadAndWaitAsync(ct);
            var tasks = await WaitForTasksXhrAsync(ct, timeoutSeconds: 35);
            if (tasks != null)
            {
                var current = tasks.FirstOrDefault(t => t.TaskId == _expectedTaskId)
                              ?? tasks.FirstOrDefault();
                if (current != null && current.State != currentStateStr)
                {
                    _currentTask = current;
                    _logger.Info(_state,
                        $"[STATE→DispatchTaskState] state 变为 {current.State}");
                    await DispatchTaskState(current.State, ct);
                    return;
                }
            }
            await Task.Delay(PollInterval, ct);
        }

        if (!ct.IsCancellationRequested)
            await Halt($"[HALT] 任务 {currentStateStr} 状态超过 {timeout.TotalMinutes} 分钟未改变，请人工干预");
    }

    private async Task RunInspectResult(CancellationToken ct)
    {
        await TransitionTo(OrchestratorState.InspectResult);
        if (_currentTask == null || string.IsNullOrEmpty(_currentTask.HtmlUrl))
        {
            await Halt("InspectResult: html_url 为空，无法打开详情页");
            return;
        }

        // Stop other polling — already handled by single CTS
        _logger.Info(OrchestratorState.InspectResult, $"打开任务详情: {_currentTask.HtmlUrl}");
        await _browser.NavigateAsync(_currentTask.HtmlUrl, ct);

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var diffResult = await WaitForDiffXhrAsync(ct, InspectTimeout);
            if (diffResult == null)
            {
                _logger.Warn(OrchestratorState.InspectResult, $"diff XHR 第 {attempt}/3 次未收到，重试");
                if (attempt < 3) { await Task.Delay(TimeSpan.FromSeconds(10), ct); continue; }
                await Halt("InspectResult: 3 次未收到 diff XHR");
                return;
            }

            var hasSession = diffResult.Files.Any(f => UrlMatcher.IsSessionFile(f.Path));
            _logger.Info(OrchestratorState.InspectResult,
                $"[XHR-HIT] diff，files={diffResult.Files.Count}，hasSessionMd={hasSession}");

            _diffHasSessionFile = hasSession;
            if (hasSession) { await RunEndMarkerCheck(ct); }
            else { await RunSendContinue(ct); }
            return;
        }
    }

    private async Task RunEndMarkerCheck(CancellationToken ct)
    {
        await TransitionTo(OrchestratorState.EndMarkerCheck);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(EndMarkerTimeout);

        try
        {
            await _browser.ExecuteScriptAsync(BrowserScripts.ScrollToBottomUntilStable);
            var tailJson = await _browser.ExecuteScriptAsync(BrowserScripts.GetLastAssistantMessageTail);

            // tailJson is a JSON array e.g. ["line1","line2","line3"]
            List<string> tail;
            try
            {
                tail = System.Text.Json.JsonSerializer.Deserialize<List<string>>(tailJson)
                       ?? new List<string>();
            }
            catch
            {
                tail = new List<string>();
            }

            _logger.Info(OrchestratorState.EndMarkerCheck,
                $"[R-EndMarker] 末尾行: {string.Join(" | ", tail)}");

            var result = EndMarkerChecker.Evaluate(tail);
            switch (result)
            {
                case EndMarkerResult.AllDone:
                    _logger.Info(OrchestratorState.EndMarkerCheck, "===任务全部完成=== 检测到，进入 CreatePR");
                    await RunCreatePR(ct);
                    break;
                case EndMarkerResult.Continue:
                    _logger.Info(OrchestratorState.EndMarkerCheck, "部分完成或出错，触发 SendContinue");
                    await RunSendContinue(ct);
                    break;
                case EndMarkerResult.NotFound:
                    await Halt("[HALT] EndMarkerCheck: 未找到结束标记，请人工干预");
                    break;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            await Halt("[HALT] EndMarkerCheck 超时 120s，请人工干预");
        }
    }

    private async Task RunSendContinue(CancellationToken ct)
    {
        await TransitionTo(OrchestratorState.SendContinue);
        _continueCount++;
        _logger.Info(OrchestratorState.SendContinue, $"[SEND-CONTINUE] 第 {_continueCount}/5 次");

        if (_continueCount > 5)
        {
            await Halt("[HALT] SendContinue 已超过 5 次上限，请人工干预");
            return;
        }

        // Ensure we're on the task detail page
        if (_currentTask != null && !_browser.CurrentUrl.Contains(_currentTask.TaskId))
            await _browser.NavigateAsync(_currentTask.HtmlUrl!, ct);

        var available = await _browser.ExecuteScriptAsync(BrowserScripts.CheckChatFormAvailable);
        if (available != "true")
        {
            await Halt("[HALT] SendContinue: 未找到可用的 task-chat-input-form，请人工干预");
            return;
        }

        var countBefore = int.TryParse(
            await _browser.ExecuteScriptAsync(BrowserScripts.GetUserMessageCount), out var c) ? c : 0;

        var sent = await _browser.ExecuteScriptAsync(
            BrowserScripts.TypeAndSubmitChatForm("继续执行并完成本次任务完整需求并记录日志"));
        if (sent != "true")
        {
            await Halt("[HALT] SendContinue: 提交失败，请人工干预");
            return;
        }

        // Wait for new user message to appear (up to 30s)
        var submitDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < submitDeadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);
            var countNow = int.TryParse(
                await _browser.ExecuteScriptAsync(BrowserScripts.GetUserMessageCount), out var cn) ? cn : 0;
            if (countNow > countBefore) break;
        }

        await RunAwaitInProgress(ct);
    }

    private async Task RunHandleFailed()
    {
        await TransitionTo(OrchestratorState.HandleFailed);
        await Halt($"[HALT] 任务 state=failed，taskId={_currentTask?.TaskId}");
    }

    private async Task RunHandleTimedOut(CancellationToken ct)
    {
        await TransitionTo(OrchestratorState.HandleTimedOut);
        if (_currentTask == null) { await Halt("HandleTimedOut: 无任务信息"); return; }

        await _browser.NavigateAsync(_currentTask.HtmlUrl, ct);
        var available = await _browser.ExecuteScriptAsync(BrowserScripts.CheckChatFormAvailable);
        if (available == "true") { await RunSendContinue(ct); }
        else { await Halt("[HALT] HandleTimedOut: 无 input form，流程设计缺陷，请人工干预"); }
    }

    private async Task RunHandleWaitingUser()
    {
        await TransitionTo(OrchestratorState.HandleWaitingUser);
        await Halt("[HALT] state=waiting_for_user，流程设计缺陷，请人工干预");
    }

    private async Task RunHandleCancelled()
    {
        await TransitionTo(OrchestratorState.HandleCancelled);
        await Halt("[HALT] state=cancelled，流程设计缺陷，请人工干预");
    }

    private async Task RunCreatePR(CancellationToken ct)
    {
        await TransitionTo(OrchestratorState.CreatePR);
        var deadline = DateTime.UtcNow + CreatePrTimeout;
        var reloadDone = false;

        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            var enabled = await _browser.ExecuteScriptAsync(
                BrowserScripts.IsButtonEnabled("Create pull request"));
            if (enabled == "true")
            {
                _logger.Info(OrchestratorState.CreatePR, "[CLICK] Create pull request");
                await _browser.ExecuteScriptAsync(BrowserScripts.ClickButton("Create pull request"));

                // Wait for navigation to /pull/{n}
                var prDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                while (DateTime.UtcNow < prDeadline && !ct.IsCancellationRequested)
                {
                    await Task.Delay(1000, ct);
                    if (_browser.CurrentUrl.Contains("/pull/")) break;
                }

                if (!_browser.CurrentUrl.Contains("/pull/"))
                {
                    await Halt("CreatePR: 点击后未跳转到 /pull/{n} 页面");
                    return;
                }

                await RunMarkReady(ct);
                return;
            }

            // 5 min mark: reload once
            if (!reloadDone && DateTime.UtcNow - (deadline - CreatePrTimeout) >= CreatePrReloadAfter)
            {
                _logger.Info(OrchestratorState.CreatePR, "Create pull request 按钮 5 分钟未可用，Reload 一次");
                await ReloadAndWaitAsync(ct);
                reloadDone = true;
            }

            await Task.Delay(PollInterval, ct);
        }

        await Halt("[HALT] CreatePR: 15 分钟内按钮未可用，请人工干预");
    }

    private async Task RunMarkReady(CancellationToken ct)
    {
        await TransitionTo(OrchestratorState.MarkReady);
        var wipText = "This pull request is still a work in progress";
        var hasWip = await _browser.ExecuteScriptAsync(BrowserScripts.PageContainsText(wipText));
        if (hasWip == "true")
        {
            _logger.Info(OrchestratorState.MarkReady, "[CLICK] Ready for review");
            await _browser.ExecuteScriptAsync(BrowserScripts.ClickButton("Ready for review"));
            // Allow page to update
            await Task.Delay(2000, ct);
        }
        await RunMerge(ct);
    }

    private async Task RunMerge(CancellationToken ct)
    {
        await TransitionTo(OrchestratorState.Merge);

        // Step 1: Wait for "Merge pull request" button
        if (!await WaitForButtonEnabled("Merge pull request", MergeButtonTimeout, MergePollInterval, ct))
        {
            await Halt("[HALT] Merge: 'Merge pull request' 按钮 10 分钟内未可用，请人工干预");
            return;
        }

        _logger.Info(OrchestratorState.Merge, "[CLICK] Merge pull request");
        await _browser.ExecuteScriptAsync(BrowserScripts.ClickButton("Merge pull request"));

        // Step 2: Wait for "Confirm merge" button
        if (!await WaitForButtonEnabled("Confirm merge", MergeButtonTimeout, MergePollInterval, ct))
        {
            await Halt("[HALT] Merge: 'Confirm merge' 按钮 10 分钟内未可用，请人工干预");
            return;
        }

        _logger.Info(OrchestratorState.Merge, "[CLICK] Confirm merge");
        await _browser.ExecuteScriptAsync(BrowserScripts.ClickButton("Confirm merge"));

        // Step 3: Wait for "Confirm merge" to disappear
        var disappearDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < disappearDeadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);
            var exists = await _browser.ExecuteScriptAsync(BrowserScripts.ButtonExists("Confirm merge"));
            if (exists != "true") break;
        }

        await TransitionTo(OrchestratorState.Done);
        _logger.Info(OrchestratorState.Done,
            $"[TASK-DONE] 文件={_currentTaskFile}，URL={_browser.CurrentUrl}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<bool> WaitForButtonEnabled(string text, TimeSpan timeout, TimeSpan interval, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            var enabled = await _browser.ExecuteScriptAsync(BrowserScripts.IsButtonEnabled(text));
            if (enabled == "true") return true;
            await Task.Delay(interval, ct);
        }
        return false;
    }

    private async Task ReloadAndWaitAsync(CancellationToken ct)
    {
        var url = _browser.CurrentUrl;
        if (!string.IsNullOrEmpty(url))
            await _browser.NavigateAsync(url, ct);
    }

    private async Task<List<TaskInfo>?> WaitForTasksXhrAsync(CancellationToken ct, int timeoutSeconds = 35)
    {
        _pendingTasksXhr = new TaskCompletionSource<List<TaskInfo>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        cts.Token.Register(() => _pendingTasksXhr.TrySetCanceled());
        try { return await _pendingTasksXhr.Task; }
        catch { return null; }
        finally { _pendingTasksXhr = null; }
    }

    private async Task<DiffResult?> WaitForDiffXhrAsync(CancellationToken ct, TimeSpan timeout)
    {
        _pendingDiffXhr = new TaskCompletionSource<DiffResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        cts.Token.Register(() => _pendingDiffXhr.TrySetResult(null));
        try { return await _pendingDiffXhr.Task; }
        catch { return null; }
        finally { _pendingDiffXhr = null; }
    }

    private void OnXhrReceived(string url, string body)
    {
        if (_urlMatcher == null) return;
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

            // Tasks XHR
            if (_pendingTasksXhr != null && _urlMatcher.IsTasksUrl(uri))
            {
                _logger.Info(_state, $"[XHR-HIT] tasks，URL={url}");
                var (tasks, err) = AgentResponseParser.ParseTaskList(body);
                if (err != null) _logger.Warn(_state, $"ParseTaskList: {err}");
                _pendingTasksXhr.TrySetResult(tasks);
                return;
            }

            // Diff XHR
            if (_pendingDiffXhr != null && UrlMatcher.IsDiffUrl(uri, _expectedTaskId))
            {
                _logger.Info(_state, $"[XHR-HIT] diff，URL={url}");
                var (result, err) = AgentResponseParser.ParseDiff(body);
                if (err != null) _logger.Warn(_state, $"ParseDiff: {err}");
                _pendingDiffXhr.TrySetResult(result);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn(_state, $"OnXhrReceived 异常: {ex.Message}");
        }
    }

    private async Task TransitionTo(OrchestratorState newState)
    {
        _logger.Info(_state, $"[{_state}→{newState}]");
        _state = newState;
        StateChanged?.Invoke(newState);
        await Task.Yield(); // allow UI to update
    }

    private async Task Halt(string reason, Exception? ex = null)
    {
        _logger.Error(_state, reason, ex, _browser.CurrentUrl);
        await TransitionTo(OrchestratorState.Halt);
        _cts?.Cancel();
        HaltRequested?.Invoke();
    }
}
