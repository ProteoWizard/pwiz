namespace pwiz.Common.DataBinding.Controls.Editor
{
    partial class ViewEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ViewEditor));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxViewName = new System.Windows.Forms.TextBox();
            this.tabPageFilter = new System.Windows.Forms.TabPage();
            this.tabPageColumns = new System.Windows.Forms.TabPage();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageSource = new System.Windows.Forms.TabPage();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.toolButtonUndo = new System.Windows.Forms.ToolStripButton();
            this.toolButtonRedo = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolButtonShowAdvanced = new System.Windows.Forms.ToolStripButton();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnPreview = new System.Windows.Forms.Button();
            this.panelViewEditor = new System.Windows.Forms.Panel();
            this.panelButtons = new System.Windows.Forms.Panel();
            this.tabControl1.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panelViewEditor.SuspendLayout();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(544, 6);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(463, 6);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 6);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(64, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "View &Name:";
            // 
            // tbxViewName
            // 
            this.tbxViewName.Location = new System.Drawing.Point(71, 3);
            this.tbxViewName.Name = "tbxViewName";
            this.tbxViewName.Size = new System.Drawing.Size(214, 20);
            this.tbxViewName.TabIndex = 0;
            // 
            // tabPageFilter
            // 
            this.tabPageFilter.Location = new System.Drawing.Point(4, 22);
            this.tabPageFilter.Name = "tabPageFilter";
            this.tabPageFilter.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageFilter.Size = new System.Drawing.Size(614, 309);
            this.tabPageFilter.TabIndex = 1;
            this.tabPageFilter.Text = "Filter";
            this.tabPageFilter.UseVisualStyleBackColor = true;
            // 
            // tabPageColumns
            // 
            this.tabPageColumns.Location = new System.Drawing.Point(4, 22);
            this.tabPageColumns.Name = "tabPageColumns";
            this.tabPageColumns.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageColumns.Size = new System.Drawing.Size(614, 309);
            this.tabPageColumns.TabIndex = 0;
            this.tabPageColumns.Text = "Columns";
            this.tabPageColumns.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPageColumns);
            this.tabControl1.Controls.Add(this.tabPageFilter);
            this.tabControl1.Controls.Add(this.tabPageSource);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 25);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(622, 335);
            this.tabControl1.TabIndex = 2;
            // 
            // tabPageSource
            // 
            this.tabPageSource.Location = new System.Drawing.Point(4, 22);
            this.tabPageSource.Name = "tabPageSource";
            this.tabPageSource.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageSource.Size = new System.Drawing.Size(614, 309);
            this.tabPageSource.TabIndex = 3;
            this.tabPageSource.Text = "Source";
            this.tabPageSource.UseVisualStyleBackColor = true;
            // 
            // toolStrip
            // 
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolButtonUndo,
            this.toolButtonRedo,
            this.toolStripSeparator1,
            this.toolButtonShowAdvanced});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(622, 25);
            this.toolStrip.TabIndex = 5;
            this.toolStrip.Text = "toolStrip1";
            // 
            // toolButtonUndo
            // 
            this.toolButtonUndo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolButtonUndo.Enabled = false;
            this.toolButtonUndo.Image = ((System.Drawing.Image)(resources.GetObject("toolButtonUndo.Image")));
            this.toolButtonUndo.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolButtonUndo.Name = "toolButtonUndo";
            this.toolButtonUndo.Size = new System.Drawing.Size(23, 22);
            this.toolButtonUndo.Text = "Undo";
            this.toolButtonUndo.Click += new System.EventHandler(this.toolButtonUndo_Click);
            // 
            // toolButtonRedo
            // 
            this.toolButtonRedo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolButtonRedo.Enabled = false;
            this.toolButtonRedo.Image = ((System.Drawing.Image)(resources.GetObject("toolButtonRedo.Image")));
            this.toolButtonRedo.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolButtonRedo.Name = "toolButtonRedo";
            this.toolButtonRedo.Size = new System.Drawing.Size(23, 22);
            this.toolButtonRedo.Text = "Redo";
            this.toolButtonRedo.Click += new System.EventHandler(this.toolButtonRedo_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // toolButtonShowAdvanced
            // 
            this.toolButtonShowAdvanced.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolButtonShowAdvanced.Image = ((System.Drawing.Image)(resources.GetObject("toolButtonShowAdvanced.Image")));
            this.toolButtonShowAdvanced.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolButtonShowAdvanced.Name = "toolButtonShowAdvanced";
            this.toolButtonShowAdvanced.Size = new System.Drawing.Size(96, 22);
            this.toolButtonShowAdvanced.Text = "Show Advanced";
            this.toolButtonShowAdvanced.Visible = false;
            this.toolButtonShowAdvanced.Click += new System.EventHandler(this.toolButtonShowAdvanced_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnPreview);
            this.panel1.Controls.Add(this.tbxViewName);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(622, 25);
            this.panel1.TabIndex = 7;
            // 
            // btnPreview
            // 
            this.btnPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPreview.Location = new System.Drawing.Point(535, 0);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(75, 23);
            this.btnPreview.TabIndex = 4;
            this.btnPreview.Text = "Preview...";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Visible = false;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);
            // 
            // panelViewEditor
            // 
            this.panelViewEditor.Controls.Add(this.tabControl1);
            this.panelViewEditor.Controls.Add(this.panel1);
            this.panelViewEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelViewEditor.Location = new System.Drawing.Point(0, 25);
            this.panelViewEditor.Name = "panelViewEditor";
            this.panelViewEditor.Size = new System.Drawing.Size(622, 360);
            this.panelViewEditor.TabIndex = 8;
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnCancel);
            this.panelButtons.Controls.Add(this.btnOK);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelButtons.Location = new System.Drawing.Point(0, 385);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(622, 32);
            this.panelButtons.TabIndex = 9;
            // 
            // ViewEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(622, 417);
            this.Controls.Add(this.panelViewEditor);
            this.Controls.Add(this.panelButtons);
            this.Controls.Add(this.toolStrip);
            this.Name = "ViewEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Customize View";
            this.Load += new System.EventHandler(this.OnLoad);
            this.tabControl1.ResumeLayout(false);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panelViewEditor.ResumeLayout(false);
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxViewName;
        private System.Windows.Forms.TabPage tabPageFilter;
        private System.Windows.Forms.TabPage tabPageColumns;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPageSource;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton toolButtonUndo;
        private System.Windows.Forms.ToolStripButton toolButtonRedo;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton toolButtonShowAdvanced;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panelViewEditor;
        private System.Windows.Forms.Panel panelButtons;
        private System.Windows.Forms.Button btnPreview;
    }
}