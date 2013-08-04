namespace pwiz.Skyline.FileUI
{
    partial class ExportChromatogramDlg
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
            this.checkedListVars = new System.Windows.Forms.CheckedListBox();
            this.label2 = new System.Windows.Forms.Label();
            this.boxCheckAll = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.checkBoxPrecursors = new System.Windows.Forms.CheckBox();
            this.checkBoxProducts = new System.Windows.Forms.CheckBox();
            this.checkBoxTic = new System.Windows.Forms.CheckBox();
            this.checkBoxBasePeak = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(273, 42);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOk.Location = new System.Drawing.Point(273, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 8;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // checkedListVars
            // 
            this.checkedListVars.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.checkedListVars.CheckOnClick = true;
            this.checkedListVars.FormattingEnabled = true;
            this.checkedListVars.Location = new System.Drawing.Point(15, 33);
            this.checkedListVars.Name = "checkedListVars";
            this.checkedListVars.Size = new System.Drawing.Size(226, 229);
            this.checkedListVars.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 17);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(80, 13);
            this.label2.TabIndex = 0;
            this.label2.Text = "&Files To Export:";
            // 
            // boxCheckAll
            // 
            this.boxCheckAll.AutoSize = true;
            this.boxCheckAll.Location = new System.Drawing.Point(15, 268);
            this.boxCheckAll.Name = "boxCheckAll";
            this.boxCheckAll.Size = new System.Drawing.Size(70, 17);
            this.boxCheckAll.TabIndex = 2;
            this.boxCheckAll.Text = "Select All";
            this.boxCheckAll.UseVisualStyleBackColor = true;
            this.boxCheckAll.CheckedChanged += new System.EventHandler(this.checkAll_clicked);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 302);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(45, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Include:";
            // 
            // checkBoxPrecursors
            // 
            this.checkBoxPrecursors.AutoSize = true;
            this.checkBoxPrecursors.Location = new System.Drawing.Point(12, 327);
            this.checkBoxPrecursors.Name = "checkBoxPrecursors";
            this.checkBoxPrecursors.Size = new System.Drawing.Size(76, 17);
            this.checkBoxPrecursors.TabIndex = 4;
            this.checkBoxPrecursors.Text = "Precursors";
            this.checkBoxPrecursors.UseVisualStyleBackColor = true;
            this.checkBoxPrecursors.CheckedChanged += new System.EventHandler(this.checkBoxPrecursors_CheckedChanged);
            // 
            // checkBoxProducts
            // 
            this.checkBoxProducts.AutoSize = true;
            this.checkBoxProducts.Location = new System.Drawing.Point(12, 350);
            this.checkBoxProducts.Name = "checkBoxProducts";
            this.checkBoxProducts.Size = new System.Drawing.Size(68, 17);
            this.checkBoxProducts.TabIndex = 5;
            this.checkBoxProducts.Text = "Products";
            this.checkBoxProducts.UseVisualStyleBackColor = true;
            this.checkBoxProducts.CheckedChanged += new System.EventHandler(this.checkBoxProducts_CheckedChanged);
            // 
            // checkBoxTic
            // 
            this.checkBoxTic.AutoSize = true;
            this.checkBoxTic.Location = new System.Drawing.Point(114, 350);
            this.checkBoxTic.Name = "checkBoxTic";
            this.checkBoxTic.Size = new System.Drawing.Size(48, 17);
            this.checkBoxTic.TabIndex = 7;
            this.checkBoxTic.Text = "TICs";
            this.checkBoxTic.UseVisualStyleBackColor = true;
            this.checkBoxTic.CheckedChanged += new System.EventHandler(this.checkBoxTic_CheckedChanged);
            // 
            // checkBoxBasePeak
            // 
            this.checkBoxBasePeak.AutoSize = true;
            this.checkBoxBasePeak.Location = new System.Drawing.Point(114, 327);
            this.checkBoxBasePeak.Name = "checkBoxBasePeak";
            this.checkBoxBasePeak.Size = new System.Drawing.Size(83, 17);
            this.checkBoxBasePeak.TabIndex = 6;
            this.checkBoxBasePeak.Text = "Base Peaks";
            this.checkBoxBasePeak.UseVisualStyleBackColor = true;
            this.checkBoxBasePeak.CheckedChanged += new System.EventHandler(this.checkBoxBasePeak_CheckedChanged);
            // 
            // ExportChromatogramDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(360, 403);
            this.Controls.Add(this.checkBoxBasePeak);
            this.Controls.Add(this.checkBoxTic);
            this.Controls.Add(this.checkBoxProducts);
            this.Controls.Add(this.checkBoxPrecursors);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.boxCheckAll);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.checkedListVars);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExportChromatogramDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Export Chromatograms";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.CheckedListBox checkedListVars;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox boxCheckAll;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox checkBoxPrecursors;
        private System.Windows.Forms.CheckBox checkBoxProducts;
        private System.Windows.Forms.CheckBox checkBoxTic;
        private System.Windows.Forms.CheckBox checkBoxBasePeak;
    }
}