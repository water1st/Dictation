# 日中／英中运行时词库 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将日语和英语运行时查词结果从英文释义修正为中文释义：日语以 JMdict 结构加 Tomoshi 精确中文，英语以 Open English WordNet 结构加 Chinese Open WordNet 精确中文，并用 ECDICT 为未覆盖义项生成至多一个宽泛回退；缺失中文时允许进入 Import Inbox 补写，但不能进入学习或复习。

**Architecture:** 构建器离线合并权威结构源和中文源，生成 schema v2 的两个只读 SQLite；DataAccess 只查询面向学习的中文表，Service 映射来源、精度和缺失状态；Import Inbox 保存可编辑中文快照，并在 Service 层执行学习／复习门禁。英文 definition 和 JMdict 原始 gloss 只保留在审计表，绝不作为 UI 回退。

**Tech Stack:** C# 14、.NET 10、System.Text.Json、System.Xml、Dapper 2.1.79、Microsoft.Data.Sqlite 10.0.10、YamlDotNet 18.1.0、xUnit v3。

**Spec:** `docs/superpowers/specs/2026-08-31-single-window-chinese-lexicon-design.md`

## Global Constraints

- 本计划先于单窗口页面计划执行；本计划不修改 `Dictation.Presentation` 生产代码，并保留当前未提交的 Task 2 文件。若 schema v2 fixture 使当前未提交的 Presentation 测试仍断言英文，只允许把该测试数据／断言机械改为中文并留给下一份计划提交，不能借机改页面结构。
- 不增加新的 NuGet 包。Tomoshi 与 ECDICT 直接用现有 `Microsoft.Data.Sqlite`；JSON/XML 使用 .NET BCL。
- 运行时仍只读 `Resources/Lexicons/JapaneseLexicon.db` 和 `EnglishLexicon.db`，不读取原始词库、不联网、不反向查询中文。
- 两个生成库的 `schema_version` 同步从 `1` 升到 `2`。旧 schema 必须给出中文错误，不能被当作空结果。
- `SourceSenseId` 继续作为公开稳定学习义项 ID：精确英语义项沿用 OEWN SenseId；宽泛义项使用 `ecdict:{normalized-lemma}:{pos}`；完全缺失使用 `missing:{normalized-lemma}:{pos}`。
- 同一规范化 lemma + POS 的未覆盖 OEWN Sense 只生成一个宽泛或缺失义项；不能把一条 ECDICT 翻译复制成多个“精确义项”。
- 用户修改只写 `DATA/UserData.db` 的 Inbox 快照；绝不修改两个不可变词库。
- 每个 RED 必须先让测试项目编译成功，再运行并发现目标测试，产生断言或业务行为失败。编译错误、零测试、还原失败和环境错误均不算 RED。
- RED 前记录目标测试文件 SHA-256；测试冻结后只 mutation／修正生产代码；GREEN 后再次确认测试哈希未变。临时 mutation 不得提交。
- 原始 Tomoshi、COW、ECDICT、JMdict 和 OEWN 文件不提交；真实构建输出放临时目录，源码范围不得出现 `*.db`。

---

## File and Contract Map

### Builder inputs and Japanese merge

- Modify: `Dictation.LexiconBuilder/BuilderOptions.cs`
- Modify: `Dictation.LexiconBuilder/Program.cs`
- Create: `Dictation.LexiconBuilder/JmDict/TomoshiChineseGlossIndex.cs`
- Modify: `Dictation.LexiconBuilder/JmDict/JapaneseLexiconDatabaseBuilder.cs`
- Modify: `Dictation.LexiconBuilder/JmDict/JmDictModels.cs` only if a named Chinese-meaning model is required
- Modify: `Dictation.Tests/LexiconBuilder/JapaneseLexiconDatabaseBuilderTests.cs`
- Create: `Dictation.Tests/LexiconBuilder/TomoshiChineseGlossIndexTests.cs`

### English exact and fallback merge

- Modify: `Dictation.LexiconBuilder/WordNet/WordNetModels.cs`
- Modify: `Dictation.LexiconBuilder/WordNet/WordNetYamlParser.cs`
- Create: `Dictation.LexiconBuilder/WordNet/ChineseOpenWordNetParser.cs`
- Create: `Dictation.LexiconBuilder/WordNet/EcdictTranslationReader.cs`
- Create: `Dictation.LexiconBuilder/WordNet/EnglishLearningSenseBuilder.cs`
- Modify: `Dictation.LexiconBuilder/WordNet/EnglishLexiconDatabaseBuilder.cs`
- Modify: `Dictation.LexiconBuilder/WordNet/EnglishLexiconBuildSummary.cs`
- Modify/Create: corresponding tests under `Dictation.Tests/LexiconBuilder/`

### Runtime and Inbox

- Modify: `Dictation.DataAccess/Lexicon/JapaneseLexiconLookupPo.cs`
- Modify: `Dictation.DataAccess/Lexicon/EnglishLexiconLookupPo.cs`
- Modify: `Dictation.DataAccess/Lexicon/JapaneseLexiconDataAccessor.cs`
- Modify: `Dictation.DataAccess/Lexicon/EnglishLexiconDataAccessor.cs`
- Modify: `Dictation.Services/Lexicon/LexiconLookupResultDto.cs`
- Modify: `Dictation.Services/Lexicon/LexiconLookupService.cs`
- Modify: `Dictation.DataAccess/Runtime/SchemaMigrationRunner.cs`
- Modify: `Dictation.DataAccess/Importing/ImportInboxPo.cs`
- Modify: `Dictation.DataAccess/Importing/ImportInboxDataAccessor.cs`
- Modify: `Dictation.Services/Importing/ImportInboxDto.cs`
- Modify: `Dictation.Services/Importing/ImportInboxService.cs`
- Modify: `Dictation.Tests/Infrastructure/LexiconTestDatabases.cs`
- Modify: corresponding DataAccess/Service tests

### Reproducibility and documentation

- Implement: `scripts/Build-Lexicons.ps1`
- Modify: `docs/resources/ThirdPartyResources.md`
- Modify: `docs/requirements/RefactorSpecification.md` only where it still states that English gloss is the runtime canonical meaning

## Fixed Public Models

Service uses one shared metadata vocabulary:

```csharp
public enum LexiconMeaningSource
{
    Tomoshi,
    ChineseOpenWordNet,
    Ecdict,
    User,
    Missing
}

public enum LexiconMeaningPrecision
{
    Exact,
    Broad,
    UserProvided,
    Missing
}
```

`LexiconLookupResultDto` and `ImportWordGroupMemberDto` keep their current fields and add `MeaningSource`, `MeaningPrecision` and `HasChineseMeaning`. New constructor arguments are appended after existing arguments during this plan so current uncommitted Presentation code remains compilable; production mappers must always pass explicit metadata. `HasChineseMeaning` is true only when the normalized `Meanings` collection contains at least one non-empty value; “暂无中文释义” is Presentation text, never stored as a meaning.

Builder options represent only valid combinations:

```csharp
internal sealed class BuilderOptions
{
    internal static BuilderOptions ForJapanese(
        string jmDictPath,
        string tomoshiDatabasePath,
        string outputPath);

    internal static BuilderOptions ForEnglish(
        string wordNetDirectory,
        string chineseWordNetXmlPath,
        string ecdictDatabasePath,
        string outputPath);
}
```

CLI syntax is fixed:

```text
--jmdict <JMdict.gz> --tomoshi <tomoshi.db> --output <JapaneseLexicon.db>
--wordnet <yaml-dir> --cow <omw-cmn.xml> --ecdict <stardict.db> --output <EnglishLexicon.db>
```

---

### Task 1: Parse Tomoshi Chinese meanings by exact JMdict sense

**Files:**
- Create: `Dictation.LexiconBuilder/JmDict/TomoshiChineseGlossIndex.cs`
- Create: `Dictation.Tests/LexiconBuilder/TomoshiChineseGlossIndexTests.cs`

**Contract:**

```csharp
internal sealed class TomoshiChineseGlossIndex
{
    internal static TomoshiChineseGlossIndex Load(string databasePath);

    internal IReadOnlyList<string> GetMeanings(
        long jmDictEntryId,
        int oneBasedSenseIndex);
}
```

- [ ] **Step 1: Add a real-shape Tomoshi fixture and tests**

Create a temporary SQLite with the actual table:

```sql
CREATE TABLE zh_defs (
    entry_id TEXT NOT NULL,
    locale TEXT NOT NULL DEFAULT 'zh-CN',
    data TEXT NOT NULL,
    PRIMARY KEY (entry_id, locale)
);
```

Insert `1358280` with JSON keys `"0"` and `"1"`. Assert that one-based sense 1 and 2 return different Chinese lists, normalization removes blank/duplicate values while preserving order, an absent entry/sense returns an empty list, and malformed JSON or a non-positive sense index throws a source-specific exception.

- [ ] **Step 2: Establish a behavioral RED**

Add only enough production surface to compile. Run:

```powershell
dotnet build Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
Get-FileHash Dictation.Tests/LexiconBuilder/TomoshiChineseGlossIndexTests.cs -Algorithm SHA256
```

Temporarily look up JSON key `oneBasedSenseIndex` instead of `oneBasedSenseIndex - 1`, then run:

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.LexiconBuilder.TomoshiChineseGlossIndexTests
```

Expected RED: tests are discovered and compiled, but sense 1 returns the second meaning or an empty list. Restore that production mutation before continuing.

- [ ] **Step 3: Implement the minimal index**

Open Tomoshi with `Mode=ReadOnly;Pooling=False`, stream `entry_id, data` for `locale='zh-CN'`, parse with `JsonDocument`, convert zero-based JSON sense keys to one-based indexes, trim + NFC-normalize + ordinal-deduplicate gloss text, and dispose every reader/connection. Missing data is not an error.

- [ ] **Step 4: Verify GREEN and commit**

Run the focused class, recheck the frozen test hash, then:

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.LexiconBuilder.TomoshiChineseGlossIndexTests
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
```

Commit only this adapter and its tests:

```powershell
git commit -m "词库：解析Tomoshi日语中文义项"
```

---

### Task 2: Build Japanese schema v2 with Chinese-only learning meanings

**Files:**
- Modify: `Dictation.LexiconBuilder/BuilderOptions.cs`
- Modify: `Dictation.LexiconBuilder/Program.cs`
- Modify: `Dictation.LexiconBuilder/JmDict/JapaneseLexiconDatabaseBuilder.cs`
- Modify: `Dictation.Tests/LexiconBuilder/JapaneseLexiconDatabaseBuilderTests.cs`

**Schema:**

Keep `Entries`, `PartsOfSpeech` and `Forms`. Replace the ambiguous runtime use of `Glosses` with:

```sql
CREATE TABLE SourceGlosses (
    EntryId TEXT NOT NULL,
    Language TEXT NOT NULL,
    GlossIndex INTEGER NOT NULL,
    GlossType TEXT NULL,
    Text TEXT NOT NULL,
    PRIMARY KEY (EntryId, Language, GlossIndex)
);

CREATE TABLE ChineseMeanings (
    EntryId TEXT NOT NULL,
    MeaningIndex INTEGER NOT NULL,
    Text TEXT NOT NULL,
    MeaningSource TEXT NOT NULL CHECK (MeaningSource = 'Tomoshi'),
    MeaningPrecision TEXT NOT NULL CHECK (MeaningPrecision = 'Exact'),
    PRIMARY KEY (EntryId, MeaningIndex)
);
```

- [ ] **Step 1: Extend builder and CLI tests**

Assert `--jmdict` requires exactly one `--tomoshi` and rejects `--cow/--ecdict`. Build a two-sense `食べる` fixture where Tomoshi supplies different Chinese meanings; assert `jmdict:1358280:1` and `:2` remain separate, only Chinese appears in `ChineseMeanings`, original English stays in `SourceGlosses`, and a missing Tomoshi sense still has its Entry/forms with zero Chinese meanings.

- [ ] **Step 2: Establish a behavioral RED**

After a successful test-project build and frozen test hash, temporarily write `schema_version=1`. Run the Japanese builder class. Expected RED: the database builds, but the schema assertion expects `2` and receives `1`. Restore the mutation.

- [ ] **Step 3: Implement schema v2 and metadata**

Use `BuilderOptions.ForJapanese` and the index from Task 1. Write metadata keys for JMdict SHA-256, Tomoshi DB SHA-256, source creation date, Chinese meaning count, missing-Chinese entry count and generator version. Insert all original glosses into `SourceGlosses` and only Tomoshi `zh-CN` text into `ChineseMeanings`. No string similarity or English fallback is allowed.

- [ ] **Step 4: Verify representative forms remain unchanged**

Tests must retain existing assertions for `食べる`, `行く` and `掃除／掃除する` forms, common/rare/no-kanji classification, separate Sense indexes and deterministic row order.

- [ ] **Step 5: GREEN and commit**

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.LexiconBuilder.JapaneseLexiconDatabaseBuilderTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.LexiconBuilder.TomoshiChineseGlossIndexTests
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
git commit -m "词库：生成日中运行时词库"
```

---

### Task 3: Preserve OEWN ILI and parse Chinese Open WordNet

**Files:**
- Modify: `Dictation.LexiconBuilder/WordNet/WordNetModels.cs`
- Modify: `Dictation.LexiconBuilder/WordNet/WordNetYamlParser.cs`
- Create: `Dictation.LexiconBuilder/WordNet/ChineseOpenWordNetParser.cs`
- Modify/Create: `Dictation.Tests/LexiconBuilder/WordNetYamlParserTests.cs`
- Create: `Dictation.Tests/LexiconBuilder/ChineseOpenWordNetParserTests.cs`

**Contract:**

```csharp
internal sealed class EnglishSynset
{
    public string SynsetId { get; }
    public string? Ili { get; }
}

internal sealed class ChineseOpenWordNetParser
{
    internal IReadOnlyDictionary<string, IReadOnlyList<string>> ParseByIli(
        string xmlPath);
}
```

- [ ] **Step 1: Test the real WN-LMF shape**

The fixture must contain `LexicalEntry/Lemma/Sense` and `Synset ili="i..." members="..."`. Assert member IDs resolve to ordered simplified-Chinese lemma text, repeated lemmas are ordinal-deduplicated, missing/blank ILI is skipped, unknown member references and duplicate conflicting member definitions throw `LexiconBuildException`.

Add OEWN YAML fixture assertions that `ili` is preserved as nullable text and does not replace `SynsetId`.

- [ ] **Step 2: Establish behavioral RED**

Build tests successfully, freeze both test hashes, then temporarily set every parsed OEWN `Ili` to null. Expected RED: the parser test expects the fixture ILI but receives null. Restore the mutation.

- [ ] **Step 3: Implement streaming XML and YAML mapping**

Use two streaming `XmlReader` passes from the BCL with `DtdProcessing=Ignore` and `XmlResolver=null`: the first pass maps COW Sense/member IDs to Lemma text; the second maps Synset ILI to declared member order. Normalize all text to NFC. Add nullable `Ili` to `RawSynset` and `EnglishSynset`.

- [ ] **Step 4: GREEN and commit**

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.LexiconBuilder.WordNetYamlParserTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.LexiconBuilder.ChineseOpenWordNetParserTests
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
git commit -m "词库：对齐英语ILI中文义项"
```

---

### Task 4: Generate one POS-aware ECDICT fallback per uncovered word

**Files:**
- Create: `Dictation.LexiconBuilder/WordNet/EcdictTranslationReader.cs`
- Create: `Dictation.LexiconBuilder/WordNet/EnglishLearningSenseBuilder.cs`
- Create: `Dictation.Tests/LexiconBuilder/EcdictTranslationReaderTests.cs`
- Create: `Dictation.Tests/LexiconBuilder/EnglishLearningSenseBuilderTests.cs`

**Contracts:**

```csharp
internal sealed class EcdictTranslationReader : IDisposable
{
    internal EcdictTranslationReader(string databasePath);

    internal IReadOnlyList<string> Read(
        string normalizedLemma,
        EnglishPartOfSpeech partOfSpeech);
}

internal sealed record EnglishLearningSense(
    string LearningSenseId,
    string SourceLexemeId,
    string Headword,
    EnglishPartOfSpeech PartOfSpeech,
    bool IsPhrase,
    string MeaningSource,
    string MeaningPrecision,
    int SortOrder,
    IReadOnlyList<string> SourceSenseIds,
    IReadOnlyList<string> Meanings);
```

For exact items, `SourceLexemeId` remains the OEWN LexemeId. For broad/missing items it is the stable family ID `oewn:{normalized-lemma}:{pos}`. `LearningSenseSources` retains every underlying OEWN Sense, so runtime form union and one-hop derivation lookup never depend on choosing an arbitrary canonical lexeme.

- [ ] **Step 1: Test ECDICT POS filtering**

Use the real `stardict` columns `word, translation, pos`. Cover `go`, `do`, `child` and `good`. Map OEWN noun to `n.`, verb to `v./vi./vt.`, adjective/satellite to `a./adj.` and adverb to `ad./adv.`. Strip only the recognized prefix; preserve the rest of each Chinese line as one broad meaning. Wrong-POS lines must not leak into a result.

- [ ] **Step 2: Test exact/broad/missing assembly**

Given one mapped ILI and two uncovered senses for the same lemma/POS, assert:

```csharp
Assert.Single(results, item => item.MeaningPrecision == "Exact");
var broad = Assert.Single(results, item => item.MeaningPrecision == "Broad");
Assert.Equal("ecdict:go:v", broad.LearningSenseId);
Assert.Equal(2, broad.SourceSenseIds.Count);
```

When ECDICT also lacks text, assert exactly one `missing:go:v` with empty meanings. Exact senses never receive ECDICT text.

- [ ] **Step 3: Establish behavioral RED**

After compile and hash freeze, temporarily emit one fallback per uncovered source Sense rather than grouping by normalized lemma + POS. Expected RED: the test expects one broad item but receives two. Restore the mutation.

- [ ] **Step 4: Implement deterministic assembly**

Keep one read-only ECDICT connection and one prepared parameterized lookup command. Cache by normalized lemma. Exact COW matches create one learning item per OEWN Sense; remaining senses group by lemma + POS and become one broad or missing item. Sort exact items by OEWN sense order and append the single fallback. Use ordinal stable IDs and ordering.

- [ ] **Step 5: GREEN and commit**

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.LexiconBuilder.EcdictTranslationReaderTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.LexiconBuilder.EnglishLearningSenseBuilderTests
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
git commit -m "词库：补全英语宽泛中文义项"
```

---

### Task 5: Build English schema v2 learning views

**Files:**
- Modify: `Dictation.LexiconBuilder/BuilderOptions.cs`
- Modify: `Dictation.LexiconBuilder/Program.cs`
- Modify: `Dictation.LexiconBuilder/WordNet/EnglishLexiconDatabaseBuilder.cs`
- Modify: `Dictation.LexiconBuilder/WordNet/EnglishLexiconBuildSummary.cs`
- Modify: `Dictation.Tests/LexiconBuilder/EnglishLexiconDatabaseBuilderTests.cs`

**Schema additions:**

```sql
CREATE TABLE Synsets (
    SynsetId TEXT NOT NULL PRIMARY KEY,
    PartOfSpeech TEXT NOT NULL,
    Ili TEXT NULL
);

CREATE TABLE LearningSenses (
    LearningSenseId TEXT NOT NULL PRIMARY KEY,
    SourceLexemeId TEXT NOT NULL,
    Headword TEXT NOT NULL,
    PartOfSpeech TEXT NOT NULL,
    IsPhrase INTEGER NOT NULL CHECK (IsPhrase IN (0, 1)),
    MeaningSource TEXT NOT NULL CHECK (MeaningSource IN ('ChineseOpenWordNet', 'Ecdict', 'Missing')),
    MeaningPrecision TEXT NOT NULL CHECK (MeaningPrecision IN ('Exact', 'Broad', 'Missing')),
    SortOrder INTEGER NOT NULL
);

CREATE TABLE LearningSenseSources (
    LearningSenseId TEXT NOT NULL,
    SenseId TEXT NOT NULL,
    PRIMARY KEY (LearningSenseId, SenseId),
    FOREIGN KEY (LearningSenseId) REFERENCES LearningSenses (LearningSenseId),
    FOREIGN KEY (SenseId) REFERENCES Senses (SenseId)
);

CREATE TABLE LearningMeanings (
    LearningSenseId TEXT NOT NULL,
    MeaningIndex INTEGER NOT NULL,
    Text TEXT NOT NULL,
    PRIMARY KEY (LearningSenseId, MeaningIndex),
    FOREIGN KEY (LearningSenseId) REFERENCES LearningSenses (LearningSenseId)
);
```

- [ ] **Step 1: Extend CLI and schema tests**

`--wordnet` must require exactly one `--cow` and `--ecdict` and reject `--tomoshi`. Assert schema v2, ILI persistence, exact/broad/missing rows, mapping rows, deterministic order and that `LearningMeanings` never contains an OEWN English definition.

- [ ] **Step 2: Protect all approved form groups**

Retain explicit database tests for:

```text
go / goes / went / gone / going
do / does / did / done / doing
child / children
good / better / best
```

Phrase entries remain Base-only. Direct derivations remain one hop and do not merge phrases.

- [ ] **Step 3: Establish behavioral RED**

After compile and test-hash freeze, temporarily write `schema_version=1`. Expected RED: builder completes but schema assertion receives `1` rather than `2`. Restore it.

- [ ] **Step 4: Implement schema and metadata**

Keep `Definitions` and `Examples` for audit only. Write `LearningSenses` from Task 4 and map all covered source senses through `LearningSenseSources`. Add metadata for OEWN fingerprint, COW XML SHA-256, ECDICT DB SHA-256, exact/broad/missing counts and exact ILI coverage.

- [ ] **Step 5: GREEN and commit**

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.LexiconBuilder.EnglishLexiconDatabaseBuilderTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.LexiconBuilder.EnglishLearningSenseBuilderTests
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
git commit -m "词库：生成英中运行时词库"
```

---

### Task 6: Return Chinese-only runtime DTOs

**Files:**
- Modify: both Lexicon PO/DataAccessor files listed above
- Modify: `Dictation.Services/Lexicon/LexiconLookupResultDto.cs`
- Modify: `Dictation.Services/Lexicon/LexiconLookupService.cs`
- Modify: `Dictation.Tests/Infrastructure/LexiconTestDatabases.cs`
- Modify: Japanese/English DataAccessor and Service tests

- [ ] **Step 1: Upgrade minimal runtime fixtures to schema v2**

Japanese fixture stores Chinese in `ChineseMeanings` and English only in `SourceGlosses`. English fixture stores English in `Definitions` and Chinese only in `LearningMeanings`. Include one exact, one broad and one missing result.

- [ ] **Step 2: Write runtime behavior tests**

Assert Japanese `食べる` returns `吃；吃饭`-style Chinese and never `to eat`. Assert English `went` returns `go` learning results with Chinese meanings and complete forms, never the definition. Verify source/precision enums and `HasChineseMeaning`.

For English import groups, anchor `LearningSenseId` through `LearningSenseSources`, follow only direct undirected `Derivations`, map neighbor source senses back to learning senses, deduplicate and exclude phrases. A broad anchor may map multiple OEWN source senses but still appears once.

If the current untracked Presentation tests inspect the shared fixture, update only their sample meaning text/expectation from English to Chinese at this step. Keep those edits unstaged during this plan; their page-level rewrite and commit belong to `2026-08-31-single-window-navigation.md`.

- [ ] **Step 3: Establish behavioral RED**

After compile and hash freeze, temporarily point Japanese meaning SQL at `SourceGlosses WHERE Language='eng'`. Expected RED: the Chinese-only assertion receives `to eat`. Restore the SQL.

- [ ] **Step 4: Implement DataAccess and Service mapping**

Japanese queries `ChineseMeanings`. English searches `Forms -> source Lexemes/Senses -> LearningSenseSources -> LearningSenses`, then reads `LearningMeanings` and unions mapped source-lexeme forms in stable order. DataAccess returns strings; Service alone parses them into enums and Chinese labels.

- [ ] **Step 5: GREEN and commit**

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.DataAccess.JapaneseLexiconDataAccessorTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.DataAccess.EnglishLexiconDataAccessorTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Services.LexiconLookupServiceTests
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
git commit -m "词库：运行时只返回中文释义"
```

---

### Task 7: Make Inbox meanings editable and gate Learning/Review

**Files:**
- Modify: `Dictation.DataAccess/Runtime/SchemaMigrationRunner.cs`
- Modify: all Importing PO/Accessor/DTO/Service files listed above
- Modify: `Dictation.Tests/DataAccess/ImportInboxDataAccessorTests.cs`
- Modify: `Dictation.Tests/Services/ImportInboxServiceTests.cs`
- Modify: `Dictation.Tests/DataAccess/SchemaMigrationRunnerTests.cs`

**New DTOs:**

```csharp
public sealed record ImportSelectionDto(
    string SourceText,
    string SourceSenseId,
    IReadOnlyList<string>? ChineseMeaningOverride = null);

public sealed record UpdateImportMeaningsRequestDto(
    string WordGroupId,
    string SourceSenseId,
    IReadOnlyList<string> Meanings);
```

`ImportInboxService` adds:

```csharp
public Task UpdateMeaningsAsync(
    UpdateImportMeaningsRequestDto request,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 1: Add migration 3 and persistence tests**

Migration 3 adds `MeaningLanguage`, `MeaningSource` and `MeaningPrecision` to `WordGroupMembers`. Existing rows use `und/Legacy/Missing` so old English snapshots are not mistaken for Chinese; new snapshots use `zh-CN` and the runtime source/precision. Test backup, migration order and rollback.

- [ ] **Step 2: Test staging and editing**

Staging an empty-missing result is allowed and remains pending. A supplied override applies only to the selected anchor member, is NFC-normalized and stored as `zh-CN/User/UserProvided`. `UpdateMeaningsAsync` replaces one Inbox member’s meanings transactionally and rejects unknown groups, non-Inbox groups, blank lists and wrong members.

- [ ] **Step 3: Test decision gate and immutable-resource safety**

Learning and Review must reject any selected group containing a member without non-empty `zh-CN` meanings; Discard remains allowed. After user editing, both decisions succeed. Hash both immutable lexicon DBs before and after editing and assert they are unchanged.

- [ ] **Step 4: Establish behavioral RED**

After compile and frozen hashes, temporarily bypass the missing-meaning guard in `ImportInboxService.DecideAsync`. Expected RED: the test expects a business validation exception but the item moves to Learning or Review. Restore the bypass.

- [ ] **Step 5: Implement the minimum transaction path**

DataAccessor only performs CRUD/transaction checks. Service normalizes overrides, hides `MeaningLanguage!='zh-CN'` values from DTOs, performs the Learning/Review rule, and marks user-edited snapshots. Do not create a Repository or write to lexicon connections.

- [ ] **Step 6: GREEN and commit**

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.DataAccess.SchemaMigrationRunnerTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.DataAccess.ImportInboxDataAccessorTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Services.ImportInboxServiceTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
git commit -m "导入：校验并保存中文释义快照"
```

---

### Task 8: Add reproducible real-resource gates and correct documentation

**Files:**
- Implement: `scripts/Build-Lexicons.ps1`
- Modify: `docs/resources/ThirdPartyResources.md`
- Modify: `docs/requirements/RefactorSpecification.md`

- [ ] **Step 1: Implement a parameterized build script**

The script accepts explicit JMdict, Tomoshi DB, OEWN YAML, COW archive, ECDICT archive and output paths. The durable input layout is fixed as:

```text
D:\Source\Repos\Dictation\Resources\JMdict.gz
D:\Source\Repos\Dictation\Resources\english-wordnet\src\yaml\
D:\Source\Repos\Dictation\Resources\tomoshi-dict-open.db
D:\Source\Repos\Dictation\Resources\omw-cmn-2.0.tar.xz
D:\Source\Repos\Dictation\Resources\ecdict-sqlite-28.zip
```

It verifies the approved archive/raw SHA-256 values, extracts COW and ECDICT into a caller-provided temporary directory using Windows `tar`/`Expand-Archive`, builds into a separate caller-provided output directory, runs `PRAGMA integrity_check`, checks `schema_version=2` and prints row counts/coverage. It never writes under `CONFIG/DATA/LOGS/BACKUPS` and never downloads. If any durable input is missing, stop Task 8 and report that exact path rather than using `AppData\Local\Temp` as an implicit dependency.

- [ ] **Step 2: Run each real build twice**

Use durable user-provided paths, not the audit temp directory. Compare both database hashes and metadata. Validate:

```text
Japanese: 食べる, 行く, 掃除 have Chinese and approved forms
English: go, do, child, good have Chinese and approved forms
COW unique ILI = 42300
OEWN/COW ILI intersection = 38867
LearningMeanings containing only ASCII English definitions = 0
PRAGMA integrity_check = ok
```

- [ ] **Step 3: Correct docs and license inventory**

Remove statements that English gloss/definition is the user-visible canonical meaning. Record the three Chinese resources, actual output counts/hashes, schema v2, license/attribution paths and the fact that raw sources are excluded from release ZIP.

- [ ] **Step 4: Final verification and docs commit**

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet build Dictation.sln --configuration Debug --no-restore
rg --files -g "*.db" -g "!**/bin/**" -g "!**/obj/**"
git diff --check
git commit -m "文档：固定日中英中词库构建门禁"
```

Expected `rg` result: no source-controlled database files.

## Self-Review

- **Spec coverage:** exact Japanese sense alignment, exact English ILI alignment, one ECDICT fallback, missing-Chinese editing, Inbox-only user writes, immutable runtime DBs, representative word groups and real resource gates are all assigned to explicit tasks.
- **Placeholder scan:** every task names files, interfaces, schema, RED mutation, expected failure and GREEN commands; no implementation decision is left as TODO/TBD.
- **Type consistency:** generated DB strings become DataAccess PO fields; Service maps them to shared enums/DTOs; Import Inbox snapshots copy DTO values and never reference immutable DB rows.
- **Simplicity:** reuses existing SQLite, Dapper, JSON, XML, YAML and DI; adds no Repository, provider interface, online service, translation model, second ORM or heavy library.
- **Safety:** current uncommitted Presentation work is outside this plan; raw sources and generated databases remain outside Git; user data is only changed through schema migration and transactional DataAccessor methods.
