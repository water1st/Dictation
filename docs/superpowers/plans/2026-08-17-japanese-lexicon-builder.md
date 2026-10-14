# 日语离线词库构建 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将用户提供的 `JMdict.gz` 流式转换为可由运行时只读查询的日语 SQLite 词库，按“词组 + 义项”建立稳定学习词条，并生成普通形、礼貌形、て形、た形。

**Architecture:** 新增独立 `.NET 10` 控制台项目 `Dictation.LexiconBuilder`。解析器按 `<entry>` 流式读取 JMdict，不把完整 XML 放入内存；词形生成器只处理已明确支持的现代动词类别；数据库写入器在临时文件中单事务构建，成功后原子替换目标。这个计划只交付真实可运行的构建工具，不接入 WPF，也不伪造尚未生成的中文释义。

**Tech Stack:** C# 14、.NET 10、`System.Xml`、`System.IO.Compression`、`Microsoft.Data.Sqlite` 10.0.10、xUnit v3。

## Global Constraints

- 只读取显式传入的本地 JMdict 文件；不使用网络、Python、EF、Repository、CUDA 或运行时下载。
- 原始 `Resources/`、生成的 SQLite、压缩包和临时数据库都不得提交到 Git。
- 一个 `ent_seq + sense index` 是一个稳定学习词条；同义项的全部写法、读法和活用属于该词条。
- JMdict 后续义项缺少 `<pos>` 时继承前一义项的词性；`stagk`、`stagr`、`re_restr` 和 `re_nokanji` 必须限制适用形式。
- 短语只保留固定词形，不参与动词活用。罕见汉字保留用于浏览，但标记为 `Rare`，后续出题层按无汉字处理。
- 词库保留 JMdict 原始多语言 gloss 供追溯，但只有含 English gloss 的 sense 建立学习词条；English gloss 是后续中文翻译的规范来源。不得假定其他语言 gloss 与英文逐条对齐。
- 所有测试先红后绿；worker 不执行 Git 操作，提交由控制器在独立验收后完成。

---

### Task 1: 流式解析 JMdict 为“词组 + 义项”模型

**Files:**
- Create: `Dictation.LexiconBuilder/Dictation.LexiconBuilder.csproj`
- Create: `Dictation.LexiconBuilder/JmDict/JmDictModels.cs`
- Create: `Dictation.LexiconBuilder/JmDict/JmDictParser.cs`
- Create: `Dictation.LexiconBuilder/Properties/AssemblyInfo.cs`
- Modify: `Dictation.sln`
- Modify: `Dictation.Tests/Dictation.Tests.csproj`
- Create: `Dictation.Tests/LexiconBuilder/JmDictParserTests.cs`

**Interfaces:**
- Produces: `IEnumerable<JmDictEntry> JmDictParser.Parse(Stream xmlStream)`。
- `JmDictEntry` 包含 `Sequence`、`Orthographies`、`Readings`、`Senses`；`JmDictSense` 包含从 1 开始的 `Index`、继承后的 `PartsOfSpeech`、限制项和原始多语言 gloss。

- [ ] **Step 1: 写最小 JMdict XML 行为测试**

```csharp
var entries = parser.Parse(CreateXmlStream(MiniJmDict)).ToArray();
Assert.Equal(12345, entries[0].Sequence);
Assert.Equal(new[] { "食べる" }, entries[0].Orthographies.Select(x => x.Text));
Assert.Equal("Ichidan verb", entries[0].Senses[1].PartsOfSpeech.Single());
Assert.Equal("dut", entries[0].Senses[1].Glosses.Single().Language);
```

测试还要用独立 fixture 覆盖：后续义项 POS 继承、`re_restr`、`re_nokanji`、`stagk`/`stagr`、默认 gloss 语言 `eng`、空流、无效 `ent_seq` 和缺失必需 reading。

- [ ] **Step 2: 运行聚焦测试并确认因类型尚不存在而失败**

Run: `dotnet test Dictation.Tests/Dictation.Tests.csproj -- --filter-class Dictation.Tests.LexiconBuilder.JmDictParserTests`

Expected: 编译失败，指出 `JmDictParser` 或模型类型不存在。

- [ ] **Step 3: 实现逐 entry 解析**

```csharp
var settings = new XmlReaderSettings
{
    DtdProcessing = DtdProcessing.Parse,
    XmlResolver = null,
    CloseInput = false
};
```

用 `XmlReader` 定位 `<entry>`，每次只把当前 entry 读为 `XElement`。实体解析后的 POS 描述原样保存；不得加载整个 `XDocument`。解析异常包装为包含序号或 XML 位置的中文 `LexiconBuildException`。

- [ ] **Step 4: 运行聚焦测试并确认全绿**

Run: `dotnet test Dictation.Tests/Dictation.Tests.csproj -- --filter-class Dictation.Tests.LexiconBuilder.JmDictParserTests`

- [ ] **Step 5: 由控制器在本任务与 Task 2、3 形成可运行工具后一并提交**

---

### Task 2: 生成现代日语动词目标词形

**Files:**
- Create: `Dictation.LexiconBuilder/JmDict/JapaneseFormGenerator.cs`
- Create: `Dictation.Tests/LexiconBuilder/JapaneseFormGeneratorTests.cs`

**Interfaces:**
- Consumes: `JmDictEntry` 与 `JmDictSense`。
- Produces: `IReadOnlyList<JapaneseLexiconForm> Generate(JmDictEntry entry, JmDictSense sense)`；每项含 `Surface`、`Reading`、`FormKind`、`ScriptKind`、`IsCommon`、`IsPrimary`。

- [ ] **Step 1: 写表驱动的字面期望测试**

```csharp
[Theory]
[InlineData("食べる", "たべる", "食べます", "食べて", "食べた")]
[InlineData("書く", "かく", "書きます", "書いて", "書いた")]
[InlineData("泳ぐ", "およぐ", "泳ぎます", "泳いで", "泳いだ")]
[InlineData("行く", "いく", "行きます", "行って", "行った")]
[InlineData("掃除する", "そうじする", "掃除します", "掃除して", "掃除した")]
[InlineData("来る", "くる", "来ます", "来て", "来た")]
```

另测 `話す`、`待つ`、`死ぬ`、`遊ぶ`、`読む`、`買う`、`ある`、`問う`，以及 kana 对应形式。名词只产生 Dictionary；`expressions (phrases, clauses, etc.)` 不活用；古语/未支持类别只保留 Dictionary，不能猜测。

- [ ] **Step 2: 运行测试并确认缺少生成器导致红灯**

Run: `dotnet test Dictation.Tests/Dictation.Tests.csproj -- --filter-class Dictation.Tests.LexiconBuilder.JapaneseFormGeneratorTests`

- [ ] **Step 3: 实现最小显式规则表**

```csharp
private static readonly IReadOnlyDictionary<char, GodanEnding> GodanEndings =
    new Dictionary<char, GodanEnding>
    {
        ['く'] = new("きます", "いて", "いた"),
        ['ぐ'] = new("ぎます", "いで", "いだ"),
        ['す'] = new("します", "して", "した")
    };
```

按 JMdict POS 描述选择 Ichidan、Godan、Iku special、U special、Suru、Kuru、Zuru；规则必须同时作用于写法和 reading，不能根据结尾猜测一段/五段类别。去重使用 ordinal 字符串键，排序固定为 Dictionary、Polite、Te、Ta，再按 primary/common/surface。

- [ ] **Step 4: 运行生成器测试和解析器测试并确认全绿**

- [ ] **Step 5: 做 mutation check**

临时把 `行く` 的 て形改成 `行いて`，确认对应测试失败；立即恢复。

---

### Task 3: 构建原子、可查询的 SQLite 词库

**Files:**
- Create: `Dictation.LexiconBuilder/BuilderOptions.cs`
- Create: `Dictation.LexiconBuilder/JmDict/JapaneseLexiconDatabaseBuilder.cs`
- Create: `Dictation.LexiconBuilder/Program.cs`
- Create: `Dictation.Tests/LexiconBuilder/JapaneseLexiconDatabaseBuilderTests.cs`

**Interfaces:**
- Produces CLI: `Dictation.LexiconBuilder --jmdict <JMdict.gz> --output <JapaneseLexicon.db>`。
- Produces tables: `BuildMetadata(Key, Value)`、`Entries(EntryId, SourceSequence, SenseIndex, PrimaryWritten, PrimaryReading, LexicalCategory, VerbClass, Transitivity, KanjiUsage, IsPhrase)`、`Glosses(EntryId, Language, GlossIndex, Text)`、`Forms(FormId, EntryId, Surface, Reading, FormKind, ScriptKind, IsCommon, IsPrimary)`。
- Stable `EntryId`: `jmdict:{SourceSequence}:{SenseIndex}`。

- [ ] **Step 1: 写真实 SQLite 集成测试**

用临时 gzipped mini JMdict 调用 builder，然后通过 `Microsoft.Data.Sqlite` 查询字面结果：

```sql
SELECT EntryId, PrimaryWritten, VerbClass FROM Entries ORDER BY EntryId;
SELECT Surface, Reading, FormKind FROM Forms WHERE EntryId = 'jmdict:12345:1';
```

断言不同 sense 生成不同 EntryId；相同 sense 的汉字/假名/活用都指向同一 EntryId；`IX_Forms_Surface` 存在；输入 SHA-256、JMdict 创建日期、schema version 和生成器版本进入 metadata。另测缺失参数、输入不存在、输出目录不存在、损坏 gzip 和失败时不覆盖已有目标文件。

- [ ] **Step 2: 运行集成测试并确认 builder/CLI 尚不存在而失败**

- [ ] **Step 3: 实现临时文件 + 单事务写入 + 原子替换**

输出先写到目标同目录的随机 `.tmp` 文件。启用 `PRAGMA journal_mode=OFF`、`synchronous=OFF` 仅用于一次性构建临时库；建表、批量 insert、索引和 metadata 在单事务中完成。成功关闭连接后使用 `File.Move(temp, output, overwrite: true)`；失败删除自己创建的 temp，保留原输出。

- [ ] **Step 4: 校验 CLI 退出码和中文错误**

成功返回 0；参数/输入错误返回 2；构建失败返回 1。不要输出每个词条，只输出来源、条目/义项/形式数量、目标路径和耗时摘要。

- [ ] **Step 5: 运行全部聚焦测试、全量测试和 build**

Run:

```powershell
dotnet restore Dictation.sln
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build
```

- [ ] **Step 6: 控制器提交可运行构建工具**

Commit: `词库：实现JMdict离线构建工具`

---

### Task 4: 用用户真实 JMdict 做构建验收

**Files:**
- Modify: `docs/resources/ThirdPartyResources.md`

**Interfaces:**
- Consumes: `D:\Source\Repos\Dictation\Resources\JMdict.gz`。
- Produces: 仅位于临时目录的验收数据库及构建统计；不提交数据库。

- [ ] **Step 1: 在临时目录运行 Release builder**

```powershell
dotnet run --project Dictation.LexiconBuilder/Dictation.LexiconBuilder.csproj --configuration Release -- --jmdict D:\Source\Repos\Dictation\Resources\JMdict.gz --output $env:TEMP\Dictation-Lexicon-Smoke\JapaneseLexicon.db
```

- [ ] **Step 2: 查询代表性词条**

用只读 SQLite 连接验证 `食べる/食べます/食べて/食べた`、`行く/行きます/行って/行った`、`掃除する/掃除します` 和 kana 形式能回到同一个相应 EntryId；验证不同义项独立、罕见/无汉字标记和 phrase 不活用。

- [ ] **Step 3: 记录可追溯统计**

把 schema version、输入 SHA-256、JMdict 创建日期、生成条目/义项/形式数量、数据库大小和数据库 SHA-256 写入资源文档；不得把临时绝对路径写进发布规格。

- [ ] **Step 4: 删除临时验收数据库并做最终检查**

确认工作树没有 `.db`、`.gz`、模型或临时文件；运行 `git diff --check`、全量 build/test。

- [ ] **Step 5: 控制器提交真实资源验收与文档**

Commit: `词库：验证日语离线词库产物`

## Self-Review

- Spec coverage: 本计划覆盖 JMdict 流式解析、不同义项独立学习词条、整组词形、日语现代动词四类目标形式、常用/罕见/无汉字元数据、短语固定组和可追溯只读 SQLite。
- Explicit non-goals: WPF/MCP 导入、用户库、FSRS、英语 WordNet 和中文翻译由后续独立垂直计划接入；本工具为这些功能提供稳定只读数据契约。
- Type consistency: parser 产出的 `JmDictEntry/JmDictSense` 是 form generator 和 database builder 的唯一输入；SQLite `EntryId` 在所有表中一致使用 `jmdict:{sequence}:{sense}`。
