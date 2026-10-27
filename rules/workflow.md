# Development Workflow Rules

> Task 管理和版本批次见 `rules/project-management.md`。正式文档规范见 `rules/design-documents.md`。

## 必须遵守的执行顺序

- 每个 Task 都必须按本文件与 `rules/project-management.md` 中适用的前置条件、开发、验证、PR 和完成步骤依次执行；各阶段的状态同步与准入检查不得自行省略、倒置或以口头判断代替。纯文档或规则 Task 使用其对应验证要求，不套用无关代码阶段。
- 某一步骤无法执行或验证失败时，停止进入下一阶段，记录具体障碍；只有用户明确批准调整该步骤后才能改变既定流程，不能自行宣布通过。

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

- 新功能、新增业务行为及缺少有效测试覆盖的行为修复，必须在完整的最小垂直切片上先编写测试，覆盖适用的正常、边界、非法输入、异常和状态变化，并完成 RED→GREEN。
- 已针对同一行为完成过有效 RED→GREEN 的测试，若断言仍正确且覆盖当前改动，可直接用于后续 Debug、Bug 修复、重构和回归验证；正常执行并确认通过即可，不得仅为重演 RED 而修改测试或生产代码。
- 快速 Debug、定位问题不强制新增单元测试或制造 RED；最终交付的行为修复仍须通过适用的测试。既有测试不覆盖目标行为或预期行为已变更时，新增或修正测试并完成有效 RED→GREEN。
- 首次执行 RED→GREEN 时，RED 必须成功编译并执行预期测试，因行为断言失败；编译失败、零测试或环境错误不算 RED。
- 需要首次执行 RED→GREEN 的测试在 RED 前冻结正确断言；API 变更所需的测试编译适配须在冻结前完成，不得在 GREEN 阶段修改期望值迎合实现。若测试本身错误，作废本轮证据，修正后重新冻结并执行 RED→GREEN。
- 对需首次执行 RED→GREEN 的业务垂直切片，严格执行两次中文提交：先在可编译的最小生产骨架上冻结测试并运行行为断言失败的 RED，再仅修改生产实现使同一批测试 GREEN，首次提交；随后贯通 WPF/ViewModel、Service、DTO/Mapper、DataAccessor/PO 与数据库或外部 Provider，相关构建、测试及冒烟通过后第二次提交。复用已验证测试且无需新增 RED 的改动，不套用首次红绿阶段的提交要求。单个切片约 2–3 人/小时，每次提交只包含一个可验证工作单元，任何提交不得保留临时 RED。
- 验证命令与测试基线见本文件「环境与验证命令」。
- 单纯 Rules、文档、Skills、配置、注释及不改变行为的纯文件移动不制造 RED 测试；采用静态检查、内容与引用核对、适用构建及前后回归。映射、DI 边界及业务状态变化仍须真实行为断言验证。

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
- **Document and Rule Task Gate**：审阅完整 diff，确认只修改授权文件和章节，核对受影响术语及引用；不得加入无依据的新规则，不得让 Task 内容或聊天讨论以原文或改写形式泄漏进正式文档。Skills 修改另需检查相对引用、许可证、依赖及实际调用可用性。

## PR Gate

- 最终验证通过且卡片同步至 `PR` 后交付 PR 审阅；PR 描述写明修改范围、实际执行过的验证命令、未验证项和限制。只有对应 PR 实际合并，普通及父 Task 才能进入 `已完成`；子任务按父子组规则处理。
- 来源于 GitHub Issue 的 Fix Task 必须在 Task 记录 `Issue: #<issue-number>`，PR 描述包含对应的 `Fixes #<issue-number>`。
- 代码 Task 的 `ProjectInfo.json.programVersion` 必须等于该 Task 的 Target Program Version。
- 评审要求返工时先将卡片同步回 `開發中`，完成开发和最终验收后再回到 `PR`。

## 环境与验证命令

开发基线为 Windows 11、稳定版 Visual Studio 2026 与 .NET 10；允许使用实际兼容的较新稳定 SDK，具体规则见 `rules/tooling.md`。

```powershell
dotnet restore Dictation.sln
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 2108 --max-threads 4
dotnet run --project Dictation.Presentation/Dictation.Presentation.csproj
```

`Dictation.Tests` 使用 xUnit v3 与 Microsoft.Testing.Platform；测试方法使用英文 `Behavior_Scenario_Expected` 语义结构命名，并遵守 `rules/code-style.md` 的英文标识符要求。聚焦测试使用 `--filter-class` 或 `--filter-method`，并设置适用的 `--minimum-expected-tests`；零测试不算通过。全量测试的基线为 2108 项，不得随意降低；有意删除失效测试须说明原因及数量变化。实际运行时执行适用的构建、测试、冒烟与定向检索；提交前审阅完整 diff，不能运行的检查如实报告。


## Final Response

- 报告实际变更、涉及文件、真实运行的验证、无法验证的项目、剩余风险和待决策，以及 Trello 最终状态和同步结果。

## Checkpoint, Suspension and Resume

- 抢占或挂起前核对 Checklist、保存当前分支修改和 Task 相关 Checkpoint Commit；父子组记录父 ID、当前子 ID、Commit SHA、未完成工作与恢复入口。
- 确认当前变更已安全保存、Trello `掛起` 状态同步成功，才在**原目录**切换另一个任务分支。
- 不擅自调用 `git reset --hard`、`git clean` 或删除未跟踪文件。发现文件被其他任务修改，先核对来源，不覆盖。
- 恢复时读取 Task、Checkpoint、分支和实际 Commit，结合未提交 Diff 判断真实进度；Checklist 不是代码事实的替代品。
