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

恢复后全量 **1485/1485**，无跳过；解决方案 Debug 构建 **0 警告、0 错误**。临时错误没有保留在源码中。

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1485
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class 'Dictation.Tests.Presentation.TextImport*' --minimum-expected-tests 23
```

## 真实资源及运行冒烟

只读使用正式日中／英中数据库，冒烟用户数据库在本计划临时目录隔离。UTF-8 BOM TXT 的重复行去重：`食べる` 返回 2 个候选，选定“吃；食用”；`went` 返回 17 个候选，选定中文义项并整组进入 `Inbox/Pending`。没有替用户确认到学习或复习。

自包含 `win-x64` 发布成功。隐藏窗口传入示例 TXT 后，实际主窗口标题变为“日语文本导入”；次实例退出、首实例保留，主窗口正常关闭。真实 WPF 渲染及双向选择绑定由 STA 测试验证；隐藏窗口冒烟不等于人工视觉验收。

已有预览更新程序文件与 `Samples/`，保留词库和用户运行数据；尚未包含 TTS 模型，不是完整最终发行包。

## 实现取舍

- 候选复用已有查询 DTO，避免重复 DTO 和 Mapper；测试沿用 `Core/` 组织。
- `LoadTextAsync`、`LoadFileAsync` 共用私有输入准备流程，文件读取及查询保持一次忙状态，不并存第二套导入逻辑。
- Core 单独提交，完整页面及文件入口一起提交，不提交忽略启动路径参数的中间入口。

## 最终交付约束

验收目录为 `D:\Source\Repos\Dictation`。最终集成并确认代码、资源和用户数据已保留后，清理此次创建的 `Dictation-refactor`、`Dictation-LexiconValidation`、`Dictation-AGENTS-before-878592a.md` 等明确临时产物；不得把 `D:\Source\Repos` 整体当临时目录或删除其他项目。
