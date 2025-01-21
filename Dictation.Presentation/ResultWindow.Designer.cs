namespace Dictation.Presentation
{
    partial class ResultWindow
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
            lableAaccuracyRate = new Label();
            resultGridView = new DataGridView();
            word = new DataGridViewTextBoxColumn();
            btnExport = new Button();
            ((System.ComponentModel.ISupportInitialize)resultGridView).BeginInit();
            SuspendLayout();
            // 
            // lableAaccuracyRate
            // 
            lableAaccuracyRate.AutoSize = true;
            lableAaccuracyRate.Location = new Point(16, 35);
            lableAaccuracyRate.Name = "lableAaccuracyRate";
            lableAaccuracyRate.Size = new Size(109, 24);
            lableAaccuracyRate.TabIndex = 1;
            lableAaccuracyRate.Text = "正确率：0%";
            // 
            // resultGridView
            // 
            resultGridView.BackgroundColor = SystemColors.ButtonHighlight;
            resultGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            resultGridView.Columns.AddRange(new DataGridViewColumn[] { word });
            resultGridView.Location = new Point(16, 91);
            resultGridView.Name = "resultGridView";
            resultGridView.RowHeadersWidth = 62;
            resultGridView.Size = new Size(772, 347);
            resultGridView.TabIndex = 2;
            // 
            // word
            // 
            word.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            word.HeaderText = "错误的单词";
            word.MinimumWidth = 8;
            word.Name = "word";
            word.ReadOnly = true;
            // 
            // btnExport
            // 
            btnExport.Enabled = false;
            btnExport.Location = new Point(676, 25);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(112, 34);
            btnExport.TabIndex = 3;
            btnExport.Text = "导出错误单词";
            btnExport.UseVisualStyleBackColor = true;
            btnExport.Click += btnExport_Click;
            // 
            // ResultWindow
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(btnExport);
            Controls.Add(resultGridView);
            Controls.Add(lableAaccuracyRate);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "ResultWindow";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "听写结果";
            ((System.ComponentModel.ISupportInitialize)resultGridView).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Label lableAaccuracyRate;
        private DataGridView resultGridView;
        private DataGridViewTextBoxColumn word;
        private Button btnExport;
    }
}