# 独立五十音听写 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans，按已批准完整目标串行实现；不分派实现代理，全计划结束仅一次最终只读审查。Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 日语首页提供独立五十音听写，保存范围、全量／每日数量、原会话与独立 FSRS，完成纳入后续日语打卡。

**Architecture:** Presentation → Core/Kana → DataAccess/Kana。内置假名事实表不创建词库词条；复用现有判题和 FSRS，分别存储五十音计划、记忆、会话和观察。UI 使用同窗口导航和现有内存语音，OCR 输入仍留最后切片。

**Tech Stack:** .NET 10、WPF、现有 Dapper/SQLite、xUnit v3；无新增依赖。

**Spec:** `docs/requirements/RefactorSpecification.md` 第4、7、9、10节；完整目标已批准持续串行，不重启 GOAL、不重新询问已定需求。

## Global Constraints

- 主目录 Dictation、codex/ 分支、唯一 DI、Core 业务接口；不合并、推送、安装工具或污染用户 Resources。
- 至少一种字形、至少一种音类；范围含清音、浊音／半浊音、拗音。内置现代清音46、浊音／半浊音25、拗音36，双字形共214项；音类事实依据国际交流基金 Irodori，不含古假名和外来音扩展。
- `じ／ぢ`、`ず／づ`、`お／を` 等同音项及对应片假名、拗音补充行提示，不将听不出的差别判成记忆错误。
- 全量首次正确即完成该项；答错立即显示答案，间隔3–7题后重试，错误项连续两次正确才完成。剩余候选不足时提前释放，不能死锁或伪造填充题。
- 每项在一轮只更新一次独立 FSRS，该轮任意错误最终仍为 Again；浏览／TTS失败不写观察。每日X按未测／到期风险优先，不强取未来项凑数，全量忽略到期但每项每日只计一次。
- 恢复旧活动轮次优先于新设置，设置只影响后续轮次；取消和重复提交不增加观察或重复计数。完成日期按实际作答本地日期，跨天仍恢复未完轮次。
- 测试冻结后编译成功、行为断言失败才算 RED；生产恢复 GREEN，每阶段仅一次全量，不重做旧验证。

## Review Focus

1. 同音假名与对应字形不能靠无法区分的音频误判；Task1验证行提示和字形精确比较，Task2验证提示绑定。
2. 全量逐项首次覆盖与错题间隔不会因为只有一个剩余项而死锁；Task1实际完成全量和单项错题重试。
3. 计划变更、跨天恢复和重复提交不能抹掉原轮次或反复写FSRS；Task1真实SQLite、可控时钟和陈旧题号验证。
4. 五十音不创建词库或词汇进度，不混入英语；Task1核对词汇表为空，Task2检查日语专属入口。
5. 退出／取消／音频错误不记错，不发布迟到页面状态；Task2验证真实导航生命周期和受控音频失败。

### Task 1：内置范围、计划及独立听写持久化

**Files:** `Core/Kana` 中每个类型独立文件（Catalog、PlanDto/Service/Mapper、SessionDto、QuestionDto、FeedbackDto、ErrorDto、SessionService/Planner/Mapper、按读写能力拆分合同）；`DataAccess/Kana` 中Plan/Session/Memory PO及Plan/Session DataAccessor；迁移13在独立`KanaSchemaMigration`定义，由原Runner追加；注册沿用唯一三层入口。测试 `Tests/Core/KanaServiceTests.cs`；旧 `SchemaMigrationRunnerTests`、`StartupInitializerTests`、`StableImportMetadataTests`、`LearningPlanDataAccessorTests`、`LearningSessionDataAccessorTests`、`LearningSharedMigrationTests` 仅适配新表、版本与历史结构夹具，保留原数据／词形／记忆／备份／完整性期望。

**Interfaces:**
- `IKanaPlanReader.LoadAsync(token)->KanaPlanDto`，`IKanaPlanEditor.SaveAsync(KanaPlanDto,token)`：Enabled、Hiragana、Katakana、Clear、Voiced、Contracted、ReviewAll、DailyTarget(1–214)。
- `IKanaSessionReader.GetCurrentQuestionAsync(sessionId,token)->KanaQuestionDto?`、`GetProgressAsync(token)->KanaProgressDto`；`IKanaSessionOperations.StartOrResumeAsync(token)->KanaSessionDto?`、`SubmitAsync(sessionId,questionOrdinal,text,token)->KanaFeedbackDto`。
- Question含轮次ID、题号、字形／音类／必要行提示和朗读文本，不显示待答文本；Feedback才返回正确答案和原始错误摘要。

- [x] 先写十二项真实DI/SQLite测试：默认与非法范围／数量；214事实项及限定范围；每日数量和独立性；全量第一次逐项覆盖；错误延后重试与Again；同音提示、NFC和片假名精确判定；保存恢复、原活动设置不变；跨天实际完成统计；取消／陈旧题号／非法轮次；未来项不凑数；数据失败传播。修正错误表名夹具后重新冻结，只在实现临时数量+1及强行Good，编译成功后12项5行为RED、7通过、0跳过。
- [x] 只恢复生产实现为GREEN：新12项及相关19项共31/31，另五项旧迁移适配5/5通过；CRUD和事务只在DAO，FSRS与判断只在Core，纯转换只在Mapper；迁移不改前12版本。原始观察及状态／记忆提交单事务，题号乐观锁拒绝重复。
- [x] solution零警告错误、相关31/31及旧夹具适配5/5，最终1811/1811全量、冻结hash和diff通过，证据`docs/verification/2026-10-08-kana-backend.md`；首次中文提交，不为进度记录再跑一遍全量。首次全量5个旧夹具失败和无效零测试过滤均如实记录，未当作通过。

### Task 2：日语专属入口、范围设置与单窗口听写

**Files:** KanaPlan/KanaSession ViewModel及页面、首页入口、导航最小能力接口、唯一DI和App模板；`Tests/Presentation/KanaPresentationTests.cs`。复用现有内存音频和输入适配，不把五十音伪装成词库成员，不另造Window。

- [ ] 先写STA/XAML+DI测试：日语专属入口、英语隐藏、两组选项分别阻止取消最后一项、全量／每日X、进度、真实开始→播放→作答→即时反馈→稍后重试→结算→返回；音频失败不观察、取消与退出不迟到污染。
- [ ] 冻结后仅生产临时行为错误取得有效RED；恢复GREEN，主题和返回动画沿用Shell，只有题型／字形／必要行提示，不显示答案，喇叭播放实际目标音。
- [ ] 构建、完整测试和实际链路冒烟通过后第二次中文提交；完整计划唯一最终只读审查，集中处理阻塞问题，记录并提交后精确删除本计划临时目录。OCR和统计日历接入仍按后续原目标切片完成，不冒称整个产品已完成。

假名事实来源：[国际交流基金 Irodori 拗音表](https://www.irodori-online.jpf.go.jp/a1/introduction/02-008-16-01/)、[Irodori 假名练习](https://www.irodori.jpf.go.jp/assets/data/Kana_all.pdf)。只实现语言事实，不分发第三方插图或教材。
