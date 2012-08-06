namespace pwiz.Skyline.EditUI
{
    partial class FindNodeDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FindNodeDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnFindNext = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textSequence = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.radioDown = new System.Windows.Forms.RadioButton();
            this.radioUp = new System.Windows.Forms.RadioButton();
            this.cbCaseSensitive = new System.Windows.Forms.CheckBox();
            this.btnFindAll = new System.Windows.Forms.Button();
            this.btnShowHideAdvanced = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.checkedListBoxFinders = new System.Windows.Forms.CheckedListBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnFindNext
            // 
            resources.ApplyResources(this.btnFindNext, "btnFindNext");
            this.btnFindNext.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnFindNext.Name = "btnFindNext";
            this.btnFindNext.UseVisualStyleBackColor = true;
            this.btnFindNext.Click += new System.EventHandler(this.btnFindNext_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textSequence
            // 
            resources.ApplyResources(this.textSequence, "textSequence");
            this.textSequence.Name = "textSequence";
            this.textSequence.TextChanged += new System.EventHandler(this.textSequence_TextChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioDown);
            this.groupBox1.Controls.Add(this.radioUp);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // radioDown
            // 
            resources.ApplyResources(this.radioDown, "radioDown");
            this.radioDown.Checked = true;
            this.radioDown.Name = "radioDown";
            this.radioDown.TabStop = true;
            this.radioDown.UseVisualStyleBackColor = true;
            // 
            // radioUp
            // 
            resources.ApplyResources(this.radioUp, "radioUp");
            this.radioUp.Name = "radioUp";
            this.radioUp.TabStop = true;
            this.radioUp.UseVisualStyleBackColor = true;
            // 
            // cbCaseSensitive
            // 
            resources.ApplyResources(this.cbCaseSensitive, "cbCaseSensitive");
            this.cbCaseSensitive.Name = "cbCaseSensitive";
            this.cbCaseSensitive.UseVisualStyleBackColor = true;
            // 
            // btnFindAll
            // 
            resources.ApplyResources(this.btnFindAll, "btnFindAll");
            this.btnFindAll.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnFindAll.Name = "btnFindAll";
            this.btnFindAll.UseVisualStyleBackColor = true;
            this.btnFindAll.Click += new System.EventHandler(this.btnFindAll_Click);
            // 
            // btnShowHideAdvanced
            // 
            resources.ApplyResources(this.btnShowHideAdvanced, "btnShowHideAdvanced");
            this.btnShowHideAdvanced.Name = "btnShowHideAdvanced";
            this.btnShowHideAdvanced.UseVisualStyleBackColor = true;
            this.btnShowHideAdvanced.Click += new System.EventHandler(this.btnShowHideAdvanced_Click);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // checkedListBoxFinders
            // 
            this.checkedListBoxFinders.CheckOnClick = true;
            this.checkedListBoxFinders.FormattingEnabled = true;
            resources.ApplyResources(this.checkedListBoxFinders, "checkedListBoxFinders");
            this.checkedListBoxFinders.Name = "checkedListBoxFinders";
            this.checkedListBoxFinders.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.checkedListBoxOptions_ItemCheck);
            // 
            // FindNodeDlg
            // 
            this.AcceptButton = this.btnFindNext;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnShowHideAdvanced);
            this.Controls.Add(this.btnFindAll);
            this.Controls.Add(this.cbCaseSensitive);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.textSequence);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnFindNext);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.checkedListBoxFinders);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FindNodeDlg";
            this.ShowInTaskbar = false;
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnFindNext;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textSequence;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton radioDown;
        private System.Windows.Forms.RadioButton radioUp;
        private System.Windows.Forms.CheckBox cbCaseSensitive;
        private System.Windows.Forms.Button btnFindAll;
        private System.Windows.Forms.Button btnShowHideAdvanced;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckedListBox checkedListBoxFinders;
    }
}