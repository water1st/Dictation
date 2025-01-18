using Dictation.Core;

namespace Dictation.Presentation
{
    public partial class ReviewWindow : Form
    {
        private readonly WordDrawingCollection wordDrawings;
        private readonly ITTSPlayer player;
        private readonly WindowFactory windowFactory;
        private bool isJudged = false;

        public ReviewWindow(WordDrawingCollection wordDrawings, TTSFactory ttsFactory, WindowFactory windowFactory)
        {
            InitializeComponent();
            this.wordDrawings = wordDrawings;
            player = ttsFactory.CreateTTSPlayer(TTSOption.Instance.Target, TTSOption.Instance.LanguageName);
            this.windowFactory = windowFactory;

            InitializeDataGridView();
        }


        private void InitializeDataGridView()
        {
            foreach (var wordDrawing in wordDrawings)
            {
                var index = reviewGridView.Rows.Add();
                var row = reviewGridView.Rows[index];

                row.Cells["Word"].Value = wordDrawing.Word;
                row.Cells["Image"].Value = wordDrawing.Drawing;
            }
        }

        private void reviewGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == reviewGridView.Columns["Play"].Index)
            {
                PlayButton_Click(sender, e);
            }

            if (e.RowIndex >= 0 && e.ColumnIndex == reviewGridView.Columns["Correct"].Index)
            {
                CorrectButton_Click(sender, e);
            }

            if (e.RowIndex >= 0 && e.ColumnIndex == reviewGridView.Columns["Incorrect"].Index)
            {
                IncorrectButton_Click(sender, e);
            }


            if (wordDrawings.All(w => w.IsJudged))
            {
                isJudged = true;
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            player.Dispose();

            if (!isJudged)
                DefaultJudge();

            var window = windowFactory.GetWindow<ResultWindow>();
            window.Show();

            base.OnClosed(e);
        }

        private void DefaultJudge()
        {
            foreach (var word in wordDrawings)
            {
                if (!word.IsJudged)
                    word.Judge(false);
            }
            isJudged = true;
        }

        private void PlayButton_Click(object sender, DataGridViewCellEventArgs e)
        {
            var word = wordDrawings[e.RowIndex].Word;

            player.Play(word);
        }


        private void CorrectButton_Click(object sender, DataGridViewCellEventArgs e)
        {
            var word = wordDrawings[e.RowIndex];
            word.Judge(true);
            reviewGridView.Rows[e.RowIndex].Visible = false;
        }

        private void IncorrectButton_Click(object sender, DataGridViewCellEventArgs e)
        {
            var word = wordDrawings[e.RowIndex];
            word.Judge(false);
            reviewGridView.Rows[e.RowIndex].Visible = false;
        }
    }
}
