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
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // listBoxElementTypes
            // 
            this.listBoxElementTypes.FormattingEnabled = true;
            this.listBoxElementTypes.Location = new System.Drawing.Point(4, 52);
            this.listBoxElementTypes.Name = "listBoxElementTypes";
            this.listBoxElementTypes.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBoxElementTypes.Size = new System.Drawing.Size(211, 121);
            this.listBoxElementTypes.TabIndex = 2;
            this.listBoxElementTypes.SelectedIndexChanged += new System.EventHandler(this.checkedListBoxElementTypes_SelectedIndexChanged);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(4, 179);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
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
            this.splitContainer1.Size = new System.Drawing.Size(524, 283);
            this.splitContainer1.SplitterDistance = 140;
            this.splitContainer1.TabIndex = 3;
            // 
            // lblChooseAnnotations
            // 
            this.lblChooseAnnotations.AutoSize = true;
            this.lblChooseAnnotations.Location = new System.Drawing.Point(3, 5);
            this.lblChooseAnnotations.Name = "lblChooseAnnotations";
            this.lblChooseAnnotations.Size = new System.Drawing.Size(127, 13);
            this.lblChooseAnnotations.TabIndex = 0;
            this.lblChooseAnnotations.Text = "Export these annotations:";
            // 
            // listBoxAnnotations
            // 
            this.listBoxAnnotations.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxAnnotations.FormattingEnabled = true;
            this.listBoxAnnotations.Location = new System.Drawing.Point(3, 21);
            this.listBoxAnnotations.Name = "listBoxAnnotations";
            this.listBoxAnnotations.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBoxAnnotations.Size = new System.Drawing.Size(518, 95);
            this.listBoxAnnotations.TabIndex = 1;
            this.listBoxAnnotations.SelectedIndexChanged += new System.EventHandler(this.ListBoxSelectionChanged);
            // 
            // listBoxProperties
            // 
            this.listBoxProperties.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxProperties.FormattingEnabled = true;
            this.listBoxProperties.Location = new System.Drawing.Point(3, 16);
            this.listBoxProperties.Name = "listBoxProperties";
            this.listBoxProperties.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBoxProperties.Size = new System.Drawing.Size(518, 95);
            this.listBoxProperties.TabIndex = 1;
            this.listBoxProperties.SelectedIndexChanged += new System.EventHandler(this.ListBoxSelectionChanged);
            // 
            // lblChooseProperties
            // 
            this.lblChooseProperties.AutoSize = true;
            this.lblChooseProperties.Location = new System.Drawing.Point(3, 0);
            this.lblChooseProperties.Name = "lblChooseProperties";
            this.lblChooseProperties.Size = new System.Drawing.Size(145, 13);
            this.lblChooseProperties.TabIndex = 0;
            this.lblChooseProperties.Text = "Export these other properties:";
            // 
            // btnExport
            // 
            this.btnExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExport.Location = new System.Drawing.Point(362, 481);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(75, 23);
            this.btnExport.TabIndex = 5;
            this.btnExport.Text = "Export...";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(443, 481);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // lblElementTypes
            // 
            this.lblElementTypes.AutoSize = true;
            this.lblElementTypes.Location = new System.Drawing.Point(4, 36);
            this.lblElementTypes.Name = "lblElementTypes";
            this.lblElementTypes.Size = new System.Drawing.Size(146, 13);
            this.lblElementTypes.TabIndex = 1;
            this.lblElementTypes.Text = "Export these types of objects:";
            // 
            // cbxRemoveBlankRows
            // 
            this.cbxRemoveBlankRows.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbxRemoveBlankRows.AutoSize = true;
            this.cbxRemoveBlankRows.Location = new System.Drawing.Point(4, 468);
            this.cbxRemoveBlankRows.Name = "cbxRemoveBlankRows";
            this.cbxRemoveBlankRows.Size = new System.Drawing.Size(120, 17);
            this.cbxRemoveBlankRows.TabIndex = 4;
            this.cbxRemoveBlankRows.Text = "Remove blank rows";
            this.cbxRemoveBlankRows.UseVisualStyleBackColor = true;
            // 
            // lblInstructions
            // 
            this.lblInstructions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblInstructions.AutoEllipsis = true;
            this.lblInstructions.Location = new System.Drawing.Point(7, 7);
            this.lblInstructions.Name = "lblInstructions";
            this.lblInstructions.Size = new System.Drawing.Size(511, 23);
            this.lblInstructions.TabIndex = 0;
            this.lblInstructions.Text = "Choose which annotations and properties that you would like to export to a text f" +
    "ile.";
            // 
            // ExportAnnotationsDlg
            // 
            this.AcceptButton = this.btnExport;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(530, 516);
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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Export Annotations and Properties";
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