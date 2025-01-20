using Dictation.Core;

namespace Dictation.Presentation
{
    public partial class MainWindow : Form
    {
        private readonly WordCollection wordCollection;
        private readonly WindowFactory windowFactory;

        public MainWindow(WordCollection wordCollection, WindowFactory windowFactory)
        {
            this.wordCollection = wordCollection;
            this.windowFactory = windowFactory;
            InitializeComponent();

            InitializeLanuageOptions();
            InitializePlayMod();

            UpdateWordGrid();
        }

        private void InitializePlayMod()
        {
            listPlayMod.DataSource = TTSOption.SupportPlayMods.ToList();
            listPlayMod.DisplayMember = "Value";
            listPlayMod.ValueMember = "Key";

            listPlayMod.SelectedIndex = 0;
        }


        private void InitializeLanuageOptions()
        {
            listLanguage.DataSource = TTSOption.SupportLanguages.ToList();
            listLanguage.DisplayMember = "Value";
            listLanguage.ValueMember = "Key";

            listLanguage.SelectedIndex = 0;
        }

        private void UpdateWordGrid()
        {
            dataGridViewWords.Rows.Clear();

            var words = wordCollection.GetWords();
            foreach (var word in words)
            {
                dataGridViewWords.Rows.Add(word);
            }


            lbWordCount.Text = $"总共：{wordCollection.Count}个单词";
        }

        private void btnImportWords_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                openFileDialog.Title = "选择一个文本文件";

                // 显示对话框并判断用户是否选择了文件
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // 读取文件内容
                        var lines = File.ReadAllLines(openFileDialog.FileName);

                        wordCollection.Import(lines);

                        // 更新 DataGridView 显示
                        UpdateWordGrid();
                        MessageBox.Show("单词导入成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        // 捕获错误并显示错误消息
                        MessageBox.Show($"导入单词时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void btnAddWord_Click(object sender, EventArgs e)
        {
            var word = txtWord.Text.Trim();
            if (!string.IsNullOrEmpty(word))
            {
                wordCollection.AddWord(word);
                UpdateWordGrid();
                txtWord.Text = string.Empty;
            }
        }

        private void btnClearWords_Click(object sender, EventArgs e)
        {
            wordCollection.ClearWords();
            UpdateWordGrid();
        }

        private void dataGridViewWords_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // 判断是否点击了按钮列
            if (e.RowIndex >= 0 && e.ColumnIndex == dataGridViewWords.Columns["ActionColumn"].Index)
            {
                var word = dataGridViewWords.Rows[e.RowIndex].Cells["WordColumn"].Value?.ToString();
                if (!string.IsNullOrEmpty(word))
                {
                    wordCollection.RemoveWord(word); // 删除逻辑
                    UpdateWordGrid(); // 更新视图
                }
            }
        }

        private void btnStartDictation_Click(object sender, EventArgs e)
        {
            if (wordCollection.Count == 0)
            {
                MessageBox.Show("请先导入单词或添加单词！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }




            try
            {
                var dictationWindow = windowFactory.GetWindow<DictationWindow>();

                dictationWindow.Show();

                Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生错误：{ex.Message}", "程序错误");
            }

        }

        private void listLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            TTSOption.Instance.Language = (KeyValuePair<string, string>)listLanguage.SelectedItem;
        }

        private void listPlayMod_SelectedIndexChanged(object sender, EventArgs e)
        {
            TTSOption.Instance.PlayMod = ((KeyValuePair<string, string>)listPlayMod.SelectedItem).Key;
        }

        private void dataGridViewWords_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void dataGridViewWords_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    if (Path.GetExtension(file).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var lines = File.ReadAllLines(file);
                            wordCollection.Import(lines);
                            MessageBox.Show("单词导入成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"导入单词时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                UpdateWordGrid();
            }
        }
    }
}
