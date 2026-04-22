# R-Polling：轮询与去重规则

## 单 Timer 原则

- 全局只维护一个 `CancellationTokenSource`（`_cts`）。
- 每次进入新 State，先取消旧 `_cts`（`_cts?.Cancel(); _cts?.Dispose()`），再 `new CancellationTokenSource()` 赋值给 `_cts`。
- 所有 `PeriodicTimer` / `Task.Delay` 均传入当前 `_cts.Token`。
- 禁止 `static` 定时器字段；禁止多个并发 `while` 循环监听同一事件。

## 刷新规则

- **"刷新页面"**：调用 `webView.CoreWebView2.Navigate(currentUrl)`，等待 `NavigationCompleted`（`IsSuccess==true`）后才允许继续检查。
- **"不刷新"**：只观察 DOM/按钮可用性，不触发 `Navigate` 或 `Reload`。

## 退避策略

- 网络导航失败（`IsSuccess==false`）：最多重试 3 次，间隔指数退避（1s、2s、4s）；超限 → `Halt`。
- HTTP 429 / `Retry-After` 头：读取值后 `await Task.Delay(retryAfter)`，再重试；最多 3 次。
- 一般异常（非取消）：记录 WARN，重试最多 3 次；超限 → `Halt`。

## 超时触发人工干预

| 场景 | 超时 | 动作 |
|---|---|---|
| queued / idle 未变更 | 10 min | `Halt` + INFO |
| InspectResult 未收到 diff XHR | 60s | 重试 3 次后 `Halt` |
| EndMarkerCheck 无结果 | 120s | `Halt` |
| CreatePR 按钮不可用 | 总体 15 min（5 min 时 Reload 一次） | `Halt` |
| Merge pull request 按钮不可用 | 10 min | `Halt` |
| Confirm merge 按钮不可用 | 10 min | `Halt` |

## 心跳日志

- `AwaitInProgress` 无超时限制，但每 5 分钟输出一次 INFO 心跳：

  ```
  [HEARTBEAT] AwaitInProgress 已运行 {elapsed} min，taskId={taskId}
  ```
