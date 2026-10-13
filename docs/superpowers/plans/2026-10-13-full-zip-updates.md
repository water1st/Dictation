# 全量 ZIP 更新与发布收尾计划

> 执行要求：使用 `superpowers:executing-plans`，主代理串行执行。用户已于 2026-10-13 要求按原 GOAL 直接收尾，不再展开需求选择；这里仅落实已批准规格，不添加更新渠道、安装器或增量更新。

**目标：**启动异步检查，有新版只显示工具栏按钮；点击才下载并校验全量 ZIP，由隐藏 Updater 保留用户数据、替换程序、失败回滚并重启。master push 自动构建并发布 ZIP／SHA-256，PR 不发布。

**架构：**业务与包验证在 Core/Updating，Presentation 只绑定和启动隐藏进程；Updater 使用 Core 的相同安装能力。DataAccess 不增加配置或更新存储。使用 BCL HttpClient、System.Text.Json、SHA256、ZipFile 和现有 DI，不引入更新框架或第二套业务层。

**规格：**`docs/requirements/RefactorSpecification.md` 第 2、3、9、11 节和当前 `AGENTS.md`。

## 全局约束

- 沿用 `D:\Source\Repos\Dictation`、`codex/dictation-refactor`，当前 BASE 为 a10b862；不推送、合并或新建工作区。
- 沿用已验证的 2018 项基线，不重复旧模块 RED。每次产品提交验证本次构建、全量及真实行为。
- 根目录 CONFIG、DATA、LOGS、BACKUPS、CACHE 以及不在包文件清单中的用户文件必须保留。
- 只从固定仓库 water1st/Dictation 的正式 GitHub Release 下载，不取源码 ZIP，不处理草稿／预发行／降级。
- 新版为 `major.minor.run_number`。发布资产固定为 `Dictation-win-x64.zip` 与 `Dictation-win-x64.zip.sha256`。
- 发布 ZIP 包含 `package-manifest.json`（version、files），列出所有受管理文件；不包含运行数据。更新只替换旧／新清单管理的文件，不能按根目录全删或把未知用户文件当旧程序删除。
- Updater 放在 `Updater/Dictation.Updater.exe`，执行前复制其专用运行目录到 CACHE 的独占任务目录；不能锁住将被替换的原程序 DLL。
- 不重新构建／复制已验证本地词库、TTS 模型；正式 CI 独立取固定哈希资源并验证许可门槛。未核验的发布许可不得伪装为完成，不代替用户选择根 LICENSE。

## Task 1：可回滚的受管理文件安装

审查重点及归属：ZIP 的 Windows 大小写／设备名／重解析点由 Task 1 拒绝；未知用户文件碰撞由 Task 1 在改动前拒绝；锁文件后的已移动文件由 Task 1 回滚；慢网络或取消下载由 Task 2 保留原窗口并清理自己的未完成包；无现成资源／许可缺口由 Task 3 明确阻止发布，不伪造 Release 成功。各任务测试包含对应行为。

**文件：**Core/Updating 下新增 `IUpdateInstaller.cs`、`ApplyUpdateRequestDto.cs`、`UpdateInstaller.cs`、`UpdatePackageManifest.cs`、`UpdatePackageValidator.cs`、`UpdateFileTransaction.cs`；修改 Core 唯一注册入口；新增 `Dictation.Tests/Core/UpdateInstallerTests.cs`。

**合同：**`IUpdateInstaller.ApplyAsync(ApplyUpdateRequestDto request, CancellationToken cancellationToken = default) : Task`。请求包含绝对 RootDirectory、PackagePath、64 位十六进制 Sha256、三段 Version。安装不读取用户数据库、不启动 WPF；父进程等待及重启属于 Task 2。

- [x] 先写真实临时目录／ZIP 测试，至少覆盖成功替换、移除过期受管理文件、增加新文件、五个运行目录不变、未知文件保留、哈希不符、坏 ZIP、缺文件、版本不符／降级／相同版本、重复路径／大小写冲突、ZIP 越界／保留目录／设备路径／符号链接、未知文件冲突、取消及 Windows 文件锁导致的部分移动回滚。
- [x] 先可编译骨架并冻结测试，执行编译成功的行为 RED；只改生产实现转 GREEN。
- [x] 复用 ZipFile 解压，验证全部条目后才移动旧文件；移动记录用于回滚，回滚不能被取消打断。备份和暂存只在此任务 CACHE 内。
- [x] 构建、全量与真实文件锁冒烟通过后提交“实现全量更新包校验与受管理文件回滚”。

## Task 2：隐藏更新进程与非模态工具栏入口

**文件：**Core/Updating 增加 `IUpdateReleaseQuery.cs`、`IUpdatePreparation.cs`、`AvailableUpdateDto.cs`、`PreparedUpdateDto.cs`、`GitHubReleaseDocument.cs`、`GitHubReleaseAssetDocument.cs`、`GitHubUpdateMapper.cs`、`GitHubUpdateProvider.cs`、`UpdateService.cs`；新增 `Dictation.Updater/Dictation.Updater.csproj`、`Program.cs`；Presentation/Updating 增加 `IUpdateLauncher.cs`、`UpdateLauncher.cs`，新增 ViewModels/UpdateViewModel.cs；修改 Shell、HomeWindow、DI 和解决方案。每个文件一个主要类型。

**合同：**`IUpdateReleaseQuery.CheckAsync(CancellationToken = default) : Task<AvailableUpdateDto?>`，`IUpdatePreparation.PrepareAsync(AvailableUpdateDto update, CancellationToken = default) : Task<PreparedUpdateDto>`。AvailableUpdateDto 保存 Version／PackageUri／ChecksumUri；PreparedUpdateDto 保存独占任务目录、RunnerPath、PackagePath、Sha256、Version、RootDirectory。`IUpdateLauncher.Launch(PreparedUpdateDto update)` 只启动隐藏进程，成功后 UI 关闭；启动失败仍留在原窗口。

**启动失败恢复补齐：**Task 1 的安装能力目前只覆盖文件替换失败。接入进程时改为 `IUpdateInstaller.ApplyAsync(...) : Task<UpdateInstallationDto>`，收据含 RootDirectory、TransactionDirectory、Version；同一实现增加窄 `IUpdateRollback.RollbackAsync(UpdateInstallationDto, CancellationToken)` 与 `IUpdateCommit.CommitAsync(UpdateInstallationDto, CancellationToken)`。旧备份在新版 WPF 发出启动就绪信号前不清理；迁移／启动失败时先回滚程序，再重启旧版。WPF 在迁移、Host 和窗口 Loaded 成功后，由 Core 的 `IUpdateStartupNotifier.NotifyReady()` 写入限定 CACHE 任务的就绪文件（Updater 通过环境变量传递，不改变用户文件导入参数或次实例规则）。三种安装能力保留同一 DI 实例，不做通用事务框架。该补齐仅落实原“失败回滚、重启并迁移”，不是额外恢复界面。

- [ ] 用受控 HttpMessageHandler、真实文件及 STA 先覆盖正式新版本／无新版／旧版／404／离线／坏 JSON／缺资产／不可信下载地址／哈希不符／取消／部分下载清理；数据保留沿用 Task 1，不另建安装逻辑。
- [ ] 开发目录无 package-manifest 时不自动查询或更新源码；发行目录 Loaded 后异步检查，只发布按钮，不弹窗、不自动下载。VM 独立版本屏障、取消和页内错误；月份／语言页不影响更新入口。
- [ ] Updater 用 Generic Host 和现有 Core 注册，等待原 PID 退出，不强杀进程；调用 Task 1 能力，成功重启根目录主程序，失败记录 LOGS 并重启旧程序。参数用微软配置绑定，禁止手写第二套业务／容器。
- [ ] 真实独占 runner、隐藏窗口句柄和父进程退出／更新失败恢复冒烟；构建／全量通过后分别提交最小能力与 UI／进程打通结果。

## Task 3：可重复的 ZIP 打包与 master 发布

**文件：**新增 `VERSION`、`scripts/Publish-Portable.ps1`、`scripts/Get-ReleaseResources.ps1`、`scripts/Test-ReleaseResources.ps1`，更新 `.github/workflows/dotnet-desktop.yml` 与资源文档；增加执行脚本的隔离包测试，不用源码字符串断言冒充行为。

- [ ] VERSION 仅 `1.0`；构建用 `1.0.$run_number`。分别 self-contained win-x64 publish 主程序／Updater，独占暂存目录写文件清单并 ZIP／SHA256；使用清单统一生成及 Task 1 验证，禁止打包 CONFIG／DATA 等。
- [ ] CI 用稳定 .NET 10、官方 actions、GITHUB_TOKEN 的最小 contents 权限；restore/build/test（门槛为当时实际基线），PR 只验证，master push 才 `gh release create` 并上传成对资产。未实际运行 GitHub CI 时只报告本地验证，不能称远端 Release 已成功。
- [ ] 资源获取固定来源版本／SHA／许可证；本地复用 artifacts/Dictation-preview/Resources，不覆盖用户运行目录。缺许可核验必须显式阻止外部发布，不以绿色打包掩盖。
- [ ] 测试实际包清单、受保护目录排除、SHA、两份输出、缺资源拒绝、错误哈希拒绝及自包含启动。一次最终只读审查本计划差异，修复真实问题、不重审旧模块。

## 验证与交付边界

构建：`dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1`；聚焦 `--filter-class '*UpdateInstallerTests' --minimum-expected-tests <实际数>`，全量门槛至少 2018 并随新增用例增加；`git diff --check`。永久证据记在 `docs/verification/2026-10-13-full-zip-updates.md`。

此计划不增加 OCR 或其它新需求。完成后执行原 GOAL 最后的 OCR 与整体交付清理，完整验收后结束 GOAL，不因单阶段通过缩小成功条件。

## 技术依据

GitHub 元数据和资产字段按[官方 Release API](https://docs.github.com/en/rest/releases/releases#get-the-latest-release)；解压复用[微软 ZipFile](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile.extracttodirectory?view=net-10.0)，不自写压缩算法；SDK 安装沿用[官方 setup-dotnet](https://github.com/actions/setup-dotnet)。
