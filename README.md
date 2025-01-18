# 项目名称

Dictation Application

## 项目简介

Dictation Application 是一个用于手写单词听写应用程序。用户可以导入单词列表，进行听写练习，并在听写过程中绘制单词。应用程序会记录用户的听写结果，并提供结果导出功能。

## 功能特性

- 导入单词列表（✔️已完成）
- 添加和删除单词（✔️已完成）
- 清空单词列表（✔️已完成）
- 根据单词意义进行听写（✔️已完成）
- 根据单词发声进行听写（✔️已完成）
- 手写单词（✔️已完成）
- 检查听写结果（✔️已完成）
- 统计并显示听写结果（✔️已完成）
- 导出错误单词列表（✔️已完成）
- OCR自动检查听写结果（❌待完成）
- 自动适配对应语言TTS（❌待完成）

### 已知问题
- 目前并未适配数位板压力传感支持，使用Windows Ink数位板时，小幅度笔触无法被识别，需要在数位板驱动上关闭Windows Ink功能后可正常使用。

## 安装与运行

### 开发环境要求

- .NET 8
- Visual Studio 2022
- edge浏览器
- Windows内置TTS

### 运行环境要求

- Windows 7或以上
- 确认已安装 .NET 桌面运行时 8.0.12(https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)
- 确认已安装 Windows内置TTS或Edge浏览器

### 安装步骤
下载编译好的二进制包，解压到电脑后，运行`Dictation.Presentation.exe`即可


## 使用说明
### 主窗口 (MainWindow)

1. **导入单词**：点击“导入单词”按钮，选择一个文本文件，文件中的每一行代表一个单词。
2. **添加单词**：在文本框中输入单词，点击“添加单词”按钮。
3. **删除单词**：在单词列表中点击删除按钮。
4. **清空单词**：点击“清空单词”按钮，清空所有单词。
5. **开始听写**：点击“开始听写”按钮，进入听写窗口。

#### 模式说明
读意： TTS播放单词意义的语音，如`你好_Hello`，单词为`Hello`意义为`你好`，则会播放`你好`


读音：TTS播放单词发音的语音，如`世界_World`，单词为`World`意义为`世界`，则会播放`World`
  
#### TTS说明
系统 TTS：声音生硬机器感十足，响应速度快。使用前请确保你的Windows已经安装该语言的TTS语音包


Edge TTS：声音生动像真人发声，响应速度慢。使用前请确保你已安装最新Edge浏览器

#### 导入说明
使用txt文档，每行一个单词，单词使用分隔符分割单词和意义，使用`#`注释，例：
```
你好_Hello
世界_World
```
#### 分隔符
默认分隔符为`_`，可在文档第一行使用`#separator:`自定义分隔符，例：
```
#separator:|
你好|Hello
世界|World
```

### 听写窗口 (DictationWindow)

1. **播放当前单词**：点击“播放”按钮，播放当前单词的发音/意义。
2. **手写单词**：在画板上绘制当前单词（建议使用数位板）。
3. **清空画板**：点击“清空”按钮，清空画板内容。
4. **下一个单词**：点击“下一个”按钮，保存当前绘制并播放下一个单词。
5. **结束听写**：听写结束后，自动关闭窗口并显示检查窗口。


### 检查窗口 (ReviewWindow)
1. **呈现听写的单词和手写图像**：列表中每行呈现听写单词和图像
2. **重听音频**：点击“播放”按钮，播放当前单词的发音/意义。
3. **判断是否正确**：点击✔️或❌判定是否正确
4. **检查结束**：当全部单词判定后，为检查结束，检查结束后会自动关闭窗口，显示结果窗口

### 结果窗口 (ResultWindow)

1. **查看结果**：结果窗口显示听写的正确率和错误单词列表。
2. **导出错误单词**：点击“导出”按钮，保存错误单词列表到文本文件。


## 代码结构

- `Dictation.Presentation`：包含应用程序的用户界面和交互逻辑。
- `Dictation.Core`：包含核心业务逻辑和数据模型。

## 主要类和接口

### DictationManager
听写管理，主要负责播放单词
- `PlayNextWord()`：播放下个单词。
- `PlayCurrentWord()`：播放当前单词。
- `Stop()`：停止听写。

### ITTSPlayer
TTS播放器接口，主要负责抽象TTS，所有使用TTS的地方只需要Play即可
- `Play()`：播放单词。
  
### ILanguageSettableTTSPlayer
包含TTS所有接口的情况下添加了设置语言方法，用于TTS的初始化，不公开给包外成员
- `SetLanguage()` 设置该TTS使用语言

### SystemTTSPlayer
- `Play()`：调用Window内置TTS播放单词

### EdgeTTSPlayer
- `Play()`：调用Edge浏览器的TTS播放单词
  
### TTSProxy

- `Play()`：播放根据模式代理并播放单词
  
### WordCollection

- `AddWord()`：添加单词
- `RemoveWord()`：删除单词
- `Import()`：导入单词
- `GetWords()`：获取单词

### MainWindow

- `InitializeLanuageOptions()`：初始化语言选项。
- `InitializePlayMod()`：初始化播放模式。
- `UpdateWordGrid()`：更新单词列表显示。
- `btnImportWords_Click()`：导入单词文件。
- `btnAddWord_Click()`：添加单词。
- `btnClearWords_Click()`：清空单词。
- `btnStartDictation_Click()`：开始听写。

### DictationWindow

- `InitializeDrawingBoard()`：初始化画板。
- `btnNext_Click()`：播放下一个单词。
- `SaveBitmap()`：保存当前绘制。
- `btnPlay_Click()`：播放当前单词。
- `drawingPanel_MouseDown()`：鼠标按下事件。
- `drawingPanel_MouseUp()`：鼠标抬起事件。
- `drawingPanel_MouseMove()`：鼠标移动事件。

### ResultWindow

- `InitializeResultGridView()`：初始化结果列表。
- `btnExport_Click()`：导出错误单词。

### 扩展性

#### 扩展新的 TTS

`ITTSPlayer`、`ILanguageSettableTTSPlayer` 接口定义了播放单词的基本方法。要扩展新的 TTS 播放器，需要实现 `ITTSPlayer`或`ILanguageSettableTTSPlayer` 接口，并在 `DictationCoreServiceCollectionExtensions` 中注册新的 TTS 播放器。

## 贡献

欢迎提交问题和贡献代码。请确保在提交之前阅读并遵循项目的贡献指南。

## 许可证

此项目使用 MIT 许可证。

---

如有任何问题或建议，请在Issue提出你的建议或问题。感谢您的使用和支持！

