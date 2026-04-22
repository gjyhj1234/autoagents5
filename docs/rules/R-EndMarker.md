# R-EndMarker：会话结束标记识别规则

## 定义

会话结束标记是 Copilot Agent 最后一条 assistant 消息正文中出现的特定字符串，用于 WinForm 客户端判断任务是否真正完成。

## 识别算法

```
1. scrollToBottomUntilStable():
   - 每 300ms 检查页面 scrollHeight
   - 连续 3 次 scrollHeight 值不变 → 视为稳定
   - 最多等待 30s；超时视为已稳定，继续下一步

2. 定位最后一条 assistant 消息：
   - 在 WebView2 中执行 JavaScript，获取页面内语义上最后一个 assistant/bot 消息容器
   - 参考选择器（以实际 GitHub 页面 DOM 为准，需在运行时验证）：
     [data-author-type="bot"]:last-of-type，或类似属性的最后元素

3. 提取末尾行：
   - lines = element.innerText.split('\n').filter(line => line.trim() !== '')
   - tail = lines.slice(-3)   // 最后 3 个非空行

4. 匹配（取 tail 中序号最大即最后出现的匹配行）：
   - "===任务全部完成===" → AllDone
   - "===部分任务未完成===" → Continue
   - "===任务执行出现错误===" → Continue
   - tail 中无以上任何匹配 → Halt（需人工干预）

5. 多标记冲突：以 tail 数组中下标最大（最后）的那行为准。
```

## Agent 侧结束文本强制规则

每个任务的会话输出必须以且仅以以下三行之一结束，其后不允许有任何其他内容（包括空行除外）：

| 标记 | WinForm 处理 |
|---|---|
| `===任务全部完成===` | 触发 CreatePR 流程 |
| `===部分任务未完成===` | 触发 SendContinue（最多 5 次） |
| `===任务执行出现错误===` | 触发 SendContinue（最多 5 次） |

> **PM 须在所有下游任务文件中明确引用本规则**，确保每个 Agent 会话末尾必须输出上述结束标记之一。
