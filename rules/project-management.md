# Project Management Rules

> Trello 是仓库修改 Task 的生命周期和任务执行元数据权威；代码、正式设计和 Git 历史分别保留自身权威。读取本文件时始终遵守 `/AGENTS.md`。

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

- 项目根目录 `ProjectInfo.json` 的 `programVersion` 是 Program Version 权威值，初始化为 `2.0.0`；本版本不因本次纯规则/元数据初始化自动增加。
- 项目自有代码 Task 在排期或开始前确定 Target Program Version。多个 Task 可以共用一个计划批次；新批次通常递增最末段（如 `2.0.0 → 2.0.1`），除非用户批准其他版本。
- 未排期 P0 代码抢占任务归当前有效批次。父子共享版本；版本转换必须校验当前值、有效的批次授权和顺序，禁止擅自回退或跳版本。
- 纯 `AGENTS.md`、`rules/*.md`、`.agents/skills/**`、普通配置和 `.github/**` 修改不消耗 Program Version。正式设计文档按 `rules/design-documents.md` 的独立 Document Version 规则执行；混合 Task 分别适用。
- 不用 Program Version 假定 SDK 精确版本或自动覆盖另一个已批准的构建/发布版本机制。

## Context and Acceptance

- Task 引用现有权威规范和源码，不复制通用规则或全篇规格；必须写清当前状态、预期结果、可验证标准、特定执行步骤与 Checklist。
- 未经当前用户批准，不把设计资料自动转为实现 Task。每个 Task 即使失去本次聊天上下文仍必须可独立执行。
- Trello 的真实状态、Task Checklist、Git 记录与实际 Diff 应互相核对；不能凭聊天记忆推断完成情况。
