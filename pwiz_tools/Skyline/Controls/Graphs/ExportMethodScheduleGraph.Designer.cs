namespace pwiz.Skyline.Controls.Graphs
{
    partial class ExportMethodScheduleGraph
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExportMethodScheduleGraph));
            this.graphControl = new ZedGraph.ZedGraphControl();
            this.btnOk = new System.Windows.Forms.Button();
            this.cbGraphType = new System.Windows.Forms.ComboBox();
            this.dataGridView = new pwiz.Common.Controls.CommonDataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // graphControl
            // 
            resources.ApplyResources(this.graphControl, "graphControl");
            this.graphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphControl.IsShowCopyMessage = false;
            this.graphControl.IsShowPointValues = true;
            this.graphControl.Name = "graphControl";
            this.graphControl.ScrollGrace = 0D;
            this.graphControl.ScrollMaxX = 0D;
            this.graphControl.ScrollMaxY = 0D;
            this.graphControl.ScrollMaxY2 = 0D;
            this.graphControl.ScrollMinX = 0D;
            this.graphControl.ScrollMinY = 0D;
            this.graphControl.ScrollMinY2 = 0D;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // cbGraphType
            // 
            resources.ApplyResources(this.cbGraphType, "cbGraphType");
            this.cbGraphType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbGraphType.FormattingEnabled = true;
            this.cbGraphType.Name = "cbGraphType";
            this.cbGraphType.SelectedIndexChanged += new System.EventHandler(this.cbGraphType_SelectedIndexChanged);
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            resources.ApplyResources(this.dataGridView, "dataGridView");
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            // 
            // ExportMethodScheduleGraph
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOk;
            this.Controls.Add(this.dataGridView);
            this.Controls.Add(this.cbGraphType);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.graphControl);
            this.MinimizeBox = false;
            this.Name = "ExportMethodScheduleGraph";
            this.ShowInTaskbar = false;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ExportMethodScheduleGraph_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private ZedGraph.ZedGraphControl graphControl;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.ComboBox cbGraphType;
        private pwiz.Common.Controls.CommonDataGridView dataGridView;
    }
}