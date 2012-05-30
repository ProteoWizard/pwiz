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
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(293, 61);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Close";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnFindNext
            // 
            this.btnFindNext.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnFindNext.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnFindNext.Enabled = false;
            this.btnFindNext.Location = new System.Drawing.Point(293, 7);
            this.btnFindNext.Name = "btnFindNext";
            this.btnFindNext.Size = new System.Drawing.Size(75, 23);
            this.btnFindNext.TabIndex = 4;
            this.btnFindNext.Text = "&Find Next";
            this.btnFindNext.UseVisualStyleBackColor = true;
            this.btnFindNext.Click += new System.EventHandler(this.btnFindNext_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(56, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Find what:";
            // 
            // textSequence
            // 
            this.textSequence.Location = new System.Drawing.Point(16, 29);
            this.textSequence.Name = "textSequence";
            this.textSequence.Size = new System.Drawing.Size(249, 20);
            this.textSequence.TabIndex = 1;
            this.textSequence.TextChanged += new System.EventHandler(this.textSequence_TextChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioDown);
            this.groupBox1.Controls.Add(this.radioUp);
            this.groupBox1.Location = new System.Drawing.Point(16, 66);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(130, 44);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Direction";
            // 
            // radioDown
            // 
            this.radioDown.AutoSize = true;
            this.radioDown.Checked = true;
            this.radioDown.Location = new System.Drawing.Point(53, 18);
            this.radioDown.Name = "radioDown";
            this.radioDown.Size = new System.Drawing.Size(53, 17);
            this.radioDown.TabIndex = 1;
            this.radioDown.TabStop = true;
            this.radioDown.Text = "&Down";
            this.radioDown.UseVisualStyleBackColor = true;
            // 
            // radioUp
            // 
            this.radioUp.AutoSize = true;
            this.radioUp.Location = new System.Drawing.Point(7, 18);
            this.radioUp.Name = "radioUp";
            this.radioUp.Size = new System.Drawing.Size(39, 17);
            this.radioUp.TabIndex = 0;
            this.radioUp.TabStop = true;
            this.radioUp.Text = "&Up";
            this.radioUp.UseVisualStyleBackColor = true;
            // 
            // cbCaseSensitive
            // 
            this.cbCaseSensitive.AutoSize = true;
            this.cbCaseSensitive.Location = new System.Drawing.Point(153, 84);
            this.cbCaseSensitive.Name = "cbCaseSensitive";
            this.cbCaseSensitive.Size = new System.Drawing.Size(83, 17);
            this.cbCaseSensitive.TabIndex = 3;
            this.cbCaseSensitive.Text = "Match Case";
            this.cbCaseSensitive.UseVisualStyleBackColor = true;
            // 
            // btnFindAll
            // 
            this.btnFindAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnFindAll.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnFindAll.Enabled = false;
            this.btnFindAll.Location = new System.Drawing.Point(293, 34);
            this.btnFindAll.Name = "btnFindAll";
            this.btnFindAll.Size = new System.Drawing.Size(75, 23);
            this.btnFindAll.TabIndex = 5;
            this.btnFindAll.Text = "Find &All";
            this.btnFindAll.UseVisualStyleBackColor = true;
            this.btnFindAll.Click += new System.EventHandler(this.btnFindAll_Click);
            // 
            // btnShowHideAdvanced
            // 
            this.btnShowHideAdvanced.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnShowHideAdvanced.AutoSize = true;
            this.btnShowHideAdvanced.Location = new System.Drawing.Point(252, 90);
            this.btnShowHideAdvanced.Name = "btnShowHideAdvanced";
            this.btnShowHideAdvanced.Size = new System.Drawing.Size(116, 23);
            this.btnShowHideAdvanced.TabIndex = 7;
            this.btnShowHideAdvanced.Text = "<< Hide Ad&vanced";
            this.btnShowHideAdvanced.UseVisualStyleBackColor = true;
            this.btnShowHideAdvanced.Click += new System.EventHandler(this.btnShowHideAdvanced_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 130);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(80, 13);
            this.label2.TabIndex = 8;
            this.label2.Text = "Also search for:";
            // 
            // checkedListBoxFinders
            // 
            this.checkedListBoxFinders.CheckOnClick = true;
            this.checkedListBoxFinders.FormattingEnabled = true;
            this.checkedListBoxFinders.Location = new System.Drawing.Point(12, 146);
            this.checkedListBoxFinders.Name = "checkedListBoxFinders";
            this.checkedListBoxFinders.Size = new System.Drawing.Size(356, 109);
            this.checkedListBoxFinders.TabIndex = 9;
            this.checkedListBoxFinders.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.checkedListBoxOptions_ItemCheck);
            // 
            // FindNodeDlg
            // 
            this.AcceptButton = this.btnFindNext;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(380, 263);
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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Find";
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