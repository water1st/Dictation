# Core 与 Services 合并设计

## 目标

将目前独立的 `Dictation.Services` 业务项目完整并入 `Dictation.Core`，使最终三层物理结构为 `Dictation.Presentation`、`Dictation.Core`、`Dictation.DataAccess`。合并时同步删除已被 WPF 单窗口和新 TTS 链路取代的旧 Core/WinForms 实现；不改变已经通过测试的新业务行为、数据库结构或公开交互。

## 项目与依赖方向

`Dictation.Core` 升级为 `net10.0`，直接引用 `Dictation.DataAccess`，并接收原 `Dictation.Services` 的 NuGet 依赖。依赖方向固定为：

```text
Presentation ──> Core ──> DataAccess
TtsHost ───────> Core
Tests ─────────> Presentation / Core / DataAccess / TtsHost
```

`DataAccess` 不得反向引用 `Core`，因此不会形成循环依赖。`Dictation.Services` 从解决方案和所有项目引用中删除，不保留转发程序集、类型别名或兼容命名空间。

## 代码归属与命名空间

原 `Dictation.Services/<Feature>/` 文件迁入 `Dictation.Core/<Feature>/`，命名空间同步由 `Dictation.Services.*` 改为 `Dictation.Core.*`。配置、导入、学习、词库、单例、启动和 TTS 模块仍各自保留目录边界；DTO、业务模型和 Provider 接口继续与 DataAccess PO 分离。

现有 `AddDictationCore()` 成为唯一 Core 注册入口，只保留新业务服务注册。`AddDictationServices()` 随项目删除，Presentation 和测试只调用一次 `AddDictationCore()`，避免构建两套容器或重复注册。

## 遗留代码清理

引用扫描已经确认旧 `DictationManager`、`WordCollection`、绘图集合、Edge/System TTS 与工厂只被旧 WinForms 窗口互相引用，当前 WPF `Program`/`App`/单窗口页面不使用它们。因此本次一并删除这些 Core 文件和旧 TTS 包引用。

Presentation 删除 `MainWindow`、`DictationWindow`、`ReviewWindow`、`ResultWindow` 的 `.cs`/`.Designer.cs`/`.resx`、仅供它们使用的 `WindowFactory` 和空的旧强类型资源文件；项目关闭 `UseWindowsForms`。保留 `HomeWindow`、全部 `UserControl`、ViewModel、主题、导航、`pen.ico` 以及新 TTS Host。删除后的编译和全量测试是“确实无用”的最终证据。

## 文档与测试

更新 `AGENTS.md`、当前需求规格和仍在执行的学习模式计划，使权威描述改为 Presentation/Core/DataAccess。已经完成的历史计划保留原路径作为执行记录，不进行无意义的全文改写。

迁移属于结构重构，不新增业务行为：先记录全量测试基线，再完成机械迁移；通过编译期架构测试或源码扫描证明解决方案中不存在 `Dictation.Services` 项目、项目引用、命名空间和 DI 入口。最后运行全量测试与解决方案构建，测试数量不得减少，并单独提交一次中文架构纠偏提交。完成后再继续学习页面 Task 5。

## 验收标准

- 解决方案不存在 `Dictation.Services` 项目或程序集引用。
- 生产代码不存在 `Dictation.Services` 命名空间或 using。
- `Dictation.Core` 为 `net10.0`，且是业务服务的唯一归属。
- `AddDictationCore()` 是唯一 Core/业务注册入口。
- DataAccess 不引用 Core；项目依赖无环。
- 全量测试数量不减少且全部通过，解决方案构建为零错误。
- Core 不再包含旧 Edge/System TTS、绘图或旧听写类型和对应包。
- Presentation 不再启用 WinForms，程序集没有 `Form` 子类或 `WindowFactory`。
- 本次迁移不改变 SQLite schema、用户数据、新 TTS 进程协议或学习行为。
