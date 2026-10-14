# Dictation · Repository Agent Rules

> 本仓库的权威项目身份和项目程序版本位于根目录 `ProjectInfo.json`。当前提交分支基于 `master`；经批准的目标技术架构参考 `codex/dictation-refactor`。参考分支不是 PR 目标，且其中的代码不代表 `master` 已经实现的功能。

## Non-Negotiables

- 仓库文件修改必须先有有效 Trello Task ID 和用户授权；讨论、审计、需求澄清及草案计划本身不自动创建 Task。
- 使用 Development Hub 统一管理任务；不同项目允许并行。同一项目限制一个逻辑任务交付单元，父任务和当前子任务视为一个单元。
- 每张 Task 必须关联正确的项目标签，名称必须等于 `ProjectInfo.json.projectName`，同一项目颜色固定，不同项目颜色不同。
- 所有开发任务分支以 `codex/` 开头，由任务分支向 `master` 提交 PR。不得直接提交到 `master` 或擅自合并 PR。
- 默认只在项目原始 Git 工作目录通过切换分支开发；未经用户对当次任务的明确授权，禁止新建 Worktree、额外 Clone、复制源码或使用 Agent/IDE 隔离开发工作区。不得擅自丢弃未提交修改。
- C# 开发者手写代码严格一文件一类型（类、接口、结构体、枚举、记录、委托），不得以嵌套类型规避。禁止 Primary Constructor 和 Top-Level Statements；入口使用显式 `Program.Main`。框架生成代码按生成器规则处理。
- 对正式文档的修改必须先理解最终设计意图，正文直接描述生效状态。用户原话、背景故事、讨论原因、修改轨迹、已废弃的设计不属于正式设计定义；不得未经确认将示例和设想写成规则。
- 对不确定的需求调用适用的仓库 Skills 访谈用户；发现可以独立查明的技术事实应自行查证。Skills 不得越过用户授权、Task 约束或本文件。
- SDK 的精确补丁号不是硬性兼容门槛；兼容且经实际验证的较新 SDK 可以直接使用。区分目标框架、SDK 和项目程序版本。
- 保留 Dictation 已批准的 .NET 10、WPF、SQLite/Dapper、xUnit v3 等目标架构和业务职责；当前 `master` 与参考分支的差异必须实事求是，不得擅自实施重构。
- 不提交秘密、Trello Board ID 或 Label ID；GitHub Secrets 只在获准的第二阶段接入。
- 修改必须遵守当前 Task 的范围、Acceptance Criteria、相关权威文件及未提交工作状态。避免无关整理和推测性实现。

## Rule Priority

1. 本文件 Non-Negotiables。
2. 与当次任务最直接相关的 `rules/*.md`。
3. 本文件其他指南。
4. 环境提供的全局 Agent 规则。
5. 仓库 Skills 的工作指引；与上述规则冲突时必须服从高优先级规则。

## Rule Routing

- Trello、项目归属、入库、状态、父子任务、版本批次：`rules/project-management.md`。
- 分支、Checkpoint、TDD、验证、提交与 PR：`rules/workflow.md`。
- 技术选型、项目/模块目录、依赖边界、数据库表结构：`rules/architecture.md`。
- C# 风格、命名、接口、类型和依赖编写：`rules/code-style.md`。
- SDK 兼容、开发环境、外部工具及工作区约束：`rules/tooling.md`。
- 正式文档、需求访谈、领域术语、版本、删改和验收：`rules/design-documents.md`。

只加载与工作直接相关的 Rules，不假定文件中存在未读取的规则。若 `CONTEXT.md` 存在，使用其中已确定的领域术语。Task 是执行入口和上下文索引，业务事实以实际规范、文件和源码为权威。

## Skills Routing

- 需求不清楚、需要逐层确认决策：`.agents/skills/grilling/SKILL.md`。
- 需要结合权威资料和术语审问需求、处理较复杂 Bug：`.agents/skills/grill-with-docs/SKILL.md`。
- 修改领域术语、术语表或重要架构决策：`.agents/skills/domain-modeling/SKILL.md`。
- 专项计划/代码/QA/安全审查按可用环境使用 `.agents/skills/gstack/SKILL.md`。不能以工具不可用为由自行搭建额外工作区或安装全局组件。

## Working Mode

1. 理解请求、读取相关文件和规则，区分调查事实与待决选择。
2. 对复杂或不明确需求调用 Skills，提出有依赖顺序的问题；获得用户对最终方案的确认。
3. 只在已批准的可执行工作需要修改仓库时创建/读取相应 Trello Task；先检查项目、标签、ID、优先级、状态和执行槽。
4. 在原目录创建/切换 Task 分支；父子任务遵守同分支同 PR 和独立 Commit ID。
5. 按任务性质执行必要的 TDD、文档修改、构建、测试、审查和版本操作。
6. 检查真实 Diff、Git 状态、Trello Checklist、版本和验收要求；提交并创建待审查 PR，不自行合并。
7. 报告实际完成项、验证结果和未解决阻碍；不声称未实际运行的测试通过。
