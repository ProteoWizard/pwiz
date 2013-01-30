namespace pwiz.Topograph.ui.Forms
{
    partial class AcceptanceCriteriaForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AcceptanceCriteriaForm));
            this.label1 = new System.Windows.Forms.Label();
            this.cbxAllowNoMs2Id = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxMinDeconvolutionScore = new System.Windows.Forms.TextBox();
            this.checkedListBoxAcceptableIntegrationNotes = new System.Windows.Forms.CheckedListBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.tbxMinAuc = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tbxMinTurnoverScore = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(15, 11);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(481, 43);
            this.label1.TabIndex = 0;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // cbxAllowNoMs2Id
            // 
            this.cbxAllowNoMs2Id.AutoSize = true;
            this.cbxAllowNoMs2Id.Location = new System.Drawing.Point(19, 50);
            this.cbxAllowNoMs2Id.Name = "cbxAllowNoMs2Id";
            this.cbxAllowNoMs2Id.Size = new System.Drawing.Size(213, 17);
            this.cbxAllowNoMs2Id.TabIndex = 1;
            this.cbxAllowNoMs2Id.Text = "Accept samples which have no MS2 id.";
            this.cbxAllowNoMs2Id.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(18, 81);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(151, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Minimum Deconvolution Score";
            // 
            // tbxMinDeconvolutionScore
            // 
            this.tbxMinDeconvolutionScore.Location = new System.Drawing.Point(193, 76);
            this.tbxMinDeconvolutionScore.Name = "tbxMinDeconvolutionScore";
            this.tbxMinDeconvolutionScore.Size = new System.Drawing.Size(261, 20);
            this.tbxMinDeconvolutionScore.TabIndex = 3;
            // 
            // checkedListBoxAcceptableIntegrationNotes
            // 
            this.checkedListBoxAcceptableIntegrationNotes.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.checkedListBoxAcceptableIntegrationNotes.FormattingEnabled = true;
            this.checkedListBoxAcceptableIntegrationNotes.Location = new System.Drawing.Point(18, 228);
            this.checkedListBoxAcceptableIntegrationNotes.Name = "checkedListBoxAcceptableIntegrationNotes";
            this.checkedListBoxAcceptableIntegrationNotes.Size = new System.Drawing.Size(477, 94);
            this.checkedListBoxAcceptableIntegrationNotes.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.Location = new System.Drawing.Point(18, 170);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(488, 45);
            this.label3.TabIndex = 5;
            this.label3.Text = "When Topograph does peak integration, it sets the IntegrationNote to indicate if " +
                "there was any reason to suspect that the peak was not correctly found.  Which In" +
                "tegrationNotes are acceptable?";
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(330, 461);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 6;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.BtnSaveOnClick);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(421, 460);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(18, 110);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(207, 13);
            this.label4.TabIndex = 8;
            this.label4.Text = "Minimum Area Under Chromatogram Curve";
            // 
            // tbxMinAuc
            // 
            this.tbxMinAuc.Location = new System.Drawing.Point(240, 107);
            this.tbxMinAuc.Name = "tbxMinAuc";
            this.tbxMinAuc.Size = new System.Drawing.Size(214, 20);
            this.tbxMinAuc.TabIndex = 9;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(19, 135);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(125, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "Minimum Turnover Score";
            // 
            // tbxMinTurnoverScore
            // 
            this.tbxMinTurnoverScore.Location = new System.Drawing.Point(195, 134);
            this.tbxMinTurnoverScore.Name = "tbxMinTurnoverScore";
            this.tbxMinTurnoverScore.Size = new System.Drawing.Size(259, 20);
            this.tbxMinTurnoverScore.TabIndex = 11;
            // 
            // AcceptanceCriteriaForm
            // 
            this.AcceptButton = this.btnSave;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(508, 485);
            this.Controls.Add(this.tbxMinTurnoverScore);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.tbxMinAuc);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.checkedListBoxAcceptableIntegrationNotes);
            this.Controls.Add(this.tbxMinDeconvolutionScore);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.cbxAllowNoMs2Id);
            this.Controls.Add(this.label1);
            this.Name = "AcceptanceCriteriaForm";
            this.TabText = "AcceptanceCriteriaForm";
            this.Text = "AcceptanceCriteriaForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox cbxAllowNoMs2Id;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxMinDeconvolutionScore;
        private System.Windows.Forms.CheckedListBox checkedListBoxAcceptableIntegrationNotes;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbxMinAuc;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbxMinTurnoverScore;
    }
}