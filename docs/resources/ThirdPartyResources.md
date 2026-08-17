# 第三方资源与发布清单

## 1. 资源边界

下表的原始输入暂存于开发机 `D:\Source\Repos\Dictation\Resources`。这是用户工作区中的输入位置，不是应用运行时依赖路径，也不属于本隔离工作树；原始资源不直接提交到本仓库。发布根目录只包含经过确认的 `Resources/` 运行时子集。

资源分为三类：构建时输入（用于生成只读数据库）、运行时模型/词库（随发布 ZIP 提供）和许可证/归属文件。每次发布都要生成版本、来源、许可证、文件大小、SHA-256 的 manifest；发布前重新计算哈希，不能以文件名代替校验。

## 2. 已核验原始输入

| 资源 | 已核验版本或来源 | 大小与 SHA-256 | 许可证/归属 | 当前结论 |
| --- | --- | --- | --- | --- |
| `JMdict.gz` | 创建日期 2026-08-14 | 22,155,996 bytes；`9C1DE325ED8E3D690F4DD9F9E1E114FB800ABE5B23782C43F89D9A9E44F431CF` | EDRDG JMdict，CC BY-SA 4.0；发布物须保留 EDRDG/JMdict 归属、许可副本或有效链接，并满足对应再分发义务 | 构建时解析并清洗；运行时使用生成的只读日语词库 |
| `english-wordnet` | `https://github.com/globalwordnet/english-wordnet.git`；commit `02ff9f3f5bc0a25592e7263ffdbc9bcb6564936b`；`2025-edition-160-g02ff9f3`；工作树 clean | 本次以 commit 固定，不把嵌套 Git 元数据作为发布输入 | Open English WordNet CC BY 4.0；同时保留底层 Princeton WordNet 许可证归属 | 构建时提取 synset、词性、义项和关系；具体运行表结构在词库切片固定 |
| `Kokoro-82M` | `https://huggingface.co/hexgrad/Kokoro-82M`；commit `f3ff3571791e39611d31c381e3a41a3af07b4987`；工作树 clean | `kokoro-v1_0.pth`：327,212,226 bytes；`496DBA118D1A58F5F3DB2EFC88DBDC216E0483FC89FE6E47EE1F2C53F18AD1E4` | 模型卡标记 Apache-2.0；随发布保留模型卡与适用归属文本 | 供英语英式发音使用；现有 British voices 为 `bf_alice`、`bf_emma`、`bf_isabella`、`bf_lily`、`bm_daniel`、`bm_fable`、`bm_george`、`bm_lewis`，默认 speaker 尚未选择 |
| `piper-plus-css10-ja-6lang` | `https://huggingface.co/ayousanz/piper-plus-css10-ja-6lang`；commit `bd0d812d4db9182ecdb907ef074becf9e230c17f`；工作树 clean | ONNX：39,652,717 bytes；`5EBC51DBF897238523F3DF0D6E0F6C93033BC5CDA3F8602A8379EBE2A4738C42`；`config.json`：`1328B76E67B7D785E57D0ECF1E76571F6E931B5B9313C88AFE9D0017049CB692` | 模型卡标记 CSS10 public domain；要求使用 Piper Plus PR #320 之后的运行时 | 供日语发音使用；具体 Piper Plus 运行时、裁剪文件和启动参数留给 TTS 切片验证 |
| `PP-OCRv5_server_rec_infer.tar` | PaddleOCR PP-OCRv5 server recognition inference 输入 | 84,869,120 bytes；`D99BE2FFD348943AB52876179168BE4FB5B14F5F0812F2AE4C76D89EC2EA750A` | PaddleOCR 项目 Apache-2.0；随发布保留项目许可和归属 | 已确认包含 `inference.yml`、`inference.pdiparams`、`inference.json`；Windows CPU 运行时、模型裁剪和推理参数留给 OCR 切片验证 |

## 3. 构建加工与发布取舍

`Dictation.LexiconBuilder` 在构建阶段解析 JMdict 和 English WordNet，生成版本固定的只读 SQLite 词库：清洗重复项，按义项分组，关联日语普通/礼貌/`て`/`た` 等形式，标记常用汉字/罕见汉字/无汉字，关联英语规则、例外和直接派生的词性组，并生成可编辑中文释义及来源元数据。上述加工结果不是用户数据；用户的词条、集合、进度、错题和历史留在根目录 `DATA/`，更新 ZIP 不得覆盖。

词库生成必须输出来源 commit/日期、生成器版本、许可证文件名和输入哈希。中文翻译模型尚未选定，不得在本清单或运行时宣称包含某个翻译模型；当前只要求构建阶段产出可编辑中文释义。

TTS 切片需要从 Kokoro 的 British voice 中选择并记录一个默认 speaker，同时确定 Piper Plus 的可分发运行子集；在此之前不把任一 speaker 或运行库称为最终方案。OCR 切片需要验证 Windows CPU 推理依赖、模型是否裁剪以及运行时文件清单；本资源表不预先承诺识别效果。

发布只携带运行所需子集和许可证/归属文件，不携带完整原始仓库、嵌套 `.git`、样例数据、训练文件或未使用的大模型。构建缓存可位于工作区外，但不能成为用户运行时的隐式依赖。每个 ZIP 必须带同名 `.sha256` 和资源 manifest；客户端更新先校验 ZIP 哈希，再替换程序与不可变 Resources。

## 4. 更新与合规检查

- 运行时不在线下载或更新词库；全量 ZIP 发布时可替换内置只读词库，以满足 JMdict 的许可/维护要求。
- 发布前复制或随包提供 EDRDG JMdict、Open English WordNet、Princeton WordNet、Kokoro、Piper Plus/CSS10、PaddleOCR 的适用许可证和归属说明；不得把“模型卡标记”扩大解释为所有运行时依赖的许可证。
- 资源版本、来源、commit/日期、大小、SHA-256、处理结果和许可证路径必须可从 manifest 追溯。缺少核验信息的新增资源不能进入发布子集。
- 发布校验只检查不可变 `Resources/`；根目录 `CONFIG/`、`DATA/`、`LOGS/`、`BACKUPS/` 和必要的 `CACHE/` 属于用户运行数据，不打包成会覆盖它们的文件。
