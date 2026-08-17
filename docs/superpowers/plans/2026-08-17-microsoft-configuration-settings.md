# Microsoft Configuration Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 用 `Microsoft.Extensions.Configuration` 取代自制设置读取逻辑，并仅保留最小、可靠的 JSON 原子写入能力。

**Architecture:** Generic Host 将根目录下的 `CONFIG/settings.json` 作为最后一个可选 JSON 配置源；`SettingsService` 从 DI 注入的 `IConfiguration` 同步读取并执行业务映射。DataAccess 中的具体类 `SettingsFileWriter` 只创建和原子替换设置文件，不承担读取。

**Tech Stack:** .NET 10、WPF、Generic Host、`Microsoft.Extensions.Configuration.Json`、`System.Text.Json`、xUnit v3

## Global Constraints

- 只在 `codex/dictation-refactor` 隔离分支修改，不合并或推送。
- 保持传统三层和单一 DI 容器；不引入 DDD、Repository、EF、`IOptions`、自定义配置提供器或第三方可写配置库。
- 生产读取必须使用 `IConfiguration`；自有代码仅补足微软 JSON 提供器没有的创建与写回能力。
- 测试必须覆盖正常、缺失、非法、异常、取消和状态变化；临时制造错误验证测试会失败后立即修正，错误不得提交。
- 不改动尚未提交的 JMdict 词库实现，不把两项工作放进同一提交。

---

### Task 1: 用微软配置读取设置并保留最小写入器

**Files:**
- Rename: `Dictation.DataAccess/Configuration/SettingsDataAccessor.cs` → `Dictation.DataAccess/Configuration/SettingsFileWriter.cs`
- Modify: `Dictation.DataAccess/DictationDataAccessServiceCollectionExtensions.cs`
- Modify: `Dictation.Services/Configuration/SettingsService.cs`
- Modify: `Dictation.Services/Dictation.Services.csproj`
- Modify: `Dictation.Presentation/Program.cs`
- Modify: `Dictation.Presentation/Dictation.Presentation.csproj`
- Modify: `Dictation.Presentation/ViewModels/HomeViewModel.cs`
- Rename: `Dictation.Tests/DataAccess/SettingsDataAccessorTests.cs` → `Dictation.Tests/DataAccess/SettingsFileWriterTests.cs`
- Modify: `Dictation.Tests/Services/SettingsServiceTests.cs`
- Modify: `Dictation.Tests/Presentation/HomeViewModelTests.cs`
- Modify: `Dictation.Tests/Dictation.Tests.csproj`
- Modify: `docs/requirements/RefactorSpecification.md`
- Modify: `docs/plans/2026-08-14-wpf-foundation.md`

**Interfaces:**
- Consumes: `ApplicationPaths.SettingsFile`、`SettingsPo`、`ApplicationSettingsMapper`、DI 提供的 `IConfiguration`
- Produces: `SettingsFileWriter.SaveAsync(SettingsPo, CancellationToken)`、`SettingsService.Load()`、现有 `SettingsService.SaveAsync(...)`

- [ ] **Step 1: 先改测试表达新边界**

  将 DataAccess 测试改为只验证写入；SettingsService 和 HomeViewModel 测试通过以下真实微软配置构造方式读取：

  ```csharp
  private static IConfiguration BuildConfiguration(ApplicationPaths paths)
  {
      return new ConfigurationBuilder()
          .SetBasePath(paths.Root)
          .AddJsonFile(Path.Combine("CONFIG", "settings.json"), optional: true, reloadOnChange: false)
          .Build();
  }
  ```

  测试至少包括：缺失文件产生默认值；有效文件恢复值；保存后当前 `IConfiguration` 立即返回新值；重新 Build 后从文件恢复；未知字符串抛业务异常；非法 DTO 不落盘；预取消不落盘；目标为目录时清理临时文件并抛 DataAccess 异常；HomeViewModel 保存失败保持旧状态。

- [ ] **Step 2: 运行定向测试并证明旧实现不满足新接口**

  Run:

  ```powershell
  dotnet test Dictation.Tests/Dictation.Tests.csproj --filter "FullyQualifiedName~SettingsFileWriterTests|FullyQualifiedName~SettingsServiceTests|FullyQualifiedName~HomeViewModelTests"
  ```

  Expected: FAIL，原因必须直接指向 `SettingsFileWriter`/`IConfiguration` 构造器或 `Load()` 尚不存在，而不是测试基础设施错误。保留命令与关键失败摘要作为执行记录，不提交红状态。

- [ ] **Step 3: 临时制造可观察错误并证明断言有效**

  在最小可编译骨架中临时让 `SettingsService.Load()` 无条件返回 `System/Japanese`，运行“有效文件恢复 Dark/English”测试。

  Expected: FAIL，明确显示期望 `Dark/English`、实际 `System/Japanese`。随后立即删除该临时错误；不得暂存或提交。

- [ ] **Step 4: 实现最小生产代码**

  `SettingsFileWriter` 删除全部读取代码，仅保留参数校验、运行目录创建、`JsonSerializer.SerializeAsync`、Flush、同目录临时文件替换和失败清理。

  `SettingsService` 使用以下构造边界和同步读取：

  ```csharp
  public SettingsService(IConfiguration configuration, SettingsFileWriter settingsFileWriter);
  public ApplicationSettingsDto Load();
  public Task SaveAsync(ApplicationSettingsDto settings, CancellationToken cancellationToken = default);
  ```

  `Load()` 在两个键都缺失时返回默认值，否则构造 `SettingsPo` 交给现有 Mapper。`SaveAsync` 先映射并写文件，成功后再更新 `configuration[nameof(SettingsPo.ThemeMode)]` 与 `configuration[nameof(SettingsPo.LastLanguage)]`。

  `Program` 在 `Host.CreateDefaultBuilder(args)` 后配置：

  ```csharp
  .ConfigureAppConfiguration((_, configuration) =>
  {
      configuration.SetBasePath(AppContext.BaseDirectory);
      configuration.AddJsonFile(source =>
      {
          source.Path = Path.Combine("CONFIG", "settings.json");
          source.Optional = true;
          source.ReloadOnChange = false;
          source.OnLoadException = context => context.Ignore = true;
      });
  })
  ```

  对直接使用的 `Microsoft.Extensions.Configuration.Abstractions`/`Json` 添加版本 `10.0.10` 的显式包引用。HomeViewModel 将 `await settingsService.LoadAsync(...)` 改为同步 `settingsService.Load()`，其余异步保存与并发门保持不变。

- [ ] **Step 5: 运行定向测试、完整测试和构建**

  Run:

  ```powershell
  dotnet test Dictation.Tests/Dictation.Tests.csproj --filter "FullyQualifiedName~SettingsFileWriterTests|FullyQualifiedName~SettingsServiceTests|FullyQualifiedName~HomeViewModelTests"
  dotnet test Dictation.sln
  dotnet build Dictation.sln --configuration Debug --no-restore
  git diff --check
  ```

  Expected: 所有测试通过；构建 0 errors；不新增警告；`git diff --check` 无错误。

- [ ] **Step 6: 做 WPF 配置冒烟**

  在独立临时输出目录启动应用，确认缺少 `CONFIG/settings.json` 时可进入首页；选择英语和深色后文件被创建；退出并重启后恢复英语和深色。将文件临时改成损坏 JSON，再启动应使用默认值而不是终止进程。测试完成后仅清理该临时输出目录，不触碰仓库根目录的用户资源。

- [ ] **Step 7: 修正文档并提交**

  将旧文档中“DataAccessor 负责 JSON 读取”改为“IConfiguration 负责读取，SettingsFileWriter 只负责原子写回”，并明确微软提供器没有写回 API。检查不再存在生产 `SettingsDataAccessor` 或 `JsonSerializer.Deserialize*` 设置读取：

  ```powershell
  rg -n "SettingsDataAccessor|DeserializeAsync<SettingsPo>|读取设置文件" Dictation.DataAccess Dictation.Services Dictation.Presentation docs
  git diff --check
  ```

  Expected: 生产代码无自制设置读取器；文档无冲突陈述。

  Commit:

  ```powershell
  git add Dictation.DataAccess Dictation.Services Dictation.Presentation Dictation.Tests docs/requirements/RefactorSpecification.md docs/plans/2026-08-14-wpf-foundation.md
  git commit -m "设置：改用微软配置组件读取"
  ```
