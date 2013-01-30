namespace pwiz.Topograph.ui.Forms.Dashboard
{
    partial class SettingsStep
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
            this.lblModifications = new System.Windows.Forms.Label();
            this.btnEditModifications = new System.Windows.Forms.Button();
            this.lblLabels = new System.Windows.Forms.Label();
            this.btnDefineLabels = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.btnEditMisc = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblModifications
            // 
            this.lblModifications.AutoEllipsis = true;
            this.lblModifications.AutoSize = true;
            this.lblModifications.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblModifications.Location = new System.Drawing.Point(0, 0);
            this.lblModifications.Name = "lblModifications";
            this.lblModifications.Size = new System.Drawing.Size(238, 13);
            this.lblModifications.TabIndex = 1;
            this.lblModifications.Text = "No amino acid modifications have been specified";
            // 
            // btnEditModifications
            // 
            this.btnEditModifications.AutoSize = true;
            this.btnEditModifications.Location = new System.Drawing.Point(0, 0);
            this.btnEditModifications.Name = "btnEditModifications";
            this.btnEditModifications.Size = new System.Drawing.Size(109, 23);
            this.btnEditModifications.TabIndex = 2;
            this.btnEditModifications.Text = "Edit Modifications...";
            this.btnEditModifications.UseVisualStyleBackColor = true;
            this.btnEditModifications.Click += new System.EventHandler(this.BtnEditModificationsOnClick);
            // 
            // lblLabels
            // 
            this.lblLabels.AutoEllipsis = true;
            this.lblLabels.AutoSize = true;
            this.lblLabels.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblLabels.Location = new System.Drawing.Point(0, 39);
            this.lblLabels.Name = "lblLabels";
            this.lblLabels.Size = new System.Drawing.Size(307, 13);
            this.lblLabels.TabIndex = 3;
            this.lblLabels.Text = "No heavy isotope labels have been specified in this workspace.";
            // 
            // btnDefineLabels
            // 
            this.btnDefineLabels.AutoSize = true;
            this.btnDefineLabels.Location = new System.Drawing.Point(0, 0);
            this.btnDefineLabels.Name = "btnDefineLabels";
            this.btnDefineLabels.Size = new System.Drawing.Size(111, 23);
            this.btnDefineLabels.TabIndex = 4;
            this.btnDefineLabels.Text = "Define New Label...";
            this.btnDefineLabels.UseVisualStyleBackColor = true;
            this.btnDefineLabels.Click += new System.EventHandler(this.BtnDefineLabelsOnClick);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Top;
            this.label2.Location = new System.Drawing.Point(0, 78);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(658, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "There are other settings you might want to change about how the MS1 spectra are i" +
                "nterpreted and how the chromatograms are generated.";
            // 
            // btnEditMisc
            // 
            this.btnEditMisc.AutoSize = true;
            this.btnEditMisc.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnEditMisc.Location = new System.Drawing.Point(0, 0);
            this.btnEditMisc.Name = "btnEditMisc";
            this.btnEditMisc.Size = new System.Drawing.Size(155, 23);
            this.btnEditMisc.TabIndex = 6;
            this.btnEditMisc.Text = "Edit Miscellaneous Settings...";
            this.btnEditMisc.UseVisualStyleBackColor = true;
            this.btnEditMisc.Click += new System.EventHandler(this.BtnEditMiscOnClick);
            // 
            // panel1
            // 
            this.panel1.AutoSize = true;
            this.panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel1.Controls.Add(this.btnEditModifications);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 13);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(676, 26);
            this.panel1.TabIndex = 7;
            // 
            // panel2
            // 
            this.panel2.AutoSize = true;
            this.panel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel2.Controls.Add(this.btnDefineLabels);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 52);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(676, 26);
            this.panel2.TabIndex = 8;
            // 
            // panel3
            // 
            this.panel3.AutoSize = true;
            this.panel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel3.Controls.Add(this.btnEditMisc);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel3.Location = new System.Drawing.Point(0, 91);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(676, 26);
            this.panel3.TabIndex = 9;
            // 
            // SettingsStep
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.lblLabels);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.lblModifications);
            this.Name = "SettingsStep";
            this.Size = new System.Drawing.Size(676, 449);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblModifications;
        private System.Windows.Forms.Button btnEditModifications;
        private System.Windows.Forms.Label lblLabels;
        private System.Windows.Forms.Button btnDefineLabels;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnEditMisc;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel3;
    }
}
