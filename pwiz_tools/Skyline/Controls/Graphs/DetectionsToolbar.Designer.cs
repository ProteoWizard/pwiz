using System.Drawing;

namespace pwiz.Skyline.Controls.Graphs
{
    partial class DetectionsToolbar
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
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripDropDownButtonLevel = new System.Windows.Forms.ToolStripDropDownButton();
            this.toolStripMenuItemPrecursors = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemPeptides = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripDropDownButtonQCutoff = new System.Windows.Forms.ToolStripDropDownButton();
            this.toolStripMenuItem01 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem05 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemCustom = new pwiz.Skyline.Controls.Graphs.LabeledTextMenuItem();
            this.toolStripDropDownButtonMultiple = new System.Windows.Forms.ToolStripDropDownButton();
            this.onesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.hundredsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.thousandsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripDropDownButtonRepCount = new System.Windows.Forms.ToolStripDropDownButton();
            this.toolStripMenuItemRepCountDefault = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemRepCount = new pwiz.Skyline.Controls.Graphs.LabeledTrackBarMenuItem();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripDropDownButtonLevel,
            this.toolStripSeparator2,
            this.toolStripDropDownButtonQCutoff,
            this.toolStripSeparator1,
            this.toolStripDropDownButtonMultiple,
            this.toolStripSeparator3,
            this.toolStripDropDownButtonRepCount
            });
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(646, 27);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 27);
            // 
            // toolStripDropDownButtonLevel
            // 
            this.toolStripDropDownButtonLevel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripDropDownButtonLevel.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemPrecursors,
            this.toolStripMenuItemPeptides});
            this.toolStripDropDownButtonLevel.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripDropDownButtonLevel.Name = "toolStripDropDownButtonLevel";
            this.toolStripDropDownButtonLevel.Size = new System.Drawing.Size(60, 24);
            this.toolStripDropDownButtonLevel.Text = "Level:";
            this.toolStripDropDownButtonLevel.Tag = "Level:";
            this.toolStripDropDownButtonLevel.TextImageRelation = System.Windows.Forms.TextImageRelation.TextAboveImage;
            this.toolStripDropDownButtonLevel.ToolTipText = "Select what counts would be shown: precursors or peptides.";
            this.toolStripDropDownButtonLevel.DropDownItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.DropDownItemClicked);
            // 
            // toolStripMenuItemPrecursors
            // 
            this.toolStripMenuItemPrecursors.Name = "toolStripMenuItemPrecursors";
            this.toolStripMenuItemPrecursors.Size = new System.Drawing.Size(145, 26);
            this.toolStripMenuItemPrecursors.Text = "Precursor";
            this.toolStripMenuItemPrecursors.ToolTipText = "Show precursor counts.";
            this.toolStripMenuItemPrecursors.Tag = DetectionsPlotPane.TargetType.precursor;
            // 
            // toolStripMenuItemPeptides
            // 
            this.toolStripMenuItemPeptides.Name = "toolStripMenuItemPeptides";
            this.toolStripMenuItemPeptides.Size = new System.Drawing.Size(145, 26);
            this.toolStripMenuItemPeptides.Text = "Peptides";
            this.toolStripMenuItemPeptides.Tag = DetectionsPlotPane.TargetType.peptide;
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 27);
            // 
            // toolStripDropDownButtonQCutoff
            // 
            this.toolStripDropDownButtonQCutoff.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripDropDownButtonQCutoff.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem01,
            this.toolStripMenuItem05,
            this.toolStripMenuItemCustom});
            this.toolStripDropDownButtonQCutoff.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripDropDownButtonQCutoff.Name = "toolStripDropDownButtonQCutoff";
            this.toolStripDropDownButtonQCutoff.Size = new System.Drawing.Size(121, 24);
            this.toolStripDropDownButtonQCutoff.Text = "Q-Value Cutoff:";
            this.toolStripDropDownButtonQCutoff.Tag = "Q-Value Cutoff:";
            this.toolStripDropDownButtonQCutoff.TextImageRelation = System.Windows.Forms.TextImageRelation.TextAboveImage;
            this.toolStripDropDownButtonQCutoff.DropDownItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.DropDownItemClicked);
            // 
            // toolStripMenuItem01
            // 
            this.toolStripMenuItem01.Name = "toolStripMenuItem01";
            this.toolStripMenuItem01.Size = new System.Drawing.Size(217, 26);
            this.toolStripMenuItem01.Text = "0.01";
            // 
            // toolStripMenuItem05
            // 
            this.toolStripMenuItem05.Name = "toolStripMenuItem05";
            this.toolStripMenuItem05.Size = new System.Drawing.Size(217, 26);
            this.toolStripMenuItem05.Text = "0.05";
            // 
            // toolStripMenuItemCustom
            // 
            this.toolStripMenuItemCustom.BackColor = System.Drawing.Color.Transparent;
            this.toolStripMenuItemCustom.Name = "toolStripMenuItemCustom";
            this.toolStripMenuItemCustom.Size = new System.Drawing.Size(151, 37);
            this.toolStripMenuItemCustom.Text = "0.01";
            this.toolStripMenuItemCustom.ValueChanged += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.DropDownItemClicked);
            // 
            // toolStripDropDownButtonMultiple
            // 
            this.toolStripDropDownButtonMultiple.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripDropDownButtonMultiple.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.onesToolStripMenuItem,
            this.hundredsToolStripMenuItem,
            this.thousandsToolStripMenuItem});
            this.toolStripDropDownButtonMultiple.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripDropDownButtonMultiple.Name = "toolStripDropDownButtonMultiple";
            this.toolStripDropDownButtonMultiple.Size = new System.Drawing.Size(124, 24);
            this.toolStripDropDownButtonMultiple.Text = "Count Multiple:";
            this.toolStripDropDownButtonMultiple.Tag = "Count Multiple:";
            this.toolStripDropDownButtonMultiple.ToolTipText = "Scale of the Counts axis.";
            this.toolStripDropDownButtonMultiple.DropDownItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.DropDownItemClicked);
            // 
            // onesToolStripMenuItem
            // 
            this.onesToolStripMenuItem.Name = "onesToolStripMenuItem";
            this.onesToolStripMenuItem.Size = new System.Drawing.Size(154, 26);
            this.onesToolStripMenuItem.Tag = pwiz.Skyline.Controls.Graphs.DetectionsPlotPane.YScaleFactorType.one;
            this.onesToolStripMenuItem.Text = "Ones";
            // 
            // hundredsToolStripMenuItem
            // 
            this.hundredsToolStripMenuItem.Name = "hundredsToolStripMenuItem";
            this.hundredsToolStripMenuItem.Size = new System.Drawing.Size(154, 26);
            this.hundredsToolStripMenuItem.Tag = pwiz.Skyline.Controls.Graphs.DetectionsPlotPane.YScaleFactorType.hundreds;
            this.hundredsToolStripMenuItem.Text = "Hundreds";
            // 
            // thousandsToolStripMenuItem
            // 
            this.thousandsToolStripMenuItem.Name = "thousandsToolStripMenuItem";
            this.thousandsToolStripMenuItem.Size = new System.Drawing.Size(154, 26);
            this.thousandsToolStripMenuItem.Tag = pwiz.Skyline.Controls.Graphs.DetectionsPlotPane.YScaleFactorType.thousands;
            this.thousandsToolStripMenuItem.Text = "Thousands";
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 27);
            // 
            // toolStripDropDownButtonRepCount
            // 
            this.toolStripDropDownButtonRepCount.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripDropDownButtonRepCount.DropDownItems.AddRange(
                new System.Windows.Forms.ToolStripItem[] {
                                    this.toolStripMenuItemRepCountDefault,
                                    this.toolStripMenuItemRepCount});
            this.toolStripDropDownButtonRepCount.Name = "toolStripDropDownButtonRepCount";
            this.toolStripDropDownButtonRepCount.Size = new System.Drawing.Size(121, 24);
            this.toolStripDropDownButtonRepCount.Text = "Replicate Count:";
            this.toolStripDropDownButtonRepCount.Tag = "Replicate Count:";
            this.toolStripDropDownButtonRepCount.TextImageRelation = System.Windows.Forms.TextImageRelation.TextAboveImage;
            this.toolStripDropDownButtonRepCount.DropDownItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.DropDownItemClicked);
            // 
            // toolStripMenuItemRCDefault
            // 
            this.toolStripMenuItemRepCountDefault.Name = "toolStripMenuItemRepCountDefault";
            this.toolStripMenuItemRepCountDefault.Size = new System.Drawing.Size(217, 26);
            this.toolStripMenuItemRepCountDefault.Text = "Default";
            // 
            // toolStripMenuItemRepCount
            // 
            this.toolStripMenuItemRepCount.BackColor = System.Drawing.Color.Transparent;
            this.toolStripMenuItemRepCount.Name = "toolStripMenuItemRepCount";
            this.toolStripMenuItemRepCount.Size = new System.Drawing.Size(300, 37);
            this.toolStripMenuItemRepCount.Text = "Rep Count:";
            this.toolStripMenuItemRepCount.ValueChanged += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.DropDownItemClicked);
            // 
            // DetectionsToolbar
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.toolStrip1);
            this.Name = "DetectionsToolbar";
            this.Size = new System.Drawing.Size(646, 35);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

            }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButtonLevel;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemPrecursors;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemPeptides;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButtonQCutoff;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem01;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem05;
        private pwiz.Skyline.Controls.Graphs.LabeledTextMenuItem toolStripMenuItemCustom;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButtonMultiple;
        private System.Windows.Forms.ToolStripMenuItem onesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem hundredsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem thousandsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButtonRepCount;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemRepCountDefault;
        private pwiz.Skyline.Controls.Graphs.LabeledTrackBarMenuItem toolStripMenuItemRepCount;
    }
}
