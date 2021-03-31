﻿namespace pwiz.Skyline.FileUI
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
            this.buttonOk = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonCheckForErrors = new System.Windows.Forms.Button();
            this.comboPanelOuter = new System.Windows.Forms.Panel();
            this.comboPanelInner = new System.Windows.Forms.Panel();
            this.fileLabel = new System.Windows.Forms.Label();
            this.dataGrid = new pwiz.Skyline.Controls.DataGridViewEx();
            this.comboPanelOuter.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonOk
            // 
            resources.ApplyResources(this.buttonOk, "buttonOk");
            this.buttonOk.Name = "buttonOk";
            this.buttonOk.UseVisualStyleBackColor = true;
            this.buttonOk.Click += new System.EventHandler(this.ButtonOk_Click);
            // 
            // buttonCancel
            // 
            resources.ApplyResources(this.buttonCancel, "buttonCancel");
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // buttonCheckForErrors
            // 
            resources.ApplyResources(this.buttonCheckForErrors, "buttonCheckForErrors");
            this.buttonCheckForErrors.Name = "buttonCheckForErrors";
            this.buttonCheckForErrors.UseVisualStyleBackColor = true;
            this.buttonCheckForErrors.Click += new System.EventHandler(this.ButtonCheckForErrors_Click);
            // 
            // comboPanelOuter
            // 
            this.comboPanelOuter.Controls.Add(this.comboPanelInner);
            resources.ApplyResources(this.comboPanelOuter, "comboPanelOuter");
            this.comboPanelOuter.Name = "comboPanelOuter";
            // 
            // comboPanelInner
            // 
            resources.ApplyResources(this.comboPanelInner, "comboPanelInner");
            this.comboPanelInner.Name = "comboPanelInner";
            // 
            // fileLabel
            // 
            resources.ApplyResources(this.fileLabel, "fileLabel");
            this.fileLabel.Name = "fileLabel";
            // 
            // dataGrid
            // 
            this.dataGrid.AllowUserToAddRows = false;
            this.dataGrid.AllowUserToDeleteRows = false;
            resources.ApplyResources(this.dataGrid, "dataGrid");
            this.dataGrid.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dataGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGrid.Name = "dataGrid";
            this.dataGrid.ReadOnly = true;
            this.dataGrid.RowHeadersVisible = false;
            this.dataGrid.ColumnHeadersHeightChanged += new System.EventHandler(this.DataGrid_ColumnHeadersHeightChanged);
            this.dataGrid.ColumnAdded += new System.Windows.Forms.DataGridViewColumnEventHandler(this.dataGrid_ColumnAdded);
            this.dataGrid.ColumnWidthChanged += new System.Windows.Forms.DataGridViewColumnEventHandler(this.DataGrid_ColumnWidthChanged);
            this.dataGrid.Scroll += new System.Windows.Forms.ScrollEventHandler(this.DataGrid_Scroll);
            // 
            // ImportTransitionListColumnSelectDlg
            // 
            this.AcceptButton = this.buttonOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.Controls.Add(this.buttonCheckForErrors);
            this.Controls.Add(this.comboPanelOuter);
            this.Controls.Add(this.fileLabel);
            this.Controls.Add(this.dataGrid);
            this.Controls.Add(this.buttonOk);
            this.Controls.Add(this.buttonCancel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportTransitionListColumnSelectDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Shown += OnColumnsShown;
            this.Resize += new System.EventHandler(this.form_Resize);
            this.comboPanelOuter.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public Controls.DataGridViewEx dataGrid; // Public for testing only
        public System.Windows.Forms.Button buttonOk; // Public for testing only
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Label fileLabel;
        private System.Windows.Forms.Panel comboPanelOuter;
        private System.Windows.Forms.Panel comboPanelInner;
        public System.Windows.Forms.Button buttonCheckForErrors; // Public for testing only
    }
}