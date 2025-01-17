namespace Dictation.Presentation
{
    partial class ReviewWindow
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
            reviewGridView = new DataGridView();
            Word = new DataGridViewTextBoxColumn();
            Image = new DataGridViewImageColumn();
            Play = new DataGridViewButtonColumn();
            Correct = new DataGridViewButtonColumn();
            Incorrect = new DataGridViewButtonColumn();
            ((System.ComponentModel.ISupportInitialize)reviewGridView).BeginInit();
            SuspendLayout();
            // 
            // reviewGridView
            // 
            reviewGridView.BackgroundColor = SystemColors.ButtonHighlight;
            reviewGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            reviewGridView.Columns.AddRange(new DataGridViewColumn[] { Word, Image, Play, Correct, Incorrect });
            reviewGridView.Location = new Point(21, 30);
            reviewGridView.Name = "reviewGridView";
            reviewGridView.RowHeadersWidth = 62;
            reviewGridView.RowTemplate.Height = 200;
            reviewGridView.Size = new Size(2290, 1388);
            reviewGridView.TabIndex = 0;
            reviewGridView.CellClick += reviewGridView_CellClick;
            // 
            // Word
            // 
            Word.HeaderText = "单词";
            Word.MinimumWidth = 8;
            Word.Name = "Word";
            Word.ReadOnly = true;
            Word.Width = 400;
            // 
            // Image
            // 
            Image.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            Image.HeaderText = "书写图像";
            Image.MinimumWidth = 8;
            Image.Name = "Image";
            Image.ReadOnly = true;
            // 
            // Play
            // 
            Play.HeaderText = "播放";
            Play.MinimumWidth = 8;
            Play.Name = "Play";
            Play.ReadOnly = true;
            Play.Text = "播放";
            Play.UseColumnTextForButtonValue = true;
            Play.Width = 150;
            // 
            // Correct
            // 
            Correct.HeaderText = "正确";
            Correct.MinimumWidth = 8;
            Correct.Name = "Correct";
            Correct.Text = "✔";
            Correct.UseColumnTextForButtonValue = true;
            Correct.Width = 150;
            // 
            // Incorrect
            // 
            Incorrect.HeaderText = "错误";
            Incorrect.MinimumWidth = 8;
            Incorrect.Name = "Incorrect";
            Incorrect.Text = "✘";
            Incorrect.UseColumnTextForButtonValue = true;
            Incorrect.Width = 150;
            // 
            // ReviewWindow
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(2323, 1430);
            Controls.Add(reviewGridView);
            Name = "ReviewWindow";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "检查听写结果";
            ((System.ComponentModel.ISupportInitialize)reviewGridView).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private DataGridView reviewGridView;
        private DataGridViewTextBoxColumn Word;
        private DataGridViewImageColumn Image;
        private DataGridViewButtonColumn Play;
        private DataGridViewButtonColumn Correct;
        private DataGridViewButtonColumn Incorrect;
    }
}