# Dictation Agent Instructions

执行本仓库任务前，必须核对用户授权、对应 Task 及当前仓库状态，并根据下表读取所有适用的规则文件。实施、验证和交付均须遵守这些规则规定的前置条件、执行顺序与验收要求；不得跳过流程门禁、擅自变更步骤、扩大授权范围，或将未核实的结果视为已完成。规则冲突时优先遵守适用范围更具体的要求；仍无法确定时，停止受影响操作并报告。

## 指定插件强制路由

对 GitHub、Trello、Google Drive 的远端资源操作，必须分别使用当前环境中已授权的 GitHub、Trello、Google Drive 专用插件。操作前先发现并核实对应插件的连接、权限和实际支持能力；禁止使用 Computer Use、浏览器自动化、模拟键鼠、命令行或其他间接方式替代这些插件负责的操作。

如果指定插件未连接、不可用、权限不足、不支持所需操作，或调用失败且无法通过该插件解决，必须立即停止受影响操作及依赖它的后续步骤，报告插件名称、目标操作、阻塞原因、已完成步骤和所需处理，等待用户新的明确指示。不得自动回退或绕过。具体工具边界见 `rules/tooling.md`；本地代码编辑、Git 工作区操作、构建、测试和真实应用界面验证仍按各自项目规则进行。

## 规则目录

| 适用内容 | 规则文件 |
|---|---|
| 系统架构、模块依赖、核心算法、SQLite 数据结构 | `rules/architecture.md` |
| 第三方词库与语音资源来源、版本、哈希、运行布局及许可归属 | `rules/resources.md` |
| C# 命名、文件组织、类型及注释规范 | `rules/code-style.md` |
| Git 分支、TDD、开发步骤、验证、PR、状态流转 | `rules/workflow.md` |
| Trello Task、身份、Checklist、任务生命周期与版本 | `rules/project-management.md` |
| 需求和设计文档、最终状态、文档版本 | `rules/design-documents.md` |
| GitHub/Trello 资源定位、开发环境、SDK、工具与工作区 | `rules/tooling.md` |

项目身份及版本配置以 `ProjectInfo.json` 为准。项目 Skills 位于 `.agents/skills/`，其适用条件由相应规则指定。
