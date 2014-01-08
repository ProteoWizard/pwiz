using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    partial class AlignmentForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AlignmentForm));
            this.panel1 = new System.Windows.Forms.Panel();
            this.lblFormDescription = new System.Windows.Forms.Label();
            this.comboAlignAgainst = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.colLibrary = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDataFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSlope = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colIntercept = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCorrelationCoefficient = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colOutlierCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colUnrefinedSlope = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colUnrefinedIntercept = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colUnrefinedCorrelationCoefficient = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTotalPoints = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.bindingNavigator1 = new System.Windows.Forms.BindingNavigator(this.components);
            this.bindingNavigatorAddNewItem = new System.Windows.Forms.ToolStripButton();
            this.bindingNavigatorCountItem = new System.Windows.Forms.ToolStripLabel();
            this.bindingNavigatorDeleteItem = new System.Windows.Forms.ToolStripButton();
            this.bindingNavigatorMoveFirstItem = new System.Windows.Forms.ToolStripButton();
            this.bindingNavigatorMovePreviousItem = new System.Windows.Forms.ToolStripButton();
            this.bindingNavigatorSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.bindingNavigatorPositionItem = new System.Windows.Forms.ToolStripTextBox();
            this.bindingNavigatorSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.bindingNavigatorMoveNextItem = new System.Windows.Forms.ToolStripButton();
            this.bindingNavigatorMoveLastItem = new System.Windows.Forms.ToolStripButton();
            this.bindingNavigatorSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.zedGraphControl = new ZedGraph.ZedGraphControl();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingNavigator1)).BeginInit();
            this.bindingNavigator1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.lblFormDescription);
            this.panel1.Controls.Add(this.comboAlignAgainst);
            this.panel1.Controls.Add(this.label1);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // lblFormDescription
            // 
            resources.ApplyResources(this.lblFormDescription, "lblFormDescription");
            this.lblFormDescription.AutoEllipsis = true;
            this.lblFormDescription.Name = "lblFormDescription";
            // 
            // comboAlignAgainst
            // 
            this.comboAlignAgainst.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAlignAgainst.FormattingEnabled = true;
            resources.ApplyResources(this.comboAlignAgainst, "comboAlignAgainst");
            this.comboAlignAgainst.Name = "comboAlignAgainst";
            this.comboAlignAgainst.SelectedIndexChanged += new System.EventHandler(this.comboAlignAgainst_SelectedIndexChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            this.dataGridView1.AutoGenerateColumns = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colLibrary,
            this.colDataFile,
            this.colSlope,
            this.colIntercept,
            this.colCorrelationCoefficient,
            this.colOutlierCount,
            this.colUnrefinedSlope,
            this.colUnrefinedIntercept,
            this.colUnrefinedCorrelationCoefficient,
            this.colTotalPoints});
            this.dataGridView1.DataSource = this.bindingSource;
            resources.ApplyResources(this.dataGridView1, "dataGridView1");
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            // 
            // colLibrary
            // 
            this.colLibrary.DataPropertyName = "Library";
            resources.ApplyResources(this.colLibrary, "colLibrary");
            this.colLibrary.Name = "colLibrary";
            this.colLibrary.ReadOnly = true;
            // 
            // colDataFile
            // 
            this.colDataFile.DataPropertyName = "DataFile";
            resources.ApplyResources(this.colDataFile, "colDataFile");
            this.colDataFile.Name = "colDataFile";
            this.colDataFile.ReadOnly = true;
            // 
            // colSlope
            // 
            this.colSlope.DataPropertyName = "Slope";
            resources.ApplyResources(this.colSlope, "colSlope");
            this.colSlope.Name = "colSlope";
            this.colSlope.ReadOnly = true;
            // 
            // colIntercept
            // 
            this.colIntercept.DataPropertyName = "Intercept";
            resources.ApplyResources(this.colIntercept, "colIntercept");
            this.colIntercept.Name = "colIntercept";
            this.colIntercept.ReadOnly = true;
            // 
            // colCorrelationCoefficient
            // 
            this.colCorrelationCoefficient.DataPropertyName = "CorrelationCoefficient";
            resources.ApplyResources(this.colCorrelationCoefficient, "colCorrelationCoefficient");
            this.colCorrelationCoefficient.Name = "colCorrelationCoefficient";
            this.colCorrelationCoefficient.ReadOnly = true;
            // 
            // colOutlierCount
            // 
            this.colOutlierCount.DataPropertyName = "OutlierCount";
            resources.ApplyResources(this.colOutlierCount, "colOutlierCount");
            this.colOutlierCount.Name = "colOutlierCount";
            this.colOutlierCount.ReadOnly = true;
            // 
            // colUnrefinedSlope
            // 
            this.colUnrefinedSlope.DataPropertyName = "UnrefinedSlope";
            resources.ApplyResources(this.colUnrefinedSlope, "colUnrefinedSlope");
            this.colUnrefinedSlope.Name = "colUnrefinedSlope";
            this.colUnrefinedSlope.ReadOnly = true;
            // 
            // colUnrefinedIntercept
            // 
            this.colUnrefinedIntercept.DataPropertyName = "UnrefinedIntercept";
            resources.ApplyResources(this.colUnrefinedIntercept, "colUnrefinedIntercept");
            this.colUnrefinedIntercept.Name = "colUnrefinedIntercept";
            this.colUnrefinedIntercept.ReadOnly = true;
            // 
            // colUnrefinedCorrelationCoefficient
            // 
            this.colUnrefinedCorrelationCoefficient.DataPropertyName = "UnrefinedCorrelationCoefficient";
            resources.ApplyResources(this.colUnrefinedCorrelationCoefficient, "colUnrefinedCorrelationCoefficient");
            this.colUnrefinedCorrelationCoefficient.Name = "colUnrefinedCorrelationCoefficient";
            this.colUnrefinedCorrelationCoefficient.ReadOnly = true;
            // 
            // colTotalPoints
            // 
            this.colTotalPoints.DataPropertyName = "PointCount";
            resources.ApplyResources(this.colTotalPoints, "colTotalPoints");
            this.colTotalPoints.Name = "colTotalPoints";
            this.colTotalPoints.ReadOnly = true;
            // 
            // bindingSource
            // 
            this.bindingSource.CurrentItemChanged += new System.EventHandler(this.bindingSource_CurrentItemChanged);
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dataGridView1);
            this.splitContainer1.Panel1.Controls.Add(this.bindingNavigator1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.zedGraphControl);
            // 
            // bindingNavigator1
            // 
            this.bindingNavigator1.AddNewItem = this.bindingNavigatorAddNewItem;
            this.bindingNavigator1.BindingSource = this.bindingSource;
            this.bindingNavigator1.CountItem = this.bindingNavigatorCountItem;
            this.bindingNavigator1.DeleteItem = this.bindingNavigatorDeleteItem;
            this.bindingNavigator1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bindingNavigatorMoveFirstItem,
            this.bindingNavigatorMovePreviousItem,
            this.bindingNavigatorSeparator,
            this.bindingNavigatorPositionItem,
            this.bindingNavigatorCountItem,
            this.bindingNavigatorSeparator1,
            this.bindingNavigatorMoveNextItem,
            this.bindingNavigatorMoveLastItem,
            this.bindingNavigatorSeparator2,
            this.bindingNavigatorAddNewItem,
            this.bindingNavigatorDeleteItem});
            resources.ApplyResources(this.bindingNavigator1, "bindingNavigator1");
            this.bindingNavigator1.MoveFirstItem = this.bindingNavigatorMoveFirstItem;
            this.bindingNavigator1.MoveLastItem = this.bindingNavigatorMoveLastItem;
            this.bindingNavigator1.MoveNextItem = this.bindingNavigatorMoveNextItem;
            this.bindingNavigator1.MovePreviousItem = this.bindingNavigatorMovePreviousItem;
            this.bindingNavigator1.Name = "bindingNavigator1";
            this.bindingNavigator1.PositionItem = this.bindingNavigatorPositionItem;
            // 
            // bindingNavigatorAddNewItem
            // 
            this.bindingNavigatorAddNewItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.bindingNavigatorAddNewItem, "bindingNavigatorAddNewItem");
            this.bindingNavigatorAddNewItem.Name = "bindingNavigatorAddNewItem";
            // 
            // bindingNavigatorCountItem
            // 
            this.bindingNavigatorCountItem.Name = "bindingNavigatorCountItem";
            resources.ApplyResources(this.bindingNavigatorCountItem, "bindingNavigatorCountItem");
            // 
            // bindingNavigatorDeleteItem
            // 
            this.bindingNavigatorDeleteItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.bindingNavigatorDeleteItem, "bindingNavigatorDeleteItem");
            this.bindingNavigatorDeleteItem.Name = "bindingNavigatorDeleteItem";
            // 
            // bindingNavigatorMoveFirstItem
            // 
            this.bindingNavigatorMoveFirstItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.bindingNavigatorMoveFirstItem, "bindingNavigatorMoveFirstItem");
            this.bindingNavigatorMoveFirstItem.Name = "bindingNavigatorMoveFirstItem";
            // 
            // bindingNavigatorMovePreviousItem
            // 
            this.bindingNavigatorMovePreviousItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.bindingNavigatorMovePreviousItem, "bindingNavigatorMovePreviousItem");
            this.bindingNavigatorMovePreviousItem.Name = "bindingNavigatorMovePreviousItem";
            // 
            // bindingNavigatorSeparator
            // 
            this.bindingNavigatorSeparator.Name = "bindingNavigatorSeparator";
            resources.ApplyResources(this.bindingNavigatorSeparator, "bindingNavigatorSeparator");
            // 
            // bindingNavigatorPositionItem
            // 
            resources.ApplyResources(this.bindingNavigatorPositionItem, "bindingNavigatorPositionItem");
            this.bindingNavigatorPositionItem.Name = "bindingNavigatorPositionItem";
            // 
            // bindingNavigatorSeparator1
            // 
            this.bindingNavigatorSeparator1.Name = "bindingNavigatorSeparator1";
            resources.ApplyResources(this.bindingNavigatorSeparator1, "bindingNavigatorSeparator1");
            // 
            // bindingNavigatorMoveNextItem
            // 
            this.bindingNavigatorMoveNextItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.bindingNavigatorMoveNextItem, "bindingNavigatorMoveNextItem");
            this.bindingNavigatorMoveNextItem.Name = "bindingNavigatorMoveNextItem";
            // 
            // bindingNavigatorMoveLastItem
            // 
            this.bindingNavigatorMoveLastItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.bindingNavigatorMoveLastItem, "bindingNavigatorMoveLastItem");
            this.bindingNavigatorMoveLastItem.Name = "bindingNavigatorMoveLastItem";
            // 
            // bindingNavigatorSeparator2
            // 
            this.bindingNavigatorSeparator2.Name = "bindingNavigatorSeparator2";
            resources.ApplyResources(this.bindingNavigatorSeparator2, "bindingNavigatorSeparator2");
            // 
            // zedGraphControl
            // 
            resources.ApplyResources(this.zedGraphControl, "zedGraphControl");
            this.zedGraphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.zedGraphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.zedGraphControl.IsEnableVPan = false;
            this.zedGraphControl.IsEnableVZoom = false;
            this.zedGraphControl.IsShowPointValues = true;
            this.zedGraphControl.Name = "zedGraphControl";
            this.zedGraphControl.ScrollGrace = 0D;
            this.zedGraphControl.ScrollMaxX = 0D;
            this.zedGraphControl.ScrollMaxY = 0D;
            this.zedGraphControl.ScrollMaxY2 = 0D;
            this.zedGraphControl.ScrollMinX = 0D;
            this.zedGraphControl.ScrollMinY = 0D;
            this.zedGraphControl.ScrollMinY2 = 0D;
            this.zedGraphControl.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.zedGraphControl_ContextMenuBuilder);
            // 
            // AlignmentForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.panel1);
            this.Name = "AlignmentForm";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.bindingNavigator1)).EndInit();
            this.bindingNavigator1.ResumeLayout(false);
            this.bindingNavigator1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ComboBox comboAlignAgainst;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private ZedGraph.ZedGraphControl zedGraphControl;
        private System.Windows.Forms.BindingSource bindingSource;
        private System.Windows.Forms.BindingNavigator bindingNavigator1;
        private System.Windows.Forms.ToolStripButton bindingNavigatorAddNewItem;
        private System.Windows.Forms.ToolStripLabel bindingNavigatorCountItem;
        private System.Windows.Forms.ToolStripButton bindingNavigatorDeleteItem;
        private System.Windows.Forms.ToolStripButton bindingNavigatorMoveFirstItem;
        private System.Windows.Forms.ToolStripButton bindingNavigatorMovePreviousItem;
        private System.Windows.Forms.ToolStripSeparator bindingNavigatorSeparator;
        private System.Windows.Forms.ToolStripTextBox bindingNavigatorPositionItem;
        private System.Windows.Forms.ToolStripSeparator bindingNavigatorSeparator1;
        private System.Windows.Forms.ToolStripButton bindingNavigatorMoveNextItem;
        private System.Windows.Forms.ToolStripButton bindingNavigatorMoveLastItem;
        private System.Windows.Forms.ToolStripSeparator bindingNavigatorSeparator2;
        private System.Windows.Forms.Label lblFormDescription;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLibrary;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDataFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSlope;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIntercept;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCorrelationCoefficient;
        private System.Windows.Forms.DataGridViewTextBoxColumn colOutlierCount;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUnrefinedSlope;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUnrefinedIntercept;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUnrefinedCorrelationCoefficient;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTotalPoints;
    }
}