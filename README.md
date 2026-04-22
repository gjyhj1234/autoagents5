# autoagents5

一个面向 **多角色 AI 协作开发** 的提示词脚手架。目标是让 PM 产出结构化到"编号级"的 PRD 目录，使下游 UI / 架构师 / 前端 / 后端 / 测试各 AI 角色**零联想**地完成交付。

---

## AutoAgents5 WinForm 自动化客户端

`src/` 目录包含一个 **Windows WinForm + WebView2** 桌面应用，用于全自动执行 GitHub Copilot Agents 任务闭环：  
`登录 GitHub → 读取任务 → 提交任务 → 监听执行 → 发 PR → 合并 → 循环下一条`

### 环境要求

| 组件 | 要求 |
|---|---|
| 操作系统 | Windows 10/11 x64 |
| .NET Runtime | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| WebView2 | [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) |
| 开发构建 | .NET 8 SDK |

### 编译

```bash
dotnet build src/AutoAgents5.App/AutoAgents5.App.csproj -c Release
```

或在 Visual Studio 2022+ 中打开 `AutoAgents5.sln`，选择 `Release / x64`，按 Ctrl+Shift+B。

### 运行测试

```bash
dotnet test src/AutoAgents5.Tests/AutoAgents5.Tests.csproj
```

### 使用方法

1. 启动 `AutoAgents5.App.exe`
2. 在浏览器控件中登录 GitHub（登录态自动持久化至 `%LOCALAPPDATA%\AutoAgents5\WebView2`）
3. 填写「用户名」「仓库名」「角色」，点击「▶ 启动」
4. 客户端自动从 `.ai/tasks/{role}/ready/` 读取任务文件并依次执行，全程无需人工干预
5. 如遇到需要人工干预的情况，会弹窗提示并写入日志（`./logs/run-{yyyyMMdd}.log`）

### 工程结构

```
src/
├── AutoAgents5.Core/          # 核心逻辑（状态机、规则、服务），纯 .NET，可测试
│   ├── Models/                # TaskInfo, DiffResult, OrchestratorState 等
│   └── Services/              # TaskOrchestrator, UrlMatcher, EndMarkerChecker 等
├── AutoAgents5.App/           # WinForm UI + WebView2
│   ├── MainForm.cs            # 主窗体
│   ├── WebView2BrowserBridge  # IBrowserBridge 实现
│   └── GitHubContentsApi.cs   # 读取任务文件列表
└── AutoAgents5.Tests/         # xUnit 单元测试
```

### 规则文档

`docs/rules/` 包含可复用的标准规则，供任务文件和 AI Agents 引用：

| 文件 | 说明 |
|---|---|
| `R-URL-Match.md` | XHR/Fetch URL 匹配规则 |
| `R-State-Machine.md` | 任务状态机定义 |
| `R-Polling.md` | 轮询与超时规则 |
| `R-EndMarker.md` | 会话结束标记识别规则 |
| `R-Logging.md` | 日志格式规则 |
| `R-TaskTemplate.md` | 任务文件模板规则 |

### 已知限制

- 仅支持 Windows x64（WebView2 依赖）
- GitHub 页面 DOM 结构若发生变化，可能需要更新 `BrowserScripts.cs` 中的 CSS 选择器
- 首次运行需手动在 WebView2 中完成 GitHub 登录（含 2FA）

---

## 仓库目录

```
.
├── src/            AutoAgents5 WinForm 客户端（见上）
├── docs/rules/     可复用规则文档（R-*.md）
├── product/        原始英文 Agent 文档（5 个通用产品智能体）
├── productzh/      product/ 的中文镜像
├── agents/         ⭐ 多角色 AI 提示词脚手架
│   ├── PM/         产品经理 —— PRD 拆分与目录生成
│   ├── UI/         UI/UX 设计师
│   ├── Architect/  系统架构师
│   ├── Frontend/   前端工程师
│   ├── Backend/    后端工程师
│   └── QA/         测试工程师
└── docs/           各角色产出文档（PRD / 设计 / 架构 / 测试）
```

## 快速开始（AI 协作流程）

1. 阅读 [`agents/README.md`](./agents/README.md) 了解角色协作流水线。
2. 以 **PM 角色**启动新项目：使用 [`agents/PM/产品经理-PRD拆分提示词.md`](./agents/PM/产品经理-PRD拆分提示词.md)，让 AI 先生成 `docs/prd/` 骨架。
3. 并行启动 **UI** 与 **Architect**，产出设计与接口契约。
4. **Frontend / Backend** 按页面 ID 与接口 ID 并行开发；**QA** 对照验收标准驱动测试。
5. 使用本仓库的 **WinForm 客户端**全自动执行上述流程，无需人工逐步操作。

