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
            this.btnAddNewRule = new System.Windows.Forms.Button();
            this.lblRules = new System.Windows.Forms.Label();
            this.tbxName = new System.Windows.Forms.TextBox();
            this.lblName = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.listViewRules = new System.Windows.Forms.ListView();
            this.columnHeaderSource = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderRegex = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderReplacement = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderTarget = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.toolStripRules = new System.Windows.Forms.ToolStrip();
            this.btnRemove = new System.Windows.Forms.ToolStripButton();
            this.btnUp = new System.Windows.Forms.ToolStripButton();
            this.btnDown = new System.Windows.Forms.ToolStripButton();
            this.btnEdit = new System.Windows.Forms.ToolStripButton();
            this.boundDataGridViewEx1 = new pwiz.Skyline.Controls.Databinding.BoundDataGridViewEx();
            this.bindingListSource1 = new pwiz.Common.DataBinding.Controls.BindingListSource(this.components);
            this.panelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.toolStripRules.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridViewEx1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource1)).BeginInit();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.btnAddNewRule);
            this.splitContainer1.Panel1.Controls.Add(this.lblRules);
            this.splitContainer1.Panel1.Controls.Add(this.tbxName);
            this.splitContainer1.Panel1.Controls.Add(this.lblName);
            this.splitContainer1.Panel1.Controls.Add(this.panel1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.boundDataGridViewEx1);
            this.splitContainer1.Size = new System.Drawing.Size(800, 421);
            this.splitContainer1.SplitterDistance = 237;
            this.splitContainer1.TabIndex = 6;
            // 
            // btnAddNewRule
            // 
            this.btnAddNewRule.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddNewRule.Location = new System.Drawing.Point(645, 211);
            this.btnAddNewRule.Name = "btnAddNewRule";
            this.btnAddNewRule.Size = new System.Drawing.Size(147, 23);
            this.btnAddNewRule.TabIndex = 6;
            this.btnAddNewRule.Text = "Add New Rule...";
            this.btnAddNewRule.UseVisualStyleBackColor = true;
            this.btnAddNewRule.Click += new System.EventHandler(this.btnAddNewRule_Click);
            // 
            // lblRules
            // 
            this.lblRules.AutoSize = true;
            this.lblRules.Location = new System.Drawing.Point(12, 63);
            this.lblRules.Name = "lblRules";
            this.lblRules.Size = new System.Drawing.Size(37, 13);
            this.lblRules.TabIndex = 3;
            this.lblRules.Text = "Rules:";
            // 
            // tbxName
            // 
            this.tbxName.Location = new System.Drawing.Point(15, 40);
            this.tbxName.Name = "tbxName";
            this.tbxName.Size = new System.Drawing.Size(154, 20);
            this.tbxName.TabIndex = 2;
            // 
            // lblName
            // 
            this.lblName.AutoSize = true;
            this.lblName.Location = new System.Drawing.Point(12, 24);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(38, 13);
            this.lblName.TabIndex = 1;
            this.lblName.Text = "Name:";
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.listViewRules);
            this.panel1.Controls.Add(this.toolStripRules);
            this.panel1.Location = new System.Drawing.Point(12, 79);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(772, 126);
            this.panel1.TabIndex = 5;
            // 
            // listViewRules
            // 
            this.listViewRules.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderSource,
            this.columnHeaderRegex,
            this.columnHeaderReplacement,
            this.columnHeaderTarget});
            this.listViewRules.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewRules.HideSelection = false;
            this.listViewRules.Location = new System.Drawing.Point(0, 0);
            this.listViewRules.Name = "listViewRules";
            this.listViewRules.Size = new System.Drawing.Size(740, 126);
            this.listViewRules.TabIndex = 0;
            this.listViewRules.UseCompatibleStateImageBehavior = false;
            this.listViewRules.View = System.Windows.Forms.View.Details;
            this.listViewRules.ItemActivate += new System.EventHandler(this.listViewRules_ItemActivate);
            this.listViewRules.SelectedIndexChanged += new System.EventHandler(this.listViewRules_SelectedIndexChanged);
            // 
            // columnHeaderSource
            // 
            this.columnHeaderSource.Text = "Source";
            this.columnHeaderSource.Width = 153;
            // 
            // columnHeaderRegex
            // 
            this.columnHeaderRegex.Text = "Filter";
            this.columnHeaderRegex.Width = 180;
            // 
            // columnHeaderReplacement
            // 
            this.columnHeaderReplacement.Text = "Replacement";
            this.columnHeaderReplacement.Width = 140;
            // 
            // columnHeaderTarget
            // 
            this.columnHeaderTarget.Text = "Target";
            this.columnHeaderTarget.Width = 200;
            // 
            // toolStripRules
            // 
            this.toolStripRules.Dock = System.Windows.Forms.DockStyle.Right;
            this.toolStripRules.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripRules.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnRemove,
            this.btnUp,
            this.btnDown,
            this.btnEdit});
            this.toolStripRules.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStripRules.Location = new System.Drawing.Point(740, 0);
            this.toolStripRules.Name = "toolStripRules";
            this.toolStripRules.Size = new System.Drawing.Size(32, 126);
            this.toolStripRules.TabIndex = 4;
            this.toolStripRules.Text = "toolStrip1";
            // 
            // btnRemove
            // 
            this.btnRemove.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnRemove.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            this.btnRemove.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(29, 20);
            this.btnRemove.Text = "Remove";
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnUp
            // 
            this.btnUp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUp.Image = global::pwiz.Skyline.Properties.Resources.up_pro32;
            this.btnUp.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(29, 20);
            this.btnUp.Text = "Up";
            this.btnUp.Click += new System.EventHandler(this.BtnUpOnClick);
            // 
            // btnDown
            // 
            this.btnDown.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDown.Image = global::pwiz.Skyline.Properties.Resources.down_pro32;
            this.btnDown.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(29, 20);
            this.btnDown.Text = "Down";
            this.btnDown.Click += new System.EventHandler(this.BtnDownOnClick);
            // 
            // btnEdit
            // 
            this.btnEdit.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnEdit.Image = ((System.Drawing.Image)(resources.GetObject("btnEdit.Image")));
            this.btnEdit.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.Size = new System.Drawing.Size(29, 19);
            this.btnEdit.Text = "...";
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            // 
            // boundDataGridViewEx1
            // 
            this.boundDataGridViewEx1.AutoGenerateColumns = false;
            this.boundDataGridViewEx1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.boundDataGridViewEx1.DataSource = this.bindingListSource1;
            this.boundDataGridViewEx1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.boundDataGridViewEx1.Location = new System.Drawing.Point(0, 0);
            this.boundDataGridViewEx1.MaximumColumnCount = 2000;
            this.boundDataGridViewEx1.Name = "boundDataGridViewEx1";
            this.boundDataGridViewEx1.Size = new System.Drawing.Size(800, 180);
            this.boundDataGridViewEx1.TabIndex = 0;
            // 
            // bindingListSource1
            // 
            this.bindingListSource1.NewRowHandler = null;
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnCancel);
            this.panelButtons.Controls.Add(this.btnOK);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.panelButtons.Location = new System.Drawing.Point(0, 421);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(800, 29);
            this.panelButtons.TabIndex = 9;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(722, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOK.Location = new System.Drawing.Point(641, 3);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 0;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // MetadataRuleSetEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.panelButtons);
            this.Name = "MetadataRuleSetEditor";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "MetadataRuleSetEditor";
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.toolStripRules.ResumeLayout(false);
            this.toolStripRules.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridViewEx1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource1)).EndInit();
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView listViewRules;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox tbxName;
        private System.Windows.Forms.Label lblRules;
        private System.Windows.Forms.ToolStrip toolStripRules;
        private System.Windows.Forms.ToolStripButton btnRemove;
        private System.Windows.Forms.ToolStripButton btnUp;
        private System.Windows.Forms.ToolStripButton btnDown;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button btnAddNewRule;
        private Controls.Databinding.BoundDataGridViewEx boundDataGridViewEx1;
        private System.Windows.Forms.FlowLayoutPanel panelButtons;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private Common.DataBinding.Controls.BindingListSource bindingListSource1;
        private System.Windows.Forms.ToolStripButton btnEdit;
        private System.Windows.Forms.ColumnHeader columnHeaderSource;
        private System.Windows.Forms.ColumnHeader columnHeaderRegex;
        private System.Windows.Forms.ColumnHeader columnHeaderTarget;
        private System.Windows.Forms.ColumnHeader columnHeaderReplacement;
    }
}