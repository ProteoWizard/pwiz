namespace pwiz.Common.DataBinding.Controls.Editor
{
    partial class FindColumnDlg
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FindColumnDlg));
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.btnFindNext = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnFindPrevious = new System.Windows.Forms.Button();
            this.cbCaseSensitive = new System.Windows.Forms.CheckBox();
            this.tbxFind = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // btnFindNext
            // 
            resources.ApplyResources(this.btnFindNext, "btnFindNext");
            this.btnFindNext.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnFindNext.Name = "btnFindNext";
            this.btnFindNext.UseVisualStyleBackColor = true;
            this.btnFindNext.Click += new System.EventHandler(this.FindNextButtonOnClick);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnFindPrevious
            // 
            resources.ApplyResources(this.btnFindPrevious, "btnFindPrevious");
            this.btnFindPrevious.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnFindPrevious.Name = "btnFindPrevious";
            this.btnFindPrevious.UseVisualStyleBackColor = true;
            this.btnFindPrevious.Click += new System.EventHandler(this.FindPreviousButtonOnClick);
            // 
            // cbCaseSensitive
            // 
            resources.ApplyResources(this.cbCaseSensitive, "cbCaseSensitive");
            this.cbCaseSensitive.Name = "cbCaseSensitive";
            this.cbCaseSensitive.UseVisualStyleBackColor = true;
            // 
            // tbxFind
            // 
            resources.ApplyResources(this.tbxFind, "tbxFind");
            this.tbxFind.Name = "tbxFind";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // FindColumnDlg
            // 
            this.AcceptButton = this.btnFindNext;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ControlBox = false;
            this.Controls.Add(this.btnFindPrevious);
            this.Controls.Add(this.cbCaseSensitive);
            this.Controls.Add(this.tbxFind);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnFindNext);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FindColumnDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox cbCaseSensitive;
        private System.Windows.Forms.TextBox tbxFind;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnFindNext;
        private System.Windows.Forms.Button btnFindPrevious;
        private System.Windows.Forms.Timer timer1;


    }
}