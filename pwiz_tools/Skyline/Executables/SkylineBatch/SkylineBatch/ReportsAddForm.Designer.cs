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
            this.label1 = new System.Windows.Forms.Label();
            this.textReportPath = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnAddRScript = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnReportPath = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.dataGridScripts = new System.Windows.Forms.DataGridView();
            this.columnPath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnVersion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.rVersionsDropDown = new System.Windows.Forms.ContextMenuStrip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridScripts)).BeginInit();
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
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
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
            // btnAddRScript
            // 
            resources.ApplyResources(this.btnAddRScript, "btnAddRScript");
            this.btnAddRScript.Name = "btnAddRScript";
            this.btnAddRScript.UseVisualStyleBackColor = true;
            this.btnAddRScript.Click += new System.EventHandler(this.btnAddRScript_Click);
            // 
            // btnRemove
            // 
            resources.ApplyResources(this.btnRemove, "btnRemove");
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
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
            resources.ApplyResources(this.dataGridScripts, "dataGridScripts");
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
            this.columnVersion});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridScripts.DefaultCellStyle = dataGridViewCellStyle2;
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
            this.dataGridScripts.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridScripts_CellClick);
            this.dataGridScripts.CellContentDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridScripts_CellContentDoubleClick);
            this.dataGridScripts.SelectionChanged += new System.EventHandler(this.dataGridScripts_SelectionChanged);
            // 
            // columnPath
            // 
            this.columnPath.FillWeight = 149.2386F;
            resources.ApplyResources(this.columnPath, "columnPath");
            this.columnPath.Name = "columnPath";
            // 
            // columnVersion
            // 
            this.columnVersion.FillWeight = 50.76142F;
            resources.ApplyResources(this.columnVersion, "columnVersion");
            this.columnVersion.Name = "columnVersion";
            // 
            // rVersionsDropDown
            // 
            this.rVersionsDropDown.Name = "rVersionsDropDown";
            resources.ApplyResources(this.rVersionsDropDown, "rVersionsDropDown");
            this.rVersionsDropDown.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.rVersionsDropDown_ItemClicked);
            // 
            // ReportsAddForm
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.dataGridScripts);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnReportPath);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnAddRScript);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textReportPath);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textReportName);
            this.Controls.Add(this.labelConfigName);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ReportsAddForm";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.dataGridScripts)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textReportName;
        private System.Windows.Forms.Label labelConfigName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textReportPath;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnAddRScript;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnReportPath;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.DataGridView dataGridScripts;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnPath;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnVersion;
        private System.Windows.Forms.ContextMenuStrip rVersionsDropDown;
    }
}