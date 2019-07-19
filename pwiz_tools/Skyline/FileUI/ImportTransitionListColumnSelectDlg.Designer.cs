namespace pwiz.Skyline.FileUI
{
    partial class ImportTransitionListColumnSelectDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportTransitionListColumnSelectDlg));
            this.dataGrid = new pwiz.Skyline.Controls.DataGridViewEx();
            this.buttonOk = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.fileLabel = new System.Windows.Forms.Label();
            this.comboPanelOuter = new System.Windows.Forms.Panel();
            this.comboPanelInner = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.dataGrid)).BeginInit();
            this.comboPanelOuter.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGrid
            // 
            this.dataGrid.AllowUserToAddRows = false;
            this.dataGrid.AllowUserToDeleteRows = false;
            this.dataGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGrid.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dataGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGrid.Location = new System.Drawing.Point(26, 36);
            this.dataGrid.MaximumColumnCount = null;
            this.dataGrid.Name = "dataGrid";
            this.dataGrid.ReadOnly = true;
            this.dataGrid.RowHeadersVisible = false;
            this.dataGrid.Size = new System.Drawing.Size(740, 290);
            this.dataGrid.TabIndex = 4;
            this.dataGrid.ColumnHeadersHeightChanged += new System.EventHandler(this.dataGrid_ColumnHeadersHeightChanged);
            this.dataGrid.ColumnWidthChanged += new System.Windows.Forms.DataGridViewColumnEventHandler(this.dataGrid_ColumnWidthChanged);
            this.dataGrid.Scroll += new System.Windows.Forms.ScrollEventHandler(this.dataGrid_Scroll);
            // 
            // buttonOk
            // 
            this.buttonOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOk.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.buttonOk.Location = new System.Drawing.Point(784, 30);
            this.buttonOk.Name = "buttonOk";
            this.buttonOk.Size = new System.Drawing.Size(75, 23);
            this.buttonOk.TabIndex = 5;
            this.buttonOk.Text = "OK";
            this.buttonOk.UseVisualStyleBackColor = true;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.buttonCancel.Location = new System.Drawing.Point(784, 59);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 6;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // fileLabel
            // 
            this.fileLabel.AutoSize = true;
            this.fileLabel.Location = new System.Drawing.Point(23, 9);
            this.fileLabel.Name = "fileLabel";
            this.fileLabel.Size = new System.Drawing.Size(47, 13);
            this.fileLabel.TabIndex = 7;
            this.fileLabel.Text = "CSV File";
            // 
            // comboPanelOuter
            // 
            this.comboPanelOuter.Controls.Add(this.comboPanelInner);
            this.comboPanelOuter.Location = new System.Drawing.Point(291, 131);
            this.comboPanelOuter.Name = "comboPanelOuter";
            this.comboPanelOuter.Size = new System.Drawing.Size(200, 100);
            this.comboPanelOuter.TabIndex = 8;
            // 
            // comboPanelInner
            // 
            this.comboPanelInner.Location = new System.Drawing.Point(77, 41);
            this.comboPanelInner.Name = "comboPanelInner";
            this.comboPanelInner.Size = new System.Drawing.Size(81, 35);
            this.comboPanelInner.TabIndex = 0;
            // 
            // ImportTransitionListColumnSelectDlg
            // 
            this.AcceptButton = this.buttonOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(884, 361);
            this.Controls.Add(this.comboPanelOuter);
            this.Controls.Add(this.fileLabel);
            this.Controls.Add(this.dataGrid);
            this.Controls.Add(this.buttonOk);
            this.Controls.Add(this.buttonCancel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportTransitionListColumnSelectDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import Transition List";
            ((System.ComponentModel.ISupportInitialize)(this.dataGrid)).EndInit();
            this.comboPanelOuter.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Controls.DataGridViewEx dataGrid;
        private System.Windows.Forms.Button buttonOk;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Label fileLabel;
        private System.Windows.Forms.Panel comboPanelOuter;
        private System.Windows.Forms.Panel comboPanelInner;
    }
}