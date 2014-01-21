namespace pwiz.Common.DataBinding.Controls.Editor
{
    partial class ChooseColumnsTab
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ColumnHeader colHdrName;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ChooseColumnsTab));
            this.availableFieldsTreeColumns = new pwiz.Common.DataBinding.Controls.Editor.AvailableFieldsTree();
            this.listViewColumns = new System.Windows.Forms.ListView();
            this.toolStripColumns = new System.Windows.Forms.ToolStrip();
            this.btnRemove = new System.Windows.Forms.ToolStripButton();
            this.btnUp = new System.Windows.Forms.ToolStripButton();
            this.btnDown = new System.Windows.Forms.ToolStripButton();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            colHdrName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.toolStripColumns.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // colHdrName
            // 
            resources.ApplyResources(colHdrName, "colHdrName");
            // 
            // availableFieldsTreeColumns
            // 
            resources.ApplyResources(this.availableFieldsTreeColumns, "availableFieldsTreeColumns");
            this.availableFieldsTreeColumns.CheckBoxes = true;
            this.availableFieldsTreeColumns.CheckedColumns = new pwiz.Common.DataBinding.PropertyPath[0];
            this.availableFieldsTreeColumns.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
            this.availableFieldsTreeColumns.HideSelection = false;
            this.availableFieldsTreeColumns.Name = "availableFieldsTreeColumns";
            this.availableFieldsTreeColumns.RootColumn = null;
            this.availableFieldsTreeColumns.ShowAdvancedFields = false;
            this.availableFieldsTreeColumns.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.AvailableFieldsTreeColumnsOnAfterCheck);
            this.availableFieldsTreeColumns.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.AvailableFieldsTreeColumnsOnNodeMouseDoubleClick);
            // 
            // listViewColumns
            // 
            this.listViewColumns.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            colHdrName});
            resources.ApplyResources(this.listViewColumns, "listViewColumns");
            this.listViewColumns.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.listViewColumns.HideSelection = false;
            this.listViewColumns.LabelEdit = true;
            this.listViewColumns.Name = "listViewColumns";
            this.listViewColumns.ShowItemToolTips = true;
            this.listViewColumns.UseCompatibleStateImageBehavior = false;
            this.listViewColumns.View = System.Windows.Forms.View.Details;
            this.listViewColumns.AfterLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.listViewColumns_AfterLabelEdit);
            this.listViewColumns.BeforeLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.listViewColumns_BeforeLabelEdit);
            this.listViewColumns.ItemActivate += new System.EventHandler(this.ListViewColumnsOnItemActivate);
            this.listViewColumns.SelectedIndexChanged += new System.EventHandler(this.ListViewColumnsOnSelectedIndexChanged);
            this.listViewColumns.SizeChanged += new System.EventHandler(this.ListViewColumnsOnSizeChanged);
            // 
            // toolStripColumns
            // 
            resources.ApplyResources(this.toolStripColumns, "toolStripColumns");
            this.toolStripColumns.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripColumns.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnRemove,
            this.btnUp,
            this.btnDown});
            this.toolStripColumns.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStripColumns.Name = "toolStripColumns";
            // 
            // btnRemove
            // 
            this.btnRemove.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnRemove.Image = global::pwiz.Common.Properties.Resources.Delete;
            resources.ApplyResources(this.btnRemove, "btnRemove");
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Click += new System.EventHandler(this.BtnRemoveOnClick);
            // 
            // btnUp
            // 
            this.btnUp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUp.Image = global::pwiz.Common.Properties.Resources.up_pro32;
            resources.ApplyResources(this.btnUp, "btnUp");
            this.btnUp.Name = "btnUp";
            this.btnUp.Click += new System.EventHandler(this.BtnUpOnClick);
            // 
            // btnDown
            // 
            this.btnDown.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDown.Image = global::pwiz.Common.Properties.Resources.down_pro32;
            resources.ApplyResources(this.btnDown, "btnDown");
            this.btnDown.Name = "btnDown";
            this.btnDown.Click += new System.EventHandler(this.BtnDownOnClick);
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this.availableFieldsTreeColumns, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panel1, 1, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.listViewColumns);
            this.panel1.Controls.Add(this.toolStripColumns);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // ChooseColumnsTab
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "ChooseColumnsTab";
            this.toolStripColumns.ResumeLayout(false);
            this.toolStripColumns.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private AvailableFieldsTree availableFieldsTreeColumns;
        private System.Windows.Forms.ListView listViewColumns;
        private System.Windows.Forms.ToolStrip toolStripColumns;
        private System.Windows.Forms.ToolStripButton btnRemove;
        private System.Windows.Forms.ToolStripButton btnUp;
        private System.Windows.Forms.ToolStripButton btnDown;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panel1;
    }
}
