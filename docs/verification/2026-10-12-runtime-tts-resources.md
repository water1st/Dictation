# 真实资源与预览TTS验收

## 范围

在主目录 `D:\Source\Repos\Dictation` 的 `codex/dictation-refactor`、源码基线ca5e595执行。只准备已批准的运行时资源子集与隔离诊断，不改产品代码、引包、安装工具、修改用户Resources、重做词库构建或全量1910项。统计打卡规则尚待用户回答；这项独立验收不擅自确定该规则。

当前预览入口：`artifacts/Dictation-preview/Dictation.Presentation.exe`。原本本轮mcp-task1产物只改目录名，没有再建工作树或复制另一份程序。旧preview-56ec99f及其用户数据保持不变。

## 资源准备与校验

复用Resources中已下载的Kokoro归档、OpenJTalk归档、Piper原始ONNX及config.json，四项SHA全部匹配ThirdPartyResources固定清单。只解压模型、英式词典、音色、完整eSpeak数据与实际使用的OpenJTalk四个词典文件；不复制原始Git仓库／训练权重／Piper优化缓存／中文及美式词典。Kokoro四个主要运行文件再与固定SHA核对。

这里省略的中文／美式词典是模型归档的顶层lexicon文件；eSpeak-ng-data按原包完整保留，不声称对其内部其它语言数据做裁剪。

TTS子集366文件、524,919,796字节。随预览加入模型卡、OpenJTalk COPYING、DotNetG2P／MeCab LICENSE、NOTICE、THIRD-PARTY-NOTICES及OnnxRuntime许可，共10份归属文件。预览Resources/Tts/manifest.json记录来源版本、逐文件大小和SHA、bf_emma／speaker21／CPU／2线程／speed1.0；其SHA为6BD8A5CCBE2D525F181EC17D3B8A22C47556291893D238336882FF50FAF2E952。

清单明确releaseReady=false：PiperPlus.Core、Sherpa-ONNX和eSpeak的完整再分发许可／归属／源码信息仍需发布门槛核验，不宣称完整合规发布完成。模型卡许可不是全部依赖许可。

两个正式中文词库从已验收预览复制，不重新生成：JapaneseLexicon.db为EE4411EA2766AFCD3261BFAEC7599DFD6F42AB184C3391C23496D1504999C734；EnglishLexicon.db为151F960A23FFE0008E445F893892321131466921E4A8FBB68A32972135A8302B。复制前核对源SHA，复制后在新预览经原Core查询／整组解析接口实际读取：

- 食べます→食べる，中文“吃；食用”，对应整组可以解析。
- went→go，中文“离开；移动；行动；行进；运行”，对应整组可以解析。

只读查询没有暂存导入或用户决定，不启动迁移。旧预览UserData.db仍196,608字节、修改时间2026-09-29 17:41:03。

## 真实合成结果

隔离诊断容器复用正式AddLocalTtsHost和TtsService，实际启动自包含Dictation.TtsHost.exe；不是模型替身或直接返回预制WAV。进程14556窗口句柄0，经过同一命名管道返回内存音频；NAudio严格读取WAV，检查时长、全部采样有限与非零能量。

| 真实请求 | 内存字节 | 采样率 | 时长 | RMS | 冷合成耗时 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 日语：たべます | 13,274 | 22,050 | 0.300秒 | 0.106904 | 1.35秒 |
| 英语：The child went to school. | 71,748 | 24,000 | 1.494秒 | 0.084053 | 3.05秒 |

实际输出：`Passed=true RealJapanese=true RealBritishEnglish=true MemoryOnly=true HiddenProcess=true ExitCode=0`。停止Host后子进程正常退出；没有持久化音频。合成后再次核对366项资源SHA，全部一致，文件数量不变，未生成模型缓存。

限制：这些检查证明真实模型可加载、生成／传输音频、隐藏进程及退出行为，不证明发音内容完全正确或自然度已达到用户期望；仍需实际试听，尤其短日语样本，不能把RMS非零当成发音质量验收。

初次诊断使用旧NAudio Read接口导致工具编译失败，模型未执行；修正工具为当前Span接口。随后两门语言生成成功，但工具在外部进程退出后未预先保留句柄，查询ExitCode报错；只修工具句柄保留后取得上述完整结果。两种工具故障均不计产品RED、业务缺陷或通过证据。

## 交付边界

产品源码未修改，已有1910/1910回归不再运行。尚未更新dotnet-desktop.yml、实现Updater、统计打卡或最后OCR，也未推送／合并／发布Release。本记录是完整GOAL中的资源与真实模型进展，不是整个目标完成声明。

## 后续许可补查

同日按已安装NuGet的固定提交补齐PiperPlus.Core MIT、Sherpa Apache-2.0以及eSpeak GPLv3及五项子组件许可原文，另附来源说明；归属文件从10增至19，模型文件366项未变。新清单SHA为97AE38C3A17C75D651E3F49509980BFC8050201171B5D6E49664298D24C1B099；上文6BD8A5...为首次真实合成验收时的历史清单，不覆盖历史证据。未重复合成、构建或全量测试。

实际查明的依赖链是Sherpa源码3dc7c569...中的cmake/espeak-ng-for-piper.cmake→eSpeak源码ed530aa...，后者README明确GPLv3或后续版本。原生二进制／模型数据的完整对应源码、编译配置与本项目许可适用关系尚未确定，releaseReady继续false；没有给用户源代码换许可证。固定来源链接见ThirdPartyResources第6节，不能把这些原文副本称为完整发布法律审查结论。
