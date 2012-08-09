//
// $Id: AboutForm.Designer.cs 48 2010-02-24 16:34:33Z chambm $
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

namespace BumberDash.Forms
{
    partial class AboutForm
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
            this.componentListView = new System.Windows.Forms.ListView();
            this.Component = new System.Windows.Forms.ColumnHeader();
            this.VersionColumn = new System.Windows.Forms.ColumnHeader();
            this.aboutTextBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // componentListView
            // 
            this.componentListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.componentListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.Component,
            this.VersionColumn});
            this.componentListView.FullRowSelect = true;
            this.componentListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.componentListView.LabelWrap = false;
            this.componentListView.Location = new System.Drawing.Point(12, 113);
            this.componentListView.Name = "componentListView";
            this.componentListView.Scrollable = false;
            this.componentListView.Size = new System.Drawing.Size(331, 123);
            this.componentListView.TabIndex = 0;
            this.componentListView.UseCompatibleStateImageBehavior = false;
            this.componentListView.View = System.Windows.Forms.View.Details;
            // 
            // Component
            // 
            this.Component.Text = "Component";
            this.Component.Width = 155;
            // 
            // VersionColumn
            // 
            this.VersionColumn.Text = "Version";
            this.VersionColumn.Width = 270;
            // 
            // aboutTextBox
            // 
            this.aboutTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.aboutTextBox.BackColor = System.Drawing.SystemColors.Control;
            this.aboutTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.aboutTextBox.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.aboutTextBox.ForeColor = System.Drawing.SystemColors.ControlText;
            this.aboutTextBox.Location = new System.Drawing.Point(12, 14);
            this.aboutTextBox.Multiline = true;
            this.aboutTextBox.Name = "aboutTextBox";
            this.aboutTextBox.ReadOnly = true;
            this.aboutTextBox.Size = new System.Drawing.Size(330, 97);
            this.aboutTextBox.TabIndex = 1;
            this.aboutTextBox.Text = "BumberDash <<version>>\r\n© 2010 Vanderbilt University\r\n\r\nAuthor: Jay Holman\r\n\r\nTha" +
                "nks to: David Tabb, Matt Chambers, Surendra Dasari";
            // 
            // AboutForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(355, 248);
            this.Controls.Add(this.aboutTextBox);
            this.Controls.Add(this.componentListView);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About BumberDash";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView componentListView;
        private System.Windows.Forms.ColumnHeader Component;
        private System.Windows.Forms.ColumnHeader VersionColumn;
        private System.Windows.Forms.TextBox aboutTextBox;


    }
}