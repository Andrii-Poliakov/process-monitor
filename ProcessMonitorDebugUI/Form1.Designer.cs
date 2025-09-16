namespace ProcessMonitorDebugUI
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            textBox1 = new TextBox();
            buttonGetProcessList = new Button();
            SuspendLayout();
            // 
            // textBox1
            // 
            textBox1.Location = new Point(316, 12);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.ScrollBars = ScrollBars.Both;
            textBox1.Size = new Size(593, 525);
            textBox1.TabIndex = 0;
            textBox1.WordWrap = false;
            // 
            // buttonGetProcessList
            // 
            buttonGetProcessList.Location = new Point(12, 12);
            buttonGetProcessList.Name = "buttonGetProcessList";
            buttonGetProcessList.Size = new Size(163, 23);
            buttonGetProcessList.TabIndex = 1;
            buttonGetProcessList.Text = "Get process list";
            buttonGetProcessList.UseVisualStyleBackColor = true;
            buttonGetProcessList.Click += buttonGetProcessList_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(978, 612);
            Controls.Add(buttonGetProcessList);
            Controls.Add(textBox1);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox textBox1;
        private Button buttonGetProcessList;
    }
}
