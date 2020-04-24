namespace pwiz.Skyline.SettingsUI
{
    partial class EditCrosslinkerDlg
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
            this.btnOk = new System.Windows.Forms.Button();
            this.cbxIsCrosslinker = new System.Windows.Forms.CheckBox();
            this.panelFormula = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(322, 139);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOk.Location = new System.Drawing.Point(241, 139);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 2;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // cbxIsCrosslinker
            // 
            this.cbxIsCrosslinker.AutoSize = true;
            this.cbxIsCrosslinker.Checked = true;
            this.cbxIsCrosslinker.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbxIsCrosslinker.Location = new System.Drawing.Point(12, 10);
            this.cbxIsCrosslinker.Name = "cbxIsCrosslinker";
            this.cbxIsCrosslinker.Size = new System.Drawing.Size(286, 17);
            this.cbxIsCrosslinker.TabIndex = 0;
            this.cbxIsCrosslinker.Text = "This modification can be crosslinked to another peptide";
            this.cbxIsCrosslinker.UseVisualStyleBackColor = true;
            this.cbxIsCrosslinker.CheckedChanged += new System.EventHandler(this.cbxIsCrosslinker_CheckedChanged);
            // 
            // panelFormula
            // 
            this.panelFormula.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelFormula.Location = new System.Drawing.Point(13, 33);
            this.panelFormula.Name = "panelFormula";
            this.panelFormula.Size = new System.Drawing.Size(384, 100);
            this.panelFormula.TabIndex = 1;
            // 
            // EditCrosslinkerDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(411, 174);
            this.Controls.Add(this.panelFormula);
            this.Controls.Add(this.cbxIsCrosslinker);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Name = "EditCrosslinkerDlg";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Crosslinker";
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.CheckBox cbxIsCrosslinker;
        private System.Windows.Forms.Panel panelFormula;
    }
}