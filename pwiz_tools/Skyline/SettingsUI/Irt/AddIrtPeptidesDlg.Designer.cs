namespace pwiz.Skyline.SettingsUI.Irt
{
    partial class AddIrtPeptidesDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddIrtPeptidesDlg));
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.dataGridView = new pwiz.Common.Controls.CommonDataGridView();
            this.panelKeep = new System.Windows.Forms.Panel();
            this.labelKeep = new System.Windows.Forms.Label();
            this.listKeep = new System.Windows.Forms.ListBox();
            this.labelRunsFailed = new System.Windows.Forms.Label();
            this.labelRunsConverted = new System.Windows.Forms.Label();
            this.labelPeptidesAdded = new System.Windows.Forms.Label();
            this.panelOverwrite = new System.Windows.Forms.Panel();
            this.labelOverwrite = new System.Windows.Forms.Label();
            this.listOverwrite = new System.Windows.Forms.ListBox();
            this.panelExisting = new System.Windows.Forms.Panel();
            this.radioAverage = new System.Windows.Forms.RadioButton();
            this.radioReplace = new System.Windows.Forms.RadioButton();
            this.radioSkip = new System.Windows.Forms.RadioButton();
            this.labelChoice = new System.Windows.Forms.Label();
            this.labelExisting = new System.Windows.Forms.Label();
            this.listExisting = new System.Windows.Forms.ListBox();
            this.colFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Points = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Equation = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.R = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Result = new System.Windows.Forms.DataGridViewLinkColumn();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.panelKeep.SuspendLayout();
            this.panelOverwrite.SuspendLayout();
            this.panelExisting.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.AllowUserToResizeRows = false;
            resources.ApplyResources(this.dataGridView, "dataGridView");
            this.dataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colFile,
            this.Points,
            this.Equation,
            this.R,
            this.Result});
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            this.dataGridView.RowHeadersVisible = false;
            this.dataGridView.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_CellContentClick);
            // 
            // panelKeep
            // 
            resources.ApplyResources(this.panelKeep, "panelKeep");
            this.panelKeep.Controls.Add(this.labelKeep);
            this.panelKeep.Controls.Add(this.listKeep);
            this.panelKeep.Name = "panelKeep";
            // 
            // labelKeep
            // 
            resources.ApplyResources(this.labelKeep, "labelKeep");
            this.labelKeep.Name = "labelKeep";
            // 
            // listKeep
            // 
            resources.ApplyResources(this.listKeep, "listKeep");
            this.listKeep.FormattingEnabled = true;
            this.listKeep.Name = "listKeep";
            // 
            // labelRunsFailed
            // 
            resources.ApplyResources(this.labelRunsFailed, "labelRunsFailed");
            this.labelRunsFailed.Name = "labelRunsFailed";
            // 
            // labelRunsConverted
            // 
            resources.ApplyResources(this.labelRunsConverted, "labelRunsConverted");
            this.labelRunsConverted.Name = "labelRunsConverted";
            // 
            // labelPeptidesAdded
            // 
            resources.ApplyResources(this.labelPeptidesAdded, "labelPeptidesAdded");
            this.labelPeptidesAdded.Name = "labelPeptidesAdded";
            // 
            // panelOverwrite
            // 
            resources.ApplyResources(this.panelOverwrite, "panelOverwrite");
            this.panelOverwrite.Controls.Add(this.labelOverwrite);
            this.panelOverwrite.Controls.Add(this.listOverwrite);
            this.panelOverwrite.Name = "panelOverwrite";
            // 
            // labelOverwrite
            // 
            resources.ApplyResources(this.labelOverwrite, "labelOverwrite");
            this.labelOverwrite.Name = "labelOverwrite";
            // 
            // listOverwrite
            // 
            resources.ApplyResources(this.listOverwrite, "listOverwrite");
            this.listOverwrite.FormattingEnabled = true;
            this.listOverwrite.Name = "listOverwrite";
            // 
            // panelExisting
            // 
            resources.ApplyResources(this.panelExisting, "panelExisting");
            this.panelExisting.Controls.Add(this.radioAverage);
            this.panelExisting.Controls.Add(this.radioReplace);
            this.panelExisting.Controls.Add(this.radioSkip);
            this.panelExisting.Controls.Add(this.labelChoice);
            this.panelExisting.Controls.Add(this.labelExisting);
            this.panelExisting.Controls.Add(this.listExisting);
            this.panelExisting.Name = "panelExisting";
            // 
            // radioAverage
            // 
            resources.ApplyResources(this.radioAverage, "radioAverage");
            this.radioAverage.Name = "radioAverage";
            this.radioAverage.UseVisualStyleBackColor = true;
            // 
            // radioReplace
            // 
            resources.ApplyResources(this.radioReplace, "radioReplace");
            this.radioReplace.Name = "radioReplace";
            this.radioReplace.UseVisualStyleBackColor = true;
            // 
            // radioSkip
            // 
            resources.ApplyResources(this.radioSkip, "radioSkip");
            this.radioSkip.Checked = true;
            this.radioSkip.Name = "radioSkip";
            this.radioSkip.TabStop = true;
            this.radioSkip.UseVisualStyleBackColor = true;
            // 
            // labelChoice
            // 
            resources.ApplyResources(this.labelChoice, "labelChoice");
            this.labelChoice.Name = "labelChoice";
            // 
            // labelExisting
            // 
            resources.ApplyResources(this.labelExisting, "labelExisting");
            this.labelExisting.Name = "labelExisting";
            // 
            // listExisting
            // 
            resources.ApplyResources(this.listExisting, "listExisting");
            this.listExisting.FormattingEnabled = true;
            this.listExisting.Name = "listExisting";
            // 
            // colFile
            // 
            this.colFile.FillWeight = 62.28104F;
            resources.ApplyResources(this.colFile, "colFile");
            this.colFile.Name = "colFile";
            this.colFile.ReadOnly = true;
            // 
            // Points
            // 
            this.Points.FillWeight = 15F;
            resources.ApplyResources(this.Points, "Points");
            this.Points.Name = "Points";
            this.Points.ReadOnly = true;
            // 
            // Equation
            // 
            this.Equation.FillWeight = 69.51659F;
            resources.ApplyResources(this.Equation, "Equation");
            this.Equation.Name = "Equation";
            this.Equation.ReadOnly = true;
            // 
            // R
            // 
            this.R.FillWeight = 25F;
            resources.ApplyResources(this.R, "R");
            this.R.Name = "R";
            this.R.ReadOnly = true;
            // 
            // Result
            // 
            this.Result.FillWeight = 27.4633F;
            resources.ApplyResources(this.Result, "Result");
            this.Result.Name = "Result";
            this.Result.ReadOnly = true;
            // 
            // AddIrtPeptidesDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.dataGridView);
            this.Controls.Add(this.panelKeep);
            this.Controls.Add(this.labelRunsFailed);
            this.Controls.Add(this.labelRunsConverted);
            this.Controls.Add(this.labelPeptidesAdded);
            this.Controls.Add(this.panelOverwrite);
            this.Controls.Add(this.panelExisting);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AddIrtPeptidesDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.panelKeep.ResumeLayout(false);
            this.panelKeep.PerformLayout();
            this.panelOverwrite.ResumeLayout(false);
            this.panelOverwrite.PerformLayout();
            this.panelExisting.ResumeLayout(false);
            this.panelExisting.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox listExisting;
        private System.Windows.Forms.ListBox listOverwrite;
        private System.Windows.Forms.Label labelExisting;
        private System.Windows.Forms.Label labelChoice;
        private System.Windows.Forms.RadioButton radioSkip;
        private System.Windows.Forms.RadioButton radioReplace;
        private System.Windows.Forms.RadioButton radioAverage;
        private System.Windows.Forms.Label labelOverwrite;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Panel panelExisting;
        private System.Windows.Forms.Panel panelOverwrite;
        private System.Windows.Forms.Label labelPeptidesAdded;
        private System.Windows.Forms.Label labelRunsConverted;
        private System.Windows.Forms.Label labelRunsFailed;
        private System.Windows.Forms.Panel panelKeep;
        private System.Windows.Forms.Label labelKeep;
        private System.Windows.Forms.ListBox listKeep;
        private Common.Controls.CommonDataGridView dataGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn Points;
        private System.Windows.Forms.DataGridViewTextBoxColumn Equation;
        private System.Windows.Forms.DataGridViewTextBoxColumn R;
        private System.Windows.Forms.DataGridViewLinkColumn Result;
    }
}