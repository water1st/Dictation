# TTS运行时归属与来源

此文件记录2026-10-12开发预览的来源链，不是完整发布合规认证。

## PiperPlus.Core 0.4.0

NuGet包元数据指向ayutaz/piper-plus提交782edf41248a996551163b0acb3949d5c32258e5。原始MIT许可已附PiperPlus-Core-LICENSE.txt，保留Michael Hansen（2022）和ayutaz（2025）版权说明。

源码与许可：https://github.com/ayutaz/piper-plus/blob/782edf41248a996551163b0acb3949d5c32258e5/LICENSE.md

## Sherpa-ONNX 1.13.5

NuGet托管及win-x64运行时元数据均指向k2-fsa/sherpa-onnx提交3dc7c569f31ca2cd4a20ed6f7db780327e6714c5，顶层许可为Apache-2.0，已附Sherpa-ONNX-LICENSE.txt。托管包版权元数据为Copyright 2019-2023 Xiaomi Corporation；这不替代其它源文件各自的版权归属。

源码与许可：https://github.com/k2-fsa/sherpa-onnx/blob/3dc7c569f31ca2cd4a20ed6f7db780327e6714c5/LICENSE

## eSpeak-NG依赖边界

上述Sherpa源码中的cmake/espeak-ng-for-piper.cmake固定拉取csukuangfj/espeak-ng提交ed530aa113046142eb5115cf2fc9157854d0ffe1，归档SHA-256为e4e262cbe34f7fe21f91f1ba3397f2728e1f30eafbae7853f2b753a9ed13f0dd。其README明确说明GPL version 3 or later，而非Apache-2.0。

构建定义：https://github.com/k2-fsa/sherpa-onnx/blob/3dc7c569f31ca2cd4a20ed6f7db780327e6714c5/cmake/espeak-ng-for-piper.cmake

依赖源码：https://github.com/csukuangfj/espeak-ng/tree/ed530aa113046142eb5115cf2fc9157854d0ffe1

已附该提交的COPYING、COPYING.APACHE、COPYING.BSD2、COPYING.UCD及src/ucd-tools下的两份COPYING副本。子组件许可不表示整个eSpeak改为宽松许可。

当前仅确认源码版本声明和许可文本：实际预编译二进制与Kokoro归档中eSpeak数据的完整对应源码、编译配置、再分发条件及与本项目的许可关系尚未完成核验。不能因为NuGet顶层标注Apache或模型权重标注Apache就忽略GPL依赖，也不能只附许可文件便宣称已满足全部义务。未擅自为Dictation源代码选定或更改许可证。

## 其他现有归属

模型权重许可依据随包Piper-CSS10-model-card.md、Kokoro-model-card.md；OpenJTalk以OpenJTalk-COPYING.txt为准；DotNetG2P及MeCab的LICENSE/NOTICE/THIRD-PARTY-NOTICES已分别附目录；OnnxRuntime许可见OnnxRuntime-LICENSE.txt。

保留releaseReady=false。正式Release前还需完整原生依赖归属、数据对应来源及发布许可决策，不能将此预览当作已通过完整发布审查的安装包。
