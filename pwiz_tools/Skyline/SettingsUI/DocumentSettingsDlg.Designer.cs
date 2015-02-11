namespace pwiz.Skyline.SettingsUI
{
    partial class DocumentSettingsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DocumentSettingsDlg));
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPageAnnotations = new System.Windows.Forms.TabPage();
            this.btnAddAnnotation = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.btnEditAnnotationList = new System.Windows.Forms.Button();
            this.checkedListBoxAnnotations = new System.Windows.Forms.CheckedListBox();
            this.tabPageGroupComparisons = new System.Windows.Forms.TabPage();
            this.btnAddGroupComparison = new System.Windows.Forms.Button();
            this.btnEditGroupComparisonList = new System.Windows.Forms.Button();
            this.checkedListBoxGroupComparisons = new System.Windows.Forms.CheckedListBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.tabControl.SuspendLayout();
            this.tabPageAnnotations.SuspendLayout();
            this.tabPageGroupComparisons.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.tabPageAnnotations);
            this.tabControl.Controls.Add(this.tabPageGroupComparisons);
            this.tabControl.Location = new System.Drawing.Point(12, 12);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(630, 286);
            this.tabControl.TabIndex = 0;
            // 
            // tabPageAnnotations
            // 
            this.tabPageAnnotations.Controls.Add(this.btnAddAnnotation);
            this.tabPageAnnotations.Controls.Add(this.label1);
            this.tabPageAnnotations.Controls.Add(this.btnEditAnnotationList);
            this.tabPageAnnotations.Controls.Add(this.checkedListBoxAnnotations);
            this.tabPageAnnotations.Location = new System.Drawing.Point(4, 22);
            this.tabPageAnnotations.Name = "tabPageAnnotations";
            this.tabPageAnnotations.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageAnnotations.Size = new System.Drawing.Size(622, 260);
            this.tabPageAnnotations.TabIndex = 0;
            this.tabPageAnnotations.Text = "Annotations";
            this.tabPageAnnotations.UseVisualStyleBackColor = true;
            // 
            // btnAddAnnotation
            // 
            this.btnAddAnnotation.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddAnnotation.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnAddAnnotation.Location = new System.Drawing.Point(541, 48);
            this.btnAddAnnotation.Name = "btnAddAnnotation";
            this.btnAddAnnotation.Size = new System.Drawing.Size(75, 23);
            this.btnAddAnnotation.TabIndex = 8;
            this.btnAddAnnotation.Text = "Add...";
            this.btnAddAnnotation.UseVisualStyleBackColor = true;
            this.btnAddAnnotation.Click += new System.EventHandler(this.btnAddAnnotation_Click);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label1.Location = new System.Drawing.Point(6, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(610, 42);
            this.label1.TabIndex = 7;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // btnEditAnnotationList
            // 
            this.btnEditAnnotationList.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnEditAnnotationList.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnEditAnnotationList.Location = new System.Drawing.Point(541, 77);
            this.btnEditAnnotationList.Name = "btnEditAnnotationList";
            this.btnEditAnnotationList.Size = new System.Drawing.Size(75, 23);
            this.btnEditAnnotationList.TabIndex = 6;
            this.btnEditAnnotationList.Text = "Edit List...";
            this.btnEditAnnotationList.UseVisualStyleBackColor = true;
            this.btnEditAnnotationList.Click += new System.EventHandler(this.btnEditAnnotationList_Click);
            // 
            // checkedListBoxAnnotations
            // 
            this.checkedListBoxAnnotations.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.checkedListBoxAnnotations.CheckOnClick = true;
            this.checkedListBoxAnnotations.FormattingEnabled = true;
            this.checkedListBoxAnnotations.Location = new System.Drawing.Point(9, 48);
            this.checkedListBoxAnnotations.Name = "checkedListBoxAnnotations";
            this.checkedListBoxAnnotations.Size = new System.Drawing.Size(526, 199);
            this.checkedListBoxAnnotations.TabIndex = 5;
            // 
            // tabPageGroupComparisons
            // 
            this.tabPageGroupComparisons.Controls.Add(this.btnAddGroupComparison);
            this.tabPageGroupComparisons.Controls.Add(this.btnEditGroupComparisonList);
            this.tabPageGroupComparisons.Controls.Add(this.checkedListBoxGroupComparisons);
            this.tabPageGroupComparisons.Location = new System.Drawing.Point(4, 22);
            this.tabPageGroupComparisons.Name = "tabPageGroupComparisons";
            this.tabPageGroupComparisons.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageGroupComparisons.Size = new System.Drawing.Size(622, 260);
            this.tabPageGroupComparisons.TabIndex = 1;
            this.tabPageGroupComparisons.Text = "Group Comparisons";
            this.tabPageGroupComparisons.UseVisualStyleBackColor = true;
            // 
            // btnAddGroupComparison
            // 
            this.btnAddGroupComparison.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddGroupComparison.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnAddGroupComparison.Location = new System.Drawing.Point(540, 31);
            this.btnAddGroupComparison.Name = "btnAddGroupComparison";
            this.btnAddGroupComparison.Size = new System.Drawing.Size(75, 23);
            this.btnAddGroupComparison.TabIndex = 11;
            this.btnAddGroupComparison.Text = "Add...";
            this.btnAddGroupComparison.UseVisualStyleBackColor = true;
            this.btnAddGroupComparison.Click += new System.EventHandler(this.btnAddGroupComparison_Click);
            // 
            // btnEditGroupComparisonList
            // 
            this.btnEditGroupComparisonList.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnEditGroupComparisonList.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnEditGroupComparisonList.Location = new System.Drawing.Point(540, 60);
            this.btnEditGroupComparisonList.Name = "btnEditGroupComparisonList";
            this.btnEditGroupComparisonList.Size = new System.Drawing.Size(75, 23);
            this.btnEditGroupComparisonList.TabIndex = 10;
            this.btnEditGroupComparisonList.Text = "Edit List...";
            this.btnEditGroupComparisonList.UseVisualStyleBackColor = true;
            this.btnEditGroupComparisonList.Click += new System.EventHandler(this.btnEditGroupComparisonList_Click);
            // 
            // checkedListBoxGroupComparisons
            // 
            this.checkedListBoxGroupComparisons.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.checkedListBoxGroupComparisons.CheckOnClick = true;
            this.checkedListBoxGroupComparisons.FormattingEnabled = true;
            this.checkedListBoxGroupComparisons.Location = new System.Drawing.Point(8, 31);
            this.checkedListBoxGroupComparisons.Name = "checkedListBoxGroupComparisons";
            this.checkedListBoxGroupComparisons.Size = new System.Drawing.Size(526, 199);
            this.checkedListBoxGroupComparisons.TabIndex = 9;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(563, 305);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOK.Location = new System.Drawing.Point(482, 305);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // DocumentSettingsDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(654, 340);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tabControl);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DocumentSettingsDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "Document Settings";
            this.tabControl.ResumeLayout(false);
            this.tabPageAnnotations.ResumeLayout(false);
            this.tabPageGroupComparisons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPageAnnotations;
        private System.Windows.Forms.TabPage tabPageGroupComparisons;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnEditAnnotationList;
        private System.Windows.Forms.CheckedListBox checkedListBoxAnnotations;
        private System.Windows.Forms.Button btnAddAnnotation;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnAddGroupComparison;
        private System.Windows.Forms.Button btnEditGroupComparisonList;
        private System.Windows.Forms.CheckedListBox checkedListBoxGroupComparisons;
    }
}