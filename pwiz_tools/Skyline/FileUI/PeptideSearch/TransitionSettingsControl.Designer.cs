namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class TransitionSettingsControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblPrecursorCharges = new System.Windows.Forms.Label();
            this.txtPrecursorCharges = new System.Windows.Forms.TextBox();
            this.lblIonCharges = new System.Windows.Forms.Label();
            this.txtIonCharges = new System.Windows.Forms.TextBox();
            this.lblIonTypes = new System.Windows.Forms.Label();
            this.txtIonTypes = new System.Windows.Forms.TextBox();
            this.lblTolerance = new System.Windows.Forms.Label();
            this.txtTolerance = new System.Windows.Forms.TextBox();
            this.lblToleranceUnits = new System.Windows.Forms.Label();
            this.lblIonCount = new System.Windows.Forms.Label();
            this.lblIonCountUnits = new System.Windows.Forms.Label();
            this.txtIonCount = new System.Windows.Forms.TextBox();
            this.cbExclusionUseDIAWindow = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // lblPrecursorCharges
            // 
            this.lblPrecursorCharges.AutoSize = true;
            this.lblPrecursorCharges.Location = new System.Drawing.Point(14, 3);
            this.lblPrecursorCharges.Name = "lblPrecursorCharges";
            this.lblPrecursorCharges.Size = new System.Drawing.Size(96, 13);
            this.lblPrecursorCharges.TabIndex = 0;
            this.lblPrecursorCharges.Text = "&Precursor charges:";
            // 
            // txtPrecursorCharges
            // 
            this.txtPrecursorCharges.Location = new System.Drawing.Point(17, 19);
            this.txtPrecursorCharges.Name = "txtPrecursorCharges";
            this.txtPrecursorCharges.Size = new System.Drawing.Size(76, 20);
            this.txtPrecursorCharges.TabIndex = 1;
            // 
            // lblIonCharges
            // 
            this.lblIonCharges.AutoSize = true;
            this.lblIonCharges.Location = new System.Drawing.Point(141, 3);
            this.lblIonCharges.Name = "lblIonCharges";
            this.lblIonCharges.Size = new System.Drawing.Size(66, 13);
            this.lblIonCharges.TabIndex = 2;
            this.lblIonCharges.Text = "&Ion charges:";
            // 
            // txtIonCharges
            // 
            this.txtIonCharges.Location = new System.Drawing.Point(144, 19);
            this.txtIonCharges.Name = "txtIonCharges";
            this.txtIonCharges.Size = new System.Drawing.Size(76, 20);
            this.txtIonCharges.TabIndex = 3;
            // 
            // lblIonTypes
            // 
            this.lblIonTypes.AutoSize = true;
            this.lblIonTypes.Location = new System.Drawing.Point(259, 3);
            this.lblIonTypes.Name = "lblIonTypes";
            this.lblIonTypes.Size = new System.Drawing.Size(53, 13);
            this.lblIonTypes.TabIndex = 4;
            this.lblIonTypes.Text = "Ion &types:";
            // 
            // txtIonTypes
            // 
            this.txtIonTypes.Location = new System.Drawing.Point(262, 19);
            this.txtIonTypes.Name = "txtIonTypes";
            this.txtIonTypes.Size = new System.Drawing.Size(76, 20);
            this.txtIonTypes.TabIndex = 5;
            // 
            // lblTolerance
            // 
            this.lblTolerance.AutoSize = true;
            this.lblTolerance.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblTolerance.Location = new System.Drawing.Point(14, 102);
            this.lblTolerance.Name = "lblTolerance";
            this.lblTolerance.Size = new System.Drawing.Size(104, 13);
            this.lblTolerance.TabIndex = 7;
            this.lblTolerance.Text = "Ion &match tolerance:";
            // 
            // txtTolerance
            // 
            this.txtTolerance.Location = new System.Drawing.Point(17, 118);
            this.txtTolerance.Name = "txtTolerance";
            this.txtTolerance.Size = new System.Drawing.Size(76, 20);
            this.txtTolerance.TabIndex = 8;
            // 
            // lblToleranceUnits
            // 
            this.lblToleranceUnits.AutoSize = true;
            this.lblToleranceUnits.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Italic);
            this.lblToleranceUnits.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblToleranceUnits.Location = new System.Drawing.Point(99, 121);
            this.lblToleranceUnits.Name = "lblToleranceUnits";
            this.lblToleranceUnits.Size = new System.Drawing.Size(25, 13);
            this.lblToleranceUnits.TabIndex = 9;
            this.lblToleranceUnits.Text = "m/z";
            // 
            // lblIonCount
            // 
            this.lblIonCount.AutoSize = true;
            this.lblIonCount.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblIonCount.Location = new System.Drawing.Point(141, 102);
            this.lblIonCount.Name = "lblIonCount";
            this.lblIonCount.Size = new System.Drawing.Size(31, 13);
            this.lblIonCount.TabIndex = 10;
            this.lblIonCount.Text = "&Pick:";
            // 
            // lblIonCountUnits
            // 
            this.lblIonCountUnits.AutoSize = true;
            this.lblIonCountUnits.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblIonCountUnits.Location = new System.Drawing.Point(226, 121);
            this.lblIonCountUnits.Name = "lblIonCountUnits";
            this.lblIonCountUnits.Size = new System.Drawing.Size(65, 13);
            this.lblIonCountUnits.TabIndex = 12;
            this.lblIonCountUnits.Text = "product ions";
            // 
            // txtIonCount
            // 
            this.txtIonCount.Location = new System.Drawing.Point(144, 118);
            this.txtIonCount.Name = "txtIonCount";
            this.txtIonCount.Size = new System.Drawing.Size(76, 20);
            this.txtIonCount.TabIndex = 11;
            // 
            // cbExclusionUseDIAWindow
            // 
            this.cbExclusionUseDIAWindow.AutoSize = true;
            this.cbExclusionUseDIAWindow.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.cbExclusionUseDIAWindow.Location = new System.Drawing.Point(17, 62);
            this.cbExclusionUseDIAWindow.Name = "cbExclusionUseDIAWindow";
            this.cbExclusionUseDIAWindow.Size = new System.Drawing.Size(214, 17);
            this.cbExclusionUseDIAWindow.TabIndex = 6;
            this.cbExclusionUseDIAWindow.Text = "&Use DIA precursor window for exclusion";
            this.cbExclusionUseDIAWindow.UseVisualStyleBackColor = true;
            // 
            // TransitionSettingsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.cbExclusionUseDIAWindow);
            this.Controls.Add(this.lblTolerance);
            this.Controls.Add(this.txtTolerance);
            this.Controls.Add(this.lblToleranceUnits);
            this.Controls.Add(this.lblIonCount);
            this.Controls.Add(this.lblIonCountUnits);
            this.Controls.Add(this.txtIonCount);
            this.Controls.Add(this.txtIonTypes);
            this.Controls.Add(this.lblIonTypes);
            this.Controls.Add(this.txtIonCharges);
            this.Controls.Add(this.lblIonCharges);
            this.Controls.Add(this.txtPrecursorCharges);
            this.Controls.Add(this.lblPrecursorCharges);
            this.Name = "TransitionSettingsControl";
            this.Size = new System.Drawing.Size(363, 425);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtPrecursorCharges;
        private System.Windows.Forms.Label lblPrecursorCharges;
        private System.Windows.Forms.Label lblIonCharges;
        private System.Windows.Forms.TextBox txtIonCharges;
        private System.Windows.Forms.Label lblIonTypes;
        private System.Windows.Forms.TextBox txtIonTypes;
        private System.Windows.Forms.Label lblTolerance;
        private System.Windows.Forms.TextBox txtTolerance;
        private System.Windows.Forms.Label lblToleranceUnits;
        private System.Windows.Forms.Label lblIonCount;
        private System.Windows.Forms.Label lblIonCountUnits;
        private System.Windows.Forms.TextBox txtIonCount;
        private System.Windows.Forms.CheckBox cbExclusionUseDIAWindow;
    }
}
