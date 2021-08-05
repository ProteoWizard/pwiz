namespace pwiz.Skyline.SettingsUI
{
    partial class MetadataRuleSetEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MetadataRuleSetEditor));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lblRules = new System.Windows.Forms.Label();
            this.tbxName = new System.Windows.Forms.TextBox();
            this.lblName = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.dataGridViewRules = new pwiz.Skyline.Controls.DataGridViewEx();
            this.colSource = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colPattern = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colReplacement = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTarget = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.bindingSourceRules = new System.Windows.Forms.BindingSource(this.components);
            this.toolStripRules = new System.Windows.Forms.ToolStrip();
            this.btnRemove = new System.Windows.Forms.ToolStripButton();
            this.btnUp = new System.Windows.Forms.ToolStripButton();
            this.btnDown = new System.Windows.Forms.ToolStripButton();
            this.btnEdit = new System.Windows.Forms.ToolStripButton();
            this.label1 = new System.Windows.Forms.Label();
            this.boundDataGridViewEx1 = new pwiz.Skyline.Controls.Databinding.BoundDataGridViewEx();
            this.bindingListSourceResults = new pwiz.Common.DataBinding.Controls.BindingListSource(this.components);
            this.panelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRules)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceRules)).BeginInit();
            this.toolStripRules.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridViewEx1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSourceResults)).BeginInit();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.lblRules);
            this.splitContainer1.Panel1.Controls.Add(this.tbxName);
            this.splitContainer1.Panel1.Controls.Add(this.lblName);
            this.splitContainer1.Panel1.Controls.Add(this.panel1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.label1);
            this.splitContainer1.Panel2.Controls.Add(this.boundDataGridViewEx1);
            resources.ApplyResources(this.splitContainer1.Panel2, "splitContainer1.Panel2");
            // 
            // lblRules
            // 
            resources.ApplyResources(this.lblRules, "lblRules");
            this.lblRules.Name = "lblRules";
            // 
            // tbxName
            // 
            resources.ApplyResources(this.tbxName, "tbxName");
            this.tbxName.Name = "tbxName";
            // 
            // lblName
            // 
            resources.ApplyResources(this.lblName, "lblName");
            this.lblName.Name = "lblName";
            // 
            // panel1
            // 
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Controls.Add(this.dataGridViewRules);
            this.panel1.Controls.Add(this.toolStripRules);
            this.panel1.Name = "panel1";
            // 
            // dataGridViewRules
            // 
            this.dataGridViewRules.AutoGenerateColumns = false;
            this.dataGridViewRules.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewRules.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSource,
            this.colPattern,
            this.colReplacement,
            this.colTarget});
            this.dataGridViewRules.DataSource = this.bindingSourceRules;
            resources.ApplyResources(this.dataGridViewRules, "dataGridViewRules");
            this.dataGridViewRules.Name = "dataGridViewRules";
            this.dataGridViewRules.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewRules.CurrentCellChanged += new System.EventHandler(this.dataGridViewRules_CurrentCellChanged);
            this.dataGridViewRules.SelectionChanged += new System.EventHandler(this.dataGridViewRules_SelectionChanged);
            // 
            // colSource
            // 
            this.colSource.DataPropertyName = "Source";
            this.colSource.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            resources.ApplyResources(this.colSource, "colSource");
            this.colSource.Name = "colSource";
            // 
            // colPattern
            // 
            this.colPattern.DataPropertyName = "Pattern";
            resources.ApplyResources(this.colPattern, "colPattern");
            this.colPattern.Name = "colPattern";
            // 
            // colReplacement
            // 
            this.colReplacement.DataPropertyName = "Replacement";
            resources.ApplyResources(this.colReplacement, "colReplacement");
            this.colReplacement.Name = "colReplacement";
            // 
            // colTarget
            // 
            this.colTarget.DataPropertyName = "Target";
            this.colTarget.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            resources.ApplyResources(this.colTarget, "colTarget");
            this.colTarget.Name = "colTarget";
            this.colTarget.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colTarget.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // bindingSourceRules
            // 
            this.bindingSourceRules.ListChanged += new System.ComponentModel.ListChangedEventHandler(this.bindingSourceRules_ListChanged);
            // 
            // toolStripRules
            // 
            resources.ApplyResources(this.toolStripRules, "toolStripRules");
            this.toolStripRules.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripRules.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnRemove,
            this.btnUp,
            this.btnDown,
            this.btnEdit});
            this.toolStripRules.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStripRules.Name = "toolStripRules";
            // 
            // btnRemove
            // 
            this.btnRemove.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnRemove.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            resources.ApplyResources(this.btnRemove, "btnRemove");
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnUp
            // 
            this.btnUp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUp.Image = global::pwiz.Skyline.Properties.Resources.up_pro32;
            resources.ApplyResources(this.btnUp, "btnUp");
            this.btnUp.Name = "btnUp";
            this.btnUp.Click += new System.EventHandler(this.BtnUpOnClick);
            // 
            // btnDown
            // 
            this.btnDown.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDown.Image = global::pwiz.Skyline.Properties.Resources.down_pro32;
            resources.ApplyResources(this.btnDown, "btnDown");
            this.btnDown.Name = "btnDown";
            this.btnDown.Click += new System.EventHandler(this.BtnDownOnClick);
            // 
            // btnEdit
            // 
            this.btnEdit.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            resources.ApplyResources(this.btnEdit, "btnEdit");
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // boundDataGridViewEx1
            // 
            resources.ApplyResources(this.boundDataGridViewEx1, "boundDataGridViewEx1");
            this.boundDataGridViewEx1.AutoGenerateColumns = false;
            this.boundDataGridViewEx1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.boundDataGridViewEx1.DataSource = this.bindingListSourceResults;
            this.boundDataGridViewEx1.MaximumColumnCount = 2000;
            this.boundDataGridViewEx1.Name = "boundDataGridViewEx1";
            // 
            // bindingListSourceResults
            // 
            this.bindingListSourceResults.NewRowHandler = null;
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnCancel);
            this.panelButtons.Controls.Add(this.btnOK);
            resources.ApplyResources(this.panelButtons, "panelButtons");
            this.panelButtons.Name = "panelButtons";
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
            // MetadataRuleSetEditor
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.panelButtons);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MetadataRuleSetEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRules)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceRules)).EndInit();
            this.toolStripRules.ResumeLayout(false);
            this.toolStripRules.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridViewEx1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSourceResults)).EndInit();
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox tbxName;
        private System.Windows.Forms.Label lblRules;
        private System.Windows.Forms.ToolStrip toolStripRules;
        private System.Windows.Forms.ToolStripButton btnRemove;
        private System.Windows.Forms.ToolStripButton btnUp;
        private System.Windows.Forms.ToolStripButton btnDown;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private Controls.Databinding.BoundDataGridViewEx boundDataGridViewEx1;
        private System.Windows.Forms.FlowLayoutPanel panelButtons;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private Common.DataBinding.Controls.BindingListSource bindingListSourceResults;
        private System.Windows.Forms.ToolStripButton btnEdit;
        private Controls.DataGridViewEx dataGridViewRules;
        private System.Windows.Forms.BindingSource bindingSourceRules;
        private System.Windows.Forms.DataGridViewComboBoxColumn colSource;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPattern;
        private System.Windows.Forms.DataGridViewTextBoxColumn colReplacement;
        private System.Windows.Forms.DataGridViewComboBoxColumn colTarget;
        private System.Windows.Forms.Label label1;
    }
}