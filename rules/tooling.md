# Tooling Rules

> 工具选择服从 `AGENTS.md`、Task 授权和已确认的技术架构，不让工具自行改变项目边界。

## 项目资源与工具绑定

- **唯一目标 GitHub 仓库**：`water1st/Dictation`；PR 基准分支为 `master`。GitHub 仓库、分支、Issue、PR、Commit 与文件操作均先确认归属本仓库；调用 GitHub 工具时以完整仓库名 `water1st/Dictation` 为目标，不根据聊天上下文推测或切换其它仓库。
- **唯一目标 Trello 看板**：`Development Hub`。先通过已授权 Trello 连接准确定位该看板，取得本次会话中的实际看板标识；随后将任务搜索、卡片读取、清单与状态操作限定在该看板。若同名看板不唯一、标识无法核验或 Task 不属于该看板，停止写入并报告。
- **项目身份核验**：读取 `ProjectInfo.json` 的 `projectName`、`githubRepository`、`prBaseBranch`、`trello.boardName` 和项目标签配置；对照当前 GitHub 仓库、PR 和 Trello 卡片中的完整 Task ID、项目、看板及项目标签。查询得到的实际资源标识用于本次操作，不把它们硬编码到公开仓库。
- GitHub 的分支、提交、PR 操作遵守 `rules/workflow.md`；Trello 列表、Checklist 与 Task 状态遵守 `rules/project-management.md`。找不到匹配资源时不自行创建替代资源，也不跨项目操作。

## .NET and SDK Compatibility

- 当前项目使用 .NET 10 / C# / WPF；`TargetFramework` 描述目标 API/运行时兼容性，**不等于必须安装某个精确 SDK 补丁号**。
- 优先使用当前已安装、支持目标框架且实际能成功 Restore/Build/Test 的稳定 SDK。兼容的较新 SDK、不同补丁号和合理前滚的 `global.json` 均应允许。
- 不能仅凭 `dotnet --version` 与文档示例数字不同就停止；应先检验 `dotnet --list-sdks`、适用的 `global.json`、目标框架和实际编译/测试结果。
- 若升级的 SDK 或编译器引发实际不兼容，记录具体错误、官方兼容约束及修复范围。需要锁定版本时必须存在可验证的不兼容或用户显式要求，不设置无根据的补丁锁。
- `ProjectInfo.json.programVersion` 是 Program Version，与 SDK / TargetFramework / Release 版本职责不同。
- 不擅自安装全局工具、升级 SDK、修改 CI 或发布应用；工具缺失时给出可核实的环境缺口。开发工具以实际项目约定为准。

## Verification Tools

- 验证命令见 `rules/workflow.md` 的「环境与验证命令」。
- 测试项目为 `Dictation.Tests`（xUnit v3 + Microsoft.Testing.Platform）。
- 检查真实输出，不声称未运行验证通过。数据 Schema 仅引用真实迁移、项目源码和验证事实，不推测表字段。

## Workspaces and Skills

- 原始仓库根目录是唯一默认开发工作区。通过 `git switch` 处理本项目不同 Task 分支。
- 用户未明确授权时，不允许 `git worktree add`、额外 `git clone`、复制项目到临时目录开发，或让 Codex/IDE 自动为任务创建隔离开发区。一次授权仅适用于明确任务和范围。
- 不使用 reset/clean 掩盖未提交修改；切换前保存 Checkpoint 并确认 Trello 状态。
- Skills 入口：`.agents/skills/grilling/SKILL.md`、`.agents/skills/grill-with-docs/SKILL.md`、`.agents/skills/domain-modeling/SKILL.md`、`.agents/skills/gstack/SKILL.md`。
- 需求/设计不明确先访谈；Bug 先记录 GitHub Issue，再按关联文档访谈；复杂审查可调用 gstack。任何 Skill 与授权、工作区或仓库规范冲突时服从更高规则。
