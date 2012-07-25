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
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1080, 102);
            this.panel1.TabIndex = 0;
            // 
            // lblFormDescription
            // 
            this.lblFormDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblFormDescription.AutoEllipsis = true;
            this.lblFormDescription.Location = new System.Drawing.Point(12, 9);
            this.lblFormDescription.Name = "lblFormDescription";
            this.lblFormDescription.Size = new System.Drawing.Size(1056, 58);
            this.lblFormDescription.TabIndex = 2;
            this.lblFormDescription.Text = resources.GetString("lblFormDescription.Text");
            // 
            // comboAlignAgainst
            // 
            this.comboAlignAgainst.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAlignAgainst.FormattingEnabled = true;
            this.comboAlignAgainst.Location = new System.Drawing.Point(136, 70);
            this.comboAlignAgainst.Name = "comboAlignAgainst";
            this.comboAlignAgainst.Size = new System.Drawing.Size(307, 21);
            this.comboAlignAgainst.TabIndex = 1;
            this.comboAlignAgainst.SelectedIndexChanged += new System.EventHandler(this.comboAlignAgainst_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 76);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(124, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Align Replicates Against:";
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
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 25);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.Size = new System.Drawing.Size(1080, 163);
            this.dataGridView1.TabIndex = 1;
            // 
            // bindingSource
            // 
            this.bindingSource.CurrentItemChanged += new System.EventHandler(this.bindingSource_CurrentItemChanged);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 102);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dataGridView1);
            this.splitContainer1.Panel1.Controls.Add(this.bindingNavigator1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.zedGraphControl);
            this.splitContainer1.Size = new System.Drawing.Size(1080, 378);
            this.splitContainer1.SplitterDistance = 188;
            this.splitContainer1.TabIndex = 2;
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
            this.bindingNavigator1.Location = new System.Drawing.Point(0, 0);
            this.bindingNavigator1.MoveFirstItem = this.bindingNavigatorMoveFirstItem;
            this.bindingNavigator1.MoveLastItem = this.bindingNavigatorMoveLastItem;
            this.bindingNavigator1.MoveNextItem = this.bindingNavigatorMoveNextItem;
            this.bindingNavigator1.MovePreviousItem = this.bindingNavigatorMovePreviousItem;
            this.bindingNavigator1.Name = "bindingNavigator1";
            this.bindingNavigator1.PositionItem = this.bindingNavigatorPositionItem;
            this.bindingNavigator1.Size = new System.Drawing.Size(1080, 25);
            this.bindingNavigator1.TabIndex = 2;
            this.bindingNavigator1.Text = "bindingNavigator1";
            // 
            // bindingNavigatorAddNewItem
            // 
            this.bindingNavigatorAddNewItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.bindingNavigatorAddNewItem.Image = ((System.Drawing.Image)(resources.GetObject("bindingNavigatorAddNewItem.Image")));
            this.bindingNavigatorAddNewItem.Name = "bindingNavigatorAddNewItem";
            this.bindingNavigatorAddNewItem.RightToLeftAutoMirrorImage = true;
            this.bindingNavigatorAddNewItem.Size = new System.Drawing.Size(23, 22);
            this.bindingNavigatorAddNewItem.Text = "Add new";
            // 
            // bindingNavigatorCountItem
            // 
            this.bindingNavigatorCountItem.Name = "bindingNavigatorCountItem";
            this.bindingNavigatorCountItem.Size = new System.Drawing.Size(35, 22);
            this.bindingNavigatorCountItem.Text = "of {0}";
            this.bindingNavigatorCountItem.ToolTipText = "Total number of items";
            // 
            // bindingNavigatorDeleteItem
            // 
            this.bindingNavigatorDeleteItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.bindingNavigatorDeleteItem.Image = ((System.Drawing.Image)(resources.GetObject("bindingNavigatorDeleteItem.Image")));
            this.bindingNavigatorDeleteItem.Name = "bindingNavigatorDeleteItem";
            this.bindingNavigatorDeleteItem.RightToLeftAutoMirrorImage = true;
            this.bindingNavigatorDeleteItem.Size = new System.Drawing.Size(23, 22);
            this.bindingNavigatorDeleteItem.Text = "Delete";
            // 
            // bindingNavigatorMoveFirstItem
            // 
            this.bindingNavigatorMoveFirstItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.bindingNavigatorMoveFirstItem.Image = ((System.Drawing.Image)(resources.GetObject("bindingNavigatorMoveFirstItem.Image")));
            this.bindingNavigatorMoveFirstItem.Name = "bindingNavigatorMoveFirstItem";
            this.bindingNavigatorMoveFirstItem.RightToLeftAutoMirrorImage = true;
            this.bindingNavigatorMoveFirstItem.Size = new System.Drawing.Size(23, 22);
            this.bindingNavigatorMoveFirstItem.Text = "Move first";
            // 
            // bindingNavigatorMovePreviousItem
            // 
            this.bindingNavigatorMovePreviousItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.bindingNavigatorMovePreviousItem.Image = ((System.Drawing.Image)(resources.GetObject("bindingNavigatorMovePreviousItem.Image")));
            this.bindingNavigatorMovePreviousItem.Name = "bindingNavigatorMovePreviousItem";
            this.bindingNavigatorMovePreviousItem.RightToLeftAutoMirrorImage = true;
            this.bindingNavigatorMovePreviousItem.Size = new System.Drawing.Size(23, 22);
            this.bindingNavigatorMovePreviousItem.Text = "Move previous";
            // 
            // bindingNavigatorSeparator
            // 
            this.bindingNavigatorSeparator.Name = "bindingNavigatorSeparator";
            this.bindingNavigatorSeparator.Size = new System.Drawing.Size(6, 25);
            // 
            // bindingNavigatorPositionItem
            // 
            this.bindingNavigatorPositionItem.AccessibleName = "Position";
            this.bindingNavigatorPositionItem.AutoSize = false;
            this.bindingNavigatorPositionItem.Name = "bindingNavigatorPositionItem";
            this.bindingNavigatorPositionItem.Size = new System.Drawing.Size(50, 23);
            this.bindingNavigatorPositionItem.Text = "0";
            this.bindingNavigatorPositionItem.ToolTipText = "Current position";
            // 
            // bindingNavigatorSeparator1
            // 
            this.bindingNavigatorSeparator1.Name = "bindingNavigatorSeparator1";
            this.bindingNavigatorSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // bindingNavigatorMoveNextItem
            // 
            this.bindingNavigatorMoveNextItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.bindingNavigatorMoveNextItem.Image = ((System.Drawing.Image)(resources.GetObject("bindingNavigatorMoveNextItem.Image")));
            this.bindingNavigatorMoveNextItem.Name = "bindingNavigatorMoveNextItem";
            this.bindingNavigatorMoveNextItem.RightToLeftAutoMirrorImage = true;
            this.bindingNavigatorMoveNextItem.Size = new System.Drawing.Size(23, 22);
            this.bindingNavigatorMoveNextItem.Text = "Move next";
            // 
            // bindingNavigatorMoveLastItem
            // 
            this.bindingNavigatorMoveLastItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.bindingNavigatorMoveLastItem.Image = ((System.Drawing.Image)(resources.GetObject("bindingNavigatorMoveLastItem.Image")));
            this.bindingNavigatorMoveLastItem.Name = "bindingNavigatorMoveLastItem";
            this.bindingNavigatorMoveLastItem.RightToLeftAutoMirrorImage = true;
            this.bindingNavigatorMoveLastItem.Size = new System.Drawing.Size(23, 22);
            this.bindingNavigatorMoveLastItem.Text = "Move last";
            // 
            // bindingNavigatorSeparator2
            // 
            this.bindingNavigatorSeparator2.Name = "bindingNavigatorSeparator2";
            this.bindingNavigatorSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // zedGraphControl
            // 
            this.zedGraphControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.zedGraphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.zedGraphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.zedGraphControl.IsEnableVPan = false;
            this.zedGraphControl.IsEnableVZoom = false;
            this.zedGraphControl.IsShowPointValues = true;
            this.zedGraphControl.Location = new System.Drawing.Point(0, 0);
            this.zedGraphControl.Name = "zedGraphControl";
            this.zedGraphControl.ScrollGrace = 0D;
            this.zedGraphControl.ScrollMaxX = 0D;
            this.zedGraphControl.ScrollMaxY = 0D;
            this.zedGraphControl.ScrollMaxY2 = 0D;
            this.zedGraphControl.ScrollMinX = 0D;
            this.zedGraphControl.ScrollMinY = 0D;
            this.zedGraphControl.ScrollMinY2 = 0D;
            this.zedGraphControl.Size = new System.Drawing.Size(1080, 186);
            this.zedGraphControl.TabIndex = 0;
            this.zedGraphControl.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.zedGraphControl_ContextMenuBuilder);
            // 
            // colLibrary
            // 
            this.colLibrary.DataPropertyName = "Library";
            this.colLibrary.HeaderText = "Library";
            this.colLibrary.Name = "colLibrary";
            this.colLibrary.ReadOnly = true;
            // 
            // colDataFile
            // 
            this.colDataFile.DataPropertyName = "DataFile";
            this.colDataFile.HeaderText = "DataFile";
            this.colDataFile.Name = "colDataFile";
            this.colDataFile.ReadOnly = true;
            // 
            // colSlope
            // 
            this.colSlope.DataPropertyName = "Slope";
            this.colSlope.HeaderText = "Slope";
            this.colSlope.Name = "colSlope";
            this.colSlope.ReadOnly = true;
            // 
            // colIntercept
            // 
            this.colIntercept.DataPropertyName = "Intercept";
            this.colIntercept.HeaderText = "Intercept";
            this.colIntercept.Name = "colIntercept";
            this.colIntercept.ReadOnly = true;
            // 
            // colCorrelationCoefficient
            // 
            this.colCorrelationCoefficient.DataPropertyName = "CorrelationCoefficient";
            this.colCorrelationCoefficient.HeaderText = "R";
            this.colCorrelationCoefficient.Name = "colCorrelationCoefficient";
            this.colCorrelationCoefficient.ReadOnly = true;
            // 
            // colOutlierCount
            // 
            this.colOutlierCount.DataPropertyName = "OutlierCount";
            this.colOutlierCount.HeaderText = "# Outliers";
            this.colOutlierCount.Name = "colOutlierCount";
            this.colOutlierCount.ReadOnly = true;
            // 
            // colUnrefinedSlope
            // 
            this.colUnrefinedSlope.DataPropertyName = "UnrefinedSlope";
            this.colUnrefinedSlope.HeaderText = "Unrefined Slope";
            this.colUnrefinedSlope.Name = "colUnrefinedSlope";
            this.colUnrefinedSlope.ReadOnly = true;
            this.colUnrefinedSlope.Visible = false;
            this.colUnrefinedSlope.Width = 120;
            // 
            // colUnrefinedIntercept
            // 
            this.colUnrefinedIntercept.DataPropertyName = "UnrefinedIntercept";
            this.colUnrefinedIntercept.HeaderText = "Unrefined Intercept";
            this.colUnrefinedIntercept.Name = "colUnrefinedIntercept";
            this.colUnrefinedIntercept.ReadOnly = true;
            this.colUnrefinedIntercept.Visible = false;
            this.colUnrefinedIntercept.Width = 125;
            // 
            // colUnrefinedCorrelationCoefficient
            // 
            this.colUnrefinedCorrelationCoefficient.DataPropertyName = "UnrefinedCorrelationCoefficient";
            this.colUnrefinedCorrelationCoefficient.HeaderText = "Unrefined R";
            this.colUnrefinedCorrelationCoefficient.Name = "colUnrefinedCorrelationCoefficient";
            this.colUnrefinedCorrelationCoefficient.ReadOnly = true;
            this.colUnrefinedCorrelationCoefficient.Visible = false;
            // 
            // colTotalPoints
            // 
            this.colTotalPoints.DataPropertyName = "PointCount";
            this.colTotalPoints.HeaderText = "# Points";
            this.colTotalPoints.Name = "colTotalPoints";
            this.colTotalPoints.ReadOnly = true;
            // 
            // AlignmentForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(1080, 480);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.panel1);
            this.Name = "AlignmentForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Retention time alignment";
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