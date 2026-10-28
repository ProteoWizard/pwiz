namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class OspreyLibraryModelControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OspreyLibraryModelControl));
            this.lblModel = new System.Windows.Forms.Label();
            this.comboModel = new System.Windows.Forms.ComboBox();
            this.cbUseGpu = new System.Windows.Forms.CheckBox();
            this.radioUseExistingModel = new System.Windows.Forms.RadioButton();
            this.radioTuneModel = new System.Windows.Forms.RadioButton();
            this.listTuningFiles = new System.Windows.Forms.CheckedListBox();
            this.btnBrowseTuningFiles = new System.Windows.Forms.Button();
            this.cbChooseFromSearchedFiles = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            //
            // lblModel
            //
            resources.ApplyResources(this.lblModel, "lblModel");
            this.lblModel.Name = "lblModel";
            //
            // comboModel
            //
            resources.ApplyResources(this.comboModel, "comboModel");
            this.comboModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboModel.FormattingEnabled = true;
            this.comboModel.Name = "comboModel";
            this.comboModel.SelectedIndexChanged += new System.EventHandler(this.comboModel_SelectedIndexChanged);
            //
            // cbUseGpu
            //
            resources.ApplyResources(this.cbUseGpu, "cbUseGpu");
            this.cbUseGpu.Name = "cbUseGpu";
            this.cbUseGpu.UseVisualStyleBackColor = true;
            this.cbUseGpu.CheckedChanged += new System.EventHandler(this.cbUseGpu_CheckedChanged);
            //
            // radioUseExistingModel
            //
            resources.ApplyResources(this.radioUseExistingModel, "radioUseExistingModel");
            this.radioUseExistingModel.Checked = true;
            this.radioUseExistingModel.Name = "radioUseExistingModel";
            this.radioUseExistingModel.TabStop = true;
            this.radioUseExistingModel.UseVisualStyleBackColor = true;
            this.radioUseExistingModel.CheckedChanged += new System.EventHandler(this.radioModel_CheckedChanged);
            //
            // radioTuneModel
            //
            resources.ApplyResources(this.radioTuneModel, "radioTuneModel");
            this.radioTuneModel.Name = "radioTuneModel";
            this.radioTuneModel.UseVisualStyleBackColor = true;
            this.radioTuneModel.CheckedChanged += new System.EventHandler(this.radioModel_CheckedChanged);
            //
            // listTuningFiles
            //
            resources.ApplyResources(this.listTuningFiles, "listTuningFiles");
            this.listTuningFiles.CheckOnClick = true;
            this.listTuningFiles.FormattingEnabled = true;
            this.listTuningFiles.Name = "listTuningFiles";
            //
            // btnBrowseTuningFiles
            //
            resources.ApplyResources(this.btnBrowseTuningFiles, "btnBrowseTuningFiles");
            this.btnBrowseTuningFiles.Name = "btnBrowseTuningFiles";
            this.btnBrowseTuningFiles.UseVisualStyleBackColor = true;
            this.btnBrowseTuningFiles.Click += new System.EventHandler(this.btnBrowseTuningFiles_Click);
            //
            // cbChooseFromSearchedFiles
            //
            resources.ApplyResources(this.cbChooseFromSearchedFiles, "cbChooseFromSearchedFiles");
            this.cbChooseFromSearchedFiles.Name = "cbChooseFromSearchedFiles";
            this.cbChooseFromSearchedFiles.UseVisualStyleBackColor = true;
            this.cbChooseFromSearchedFiles.CheckedChanged += new System.EventHandler(this.cbChooseFromSearchedFiles_CheckedChanged);
            //
            // OspreyLibraryModelControl
            //
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.cbChooseFromSearchedFiles);
            this.Controls.Add(this.btnBrowseTuningFiles);
            this.Controls.Add(this.listTuningFiles);
            this.Controls.Add(this.radioTuneModel);
            this.Controls.Add(this.radioUseExistingModel);
            this.Controls.Add(this.cbUseGpu);
            this.Controls.Add(this.comboModel);
            this.Controls.Add(this.lblModel);
            this.Name = "OspreyLibraryModelControl";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblModel;
        private System.Windows.Forms.ComboBox comboModel;
        private System.Windows.Forms.CheckBox cbUseGpu;
        private System.Windows.Forms.RadioButton radioUseExistingModel;
        private System.Windows.Forms.RadioButton radioTuneModel;
        private System.Windows.Forms.CheckedListBox listTuningFiles;
        private System.Windows.Forms.Button btnBrowseTuningFiles;
        private System.Windows.Forms.CheckBox cbChooseFromSearchedFiles;
    }
}
