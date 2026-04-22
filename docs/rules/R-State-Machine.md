# R-State-Machine：任务状态机

## 核心约束

- **单一 Orchestrator**：同一时刻只有一个 `State` 活跃。
- 切换 State 时必须先取消当前 `CancellationTokenSource`，再 `new CancellationTokenSource()`。
- 所有 State 切换须记录 INFO 日志：`[STATE→新STATE] 原因`。
- 禁止并发多个轮询循环。

## 状态表

| State | 进入条件 | 主要动作 | 超时 | 成功跳转 | 失败/异常跳转 |
|---|---|---|---|---|---|
| `AwaitQueued` | task.state=queued | 每 30s Navigate(same URL)，等待 tasks XHR | 10 min | state≠queued → 按新 state 转跳 | `Halt(人工干预)` |
| `AwaitInProgress` | state=in_progress | 每 30s 刷新+监听 | 无上限（心跳日志每 5 min） | state=completed → `InspectResult` | 按 state 转跳 |
| `AwaitIdle` | state=idle | 同 AwaitQueued | 10 min | 同 AwaitQueued | `Halt` |
| `InspectResult` | state=completed | **停止其它轮询**；Navigate(html_url)；监听 diff XHR | 60s | diff 含 session md → `EndMarkerCheck`；无 → `SendContinue` | 重试 3 次后 `Halt` |
| `EndMarkerCheck` | diff 含 session md | 滚动到底直到稳定；取最后 assistant 消息末尾 3 非空行；匹配 R-EndMarker | 120s | ===任务全部完成=== → `CreatePR`；其余 → `SendContinue`；无匹配 → `Halt` |
| `SendContinue` | 需追加指令 | 向 `form#task-chat-input-form textarea` 写入指令并提交；计数器+1 | 30s | 回到 `AwaitInProgress` | 超 5 次 → `Halt` |
| `HandleFailed` | state=failed | 日志 ERROR，`Halt` | — | — | — |
| `HandleTimedOut` | state=timed_out | Navigate(html_url)；有 input form → `SendContinue`；否则 `Halt` | — | — | — |
| `HandleWaitingUser` | state=waiting_for_user | 日志 ERROR "流程设计缺陷"，`Halt` | — | — | — |
| `HandleCancelled` | state=cancelled | 日志 ERROR "流程设计缺陷"，`Halt` | — | — | — |
| `CreatePR` | EndMarker=全部完成 且有 session md | **不刷新**，每 30s 检查 Create pull request 按钮；5 min 不可用 → Reload 一次；点击后等跳转至 /pull/{n} | 总体 15 min | `MarkReady` | `Halt` |
| `MarkReady` | PR 页面加载完成 | 若有 "still a work in progress" → 点 "Ready for review"；否则跳过 | 60s | `Merge` | `Halt` |
| `Merge` | PR 可合并 | **不刷新**，每 20s 检查 Merge 按钮；10 min 不可用 → `Halt`；点击后每 20s 检查 Confirm merge；10 min 不可用 → `Halt`；消失后 Done | — | `Done` | `Halt` |
| `Done` | merge 完成 | INFO "任务 {task} 已合并"；回主循环 | — | — | — |
| `Halt` | 任意错误/人工 | 停止所有 timer；UI MessageBox + ERROR 日志；保留 WebView2 | — | — | — |

## state 字段映射

```
queued           → AwaitQueued
in_progress      → AwaitInProgress
completed        → InspectResult
failed           → HandleFailed
idle             → AwaitIdle
waiting_for_user → HandleWaitingUser
timed_out        → HandleTimedOut
cancelled        → HandleCancelled
```
