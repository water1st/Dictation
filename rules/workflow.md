# Development Workflow Rules

> Task 管理和版本批次见 `rules/project-management.md`。正式文档规范见 `rules/design-documents.md`。

## Authorization and Scope

- 开始修改前读取真实 Task、引用的权威资料、实际 Git 状态，确认用户已批准执行。
- 在原仓库目录（或当次用户明确授权的工作区）切换专用分支。新增工作区需逐次用户明确授权；禁止擅自创建 Worktree、Clone、IDE/Agent 临时工作区。
- 仅实施 Task Goal 与 Acceptance Criteria 要求的文件修改；不顺手修复无关业务或调整发布流程。

## Git Traceability

- 开发分支格式 `codex/<feature|fix|opt|verify>/<task-id>/<slug>`，PR 目标分支为 `master`。
- 每次 Commit 的摘要末尾包含唯一拥有者 ID：`<中文动作句> [task-Dictation_xxxxxxxxxx]`。任务 ID 不放入 Conventional Commit 的 scope；分支名只用英文/ASCII，Commit 消息和 PR 标题、描述的自然语言使用简体中文，技术标识符保持原样。
- 普通 Task 一个分支、一个 PR；父子组仅父任务分支和 PR。子任务 Commit 只标该子 ID，PR 标题只标父 ID，PR 正文列明子任务 ID。
- 不强推、直接合并、覆盖用户未提交文件，不直接修改主分支。每次提交只包含一个可验证的工作单元；涉及 UI 的 PR 附前后截图。

## TDD for Code

- 对有可验证业务行为的代码改动，先在完整的最小垂直切片上写测试，覆盖正常、边界、非法输入、异常和状态变化。
- RED 必须成功编译并执行预期测试，因行为断言失败；编译失败、零测试或环境错误不算 RED。
- RED 前冻结正确测试；API 变更所需的测试编译适配须在冻结前完成，不得在 GREEN 阶段修改期望值迎合实现。若测试本身错误，作废本轮证据，修正测试并重新冻结后重新执行 RED→GREEN。
- 每个业务垂直切片严格执行两次中文提交：先在可编译的最小生产骨架上冻结测试并运行行为断言失败的 RED，再仅修改生产实现使同一批测试 GREEN，首次提交；随后贯通 WPF/ViewModel、Service、DTO/Mapper、DataAccessor/PO 与数据库或外部 Provider，相关构建、测试及冒烟通过后第二次提交。单个切片约 2–3 人/小时，每次提交只包含一个可验证工作单元，任何提交不得保留临时 RED。
- 验证命令与测试基线见 AGENTS.md「开发环境与命令」。
- 单纯 Rules、文档、Skills、配置修改不制造 RED 测试；采用静态检查、内容与引用核对、适用构建及前后回归。

## Task Execution Rules

### Pre-Execution Checklist Completeness Gate

- 开始或恢复任何 Task 前，对照已批准 Goal、Acceptance Criteria 和权威引用逐项检查原生 Trello Checklist：必须覆盖具体类或类组、功能模块、SQLite 迁移、UI 组件、文档单元及特有验证成果；相干单元可合并，普通成员方法不需要逐项拆分。
- 每项必须在 Execution Plan 中有同名或精确引用的 HOW：输入、职责、关键方法/接口、顺序、约束、产出与验收；描述栏中的 `Checklist:` 不是原生清单。拒绝空泛任务阶段、遗漏单元和未经批准的设计。恢复时核对 Git、实物及勾选情况。
- Task 执行仅限批准 Goal 与 Acceptance Criteria 所必需的改动；不得顺手修复、重构、优化、清理、重命名或格式化无关代码、规则、测试、配置。发现无关问题只报告；用户明确批准或另建 Task 前不能并入。
- 优先最小且高置信度的修改；未经批准不得改公共 API、配置键、数据库表或字段、迁移，也不得引入第三方依赖；不为假想的未来需求增加投机性抽象。

## Final Task Verification

- `開發中` 转 `測試中` 前，所有原生 Checklist 具体单元及逐项验收必须真实完成，开发阶段适用检查均已通过；父子组先验证子 Task 及父任务自身交付，再验收父任务集成结果。
- `測試中` 根据 Expected Result 与 Acceptance Criteria 做最终验证；失败时先同步返回 `開發中` 再修复。
- **Code Task Gate**：适用 TDD、.NET 构建、xUnit、针对性冒烟；涉及 UI 变更附前后截图。未运行的检查不得声称通过。
- **Document and Rule Task Gate**：审阅完整 diff，确认只修改授权文件和章节，核对受影响术语及引用；不得加入无依据的新规则，不得让 Task 内容或聊天讨论以原文或改写形式泄漏进正式文档。

## PR Gate

- 最终验证通过且卡片同步至 `PR` 后进行 PR 审阅；PR 描述写明修改范围、实际执行过的验证命令、未验证项和限制。
- 来源于 GitHub Issue 的 Fix Task 必须在 Task 记录 `Issue: #<issue-number>`，PR 描述包含对应的 `Fixes #<issue-number>`。
- 代码 Task 的 `ProjectInfo.json.programVersion` 必须等于该 Task 的 Target Program Version。
- 评审要求返工时先将卡片同步回 `開發中`，完成开发和最终验收后再回到 `PR`。

## Verification

- 实际可运行时执行相应 .NET 构建、xUnit 测试及冒烟；对更名符号和变更 API 做定向检索，提交前审阅完整 diff，记录不能运行的检查。未执行不得宣称通过。

## Final Response

- 报告实际变更、涉及文件、真实运行的验证、无法验证的项目、剩余风险和待决策，以及 Trello 最终状态和同步结果。

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
