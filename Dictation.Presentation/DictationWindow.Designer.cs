namespace Dictation.Presentation
{
    partial class DictationWindow
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
            btnNext = new Button();
            btnPlay = new Button();
            drawingPanel = new Panel();
            btnClear = new Button();
            SuspendLayout();
            // 
            // btnNext
            // 
            btnNext.Location = new Point(400, 27);
            btnNext.Name = "btnNext";
            btnNext.Size = new Size(112, 34);
            btnNext.TabIndex = 0;
            btnNext.Text = "下一个";
            btnNext.UseVisualStyleBackColor = true;
            btnNext.Click += btnNext_Click;
            // 
            // btnPlay
            // 
            btnPlay.Location = new Point(12, 27);
            btnPlay.Name = "btnPlay";
            btnPlay.Size = new Size(112, 34);
            btnPlay.TabIndex = 1;
            btnPlay.Text = "播放";
            btnPlay.UseVisualStyleBackColor = true;
            btnPlay.Click += btnPlay_Click;
            // 
            // drawingPanel
            // 
            drawingPanel.BackColor = SystemColors.ButtonHighlight;
            drawingPanel.Location = new Point(12, 77);
            drawingPanel.Name = "drawingPanel";
            drawingPanel.Size = new Size(500, 200);
            drawingPanel.TabIndex = 2;
            drawingPanel.MouseDown += drawingPanel_MouseDown;
            drawingPanel.MouseMove += drawingPanel_MouseMove;
            drawingPanel.MouseUp += drawingPanel_MouseUp;
            // 
            // btnClear
            // 
            btnClear.Location = new Point(204, 27);
            btnClear.Name = "btnClear";
            btnClear.Size = new Size(112, 34);
            btnClear.TabIndex = 3;
            btnClear.Text = "清除";
            btnClear.UseVisualStyleBackColor = true;
            btnClear.Click += btnClear_Click;
            // 
            // DictationWindow
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(527, 295);
            Controls.Add(btnClear);
            Controls.Add(drawingPanel);
            Controls.Add(btnPlay);
            Controls.Add(btnNext);
            Name = "DictationWindow";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "听写";
            ResumeLayout(false);
        }

        #endregion

        private Button btnNext;
        private Button btnPlay;
        private Panel drawingPanel;
        private Button btnClear;
    }
}