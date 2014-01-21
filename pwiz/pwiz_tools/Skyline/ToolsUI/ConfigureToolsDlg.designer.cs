using pwiz.Skyline.Model;

namespace pwiz.Skyline.ToolsUI
{
    partial class ConfigureToolsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ConfigureToolsDlg));
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.comboReport = new System.Windows.Forms.ComboBox();
            this.btnInitialDirectoryMacros = new System.Windows.Forms.Button();
            this.btnArguments = new System.Windows.Forms.Button();
            this.btnInitialDirectory = new System.Windows.Forms.Button();
            this.btnFindCommand = new System.Windows.Forms.Button();
            this.textInitialDirectory = new System.Windows.Forms.TextBox();
            this.textArguments = new System.Windows.Forms.TextBox();
            this.textCommand = new System.Windows.Forms.TextBox();
            this.textTitle = new System.Windows.Forms.TextBox();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.cbOutputImmediateWindow = new System.Windows.Forms.CheckBox();
            this.listTools = new System.Windows.Forms.ListBox();
            this.label5 = new System.Windows.Forms.Label();
            this.labelArguments = new System.Windows.Forms.Label();
            this.labelCommand = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.btnMoveDown = new System.Windows.Forms.Button();
            this.btnMoveUp = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnApply = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.contextMenuAdd = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.customAddContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fromFileAddContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fromWebAddContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuCommand = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.browseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editMacroToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuMacroArguments = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.contextMenuMacroInitialDirectory = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.contextMenuAdd.SuspendLayout();
            this.contextMenuCommand.SuspendLayout();
            this.SuspendLayout();
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 15000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // comboReport
            // 
            resources.ApplyResources(this.comboReport, "comboReport");
            this.comboReport.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboReport.FormattingEnabled = true;
            this.comboReport.Name = "comboReport";
            this.helpTip.SetToolTip(this.comboReport, resources.GetString("comboReport.ToolTip"));
            this.comboReport.SelectedIndexChanged += new System.EventHandler(this.comboReport_SelectedIndexChanged);
            // 
            // btnInitialDirectoryMacros
            // 
            resources.ApplyResources(this.btnInitialDirectoryMacros, "btnInitialDirectoryMacros");
            this.btnInitialDirectoryMacros.Name = "btnInitialDirectoryMacros";
            this.helpTip.SetToolTip(this.btnInitialDirectoryMacros, resources.GetString("btnInitialDirectoryMacros.ToolTip"));
            this.btnInitialDirectoryMacros.UseVisualStyleBackColor = true;
            this.btnInitialDirectoryMacros.Click += new System.EventHandler(this.btnInitialDirectoryMacros_Click);
            // 
            // btnArguments
            // 
            resources.ApplyResources(this.btnArguments, "btnArguments");
            this.btnArguments.Name = "btnArguments";
            this.helpTip.SetToolTip(this.btnArguments, resources.GetString("btnArguments.ToolTip"));
            this.btnArguments.UseVisualStyleBackColor = true;
            this.btnArguments.Click += new System.EventHandler(this.btnArguments_Click);
            // 
            // btnInitialDirectory
            // 
            resources.ApplyResources(this.btnInitialDirectory, "btnInitialDirectory");
            this.btnInitialDirectory.Name = "btnInitialDirectory";
            this.helpTip.SetToolTip(this.btnInitialDirectory, resources.GetString("btnInitialDirectory.ToolTip"));
            this.btnInitialDirectory.UseVisualStyleBackColor = true;
            this.btnInitialDirectory.Click += new System.EventHandler(this.btnInitialDirectory_Click);
            // 
            // btnFindCommand
            // 
            resources.ApplyResources(this.btnFindCommand, "btnFindCommand");
            this.btnFindCommand.Name = "btnFindCommand";
            this.helpTip.SetToolTip(this.btnFindCommand, resources.GetString("btnFindCommand.ToolTip"));
            this.btnFindCommand.UseVisualStyleBackColor = true;
            this.btnFindCommand.Click += new System.EventHandler(this.btnFindCommand_Click);
            // 
            // textInitialDirectory
            // 
            resources.ApplyResources(this.textInitialDirectory, "textInitialDirectory");
            this.textInitialDirectory.Name = "textInitialDirectory";
            this.helpTip.SetToolTip(this.textInitialDirectory, resources.GetString("textInitialDirectory.ToolTip"));
            this.textInitialDirectory.TextChanged += new System.EventHandler(this.textInitialDirectory_TextChanged);
            // 
            // textArguments
            // 
            resources.ApplyResources(this.textArguments, "textArguments");
            this.textArguments.Name = "textArguments";
            this.helpTip.SetToolTip(this.textArguments, resources.GetString("textArguments.ToolTip"));
            this.textArguments.TextChanged += new System.EventHandler(this.textArguments_TextChanged);
            // 
            // textCommand
            // 
            resources.ApplyResources(this.textCommand, "textCommand");
            this.textCommand.Name = "textCommand";
            this.helpTip.SetToolTip(this.textCommand, resources.GetString("textCommand.ToolTip"));
            this.textCommand.TextChanged += new System.EventHandler(this.textCommand_TextChanged);
            // 
            // textTitle
            // 
            resources.ApplyResources(this.textTitle, "textTitle");
            this.textTitle.Name = "textTitle";
            this.helpTip.SetToolTip(this.textTitle, resources.GetString("textTitle.ToolTip"));
            this.textTitle.TextChanged += new System.EventHandler(this.textTitle_TextChanged);
            // 
            // btnAdd
            // 
            resources.ApplyResources(this.btnAdd, "btnAdd");
            this.btnAdd.Name = "btnAdd";
            this.helpTip.SetToolTip(this.btnAdd, resources.GetString("btnAdd.ToolTip"));
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // cbOutputImmediateWindow
            // 
            resources.ApplyResources(this.cbOutputImmediateWindow, "cbOutputImmediateWindow");
            this.cbOutputImmediateWindow.Name = "cbOutputImmediateWindow";
            this.cbOutputImmediateWindow.UseVisualStyleBackColor = true;
            this.cbOutputImmediateWindow.CheckedChanged += new System.EventHandler(this.cbOutputImmediateWindow_CheckedChanged);
            // 
            // listTools
            // 
            resources.ApplyResources(this.listTools, "listTools");
            this.listTools.FormattingEnabled = true;
            this.listTools.Name = "listTools";
            this.listTools.SelectedIndexChanged += new System.EventHandler(this.listTools_SelectedIndexChanged);
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // labelArguments
            // 
            resources.ApplyResources(this.labelArguments, "labelArguments");
            this.labelArguments.Name = "labelArguments";
            // 
            // labelCommand
            // 
            resources.ApplyResources(this.labelCommand, "labelCommand");
            this.labelCommand.Name = "labelCommand";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // btnMoveDown
            // 
            resources.ApplyResources(this.btnMoveDown, "btnMoveDown");
            this.btnMoveDown.Name = "btnMoveDown";
            this.btnMoveDown.UseVisualStyleBackColor = true;
            this.btnMoveDown.Click += new System.EventHandler(this.btnMoveDown_Click);
            // 
            // btnMoveUp
            // 
            resources.ApplyResources(this.btnMoveUp, "btnMoveUp");
            this.btnMoveUp.Name = "btnMoveUp";
            this.btnMoveUp.UseVisualStyleBackColor = true;
            this.btnMoveUp.Click += new System.EventHandler(this.btnMoveUp_Click);
            // 
            // btnRemove
            // 
            resources.ApplyResources(this.btnRemove, "btnRemove");
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.EnabledChanged += new System.EventHandler(this.btnRemove_EnabledChanged);
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnApply
            // 
            resources.ApplyResources(this.btnApply, "btnApply");
            this.btnApply.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnApply.Name = "btnApply";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // contextMenuAdd
            // 
            this.contextMenuAdd.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.customAddContextMenuItem,
            this.fromFileAddContextMenuItem,
            this.fromWebAddContextMenuItem});
            this.contextMenuAdd.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuAdd, "contextMenuAdd");
            // 
            // customAddContextMenuItem
            // 
            this.customAddContextMenuItem.Name = "customAddContextMenuItem";
            resources.ApplyResources(this.customAddContextMenuItem, "customAddContextMenuItem");
            this.customAddContextMenuItem.Click += new System.EventHandler(this.customAddContextMenuItem_Click);
            // 
            // fromFileAddContextMenuItem
            // 
            this.fromFileAddContextMenuItem.Name = "fromFileAddContextMenuItem";
            resources.ApplyResources(this.fromFileAddContextMenuItem, "fromFileAddContextMenuItem");
            this.fromFileAddContextMenuItem.Click += new System.EventHandler(this.fromFileAddContextMenuItem_Click);
            // 
            // fromWebAddContextMenuItem
            // 
            this.fromWebAddContextMenuItem.Name = "fromWebAddContextMenuItem";
            resources.ApplyResources(this.fromWebAddContextMenuItem, "fromWebAddContextMenuItem");
            this.fromWebAddContextMenuItem.Click += new System.EventHandler(this.fromWebAddContextMenuItem_Click);
            // 
            // contextMenuCommand
            // 
            this.contextMenuCommand.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.browseToolStripMenuItem,
            this.editMacroToolStripMenuItem});
            this.contextMenuCommand.Name = "contextMenuCommand";
            resources.ApplyResources(this.contextMenuCommand, "contextMenuCommand");
            // 
            // browseToolStripMenuItem
            // 
            this.browseToolStripMenuItem.Name = "browseToolStripMenuItem";
            resources.ApplyResources(this.browseToolStripMenuItem, "browseToolStripMenuItem");
            this.browseToolStripMenuItem.Click += new System.EventHandler(this.browseToolStripMenuItem_Click);
            // 
            // editMacroToolStripMenuItem
            // 
            this.editMacroToolStripMenuItem.Name = "editMacroToolStripMenuItem";
            resources.ApplyResources(this.editMacroToolStripMenuItem, "editMacroToolStripMenuItem");
            this.editMacroToolStripMenuItem.Click += new System.EventHandler(this.editMacroToolStripMenuItem_Click);
            // 
            // contextMenuMacroArguments
            // 
            this.contextMenuMacroArguments.Name = "contextMenuMacroArguments";
            resources.ApplyResources(this.contextMenuMacroArguments, "contextMenuMacroArguments");
            // 
            // contextMenuMacroInitialDirectory
            // 
            this.contextMenuMacroInitialDirectory.Name = "contextMenuMacroInitialDirectory";
            resources.ApplyResources(this.contextMenuMacroInitialDirectory, "contextMenuMacroInitialDirectory");
            // 
            // ConfigureToolsDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.label6);
            this.Controls.Add(this.comboReport);
            this.Controls.Add(this.cbOutputImmediateWindow);
            this.Controls.Add(this.btnInitialDirectoryMacros);
            this.Controls.Add(this.btnArguments);
            this.Controls.Add(this.btnInitialDirectory);
            this.Controls.Add(this.btnFindCommand);
            this.Controls.Add(this.listTools);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.labelArguments);
            this.Controls.Add(this.labelCommand);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textInitialDirectory);
            this.Controls.Add(this.textArguments);
            this.Controls.Add(this.textCommand);
            this.Controls.Add(this.textTitle);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnMoveDown);
            this.Controls.Add(this.btnMoveUp);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.btnApply);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConfigureToolsDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ConfigureToolsDlg_KeyDown);
            this.contextMenuAdd.ResumeLayout(false);
            this.contextMenuCommand.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        public System.Windows.Forms.Button btnApply;
        public System.Windows.Forms.Button btnAdd;
        public System.Windows.Forms.Button btnRemove;
        public System.Windows.Forms.Button btnMoveUp;
        public System.Windows.Forms.Button btnMoveDown;
        private System.Windows.Forms.Label label1;
        public System.Windows.Forms.TextBox textTitle;
        public System.Windows.Forms.TextBox textCommand;
        public System.Windows.Forms.TextBox textArguments;
        public System.Windows.Forms.TextBox textInitialDirectory;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label labelCommand;
        private System.Windows.Forms.Label labelArguments;
        private System.Windows.Forms.Label label5;
        public System.Windows.Forms.ListBox listTools;
        public System.Windows.Forms.Button btnFindCommand;
        public System.Windows.Forms.Button btnInitialDirectory;
        public System.Windows.Forms.Button btnArguments;
        public System.Windows.Forms.Button btnInitialDirectoryMacros;
        public System.Windows.Forms.CheckBox cbOutputImmediateWindow;
        public System.Windows.Forms.ComboBox comboReport;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.ContextMenuStrip contextMenuAdd;
        private System.Windows.Forms.ToolStripMenuItem customAddContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fromFileAddContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fromWebAddContextMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuCommand;
        private System.Windows.Forms.ToolStripMenuItem browseToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editMacroToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuMacroArguments;
        private System.Windows.Forms.ContextMenuStrip contextMenuMacroInitialDirectory;
    }
}