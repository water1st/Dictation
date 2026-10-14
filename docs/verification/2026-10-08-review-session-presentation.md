# 单窗口复习入口与共用练习页面验证

## 实现与复用

直接移动并扩展原 `LearningSessionViewModel`／`LearningSessionPage` 为 `PracticeSessionViewModel`／`PracticeSessionPage`，不保留兼容壳或克隆复习页面。学习与复习各由内部 workflow 适配原 Core 合同，通过唯一 DI 选择；共用导航、页面、状态与内存音频输出，不共享成绩。纯 DTO 转换由对应 Mapper 完成。

首页新增“开始复习”，每日复习徽标归属于这个实际入口；复习词库与计划仍分别保留。页面固定当前语言，支持即时反馈、稍后重练、结算错误汇总、中文释义／属性浏览和喇叭重播。无候选时显示原因，不伪造完成会话。返回时立即取消当前操作，旧页退场动画尚未结束也不发布晚结果。

## 编译成功的行为 RED → GREEN

新增 8 项 `ReviewSessionPresentationTests`。机械移动和旧构造／反射／DI 清单适配均在冻结前完成；7 份新增及受影响测试文件冻结。构建 0 警告／错误后，8 项实际执行、8 项页面类型断言失败（期望共用练习页，实际停在首页），0 跳过。随后仅补生产导航、页面及语音错误原因提示；相同 8 项全部通过，7 份冻结哈希均未改变。

| `Dictation.Tests/` 文件 | 相同 SHA-256 |
| --- | --- |
| `Presentation/ReviewSessionPresentationTests.cs` | `AFF2E10409ADDCA8180E51DB14C6B25B53C3F7A2C57B673DC80770CDB866375D` |
| `Architecture/CoreLayerArchitectureTests.cs` | `C16A7760581150C62E1DA0C7332D8A5C017276C9D98331AB52BCEAEF1F823779` |
| `Infrastructure/LearningPresentationTestEnvironment.cs` | `E037EACD9C815BC51C57020C2C6EF3837FF3CCE56E32402716E5096481BC7F0B` |
| `Presentation/LearningSessionViewModelTests.cs` | `63A074A3E4C560DCFC05DDB16FD52168513FCB02066C68DAFD419FDEE410BA4B` |
| `Presentation/LearningSessionNavigationAndRenderingTests.cs` | `76F6FDDE150F131B4AEB2B7742ED931B9FE584387213E3851463117FC73DEA5C` |
| `Presentation/LearningPlanNavigationAndRenderingTests.cs` | `4EAC00853484DBB11CD48FFE12C08F977393BE37FF5162871A0023D082683A27` |
| `Presentation/HomeViewModelTests.cs` | `7A68CBA910D1C2434782E77519BE471371F9D8F46832CB293D8EC45612E7DE97` |

## 最终验证

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1757 --max-threads 4
git diff --check
```

构建 0 警告、0 错误；本次页面整合全量仅跑一次，1757/1757，0 失败／跳过，约 2 分钟。测试验证错误后两次正确仍保留错误、真实 FSRS lapse、当天整组数与学习成绩隔离；失败音频不能提交、不判错且可重播；浏览不写记忆；真实浅深主题绑定、禁拼写提醒输入；返回时非合作依赖的晚反馈被丢弃。

## 正式词库隔离冒烟

临时 STA 工具复用已验证的正式只读词库，用户数据库、配置及截图均在本计划临时目录。应用实际 DataTemplate 创建共用 UserControl，未创建 Window。日语“食べる”显示“吃；食用”，礼貌形假名两次作答完成一组；英语“go”整组包含关联成员，4 次作答完成一组。两门语言完成后均显示“复习 1/20”，学习保持“学习 0/20”。工具实际输出 `Passed=true SharedUserControl=true NativeModes=true ExtraWindows=0`。浅深渲染截图已实际查看；这是无窗口 STA 业务／渲染冒烟，不是人工点击验收或正式 TTS/OCR 验收。

诊断工具初次缺命名空间／TTS 注册、重复使用已导入的临时数据库，以及英语取答案 SQL 误按字形筛选，均仅修复诊断工具；生产实现与冻结测试不变。这些失败不算验收通过或 RED。

冒烟前后正式词库 SHA-256 相同：日语 `EE4411EA2766AFCD3261BFAEC7599DFD6F42AB184C3391C23496D1504999C734`、英语 `151F960A23FFE0008E445F893892321131466921E4A8FBB68A32972135A8302B`。原预览 `DATA/UserData.db` 保持 196608 字节、修改时间 2026-09-29 17:41:03，哈希 `D10D7879DCE222E2A3FF34B2341BCE95EDAA220B53AAF7C01039BA3325FF4BD5` 不变。

完整目标仍包含错题管理、五十音、MCP、统计打卡、更新发布及最后的 OCR；本切片不结束 GOAL，也不合并、推送或覆盖用户资源。
