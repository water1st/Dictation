# Project Management Rules

> Trello 是仓库修改 Task 的生命周期和任务执行元数据权威；代码、正式设计和 Git 历史分别保留自身权威。读取本文件时始终遵守 `/AGENTS.md`。

## Sources of Truth

- Trello 是 Task 生命周期状态和任务特有执行记录的权威；`AGENTS.md` 与 `rules/*.md` 是工程约束和工作流权威；仓库是实际已实现内容的权威；`docs/` 下已批准的正式需求与设计文档是批准需求的权威。
- Task 只记录该交付单元的现状、目标、特有实施方法、约束及完成证据；已有通用规则和权威业务事实应准确引用，不复制为另一套定义。
- Trello 卡片与已批准设计、规则或仓库实际状态发生实质冲突时，停止修改并报告冲突。

## State Principle

- 正常生命周期为 `需求池 → 已確認／待排期 → 待開發 → 開發中 → 測試中 → PR → 已完成`，`掛起` 和 `廢棄` 是侧状态。目标状态是开展该阶段工作的前置条件：先移动并确认同步，再执行相应工作；`廢棄` 和 `已完成` 遵守各自收尾程序。
- `需求池`：存在已拆分但尚未进入近期排期的任务；批准的可执行计划可直接形成任务，不把未经批准的资料直接变成任务。
- `已確認／待排期`：选入近期候选队列，仍需确定顺序、版本、目标和执行依据；此状态不授予修改仓库的权限。
- `待開發`：已获实施授权、执行资料齐全，但尚未开始改文件。
- `開發中`：实际实施及开发阶段检查；首次改文件或评审返工前必须已同步到此列。
- `測試中`：交付单元完成开发阶段检查后，对 Expected Result 与 Acceptance Criteria 做最终验收；失败须先返回 `開發中` 再修正。
- `PR`：最终验收通过，进入 PR 审阅与合并阶段；评审要求改文件时先返回 `開發中`。
- `已完成`：普通或父任务的 PR 已真实合并；子任务按父子组独立验收例外。
- `掛起`：已开始的任务临时中断，先保存 Checkpoint、同步 Checklist 与 Git 状态，再移动并核验；恢复时依实际记录返回原状态。
- `廢棄`：停止执行，确认状态同步后才在授权范围内清理；保留历史与 Task ID，不重用。
- 不为凑足状态流转而虚假移动卡片。执行槽仍以 Project 为单位，父任务及当前子任务为同一逻辑单元。

## State Synchronization

- 任何状态同步失败，都不得开始该目标状态的工作；如实报告失败和部分成功的操作。
- 父子组的状态切换必须核对所有受影响卡片及原生 Checklist，部分成功不能表述为整体同步完成。
- 禁止用 Markdown TODO、状态文件或其他第二套跟踪替代 Trello。

## Task Creation and Card Content

- Task 必备字段依 `Task Identity` 的完整字段清单；约束、依赖只在实质影响执行时填写，不用空白模板。
- **Final-State Input Filter**：用户对话仅作为输入；写 Task、设计或规格前筛选最终生效决定、当前约束与已核验事实，排除历史、抱怨、疑问、举例、推理、假设和被否决方案；不通过改写保留已淘汰内容，未定事项不得伪装成已批准。
- **Current State and Context References**：Current State 指向实际代码、正式资料和当前状态；仓库引用使用相对路径与稳定章节或符号，行号仅作辅助；已有内容准确引用，不整段复制。无任务专属上下文则填 `None`。
- **Expected Result**：写成可以客观检验的最终结果及 Acceptance Criteria，不把通用项目流程重复作为任务验收。

## Execution Plan and Checklist

- 每个 Task 必须有真实的原生 Trello Checklist；卡片描述出现 `Checklist:` 文字不算原生 Checklist。
- 写入前先清点 Goal、Acceptance Criteria 和已批准资料所需的全部具体交付单元，按类、紧密相关的类组、功能模块、数据库迁移、UI 组件或可核验文档改动列项；不把普通成员方法独立拆项。
- **Coverage test**：接手者能从条目和引用判断哪些具体单元尚未完成；“实现系统”“完成功能”“测试全部”等抽象阶段用语不合格；不增添未经批准的单元。
- **Method mapping**：Execution Plan 必须逐项对应 Checklist 的名称或明确引用，提供输入、职责、相关方法或接口、交互顺序、约束、产出及完成验证；复杂方法细节写在计划中，不把隐藏交付内容埋入长篇文字。
- 仅示意，不是已批准架构，也不限定条目数量：`Service + DTO/Mapper` 可作一个相干单元，`DataAccessor + SQLite 数据库迁移` 可作一个单元，`ViewModel/View` 绑定可作一个单元，xUnit 行为验证可针对具体业务单元。实际名称、拆分与数量以批准内容为准。
- 每项产生并核验相应结果后才勾选；父卡的子项必须准确引用子 Task ID。创建分支、Commit、PR、Trello 状态与常规构建测试等通用流程不得成为 Checklist 项。

## Context Independence Gate

- 进入 `待開發` 或 `開發中` 之前，假设新执行者既没有聊天历史也没有模型记忆。仅凭 `AGENTS.md` 所路由规则、Task 及其准确引用、当前 Git 分支与历史，必须能识别现状、目标、实施方法、约束、每项具体交付和验收标准。
- 如必须猜测缺失的工作单元或 HOW，Task 不完整；同时必须排除对话历史、失效决定及未经批准的推测。

## Project Identity and Label

- 本仓库项目为 `Dictation`，GitHub 为 `water1st/Dictation`，Trello 看板名称为 `Development Hub`，PR 目标 `master`；读取 `ProjectInfo.json` 进行核对。
- 每个项目有且仅有一个与 `projectName` 完全相同的项目标签，其颜色由 `ProjectInfo.json.trello.projectLabel.color` 固定保存。不同项目不得复用同一颜色，且应避免与问题状态标签混淆。
- 新项目首次加入时应在看板中检查现有项目标签及已占用颜色，随机选择未占用且易辨识的 Trello 合法颜色，并保存；后续复用现有标签与配置，不重新随机分配。颜色无法核验或撞色时停止，不创建同名或错色标签。
- 每张卡片必须有且只有一个项目身份标签。状态标签可以叠加：`Blocked`（红色，当前阻塞）与 `Delayed`（紫色，曾发生延误）。
- 禁止把 Board ID、Label ID、API Key 或 Token 放入公开仓库。通过授权的 Trello 工具或受保护的执行配置解析实际资源，项目/看板查找必须精确且唯一。

## Task Identity

- 格式 `task-{ProjectName}_{10位随机小写十六进制}`，正则 `^task-(?<project>[A-Za-z0-9-]+)_(?<id>[0-9a-f]{10})$`。
- Task ID 不使用日期或计数器，不重复、不更改、不复用；检查同一管理范围中的冲突。
- Trello 卡片标题末尾使用 `[task-Dictation_xxxxxxxxxx]`；Git 分支、Commit、PR 使用同一个 ID，父子任务例外见下文。
- Task 必须标识 Project、Repository、Board、Type、Priority、Scope、Target Version(s)、Parent Task（适用时）、Goal、Current State、Expected Result、Acceptance Criteria、Required Context References、Execution Plan、Checklist。

## When Tracking Begins

- 纯讨论、访谈、审计、复核、研究和未经批准的计划不因其存在而自动建立 Task。
- 用户批准的计划若已包含可执行任务划分，按原划分入 `需求池`，不二次机械拆分；仅在用户要求拆分时，把未拆分计划细化为 Task。
- 任何即将发生的仓库修改，无论大小，必须在首次修改前拥有明确且完整的 Task；只为状态流转、普通 PR 操作或 Checklist 管理不得额外制造业务 Task。
- 用户授权直接实施，Task 可按授权进入 `待開發` 或直接进入 `開發中`。不得把需求讨论视为实施授权。
- 新 Bug 先检查并记录 GitHub Issue（复用重复 Issue），调用 `grill-with-docs` 核查现象、预期、设计与修复方向；确认修复方案后创建关联 Issue 的 Fix Task。纯讨论不额外创建开发 Task。

## Lifecycle and Scheduling

- 正常生命周期：`需求池 → 已確認／待排期 → 待開發 → 開發中 → 測試中 → PR → 已完成`；`掛起`、`廢棄` 是侧状态。仅按真实工作证据转换，允许合理的直接入列，不为了凑流程虚假搬动。
- P0–P3 优先级、任务类型 Feature/Fix/Optimization/Verification；同优先级按进入排期队列的顺序 FIFO。
- P1–P3 一般不得抢占正在执行的任务；获批准的 P0 可抢占低优先级任务。相同级别 P0 不自动相互抢占，用户可显式批准其他抢占。
- **执行槽以 Project 为单位**：不同 Project 的任务可同时处于 `開發中`、`測試中` 或 `PR`。同一 Project 同时最多一个逻辑交付单元占用上述链路。项目 A 的队列、阻塞、抢占、挂起与版本批次不影响项目 B。
- `Blocked` 标记当前阻塞；外部耽误标记 `Delayed`。状态标记不代替生命周期列表。挂起须先保存 Checkpoint、Checklist 和恢复所需的 Git 状态。

## Parent and Child

- 父 Task 与各子 Task 分别持有唯一不可变 ID。子卡在 `Parent Task` 中仅填写父 ID；父卡 Checklist 含子项名称及完整子 ID。
- 整个父子组是一个逻辑执行单元，共用**父 Task 的分支和单一 PR**；子任务提交的 Commit 使用子 ID，父任务自身工作使用父 ID。
- 父任务与当前一个子任务可同时处于 `開發中`；不允许两个子任务并行进入开发/测试。
- 子任务已完成则勾选父 Checklist 项。子任务被砍掉则从父 Checklist **删除**对应项（不勾选、不挤占进度）。挂起、恢复按父子整体 Checkpoint。父任务废弃则全部子任务废弃，保存历史卡片。
- 当 Trello 工具不支持删除 Checklist Item 时，报告阻碍，不以勾选完成替代删除。

## Program and Document Versions

- 项目根目录 `ProjectInfo.json` 的 `programVersion` 是 Program Version 权威值。
- 项目自有代码 Task 在排期或开始前确定 Target Program Version。多个 Task 可以共用一个计划批次；新批次通常递增最末段（如 `2.0.0 → 2.0.1`），除非用户批准其他版本。
- 未排期 P0 代码抢占任务归当前有效批次。父子共享版本；版本转换必须校验当前值、有效的批次授权和顺序，禁止擅自回退或跳版本。
- 纯 `AGENTS.md`、`rules/*.md`、`.agents/skills/**`、普通配置和 `.github/**` 修改不消耗 Program Version。正式设计文档按 `rules/design-documents.md` 的独立 Document Version 规则执行；混合 Task 分别适用。
- 不用 Program Version 假定 SDK 精确版本或自动覆盖另一个已批准的构建/发布版本机制。

## Task 内容与 Checklist

- Task 的 Execution Plan 和 Checklist 仅记录该任务节点的特有交付及必要实施方法；不能复制通用项目管理规范或开发流程。
- Checklist 只列可核验的任务特有交付项，例如实际文档、工具脚本、功能模块和父卡直接子任务 ID。
- 创建 Task、创建/切换分支、提交 Commit、创建/更新 PR、变更 Trello 状态、一般构建测试与常规验收属于统一工作流，不得列为 Task Checklist 项。
- 不按开发阶段拆分同一个 Task 的 Checklist，不创建重复、空白或仅表示普通工作流状态的 Checklist。
- Task-specific Acceptance Criteria 表示交付验证结果；一般性测试和 PR 准入不复制到每个 Task。
- 父卡中子任务仅出现一次，子任务的具体工作留在子卡。废弃子任务时删除父卡对应项，不以勾选模拟完成。
- Required Context References 使用仓库相对路径与稳定章节/代码符号，避免复制已有大段文档。
- Tool 无法执行真实的删除/修改时报告阻碍，不以重命名空白条目、虚假打勾替代删除。

## Context and Acceptance

- Task 引用现有权威规范和源码，不复制通用规则或全篇规格；必须写清当前状态、预期结果、可验证标准、特定执行步骤与 Checklist。
- 未经当前用户批准，不把设计资料自动转为实现 Task。每个 Task 即使失去既往聊天上下文仍必须可独立执行。
- Trello 的真实状态、Task Checklist、Git 记录与实际 Diff 应互相核对；不能凭聊天记忆推断完成情况。
