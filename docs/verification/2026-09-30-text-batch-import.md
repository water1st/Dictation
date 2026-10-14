# 文本批量导入验收记录

## 范围

源语言逐行词表 → 精确义项候选 → 单窗口选择 → 整组暂存 → 统一收件箱。包含粘贴、UTF-8 TXT、单文件拖放和主实例启动参数，不绕过学习／复习／丢弃确认。不包含 OCR 或尚未完成的复习调度等目标。

## 测试证据

RED 必须先构建成功，再实际运行测试；编译错误和无效过滤导致的零测试不算 RED。测试冻结后只修改生产实现。

| 测试文件 | 有效 RED | 恢复后 GREEN | 冻结 SHA-256 |
| --- | --- | --- | --- |
| `TextImportPreparationServiceTests.cs` | 12 项，8 个行为失败 | 12/12 | `9BF5D1277ABBC75288399396B1FA4FBB90FC4AF1C4854C31C460C1F152A136FC` |
| `TextImportViewModelTests.cs` | 11 项，9 失败；另将多义项错误默认选首项，11 项中 3 个断言失败 | 11/11 | `192FB27DDCDFB3B9814644061F1EBB43F06A49148794339A54B8AAE61C43EAE7` |
| `TextImportRenderingTests.cs` | 可编译空页面，2 个控件／绑定断言失败 | 2/2 | `B20FD44EFB2C95902C37F505E1B0641E8C5E7BD7AC843E51CF1D023933EE555A` |
| `TextImportFileTests.cs` | 10 项，9 个编码／文件／路由断言失败 | 10/10 | `3D7115370D7BAE7E63F67F5859B738588F86EC2A3AA0E94DBF2083F76F270FD1` |
| `TextImportPaginationTests.cs` | 编译成功，3 项中 2 失败，期望 70／100 个精确义项但实际仅 50 | 3/3 | `E97B11118ACB379F964130FC95A0869006625711C8B1749AEB9FB41C13E310C3` |
| `TextImportLifecycleTests.cs` | 编译成功，6 个后台响应／进行中取消／离页断言均失败 | 6/6 | `7E117830D3614DBB0A291E61F7A2B072D1E05D8CEFA2695B00BC907F4F7C51FC` |

首次完整链路全量 1485/1485；独立审查修正后全量 **1494/1494**，无跳过；解决方案 Debug 构建 **0 警告、0 错误**。原有冻结测试及本轮新增冻结测试哈希均未改变。分页测试首次夹具连接池锁文件不算 RED，改为不使用连接池、重新冻结后取得表中业务断言失败；生命周期测试编译适配发生在冻结前，不算 RED。临时错误没有保留在源码中。

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1494
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class 'Dictation.Tests.Presentation.TextImport*' --minimum-expected-tests 29
```

## 真实资源及运行冒烟

只读使用正式日中／英中数据库，冒烟用户数据库在本计划临时目录隔离。UTF-8 BOM TXT 的重复行去重：`食べる` 返回 2 个候选，选定“吃；食用”；`went` 返回 17 个候选，选定中文义项并整组进入 `Inbox/Pending`。没有替用户确认到学习或复习。

修正后正式日语 `こう` 返回完整 **86** 个精确候选，最后一个“绞刑（律令制中的死刑处罚方式）”义项成功整组暂存到 Inbox。原有普通查词 1–50 上限保持不变，导入按精确页完整遍历，不依靠更大的固定上限。

自包含 `win-x64` 发布成功。隐藏窗口传入示例 TXT 后，实际主窗口标题变为“日语文本导入”；次实例退出、首实例保留，主窗口正常关闭。真实 WPF 渲染及双向选择绑定由 STA 测试验证；隐藏窗口冒烟不等于人工视觉验收。

已有预览更新程序文件与 `Samples/`，保留词库和用户运行数据；尚未包含 TTS 模型，不是完整最终发行包。

## 实现取舍

- 候选复用已有查询 DTO，避免重复 DTO 和 Mapper；测试沿用 `Core/` 组织。
- `LoadTextAsync`、`LoadFileAsync` 共用私有输入准备流程，文件读取及查询保持一次忙状态，不并存第二套导入逻辑。
- Core 单独提交，完整页面及文件入口一起提交，不提交忽略启动路径参数的中间入口。

## 独立审查与统一修正

审查范围 `756b864..c2bcf37`，发现两个 Important，无 Critical 或 Minor：精确候选固定 50 项截断，以及 SQLite 同步工作阻塞 Dispatcher 且缺少进行中取消。两项均纳入同一轮修正，不追加反复审查：查询合同增加精确分页；页面后台执行输入准备与暂存，在 Dispatcher 应用结果；取消按钮、返回时取消与作用域释放相接。测试覆盖不合作的后台结果也不得回写已释放页面、取消后允许重试、外部取消正常传播，以及真实暂存前取消不产生部分收件箱。

取消不是撤销：已经提交成功的批次仍留在收件箱，界面文案说明这一边界。转场期间的返回限制仍保留，但启动文件的长匹配不再锁住返回按钮。

## 最终交付约束

验收目录为 `D:\Source\Repos\Dictation`。最终集成并确认代码、资源和用户数据已保留后，清理此次创建的 `Dictation-refactor`、`Dictation-LexiconValidation`、`Dictation-AGENTS-before-878592a.md` 等明确临时产物；不得把 `D:\Source\Repos` 整体当临时目录或删除其他项目。
