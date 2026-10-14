# WPF 基础垂直切片执行计划

## 目标与边界

本切片控制在约 2–3 人/小时，交付一个能启动、能持久化设置、能执行数据库迁移并能处理单例的 WPF 首页。它只建立真实可验证的基础链路，不铺设没有行为的业务空壳。

本切片允许修改 `Dictation.sln`、`Dictation.Presentation/`、新增 `Dictation.Services/`、`Dictation.DataAccess/`、`Dictation.Tests/`、`global.json` 和项目文档所需的项目文件；保留现有 WinForms `.cs`、`.Designer.cs`、`.resx` 及其资源，不复制 `Resources/` 原始模型，不改 CI，不实现词库、学习、FSRS、OCR、TTS、MCP、更新器或五十音。

## 预期文件布局与职责

### 构建与项目

- `global.json`：固定已安装的 .NET SDK `10.0.400`。
- `Dictation.sln`：加入 `Dictation.Services`、`Dictation.DataAccess` 和 `Dictation.Tests`。
- `Dictation.Presentation/Dictation.Presentation.csproj`：迁移到 `net10.0-windows`，同时启用 WPF 与 WinForms，使旧窗体在迁移期间仍可编译；引用 Services、DataAccess 和 `Microsoft.Extensions.Hosting 10.0.10`。保留 `App.xaml` 的 WPF 资源字典能力，但将它从默认 `ApplicationDefinition` 调整为 `Page`（或等价地禁用 WPF 自动入口），设置 `<StartupObject>Dictation.Presentation.Program</StartupObject>`，确保只有显式的 `[STAThread] Program.Main` 一个入口。
- `Dictation.Services/Dictation.Services.csproj`：`net10.0`，引用 `Dictation.DataAccess` 和 `Microsoft.Extensions.DependencyInjection.Abstractions 10.0.10`；不为 DI 扩展引入完整 Hosting 包。
- `Dictation.DataAccess/Dictation.DataAccess.csproj`：`net10.0`，引用 `Dapper 2.1.79`、`Microsoft.Data.Sqlite 10.0.10` 和 `Microsoft.Extensions.DependencyInjection.Abstractions 10.0.10`。
- `Dictation.Tests/Dictation.Tests.csproj`：`net10.0-windows`，引用三个应用项目，使用 `xunit.v3 3.2.2`，开启可空引用和隐式 using。

### DataAccess

- `Dictation.DataAccess/Runtime/ApplicationPaths.cs`：生产使用 `AppContext.BaseDirectory` 计算根目录，测试注入临时根目录；创建并列的 `CONFIG`、`DATA`、`LOGS`、`BACKUPS`；`CACHE` 只有被明确请求时创建；提供 `DATA/UserData.db` 和 `CONFIG/settings.json` 路径，不依赖 current working directory。
- `Dictation.DataAccess/Runtime/SqliteConnectionFactory.cs`：每次操作创建新的 SQLite 连接，不保存共享连接。
- `Dictation.DataAccess/Runtime/SchemaMigrationRunner.cs`：`SchemaMigrations` 本身就是版本记录。新建空数据库不生成备份；只有已有数据库存在且有待执行迁移时，先备份到根 `BACKUPS/`，再在一个事务中执行全部 pending migrations；任一步失败回滚并抛出启动阻止异常，备份失败也阻止启动。首迁移只建立 `SchemaMigrations`，不预建未使用的业务表。
- 设置配置不属于 DataAccess；DataAccess 只注册连接工厂和迁移器，不注册设置读写类。

### Services

- `Dictation.Services/Configuration/ApplicationSettingsDto.cs`：定义 `ThemeMode`（System/Light/Dark）和 `Language`（Japanese/English）等跨 UI 设置 DTO。
- `Dictation.Services/Configuration/ApplicationSettingsDocument.cs`、`ApplicationSettingsMapper.cs`、`ConfigurationWriteExtensions.cs`、`SettingsPersistenceException.cs`：集中保存设置文件模型、DTO 映射和微软配置扩展的最小原子写回能力。`GetValue<T>` 负责读取，`SetValueAsync<T>` 一次写入完整文档并 Reload；微软配置组件没有官方 JSON 写回 API，因此不在 DataAccess 重复造配置读写层。
- `Dictation.Services/Configuration/SettingsService.cs`：注入 `IConfiguration` 与 `ApplicationPaths`，将 DTO 枚举与文档字符串互相映射，拒绝未知业务枚举；两个配置键都缺失时使用 System 主题和首次默认 Japanese 语言，不泄漏 JSON 细节给 Presentation。
- `Dictation.Services/Startup/StartupInitializer.cs`：作为 Host 启动阶段的唯一初始化编排者，调用 DataAccess 创建运行目录并执行 SQLite 迁移；只由主实例在 `StartAsync` 后执行。
- `Dictation.Services/SingleInstance/SingleInstanceCoordinator.cs`：使用命名互斥体判断主进程；主进程创建命名管道接收无参数的“激活”信号，次进程用短超时发送后静默退出；激活回调通过 WPF Dispatcher 通知 Presentation 还原并置前。
- `Dictation.Services/DictationServicesServiceCollectionExtensions.cs`：注册设置服务、启动初始化器、运行目录和单例协调器；只为操作系统进程边界保留必要的 Provider seam。

### Presentation

- `Dictation.Presentation/Program.cs`：改为唯一 `[STAThread]` 的 WPF 入口，按固定流程构建唯一 Host→解析并先检查单例→次实例用短超时发送无参数激活信号、释放 Host、以退出码 0 结束（不创建运行目录/数据库/窗口）→仅主实例 `StartAsync` Host（由 startup initializer 创建目录并执行迁移）→解析 `App`/`HomeWindow` 并 `Run`→`finally` 中 `StopAsync`/`Dispose`。激活回调 dispatch 到 WPF Dispatcher，还原最小化窗口并置前。
- `Dictation.Presentation/App.xaml`、`App.xaml.cs`：不设置 `StartupUri`；只初始化 WPF 资源、设置 `ShutdownMode` 和执行应用退出相关 UI 清理，不启动/停止 Host、不执行迁移。
- `Dictation.Presentation/HomeWindow.xaml`、`HomeWindow.xaml.cs`：最小首页宿主；代码后置只处理窗口生命周期和输入适配，不写业务规则。
- `Dictation.Presentation/ViewModels/HomeViewModel.cs`：通过 `SettingsService` 读取/保存主题和上次语言，提供真实的日语/英语首页选择；不添加尚不存在的会话状态、进度或导航占位。
- `Dictation.Presentation/Theme/ThemeResources.xaml`、`ThemeManager.cs`、`ISystemThemeProvider.cs`、`WindowsSystemThemeProvider.cs`：提供系统、浅色、深色三种基础资源，游戏化但克制；System 模式读取 Windows 当前应用主题，主题变化事件更新资源，退出时解除订阅。缺失或未知系统主题值安全回退到 Light；`ISystemThemeProvider` 是用于平台替换和测试的真实 Provider seam，不引入第三方 UI 框架。
- 旧 `MainWindow.cs` 等 WinForms 文件保持原路径和内容，避免 WPF 新窗口与旧 `MainWindow` 类型重名；WPF 根窗口使用 `HomeWindow`。

### 测试

测试目录按行为分为 `Infrastructure/`、`DataAccess/`、`Services/`、`Presentation/`，测试文件使用 `TypeNameTests.cs`。测试使用临时目录和内存替身，不写用户真实目录，不依赖 Edge、Windows TTS、CUDA 或网络。

## 执行顺序

### 1. 先写测试，再证明测试能发现错误

先创建 `Dictation.Tests` 和以下测试，不先写生产逻辑：

- `ApplicationPathsTests`：创建四个并列目录；默认不创建 CACHE；同一路径重复初始化幂等；空/文件占用路径抛出异常。
- `ApplicationSettingsMapperTests`、`SettingsServiceTests`：通过 Microsoft `GetValue<T>`、`AddJsonFile` 和 `IConfigurationRoot.Reload()` 验证映射、缺失/非法配置、首次及覆盖保存、取消、路径冲突、临时文件清理、非 root 配置拒绝和失败不改变当前配置。
- `SchemaMigrationRunnerTests`：新建空数据库创建 `SchemaMigrations` 且不生成备份；已有库有 pending migrations 时先备份；重复运行无 pending 时不备份；迁移失败回滚；用无效数据库文件或备份目标冲突路径证明备份/数据库错误阻止启动，不依赖 ACL 或管理员权限。
- `SingleInstanceCoordinatorTests`：第一个实例成为主实例；第二实例不获得主实例资格并发送激活信号；无效信号不携带参数；释放后可重新获得互斥体；模拟管道异常不会让主窗口崩溃；激活回调可 dispatch 到 WPF UI 线程。
- `SettingsServiceTests`：缺失配置时产生 System 主题和首次 Japanese 语言默认值；从 `IConfiguration` 读取并完成 DTO↔Document 映射；未知或部分缺失值拒绝；写入成功后当前配置立即可见，重新构建 JSON 配置后状态仍保持；非法 DTO 或写入失败不改变文件和当前配置。
- `HomeViewModelTests`：加载上次语言；切换日语/英语立即持久化；主题变更持久化；加载失败向 UI 暴露可读错误状态而不执行半次保存。
- `ThemeManagerTests`：使用 fake `ISystemThemeProvider` 验证 System 初始应用和系统事件切换；手动 Light/Dark 忽略系统事件；Dispose 解除订阅。`WindowsSystemThemeProvider` 的注册表读取/Windows 用户偏好事件仅做适配，可用内部输入测试纯映射；真实系统变化由人工冒烟验证，缺失/未知值回退 Light。

只实现使测试可编译的最小骨架后，故意让一个针对性断言失败：在文件确实存在且包含 `English` 时，让 `SettingsService.Load` 错误返回默认值。运行：

```powershell
dotnet restore Dictation.sln
dotnet test Dictation.Tests/Dictation.Tests.csproj -- --filter-method Dictation.Tests.Services.SettingsServiceTests.Load_WhenSettingsFileIsValid_RestoresSavedValues
```

验收证据应是该断言报告期望 `Dark/English`、实际为 `System/Japanese`，证明测试不是走过场。随后修正为：由 `IConfiguration.GetValue<T>` 读取两个字符串，仅在两个键都缺失时生成 System 主题和首次 Japanese 语言默认值；不把故意错误留在工作区或提交。

### 2. 修正最小实现并完成第一次提交

修正设置、路径、迁移和单例的最小实现；运行：

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug
dotnet test Dictation.sln --configuration Debug
dotnet build Dictation.sln --configuration Debug --no-restore
```

相关测试全部通过后，建议提交 `基础：实现设置迁移与单例核心`。提交前检查 `git diff --check`，不得包含故意失败实现、数据库、CONFIG 或构建输出。

### 3. 打通 WPF/ViewModel→SettingsService→DTO/Document→Microsoft IConfiguration/CONFIG/settings.json

在最小实现提交后，把 Generic Host、WPF App、HomeWindow 和 HomeViewModel 接入同一个 DI 容器：主实例启动 Host 后创建运行目录、执行迁移并加载设置；首页只显示真实可用的日语/英语选择、主题选项和基础导航视觉，不显示虚假学习/复习进度；点击语言或主题后经 SettingsService 保存到 `CONFIG/settings.json`，重启后读取；次进程按短超时发送无参数信号后退出，主进程将激活回调 dispatch 到 WPF UI 线程，还原最小化窗口并置前。所有设置 IO 仍通过 Services.Configuration 的配置入口，ViewModel 不直接读 JSON/SQL。

运行完整验证：

```powershell
dotnet test Dictation.sln --configuration Debug
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet run --project Dictation.Presentation/Dictation.Presentation.csproj --configuration Debug
```

手工冒烟需确认：首次启动创建根目录和 `DATA/UserData.db`；数据库有 `SchemaMigrations` 且新库没有无意义备份；主题和最后语言重启后保留；在 System 模式改变 Windows 应用主题后界面随之变化；第二次启动不弹窗、不激活新窗口，并使第一个最小化窗口还原置前；关闭程序后没有残留第二进程或后台监听；WPF 首页可显示且旧 WinForms 源码仍编译。无错误后建议第二次提交 `基础：打通WPF首页设置与单例启动`。

## 完成标准

本切片完成时，现有解决方案构建无错误，全部测试通过，两个中文提交只包含本切片文件；WPF 是新的启动入口，旧 WinForms 代码仍可编译但未被删除；任何词库、TTS、OCR、学习、复习、MCP、统计和更新行为均留给后续垂直切片。
