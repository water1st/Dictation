# Development Workflow Rules

> Task 管理和版本批次见 `rules/project-management.md`。正式文档规范见 `rules/design-documents.md`。

## Authorization and Scope

- 开始修改前读取真实 Task、引用的权威资料、实际 Git 状态，确认用户已批准执行。
- 在原仓库目录（或当次用户明确授权的工作区）切换专用分支。新增工作区需逐次用户明确授权；禁止擅自创建 Worktree、Clone、IDE/Agent 临时工作区。
- 仅实施 Task Goal 与 Acceptance Criteria 要求的文件修改；不顺手修复无关业务或调整发布流程。

## Git Traceability

- 开发分支格式 `codex/<feature|fix|opt|verify>/<task-id>/<slug>`，本项目最终 PR 目标 `master`，本项目 PR 目标是 `master`，架构与构建环境以当前仓库的实际代码及已批准的规范为准。
- 每次 Commit 的摘要末尾包含唯一拥有者 ID：`<中文动作句> [task-Dictation_xxxxxxxxxx]`。任务 ID 不放入 conventional-commit 的 scope 中。
- 普通 Task 一个分支、一个 PR；父子组仅父任务分支和 PR。子任务 Commit 只标该子 ID，PR 标题只标父 ID，PR 正文列明子任务 ID。
- 不强推、直接合并、覆盖用户未提交文件，不直接修改主分支。

## TDD for Code

- 对有可验证业务行为的代码改动，先在完整的最小垂直切片上写测试，覆盖正常、边界、非法输入、异常和状态变化。
- RED 必须成功编译并执行预期测试，因行为断言失败；编译失败、零测试或环境错误不算 RED。
- RED 后冻结正确测试；先让生产代码使同一批测试 GREEN。测试本身错误时，作废该轮证据，修正并重新冻结后从 RED 开始。
- 每个业务切片按 Dictation 已批准流程做两次中文 Commit：行为 GREEN 证据后一次；集成穿透和相关构建/测试/冒烟全部通过后一次。
- 采用现有 `Dictation.Tests` **xUnit v3 测试项目**与 Microsoft.Testing.Platform。目标参考分支可使用：
  `dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 2108 --max-threads 4`。
  测试项目现已存在于 `master`；实际执行命令时应先确认构建与测试基线。冻结与测试数量阈值按有效基线和有据可查的合法变化处理。
- 单纯 Rules、文档、Skills、配置修改不制造 RED 测试；采用静态检查、内容与引用核对、适用构建及前后回归。

## Checkpoint, Suspension and Resume

- 抢占或挂起前核对 Checklist、保存当前分支修改和 Task 相关 Checkpoint Commit；父子组记录父 ID、当前子 ID、Commit SHA、未完成工作与恢复入口。
- 确认当前变更已安全保存、Trello `掛起` 状态同步成功，才在**原目录**切换另一个任务分支。
- 不擅自调用 `git reset --hard`、`git clean` 或删除未跟踪文件。发现文件被其他任务修改，先核对来源，不覆盖。
- 恢复时读取 Task、Checkpoint、分支和实际 Commit，结合未提交 Diff 判断真实进度；Checklist 不是代码事实的替代品。

## Task 验证与状态边界

- 开始实际修改前 Task 必须处于已同步的 `開發中`。代码开发的适用 RED/GREEN、构建与测试以及文档源一致性检查属于此阶段。
- 交付项实现完毕并通过开发阶段检查后才进入 `測試中`。在 `測試中` 对 Expected Result 和 Acceptance Criteria 作最终验证，失败则先同步返回 `開發中` 再修复。
- 最终验收完成，确认没有尚未修复的阻碍后才进入 `PR` 并交付审查。评审需要修改时，先同步返回 `開發中`。
- 普通 Task 和父 Task 只在对应 PR 实际合并后进入 `已完成`；已完成的子任务可按父子任务规则例外处理。
- Task Checklist 只记录特有交付成果；通用流程步骤不纳入 Checklist 进度。未验证项按实报告，不能假称通过。
- 正式发布需要用户明确单独授权；普通 PR、push 与 merge 均不创建 Release 或发行资源。

## Verification, PR and Completion

- 业务开发完成适用的测试、构建、冒烟后移入 `測試中`，最终验收通过后进入 `PR`，提交带证据的待审查 PR。
- 正式文档：核验语义和约束、最终状态、关联引用、文档版本以及变更范围。
- Skills：检查相对文件引用、许可证、依赖和实际调用可用性；无法验证不得声称完整。
- PR 明确变更范围、真实执行的验证命令、未验证项目和限制。涉及 UI 行为时按实际要求提供截图。
- 合并成功才完成父 Task（子 Task 的内部完成遵守父子组规则）；不得把 PR 创建或关闭当成合并。
- 自动化应确认真实合并、仓库、目标分支、完整 Task ID、唯一卡片及合法状态；有异常则不变更任何卡片。
