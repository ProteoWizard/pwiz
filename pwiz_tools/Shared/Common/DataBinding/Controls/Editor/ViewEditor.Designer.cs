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
            this.toolButtonFind = new System.Windows.Forms.ToolStripButton();
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
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // tbxViewName
            // 
            resources.ApplyResources(this.tbxViewName, "tbxViewName");
            this.tbxViewName.Name = "tbxViewName";
            // 
            // tabPageFilter
            // 
            resources.ApplyResources(this.tabPageFilter, "tabPageFilter");
            this.tabPageFilter.Name = "tabPageFilter";
            this.tabPageFilter.UseVisualStyleBackColor = true;
            // 
            // tabPageColumns
            // 
            resources.ApplyResources(this.tabPageColumns, "tabPageColumns");
            this.tabPageColumns.Name = "tabPageColumns";
            this.tabPageColumns.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPageColumns);
            this.tabControl1.Controls.Add(this.tabPageFilter);
            this.tabControl1.Controls.Add(this.tabPageSource);
            resources.ApplyResources(this.tabControl1, "tabControl1");
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            // 
            // tabPageSource
            // 
            resources.ApplyResources(this.tabPageSource, "tabPageSource");
            this.tabPageSource.Name = "tabPageSource";
            this.tabPageSource.UseVisualStyleBackColor = true;
            // 
            // toolStrip
            // 
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolButtonUndo,
            this.toolButtonRedo,
            this.toolStripSeparator1,
            this.toolButtonFind,
            this.toolButtonShowAdvanced});
            resources.ApplyResources(this.toolStrip, "toolStrip");
            this.toolStrip.Name = "toolStrip";
            // 
            // toolButtonUndo
            // 
            this.toolButtonUndo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.toolButtonUndo, "toolButtonUndo");
            this.toolButtonUndo.Name = "toolButtonUndo";
            this.toolButtonUndo.Click += new System.EventHandler(this.toolButtonUndo_Click);
            // 
            // toolButtonRedo
            // 
            this.toolButtonRedo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.toolButtonRedo, "toolButtonRedo");
            this.toolButtonRedo.Name = "toolButtonRedo";
            this.toolButtonRedo.Click += new System.EventHandler(this.toolButtonRedo_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            // 
            // toolButtonFind
            // 
            this.toolButtonFind.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.toolButtonFind, "toolButtonFind");
            this.toolButtonFind.Name = "toolButtonFind";
            this.toolButtonFind.Click += new System.EventHandler(this.toolButtonFind_Click);
            // 
            // toolButtonShowAdvanced
            // 
            this.toolButtonShowAdvanced.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            resources.ApplyResources(this.toolButtonShowAdvanced, "toolButtonShowAdvanced");
            this.toolButtonShowAdvanced.Name = "toolButtonShowAdvanced";
            this.toolButtonShowAdvanced.Click += new System.EventHandler(this.toolButtonShowAdvanced_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnPreview);
            this.panel1.Controls.Add(this.tbxViewName);
            this.panel1.Controls.Add(this.label2);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // btnPreview
            // 
            resources.ApplyResources(this.btnPreview, "btnPreview");
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);
            // 
            // panelViewEditor
            // 
            this.panelViewEditor.Controls.Add(this.tabControl1);
            this.panelViewEditor.Controls.Add(this.panel1);
            resources.ApplyResources(this.panelViewEditor, "panelViewEditor");
            this.panelViewEditor.Name = "panelViewEditor";
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnCancel);
            this.panelButtons.Controls.Add(this.btnOK);
            resources.ApplyResources(this.panelButtons, "panelButtons");
            this.panelButtons.Name = "panelButtons";
            // 
            // ViewEditor
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.panelViewEditor);
            this.Controls.Add(this.panelButtons);
            this.Controls.Add(this.toolStrip);
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ViewEditor";
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.OnLoad);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ViewEditor_KeyDown);
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
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.ToolStripButton toolButtonFind;
    }
}