# 第三方资源与发布清单

## 1. 资源边界

下表的原始输入暂存于开发机 `D:\Source\Repos\Dictation\Resources`。这是用户工作区中的输入位置，不是应用运行时依赖路径，也不属于本隔离工作树；原始资源不直接提交到本仓库。发布根目录只包含经过确认的 `Resources/` 运行时子集。

资源分为三类：构建时输入（用于生成只读数据库）、运行时模型/词库（随发布 ZIP 提供）和许可证/归属文件。每次发布都要生成版本、来源、许可证、文件大小、SHA-256 的 manifest；发布前重新计算哈希，不能以文件名代替校验。

## 2. 已核验原始输入

| 资源 | 已核验版本或来源 | 大小与 SHA-256 | 许可证/归属 | 当前结论 |
| --- | --- | --- | --- | --- |
| `JMdict.gz` | 创建日期 2026-08-14 | 22,155,996 bytes；`9C1DE325ED8E3D690F4DD9F9E1E114FB800ABE5B23782C43F89D9A9E44F431CF` | EDRDG JMdict，CC BY-SA 4.0；发布物须保留 EDRDG/JMdict 归属、许可副本或有效链接，并满足对应再分发义务 | 构建时解析并清洗；运行时使用生成的只读日语词库 |
| `tomoshi-dict-open.db` | Tomoshi `v2026-08-12` 原始 SQLite | 570,077,184 bytes；`C935FFD8E030F496C251B38819BF63005729D2DBC98BA0AF8C55DE507A455290` | `zh_defs` 为 CC BY-SA 4.0；发布归属固定写入 `Resources/Attribution/Lexicons/Tomoshi.txt` 并附许可 | 只按 JMdict `entry_id + sense ordinal` 提取中文；原库不进入发布 ZIP |
| `english-wordnet` | `https://github.com/globalwordnet/english-wordnet.git`；commit `02ff9f3f5bc0a25592e7263ffdbc9bcb6564936b`；`2025-edition-160-g02ff9f3`；工作树 clean | 本次以 commit 固定，不把嵌套 Git 元数据作为发布输入 | Open English WordNet CC BY 4.0；同时保留底层 Princeton WordNet 许可证归属 | 构建时提取 synset、词性、义项和关系；具体运行表结构在词库切片固定 |
| `omw-cmn-2.0.tar.xz` | Chinese Open WordNet 2.0 | 1,705,740 bytes；`7D07AF60A6CED0CEDC4CA114D0B60A796D3F138DF0F3BE21F6322E53D004E91C` | 归档内 `omw-cmn/LICENSE`、`omw-cmn/citation.bib`；发布时复制到 `Resources/Attribution/Lexicons/ChineseOpenWordNet/` | 通过 ILI 提供精确中文；原始 XML 与归档不进入发布 ZIP |
| `ecdict-sqlite-28.zip` | ECDICT SQLite 1.0.28 | 216,765,132 bytes；`EA01F76A3B3351021CE47077E89234465CC9441C8793054495320D06C0C3F3F6` | 上游 MIT；归档仅含数据库，发布归属固定写入 `Resources/Attribution/Lexicons/ECDICT.txt` 并附 MIT 文本 | 只为未获精确中文的 lemma+POS 生成一个宽泛回退；原库不进入发布 ZIP |
| `Kokoro-82M` | `https://huggingface.co/hexgrad/Kokoro-82M`；commit `f3ff3571791e39611d31c381e3a41a3af07b4987`；工作树 clean | `kokoro-v1_0.pth`：327,212,226 bytes；`496DBA118D1A58F5F3DB2EFC88DBDC216E0483FC89FE6E47EE1F2C53F18AD1E4` | 模型卡标记 Apache-2.0；随发布保留模型卡与适用归属文本 | 仅作为原始模型输入和归属参考；纯 .NET 运行时不直接加载 `.pth`，发布使用下方 Sherpa-ONNX 转换资源子集 |
| `piper-plus-css10-ja-6lang` | `https://huggingface.co/ayousanz/piper-plus-css10-ja-6lang`；commit `bd0d812d4db9182ecdb907ef074becf9e230c17f`；工作树 clean | ONNX：39,652,717 bytes；`5EBC51DBF897238523F3DF0D6E0F6C93033BC5CDA3F8602A8379EBE2A4738C42`；`config.json`：`1328B76E67B7D785E57D0ECF1E76571F6E931B5B9313C88AFE9D0017049CB692` | 模型卡标记 CSS10 public domain；要求使用 Piper Plus PR #320 之后的运行时 | 供日语发音使用；运行时固定使用 `PiperPlus.Core` 0.4.0 与 OpenJTalk 词典 |
| `PP-OCRv5_server_rec_infer.tar` | PaddleOCR PP-OCRv5 server recognition inference 输入 | 84,869,120 bytes；`D99BE2FFD348943AB52876179168BE4FB5B14F5F0812F2AE4C76D89EC2EA750A` | PaddleOCR 项目 Apache-2.0；随发布保留项目许可和归属 | 已确认包含 `inference.yml`、`inference.pdiparams`、`inference.json`；Windows CPU 运行时、模型裁剪和推理参数留给 OCR 切片验证 |

## 2.1 日语 TTS 运行时固定依赖

`Dictation.TtsHost` 只使用本地 CPU 推理，不调用 `DictionaryManager`、`HttpClient` 或任何运行时下载。四个 NuGet 依赖固定如下，发布时须随包保留对应许可证和归属文件：

| NuGet 包 | 版本 | 许可证 |
| --- | --- | --- |
| `PiperPlus.Core` | `0.4.0` | MIT |
| `Microsoft.ML.OnnxRuntime` | `1.24.3` | MIT |
| `DotNetG2P` | `1.8.0` | Apache-2.0 |
| `DotNetG2P.MeCab` | `1.8.0` | Apache-2.0 |

OpenJTalk 词典下载地址为 [`open_jtalk_dic_utf_8-1.11.tar.gz`](https://github.com/r9y9/open_jtalk/releases/download/v1.11.1/open_jtalk_dic_utf_8-1.11.tar.gz)，固定 SHA-256 为 `FE6BA0E43542CEF98339ABDFFD903E062008EA170B04E7E2A35DA805902F382A`。构建或发布前解压并核验后，运行时布局必须为：

```text
Resources/Tts/Japanese/PiperPlus/
├── css10-ja-6lang-fp16.onnx
├── config.json
└── OpenJTalk/open_jtalk_dic_utf_8-1.11/
    ├── sys.dic
    ├── matrix.bin
    ├── char.bin
    └── unk.dic
```

OpenJTalk 词典的 COPYING 适用 Modified BSD 风格条款，并包含 NAIST、UniDic、Open JTalk/HTS Working Group 等版权与免责声明。源码和二进制再分发都必须保留适用的版权、条件和免责声明；发布包须随附对应 notice，不能仅保留下载链接。

DotNetG2P.MeCab 依赖的 MeCab/NMeCab 第三方 notices 也必须随发布包保留，包括包内 NOTICE、THIRD-PARTY-NOTICES.md 及适用许可证，并在资源归属目录中登记。

模型、配置和词典在首次日语合成请求时才加载；TTS Host 启动和 Ping 不访问这些大文件。资源缺失时返回中文错误，不能通过联网补齐。

## 2.2 英语 Kokoro 运行时固定依赖

英语使用 `org.k2fsa.sherpa.onnx` 1.13.5（Apache-2.0）和 `Microsoft.ML.OnnxRuntime` 1.27.1（MIT），仅发布 `win-x64` CPU 原生运行时。官方 Kokoro v1.0 Sherpa-ONNX 归档地址为 [`kokoro-multi-lang-v1_0.tar.bz2`](https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-multi-lang-v1_0.tar.bz2)。上游于 2026-09-08 更新了同名 Release 资产；2026-09-28 下载的归档为 349,906,910 bytes，SHA-256 为 `C5F7E2D2CAF082BC1D20FB70334A61D99D20B484500AAD32E7CF84C128EA3298`，与 GitHub Release API 的大小及 `digest` 一致。发布只抽取英语运行时子集，并在发布 manifest 中再次核验：

| 文件 | 大小 | SHA-256 |
| --- | ---: | --- |
| `model.onnx` | 325,560,556 bytes | `B40F62B166AC8164B0627EF48A0B358EDA0985E272FB03EF5252E7206305DA11` |
| `voices.bin` | 28,200,960 bytes | `1C5A5B983D3D50D8586D437A51F3FAA2DA7919CE76A013C081E65671A3447C29` |
| `tokens.txt` | 687 bytes | `6EBB6BB288F20F3AE8D004D3C2CA27697DA27C037D75E81A60E2A6A663F95425` |
| `lexicon-gb-en.txt` | 6,366,635 bytes | `C4CBB37316F62210DFF52718A7AFCAAE24F50C032CC75AB47AE67B831D1049E7` |

运行时固定布局为：

```text
Resources/Tts/English/Kokoro/
├── model.onnx
├── voices.bin
├── tokens.txt
├── lexicon-gb-en.txt
└── espeak-ng-data/
```

默认英式女声为 `bf_emma`，Sherpa speaker id 为 `21`；合成使用 CPU、2 个线程和速度 `1.0`。构造、DI、TTS Host 启动和 Ping 都不加载模型，首次英语合成才检查并懒加载上述资源；运行时不联网补齐。生成的采样只在内存中转换为 RIFF/WAV 并通过命名管道返回，严禁调用 `SaveToWaveFile`、写入音频文件或创建缓存。英语发布子集只省略未使用的中文 `dict/`、中文 lexicon/FST 和 `lexicon-us-en.txt`；`voices.bin` 与 `espeak-ng-data/` 按官方包完整保留，不声称对其中内容做了裁剪。Apache-2.0 许可证及归属说明须随发布包保留。

## 2.3 日中词库真实构建验收

2026-09-04 使用固定的 `JMdict.gz` 与 Tomoshi 数据库连续构建两次。产物 metadata 完全一致：`schema_version=2`、`generator_version=1.0.0.0`、`source_entries=218431`、`entries/senses=253103`、`source_glosses=442059`、`chinese_meanings=251373`、`missing_chinese_entries=1730`、`forms=911050`。

两次 SQLite 均为 300,396,544 bytes，SHA-256 均为 `EE4411EA2766AFCD3261BFAEC7599DFD6F42AB184C3391C23496D1504999C734`，`PRAGMA integrity_check=ok`。`食べる`（`jmdict:1358280:1`）、`行く`（`jmdict:1578850:1`）和 `掃除`（`jmdict:1399790:1`）都有中文；前两者覆盖普通形、礼貌形、て形、た形，`掃除` 与 `掃除する/します/して/した` 保持同组。

原始英文 gloss 只留在 `SourceGlosses` 供审计，运行时仅查询 `ChineseMeanings`；缺少中文时返回明确缺失状态，不回退显示英文。

## 2.4 英中词库真实构建验收

2026-09-04 使用 clean commit `02ff9f3f5bc0a25592e7263ffdbc9bcb6564936b` 的 OEWN YAML、COW 2.0 与 ECDICT 1.0.28 连续构建两次。当前 YAML 工作树字节指纹为 `f6fa481a5b29d9f33e2178dbee74d0d1664eb513782ca483be88e925942bca77`；解压后的 COW XML 与 ECDICT SQLite 哈希分别为 `c02b74ef813380f2a1de089d6d5aad6c58b50bb06a3ed3c4a8d963160ac40909`、`2b5b40c2bdba04da0a51c8672e090f166987d5d895f32eb3fbfc5a516455fc75`。

产物 metadata 完全一致：`schema_version=2`、`lexemes=135994`、`senses=185149`、`synsets=107525`、`definitions=107530`、`examples=50023`、`forms=221746`、`derivations=73514`、`learning_senses=161298`，其中精确 70,969、宽泛 46,630、缺失 43,699。COW 有 42,300 个唯一 ILI，OEWN 有 104,314 个，交集 38,867（37.26%）；`LearningMeanings` 中纯 ASCII 英文释义为 0。

两次 SQLite 均为 154,562,560 bytes，SHA-256 均为 `151F960A23FFE0008E445F893892321131466921E4A8FBB68A32972135A8302B`，`PRAGMA integrity_check=ok`。`go/goes/went/gone/going`、`do/does/did/done/doing`、`child/children`、`good/better/best` 的词形和中文释义均通过门禁。OEWN `Definitions/Examples` 只供构建审计，运行时只读 `LearningSenses/LearningMeanings`。

## 2.5 运行时词库布局与查词行为

[`scripts/Build-Lexicons.ps1`](../../scripts/Build-Lexicons.ps1) 接收五项显式输入、一个显式解压目录和一个显式输出目录；它复用 .NET SDK、Windows `tar`、PowerShell `Expand-Archive` 与 `sqlite3`，不下载资源、不使用隐式临时输入，并拒绝向 `CONFIG/`、`DATA/`、`LOGS/`、`BACKUPS/` 或 `CACHE/` 写入。脚本固定校验输入哈希、schema v2、完整性、覆盖率、代表词和中文门禁。两次复现输出位于源码仓库外，生成数据库与解压副本不提交。

发布根目录固定提供由构建器生成的只读词库，运行时不在线下载或修改：

```text
Resources/Lexicons/
├── JapaneseLexicon.db
└── EnglishLexicon.db
```

词库缺失或不可读时，查词窗口显示中文错误，主程序仍可启动；真实数据库不提交到仓库。

## 3. 构建加工与发布取舍

`Dictation.LexiconBuilder` 在构建阶段生成版本固定的只读日中、英中 SQLite：日语以 JMdict 定义结构和词形，以 Tomoshi 精确补入中文；英语以 OEWN 定义结构和词形，以 COW ILI 提供精确中文，再以 ECDICT 为未覆盖的 lemma+POS 提供一个宽泛中文。JMdict 原始 gloss、OEWN definition/example 只作为审计数据，绝不成为 UI 释义或缺失中文时的回退。系统只支持源语言正向查询，不支持中文反向查询。

词库生成输出来源 commit/日期、生成器版本、输入哈希、中文覆盖与缺失数量。上述加工结果不是用户数据；用户词条、集合、进度、错题和历史留在根目录 `DATA/`，更新 ZIP 不得覆盖。

英语 TTS 已固定使用上述 Kokoro 英式女声 `bf_emma`（speaker id `21`）。日语 TTS 已固定使用上述 Piper Plus 资源布局和 CPU 运行时。OCR 切片需要验证 Windows CPU 推理依赖、模型是否裁剪以及运行时文件清单；本资源表不预先承诺识别效果。

发布只携带运行所需子集和许可证/归属文件，不携带完整原始仓库、嵌套 `.git`、样例数据、训练文件或未使用的大模型。构建缓存可位于工作区外，但不能成为用户运行时的隐式依赖。每个 ZIP 必须带同名 `.sha256` 和资源 manifest；客户端更新先校验 ZIP 哈希，再替换程序与不可变 Resources。

## 4. 更新与合规检查

- 运行时不在线下载或更新词库；全量 ZIP 发布时可替换内置只读词库，以满足 JMdict 的许可/维护要求。
- 发布前复制或随包提供 EDRDG JMdict、Open English WordNet、Princeton WordNet、Kokoro、Piper Plus/CSS10、PaddleOCR 的适用许可证和归属说明；不得把“模型卡标记”扩大解释为所有运行时依赖的许可证。
- 资源版本、来源、commit/日期、大小、SHA-256、处理结果和许可证路径必须可从 manifest 追溯。缺少核验信息的新增资源不能进入发布子集。
- 发布校验只检查不可变 `Resources/`；根目录 `CONFIG/`、`DATA/`、`LOGS/`、`BACKUPS/` 和必要的 `CACHE/` 属于用户运行数据，不打包成会覆盖它们的文件。
