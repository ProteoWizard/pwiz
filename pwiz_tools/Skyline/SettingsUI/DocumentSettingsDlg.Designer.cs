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
            this.tabPageReports = new System.Windows.Forms.TabPage();
            this.labelReports = new System.Windows.Forms.Label();
            this.chooseViewsControl = new pwiz.Common.DataBinding.Controls.Editor.ChooseViewsControl();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.tabControl.SuspendLayout();
            this.tabPageAnnotations.SuspendLayout();
            this.tabPageGroupComparisons.SuspendLayout();
            this.tabPageReports.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            resources.ApplyResources(this.tabControl, "tabControl");
            this.tabControl.Controls.Add(this.tabPageAnnotations);
            this.tabControl.Controls.Add(this.tabPageGroupComparisons);
            this.tabControl.Controls.Add(this.tabPageReports);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            // 
            // tabPageAnnotations
            // 
            this.tabPageAnnotations.Controls.Add(this.btnAddAnnotation);
            this.tabPageAnnotations.Controls.Add(this.label1);
            this.tabPageAnnotations.Controls.Add(this.btnEditAnnotationList);
            this.tabPageAnnotations.Controls.Add(this.checkedListBoxAnnotations);
            resources.ApplyResources(this.tabPageAnnotations, "tabPageAnnotations");
            this.tabPageAnnotations.Name = "tabPageAnnotations";
            this.tabPageAnnotations.UseVisualStyleBackColor = true;
            // 
            // btnAddAnnotation
            // 
            resources.ApplyResources(this.btnAddAnnotation, "btnAddAnnotation");
            this.btnAddAnnotation.Name = "btnAddAnnotation";
            this.btnAddAnnotation.UseVisualStyleBackColor = true;
            this.btnAddAnnotation.Click += new System.EventHandler(this.btnAddAnnotation_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // btnEditAnnotationList
            // 
            resources.ApplyResources(this.btnEditAnnotationList, "btnEditAnnotationList");
            this.btnEditAnnotationList.Name = "btnEditAnnotationList";
            this.btnEditAnnotationList.UseVisualStyleBackColor = true;
            this.btnEditAnnotationList.Click += new System.EventHandler(this.btnEditAnnotationList_Click);
            // 
            // checkedListBoxAnnotations
            // 
            resources.ApplyResources(this.checkedListBoxAnnotations, "checkedListBoxAnnotations");
            this.checkedListBoxAnnotations.CheckOnClick = true;
            this.checkedListBoxAnnotations.FormattingEnabled = true;
            this.checkedListBoxAnnotations.Name = "checkedListBoxAnnotations";
            // 
            // tabPageGroupComparisons
            // 
            this.tabPageGroupComparisons.Controls.Add(this.btnAddGroupComparison);
            this.tabPageGroupComparisons.Controls.Add(this.btnEditGroupComparisonList);
            this.tabPageGroupComparisons.Controls.Add(this.checkedListBoxGroupComparisons);
            resources.ApplyResources(this.tabPageGroupComparisons, "tabPageGroupComparisons");
            this.tabPageGroupComparisons.Name = "tabPageGroupComparisons";
            this.tabPageGroupComparisons.UseVisualStyleBackColor = true;
            // 
            // btnAddGroupComparison
            // 
            resources.ApplyResources(this.btnAddGroupComparison, "btnAddGroupComparison");
            this.btnAddGroupComparison.Name = "btnAddGroupComparison";
            this.btnAddGroupComparison.UseVisualStyleBackColor = true;
            this.btnAddGroupComparison.Click += new System.EventHandler(this.btnAddGroupComparison_Click);
            // 
            // btnEditGroupComparisonList
            // 
            resources.ApplyResources(this.btnEditGroupComparisonList, "btnEditGroupComparisonList");
            this.btnEditGroupComparisonList.Name = "btnEditGroupComparisonList";
            this.btnEditGroupComparisonList.UseVisualStyleBackColor = true;
            this.btnEditGroupComparisonList.Click += new System.EventHandler(this.btnEditGroupComparisonList_Click);
            // 
            // checkedListBoxGroupComparisons
            // 
            resources.ApplyResources(this.checkedListBoxGroupComparisons, "checkedListBoxGroupComparisons");
            this.checkedListBoxGroupComparisons.CheckOnClick = true;
            this.checkedListBoxGroupComparisons.FormattingEnabled = true;
            this.checkedListBoxGroupComparisons.Name = "checkedListBoxGroupComparisons";
            // 
            // tabPageReports
            // 
            this.tabPageReports.Controls.Add(this.labelReports);
            this.tabPageReports.Controls.Add(this.chooseViewsControl);
            resources.ApplyResources(this.tabPageReports, "tabPageReports");
            this.tabPageReports.Name = "tabPageReports";
            this.tabPageReports.UseVisualStyleBackColor = true;
            // 
            // labelReports
            // 
            resources.ApplyResources(this.labelReports, "labelReports");
            this.labelReports.Name = "labelReports";
            // 
            // chooseViewsControl
            // 
            this.chooseViewsControl.AllowEditing = false;
            resources.ApplyResources(this.chooseViewsControl, "chooseViewsControl");
            this.chooseViewsControl.FilterRowSources = false;
            this.chooseViewsControl.GrayDisabledRowSources = false;
            this.chooseViewsControl.MultiSelect = true;
            this.chooseViewsControl.Name = "chooseViewsControl";
            this.chooseViewsControl.ShowCheckboxes = false;
            this.chooseViewsControl.ShowGroupChooser = false;
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // DocumentSettingsDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tabControl);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DocumentSettingsDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.tabControl.ResumeLayout(false);
            this.tabPageAnnotations.ResumeLayout(false);
            this.tabPageGroupComparisons.ResumeLayout(false);
            this.tabPageReports.ResumeLayout(false);
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
        private System.Windows.Forms.TabPage tabPageReports;
        private Common.DataBinding.Controls.Editor.ChooseViewsControl chooseViewsControl;
        private System.Windows.Forms.Label labelReports;
    }
}