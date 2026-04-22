# R-TaskTemplate：任务文件模板规则

## 文件命名约定

| 目录 | 命名规则 |
|---|---|
| `ready/` | 任意有意义名称，如 `01-research.md` |
| `queued/` | `{yyMMdd_HHmmss}_{N}_{taskname}.md`（N 从 1 开始，表示执行顺序） |
| `completed/` | 与 ready 同名（直接移动，不重命名） |

## 目录不变式（任何时刻均须满足）

- `ready/` 目录最多 **1** 个文件。
- `queued / ready / completed` 三目录内文件名（不含路径前缀）**互斥**，同一任务文件只能出现在一个目录中。
- 操作顺序：先写目标文件，再删源文件（避免文件丢失）。

## 任务文件结构（固定小节，顺序不变）

```
---
role: pm
task_id: 任务唯一ID或简短描述
created_at: YYYY-MM-DD
---
```

### 1. 引用的 Agent
```
@agents/{Role}/*.md
```

### 2. 需阅读的上下文
- 顺序任务的历史产出文件（`.ai/workplace/{前序文件}.md`）
- 规则引用（`docs/rules/R-EndMarker.md`，`docs/rules/R-TaskTemplate.md`，其他相关规则）

### 3. 本次任务要求
- 用可测量、可验收的条目描述，每条以 `-` 列举
- **颗粒度要求**：拆解到最小可验收单元（例如登录功能须列明：字段清单、必填规则、校验逻辑、错误态、2FA 处理等）

### 4. 本次任务输出
- 明确输出文件路径（`.ai/workplace/...`）及格式

### 5. 收尾动作（强制执行）
1. 将本任务从 `/.ai/tasks/{role}/ready/{task}.md` **移动**到 `/.ai/tasks/{role}/completed/{task}.md`
2. 若本轮产出多个后续任务：
   - 写入 `/.ai/tasks/{role}/queued/{yyMMdd_HHmmss}_{N}_{taskname}.md`
   - 将 `queued/` 中序号最小（N=1）的文件 **移动**到 `/ready/`
3. 严格维持目录不变式

### 6. 结束文本（强制，会话最后输出）
会话输出的最后一行必须是且仅是以下之一，之后**不允许**有任何其他内容：

```
===任务全部完成===
===部分任务未完成===
===任务执行出现错误===
```

---

## PM 角色附加要求

- 调研/需求/设计文档须达到下游角色可直接引用的细颗粒度。
- 示例（登录功能须包含）：
  - 所有表单字段（字段名、类型、是否必填、校验规则、最大长度）
  - 支持的登录方式（用户名/邮箱密码、OAuth、2FA、SSO）
  - 错误态列表（错误码、提示文案、是否可重试）
  - 成功后跳转逻辑
- **PM 须在所有下游任务文件中显式引用 `docs/rules/R-EndMarker.md`**，强调每个 Agent 会话末尾必须以结束标记结束。
- PM 生成批量任务时，须将所有后续任务写入 `queued/`，只将第一个移入 `ready/`。

---

## 任务文件示例

```markdown
---
role: pm
task_id: 01-initial-research
created_at: 2026-04-22
---

## 引用的 Agent
@agents/PM/产品经理-PRD拆分提示词.md

## 需阅读的上下文
- docs/rules/R-EndMarker.md
- docs/rules/R-TaskTemplate.md

## 本次任务要求
- 调研用户登录功能，输出字段清单、校验规则、错误态
- 调研用户注册功能，输出字段清单、校验规则

## 本次任务输出
- `.ai/workplace/session_pm_20260422_100000.md`（Markdown 格式）

## 收尾动作
1. 移动本文件：`ready/01-initial-research.md` → `completed/01-initial-research.md`
2. 无后续任务

===任务全部完成===
```
