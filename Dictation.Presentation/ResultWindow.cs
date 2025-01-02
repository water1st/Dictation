using Dictation.Core;
using System.Data;
using System.Text;
using System.Windows.Forms;

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
            var accuracyRate = (wordDrawings.Count - words.Length) * 100 / wordDrawings.Count;

            try
            {
                lableAaccuracyRate.Text = $"正确率：{accuracyRate}%";
            }
            catch (Exception ex) { }


            InitializeResultGridView();


            if (accuracyRate < 100)
            {
                btnExport.Enabled = true;
            }
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

        private void btnExport_Click(object sender, EventArgs e)
        {
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                saveFileDialog.DefaultExt = "txt";
                saveFileDialog.Title = "保存文件";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var path = saveFileDialog.FileName;

                    var content = new StringBuilder();
                    foreach (var word in wordDrawings)
                    {
                        if (!word.IsCorrect)
                            content.AppendLine(word.Word.ToString());
                    }

                    File.WriteAllText(path, content.ToString(), Encoding.UTF8);
                }

            }

        }
    }
}
