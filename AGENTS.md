# Dictation 工程规则入口

本文件只负责规则路由与优先级。开始任务时先读取本文件，再按任务涉及的内容读取相应规则；同一任务涉及多个领域时必须同时遵守全部适用文件，不得以仅阅读其中一个文件代替其余规则。

| 适用内容 | 规则文件 |
|---|---|
| 系统架构、模块依赖、核心算法、SQLite 数据结构 | `rules/architecture.md` |
| C# 命名、文件组织、类型及注释规范 | `rules/code-style.md` |
| Git 分支、TDD、开发步骤、验证、PR、状态流转 | `rules/workflow.md` |
| Trello Task、身份、Checklist、任务生命周期与版本 | `rules/project-management.md` |
| 需求和设计文档、最终状态、文档版本 | `rules/design-documents.md` |
| GitHub/Trello 资源定位、开发环境、SDK、工具与工作区 | `rules/tooling.md` |

项目身份及版本配置以 `ProjectInfo.json` 为准。项目 Skills 的入口位于 `.agents/skills/`，实际调用条件由相关规则指定。

## 规则优先级

`AGENTS.md` 的路由及优先级要求优先于 `rules/*.md`。各规则文件之间，以适用范围更具体者为准；发现实质冲突时报告冲突，不自行选取方便执行的版本。
