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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExportChromatogramDlg));
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
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // checkedListVars
            // 
            resources.ApplyResources(this.checkedListVars, "checkedListVars");
            this.checkedListVars.CheckOnClick = true;
            this.checkedListVars.FormattingEnabled = true;
            this.checkedListVars.Name = "checkedListVars";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // boxCheckAll
            // 
            resources.ApplyResources(this.boxCheckAll, "boxCheckAll");
            this.boxCheckAll.Name = "boxCheckAll";
            this.boxCheckAll.UseVisualStyleBackColor = true;
            this.boxCheckAll.CheckedChanged += new System.EventHandler(this.checkAll_clicked);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // checkBoxPrecursors
            // 
            resources.ApplyResources(this.checkBoxPrecursors, "checkBoxPrecursors");
            this.checkBoxPrecursors.Name = "checkBoxPrecursors";
            this.checkBoxPrecursors.UseVisualStyleBackColor = true;
            this.checkBoxPrecursors.CheckedChanged += new System.EventHandler(this.checkBoxPrecursors_CheckedChanged);
            // 
            // checkBoxProducts
            // 
            resources.ApplyResources(this.checkBoxProducts, "checkBoxProducts");
            this.checkBoxProducts.Name = "checkBoxProducts";
            this.checkBoxProducts.UseVisualStyleBackColor = true;
            this.checkBoxProducts.CheckedChanged += new System.EventHandler(this.checkBoxProducts_CheckedChanged);
            // 
            // checkBoxTic
            // 
            resources.ApplyResources(this.checkBoxTic, "checkBoxTic");
            this.checkBoxTic.Name = "checkBoxTic";
            this.checkBoxTic.UseVisualStyleBackColor = true;
            this.checkBoxTic.CheckedChanged += new System.EventHandler(this.checkBoxTic_CheckedChanged);
            // 
            // checkBoxBasePeak
            // 
            resources.ApplyResources(this.checkBoxBasePeak, "checkBoxBasePeak");
            this.checkBoxBasePeak.Name = "checkBoxBasePeak";
            this.checkBoxBasePeak.UseVisualStyleBackColor = true;
            this.checkBoxBasePeak.CheckedChanged += new System.EventHandler(this.checkBoxBasePeak_CheckedChanged);
            // 
            // ExportChromatogramDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
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