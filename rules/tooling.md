# Tooling Rules

> 工具选择服从 `AGENTS.md`、Task 授权和已确认的技术架构，不让工具自行改变项目边界。

## 项目资源与工具绑定

- **唯一目标 GitHub 仓库**：`water1st/Dictation`；PR 基准分支为 `master`。GitHub 仓库、分支、Issue、PR、Commit 与文件操作均先确认归属本仓库；调用 GitHub 工具时以完整仓库名 `water1st/Dictation` 为目标，不根据聊天上下文推测或切换其它仓库。
- **唯一目标 Trello 看板**：`Development Hub`。先通过已授权 Trello 连接准确定位该看板，取得本次会话中的实际看板标识；随后将任务搜索、卡片读取、清单与状态操作限定在该看板。若同名看板不唯一、标识无法核验或 Task 不属于该看板，停止写入并报告。
- **项目身份核验**：读取 `ProjectInfo.json` 的 `projectName`、`githubRepository`、`prBaseBranch`、`trello.boardName` 和项目标签配置；对照当前 GitHub 仓库、PR 和 Trello 卡片中的完整 Task ID、项目、看板及项目标签。查询得到的实际资源标识用于本次操作，不把它们硬编码到公开仓库。
- GitHub 的分支、提交、PR 操作遵守 `rules/workflow.md`；Trello 列表、Checklist 与 Task 状态遵守 `rules/project-management.md`。找不到匹配资源时不自行创建替代资源，也不跨项目操作。

## 指定外部服务插件强制路由

本项目指定的外部服务插件仅为 GitHub、Trello 和 Google Drive。执行远端资源操作前，必须从当前环境中发现已授权的对应插件，核实连接、权限、可用动作及其参数；不能未经检查就认定插件不存在。

- **GitHub 插件：** 对 `water1st/Dictation` 的远端仓库、分支、Issue、PR、远端提交及远端文件的读取和管理，必须使用 GitHub 专用插件，继续核实 `master` 基准分支及 Task ID。普通本地源码编辑和本地 Git 工作区操作仍可使用本地文件、Git 工具，但不得通过 Shell、网页或其他间接途径绕过远端 GitHub 插件。
- **Trello 插件：** `Development Hub` 看板内 Dictation 项目的列表、卡片、原生 Checklist、检查项和生命周期状态必须使用 Trello 专用插件，继续遵守 `rules/project-management.md` 的身份、标签、权限与状态门禁。
- **Google Drive 插件：** Drive 文件、文件夹、文档、任务附件的搜索、读取、上传、创建、修改、位置验证及管理必须使用 Google Drive 专用插件。

**禁止替代与回退：** 上述插件负责的操作禁止改用 Computer Use、浏览器自动化、模拟键鼠、命令行、脚本或其他间接方式。插件未连接、不可用、权限不足、不支持所需动作，或调用失败且无法通过该插件解决时，立即停止该操作及依赖步骤，向用户报告插件名称、原计划操作、具体阻塞、已完成的步骤和所需处理，等待用户新的明确指示；不得自动回退或绕行。

**结果验证与边界：** 插件写入后检查返回值并在必要时回读实际远端状态，不能将部分成功报告为全部完成。正常本地文件编辑、Git 工作区操作、.NET 构建与测试、真实 WPF 界面验证不受禁止；但不得借本地工具或 GUI 测试规避上述三插件的远端资源职责。保留 `ProjectInfo.json` 的项目身份核验及既有 Task、PR、版本规则。

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

## 仓库完整性与最小化原则

- Git 仓库必须保持为**最小、完整、可复现的开发项目**。在具备项目明确要求的外部工具和依赖的环境中，全新 Clone 后应能依据版本化的源码、配置、必要资源、项目规则、Skills、自动化工作流及依赖说明，完成还原、构建、测试、运行和持续开发；不得依赖未说明的本地私有文件或历史任务中间产物。
- 只将项目当前真正需要且具有持续用途的文件纳入版本控制。开发过程中产生的一次性分析、草稿、调试脚本、临时数据及过程报告不属于仓库交付内容。文件是否保留取决于实际作用，不取决于名称、目录或创建者。
- 最小化与完整性必须同时成立：不得为了减少文件数量删除必要的源码、配置、规则、Skills、工作流、构建工具或资源，也不得因某项工作曾使用过临时文件便将其永久保留。

## Workspaces and Skills

- 原始仓库根目录是唯一默认开发工作区。通过 `git switch` 处理本项目不同 Task 分支。
- 用户未明确授权时，不允许 `git worktree add`、额外 `git clone`、复制项目到临时目录开发，或让 Codex/IDE 自动为任务创建隔离开发区。一次授权仅适用于明确任务和范围。
- 不使用 reset/clean 掩盖未提交修改；切换前保存 Checkpoint 并确认 Trello 状态。
- 开发过程中需要生成的临时文件统一存放在仓库根目录 `dev-temp/`，可按任务分子目录；不得散落在仓库其他目录或仓库外。该目录整体加入 `.gitignore`，不得强制提交其中内容。
- **`dev-temp/` 必须可随时整体删除，且不得成为任何正式依赖。** 目录外的源码、资源、规则、配置、脚本以及构建、运行、测试、调试和持续开发流程均不得引用或依赖其内部文件；在目录不存在或被清空时，项目仍须能正常还原依赖、构建、运行、测试、调试及开发。需要长期使用的成果必须先移至相应的正式项目位置并按正常规则纳入版本控制，再由正式文件引用。
- 临时文件包括一次性分析、草稿、测试或验证过程记录、调试脚本、日志及其他中间产物。无长期用途的工作产物不得作为正式文件进入 Git；不必落盘的信息直接记录在任务管理系统、PR 或对话中。
- 持续使用的正式项目文件按实际职责保存在对应目录，并纳入版本管理。项目指令、规则、Skills 和工作流配置（如 `AGENTS.md`、`rules/`、`.agents/`、`.github/`）属于正式项目资产，不应被当作临时产物。普通脚本不享有目录级保留例外，是否保存取决于真实的持续用途。
- 对现有文件提出清理性删除之前，先按编号向用户列出相对路径、文件内容及存在原因、建议删除原因；取得对具体对象的明确批准后方可删除或取消追踪。文件的创建者、名称或所属目录不能单独决定是否删除。
- Skills 入口：`.agents/skills/grilling/SKILL.md`、`.agents/skills/grill-with-docs/SKILL.md`、`.agents/skills/domain-modeling/SKILL.md`、`.agents/skills/gstack/SKILL.md`。
- 需求/设计不明确先访谈；Bug 先记录 GitHub Issue，再按关联文档访谈；复杂审查可调用 gstack。任何 Skill 与授权、工作区或仓库规范冲突时服从更高规则。
