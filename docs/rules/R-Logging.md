# R-Logging：日志规则

## 格式

```
[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] [STATE] message
```

示例：

```
[2026-04-22 10:30:00.123] [INFO] [AwaitQueued] 任务状态变更：queued → in_progress，taskId=c0de5caf
[2026-04-22 10:35:00.456] [ERROR] [Merge] 合并按钮 10 分钟内不可用，停机等待人工干预
  URL: https://github.com/owner/repo/pull/42
  System.TimeoutException: Merge button not enabled after 10 minutes
    at AutoAgents5.Core.Orchestrator.MergeAsync() ...
```

## 级别定义

| 级别 | 用途 |
|---|---|
| TRACE | 详细调试，XHR 原始 payload 摘要（前 200 字符） |
| DEBUG | 内部状态变量、DOM 查询结果、按钮 enabled 状态 |
| INFO | **强制**：状态迁移、XHR 命中、按钮点击、人工干预触发、心跳、任务开始/结束 |
| WARN | 重试、降级、非致命异常、rate-limit 等待 |
| ERROR | 致命错误（Halt 触发），必须附异常堆栈和当前 URL |

## 文件策略

- 路径：`./logs/run-{yyyyMMdd}.log`（相对于可执行文件目录）
- 按天轮转（新日期自动新建文件）
- 每条日志同时 append 到 UI `RichTextBox`（通过 `Invoke` 切回 UI 线程）
- `logs/` 目录须在 `.gitignore` 中排除

## 颜色映射（UI RichTextBox）

| 级别 | 前景色 |
|---|---|
| TRACE | Gray |
| DEBUG | DarkGray |
| INFO | Black (或白底黑字) |
| WARN | DarkOrange |
| ERROR | Red |

## 强制记录节点

以下节点必须输出 INFO 日志：

1. 状态机 State 切换（`[OldState→NewState] 原因`）
2. XHR 命中（`[XHR-HIT] tasks / diff，URL=...`）
3. 按钮点击（`[CLICK] 按钮名称，URL=...`）
4. 发送"继续执行"指令（`[SEND-CONTINUE] 第 N/5 次`）
5. 人工干预触发（`[HALT] 原因，请人工处理`）
6. 任务开始（`[TASK-START] 文件={task}.md，role={role}`）
7. 任务合并完成（`[TASK-DONE] 文件={task}.md，PR=#{n}`）
8. 心跳（`[HEARTBEAT] ...`）
