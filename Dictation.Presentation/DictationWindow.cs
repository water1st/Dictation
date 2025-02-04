using Dictation.Core;
using System.Drawing.Drawing2D;

namespace Dictation.Presentation
{
    public partial class DictationWindow : Form
    {
        private readonly WordDrawingCollection wordDrawings;
        private readonly DictationManager dictationManager;
        private readonly WindowFactory windowFactory;
        private string currentWord;
        private Point lastPoint = Point.Empty;
        private Bitmap drawingBitmap;
        private Graphics drawingGraphics;
        GraphicsPath currentPath;
        private bool end = false;

        public DictationWindow(WordDrawingCollection wordDrawings,
            DictationManager dictationManager, WindowFactory windowFactory)
        {
            this.wordDrawings = wordDrawings;
            this.dictationManager = dictationManager;
            this.windowFactory = windowFactory;
            InitializeComponent();

            InitializeDrawingBoard();

            // 启用双缓冲
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            UpdateStyles();
        }

        private void UpdateStatus()
        {
            labelStatus.Text = $"{dictationManager.Current}/{dictationManager.WordCount}";
        }

        private void InitializeDrawingBoard()
        {
            if (drawingPanel == null) throw new InvalidOperationException("画板控件未初始化");

            // 创建位图并设置到画板背景
            drawingBitmap = new Bitmap(drawingPanel.Width, drawingPanel.Height);
            drawingPanel.BackgroundImage = drawingBitmap;

            // 创建绘图对象并设置背景颜色
            drawingGraphics = Graphics.FromImage(drawingBitmap);
            drawingGraphics.Clear(Color.White);
            currentPath = new GraphicsPath();
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentWord))
            {
                MessageBox.Show("没有当前单词播放！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SaveBitmap();

            //请求播放下一个单词
            if (!dictationManager.IsEnd)
            {
                InitializeDrawingBoard();
                currentWord = dictationManager.PlayNextWord();
                UpdateStatus();
            }
            else
            {
                end = true;
                Close();
            }
        }

        private void SaveBitmap()
        {
            // 1. 保存画布内容为位图
            var bitmap = new Bitmap(drawingPanel.Width, drawingPanel.Height);
            drawingPanel.DrawToBitmap(bitmap, new Rectangle(0, 0, drawingPanel.Width, drawingPanel.Height));

            // 2. 将当前单词和位图保存到 WordDrawingCollection
            var wordDrawing = new WordDrawing(currentWord, bitmap);
            wordDrawings.Add(wordDrawing);
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            dictationManager.PlayCurrentWord();
        }

        private void OpenReviewWindow()
        {
            var reviewWindow = windowFactory.GetWindow<ReviewWindow>();
            reviewWindow.Show();
        }

        private void drawingPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                lastPoint = e.Location; // 记录初始点

                currentPath.Reset();
                currentPath.StartFigure(); // 开始一个新的路径
                currentPath.AddLine(lastPoint, lastPoint); // 第一个点
            }
        }

        private void drawingPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                currentPath.CloseFigure(); // 结束当前路径
                lastPoint = Point.Empty; // 重置点
            }
        }

        private void drawingPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (lastPoint != Point.Empty)
            {
                // 增加当前点到路径中
                currentPath.AddLine(lastPoint, e.Location); // 连接当前点与上一个点

                // 使用内存位图进行绘制
                using (var pen = new Pen(Color.Black, 5))
                {
                    drawingGraphics.DrawPath(pen, currentPath);
                }

                // 只重绘画板区域
                drawingPanel.Invalidate(new Rectangle(Math.Min(lastPoint.X, e.X) - 5, Math.Min(lastPoint.Y, e.Y) - 5, Math.Abs(lastPoint.X - e.X) + 10, Math.Abs(lastPoint.Y - e.Y) + 10));

                lastPoint = e.Location; // 更新上一个点
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveAndCloseWindow();

            dictationManager.Dispose();

            base.OnClosed(e);
        }

        private void SaveAndCloseWindow()
        {
            if (!end)
            {
                if (wordDrawings.Any(c => c.Word == currentWord && c.Drawing == null))
                    SaveBitmap();

                InitializeDrawingBoard();

                var words = dictationManager.Stop();
                foreach (var word in words)
                {
                    var bitmap = new Bitmap(drawingPanel.Width, drawingPanel.Height);
                    drawingPanel.DrawToBitmap(bitmap, new Rectangle(0, 0, drawingPanel.Width, drawingPanel.Height));

                    // 2. 将当前单词和位图保存到 WordDrawingCollection
                    var wordDrawing = new WordDrawing(word, bitmap);
                    wordDrawings.Add(wordDrawing);
                }
            }

            OpenReviewWindow();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            currentWord = dictationManager.PlayNextWord();
            UpdateStatus();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            InitializeDrawingBoard();
        }
    }
}
