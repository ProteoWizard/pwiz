namespace pwiz.Skyline.SettingsUI
{
    partial class ViewLibraryDlg
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
            this.ViewLibrarySplitCcontainer = new System.Windows.Forms.SplitContainer();
            this.PeptideListSplitContainer = new System.Windows.Forms.SplitContainer();
            this.PeptideListPanel = new System.Windows.Forms.Panel();
            this.PeptideListBox = new System.Windows.Forms.ListBox();
            this.PageCount = new System.Windows.Forms.Label();
            this.PeptideCount = new System.Windows.Forms.Label();
            this.NextLink = new System.Windows.Forms.LinkLabel();
            this.PreviousLink = new System.Windows.Forms.LinkLabel();
            this.PeptideEditPanel = new System.Windows.Forms.Panel();
            this.LibraryComboBox = new System.Windows.Forms.ComboBox();
            this.PeptideLabel = new System.Windows.Forms.Label();
            this.PeptideTextBox = new System.Windows.Forms.TextBox();
            this.GraphPanel = new System.Windows.Forms.Panel();
            this.graphControl = new pwiz.MSGraph.MSGraphControl();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.ViewLibraryPanel = new System.Windows.Forms.Panel();
            this.LibraryLabel = new System.Windows.Forms.Label();
            this.ViewLibrarySplitCcontainer.Panel1.SuspendLayout();
            this.ViewLibrarySplitCcontainer.Panel2.SuspendLayout();
            this.ViewLibrarySplitCcontainer.SuspendLayout();
            this.PeptideListSplitContainer.Panel1.SuspendLayout();
            this.PeptideListSplitContainer.Panel2.SuspendLayout();
            this.PeptideListSplitContainer.SuspendLayout();
            this.PeptideListPanel.SuspendLayout();
            this.PeptideEditPanel.SuspendLayout();
            this.GraphPanel.SuspendLayout();
            this.panel2.SuspendLayout();
            this.ViewLibraryPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // ViewLibrarySplitCcontainer
            // 
            this.ViewLibrarySplitCcontainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ViewLibrarySplitCcontainer.Location = new System.Drawing.Point(0, 0);
            this.ViewLibrarySplitCcontainer.Name = "ViewLibrarySplitCcontainer";
            // 
            // ViewLibrarySplitCcontainer.Panel1
            // 
            this.ViewLibrarySplitCcontainer.Panel1.Controls.Add(this.PeptideListSplitContainer);
            this.ViewLibrarySplitCcontainer.Panel1.Controls.Add(this.PeptideEditPanel);
            // 
            // ViewLibrarySplitCcontainer.Panel2
            // 
            this.ViewLibrarySplitCcontainer.Panel2.Controls.Add(this.GraphPanel);
            this.ViewLibrarySplitCcontainer.Panel2.Controls.Add(this.panel2);
            this.ViewLibrarySplitCcontainer.Size = new System.Drawing.Size(739, 389);
            this.ViewLibrarySplitCcontainer.SplitterDistance = 258;
            this.ViewLibrarySplitCcontainer.TabIndex = 0;
            // 
            // PeptideListSplitContainer
            // 
            this.PeptideListSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PeptideListSplitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.PeptideListSplitContainer.IsSplitterFixed = true;
            this.PeptideListSplitContainer.Location = new System.Drawing.Point(0, 63);
            this.PeptideListSplitContainer.Name = "PeptideListSplitContainer";
            this.PeptideListSplitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // PeptideListSplitContainer.Panel1
            // 
            this.PeptideListSplitContainer.Panel1.Controls.Add(this.PeptideListPanel);
            this.PeptideListSplitContainer.Panel1.Padding = new System.Windows.Forms.Padding(0, 3, 0, 0);
            // 
            // PeptideListSplitContainer.Panel2
            // 
            this.PeptideListSplitContainer.Panel2.Controls.Add(this.PageCount);
            this.PeptideListSplitContainer.Panel2.Controls.Add(this.PeptideCount);
            this.PeptideListSplitContainer.Panel2.Controls.Add(this.NextLink);
            this.PeptideListSplitContainer.Panel2.Controls.Add(this.PreviousLink);
            this.PeptideListSplitContainer.Panel2MinSize = 50;
            this.PeptideListSplitContainer.Size = new System.Drawing.Size(258, 326);
            this.PeptideListSplitContainer.SplitterDistance = 272;
            this.PeptideListSplitContainer.TabIndex = 2;
            // 
            // PeptideListPanel
            // 
            this.PeptideListPanel.Controls.Add(this.PeptideListBox);
            this.PeptideListPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PeptideListPanel.Location = new System.Drawing.Point(0, 3);
            this.PeptideListPanel.Name = "PeptideListPanel";
            this.PeptideListPanel.Size = new System.Drawing.Size(258, 269);
            this.PeptideListPanel.TabIndex = 3;
            // 
            // PeptideListBox
            // 
            this.PeptideListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PeptideListBox.FormattingEnabled = true;
            this.PeptideListBox.Location = new System.Drawing.Point(0, 0);
            this.PeptideListBox.MinimumSize = new System.Drawing.Size(258, 264);
            this.PeptideListBox.Name = "PeptideListBox";
            this.PeptideListBox.Size = new System.Drawing.Size(258, 264);
            this.PeptideListBox.TabIndex = 0;
            this.PeptideListBox.SelectedIndexChanged += new System.EventHandler(this.PeptideListBox_SelectedIndexChanged);
            // 
            // PageCount
            // 
            this.PageCount.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.PageCount.AutoSize = true;
            this.PageCount.Location = new System.Drawing.Point(107, 0);
            this.PageCount.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.PageCount.Name = "PageCount";
            this.PageCount.Size = new System.Drawing.Size(35, 13);
            this.PageCount.TabIndex = 2;
            this.PageCount.Text = "label1";
            // 
            // PeptideCount
            // 
            this.PeptideCount.AutoSize = true;
            this.PeptideCount.Font = new System.Drawing.Font("Bell MT", 9.75F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.PeptideCount.Location = new System.Drawing.Point(0, 23);
            this.PeptideCount.Margin = new System.Windows.Forms.Padding(0);
            this.PeptideCount.Name = "PeptideCount";
            this.PeptideCount.Size = new System.Drawing.Size(72, 15);
            this.PeptideCount.TabIndex = 0;
            this.PeptideCount.Text = "PeptideCount";
            // 
            // NextLink
            // 
            this.NextLink.AutoSize = true;
            this.NextLink.Dock = System.Windows.Forms.DockStyle.Right;
            this.NextLink.Location = new System.Drawing.Point(214, 0);
            this.NextLink.Name = "NextLink";
            this.NextLink.Size = new System.Drawing.Size(44, 13);
            this.NextLink.TabIndex = 1;
            this.NextLink.TabStop = true;
            this.NextLink.Text = "Next >>";
            this.NextLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.NextLink_LinkClicked);
            // 
            // PreviousLink
            // 
            this.PreviousLink.AutoSize = true;
            this.PreviousLink.Dock = System.Windows.Forms.DockStyle.Left;
            this.PreviousLink.Location = new System.Drawing.Point(0, 0);
            this.PreviousLink.Name = "PreviousLink";
            this.PreviousLink.Size = new System.Drawing.Size(63, 13);
            this.PreviousLink.TabIndex = 0;
            this.PreviousLink.TabStop = true;
            this.PreviousLink.Text = "<< Previous";
            this.PreviousLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.PreviousLink_LinkClicked);
            // 
            // PeptideEditPanel
            // 
            this.PeptideEditPanel.Controls.Add(this.LibraryComboBox);
            this.PeptideEditPanel.Controls.Add(this.PeptideLabel);
            this.PeptideEditPanel.Controls.Add(this.PeptideTextBox);
            this.PeptideEditPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.PeptideEditPanel.Location = new System.Drawing.Point(0, 0);
            this.PeptideEditPanel.Name = "PeptideEditPanel";
            this.PeptideEditPanel.Size = new System.Drawing.Size(258, 63);
            this.PeptideEditPanel.TabIndex = 0;
            // 
            // LibraryComboBox
            // 
            this.LibraryComboBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LibraryComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.LibraryComboBox.FormattingEnabled = true;
            this.LibraryComboBox.Location = new System.Drawing.Point(0, 0);
            this.LibraryComboBox.Name = "LibraryComboBox";
            this.LibraryComboBox.Size = new System.Drawing.Size(258, 21);
            this.LibraryComboBox.TabIndex = 1;
            this.LibraryComboBox.SelectedIndexChanged += new System.EventHandler(this.LibraryComboBox_SelectedIndexChanged);
            // 
            // PeptideLabel
            // 
            this.PeptideLabel.AutoSize = true;
            this.PeptideLabel.Location = new System.Drawing.Point(0, 27);
            this.PeptideLabel.Name = "PeptideLabel";
            this.PeptideLabel.Size = new System.Drawing.Size(46, 13);
            this.PeptideLabel.TabIndex = 3;
            this.PeptideLabel.Text = "Peptide:";
            // 
            // PeptideTextBox
            // 
            this.PeptideTextBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.PeptideTextBox.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.PeptideTextBox.Location = new System.Drawing.Point(0, 43);
            this.PeptideTextBox.Name = "PeptideTextBox";
            this.PeptideTextBox.Size = new System.Drawing.Size(258, 20);
            this.PeptideTextBox.TabIndex = 0;
            this.PeptideTextBox.TextChanged += new System.EventHandler(this.PeptideTextBox_TextChanged);
            // 
            // GraphPanel
            // 
            this.GraphPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.GraphPanel.Controls.Add(this.graphControl);
            this.GraphPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GraphPanel.Location = new System.Drawing.Point(0, 0);
            this.GraphPanel.Name = "GraphPanel";
            this.GraphPanel.Size = new System.Drawing.Size(477, 333);
            this.GraphPanel.TabIndex = 0;
            // 
            // graphControl
            // 
            this.graphControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.graphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphControl.IsEnableVPan = false;
            this.graphControl.IsEnableVZoom = false;
            this.graphControl.IsShowCopyMessage = false;
            this.graphControl.Location = new System.Drawing.Point(0, 0);
            this.graphControl.MinimumSize = new System.Drawing.Size(473, 328);
            this.graphControl.Name = "graphControl";
            this.graphControl.ScrollGrace = 0;
            this.graphControl.ScrollMaxX = 0;
            this.graphControl.ScrollMaxY = 0;
            this.graphControl.ScrollMaxY2 = 0;
            this.graphControl.ScrollMinX = 0;
            this.graphControl.ScrollMinY = 0;
            this.graphControl.ScrollMinY2 = 0;
            this.graphControl.Size = new System.Drawing.Size(473, 329);
            this.graphControl.TabIndex = 3;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.btnCancel);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 333);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(477, 56);
            this.panel2.TabIndex = 1;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(402, 33);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 0;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // ViewLibraryPanel
            // 
            this.ViewLibraryPanel.Controls.Add(this.ViewLibrarySplitCcontainer);
            this.ViewLibraryPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ViewLibraryPanel.Location = new System.Drawing.Point(10, 25);
            this.ViewLibraryPanel.Name = "ViewLibraryPanel";
            this.ViewLibraryPanel.Size = new System.Drawing.Size(739, 389);
            this.ViewLibraryPanel.TabIndex = 2;
            // 
            // LibraryLabel
            // 
            this.LibraryLabel.AutoSize = true;
            this.LibraryLabel.Location = new System.Drawing.Point(10, 9);
            this.LibraryLabel.Name = "LibraryLabel";
            this.LibraryLabel.Size = new System.Drawing.Size(41, 13);
            this.LibraryLabel.TabIndex = 4;
            this.LibraryLabel.Text = "Library:";
            // 
            // ViewLibraryDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(759, 424);
            this.Controls.Add(this.LibraryLabel);
            this.Controls.Add(this.ViewLibraryPanel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(775, 460);
            this.Name = "ViewLibraryDlg";
            this.Padding = new System.Windows.Forms.Padding(10, 25, 10, 10);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Spectral Library Explorer";
            this.Load += new System.EventHandler(this.ViewLibraryDlg_Load);
            this.ViewLibrarySplitCcontainer.Panel1.ResumeLayout(false);
            this.ViewLibrarySplitCcontainer.Panel2.ResumeLayout(false);
            this.ViewLibrarySplitCcontainer.ResumeLayout(false);
            this.PeptideListSplitContainer.Panel1.ResumeLayout(false);
            this.PeptideListSplitContainer.Panel2.ResumeLayout(false);
            this.PeptideListSplitContainer.Panel2.PerformLayout();
            this.PeptideListSplitContainer.ResumeLayout(false);
            this.PeptideListPanel.ResumeLayout(false);
            this.PeptideEditPanel.ResumeLayout(false);
            this.PeptideEditPanel.PerformLayout();
            this.GraphPanel.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.ViewLibraryPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer ViewLibrarySplitCcontainer;
        private System.Windows.Forms.SplitContainer PeptideListSplitContainer;
        private System.Windows.Forms.Panel PeptideListPanel;
        private System.Windows.Forms.ListBox PeptideListBox;
        private System.Windows.Forms.Label PageCount;
        private System.Windows.Forms.Label PeptideCount;
        private System.Windows.Forms.LinkLabel NextLink;
        private System.Windows.Forms.LinkLabel PreviousLink;
        private System.Windows.Forms.Panel PeptideEditPanel;
        private System.Windows.Forms.TextBox PeptideTextBox;
        private System.Windows.Forms.Panel ViewLibraryPanel;
        private System.Windows.Forms.Label PeptideLabel;
        private System.Windows.Forms.ComboBox LibraryComboBox;
        private System.Windows.Forms.Label LibraryLabel;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Panel GraphPanel;
        private pwiz.MSGraph.MSGraphControl graphControl;




    }
}
