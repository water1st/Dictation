namespace Dictation.Presentation
{
    partial class MainWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnAddWord = new Button();
            txtWord = new TextBox();
            btnStartDictation = new Button();
            btnClearWords = new Button();
            btnImportWords = new Button();
            dataGridViewWords = new DataGridView();
            WordColumn = new DataGridViewTextBoxColumn();
            ActionColumn = new DataGridViewButtonColumn();
            listLanguage = new ComboBox();
            lbWordCount = new Label();
            listPlayMod = new ComboBox();
            labelTTSName = new Label();
            labelPlayMod = new Label();
            ((System.ComponentModel.ISupportInitialize)dataGridViewWords).BeginInit();
            SuspendLayout();
            // 
            // btnAddWord
            // 
            btnAddWord.Location = new Point(333, 75);
            btnAddWord.Name = "btnAddWord";
            btnAddWord.Size = new Size(112, 34);
            btnAddWord.TabIndex = 10;
            btnAddWord.Text = "添加单词";
            btnAddWord.UseVisualStyleBackColor = true;
            btnAddWord.Click += btnAddWord_Click;
            // 
            // txtWord
            // 
            txtWord.Location = new Point(19, 79);
            txtWord.Name = "txtWord";
            txtWord.Size = new Size(288, 30);
            txtWord.TabIndex = 9;
            // 
            // btnStartDictation
            // 
            btnStartDictation.Location = new Point(333, 23);
            btnStartDictation.Name = "btnStartDictation";
            btnStartDictation.Size = new Size(112, 34);
            btnStartDictation.TabIndex = 8;
            btnStartDictation.Text = "开始听写";
            btnStartDictation.UseVisualStyleBackColor = true;
            btnStartDictation.Click += btnStartDictation_Click;
            // 
            // btnClearWords
            // 
            btnClearWords.Location = new Point(172, 23);
            btnClearWords.Name = "btnClearWords";
            btnClearWords.Size = new Size(112, 34);
            btnClearWords.TabIndex = 7;
            btnClearWords.Text = "清空单词";
            btnClearWords.UseVisualStyleBackColor = true;
            btnClearWords.Click += btnClearWords_Click;
            // 
            // btnImportWords
            // 
            btnImportWords.Location = new Point(12, 23);
            btnImportWords.Name = "btnImportWords";
            btnImportWords.Size = new Size(112, 34);
            btnImportWords.TabIndex = 6;
            btnImportWords.Text = "导入单词";
            btnImportWords.UseVisualStyleBackColor = true;
            btnImportWords.Click += btnImportWords_Click;
            // 
            // dataGridViewWords
            // 
            dataGridViewWords.AllowDrop = true;
            dataGridViewWords.BackgroundColor = SystemColors.ButtonHighlight;
            dataGridViewWords.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewWords.Columns.AddRange(new DataGridViewColumn[] { WordColumn, ActionColumn });
            dataGridViewWords.Location = new Point(19, 127);
            dataGridViewWords.Name = "dataGridViewWords";
            dataGridViewWords.RowHeadersWidth = 62;
            dataGridViewWords.Size = new Size(917, 311);
            dataGridViewWords.TabIndex = 11;
            dataGridViewWords.CellContentClick += dataGridViewWords_CellContentClick;
            dataGridViewWords.DragDrop += dataGridViewWords_DragDrop;
            dataGridViewWords.DragEnter += dataGridViewWords_DragEnter;
            // 
            // WordColumn
            // 
            WordColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            WordColumn.HeaderText = "单词";
            WordColumn.MinimumWidth = 8;
            WordColumn.Name = "WordColumn";
            WordColumn.ReadOnly = true;
            // 
            // ActionColumn
            // 
            ActionColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            ActionColumn.HeaderText = "动作";
            ActionColumn.MinimumWidth = 8;
            ActionColumn.Name = "ActionColumn";
            ActionColumn.ReadOnly = true;
            ActionColumn.Text = "删除";
            ActionColumn.UseColumnTextForButtonValue = true;
            ActionColumn.Width = 52;
            // 
            // listLanguage
            // 
            listLanguage.FormattingEnabled = true;
            listLanguage.Location = new Point(569, 20);
            listLanguage.Name = "listLanguage";
            listLanguage.Size = new Size(182, 32);
            listLanguage.TabIndex = 12;
            listLanguage.SelectedIndexChanged += listLanguage_SelectedIndexChanged;
            // 
            // lbWordCount
            // 
            lbWordCount.AutoSize = true;
            lbWordCount.Location = new Point(793, 28);
            lbWordCount.Name = "lbWordCount";
            lbWordCount.Size = new Size(129, 24);
            lbWordCount.TabIndex = 13;
            lbWordCount.Text = "总共：0个单词";
            // 
            // listPlayMod
            // 
            listPlayMod.FormattingEnabled = true;
            listPlayMod.Location = new Point(569, 75);
            listPlayMod.Name = "listPlayMod";
            listPlayMod.Size = new Size(182, 32);
            listPlayMod.TabIndex = 14;
            listPlayMod.SelectedIndexChanged += listPlayMod_SelectedIndexChanged;
            // 
            // labelTTSName
            // 
            labelTTSName.AutoSize = true;
            labelTTSName.Location = new Point(463, 28);
            labelTTSName.Name = "labelTTSName";
            labelTTSName.Size = new Size(58, 24);
            labelTTSName.TabIndex = 15;
            labelTTSName.Text = "TTS：";
            // 
            // labelPlayMod
            // 
            labelPlayMod.AutoSize = true;
            labelPlayMod.Location = new Point(463, 82);
            labelPlayMod.Name = "labelPlayMod";
            labelPlayMod.Size = new Size(100, 24);
            labelPlayMod.TabIndex = 16;
            labelPlayMod.Text = "播放模式：";
            // 
            // MainWindow
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(949, 447);
            Controls.Add(labelPlayMod);
            Controls.Add(labelTTSName);
            Controls.Add(listPlayMod);
            Controls.Add(lbWordCount);
            Controls.Add(listLanguage);
            Controls.Add(dataGridViewWords);
            Controls.Add(btnAddWord);
            Controls.Add(txtWord);
            Controls.Add(btnStartDictation);
            Controls.Add(btnClearWords);
            Controls.Add(btnImportWords);
            Name = "MainWindow";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "听写不求人";
            ((System.ComponentModel.ISupportInitialize)dataGridViewWords).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Button btnAddWord;
        private TextBox txtWord;
        private Button btnStartDictation;
        private Button btnClearWords;
        private Button btnImportWords;
        private DataGridView dataGridViewWords;
        private DataGridViewTextBoxColumn WordColumn;
        private DataGridViewButtonColumn ActionColumn;
        private ComboBox listLanguage;
        private Label lbWordCount;
        private ComboBox listPlayMod;
        private Label labelTTSName;
        private Label labelPlayMod;
    }
}