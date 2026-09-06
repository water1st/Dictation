# Core 与 Services 合并设计

## 目标

将目前独立的 `Dictation.Services` 业务项目完整并入 `Dictation.Core`，使最终三层物理结构为 `Dictation.Presentation`、`Dictation.Core`、`Dictation.DataAccess`。这次调整只改变代码归属、命名空间、项目引用和 DI 注册入口，不改变已经通过测试的业务行为、数据库结构或公开交互。

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

现有 `AddDictationCore()` 成为唯一 Core 注册入口：合并新业务服务注册，并暂时保留旧 WinForms 仍需要的注册。`AddDictationServices()` 随项目删除，Presentation 和测试只调用一次 `AddDictationCore()`，避免构建两套容器或重复注册。

## 遗留代码策略

`Dictation.Core` 中现有 Edge TTS、System Speech、绘图和旧听写类型暂不在本次迁移中删除，因为尚存 WinForms 编译引用。它们继续作为明确的迁移遗留项；待 WPF、TTS 和学习链路完全替代后，再按原总计划删除。合并不新增对这些旧类型的依赖。

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
- 本次迁移不改变 SQLite schema、用户数据、TTS 进程协议或学习行为。
