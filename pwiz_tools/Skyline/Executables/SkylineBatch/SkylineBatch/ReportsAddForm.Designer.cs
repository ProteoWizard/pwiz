namespace SkylineBatch
{
    partial class ReportsAddForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ReportsAddForm));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.textReportName = new System.Windows.Forms.TextBox();
            this.labelConfigName = new System.Windows.Forms.Label();
            this.labelReportPath = new System.Windows.Forms.Label();
            this.textReportPath = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnReportPath = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.dataGridScripts = new System.Windows.Forms.DataGridView();
            this.columnPath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnUrl = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnVersion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.radioResultsFile = new System.Windows.Forms.RadioButton();
            this.radioRefinedFile = new System.Windows.Forms.RadioButton();
            this.label3 = new System.Windows.Forms.Label();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnAdd = new System.Windows.Forms.ToolStripButton();
            this.btnDelete = new System.Windows.Forms.ToolStripButton();
            this.btnEdit = new System.Windows.Forms.ToolStripButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.checkBoxImport = new System.Windows.Forms.CheckBox();
            this.checkBoxCultureInvariant = new System.Windows.Forms.CheckBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridScripts)).BeginInit();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // textReportName
            // 
            resources.ApplyResources(this.textReportName, "textReportName");
            this.textReportName.Name = "textReportName";
            // 
            // labelConfigName
            // 
            resources.ApplyResources(this.labelConfigName, "labelConfigName");
            this.labelConfigName.Name = "labelConfigName";
            // 
            // labelReportPath
            // 
            resources.ApplyResources(this.labelReportPath, "labelReportPath");
            this.labelReportPath.Name = "labelReportPath";
            // 
            // textReportPath
            // 
            resources.ApplyResources(this.textReportPath, "textReportPath");
            this.textReportPath.Name = "textReportPath";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnReportPath
            // 
            resources.ApplyResources(this.btnReportPath, "btnReportPath");
            this.btnReportPath.Name = "btnReportPath";
            this.btnReportPath.UseVisualStyleBackColor = true;
            this.btnReportPath.Click += new System.EventHandler(this.btnReportPath_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // dataGridScripts
            // 
            this.dataGridScripts.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridScripts.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridScripts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridScripts.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnPath,
            this.columnUrl,
            this.columnVersion});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridScripts.DefaultCellStyle = dataGridViewCellStyle2;
            resources.ApplyResources(this.dataGridScripts, "dataGridScripts");
            this.dataGridScripts.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dataGridScripts.MultiSelect = false;
            this.dataGridScripts.Name = "dataGridScripts";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridScripts.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dataGridScripts.RowHeadersVisible = false;
            this.dataGridScripts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridScripts.CellContentDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridScripts_CellContentDoubleClick);
            this.dataGridScripts.SelectionChanged += new System.EventHandler(this.dataGridScripts_SelectionChanged);
            // 
            // columnPath
            // 
            this.columnPath.FillWeight = 149.2386F;
            resources.ApplyResources(this.columnPath, "columnPath");
            this.columnPath.Name = "columnPath";
            // 
            // columnUrl
            // 
            resources.ApplyResources(this.columnUrl, "columnUrl");
            this.columnUrl.Name = "columnUrl";
            // 
            // columnVersion
            // 
            this.columnVersion.FillWeight = 50.76142F;
            resources.ApplyResources(this.columnVersion, "columnVersion");
            this.columnVersion.Name = "columnVersion";
            // 
            // radioResultsFile
            // 
            resources.ApplyResources(this.radioResultsFile, "radioResultsFile");
            this.radioResultsFile.Name = "radioResultsFile";
            this.radioResultsFile.TabStop = true;
            this.radioResultsFile.UseVisualStyleBackColor = true;
            // 
            // radioRefinedFile
            // 
            resources.ApplyResources(this.radioRefinedFile, "radioRefinedFile");
            this.radioRefinedFile.Name = "radioRefinedFile";
            this.radioRefinedFile.TabStop = true;
            this.radioRefinedFile.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // toolStrip1
            // 
            resources.ApplyResources(this.toolStrip1, "toolStrip1");
            this.toolStrip1.BackColor = System.Drawing.SystemColors.Control;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnAdd,
            this.btnDelete,
            this.btnEdit});
            this.toolStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStrip1.Name = "toolStrip1";
            // 
            // btnAdd
            // 
            this.btnAdd.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnAdd.Image = global::SkylineBatch.Properties.Resources.add;
            resources.ApplyResources(this.btnAdd, "btnAdd");
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnDelete, "btnDelete");
            this.btnDelete.Image = global::SkylineBatch.Properties.Resources.Delete;
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnEdit
            // 
            this.btnEdit.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnEdit, "btnEdit");
            this.btnEdit.Image = global::SkylineBatch.Properties.Resources.Comment;
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dataGridScripts);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.toolStrip1);
            // 
            // checkBoxImport
            // 
            resources.ApplyResources(this.checkBoxImport, "checkBoxImport");
            this.checkBoxImport.Checked = true;
            this.checkBoxImport.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxImport.Name = "checkBoxImport";
            this.toolTip1.SetToolTip(this.checkBoxImport, resources.GetString("checkBoxImport.ToolTip"));
            this.checkBoxImport.UseVisualStyleBackColor = true;
            this.checkBoxImport.CheckedChanged += new System.EventHandler(this.checkBoxImport_CheckedChanged);
            // 
            // checkBoxCultureInvariant
            // 
            resources.ApplyResources(this.checkBoxCultureInvariant, "checkBoxCultureInvariant");
            this.checkBoxCultureInvariant.Checked = true;
            this.checkBoxCultureInvariant.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxCultureInvariant.Name = "checkBoxCultureInvariant";
            this.toolTip1.SetToolTip(this.checkBoxCultureInvariant, resources.GetString("checkBoxCultureInvariant.ToolTip"));
            this.checkBoxCultureInvariant.UseVisualStyleBackColor = true;
            // 
            // ReportsAddForm
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.checkBoxCultureInvariant);
            this.Controls.Add(this.checkBoxImport);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.radioRefinedFile);
            this.Controls.Add(this.radioResultsFile);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnReportPath);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.labelReportPath);
            this.Controls.Add(this.textReportPath);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textReportName);
            this.Controls.Add(this.labelConfigName);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ReportsAddForm";
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.ReportsAddForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridScripts)).EndInit();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textReportName;
        private System.Windows.Forms.Label labelConfigName;
        private System.Windows.Forms.Label labelReportPath;
        private System.Windows.Forms.TextBox textReportPath;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnReportPath;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.DataGridView dataGridScripts;
        private System.Windows.Forms.RadioButton radioResultsFile;
        private System.Windows.Forms.RadioButton radioRefinedFile;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnAdd;
        private System.Windows.Forms.ToolStripButton btnDelete;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.CheckBox checkBoxImport;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.CheckBox checkBoxCultureInvariant;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnPath;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnUrl;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnVersion;
        public System.Windows.Forms.ToolStripButton btnEdit;
    }
}