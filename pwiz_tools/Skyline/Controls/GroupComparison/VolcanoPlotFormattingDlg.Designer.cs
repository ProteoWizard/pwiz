namespace pwiz.Skyline.Controls.GroupComparison
{
    partial class VolcanoPlotFormattingDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VolcanoPlotFormattingDlg));
            this.colorDialog1 = new System.Windows.Forms.ColorDialog();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.regexColorRowGrid1 = new RegexColorRowGrid();
            this.SuspendLayout();
            // 
            // button1
            // 
            resources.ApplyResources(this.button1, "button1");
            this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button1.Name = "button1";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            resources.ApplyResources(this.button2, "button2");
            this.button2.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button2.Name = "button2";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // regexColorRowGrid1
            // 
            this.regexColorRowGrid1.AllowUserToAddRows = true;
            this.regexColorRowGrid1.AllowUserToOrderColumns = false;
            resources.ApplyResources(this.regexColorRowGrid1, "regexColorRowGrid1");
            this.regexColorRowGrid1.Name = "regexColorRowGrid1";
            this.regexColorRowGrid1.OnCellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.regexColorRowGrid1_OnCellValueChanged);
            this.regexColorRowGrid1.OnCellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.regexColorRowGrid1_OnCellClick);
            // 
            // VolcanoPlotFormattingDlg
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.regexColorRowGrid1);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "VolcanoPlotFormattingDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ColorDialog colorDialog1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private RegexColorRowGrid regexColorRowGrid1;

    }
}