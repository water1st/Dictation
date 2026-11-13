# Development Workflow Rules

> Task 管理和版本批次见 `rules/project-management.md`。正式文档规范见 `rules/design-documents.md`。

## 必须遵守的执行顺序

- 每个 Task 都必须按本文件与 `rules/project-management.md` 中适用的前置条件、开发、验证、PR 和完成步骤依次执行；各阶段的状态同步与准入检查不得自行省略、倒置或以口头判断代替。纯文档或规则 Task 使用其对应验证要求，不套用无关代码阶段。
- 某一步骤无法执行或验证失败时，停止进入下一阶段，记录具体障碍；只有用户明确批准调整该步骤后才能改变既定流程，不能自行宣布通过。

## Authorization and Scope

- 开始修改前读取真实 Task、引用的权威资料、实际 Git 状态，确认执行授权；已进入 `待開發` 的 Task 按 `rules/project-management.md` 视为已授权，不逐项再次申请批准。
- 在原仓库目录（或当次用户明确授权的工作区）切换专用分支。新增工作区需逐次用户明确授权；禁止擅自创建 Worktree、Clone、IDE/Agent 临时工作区。
- 仅实施已批准 Goal 与 Acceptance Criteria 所必需的改动；不得顺手修复、重构、优化、清理、重命名或格式化无关代码、规则、测试、配置，也不得调整无关发布流程。发现无关问题只报告，未经用户明确批准或另建 Task 不得并入。

## Task 开始前的 Git 强制同步

每次开始新的、已获授权的 Task，在创建工作分支或修改任何仓库文件之前，必须在原仓库工作区依次执行：

1. 核实 Task 执行授权、所需 Trello 状态及当前 Git 工作区状态。
2. 执行 `git switch master`，切换至本地主分支。
3. 执行 `git pull --ff-only origin master`，同步本地主分支。
4. 执行 `git fetch origin --prune`，清理失效的远程跟踪引用。
5. 从已同步的本地 `master` 创建符合现有命名规则的 Task 工作分支。
6. 上述步骤全部成功后，才允许正式修改文件并执行 Task。

每个新 Task 均须执行，不得以本地已有主分支或此前同步过为由跳过。
`git fetch origin --prune` 只清理远程跟踪引用；不得自动删除或清理任何本地工作分支，不得丢弃未提交修改。

切换、拉取、获取更新或其他前置条件失败时，必须停止创建工作分支及后续开发并报告阻塞。
恢复已开始或挂起的 Task 时，使用原工作分支和 Checkpoint，继续遵守既有挂起与恢复规则；不得以新 Task 同步流程替代恢复流程。

## Git Traceability

- 开发分支格式 `codex/<feature|fix|opt|verify>/<task-id>/<slug>`，PR 目标分支为 `master`。
- 每次 Commit 的摘要末尾包含唯一拥有者 ID：`<中文动作句> [task-Dictation_xxxxxxxxxx]`。任务 ID 不放入 Conventional Commit 的 scope；分支名只用英文/ASCII，Commit 消息和 PR 标题、描述的自然语言使用简体中文，技术标识符保持原样。
- 普通 Task 一个分支、一个 PR；父子组仅父任务分支和 PR。子任务 Commit 只标该子 ID，PR 标题只标父 ID，PR 正文列明子任务 ID。
- 不强推、直接合并、覆盖用户未提交文件，不直接修改主分支。每次提交只包含一个可验证的工作单元。

## TDD for Code

- 新功能、新增业务行为及缺少有效测试覆盖的行为修复，必须在完整的最小垂直切片上先编写测试，覆盖适用的正常、边界、非法输入、异常和状态变化，并完成 RED→GREEN。
- 新增或修改测试时，先确定被测行为、输入、预期结果和正确断言，完成测试代码、初始化、依赖及必要的 API 编译适配；确认生产和测试目标可成功编译、链接，测试可被发现并正常执行，再冻结测试代码及影响其执行结果的测试依赖。
- 有效 RED 必须是冻结后的测试正常执行，因被测生产行为不满足正确断言而失败，且失败信息能对应目标行为。编译或链接失败、测试未发现或未执行、测试自身代码或断言错误、初始化、模拟对象、测试框架或环境异常均不算 RED。
- RED 后保持冻结测试代码及依赖不变，仅修改生产实现，使同一测试的正确断言通过，并运行相关回归确认 GREEN。冻结后只要修改对应测试代码或影响其执行结果的测试依赖，原 RED→GREEN 证据即失效；必须使用最终测试版本重新冻结并完成有效 RED→GREEN。不得修改正确断言或故意破坏正确生产实现来制造 RED。
- 已存在、未修改且正确覆盖目标行为的测试可直接运行验证，不要求提供历史 RED 证据或重新制造 RED。快速 Debug 和纯定位不强制新增测试；最终行为修复仍须通过适用测试。新增或修改对应测试时执行本节 RED→GREEN。
- 对需首次执行 RED→GREEN 的业务垂直切片，严格执行两次中文提交：先在可编译的最小生产骨架上冻结测试并运行有效 RED，再仅修改生产实现使同一批测试 GREEN，首次提交；随后贯通 WPF/ViewModel、Service、DTO/Mapper、DataAccessor/PO 与数据库或外部 Provider，相关构建、测试及冒烟通过后第二次提交。复用已有正确测试且无需新增 RED 的改动，不套用首次红绿阶段的提交要求。单个切片约 2–3 人/小时，每次提交只包含一个可验证工作单元，任何提交不得保留临时 RED。
- 完整测试命令与最终验收要求见本文件「环境与验证命令」。
- 单纯 Rules、文档、Skills、配置、注释及不改变行为的纯文件移动不制造 RED 测试；采用静态检查、内容与引用核对、适用构建及前后回归。映射、DI 边界及业务状态变化仍须真实行为断言验证。

## Task Execution Rules

### Pre-Execution Checklist Completeness Gate

- 开始或恢复任何 Task 前，对照已批准 Goal、Acceptance Criteria 和权威引用逐项检查原生 Trello Checklist：必须覆盖具体类或类组、功能模块、SQLite 迁移、UI 组件、文档单元及特有验证成果；相干单元可合并，普通成员方法不需要逐项拆分。
- 每项必须在 Execution Plan 中有同名或精确引用的 HOW：输入、职责、关键方法/接口、顺序、约束、产出与验收；描述栏中的 `Checklist:` 不是原生清单。拒绝空泛任务阶段、遗漏单元和未经批准的设计。恢复时核对 Git、实物及勾选情况。
- 优先最小且高置信度的修改；未经批准不得改公共 API、配置键、数据库表或字段、迁移，也不得引入第三方依赖；不为假想的未来需求增加投机性抽象。

## Final Task Verification

- `開發中` 转 `測試中` 前，所有原生 Checklist 具体单元及逐项验收必须真实完成，开发阶段适用检查均已通过；父子组先验证子 Task 及父任务自身交付，再验收父任务集成结果。
- `測試中` 在当前占用的工作空间根据 Expected Result 与 Acceptance Criteria 做最终验证；失败时先同步返回 `開發中` 再修复。
- **Code Task Gate**：适用 TDD、.NET 构建、xUnit、针对性冒烟；需要视觉确认时可按需截图验证，不要求截图交付。未运行的检查不得声称通过。
- **全量自动化测试门禁**：每个 Task 在最终验收和 PR 前，必须先完成当前解决方案的适用构建，再执行「环境与验证命令」中不带筛选条件的 `Dictation.Tests` 全量测试；核对实际发现及执行的测试数、通过／失败／跳过数量、完整运行结果和退出状态。测试数量由当前项目实际发现结果决定，不维护固定数量基线。测试未完成、失败、发现零测试或未经核实的跳过，不能作为通过证据；无法执行须明确报告未验证，不得声称全量测试通过。规则或文档修改不制造 RED，但仍须执行本项最终测试。
- **Document and Rule Task Gate**：审阅完整 diff，确认只修改授权文件和章节，核对受影响术语及引用；不得加入无依据的新规则，不得让 Task 内容或聊天讨论以原文或改写形式泄漏进正式文档。Skills 修改另需检查相对引用、许可证、依赖及实际调用可用性。

## PR Gate

- 最终验证通过且卡片同步至 `PR` 后交付 PR 审阅；PR 描述写明修改范围、实际执行过的验证命令、未验证项和限制。只有对应 PR 实际合并，普通及父 Task 才能进入 `已完成`；子任务按父子组规则处理。
- 来源于 GitHub Issue 的 Fix Task 必须在 Task 记录 `Issue: #<issue-number>`，PR 描述包含对应的 `Fixes #<issue-number>`。
- 代码 Task 的 `ProjectInfo.json.programVersion` 必须等于该 Task 的 Target Program Version。
- 评审要求返工时先按 `rules/project-management.md` 取得空闲工作空间并将卡片同步回 `開發中`，完成开发和最终验收后再回到 `PR`。

## 环境与验证命令

开发基线为 Windows 11、稳定版 Visual Studio 2026 与 .NET 10；允许使用实际兼容的较新稳定 SDK，具体规则见 `rules/tooling.md`。

```powershell
dotnet restore Dictation.sln
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --max-threads 4
dotnet run --project Dictation.Presentation/Dictation.Presentation.csproj
```

`Dictation.Tests` 使用 xUnit v3 与 Microsoft.Testing.Platform；测试方法使用英文 `Behavior_Scenario_Expected` 语义结构命名，并遵守 `rules/code-style.md` 的英文标识符要求。开发阶段可使用 `--filter-class` 或 `--filter-method` 进行定向测试，但最终验收必须在本次修改对应的构建成功后，执行上述不含任何筛选器的全量测试命令，覆盖当前测试项目发现的全部适用测试，并核对发现、执行、通过、失败、跳过数量及退出状态；零测试不算通过。全量测试不采用固定测试数量基线，也不要求每个 Task 更新数量基线。未经当前任务明确授权，不得删除现有有效测试、删减正确断言、删除测试数据用例，或通过 Skip、禁用、过滤、修改测试发现配置等方式隐藏失败；提交前审阅完整 diff，核对测试代码、参数化数据源及发现配置的变化。经批准的测试增删或调整必须核实原因、实际覆盖与数量变化，并在 PR 说明。实际运行时执行适用的构建、全量测试、冒烟与定向检索；无法运行的检查如实报告，不视为通过。


## Final Response

- 正常完成且 PR 成功创建或更新后，只回复“已完成。”和 PR URL，不附截图、测试日志、验收证据或过程说明。任务未完成、必要验证未通过或 PR 未交付时，简要报告真实状态及阻塞，不得宣称完成。

## Checkpoint, Suspension and Resume

- 抢占或挂起前核对 Checklist、保存当前分支修改和 Task 相关 Checkpoint Commit；父子组记录父 ID、当前子 ID、Commit SHA、未完成工作与恢复入口。
- 确认当前变更已安全保存、Trello `掛起` 状态同步成功，才在**原目录**切换另一个任务分支。
- 不擅自调用 `git reset --hard`、`git clean` 或删除未跟踪文件。发现文件被其他任务修改，先核对来源，不覆盖。
- 恢复时读取 Task、Checkpoint、分支和实际 Commit，结合未提交 Diff 判断真实进度；Checklist 不是代码事实的替代品。
