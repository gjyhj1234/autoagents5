# agents/ —— 多角色 AI 提示词脚手架

本目录为每个协作角色提供**独立的中文提示词文件**。一个 AI 会话只加载一个角色的提示词,仅读取该角色在下文"输入"中声明的文档片段,从而保证**零联想、可追溯、可并行**。

```
agents/
├── PM/          产品经理 —— 拆 PRD,产出 docs/prd/ 目录
├── UI/          UI/UX 设计师 —— 产出 docs/design/
├── Architect/   架构师 —— 产出 docs/architecture/(含 ADR、数据模型、接口契约)
├── Frontend/    前端工程师 —— 产出源码
├── Backend/     后端工程师 —— 产出源码
└── QA/          测试工程师 —— 产出 docs/qa/ + 自动化脚本
```

## 1. 角色协作流水线

```
  ┌──────────┐   PRD   ┌──────────┐   设计稿  ┌───────────┐
  │    PM    │────────▶│    UI    │──────────▶│  Frontend │
  └────┬─────┘         └──────────┘           └─────┬─────┘
       │                                            │ 引用契约
       │ 规则/实体/NFR         ADR/契约/数据模型    ▼
       │                   ┌──────────┐           ┌───────────┐
       └──────────────────▶│ Architect├──────────▶│  Backend  │
                           └────┬─────┘           └─────┬─────┘
                                │                      │
                                │  验收标准 + 接口契约 │
                                ▼                      ▼
                           ┌─────────────────────────────┐
                           │            QA              │
                           └─────────────────────────────┘
```

- **PM 是"契约之源"**:输出 `docs/prd/`,是所有下游角色的唯一事实源。
- **UI** 与 **Architect** 并行,都以 PRD 为输入。
- **Frontend/Backend** 以 UI + Architect 的输出 + PRD 为输入。
- **QA** 是全员的"验收守门员",以 PRD 的 `40-acceptance.md` 为核心驱动。

## 2. 每个角色的提示词文件,Front Matter 四要素

所有提示词文件头部统一用 YAML Front Matter 声明:

```yaml
---
name: <角色显示名>
role: PM | UI | Architect | Frontend | Backend | QA
inputs:       # 该角色**允许读取**的文档 glob
  - docs/prd/...
outputs:      # 该角色**应当落盘**的路径
  - docs/design/...
non_scope:    # 该角色**不做什么**
  - ...
---
```

这让后续 AI 编排时只需按 `role` 挑选提示词,并按 `inputs` 过滤上下文,做到高信噪比。

## 3. 在 GitHub 上使用本脚手架的推荐流程

### 3.1 仓库布局(建议)

```
.
├── agents/              # 本目录:各角色提示词
├── productzh/           # 通用产品智能体中文库(与 agents/ 独立,可复用)
├── docs/
│   ├── prd/             # PM 产出
│   ├── design/          # UI 产出
│   ├── architecture/    # 架构师产出
│   └── qa/              # QA 产出
├── apps/
│   ├── web/             # 前端代码
│   └── api/             # 后端代码
├── .github/
│   ├── ISSUE_TEMPLATE/  # 每个角色一个 issue 模板
│   ├── PULL_REQUEST_TEMPLATE.md
│   └── workflows/       # CI:文档链接校验 + 契约一致性校验
└── README.md
```

### 3.2 分支与 PR 策略

- **按文档/代码分两类 PR**:
  - `docs/*` 路径的变更由 PM / UI / Architect 发起,走 `doc-review` 模板;
  - `apps/*` 路径的变更由 Frontend / Backend 发起,走 `code-review` 模板,PR 描述必须引用至少一个 PRD 文件路径。
- **分支命名**:`pm/<F-xxx>-<short>` / `arch/<ADR-xxx>` / `fe/<P-xxx>` / `be/<API-xxx>` / `qa/<TC-xxx>`。

### 3.3 Issue 模板(建议)

- **[PM] 新功能域拆分** —— 要求填写目标、目标用户、支持端、显式非目标、交付里程碑。
- **[PRD 回写问题]** —— 从下游(FE/BE/QA)向 PM 回写需 PM 澄清的问题,模板要求 `prd_path` 与 `ambiguity` 两个必填字段。
- **[架构决策] ADR 提议** —— 结构照 `Architect/架构师提示词.md` 第 5 节。
- **[缺陷报告]** —— QA 专用,要求 `AC` 或 `TC` ID 字段。

### 3.4 CI 建议(轻量,后续可逐步引入)

1. **链接检查**:校验 PRD/设计/架构文档里的相互引用路径是否存在。
2. **Front Matter 校验**:脚本扫描 `docs/prd/**` 和 `agents/**`,校验必填字段。
3. **契约一致性**:`docs/architecture/03-api-contracts/` 中的 `prd_ref` 是否均可解析。
4. **ID 唯一性**:扫描 `P-*` / `F-*` / `E-*` / `AC-*` 避免重号。

### 3.5 与 AI IDE/Agent 的集成

- 在 IDE(如 VS Code + Copilot / Cursor 等)中,为每个角色创建一个独立的"规则文件"指向 `agents/<Role>/*.md`。
- 用户说"切到 PM 角色" → 仅加载 `agents/PM/`;说"切到后端" → 仅加载 `agents/Backend/` + 本次涉及的契约与规则文件,避免上下文污染。

## 4. 从 0 到 1 走一遍(新项目)

1. 以 **PM** 角色启动,使用 `agents/PM/产品经理-PRD拆分提示词.md`,先生成 `docs/prd/` 骨架 + 问题清单。
2. PM 与用户迭代澄清,填充 `00-overview/`、`10-information-architecture/` 和前 1-2 个业务域。
3. 并行切到 **UI** 与 **Architect**:UI 按已完成的域出 `docs/design/pages/`;Architect 出 `tech-stack` + `api-contracts` + `data-model`。
4. **QA** 尽早介入,根据 `40-acceptance.md` 先把测试计划骨架搭起来。
5. **Frontend/Backend** 按页面/接口 ID 并行开发,每个 PR 引用其对应的 PRD/设计/契约路径。
6. 上线后,PM 用 `CHANGELOG.md` 记录 PRD 变化,避免文档漂移。

## 5. 角色间沟通的"回写问题"闭环

下游发现上游文档歧义或缺失时,统一按此流程,不允许私下脑补:

1. 下游角色在本角色的 `open-questions.md` 中记录问题(`Q-xxx`)。
2. 在对应上游文档的 `90-open-questions.md` 追加一条反向索引。
3. 创建 `[PRD 回写问题]` / `[架构回写问题]` 类型的 Issue,指派给上游角色。
4. 上游决策后,同时更新:原文档 + 两侧 `open-questions.md` + `docs/prd/99-change-log/CHANGELOG.md`。

---

> **一句话总结本脚手架的价值**:让 PM 产出结构化到"编号级"的需求,让每个下游 AI 角色只读自己该读的片段,把"AI 编码时的自由联想"降到接近为零,同时保留严格的双向回写机制应对真实世界的变化。
