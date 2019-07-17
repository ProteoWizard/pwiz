namespace pwiz.Skyline.FileUI
{
    partial class ExportAnnotationsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExportAnnotationsDlg));
            this.listBoxElementTypes = new System.Windows.Forms.ListBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lblChooseAnnotations = new System.Windows.Forms.Label();
            this.listBoxAnnotations = new System.Windows.Forms.ListBox();
            this.listBoxProperties = new System.Windows.Forms.ListBox();
            this.lblChooseProperties = new System.Windows.Forms.Label();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblElementTypes = new System.Windows.Forms.Label();
            this.cbxRemoveBlankRows = new System.Windows.Forms.CheckBox();
            this.lblInstructions = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // listBoxElementTypes
            // 
            this.listBoxElementTypes.FormattingEnabled = true;
            resources.ApplyResources(this.listBoxElementTypes, "listBoxElementTypes");
            this.listBoxElementTypes.Name = "listBoxElementTypes";
            this.listBoxElementTypes.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBoxElementTypes.SelectedIndexChanged += new System.EventHandler(this.checkedListBoxElementTypes_SelectedIndexChanged);
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.lblChooseAnnotations);
            this.splitContainer1.Panel1.Controls.Add(this.listBoxAnnotations);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.listBoxProperties);
            this.splitContainer1.Panel2.Controls.Add(this.lblChooseProperties);
            // 
            // lblChooseAnnotations
            // 
            resources.ApplyResources(this.lblChooseAnnotations, "lblChooseAnnotations");
            this.lblChooseAnnotations.Name = "lblChooseAnnotations";
            // 
            // listBoxAnnotations
            // 
            resources.ApplyResources(this.listBoxAnnotations, "listBoxAnnotations");
            this.listBoxAnnotations.FormattingEnabled = true;
            this.listBoxAnnotations.Name = "listBoxAnnotations";
            this.listBoxAnnotations.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBoxAnnotations.SelectedIndexChanged += new System.EventHandler(this.ListBoxSelectionChanged);
            // 
            // listBoxProperties
            // 
            resources.ApplyResources(this.listBoxProperties, "listBoxProperties");
            this.listBoxProperties.FormattingEnabled = true;
            this.listBoxProperties.Name = "listBoxProperties";
            this.listBoxProperties.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBoxProperties.SelectedIndexChanged += new System.EventHandler(this.ListBoxSelectionChanged);
            // 
            // lblChooseProperties
            // 
            resources.ApplyResources(this.lblChooseProperties, "lblChooseProperties");
            this.lblChooseProperties.Name = "lblChooseProperties";
            // 
            // btnExport
            // 
            resources.ApplyResources(this.btnExport, "btnExport");
            this.btnExport.Name = "btnExport";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // lblElementTypes
            // 
            resources.ApplyResources(this.lblElementTypes, "lblElementTypes");
            this.lblElementTypes.Name = "lblElementTypes";
            // 
            // cbxRemoveBlankRows
            // 
            resources.ApplyResources(this.cbxRemoveBlankRows, "cbxRemoveBlankRows");
            this.cbxRemoveBlankRows.Name = "cbxRemoveBlankRows";
            this.cbxRemoveBlankRows.UseVisualStyleBackColor = true;
            // 
            // lblInstructions
            // 
            resources.ApplyResources(this.lblInstructions, "lblInstructions");
            this.lblInstructions.AutoEllipsis = true;
            this.lblInstructions.Name = "lblInstructions";
            // 
            // ExportAnnotationsDlg
            // 
            this.AcceptButton = this.btnExport;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lblInstructions);
            this.Controls.Add(this.cbxRemoveBlankRows);
            this.Controls.Add(this.lblElementTypes);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnExport);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.listBoxElementTypes);
            this.Name = "ExportAnnotationsDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox listBoxElementTypes;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox listBoxAnnotations;
        private System.Windows.Forms.Label lblChooseAnnotations;
        private System.Windows.Forms.Label lblChooseProperties;
        private System.Windows.Forms.ListBox listBoxProperties;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblElementTypes;
        private System.Windows.Forms.CheckBox cbxRemoveBlankRows;
        private System.Windows.Forms.Label lblInstructions;
    }
}