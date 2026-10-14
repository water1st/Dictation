# Microsoft Configuration Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 使用微软配置扩展读取设置，并在同一配置模块内为 `IConfiguration` 补充最小泛型 JSON 原子写回能力。

**Architecture:** Generic Host 注册 `CONFIG/settings.json`；Presentation 只调用 `SettingsService`。`SettingsService`、设置文件模型、Mapper、异常及内部 `IConfiguration.SetValueAsync<T>` 扩展集中在 `Dictation.Services.Configuration`，DataAccess 不承担读写配置。

**Tech Stack:** .NET 10、WPF、Generic Host、`Microsoft.Extensions.Configuration.Binder`、`Microsoft.Extensions.Configuration.Json`、`System.Text.Json`、xUnit v3

## Global Constraints

- 只在 `codex/dictation-refactor` 隔离分支修改，不合并或推送。
- 保持传统三层和单一 DI 容器；配置不是普通 DAL 数据，不创建设置 DataAccessor、Repository 或 Provider。
- 读取必须使用微软 `IConfiguration.GetValue<T>()`；自有代码只补官方没有的 JSON 写回。
- 设置相关类型集中在 `Dictation.Services.Configuration`，对 Presentation 只暴露 `SettingsService`。
- `SetValueAsync<T>` 的 `T` 是一次完整写入的配置文档；一次业务保存只写一次文件并 Reload 一次。
- 不引入 `IOptions`、自定义 `ConfigurationProvider`、第三方可写配置库或重型框架。
- 测试覆盖正常、缺失、非法、异常、取消和状态变化；临时错误只用于证明测试会失败，不得提交。
- 不改动尚未提交的 JMdict 词库实现，不把两项工作放进同一提交。

---

### Task 1: 统一设置配置模块

**Files:**
- Delete: `Dictation.DataAccess/Configuration/SettingsDataAccessor.cs`
- Delete: `Dictation.DataAccess/Configuration/SettingsPo.cs`
- Modify: `Dictation.DataAccess/DataAccessException.cs`
- Modify: `Dictation.DataAccess/DictationDataAccessServiceCollectionExtensions.cs`
- Create: `Dictation.Services/Configuration/ApplicationSettingsDocument.cs`
- Create: `Dictation.Services/Configuration/ConfigurationWriteExtensions.cs`
- Create: `Dictation.Services/Configuration/SettingsPersistenceException.cs`
- Modify: `Dictation.Services/Configuration/ApplicationSettingsMapper.cs`
- Modify: `Dictation.Services/Configuration/SettingsService.cs`
- Modify: `Dictation.Services/Dictation.Services.csproj`
- Modify: `Dictation.Presentation/Program.cs`
- Modify: `Dictation.Presentation/Dictation.Presentation.csproj`
- Modify: `Dictation.Presentation/ViewModels/HomeViewModel.cs`
- Delete: `Dictation.Tests/DataAccess/SettingsDataAccessorTests.cs`
- Modify: `Dictation.Tests/Services/ApplicationSettingsMapperTests.cs`
- Modify: `Dictation.Tests/Services/SettingsServiceTests.cs`
- Modify: `Dictation.Tests/Presentation/HomeViewModelTests.cs`
- Modify: `Dictation.Tests/Dictation.Tests.csproj`
- Modify: `docs/requirements/RefactorSpecification.md`
- Modify: `docs/plans/2026-08-14-wpf-foundation.md`

**Interfaces:**
- Consumes: DI 提供的 `IConfiguration`、`ApplicationPaths.SettingsFile`、微软 `GetValue<string>()` 和 `IConfigurationRoot.Reload()`
- Produces: `SettingsService.Load()`、`SettingsService.SaveAsync(...)`、模块内部 `IConfiguration.SetValueAsync<T>(string, T, CancellationToken)`

- [ ] **Step 1: 先让测试表达新的内聚边界**

  删除 DataAccess 设置测试。将 Mapper 测试切换到 `ApplicationSettingsDocument`；SettingsService 和 HomeViewModel 测试使用真实微软配置：

  ```csharp
  private static IConfiguration BuildConfiguration(ApplicationPaths paths)
  {
      return new ConfigurationBuilder()
          .SetBasePath(paths.Root)
          .AddJsonFile(Path.Combine("CONFIG", "settings.json"), optional: true, reloadOnChange: false)
          .Build();
  }
  ```

  测试至少包括：两键缺失产生默认值；有效文件恢复；只缺一键和未知值拒绝；首次保存及覆盖保存；保存后同一配置立即可见；重新 Build 后恢复；非法 DTO、预取消和写入失败均不更新文件或当前配置；不支持 Reload 的 IConfiguration 在写入前拒绝；目标路径为目录时抛 `SettingsPersistenceException` 且无临时文件；HomeViewModel 保持旧状态并显示错误；损坏 JSON 经 Host 相同策略使用默认值。

- [ ] **Step 2: 实际运行红测**

  Run:

  ```powershell
  dotnet test Dictation.Tests/Dictation.Tests.csproj --no-restore -- --filter-class Dictation.Tests.Services.ApplicationSettingsMapperTests Dictation.Tests.Services.SettingsServiceTests Dictation.Tests.Presentation.HomeViewModelTests
  ```

  Expected: FAIL，原因直接指向 `ApplicationSettingsDocument`、新构造器、`Load()`、`SetValueAsync<T>` 或 `SettingsPersistenceException` 尚不存在，而不是测试基础设施错误。记录关键失败，不提交红状态。

- [ ] **Step 3: 临时制造行为错误证明断言有效**

  在可编译骨架中临时让 `SettingsService.Load()` 无条件返回 `System/Japanese`，只运行有效文件恢复测试。

  Expected: FAIL，明确显示期望 `Dark/English`、实际 `System/Japanese`。立即删除临时错误，不得暂存或提交。

- [ ] **Step 4: 实现最小生产代码**

  `SettingsService` 构造器为：

  ```csharp
  public SettingsService(IConfiguration configuration, ApplicationPaths applicationPaths);
  ```

  `Load()` 使用微软扩展读取：

  ```csharp
  var themeMode = configuration.GetValue<string>(nameof(ApplicationSettingsDocument.ThemeMode));
  var lastLanguage = configuration.GetValue<string>(nameof(ApplicationSettingsDocument.LastLanguage));
  ```

  两值都为 `null` 时返回默认 DTO，否则构造 `ApplicationSettingsDocument` 交给 Mapper。`SaveAsync` 先映射，再一次调用：

  ```csharp
  await configuration.SetValueAsync(
      applicationPaths.SettingsFile,
      document,
      cancellationToken);
  ```

  泛型扩展签名为：

  ```csharp
  internal static Task SetValueAsync<T>(
      this IConfiguration configuration,
      string filePath,
      T value,
      CancellationToken cancellationToken = default);
  ```

  扩展先校验 `configuration is IConfigurationRoot`、路径和值，创建父目录，以 `JsonSerializer.SerializeAsync` 写同目录唯一临时文件，Flush 后 `File.Move(..., overwrite: true)`；替换成功后调用 `configurationRoot.Reload()`。取消、IO、权限错误时清理临时文件；IO/权限错误包装为 `SettingsPersistenceException`。

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

  Services 对直接使用的 `Microsoft.Extensions.Configuration.Binder`、Presentation 对直接使用的 `Microsoft.Extensions.Configuration.Json` 添加 `10.0.10` 显式引用。HomeViewModel 初始化改用同步 `settingsService.Load()`，异步保存与并发门保持不变。

- [ ] **Step 5: 修正文档与静态边界**

  将旧文档改为“微软配置扩展读、同一 Services 配置模块的 `SetValueAsync<T>` 写”，并删除任何 DataAccessor/Writer 设计陈述。运行：

  ```powershell
  rg -n "SettingsDataAccessor|SettingsFileWriter|DeserializeAsync<.*Settings|读取设置文件" Dictation.DataAccess Dictation.Services Dictation.Presentation docs
  ```

  Expected: 生产代码没有设置 DataAccessor/Writer 或手工反序列化；历史计划只在明确说明已被取代时可出现旧名称。

- [ ] **Step 6: 运行完整验收**

  Run:

  ```powershell
  dotnet test Dictation.Tests/Dictation.Tests.csproj --no-restore -- --filter-class Dictation.Tests.Services.ApplicationSettingsMapperTests Dictation.Tests.Services.SettingsServiceTests Dictation.Tests.Presentation.HomeViewModelTests
  dotnet test Dictation.sln
  dotnet build Dictation.sln --configuration Debug --no-restore
  git diff --check
  ```

  Expected: 全部测试通过；构建 0 errors；不新增警告；静态检查通过。

- [ ] **Step 7: 做 WPF 配置冒烟并提交**

  在独立临时输出目录启动应用，确认缺少设置文件时进入首页；选择英语和深色后创建文件；重启恢复；损坏 JSON 时使用默认值而不终止。不得触碰仓库根目录用户资源。

  Commit:

  ```powershell
  git add AGENTS.md Dictation.DataAccess Dictation.Services Dictation.Presentation Dictation.Tests docs/requirements/RefactorSpecification.md docs/plans/2026-08-14-wpf-foundation.md docs/superpowers
  git commit -m "设置：统一微软配置读写入口"
  ```
