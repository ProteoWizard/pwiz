namespace pwiz.Skyline.ToolsUI
{
    partial class ToolOptionsUI
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ToolOptionsUI));
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPanorama = new System.Windows.Forms.TabPage();
            this.listboxServers = new System.Windows.Forms.ListBox();
            this.lblServers = new System.Windows.Forms.Label();
            this.btnEditServers = new System.Windows.Forms.Button();
            this.tabRemote = new System.Windows.Forms.TabPage();
            this.listBoxRemoteAccounts = new System.Windows.Forms.ListBox();
            this.lblChorusAccounts = new System.Windows.Forms.Label();
            this.btnEditChorusAccountList = new System.Windows.Forms.Button();
            this.tabLanguage = new System.Windows.Forms.TabPage();
            this.labelDisplayLanguage = new System.Windows.Forms.Label();
            this.listBoxLanguages = new System.Windows.Forms.ListBox();
            this.tabMisc = new System.Windows.Forms.TabPage();
            this.btnResetSettings = new System.Windows.Forms.Button();
            this.comboCompactFormatOption = new System.Windows.Forms.ComboBox();
            this.lblCompactDocumentFormat = new System.Windows.Forms.Label();
            this.checkBoxShowWizard = new System.Windows.Forms.CheckBox();
            this.tabDisplay = new System.Windows.Forms.TabPage();
            this.comboColorScheme = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.powerOfTenCheckBox = new System.Windows.Forms.CheckBox();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.prositServerStatusLabel = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.iRTModelCombo = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.intensityModelCombo = new System.Windows.Forms.ComboBox();
            this.prositServerLabel = new System.Windows.Forms.Label();
            this.prositServerCombo = new System.Windows.Forms.ComboBox();
            this.prositDescrLabel = new System.Windows.Forms.LinkLabel();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.ceLabel = new System.Windows.Forms.Label();
            this.ceCombo = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            this.tabControl.SuspendLayout();
            this.tabPanorama.SuspendLayout();
            this.tabRemote.SuspendLayout();
            this.tabLanguage.SuspendLayout();
            this.tabMisc.SuspendLayout();
            this.tabDisplay.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            resources.ApplyResources(this.tabControl, "tabControl");
            this.tabControl.Controls.Add(this.tabPanorama);
            this.tabControl.Controls.Add(this.tabRemote);
            this.tabControl.Controls.Add(this.tabLanguage);
            this.tabControl.Controls.Add(this.tabMisc);
            this.tabControl.Controls.Add(this.tabDisplay);
            this.tabControl.Controls.Add(this.tabPage1);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            // 
            // tabPanorama
            // 
            this.tabPanorama.Controls.Add(this.listboxServers);
            this.tabPanorama.Controls.Add(this.lblServers);
            this.tabPanorama.Controls.Add(this.btnEditServers);
            resources.ApplyResources(this.tabPanorama, "tabPanorama");
            this.tabPanorama.Name = "tabPanorama";
            this.tabPanorama.UseVisualStyleBackColor = true;
            // 
            // listboxServers
            // 
            resources.ApplyResources(this.listboxServers, "listboxServers");
            this.listboxServers.FormattingEnabled = true;
            this.listboxServers.Name = "listboxServers";
            this.listboxServers.SelectionMode = System.Windows.Forms.SelectionMode.None;
            // 
            // lblServers
            // 
            resources.ApplyResources(this.lblServers, "lblServers");
            this.lblServers.Name = "lblServers";
            // 
            // btnEditServers
            // 
            resources.ApplyResources(this.btnEditServers, "btnEditServers");
            this.btnEditServers.Name = "btnEditServers";
            this.btnEditServers.UseVisualStyleBackColor = true;
            this.btnEditServers.Click += new System.EventHandler(this.btnEditServers_Click);
            // 
            // tabRemote
            // 
            this.tabRemote.Controls.Add(this.listBoxRemoteAccounts);
            this.tabRemote.Controls.Add(this.lblChorusAccounts);
            this.tabRemote.Controls.Add(this.btnEditChorusAccountList);
            resources.ApplyResources(this.tabRemote, "tabRemote");
            this.tabRemote.Name = "tabRemote";
            this.tabRemote.UseVisualStyleBackColor = true;
            // 
            // listBoxRemoteAccounts
            // 
            resources.ApplyResources(this.listBoxRemoteAccounts, "listBoxRemoteAccounts");
            this.listBoxRemoteAccounts.FormattingEnabled = true;
            this.listBoxRemoteAccounts.Name = "listBoxRemoteAccounts";
            this.listBoxRemoteAccounts.SelectionMode = System.Windows.Forms.SelectionMode.None;
            // 
            // lblChorusAccounts
            // 
            resources.ApplyResources(this.lblChorusAccounts, "lblChorusAccounts");
            this.lblChorusAccounts.Name = "lblChorusAccounts";
            // 
            // btnEditChorusAccountList
            // 
            resources.ApplyResources(this.btnEditChorusAccountList, "btnEditChorusAccountList");
            this.btnEditChorusAccountList.Name = "btnEditChorusAccountList";
            this.btnEditChorusAccountList.UseVisualStyleBackColor = true;
            this.btnEditChorusAccountList.Click += new System.EventHandler(this.btnEditChorusAccountList_Click);
            // 
            // tabLanguage
            // 
            this.tabLanguage.Controls.Add(this.labelDisplayLanguage);
            this.tabLanguage.Controls.Add(this.listBoxLanguages);
            resources.ApplyResources(this.tabLanguage, "tabLanguage");
            this.tabLanguage.Name = "tabLanguage";
            this.tabLanguage.UseVisualStyleBackColor = true;
            // 
            // labelDisplayLanguage
            // 
            resources.ApplyResources(this.labelDisplayLanguage, "labelDisplayLanguage");
            this.labelDisplayLanguage.Name = "labelDisplayLanguage";
            // 
            // listBoxLanguages
            // 
            resources.ApplyResources(this.listBoxLanguages, "listBoxLanguages");
            this.listBoxLanguages.FormattingEnabled = true;
            this.listBoxLanguages.Name = "listBoxLanguages";
            // 
            // tabMisc
            // 
            this.tabMisc.Controls.Add(this.btnResetSettings);
            this.tabMisc.Controls.Add(this.comboCompactFormatOption);
            this.tabMisc.Controls.Add(this.lblCompactDocumentFormat);
            this.tabMisc.Controls.Add(this.checkBoxShowWizard);
            resources.ApplyResources(this.tabMisc, "tabMisc");
            this.tabMisc.Name = "tabMisc";
            this.tabMisc.UseVisualStyleBackColor = true;
            // 
            // btnResetSettings
            // 
            resources.ApplyResources(this.btnResetSettings, "btnResetSettings");
            this.btnResetSettings.Name = "btnResetSettings";
            this.btnResetSettings.UseVisualStyleBackColor = true;
            this.btnResetSettings.Click += new System.EventHandler(this.btnResetSettings_Click);
            // 
            // comboCompactFormatOption
            // 
            this.comboCompactFormatOption.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCompactFormatOption.FormattingEnabled = true;
            resources.ApplyResources(this.comboCompactFormatOption, "comboCompactFormatOption");
            this.comboCompactFormatOption.Name = "comboCompactFormatOption";
            // 
            // lblCompactDocumentFormat
            // 
            resources.ApplyResources(this.lblCompactDocumentFormat, "lblCompactDocumentFormat");
            this.lblCompactDocumentFormat.Name = "lblCompactDocumentFormat";
            // 
            // checkBoxShowWizard
            // 
            resources.ApplyResources(this.checkBoxShowWizard, "checkBoxShowWizard");
            this.checkBoxShowWizard.Name = "checkBoxShowWizard";
            this.checkBoxShowWizard.UseVisualStyleBackColor = true;
            // 
            // tabDisplay
            // 
            this.tabDisplay.Controls.Add(this.comboColorScheme);
            this.tabDisplay.Controls.Add(this.label1);
            this.tabDisplay.Controls.Add(this.powerOfTenCheckBox);
            resources.ApplyResources(this.tabDisplay, "tabDisplay");
            this.tabDisplay.Name = "tabDisplay";
            this.tabDisplay.UseVisualStyleBackColor = true;
            // 
            // comboColorScheme
            // 
            this.comboColorScheme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboColorScheme.FormattingEnabled = true;
            resources.ApplyResources(this.comboColorScheme, "comboColorScheme");
            this.comboColorScheme.Name = "comboColorScheme";
            this.comboColorScheme.SelectedIndexChanged += new System.EventHandler(this.comboColorScheme_SelectedIndexChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // powerOfTenCheckBox
            // 
            resources.ApplyResources(this.powerOfTenCheckBox, "powerOfTenCheckBox");
            this.powerOfTenCheckBox.Name = "powerOfTenCheckBox";
            this.powerOfTenCheckBox.UseVisualStyleBackColor = true;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.ceLabel);
            this.tabPage1.Controls.Add(this.ceCombo);
            this.tabPage1.Controls.Add(this.prositServerStatusLabel);
            this.tabPage1.Controls.Add(this.label3);
            this.tabPage1.Controls.Add(this.iRTModelCombo);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.intensityModelCombo);
            this.tabPage1.Controls.Add(this.prositServerLabel);
            this.tabPage1.Controls.Add(this.prositServerCombo);
            this.tabPage1.Controls.Add(this.prositDescrLabel);
            this.tabPage1.Controls.Add(this.pictureBox1);
            resources.ApplyResources(this.tabPage1, "tabPage1");
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // prositServerStatusLabel
            // 
            resources.ApplyResources(this.prositServerStatusLabel, "prositServerStatusLabel");
            this.prositServerStatusLabel.Name = "prositServerStatusLabel";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // iRTModelCombo
            // 
            this.iRTModelCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.iRTModelCombo.FormattingEnabled = true;
            resources.ApplyResources(this.iRTModelCombo, "iRTModelCombo");
            this.iRTModelCombo.Name = "iRTModelCombo";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // intensityModelCombo
            // 
            this.intensityModelCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.intensityModelCombo.FormattingEnabled = true;
            resources.ApplyResources(this.intensityModelCombo, "intensityModelCombo");
            this.intensityModelCombo.Name = "intensityModelCombo";
            // 
            // prositServerLabel
            // 
            resources.ApplyResources(this.prositServerLabel, "prositServerLabel");
            this.prositServerLabel.Name = "prositServerLabel";
            // 
            // prositServerCombo
            // 
            this.prositServerCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.prositServerCombo.FormattingEnabled = true;
            resources.ApplyResources(this.prositServerCombo, "prositServerCombo");
            this.prositServerCombo.Name = "prositServerCombo";
            this.prositServerCombo.SelectedIndexChanged += new System.EventHandler(this.serverCombo_SelectedIndexChanged);
            // 
            // prositDescrLabel
            // 
            resources.ApplyResources(this.prositDescrLabel, "prositDescrLabel");
            this.prositDescrLabel.Name = "prositDescrLabel";
            this.prositDescrLabel.TabStop = true;
            this.prositDescrLabel.UseCompatibleTextRendering = true;
            this.prositDescrLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.prositDescrLabel_LinkClicked);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::pwiz.Skyline.Properties.Resources.prosit_logo_dark_blue;
            resources.ApplyResources(this.pictureBox1, "pictureBox1");
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.TabStop = false;
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
            // ceLabel
            // 
            resources.ApplyResources(this.ceLabel, "ceLabel");
            this.ceLabel.Name = "ceLabel";
            // 
            // ceCombo
            // 
            this.ceCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ceCombo.FormattingEnabled = true;
            resources.ApplyResources(this.ceCombo, "ceCombo");
            this.ceCombo.Name = "ceCombo";
            // 
            // ToolOptionsUI
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tabControl);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ToolOptionsUI";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.tabControl.ResumeLayout(false);
            this.tabPanorama.ResumeLayout(false);
            this.tabPanorama.PerformLayout();
            this.tabRemote.ResumeLayout(false);
            this.tabRemote.PerformLayout();
            this.tabLanguage.ResumeLayout(false);
            this.tabMisc.ResumeLayout(false);
            this.tabMisc.PerformLayout();
            this.tabDisplay.ResumeLayout(false);
            this.tabDisplay.PerformLayout();
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPanorama;
        private System.Windows.Forms.Button btnEditServers;
        private System.Windows.Forms.Label lblServers;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.ListBox listboxServers;
        private System.Windows.Forms.TabPage tabMisc;
        private System.Windows.Forms.TabPage tabLanguage;
        private System.Windows.Forms.ListBox listBoxLanguages;
        private System.Windows.Forms.Label labelDisplayLanguage;
        private System.Windows.Forms.TabPage tabRemote;
        private System.Windows.Forms.ListBox listBoxRemoteAccounts;
        private System.Windows.Forms.Label lblChorusAccounts;
        private System.Windows.Forms.Button btnEditChorusAccountList;
        private System.Windows.Forms.CheckBox checkBoxShowWizard;
        private System.Windows.Forms.TabPage tabDisplay;
        private System.Windows.Forms.CheckBox powerOfTenCheckBox;
        private System.Windows.Forms.ComboBox comboCompactFormatOption;
        private System.Windows.Forms.Label lblCompactDocumentFormat;
        private System.Windows.Forms.ComboBox comboColorScheme;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnResetSettings;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.ComboBox prositServerCombo;
        private System.Windows.Forms.LinkLabel prositDescrLabel;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox iRTModelCombo;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox intensityModelCombo;
        private System.Windows.Forms.Label prositServerLabel;
        private System.Windows.Forms.Label prositServerStatusLabel;
        private System.Windows.Forms.Label ceLabel;
        private System.Windows.Forms.ComboBox ceCombo;
    }
}