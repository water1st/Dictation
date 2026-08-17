# 英语离线词库构建 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将用户提供的 Open English WordNet YAML 源码转换为可由运行时只读查询的英语 SQLite 词库；不同义项保持独立学习词条，同一词元的变形属于同一组，并保存直接派生关系。

**Architecture:** 继续扩展现有 `.NET 10` 控制台项目 `Dictation.LexiconBuilder`，不新增第二套构建工具。用 YamlDotNet 逐文件读取 `entries-*.yaml` 与词性 synset 文件；词形生成器以 WordNet 明确给出的例外为最高优先级，再用有限、可测试的英语拼写规则补齐。数据库写入沿用日语构建器的同目录临时文件、单事务和成功后原子替换。英语与日语输出为两个独立 SQLite 文件。

**Tech Stack:** C# 14、.NET 10、YamlDotNet 18.1.0、`Microsoft.Data.Sqlite` 10.0.10、xUnit v3。

## Global Constraints

- 只读取显式传入的本地 `english-wordnet` 目录；不运行该仓库的 Python 脚本，不联网下载，不引入 Humanizer、完整 NLP 框架或第二个 YAML 库。
- YamlDotNet 是本切片唯一新增的构建工具包依赖；解析器一次只反序列化一个 YAML 文件，不把整个词库装入单个对象图。
- `SenseId` 使用 Open English WordNet 原始 sense key；每个 sense 是独立学习词条。`LexemeId` 由来源 lemma 与原始 lexical-entry key 稳定生成，词形归属于 lexeme，多个 sense 复用同一套词形。`n-1/n-2`、`v-1/v-2`、`a-1/a-2` 等同词性变体必须保持独立，不能只按 lemma + 基础词性合并。
- 词元基础词性严格区分 noun、verb、adjective、adverb；相同拼写的不同词性不得合并。adjective satellite 由 sense 指向的 `-s` synset 保留，不能把同一个 `a` lexical entry 人为拆开。直接 derivation 只保存一跳边，不在构建期递归聚类。
- 含空白的词元是短语，只产生固定 Base 形式，不与动词、名词或形容词变形组合。`conversion/convert`、`execution/execute` 等仍是不同词元，只通过直接派生边关联。
- 来源 `form` 是权威例外拼写。分类器必须把每个来源 form 映射到明确的 `FormKind`；不能分类时构建失败并报告文件、lemma、词性和 form，禁止丢弃或猜测。
- 规则生成不能覆盖来源例外：例如 `go` 使用来源 `went/gone`，规则只补 `goes/going`，不得生成 `goed`；`stop` 使用来源 `stopped/stopping`，不得保留 `stoped/stoping`。
- 名词规则复数只表示词元的标准拼写，不推断某个义项是否可数；后续出题层允许按词条关闭不适用形式。形容词/副词不制造 `more/most` 词组，只保存来源明确的比较级/最高级和可确定的单词式规则结果。
- 原始 `Resources/`、生成数据库和临时文件不得提交。所有测试先证明会失败再转绿；mutation 代码立即恢复，绝不提交红状态。

---

### Task 1: 解析 WordNet YAML 为词元、义项和 synset 模型

**Files:**
- Modify: `Dictation.LexiconBuilder/Dictation.LexiconBuilder.csproj`
- Create: `Dictation.LexiconBuilder/WordNet/WordNetModels.cs`
- Create: `Dictation.LexiconBuilder/WordNet/WordNetYamlParser.cs`
- Create: `Dictation.Tests/LexiconBuilder/WordNetYamlParserTests.cs`

**Interfaces:**
- Produces: `IEnumerable<EnglishLexeme> ParseEntries(string yamlDirectory)`。
- Produces: `IReadOnlyDictionary<string, WordNetSynset> ParseSynsets(string yamlDirectory)`。
- `EnglishLexeme` 包含稳定 `LexemeId`、`Lemma`、`SourceEntryKey`、规范基础 `PartOfSpeech`、来源 forms、senses 和 `IsPhrase`；每个 sense 包含原始 `SenseId`、`SynsetId`、顺序和直接 derivation sense IDs。
- `WordNetSynset` 包含 `SynsetId`、词性、definitions 和 examples。

- [ ] **Step 1: 写最小 YAML 行为测试**

fixture 必须覆盖：同一 lemma 的 noun/verb 分离、同一 lemma 的 `n-1/n-2` 独立词元、`go` 的 `went/gone`、多个 sense、直接 derivation、synset definitions/examples、`a` 词元引用 adjective satellite synset、短语、重复 sense ID、缺失 synset、未知 lexical-entry key、无效 YAML 和空目录。

```csharp
var go = parser.ParseEntries(root)
    .Single(entry => entry.Lemma == "go" && entry.PartOfSpeech == EnglishPartOfSpeech.Verb);

Assert.Equal(new[] { "gone", "went" }, go.SourceForms);
Assert.All(go.Senses, sense => Assert.StartsWith("go%2:", sense.SenseId));
```

- [ ] **Step 2: 运行聚焦测试并确认因类型尚不存在而失败**

Run: `dotnet test Dictation.Tests/Dictation.Tests.csproj -- --filter-class Dictation.Tests.LexiconBuilder.WordNetYamlParserTests`

- [ ] **Step 3: 用 YamlDotNet 实现逐文件解析和全局引用校验**

只映射实际使用字段，忽略 WordNet 其他关系，避免复制完整 schema。文件按 ordinal 相对路径排序，条目和列表保持稳定顺序。全部文件解析后一次校验 sense ID 唯一、synset 存在、derivation 目标存在；异常统一包装为带来源文件的中文 `LexiconBuildException`。

- [ ] **Step 4: 运行解析测试并做 mutation check**

临时跳过重复 sense ID 校验，确认重复测试失败；立即恢复并确认聚焦测试全绿。

---

### Task 2: 生成并审计英语整组词形

**Files:**
- Create: `Dictation.LexiconBuilder/WordNet/EnglishFormGenerator.cs`
- Create: `Dictation.LexiconBuilder/WordNet/EnglishIrregularParadigms.cs`
- Create: `Dictation.Tests/LexiconBuilder/EnglishFormGeneratorTests.cs`

**Interfaces:**
- Produces: `IReadOnlyList<EnglishLexiconForm> Generate(EnglishLexeme lexeme)`。
- `EnglishLexiconForm` 包含 `Surface`、`FormKind` 和 `FormOrigin`（`Source`/`Rule`）。同一拼写可属于多个 kind，例如 `made` 同时是过去式与过去分词。
- Form kinds：`Base`；动词 `ThirdPersonSingular/Past/PastParticiple/PresentParticiple`，并保留 `be` 所需的特殊现在时人称形式；名词 `Plural`；形容词/副词 `Comparative/Superlative`。

- [ ] **Step 1: 写字面完整词组测试**

```csharp
[Theory]
[InlineData("go", "go", "goes", "went", "gone", "going")]
[InlineData("do", "do", "does", "did", "done", "doing")]
[InlineData("play", "play", "plays", "played", "played", "playing")]
[InlineData("study", "study", "studies", "studied", "studied", "studying")]
[InlineData("stop", "stop", "stops", "stopped", "stopped", "stopping")]
[InlineData("make", "make", "makes", "made", "made", "making")]
```

另测：`child/children`、`good/better/best`、`happy/happier/happiest`、`be/am/is/are/was/were/been/being`、同一 kind 的英美拼写变体、同形过去式/过去分词、短语 `take off` 只有 Base，以及未知来源例外必须抛出包含上下文的异常。

- [ ] **Step 2: 运行聚焦测试并确认生成器缺失导致红灯**

Run: `dotnet test Dictation.Tests/Dictation.Tests.csproj -- --filter-class Dictation.Tests.LexiconBuilder.EnglishFormGeneratorTests`

- [ ] **Step 3: 实现最小确定性规则和显式例外范式**

规则只负责可由拼写确定的部分：动词 `-s/-es/-ies`、`-ed/-ied`、`-ing`；名词 `-s/-es/-ies`。形容词/副词不根据 lemma 自动制造比较级，而只分类来源明确给出的 `-er/-est` 或显式例外；`beautiful` 等没有来源比较形式时只保留 Base。`-f/-fe` 等存在大量反例的名词变化不靠通用规则猜测。来源 form 先由 suffix 规则识别，再由小型显式范式解决 `go/do/be/run/come/good` 等无法从拼写判定的情况。多个可接受拼写全部保留；最终按 kind、来源优先级、surface 固定排序并去重。

- [ ] **Step 4: 做 mutation check 并确认全绿**

临时让 `go` 产生 `goed` 或交换 `went/gone` 类型，确认字面测试失败；立即恢复。运行解析与生成器全部聚焦测试。

- [ ] **Step 5: 控制器提交纯模型、解析和词形逻辑**

Commit: `词库：实现WordNet解析与英语词形规则`

---

### Task 3: 接通原子 SQLite 构建和双来源 CLI

**Files:**
- Modify: `Dictation.LexiconBuilder/BuilderOptions.cs`
- Modify: `Dictation.LexiconBuilder/Program.cs`
- Create: `Dictation.LexiconBuilder/WordNet/EnglishLexiconDatabaseBuilder.cs`
- Create: `Dictation.Tests/LexiconBuilder/EnglishLexiconDatabaseBuilderTests.cs`
- Modify: `Dictation.Tests/LexiconBuilder/JapaneseLexiconDatabaseBuilderTests.cs`

**Interfaces:**
- Japanese CLI 保持：`Dictation.LexiconBuilder --jmdict <JMdict.gz> --output <JapaneseLexicon.db>`。
- English CLI 新增：`Dictation.LexiconBuilder --wordnet <english-wordnet-root> --output <EnglishLexicon.db>`。
- `--jmdict` 与 `--wordnet` 必须且只能出现一个；日语既有退出码和输出不回归。
- Produces tables:
  - `BuildMetadata(Key, Value)`
  - `Lexemes(LexemeId, Lemma, PartOfSpeech, IsPhrase)`
  - `Senses(SenseId, LexemeId, SynsetId, SenseOrder)`
  - `Synsets(SynsetId, PartOfSpeech)`
  - `Definitions(SynsetId, DefinitionIndex, Text)`
  - `Examples(SynsetId, ExampleIndex, Text)`
  - `Forms(FormId, LexemeId, Surface, FormKind, FormOrigin)`
  - `Derivations(SourceSenseId, TargetSenseId)`

- [ ] **Step 1: 写真实 SQLite 与 CLI 集成测试**

用临时 mini WordNet 目录调用 builder，字面查询并断言：

- 不同 sense 是不同学习 ID，但指向相同 LexemeId 和同一组 forms；
- noun `goes` 不会进入 verb `go`，不同词性完全分离；
- `conversion -> convert` 只保存直接边，不生成传递闭包；
- definition/example 回到正确 synset；
- `IX_Lexemes_Lemma`、`IX_Forms_Surface`、`IX_Senses_SynsetId` 存在；
- metadata 保存 schema/generator/source name/source tree fingerprint/counts；Git commit 由真实资源验收文档与 fingerprint 建立对应，不要求构建器解析 `.git`；
- 缺失文件、损坏 YAML、未知例外、无效参数失败时保留既有目标数据库且清理自己的 temp。

- [ ] **Step 2: 运行集成测试并确认数据库构建器尚不存在而失败**

- [ ] **Step 3: 实现英语数据库构建器**

复用日语构建器已经验证过的 SQLite 设置和原子替换模式，不抽象一套没有第二种行为的 Repository。来源 fingerprint 对排序后的相关 YAML 相对路径与文件内容做增量 SHA-256；不哈希 `.git`、文档或 Python 工具。所有表、数据、索引和 metadata 在临时数据库的单事务内完成。

- [ ] **Step 4: 扩展 CLI 而不复制入口**

`BuilderOptions` 表达唯一来源种类和路径，`LexiconBuilderCli` 只负责参数、调度、退出码和中文摘要。参数错误返回 2，构建失败返回 1，成功返回 0；摘要打印 lexemes/senses/synsets/forms/derivations、输出路径与耗时，不逐词输出。

- [ ] **Step 5: 运行聚焦测试、全量测试和 build**

```powershell
dotnet restore Dictation.sln
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build
```

- [ ] **Step 6: 控制器提交完整英语构建链路**

Commit: `词库：接通WordNet离线数据库构建`

---

### Task 4: 用用户真实 English WordNet 做构建验收

**Files:**
- Modify: `docs/resources/ThirdPartyResources.md`

**Interfaces:**
- Consumes: `D:\Source\Repos\Dictation\Resources\english-wordnet`。
- Produces: 仅位于临时目录的验收数据库、未分类审计和构建统计；不修改原始仓库，不提交数据库。

- [ ] **Step 1: 固定并校验输入版本**

确认输入 Git 工作树 clean、commit 为文档登记的 `02ff9f3f5bc0a25592e7263ffdbc9bcb6564936b`；记录实际相关 YAML fingerprint。输入版本变化时停止并更新基线，不能沿用旧统计。

- [ ] **Step 2: 运行 Release 构建并收敛词形审计**

```powershell
dotnet run --project Dictation.LexiconBuilder/Dictation.LexiconBuilder.csproj --configuration Release -- --wordnet D:\Source\Repos\Dictation\Resources\english-wordnet --output $env:TEMP\Dictation-Lexicon-Smoke\EnglishLexicon.db
```

若报告未分类来源 form，只根据实际 lemma/POS/form 增加最小范式和回归测试，重复直到 `unclassified_source_forms=0`；不得用 `Other` 吞掉错误。

- [ ] **Step 3: 查询批准的代表性业务组**

验证 `go/goes/went/gone/going`、`do/does/did/done/doing`、`child/children`、`good/better/best`；确认 `conversion/convert` 与 `execution/execute` 只有直接派生关联，不被合成一个词；短语无额外变形；不同义项独立且共享正确词元形式。

- [ ] **Step 4: 记录可追溯统计并清理临时产物**

在资源文档记录 source commit、tree fingerprint、schema/generator 版本、各表数量、未分类数量 0、SQLite 大小/SHA-256 和 `PRAGMA integrity_check=ok`。删除经过绝对路径核验的临时数据库，确认工作树没有 `.db`、YAML 副本或资源目录。

- [ ] **Step 5: 最终验证和文档提交**

运行 `git diff --check`、全量 build/test，并提交：`词库：记录WordNet真实构建验收`。

## Self-Review

- Spec coverage: 不同义项独立、全部变形归组、`go/do/child/good` 字面验收、短语独立、直接 noun/verb/adj/adv 派生关系、离线只读数据库均有明确契约。
- Reliability boundary: Open English WordNet 的 YAML forms 没有 grammatical tag；计划通过确定性分类、显式范式和真实资源零未分类门禁解决，不把未验证推断带入答题数据。
- Explicit non-goals: 本切片不接入 WPF/MCP/用户学习库，不翻译中文，不实现出题去重、英式 TTS 或复习算法；这些功能后续只消费这里稳定的 SenseId/LexemeId/Forms 契约。
- Simplicity: 复用一个构建项目、一套 CLI 和既有原子 SQLite 模式，只新增一个必要的 YAML 解析包；不引入 DDD、Repository、Python 运行时或完整 NLP 框架。
