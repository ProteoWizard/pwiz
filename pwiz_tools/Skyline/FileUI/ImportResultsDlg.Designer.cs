namespace pwiz.Skyline.FileUI
{
    partial class ImportResultsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportResultsDlg));
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.radioCreateMultipleMulti = new System.Windows.Forms.RadioButton();
            this.radioCreateMultiple = new System.Windows.Forms.RadioButton();
            this.radioAddExisting = new System.Windows.Forms.RadioButton();
            this.radioCreateNew = new System.Windows.Forms.RadioButton();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.labelTuning = new System.Windows.Forms.Label();
            this.comboTuning = new System.Windows.Forms.ComboBox();
            this.cbShowAllChromatograms = new System.Windows.Forms.CheckBox();
            this.labelOptimizing = new System.Windows.Forms.Label();
            this.comboOptimizing = new System.Windows.Forms.ComboBox();
            this.comboName = new System.Windows.Forms.ComboBox();
            this.labelNameAdd = new System.Windows.Forms.Label();
            this.textName = new System.Windows.Forms.TextBox();
            this.labelNameNew = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 15000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // radioCreateMultipleMulti
            // 
            resources.ApplyResources(this.radioCreateMultipleMulti, "radioCreateMultipleMulti");
            this.radioCreateMultipleMulti.Name = "radioCreateMultipleMulti";
            this.radioCreateMultipleMulti.TabStop = true;
            this.helpTip.SetToolTip(this.radioCreateMultipleMulti, resources.GetString("radioCreateMultipleMulti.ToolTip"));
            this.radioCreateMultipleMulti.UseVisualStyleBackColor = true;
            this.radioCreateMultipleMulti.CheckedChanged += new System.EventHandler(this.radioCreateMultipleMulti_CheckedChanged);
            // 
            // radioCreateMultiple
            // 
            resources.ApplyResources(this.radioCreateMultiple, "radioCreateMultiple");
            this.radioCreateMultiple.Checked = true;
            this.radioCreateMultiple.Name = "radioCreateMultiple";
            this.radioCreateMultiple.TabStop = true;
            this.helpTip.SetToolTip(this.radioCreateMultiple, resources.GetString("radioCreateMultiple.ToolTip"));
            this.radioCreateMultiple.UseVisualStyleBackColor = true;
            this.radioCreateMultiple.CheckedChanged += new System.EventHandler(this.radioCreateMultiple_CheckedChanged);
            // 
            // radioAddExisting
            // 
            resources.ApplyResources(this.radioAddExisting, "radioAddExisting");
            this.radioAddExisting.Name = "radioAddExisting";
            this.helpTip.SetToolTip(this.radioAddExisting, resources.GetString("radioAddExisting.ToolTip"));
            this.radioAddExisting.UseVisualStyleBackColor = true;
            this.radioAddExisting.CheckedChanged += new System.EventHandler(this.radioAddExisting_CheckedChanged);
            // 
            // radioCreateNew
            // 
            resources.ApplyResources(this.radioCreateNew, "radioCreateNew");
            this.radioCreateNew.Name = "radioCreateNew";
            this.helpTip.SetToolTip(this.radioCreateNew, resources.GetString("radioCreateNew.ToolTip"));
            this.radioCreateNew.UseVisualStyleBackColor = true;
            this.radioCreateNew.CheckedChanged += new System.EventHandler(this.radioCreateNew_CheckedChanged);
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // labelTuning
            // 
            resources.ApplyResources(this.labelTuning, "labelTuning");
            this.labelTuning.Name = "labelTuning";
            // 
            // comboTuning
            // 
            this.comboTuning.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTuning.FormattingEnabled = true;
            resources.ApplyResources(this.comboTuning, "comboTuning");
            this.comboTuning.Name = "comboTuning";
            // 
            // cbShowAllChromatograms
            // 
            resources.ApplyResources(this.cbShowAllChromatograms, "cbShowAllChromatograms");
            this.cbShowAllChromatograms.Name = "cbShowAllChromatograms";
            this.cbShowAllChromatograms.UseVisualStyleBackColor = true;
            this.cbShowAllChromatograms.CheckedChanged += new System.EventHandler(this.cbShowAllChromatograms_CheckedChanged);
            // 
            // labelOptimizing
            // 
            resources.ApplyResources(this.labelOptimizing, "labelOptimizing");
            this.labelOptimizing.Name = "labelOptimizing";
            // 
            // comboOptimizing
            // 
            this.comboOptimizing.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOptimizing.FormattingEnabled = true;
            resources.ApplyResources(this.comboOptimizing, "comboOptimizing");
            this.comboOptimizing.Name = "comboOptimizing";
            this.comboOptimizing.SelectedIndexChanged += new System.EventHandler(this.comboOptimizing_SelectedIndexChanged);
            // 
            // comboName
            // 
            this.comboName.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboName, "comboName");
            this.comboName.FormattingEnabled = true;
            this.comboName.Name = "comboName";
            // 
            // labelNameAdd
            // 
            resources.ApplyResources(this.labelNameAdd, "labelNameAdd");
            this.labelNameAdd.Name = "labelNameAdd";
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            // 
            // labelNameNew
            // 
            resources.ApplyResources(this.labelNameNew, "labelNameNew");
            this.labelNameNew.Name = "labelNameNew";
            // 
            // ImportResultsDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.labelTuning);
            this.Controls.Add(this.comboTuning);
            this.Controls.Add(this.cbShowAllChromatograms);
            this.Controls.Add(this.labelOptimizing);
            this.Controls.Add(this.comboOptimizing);
            this.Controls.Add(this.radioCreateMultipleMulti);
            this.Controls.Add(this.radioCreateMultiple);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.comboName);
            this.Controls.Add(this.labelNameAdd);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.labelNameNew);
            this.Controls.Add(this.radioAddExisting);
            this.Controls.Add(this.radioCreateNew);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportResultsDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RadioButton radioCreateNew;
        private System.Windows.Forms.RadioButton radioAddExisting;
        private System.Windows.Forms.Label labelNameNew;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label labelNameAdd;
        private System.Windows.Forms.ComboBox comboName;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.RadioButton radioCreateMultiple;
        private System.Windows.Forms.RadioButton radioCreateMultipleMulti;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.ComboBox comboOptimizing;
        private System.Windows.Forms.Label labelOptimizing;
        private System.Windows.Forms.CheckBox cbShowAllChromatograms;
        private System.Windows.Forms.Label labelTuning;
        private System.Windows.Forms.ComboBox comboTuning;
    }
}