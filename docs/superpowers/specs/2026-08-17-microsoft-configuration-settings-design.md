# Microsoft 配置设置设计

## 目标

应用设置通过一个内聚的 `Dictation.Services.Configuration` 模块完成读取、校验和写回。读取复用 `Microsoft.Extensions.Configuration` 已有扩展；微软配置体系没有 JSON 写回 API，因此在同一模块内为 `IConfiguration` 补充最小泛型 `SetValueAsync<T>` 扩展，不再手写读取器，也不把配置放进普通 DAL。

## 组件与边界

- `Program` 在 Generic Host 配置管线末尾注册根目录下的 `CONFIG/settings.json`。文件可缺失，不启用运行时文件监视；语法损坏时忽略该配置源并使用默认值，不能阻止应用启动。
- `SettingsService` 是 Presentation 唯一使用的设置入口。它注入 `IConfiguration` 和 `ApplicationPaths`，通过微软的 `GetValue<string>()` 读取 `ThemeMode`、`LastLanguage`，负责默认值和业务编排。
- `ApplicationSettingsDocument`、`ApplicationSettingsMapper`、`SettingsPersistenceException` 与写入扩展均位于 `Dictation.Services.Configuration`。文件模型只保存字符串，Mapper 负责与 DTO 枚举转换并拒绝未知业务值。
- 内部 `IConfiguration.SetValueAsync<T>(...)` 把一个完整配置文档作为 `T` 一次性写入：创建父目录、同目录临时文件序列化与替换，成功后调用微软的 `IConfigurationRoot.Reload()` 刷新当前配置。SettingsService 每次只调用一次，主题与语言不会出现半更新。
- DataAccess 不读取或写入设置；`ApplicationPaths` 仅提供统一根目录路径。不存在 `SettingsDataAccessor`、`SettingsFileWriter`、设置 Repository、自定义 `ConfigurationProvider` 或第三方可写配置库。

保持现有扁平 JSON 键名，已有文件无需迁移：

```json
{
  "ThemeMode": "Dark",
  "LastLanguage": "English"
}
```

## 错误与数据安全

两个键都缺失时返回系统主题和日语；仅缺一项或值未知时抛出 `SettingsConfigurationException`。写入前先校验 DTO，非法值不得触碰文件或当前配置。取消操作原样传播；IO 与权限错误包装为 `SettingsPersistenceException`，并尽力清理临时文件。扩展在写入前确认传入配置实现了 `IConfigurationRoot`，避免文件落盘后才发现无法 Reload。

## 测试

- 使用真实 `ConfigurationBuilder`、`AddJsonFile`、微软 `GetValue<T>()` 和 `IConfigurationRoot.Reload()` 验证缺失、有效、损坏、部分缺失及非法业务值。
- 通过 `SettingsService` 覆盖首次创建、覆盖保存、同进程立即可见、重新 Build 后恢复、空参数、预取消、路径冲突、临时文件清理和失败不改变当前配置。
- HomeViewModel 测试继续验证初始化、保存失败、状态保持、属性通知和损坏 JSON 默认启动。
- 搜索生产代码，确保不存在设置 JSON 的手工反序列化，也不存在 DataAccess 设置读写类。
