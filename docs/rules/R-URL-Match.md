# R-URL-Match：XHR/Fetch URL 匹配规则

## 任务列表接口

- **pathname 正则**：`^/copilot/api(/internal)?/agents/repos/{owner}/{repo}/tasks$`
  - `{owner}` 和 `{repo}` 在运行时动态注入，注入前须对特殊字符做正则转义（`Regex.Escape`）
- **查询参数**：接受任意 `creator_id`、`page`、`archived`、`per_page` 值，无需强验证
- **有效字段**（只读以下字段，其余一律忽略）：
  - `state`
  - `html_url`
  - `created_at`

## diff 接口

匹配条件（全部满足）：

1. `pathname` 以 `/diff` 结尾
2. `pathname` 含 `/tasks/{taskId}/`（`taskId` 来自已捕获的 `html_url` 末尾路径段）
3. `searchParams.get('base') === 'main'`

## session 文件路径正则

```
^\.ai/workplace/session_(pm|ui|architect|backend|frontend|qa)_\d{8}_\d{6}\.md$
```

## 通用解析原则

- 全部 JSON 解析使用 `System.Text.Json`，配置 `JsonSerializerOptions` 忽略未知字段（`JsonUnmappedMemberHandling.Skip` 或 `IgnoreNullValues`）。
- 任何解析异常：记录原始响应前 500 字符，继续运行，不抛出。
- URL 匹配失败：静默丢弃，不打日志（避免大量 TRACE 噪音）。
- 所有 XHR 监听通过一次注册 `CoreWebView2.WebResourceResponseReceived`，全局分发，不重复注册。
