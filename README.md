# 项目名称

Dictation Application

## 项目简介

Dictation Application 是一个用于单词听写和绘图的应用程序。用户可以导入单词列表，进行听写练习，并在听写过程中绘制单词。应用程序会记录用户的听写结果，并提供结果导出功能。

## 功能特性

- 导入单词列表
- 添加和删除单词
- 清空单词列表
- 听写练习
- 绘制单词
- 显示听写结果
- 导出错误单词列表

## 安装与运行

### 环境要求

- .NET 8
- Visual Studio 2022

### 安装步骤

1. 克隆项目代码到本地：
    
        
2. 打开 Visual Studio 2022，加载解决方案文件 `Dictation.sln`。

3. 选择 `Dictation.Presentation` 项目作为启动项目。

4. 运行项目。

## 使用说明

### 主窗口 (MainWindow)

1. **导入单词**：点击“导入单词”按钮，选择一个文本文件，文件中的每一行代表一个单词。
2. **添加单词**：在文本框中输入单词，点击“添加单词”按钮。
3. **删除单词**：在单词列表中点击删除按钮。
4. **清空单词**：点击“清空单词”按钮，清空所有单词。
5. **开始听写**：点击“开始听写”按钮，进入听写窗口。

### 听写窗口 (DictationWindow)

1. **播放当前单词**：点击“播放”按钮，播放当前单词的发音。
2. **绘制单词**：在画板上绘制当前单词。
3. **清空画板**：点击“清空”按钮，清空画板内容。
4. **下一个单词**：点击“下一个”按钮，保存当前绘制并播放下一个单词。
5. **结束听写**：听写结束后，自动关闭窗口并显示结果窗口。

### 结果窗口 (ResultWindow)

1. **查看结果**：结果窗口显示听写的正确率和错误单词列表。
2. **导出错误单词**：点击“导出”按钮，保存错误单词列表到文本文件。

## 代码结构

- `Dictation.Presentation`：包含应用程序的用户界面和交互逻辑。
- `Dictation.Core`：包含核心业务逻辑和数据模型。

## 主要类和方法

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

## 贡献

欢迎提交问题和贡献代码。请确保在提交之前阅读并遵循项目的贡献指南。

## 许可证

此项目使用 MIT 许可证。详细信息请参阅 LICENSE 文件。

---

如有任何问题或建议，请联系项目维护者。感谢您的使用和支持！
