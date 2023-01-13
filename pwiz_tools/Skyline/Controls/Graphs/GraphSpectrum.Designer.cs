
using System.Windows.Forms;

namespace pwiz.Skyline.Controls.Graphs
{

    partial class GraphSpectrum
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GraphSpectrum));
            this.toolBar = new System.Windows.Forms.ToolStrip();
            this.labelPrecursor = new System.Windows.Forms.ToolStripLabel();
            this.comboPrecursor = new System.Windows.Forms.ToolStripComboBox();
            this.labelSpectrum = new System.Windows.Forms.ToolStripLabel();
            this.comboSpectrum = new System.Windows.Forms.ToolStripComboBox();
            this.mirrorLabel = new System.Windows.Forms.ToolStripLabel();
            this.comboMirrorSpectrum = new System.Windows.Forms.ToolStripComboBox();
            this.ceLabel = new System.Windows.Forms.ToolStripLabel();
            this.comboCE = new System.Windows.Forms.ToolStripComboBox();
            this.GraphPanel = new System.Windows.Forms.Panel();
            this.msGraphExtension = new pwiz.Skyline.SettingsUI.MsGraphExtension();
            this.propertiesButton = new System.Windows.Forms.ToolStripButton();
            this.toolBar.SuspendLayout();
            this.GraphPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolBar
            // 
            this.toolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.labelPrecursor,
            this.comboPrecursor,
            this.labelSpectrum,
            this.comboSpectrum,
            this.mirrorLabel,
            this.comboMirrorSpectrum,
            this.ceLabel,
            this.comboCE,
            this.propertiesButton
            });
            resources.ApplyResources(this.toolBar, "toolBar");
            this.toolBar.Name = "toolBar";
            // 
            // labelPrecursor
            // 
            this.labelPrecursor.Name = "labelPrecursor";
            resources.ApplyResources(this.labelPrecursor, "labelPrecursor");
            // 
            // comboPrecursor
            // 
            this.comboPrecursor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboPrecursor.Name = "comboPrecursor";
            this.comboPrecursor.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            resources.ApplyResources(this.comboPrecursor, "comboPrecursor");
            this.comboPrecursor.SelectedIndexChanged += new System.EventHandler(this.comboPrecursor_SelectedIndexChanged);
            // 
            // labelSpectrum
            // 
            this.labelSpectrum.Name = "labelSpectrum";
            resources.ApplyResources(this.labelSpectrum, "labelSpectrum");
            // 
            // comboSpectrum
            // 
            this.comboSpectrum.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboSpectrum, "comboSpectrum");
            this.comboSpectrum.Name = "comboSpectrum";
            this.comboSpectrum.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.comboSpectrum.SelectedIndexChanged += new System.EventHandler(this.comboSpectrum_SelectedIndexChanged);
            // 
            // mirrorLabel
            // 
            this.mirrorLabel.Name = "mirrorLabel";
            resources.ApplyResources(this.mirrorLabel, "mirrorLabel");
            // 
            // comboMirrorSpectrum
            // 
            this.comboMirrorSpectrum.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboMirrorSpectrum, "comboMirrorSpectrum");
            this.comboMirrorSpectrum.Name = "comboMirrorSpectrum";
            this.comboMirrorSpectrum.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.comboMirrorSpectrum.SelectedIndexChanged += new System.EventHandler(this.comboMirrorSpectrum_SelectedIndexChanged);
            // 
            // ceLabel
            // 
            this.ceLabel.Name = "ceLabel";
            resources.ApplyResources(this.ceLabel, "ceLabel");
            // 
            // comboCE
            // 
            this.comboCE.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCE.Name = "comboCE";
            this.comboCE.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            resources.ApplyResources(this.comboCE, "comboCE");
            this.comboCE.SelectedIndexChanged += new System.EventHandler(this.comboCE_SelectedIndexChanged);
            // 
            // GraphPanel
            // 
            this.GraphPanel.Controls.Add(this.msGraphExtension);
            resources.ApplyResources(this.GraphPanel, "GraphPanel");
            this.GraphPanel.Name = "GraphPanel";
            // 
            // msGraphExtension
            // 
            resources.ApplyResources(this.msGraphExtension, "msGraphExtension");
            this.msGraphExtension.Name = "msGraphExtension";
            this.msGraphExtension.PropertySheetVisibilityPropName = "ViewLibraryMatchPropsVisible";
            //
            // propertiesButton
            //
            resources.ApplyResources(this.propertiesButton, "propertiesButton");
            this.propertiesButton.Name = "propertiesButton";
            this.propertiesButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.propertiesButton.Alignment = ToolStripItemAlignment.Right;
            this.propertiesButton.Image = global::pwiz.Skyline.Properties.Resources.Properties_Button;
            this.propertiesButton.Click += new System.EventHandler(this.propertiesMenuItem_Click);
            // 
            // GraphSpectrum
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.GraphPanel);
            this.Controls.Add(this.toolBar);
            this.HideOnClose = true;
            this.KeyPreview = true;
            this.Name = "GraphSpectrum";
            this.VisibleChanged += new System.EventHandler(this.GraphSpectrum_VisibleChanged);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GraphSpectrum_KeyDown);
            this.toolBar.ResumeLayout(false);
            this.toolBar.PerformLayout();
            this.GraphPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
        private System.Windows.Forms.ToolStrip toolBar;
        private System.Windows.Forms.Panel GraphPanel;
        private pwiz.Skyline.SettingsUI.MsGraphExtension msGraphExtension;
        private System.Windows.Forms.ToolStripButton propertiesButton;
        private System.Windows.Forms.ToolStripLabel labelSpectrum;
        private System.Windows.Forms.ToolStripComboBox comboSpectrum;
        private System.Windows.Forms.ToolStripLabel mirrorLabel;
        private System.Windows.Forms.ToolStripComboBox comboMirrorSpectrum;
        private System.Windows.Forms.ToolStripLabel ceLabel;
        private System.Windows.Forms.ToolStripComboBox comboCE;
        private System.Windows.Forms.ToolStripLabel labelPrecursor;
        private System.Windows.Forms.ToolStripComboBox comboPrecursor;
    }
}