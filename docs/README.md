# docs/ —— 多角色协作的唯一事实源 (Single Source of Truth)

本目录是所有角色(PM / UI / Architect / Frontend / Backend / QA)**产出文档**的统一落盘位置。

```
docs/
├── prd/            PM 产出:产品需求文档(结构见 agents/PM/ 提示词第 2 节)
├── design/         UI 产出:视觉与交互规范、组件库、设计令牌
├── architecture/   架构师产出:技术栈、数据模型、接口契约、ADR、NFR
└── qa/             QA 产出:测试计划、测试用例、缺陷报告
```

## 初始化建议

本仓库作为**脚手架**,不强制预建所有子目录。推荐按下述次序,由 PM 角色首次执行时生成:

1. PM 使用 `agents/PM/产品经理-PRD拆分提示词.md`,先创建 `docs/prd/` 骨架(第 2 节的目录树)。
2. 架构师介入前,PM 至少完成 `docs/prd/00-overview/` 和 `docs/prd/10-information-architecture/`。
3. 架构师首次产出时创建 `docs/architecture/00-overview.md` + `01-tech-stack.md` + `06-adr/ADR-001-*.md`。
4. UI 设计师根据 PRD 页面清单创建 `docs/design/pages/<P-xxx>.md`。
5. QA 按已通过评审的 PRD 子功能创建 `docs/qa/test-plans/<domain>.md`。

## 跨文档引用约定

- 引用其他文档时**一律使用相对仓库根的路径**(如 `docs/prd/20-features/F-auth/F-auth-01-login/21-rules.md#§3`),禁止使用相对本文件的路径,方便工具按绝对路径校验。
- 文件必须带 YAML Front Matter 头,包含 `id` / `status` / `last_updated` 等元数据(详见各角色提示词)。

## 变更追踪

所有 PRD 变更统一记录在 `docs/prd/99-change-log/CHANGELOG.md`;架构决策变更记录在 `docs/architecture/06-adr/` 目录下每个 ADR 自身的 Status 变化中。
