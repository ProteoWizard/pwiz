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
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
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
            this.comboReport.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.comboReport.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboReport.FormattingEnabled = true;
            this.comboReport.Location = new System.Drawing.Point(20, 362);
            this.comboReport.Margin = new System.Windows.Forms.Padding(4);
            this.comboReport.Name = "comboReport";
            this.comboReport.Size = new System.Drawing.Size(204, 24);
            this.comboReport.TabIndex = 19;
            this.helpTip.SetToolTip(this.comboReport, resources.GetString("comboReport.ToolTip"));
            this.comboReport.SelectedIndexChanged += new System.EventHandler(this.comboReport_SelectedIndexChanged);
            // 
            // btnInitialDirectoryMacros
            // 
            this.btnInitialDirectoryMacros.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnInitialDirectoryMacros.Image = ((System.Drawing.Image)(resources.GetObject("btnInitialDirectoryMacros.Image")));
            this.btnInitialDirectoryMacros.Location = new System.Drawing.Point(434, 300);
            this.btnInitialDirectoryMacros.Margin = new System.Windows.Forms.Padding(4);
            this.btnInitialDirectoryMacros.Name = "btnInitialDirectoryMacros";
            this.btnInitialDirectoryMacros.Size = new System.Drawing.Size(32, 28);
            this.btnInitialDirectoryMacros.TabIndex = 17;
            this.btnInitialDirectoryMacros.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.helpTip.SetToolTip(this.btnInitialDirectoryMacros, "This is the list of supported Initial Directory macros. These will \r\nbe replaced " +
                    "with the appropriate value when your run the tool.\r\n");
            this.btnInitialDirectoryMacros.UseVisualStyleBackColor = true;
            this.btnInitialDirectoryMacros.Click += new System.EventHandler(this.btnInitialDirectoryMacros_Click);
            // 
            // btnArguments
            // 
            this.btnArguments.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnArguments.Image = ((System.Drawing.Image)(resources.GetObject("btnArguments.Image")));
            this.btnArguments.Location = new System.Drawing.Point(434, 265);
            this.btnArguments.Margin = new System.Windows.Forms.Padding(4);
            this.btnArguments.Name = "btnArguments";
            this.btnArguments.Size = new System.Drawing.Size(32, 28);
            this.btnArguments.TabIndex = 13;
            this.btnArguments.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.helpTip.SetToolTip(this.btnArguments, "This is the list of supported argument macros. These will be\r\nreplaced with the a" +
                    "ppropriate value when your run the tool.");
            this.btnArguments.UseVisualStyleBackColor = true;
            this.btnArguments.Click += new System.EventHandler(this.btnArguments_Click);
            // 
            // btnInitialDirectory
            // 
            this.btnInitialDirectory.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnInitialDirectory.Location = new System.Drawing.Point(394, 300);
            this.btnInitialDirectory.Margin = new System.Windows.Forms.Padding(4);
            this.btnInitialDirectory.Name = "btnInitialDirectory";
            this.btnInitialDirectory.Size = new System.Drawing.Size(32, 28);
            this.btnInitialDirectory.TabIndex = 16;
            this.btnInitialDirectory.Text = "...";
            this.helpTip.SetToolTip(this.btnInitialDirectory, "Browse");
            this.btnInitialDirectory.UseVisualStyleBackColor = true;
            this.btnInitialDirectory.Click += new System.EventHandler(this.btnInitialDirectory_Click);
            // 
            // btnFindCommand
            // 
            this.btnFindCommand.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnFindCommand.Location = new System.Drawing.Point(434, 229);
            this.btnFindCommand.Margin = new System.Windows.Forms.Padding(4);
            this.btnFindCommand.Name = "btnFindCommand";
            this.btnFindCommand.Size = new System.Drawing.Size(32, 28);
            this.btnFindCommand.TabIndex = 10;
            this.btnFindCommand.Text = "...";
            this.helpTip.SetToolTip(this.btnFindCommand, "Browse");
            this.btnFindCommand.UseVisualStyleBackColor = true;
            this.btnFindCommand.Click += new System.EventHandler(this.btnFindCommand_Click);
            // 
            // textInitialDirectory
            // 
            this.textInitialDirectory.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textInitialDirectory.Location = new System.Drawing.Point(136, 303);
            this.textInitialDirectory.Margin = new System.Windows.Forms.Padding(4);
            this.textInitialDirectory.Name = "textInitialDirectory";
            this.textInitialDirectory.Size = new System.Drawing.Size(248, 22);
            this.textInitialDirectory.TabIndex = 15;
            this.helpTip.SetToolTip(this.textInitialDirectory, "Enter the working directory for the tool, or choose \r\nthe arrow button to select " +
                    "a predefined directory location\r\nor the browse button to select a directory.");
            this.textInitialDirectory.TextChanged += new System.EventHandler(this.textInitialDirectory_TextChanged);
            // 
            // textArguments
            // 
            this.textArguments.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textArguments.Location = new System.Drawing.Point(136, 267);
            this.textArguments.Margin = new System.Windows.Forms.Padding(4);
            this.textArguments.Name = "textArguments";
            this.textArguments.Size = new System.Drawing.Size(288, 22);
            this.textArguments.TabIndex = 12;
            this.helpTip.SetToolTip(this.textArguments, "Enter the arguments you wish to pass to the tool, or \r\nchoose the arrow button to" +
                    " select a predefined argument.");
            this.textArguments.TextChanged += new System.EventHandler(this.textArguments_TextChanged);
            // 
            // textCommand
            // 
            this.textCommand.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textCommand.Location = new System.Drawing.Point(136, 231);
            this.textCommand.Margin = new System.Windows.Forms.Padding(4);
            this.textCommand.Name = "textCommand";
            this.textCommand.Size = new System.Drawing.Size(288, 22);
            this.textCommand.TabIndex = 9;
            this.helpTip.SetToolTip(this.textCommand, resources.GetString("textCommand.ToolTip"));
            this.textCommand.TextChanged += new System.EventHandler(this.textCommand_TextChanged);
            // 
            // textTitle
            // 
            this.textTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textTitle.Location = new System.Drawing.Point(136, 196);
            this.textTitle.Margin = new System.Windows.Forms.Padding(4);
            this.textTitle.Name = "textTitle";
            this.textTitle.Size = new System.Drawing.Size(328, 22);
            this.textTitle.TabIndex = 7;
            this.helpTip.SetToolTip(this.textTitle, "Enter a name for the tool that will appear on the Tools menu");
            this.textTitle.TextChanged += new System.EventHandler(this.textTitle_TextChanged);
            // 
            // btnAdd
            // 
            this.btnAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAdd.Location = new System.Drawing.Point(366, 31);
            this.btnAdd.Margin = new System.Windows.Forms.Padding(4);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(100, 28);
            this.btnAdd.TabIndex = 2;
            this.btnAdd.Text = "&Add...";
            this.helpTip.SetToolTip(this.btnAdd, "Click here to add a new tool.");
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(150, 410);
            this.btnOK.Margin = new System.Windows.Forms.Padding(4);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(100, 28);
            this.btnOK.TabIndex = 21;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(16, 342);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(90, 17);
            this.label6.TabIndex = 18;
            this.label6.Text = "Input Report:";
            // 
            // cbOutputImmediateWindow
            // 
            this.cbOutputImmediateWindow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cbOutputImmediateWindow.AutoSize = true;
            this.cbOutputImmediateWindow.Location = new System.Drawing.Point(265, 364);
            this.cbOutputImmediateWindow.Margin = new System.Windows.Forms.Padding(4);
            this.cbOutputImmediateWindow.Name = "cbOutputImmediateWindow";
            this.cbOutputImmediateWindow.Size = new System.Drawing.Size(210, 21);
            this.cbOutputImmediateWindow.TabIndex = 20;
            this.cbOutputImmediateWindow.Text = "Output to Immediate Window";
            this.cbOutputImmediateWindow.UseVisualStyleBackColor = true;
            this.cbOutputImmediateWindow.CheckedChanged += new System.EventHandler(this.cbOutputImmediateWindow_CheckedChanged);
            // 
            // listTools
            // 
            this.listTools.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.listTools.FormattingEnabled = true;
            this.listTools.ItemHeight = 16;
            this.listTools.Location = new System.Drawing.Point(20, 31);
            this.listTools.Margin = new System.Windows.Forms.Padding(4);
            this.listTools.Name = "listTools";
            this.listTools.Size = new System.Drawing.Size(336, 148);
            this.listTools.TabIndex = 1;
            this.listTools.SelectedIndexChanged += new System.EventHandler(this.listTools_SelectedIndexChanged);
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(16, 306);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(103, 17);
            this.label5.TabIndex = 14;
            this.label5.Text = "&Initial directory:";
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(16, 271);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(80, 17);
            this.label4.TabIndex = 11;
            this.label4.Text = "A&rguments:";
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(16, 235);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(75, 17);
            this.label3.TabIndex = 8;
            this.label3.Text = "&Command:";
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 199);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(39, 17);
            this.label2.TabIndex = 6;
            this.label2.Text = "&Title:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 11);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(105, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Menu contents:";
            // 
            // btnMoveDown
            // 
            this.btnMoveDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnMoveDown.Location = new System.Drawing.Point(366, 151);
            this.btnMoveDown.Margin = new System.Windows.Forms.Padding(4);
            this.btnMoveDown.Name = "btnMoveDown";
            this.btnMoveDown.Size = new System.Drawing.Size(100, 28);
            this.btnMoveDown.TabIndex = 5;
            this.btnMoveDown.Text = "M&ove Down";
            this.btnMoveDown.UseVisualStyleBackColor = true;
            this.btnMoveDown.Click += new System.EventHandler(this.btnMoveDown_Click);
            // 
            // btnMoveUp
            // 
            this.btnMoveUp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnMoveUp.Location = new System.Drawing.Point(366, 116);
            this.btnMoveUp.Margin = new System.Windows.Forms.Padding(4);
            this.btnMoveUp.Name = "btnMoveUp";
            this.btnMoveUp.Size = new System.Drawing.Size(100, 28);
            this.btnMoveUp.TabIndex = 4;
            this.btnMoveUp.Text = "Move &Up";
            this.btnMoveUp.UseVisualStyleBackColor = true;
            this.btnMoveUp.Click += new System.EventHandler(this.btnMoveUp_Click);
            // 
            // btnRemove
            // 
            this.btnRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRemove.Location = new System.Drawing.Point(366, 66);
            this.btnRemove.Margin = new System.Windows.Forms.Padding(4);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(100, 28);
            this.btnRemove.TabIndex = 3;
            this.btnRemove.Text = "&Remove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.EnabledChanged += new System.EventHandler(this.btnRemove_EnabledChanged);
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnApply
            // 
            this.btnApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnApply.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnApply.Location = new System.Drawing.Point(364, 410);
            this.btnApply.Margin = new System.Windows.Forms.Padding(4);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(100, 28);
            this.btnApply.TabIndex = 23;
            this.btnApply.Text = "App&ly";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(258, 410);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 28);
            this.btnCancel.TabIndex = 22;
            this.btnCancel.Text = "Cancel";
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
            this.contextMenuAdd.Size = new System.Drawing.Size(157, 98);
            // 
            // customAddContextMenuItem
            // 
            this.customAddContextMenuItem.Name = "customAddContextMenuItem";
            this.customAddContextMenuItem.Size = new System.Drawing.Size(156, 24);
            this.customAddContextMenuItem.Text = "Custom...";
            this.customAddContextMenuItem.Click += new System.EventHandler(this.customAddContextMenuItem_Click);
            // 
            // fromFileAddContextMenuItem
            // 
            this.fromFileAddContextMenuItem.Name = "fromFileAddContextMenuItem";
            this.fromFileAddContextMenuItem.Size = new System.Drawing.Size(156, 24);
            this.fromFileAddContextMenuItem.Text = "From File...";
            this.fromFileAddContextMenuItem.Click += new System.EventHandler(this.fromFileAddContextMenuItem_Click);
            // 
            // fromWebAddContextMenuItem
            // 
            this.fromWebAddContextMenuItem.Name = "fromWebAddContextMenuItem";
            this.fromWebAddContextMenuItem.Size = new System.Drawing.Size(156, 24);
            this.fromWebAddContextMenuItem.Text = "From Web...";
            this.fromWebAddContextMenuItem.Visible = false;
            this.fromWebAddContextMenuItem.Click += new System.EventHandler(this.fromWebAddContextMenuItem_Click);
            // 
            // contextMenuCommand
            // 
            this.contextMenuCommand.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.browseToolStripMenuItem,
            this.editMacroToolStripMenuItem});
            this.contextMenuCommand.Name = "contextMenuCommand";
            this.contextMenuCommand.Size = new System.Drawing.Size(160, 52);
            // 
            // browseToolStripMenuItem
            // 
            this.browseToolStripMenuItem.Name = "browseToolStripMenuItem";
            this.browseToolStripMenuItem.Size = new System.Drawing.Size(159, 24);
            this.browseToolStripMenuItem.Text = "Browse...";
            this.browseToolStripMenuItem.Click += new System.EventHandler(this.browseToolStripMenuItem_Click);
            // 
            // editMacroToolStripMenuItem
            // 
            this.editMacroToolStripMenuItem.Name = "editMacroToolStripMenuItem";
            this.editMacroToolStripMenuItem.Size = new System.Drawing.Size(159, 24);
            this.editMacroToolStripMenuItem.Text = "Edit Macro...";
            this.editMacroToolStripMenuItem.Click += new System.EventHandler(this.editMacroToolStripMenuItem_Click);
            // 
            // contextMenuMacroArguments
            // 
            this.contextMenuMacroArguments.Name = "contextMenuMacroArguments";
            this.contextMenuMacroArguments.Size = new System.Drawing.Size(61, 4);
            // 
            // contextMenuMacroInitialDirectory
            // 
            this.contextMenuMacroInitialDirectory.Name = "contextMenuMacroInitialDirectory";
            this.contextMenuMacroInitialDirectory.Size = new System.Drawing.Size(61, 4);
            // 
            // ConfigureToolsDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(482, 453);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.comboReport);
            this.Controls.Add(this.cbOutputImmediateWindow);
            this.Controls.Add(this.btnInitialDirectoryMacros);
            this.Controls.Add(this.btnArguments);
            this.Controls.Add(this.btnInitialDirectory);
            this.Controls.Add(this.btnFindCommand);
            this.Controls.Add(this.listTools);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
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
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(500, 498);
            this.Name = "ConfigureToolsDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "External Tools";
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
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
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