# 全量 ZIP 更新验证记录

## 范围与起点

落实已批准规格第 3 节，沿用 `D:\Source\Repos\Dictation` 和 `codex/dictation-refactor`，BASE `a10b862`。本轮不新增更新渠道、安装器、增量更新或在线词库同步；未推送、合并或发布 Release。原统计阶段的 2018 项基线不重做 RED。

## Task 1：全量包安装最小能力

包始终完整下载，文件清单只用于确定程序／不可变资源归属，不是增量更新协议。安装器校验 SHA-256、三段版本及 ZIP／清单一致性，使用 .NET 10 的流式异步 ZipFile 解压。同一个只读包句柄从哈希到解压保持打开，避免重新打开时取到不同内容。实际替换仅涉及旧／新清单文件，未知用户文件碰撞提前拒绝。

CONFIG、DATA、LOGS、BACKUPS、CACHE 从不进入受管理清单；原用户文件与目录保留。旧文件移动到独占 CACHE 任务备份，异常或取消后恢复已移动的内容；回滚不被取消打断，无法恢复的备份不删除。当前只验证安装失败的回滚，尚未接入父进程等待、重启确认及重启失败后的恢复，不把这些待做环节算成已完成。

| 最终冻结测试 | SHA-256 |
| --- | --- |
| UpdateInstallerTests.cs | 4DE8EEA1A5E1AC50E2C0659A11A3E3EA61A86AB7CB93FE14C0BEB57FBD7D4948 |
| UpdateInstallerPathBoundaryTests.cs | 0C097BDCF95F873274FE71D017CC1ACB99FD64F0C00706A32EF3064428412739 |

- 主测试 29 项先写后实现。初次非法版本夹具同时使用坏哈希，冻结作废；拆开非法字段后重新冻结，构建 0 警告／0 错误，29 项实际执行、28 个行为断言失败、1 项取消通过、0 跳过。
- 只补生产实现后，29/29 转绿，冻结测试未改。
- 路径边界追加 3 项。根目录两种斜杠已正确拒绝；junction 初次测试的清理异常掩盖断言，该轮作废。先仅补夹具 finally 移除自建链接，再冻结、构建 0 警告／0 错误，3 项实际执行、1 项“应拒绝却未抛异常”行为失败、2 通过、0 跳过。只修生产检查整个 CACHE/Updates 前缀后转绿。
- 最终联合 **32/32，通过、0 跳过**，构建 **0 警告／0 错误**，两个冻结 SHA 未变。真实文件锁证明旧文件可恢复；实际 Windows junction 验证不会把新程序或任务数据写出安装目录。

所有 ZIP、假程序、链接及锁均在测试独占 TEMP 内构造。没有对当前预览、源码目录、用户实际配置或数据库执行安装。完整命令 `dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 2050 --max-threads 4` 实际 **2050/2050，通过、0 跳过**，5 分 56 秒；构建 0 警告／0 错误，冻结 SHA 未变，差异检查通过。

## 后续明确未完成

隐藏 Updater、父 PID 等待、启动确认／失败恢复、启动异步检查、非模态按钮、GitHub CI ZIP／SHA-256 发行、许可门槛以及最终真实预览更新均未在本阶段实施。完整 GOAL 还包括最后 OCR／人工复核和交付清理，不能用此安装器绿灯代替。

## Task 2：正式新版查询最小能力

安装器已提交 `7e1a7ed`。查询通过固定仓库的官方 API 获取元数据，过滤草稿、预发行、相同／旧版本及旧工作流的非数字标签，只返回同一标签下 ZIP／SHA-256 两份成品；缺资产、坏 JSON、非 HTTPS／外部仓库地址明确失败，不冒充“没有新版本”。404 表示还没有发行，其它服务错误传播；取消与检查超时停止 HTTP。

`UpdateService` 经唯一 Core 注册提供窄 `IUpdateReleaseQuery`。开发目录没有 package-manifest 时直接返回空，不联网或写数据库；发行目录以实际安装清单版本比较，不能用源码程序集默认版本误判降级。检查不下载任何更新包。生产提供器采用 BCL HttpClient，JSON 文档保持 internal、纯转换在对应 Mapper，无新依赖。

| 冻结测试 | SHA-256 |
| --- | --- |
| GitHubUpdateProviderTests.cs | E455825554B13EB74D3C40C61923E3AF44ECB6240F77129527F52477EFBF95D8 |
| UpdateReleaseQueryTests.cs | ECE9A238F63286B921E7FB9B390B764A91AB4B90621D3D26B3176A4D66BBB374 |

提供器可编译骨架：15 项实际执行、10 个行为断言失败、5 通过；业务入口骨架：3 项实际执行、2 个值断言失败、1 个开发目录用例通过。两次构建均 0 警告／0 错误，0 跳过。仅补生产实现后，联合 **18/18** 转绿、解决方案 0 警告／0 错误，冻结 SHA 不变。测试只替换 HTTP 传输，真实解析、Mapper、发行目录文件读取、DI 和版本判断均执行，未访问真实网络。

提交前完整命令 `dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 2068 --max-threads 4` 实际 **2068/2068，通过、0 跳过**，5 分 58 秒；构建 0 警告／0 错误，两个冻结 SHA 不变、差异检查通过。本段不代表 UI、下载准备、隐藏进程、重启恢复或 CI 已实现；Task 2 整体仍未完成。
