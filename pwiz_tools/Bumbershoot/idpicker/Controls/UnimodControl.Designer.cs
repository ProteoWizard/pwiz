//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

namespace IDPicker.Controls
{
    partial class UnimodControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.label2 = new System.Windows.Forms.Label();
            this.MassesLabel = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.SitesLabel = new System.Windows.Forms.Label();
            this.SiteFilterBox = new System.Windows.Forms.ComboBox();
            this.HiddenModBox = new System.Windows.Forms.CheckBox();
            this.UnimodTree = new System.Windows.Forms.TreeView();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.label2);
            this.splitContainer1.Panel1.Controls.Add(this.MassesLabel);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            this.splitContainer1.Panel1.Controls.Add(this.SitesLabel);
            this.splitContainer1.Panel1.Controls.Add(this.SiteFilterBox);
            this.splitContainer1.Panel1.Controls.Add(this.HiddenModBox);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.UnimodTree);
            this.splitContainer1.Size = new System.Drawing.Size(384, 315);
            this.splitContainer1.SplitterDistance = 25;
            this.splitContainer1.SplitterWidth = 1;
            this.splitContainer1.TabIndex = 0;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.label2.Location = new System.Drawing.Point(288, 7);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(80, 12);
            this.label2.TabIndex = 5;
            this.label2.Text = "Selected Masses:";
            // 
            // MassesLabel
            // 
            this.MassesLabel.AutoSize = true;
            this.MassesLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.MassesLabel.Location = new System.Drawing.Point(366, 7);
            this.MassesLabel.Name = "MassesLabel";
            this.MassesLabel.Size = new System.Drawing.Size(11, 12);
            this.MassesLabel.TabIndex = 4;
            this.MassesLabel.Text = "0";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.label1.Location = new System.Drawing.Point(199, 7);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(67, 12);
            this.label1.TabIndex = 3;
            this.label1.Text = "Selected Sites:";
            // 
            // SitesLabel
            // 
            this.SitesLabel.AutoSize = true;
            this.SitesLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.SitesLabel.Location = new System.Drawing.Point(265, 7);
            this.SitesLabel.Name = "SitesLabel";
            this.SitesLabel.Size = new System.Drawing.Size(11, 12);
            this.SitesLabel.TabIndex = 2;
            this.SitesLabel.Text = "0";
            // 
            // SiteFilterBox
            // 
            this.SiteFilterBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.SiteFilterBox.FormattingEnabled = true;
            this.SiteFilterBox.Items.AddRange(new object[] {
            "All Sites",
            "NTerminus",
            "CTerminus",
            "Alanine",
            "Cysteine",
            "AsparticAcid",
            "GlutamicAcid",
            "Phenylalanine",
            "Glycine",
            "Histidine",
            "Isoleucine",
            "Lysine",
            "Leucine",
            "Methionine",
            "Asparagine",
            "Proline",
            "Glutamine",
            "Arginine",
            "Serine",
            "Threonine",
            "Valine",
            "Tryptophan",
            "Tyrosine"});
            this.SiteFilterBox.Location = new System.Drawing.Point(108, 3);
            this.SiteFilterBox.Name = "SiteFilterBox";
            this.SiteFilterBox.Size = new System.Drawing.Size(85, 21);
            this.SiteFilterBox.TabIndex = 1;
            this.SiteFilterBox.SelectedIndexChanged += new System.EventHandler(this.FilterEventRaised);
            // 
            // HiddenModBox
            // 
            this.HiddenModBox.AutoSize = true;
            this.HiddenModBox.Location = new System.Drawing.Point(3, 5);
            this.HiddenModBox.Name = "HiddenModBox";
            this.HiddenModBox.Size = new System.Drawing.Size(90, 17);
            this.HiddenModBox.TabIndex = 0;
            this.HiddenModBox.Text = "Show Hidden";
            this.HiddenModBox.UseVisualStyleBackColor = true;
            this.HiddenModBox.CheckedChanged += new System.EventHandler(this.FilterEventRaised);
            // 
            // UnimodTree
            // 
            this.UnimodTree.CheckBoxes = true;
            this.UnimodTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.UnimodTree.Location = new System.Drawing.Point(0, 0);
            this.UnimodTree.Name = "UnimodTree";
            this.UnimodTree.Size = new System.Drawing.Size(384, 289);
            this.UnimodTree.TabIndex = 0;
            this.UnimodTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.UnimodTree_AfterCheck);
            // 
            // UnimodControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.splitContainer1);
            this.Name = "UnimodControl";
            this.Size = new System.Drawing.Size(384, 315);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TreeView UnimodTree;
        private System.Windows.Forms.ComboBox SiteFilterBox;
        private System.Windows.Forms.CheckBox HiddenModBox;
        private System.Windows.Forms.Label SitesLabel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label MassesLabel;
    }
}
