using Dictation.Core;
using System.Data;

namespace Dictation.Presentation
{
    public partial class ResultWindow : Form
    {
        private readonly WindowFactory windowFactory;
        private readonly WordDrawingCollection wordDrawings;
        private readonly string[] words;

        public ResultWindow(WindowFactory windowFactory, WordDrawingCollection wordDrawings)
        {
            this.windowFactory = windowFactory;
            this.wordDrawings = wordDrawings;
            words = wordDrawings.Where(word => word.IsCorrect == false).Select(word => word.Word).ToArray();
            InitializeComponent();

            try
            {
                lableAaccuracyRate.Text = $"正确率：{(wordDrawings.Count - words.Length) * 100 / wordDrawings.Count}%";
            }
            catch (Exception ex) { }
            

            InitializeResultGridView();
        }

        private void InitializeResultGridView()
        {
            foreach (var worl in words)
            {
                resultGridView.Rows.Add(worl);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            wordDrawings.Clear();
            windowFactory.GetWindow<MainWindow>().Show();
            base.OnClosed(e);
        }

    }
}
