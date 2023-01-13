using System;

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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ViewLibraryDlg));
            this.splitPeptideList = new System.Windows.Forms.SplitContainer();
            this.PeptideListPanel = new System.Windows.Forms.Panel();
            this.listPeptide = new System.Windows.Forms.ListBox();
            this.cbShowModMasses = new System.Windows.Forms.CheckBox();
            this.PageCount = new System.Windows.Forms.Label();
            this.PeptideCount = new System.Windows.Forms.Label();
            this.NextLink = new System.Windows.Forms.LinkLabel();
            this.PreviousLink = new System.Windows.Forms.LinkLabel();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.PeptideEditPanel = new System.Windows.Forms.Panel();
            this.byLabel = new System.Windows.Forms.Label();
            this.comboFilterCategory = new System.Windows.Forms.ComboBox();
            this.filterLabel = new System.Windows.Forms.Label();
            this.MoleculeLabel = new System.Windows.Forms.Label();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.comboLibrary = new System.Windows.Forms.ComboBox();
            this.btnLibDetails = new System.Windows.Forms.Button();
            this.PeptideLabel = new System.Windows.Forms.Label();
            this.textPeptide = new System.Windows.Forms.TextBox();
            this.GraphPanel = new System.Windows.Forms.Panel();
            this.msGraphExtension1 = new pwiz.Skyline.SettingsUI.MsGraphExtension();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.specialionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ionTypesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.btnAIons = new System.Windows.Forms.ToolStripButton();
            this.btnBIons = new System.Windows.Forms.ToolStripButton();
            this.btnCIons = new System.Windows.Forms.ToolStripButton();
            this.btnXIons = new System.Windows.Forms.ToolStripButton();
            this.btnYIons = new System.Windows.Forms.ToolStripButton();
            this.btnZIons = new System.Windows.Forms.ToolStripButton();
            this.btnFragmentIons = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.charge1Button = new System.Windows.Forms.ToolStripButton();
            this.charge2Button = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.propertiesButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.copyMetafileButton = new System.Windows.Forms.ToolStripButton();
            this.btnCopy = new System.Windows.Forms.ToolStripButton();
            this.btnSave = new System.Windows.Forms.ToolStripButton();
            this.btnPrint = new System.Windows.Forms.ToolStripButton();
            this.panel2 = new System.Windows.Forms.Panel();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.labelFilename = new System.Windows.Forms.Label();
            this.comboRedundantSpectra = new System.Windows.Forms.ComboBox();
            this.labelRT = new System.Windows.Forms.Label();
            this.cbAssociateProteins = new System.Windows.Forms.CheckBox();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnAddAll = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.ViewLibraryPanel = new System.Windows.Forms.Panel();
            this.LibraryLabel = new System.Windows.Forms.Label();
            this.contextMenuSpectrum = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.fragmentionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorIonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator11 = new System.Windows.Forms.ToolStripSeparator();
            this.chargesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator12 = new System.Windows.Forms.ToolStripSeparator();
            this.ranksContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scoreContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ionMzValuesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.observedMzValuesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.duplicatesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator13 = new System.Windows.Forms.ToolStripSeparator();
            this.lockYaxisContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator14 = new System.Windows.Forms.ToolStripSeparator();
            this.graphPropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.spectrumPropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showChromatogramsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator15 = new System.Windows.Forms.ToolStripSeparator();
            this.zoomSpectrumContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator27 = new System.Windows.Forms.ToolStripSeparator();
            ((System.ComponentModel.ISupportInitialize)(this.splitPeptideList)).BeginInit();
            this.splitPeptideList.Panel1.SuspendLayout();
            this.splitPeptideList.Panel2.SuspendLayout();
            this.splitPeptideList.SuspendLayout();
            this.PeptideListPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.PeptideEditPanel.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.GraphPanel.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.ViewLibraryPanel.SuspendLayout();
            this.contextMenuSpectrum.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitPeptideList
            // 
            resources.ApplyResources(this.splitPeptideList, "splitPeptideList");
            this.splitPeptideList.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitPeptideList.Name = "splitPeptideList";
            // 
            // splitPeptideList.Panel1
            // 
            this.splitPeptideList.Panel1.Controls.Add(this.PeptideListPanel);
            resources.ApplyResources(this.splitPeptideList.Panel1, "splitPeptideList.Panel1");
            // 
            // splitPeptideList.Panel2
            // 
            this.splitPeptideList.Panel2.Controls.Add(this.PageCount);
            this.splitPeptideList.Panel2.Controls.Add(this.PeptideCount);
            this.splitPeptideList.Panel2.Controls.Add(this.NextLink);
            this.splitPeptideList.Panel2.Controls.Add(this.PreviousLink);
            // 
            // PeptideListPanel
            // 
            this.PeptideListPanel.Controls.Add(this.listPeptide);
            this.PeptideListPanel.Controls.Add(this.cbShowModMasses);
            resources.ApplyResources(this.PeptideListPanel, "PeptideListPanel");
            this.PeptideListPanel.Name = "PeptideListPanel";
            this.PeptideListPanel.Resize += new System.EventHandler(this.PeptideListPanel_Resize);
            // 
            // listPeptide
            // 
            resources.ApplyResources(this.listPeptide, "listPeptide");
            this.listPeptide.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.listPeptide.FormattingEnabled = true;
            this.listPeptide.Name = "listPeptide";
            this.listPeptide.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.listPeptide_DrawItem);
            this.listPeptide.SelectedIndexChanged += new System.EventHandler(this.listPeptide_SelectedIndexChanged);
            this.listPeptide.MouseLeave += new System.EventHandler(this.listPeptide_MouseLeave);
            this.listPeptide.MouseMove += new System.Windows.Forms.MouseEventHandler(this.listPeptide_MouseMove);
            // 
            // cbShowModMasses
            // 
            resources.ApplyResources(this.cbShowModMasses, "cbShowModMasses");
            this.cbShowModMasses.Name = "cbShowModMasses";
            this.cbShowModMasses.UseVisualStyleBackColor = true;
            this.cbShowModMasses.CheckedChanged += new System.EventHandler(this.cbShowModMasses_CheckedChanged);
            // 
            // PageCount
            // 
            resources.ApplyResources(this.PageCount, "PageCount");
            this.PageCount.Name = "PageCount";
            // 
            // PeptideCount
            // 
            resources.ApplyResources(this.PeptideCount, "PeptideCount");
            this.PeptideCount.Name = "PeptideCount";
            // 
            // NextLink
            // 
            resources.ApplyResources(this.NextLink, "NextLink");
            this.NextLink.Name = "NextLink";
            this.NextLink.TabStop = true;
            this.NextLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.NextLink_LinkClicked);
            // 
            // PreviousLink
            // 
            resources.ApplyResources(this.PreviousLink, "PreviousLink");
            this.PreviousLink.Name = "PreviousLink";
            this.PreviousLink.TabStop = true;
            this.PreviousLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.PreviousLink_LinkClicked);
            // 
            // splitMain
            // 
            resources.ApplyResources(this.splitMain, "splitMain");
            this.splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.splitPeptideList);
            this.splitMain.Panel1.Controls.Add(this.PeptideEditPanel);
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.GraphPanel);
            this.splitMain.Panel2.Controls.Add(this.panel2);
            this.splitMain.TabStop = false;
            this.splitMain.MouseDown += new System.Windows.Forms.MouseEventHandler(this.splitMain_MouseDown);
            this.splitMain.MouseUp += new System.Windows.Forms.MouseEventHandler(this.splitMain_MouseUp);
            // 
            // PeptideEditPanel
            // 
            this.PeptideEditPanel.Controls.Add(this.byLabel);
            this.PeptideEditPanel.Controls.Add(this.comboFilterCategory);
            this.PeptideEditPanel.Controls.Add(this.filterLabel);
            this.PeptideEditPanel.Controls.Add(this.MoleculeLabel);
            this.PeptideEditPanel.Controls.Add(this.tableLayoutPanel1);
            this.PeptideEditPanel.Controls.Add(this.PeptideLabel);
            this.PeptideEditPanel.Controls.Add(this.textPeptide);
            resources.ApplyResources(this.PeptideEditPanel, "PeptideEditPanel");
            this.PeptideEditPanel.Name = "PeptideEditPanel";
            // 
            // byLabel
            // 
            resources.ApplyResources(this.byLabel, "byLabel");
            this.byLabel.Name = "byLabel";
            // 
            // comboFilterCategory
            // 
            resources.ApplyResources(this.comboFilterCategory, "comboFilterCategory");
            this.comboFilterCategory.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboFilterCategory.FormattingEnabled = true;
            this.comboFilterCategory.Name = "comboFilterCategory";
            this.comboFilterCategory.SelectedIndexChanged += new System.EventHandler(this.comboFilterCategory_SelectedIndexChanged);
            // 
            // filterLabel
            // 
            resources.ApplyResources(this.filterLabel, "filterLabel");
            this.filterLabel.Name = "filterLabel";
            // 
            // MoleculeLabel
            // 
            resources.ApplyResources(this.MoleculeLabel, "MoleculeLabel");
            this.MoleculeLabel.Name = "MoleculeLabel";
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this.comboLibrary, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnLibDetails, 1, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // comboLibrary
            // 
            resources.ApplyResources(this.comboLibrary, "comboLibrary");
            this.comboLibrary.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLibrary.FormattingEnabled = true;
            this.comboLibrary.Name = "comboLibrary";
            this.comboLibrary.SelectedIndexChanged += new System.EventHandler(this.LibraryComboBox_SelectedIndexChanged);
            // 
            // btnLibDetails
            // 
            this.btnLibDetails.DialogResult = System.Windows.Forms.DialogResult.OK;
            resources.ApplyResources(this.btnLibDetails, "btnLibDetails");
            this.btnLibDetails.Name = "btnLibDetails";
            this.btnLibDetails.UseVisualStyleBackColor = true;
            this.btnLibDetails.Click += new System.EventHandler(this.btnLibDetails_Click);
            // 
            // PeptideLabel
            // 
            resources.ApplyResources(this.PeptideLabel, "PeptideLabel");
            this.PeptideLabel.Name = "PeptideLabel";
            // 
            // textPeptide
            // 
            resources.ApplyResources(this.textPeptide, "textPeptide");
            this.textPeptide.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textPeptide.Name = "textPeptide";
            this.textPeptide.TextChanged += new System.EventHandler(this.textPeptide_TextChanged);
            this.textPeptide.KeyDown += new System.Windows.Forms.KeyEventHandler(this.PeptideTextBox_KeyDown);
            // 
            // GraphPanel
            // 
            this.GraphPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.GraphPanel.Controls.Add(this.msGraphExtension1);
            this.GraphPanel.Controls.Add(this.toolStrip1);
            resources.ApplyResources(this.GraphPanel, "GraphPanel");
            this.GraphPanel.Name = "GraphPanel";
            // 
            // msGraphExtension1
            // 
            resources.ApplyResources(this.msGraphExtension1, "msGraphExtension1");
            this.msGraphExtension1.Name = "msGraphExtension1";
            this.msGraphExtension1.PropertySheetVisibilityPropName = "ViewLibraryPropertiesVisible";
            // 
            // toolStrip1
            // 
            resources.ApplyResources(this.toolStrip1, "toolStrip1");
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnAIons,
            this.btnBIons,
            this.btnCIons,
            this.btnXIons,
            this.btnYIons,
            this.btnZIons,
            this.btnFragmentIons,
            this.toolStripSeparator1,
            this.charge1Button,
            this.charge2Button,
            this.toolStripSeparator2,
            this.propertiesButton,
            this.toolStripSeparator3,
            this.copyMetafileButton,
            this.btnCopy,
            this.btnSave,
            this.btnPrint});
            this.toolStrip1.Name = "toolStrip1";
            // 
            // btnAIons
            // 
            this.btnAIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnAIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_A;
            resources.ApplyResources(this.btnAIons, "btnAIons");
            this.btnAIons.Name = "btnAIons";
            this.btnAIons.Click += new System.EventHandler(this.aionsContextMenuItem_Click);
            // 
            // btnBIons
            // 
            this.btnBIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnBIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_B;
            resources.ApplyResources(this.btnBIons, "btnBIons");
            this.btnBIons.Name = "btnBIons";
            this.btnBIons.Click += new System.EventHandler(this.bionsContextMenuItem_Click);
            // 
            // btnCIons
            // 
            this.btnCIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnCIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_C;
            resources.ApplyResources(this.btnCIons, "btnCIons");
            this.btnCIons.Name = "btnCIons";
            this.btnCIons.Click += new System.EventHandler(this.cionsContextMenuItem_Click);
            // 
            // btnXIons
            // 
            this.btnXIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnXIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_X;
            resources.ApplyResources(this.btnXIons, "btnXIons");
            this.btnXIons.Name = "btnXIons";
            this.btnXIons.Click += new System.EventHandler(this.xionsContextMenuItem_Click);
            // 
            // btnYIons
            // 
            this.btnYIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnYIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_Y;
            resources.ApplyResources(this.btnYIons, "btnYIons");
            this.btnYIons.Name = "btnYIons";
            this.btnYIons.Click += new System.EventHandler(this.yionsContextMenuItem_Click);
            // 
            // btnZIons
            // 
            this.btnZIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnZIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_Z;
            resources.ApplyResources(this.btnZIons, "btnZIons");
            this.btnZIons.Name = "btnZIons";
            this.btnZIons.Click += new System.EventHandler(this.zionsContextMenuItem_Click);
            // 
            // btnFragmentIons
            // 
            this.btnFragmentIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnFragmentIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_fragments;
            resources.ApplyResources(this.btnFragmentIons, "btnFragmentIons");
            this.btnFragmentIons.Name = "btnFragmentIons";
            this.btnFragmentIons.Click += new System.EventHandler(this.fragmentionsContextMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            // 
            // charge1Button
            // 
            this.charge1Button.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.charge1Button.Image = global::pwiz.Skyline.Properties.Resources.Ions_1;
            resources.ApplyResources(this.charge1Button, "charge1Button");
            this.charge1Button.Name = "charge1Button";
            this.charge1Button.Click += new System.EventHandler(this.charge1ContextMenuItem_Click);
            // 
            // charge2Button
            // 
            this.charge2Button.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.charge2Button.Image = global::pwiz.Skyline.Properties.Resources.Ions_2;
            resources.ApplyResources(this.charge2Button, "charge2Button");
            this.charge2Button.Name = "charge2Button";
            this.charge2Button.Click += new System.EventHandler(this.charge2ContextMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            resources.ApplyResources(this.toolStripSeparator2, "toolStripSeparator2");
            // 
            // propertiesButton
            // 
            this.propertiesButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.propertiesButton.Image = global::pwiz.Skyline.Properties.Resources.Properties_Button;
            resources.ApplyResources(this.propertiesButton, "propertiesButton");
            this.propertiesButton.Name = "propertiesButton";
            this.propertiesButton.Click += new System.EventHandler(this.propertiesMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            resources.ApplyResources(this.toolStripSeparator3, "toolStripSeparator3");
            // 
            // copyMetafileButton
            // 
            this.copyMetafileButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.copyMetafileButton.Image = global::pwiz.Skyline.Properties.Resources.Copy;
            resources.ApplyResources(this.copyMetafileButton, "copyMetafileButton");
            this.copyMetafileButton.Name = "copyMetafileButton";
            this.copyMetafileButton.Click += new System.EventHandler(this.copyMetafileButton_Click);
            // 
            // btnCopy
            // 
            this.btnCopy.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnCopy.Image = global::pwiz.Skyline.Properties.Resources.Copy_Bitmap;
            resources.ApplyResources(this.btnCopy, "btnCopy");
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
            // 
            // btnSave
            // 
            this.btnSave.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnSave.Image = global::pwiz.Skyline.Properties.Resources.Save;
            resources.ApplyResources(this.btnSave, "btnSave");
            this.btnSave.Name = "btnSave";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnPrint
            // 
            this.btnPrint.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnPrint.Image = global::pwiz.Skyline.Properties.Resources.Print;
            resources.ApplyResources(this.btnPrint, "btnPrint");
            this.btnPrint.Name = "btnPrint";
            this.btnPrint.Click += new System.EventHandler(this.btnPrint_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.tableLayoutPanel2);
            this.panel2.Controls.Add(this.cbAssociateProteins);
            this.panel2.Controls.Add(this.btnAdd);
            this.panel2.Controls.Add(this.btnAddAll);
            this.panel2.Controls.Add(this.btnCancel);
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Name = "panel2";
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(this.tableLayoutPanel2, "tableLayoutPanel2");
            this.tableLayoutPanel2.Controls.Add(this.labelFilename, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.comboRedundantSpectra, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.labelRT, 2, 0);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // labelFilename
            // 
            resources.ApplyResources(this.labelFilename, "labelFilename");
            this.labelFilename.Name = "labelFilename";
            // 
            // comboRedundantSpectra
            // 
            resources.ApplyResources(this.comboRedundantSpectra, "comboRedundantSpectra");
            this.comboRedundantSpectra.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRedundantSpectra.DropDownWidth = 200;
            this.comboRedundantSpectra.Name = "comboRedundantSpectra";
            this.comboRedundantSpectra.SelectedIndexChanged += new System.EventHandler(this.redundantSpectrum_Changed);
            this.comboRedundantSpectra.Click += new System.EventHandler(this.redundantSpectrum_InsertComboItems);
            this.comboRedundantSpectra.Enter += new System.EventHandler(this.redundantSpectrum_InsertComboItems);
            // 
            // labelRT
            // 
            resources.ApplyResources(this.labelRT, "labelRT");
            this.labelRT.Name = "labelRT";
            // 
            // cbAssociateProteins
            // 
            resources.ApplyResources(this.cbAssociateProteins, "cbAssociateProteins");
            this.cbAssociateProteins.Name = "cbAssociateProteins";
            this.modeUIHandler.SetUIMode(this.cbAssociateProteins, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.cbAssociateProteins.UseVisualStyleBackColor = true;
            // 
            // btnAdd
            // 
            resources.ApplyResources(this.btnAdd, "btnAdd");
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnAddAll
            // 
            resources.ApplyResources(this.btnAddAll, "btnAddAll");
            this.btnAddAll.Name = "btnAddAll";
            this.btnAddAll.UseVisualStyleBackColor = true;
            this.btnAddAll.Click += new System.EventHandler(this.btnAddAll_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // ViewLibraryPanel
            // 
            this.ViewLibraryPanel.Controls.Add(this.splitMain);
            resources.ApplyResources(this.ViewLibraryPanel, "ViewLibraryPanel");
            this.ViewLibraryPanel.Name = "ViewLibraryPanel";
            // 
            // LibraryLabel
            // 
            resources.ApplyResources(this.LibraryLabel, "LibraryLabel");
            this.LibraryLabel.Name = "LibraryLabel";
            // 
            // contextMenuSpectrum
            // 
            this.contextMenuSpectrum.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { 
            this.ionTypesContextMenuItem,
            this.specialionsContextMenuItem,
            this.fragmentionsContextMenuItem,
            this.precursorIonContextMenuItem,
            this.toolStripSeparator11,
            this.chargesContextMenuItem,
            this.toolStripSeparator12,
            this.ranksContextMenuItem,
            this.scoreContextMenuItem,
            this.ionMzValuesContextMenuItem,
            this.observedMzValuesContextMenuItem,
            this.massErrorToolStripMenuItem,
            this.duplicatesContextMenuItem,
            this.toolStripSeparator13,
            this.lockYaxisContextMenuItem,
            this.toolStripSeparator14,
            this.graphPropsContextMenuItem,
            this.spectrumPropsContextMenuItem,
            this.showChromatogramsContextMenuItem,
            this.toolStripSeparator15,
            this.zoomSpectrumContextMenuItem,
            this.toolStripSeparator27});
            this.contextMenuSpectrum.Name = "contextMenuSpectrum";
            resources.ApplyResources(this.contextMenuSpectrum, "contextMenuSpectrum");

            // 
            // ionTypesContextMenuItem
            // 
            this.ionTypesContextMenuItem.Name = "ionTypesContextMenuItem";
            resources.ApplyResources(this.ionTypesContextMenuItem, "ionTypesContextMenuItem");
            this.ionTypesContextMenuItem.DropDownOpening += new System.EventHandler(this.ionTypeMenuItem_DropDownOpening);


            // 
            // fragmentionsContextMenuItem
            // 
            this.fragmentionsContextMenuItem.CheckOnClick = true;
            this.fragmentionsContextMenuItem.Name = "fragmentionsContextMenuItem";
            resources.ApplyResources(this.fragmentionsContextMenuItem, "fragmentionsContextMenuItem");
            this.fragmentionsContextMenuItem.Click += new System.EventHandler(this.fragmentionsContextMenuItem_Click);
            // 
            // precursorIonContextMenuItem
            // 
            this.precursorIonContextMenuItem.Name = "precursorIonContextMenuItem";
            resources.ApplyResources(this.precursorIonContextMenuItem, "precursorIonContextMenuItem");
            this.precursorIonContextMenuItem.Click += new System.EventHandler(this.precursorIonContextMenuItem_Click);
            
            // 
            // specialionsContextMenuItem
            // 
            this.specialionsContextMenuItem.CheckOnClick = true;
            this.specialionsContextMenuItem.Name = "specialionsContextMenuItem";
            resources.ApplyResources(this.specialionsContextMenuItem, "specialionsContextMenuItem");
            this.specialionsContextMenuItem.Click += new System.EventHandler(this.specialionsContextMenuItem_Click);

            // 
            // toolStripSeparator11
            // 
            this.toolStripSeparator11.Name = "toolStripSeparator11";
            resources.ApplyResources(this.toolStripSeparator11, "toolStripSeparator11");
            // 
            // chargesContextMenuItem
            // 
            this.chargesContextMenuItem.Name = "chargesContextMenuItem";
            resources.ApplyResources(this.chargesContextMenuItem, "chargesContextMenuItem");
            this.chargesContextMenuItem.DropDownOpening += new System.EventHandler(this.chargesMenuItem_DropDownOpening);
            // 
            // toolStripSeparator12
            // 
            this.toolStripSeparator12.Name = "toolStripSeparator12";
            resources.ApplyResources(this.toolStripSeparator12, "toolStripSeparator12");
            // 
            // ranksContextMenuItem
            // 
            this.ranksContextMenuItem.CheckOnClick = true;
            this.ranksContextMenuItem.Name = "ranksContextMenuItem";
            resources.ApplyResources(this.ranksContextMenuItem, "ranksContextMenuItem");
            this.ranksContextMenuItem.Click += new System.EventHandler(this.ranksContextMenuItem_Click);
            // 
            // scoreContextMenuItem
            // 
            this.scoreContextMenuItem.CheckOnClick = true;
            this.scoreContextMenuItem.Name = "scoreContextMenuItem";
            resources.ApplyResources(this.scoreContextMenuItem, "scoreContextMenuItem");
            this.scoreContextMenuItem.Click += new System.EventHandler(this.scoreContextMenuItem_Click);
            // 
            // ionMzValuesContextMenuItem
            // 
            this.ionMzValuesContextMenuItem.CheckOnClick = true;
            this.ionMzValuesContextMenuItem.Name = "ionMzValuesContextMenuItem";
            resources.ApplyResources(this.ionMzValuesContextMenuItem, "ionMzValuesContextMenuItem");
            this.ionMzValuesContextMenuItem.Click += new System.EventHandler(this.ionMzValuesContextMenuItem_Click);
            // 
            // observedMzValuesContextMenuItem
            // 
            this.observedMzValuesContextMenuItem.CheckOnClick = true;
            this.observedMzValuesContextMenuItem.Name = "observedMzValuesContextMenuItem";
            resources.ApplyResources(this.observedMzValuesContextMenuItem, "observedMzValuesContextMenuItem");
            this.observedMzValuesContextMenuItem.Click += new System.EventHandler(this.observedMzValuesContextMenuItem_Click);
            // 
            // massErrorToolStripMenuItem
            // 
            this.massErrorToolStripMenuItem.CheckOnClick = true;
            this.massErrorToolStripMenuItem.Name = "massErrorToolStripMenuItem";
            resources.ApplyResources(this.massErrorToolStripMenuItem, "massErrorToolStripMenuItem");
            this.massErrorToolStripMenuItem.Click += new System.EventHandler(this.massErrorToolStripMenuItem_Click);
            // 
            // duplicatesContextMenuItem
            // 
            this.duplicatesContextMenuItem.CheckOnClick = true;
            this.duplicatesContextMenuItem.Name = "duplicatesContextMenuItem";
            resources.ApplyResources(this.duplicatesContextMenuItem, "duplicatesContextMenuItem");
            this.duplicatesContextMenuItem.Click += new System.EventHandler(this.duplicatesContextMenuItem_Click);
            // 
            // toolStripSeparator13
            // 
            this.toolStripSeparator13.Name = "toolStripSeparator13";
            resources.ApplyResources(this.toolStripSeparator13, "toolStripSeparator13");
            // 
            // lockYaxisContextMenuItem
            // 
            this.lockYaxisContextMenuItem.CheckOnClick = true;
            this.lockYaxisContextMenuItem.Name = "lockYaxisContextMenuItem";
            resources.ApplyResources(this.lockYaxisContextMenuItem, "lockYaxisContextMenuItem");
            this.lockYaxisContextMenuItem.Click += new System.EventHandler(this.lockYaxisContextMenuItem_Click);
            // 
            // toolStripSeparator14
            // 
            this.toolStripSeparator14.Name = "toolStripSeparator14";
            resources.ApplyResources(this.toolStripSeparator14, "toolStripSeparator14");
            // 
            // graphPropsContextMenuItem
            // 
            this.graphPropsContextMenuItem.Name = "graphPropsContextMenuItem";
            resources.ApplyResources(this.graphPropsContextMenuItem, "graphPropsContextMenuItem");
            this.graphPropsContextMenuItem.Click += new System.EventHandler(this.graphPropsContextMenuItem_Click);
            // 
            // spectrumPropsContextMenuItem
            // 
            this.spectrumPropsContextMenuItem.Name = "spectrumPropsContextMenuItem";
            resources.ApplyResources(this.spectrumPropsContextMenuItem, "spectrumPropsContextMenuItem");
            this.spectrumPropsContextMenuItem.Click += new System.EventHandler(this.spectrumPropsContextMenuItem_Click);
            // 
            // showChromatogramsContextMenuItem
            // 
            this.showChromatogramsContextMenuItem.Name = "showChromatogramsContextMenuItem";
            resources.ApplyResources(this.showChromatogramsContextMenuItem, "showChromatogramsContextMenuItem");
            this.showChromatogramsContextMenuItem.Click += new System.EventHandler(this.showChromatogramsToolStripMenuItem_Click);
            // 
            // toolStripSeparator15
            // 
            this.toolStripSeparator15.Name = "toolStripSeparator15";
            resources.ApplyResources(this.toolStripSeparator15, "toolStripSeparator15");
            // 
            // zoomSpectrumContextMenuItem
            // 
            this.zoomSpectrumContextMenuItem.Name = "zoomSpectrumContextMenuItem";
            resources.ApplyResources(this.zoomSpectrumContextMenuItem, "zoomSpectrumContextMenuItem");
            this.zoomSpectrumContextMenuItem.Click += new System.EventHandler(this.zoomSpectrumContextMenuItem_Click);
            // 
            // toolStripSeparator27
            // 
            this.toolStripSeparator27.Name = "toolStripSeparator27";
            resources.ApplyResources(this.toolStripSeparator27, "toolStripSeparator27");
            // 
            // ViewLibraryDlg
            // 
            this.AcceptButton = this.btnCancel;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.LibraryLabel);
            this.Controls.Add(this.ViewLibraryPanel);
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ViewLibraryDlg";
            this.ShowInTaskbar = false;
            this.Activated += new System.EventHandler(this.ViewLibraryDlg_Activated);
            this.Deactivate += new System.EventHandler(this.ViewLibraryDlg_Deactivate);
            this.Load += new System.EventHandler(this.ViewLibraryDlg_Load);
            this.Shown += new System.EventHandler(this.ViewLibraryDlg_Shown);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ViewLibraryDlg_KeyDown);
            this.splitPeptideList.Panel1.ResumeLayout(false);
            this.splitPeptideList.Panel2.ResumeLayout(false);
            this.splitPeptideList.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitPeptideList)).EndInit();
            this.splitPeptideList.ResumeLayout(false);
            this.PeptideListPanel.ResumeLayout(false);
            this.PeptideListPanel.PerformLayout();
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.PeptideEditPanel.ResumeLayout(false);
            this.PeptideEditPanel.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.GraphPanel.ResumeLayout(false);
            this.GraphPanel.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.ViewLibraryPanel.ResumeLayout(false);
            this.contextMenuSpectrum.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.SplitContainer splitPeptideList;
        private System.Windows.Forms.Panel PeptideListPanel;
        private System.Windows.Forms.ListBox listPeptide;
        private System.Windows.Forms.Label PageCount;
        private System.Windows.Forms.Label PeptideCount;
        private System.Windows.Forms.LinkLabel NextLink;
        private System.Windows.Forms.LinkLabel PreviousLink;
        private System.Windows.Forms.Panel PeptideEditPanel;
        private System.Windows.Forms.TextBox textPeptide;
        private System.Windows.Forms.Panel ViewLibraryPanel;
        private System.Windows.Forms.Label PeptideLabel;
        private System.Windows.Forms.ComboBox comboLibrary;
        private System.Windows.Forms.Label LibraryLabel;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Panel GraphPanel;
        private System.Windows.Forms.ContextMenuStrip contextMenuSpectrum;
        private System.Windows.Forms.ToolStripMenuItem ionTypesContextMenuItem;

        private System.Windows.Forms.ToolStripMenuItem fragmentionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem precursorIonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem specialionsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator11;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator12;
        private System.Windows.Forms.ToolStripMenuItem ranksContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem duplicatesContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator13;
        private System.Windows.Forms.ToolStripMenuItem lockYaxisContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator14;
        private System.Windows.Forms.ToolStripMenuItem graphPropsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem spectrumPropsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator15;
        private System.Windows.Forms.ToolStripMenuItem zoomSpectrumContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator27;
        private System.Windows.Forms.CheckBox cbShowModMasses;
        private System.Windows.Forms.ToolStripMenuItem ionMzValuesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem observedMzValuesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorToolStripMenuItem;
        private System.Windows.Forms.Button btnAddAll;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.CheckBox cbAssociateProteins;
        private System.Windows.Forms.ToolStripMenuItem chargesContextMenuItem;
        private System.Windows.Forms.Label labelRT;
        private System.Windows.Forms.Button btnLibDetails;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.ToolStripMenuItem showChromatogramsContextMenuItem;
        private System.Windows.Forms.Label MoleculeLabel;
        private System.Windows.Forms.ToolStripMenuItem scoreContextMenuItem;
        private System.Windows.Forms.Label filterLabel;
        private System.Windows.Forms.Label byLabel;
        private System.Windows.Forms.ComboBox comboFilterCategory;
        private MsGraphExtension msGraphExtension1;
        private System.Windows.Forms.ToolStripButton propertiesButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Label labelFilename;
        private System.Windows.Forms.ComboBox comboRedundantSpectra;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnAIons;
        private System.Windows.Forms.ToolStripButton btnBIons;
        private System.Windows.Forms.ToolStripButton btnCIons;
        private System.Windows.Forms.ToolStripButton btnXIons;
        private System.Windows.Forms.ToolStripButton btnYIons;
        private System.Windows.Forms.ToolStripButton btnZIons;
        private System.Windows.Forms.ToolStripButton btnFragmentIons;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton charge1Button;
        private System.Windows.Forms.ToolStripButton charge2Button;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton copyMetafileButton;
        private System.Windows.Forms.ToolStripButton btnCopy;
        private System.Windows.Forms.ToolStripButton btnSave;
        private System.Windows.Forms.ToolStripButton btnPrint;
    }
}
