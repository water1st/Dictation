# Microsoft 配置设置设计

## 目标

应用设置读取使用 `Microsoft.Extensions.Configuration` 的现成 JSON 配置提供器，不再由 DataAccess 手工打开、反序列化 `CONFIG/settings.json`。微软配置体系没有 JSON 回写 API，因此仅保留一个职责单一的文件写入器补足创建和持久化能力。

## 组件与边界

- `Program` 在 Generic Host 的配置管线末尾注册 `CONFIG/settings.json`，路径以 `AppContext.BaseDirectory` 为根，文件可缺失且不启用运行时文件监视。
- JSON 文件缺失或语法损坏时，配置源不阻止应用启动，`SettingsService` 使用系统主题和日语作为默认值。合法 JSON 中出现未知主题或语言值时仍抛出 `SettingsConfigurationException`，避免静默接受错误业务值。
- `SettingsService` 直接注入 `IConfiguration` 读取 `ThemeMode` 与 `LastLanguage`，读取为同步操作；它仍负责默认值、DTO/PO 映射和业务枚举校验。
- `SettingsFileWriter` 取代 `SettingsDataAccessor`，只负责把 `SettingsPo` 以 UTF-8 JSON 写入同目录临时文件，再替换 `CONFIG/settings.json`。保存成功后，`SettingsService` 同步更新当前 `IConfiguration` 的两个键，使同一进程后续读取立即可见。
- 不引入 `IOptions`、自定义 `ConfigurationProvider`、第三方可写配置库或设置 Repository。

保持现有扁平 JSON 键名，已有文件无需迁移：

```json
{
  "ThemeMode": "Dark",
  "LastLanguage": "English"
}
```

## 错误与数据安全

写入器继续保留同目录临时文件加替换写入，避免进程中断留下半个 JSON。取消、IO 错误和权限错误会清理临时文件；IO 与权限错误包装为 `SettingsDataAccessException`。业务值先通过 Mapper 校验，非法 DTO 不得创建或修改设置文件。

## 测试

- 使用真实 `ConfigurationBuilder`、`AddJsonFile` 和内存配置验证缺失、有效、损坏及非法业务值。
- 写入器测试覆盖创建、覆盖、空参数、预取消、目标路径冲突和临时文件清理。
- Service 测试覆盖默认值、保存后同进程可见、重新构建微软 JSON 配置后可恢复，以及非法枚举不落盘。
- HomeViewModel 测试改为通过 `IConfiguration` 构造真实 SettingsService，继续验证初始化、保存失败、状态保持和属性通知。

