using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI
{
    partial class PeptideSettingsUI
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
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.toolTipSettings = new System.Windows.Forms.ToolTip(this.components);
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabDigestion = new System.Windows.Forms.TabPage();
            this.label2 = new System.Windows.Forms.Label();
            this.cbMissedCleavages = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.comboEnzyme = new System.Windows.Forms.ComboBox();
            this.label15 = new System.Windows.Forms.Label();
            this.comboBackgroundProteome = new System.Windows.Forms.ComboBox();
            this.tabPrediction = new System.Windows.Forms.TabPage();
            this.label14 = new System.Windows.Forms.Label();
            this.textMeasureRTWindow = new System.Windows.Forms.TextBox();
            this.cbUseMeasuredRT = new System.Windows.Forms.CheckBox();
            this.label13 = new System.Windows.Forms.Label();
            this.comboRetentionTime = new System.Windows.Forms.ComboBox();
            this.label9 = new System.Windows.Forms.Label();
            this.tabFilter = new System.Windows.Forms.TabPage();
            this.cbAutoSelect = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textExcludeAAs = new System.Windows.Forms.TextBox();
            this.cbRaggedEnds = new System.Windows.Forms.CheckBox();
            this.btnEditExlusions = new System.Windows.Forms.Button();
            this.listboxExclusions = new System.Windows.Forms.CheckedListBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.textMaxLength = new System.Windows.Forms.TextBox();
            this.textMinLength = new System.Windows.Forms.TextBox();
            this.tabLibrary = new System.Windows.Forms.TabPage();
            this.btnBuildLibrary = new System.Windows.Forms.Button();
            this.panelPick = new System.Windows.Forms.Panel();
            this.comboRank = new System.Windows.Forms.ComboBox();
            this.labelPeptides = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.textPeptideCount = new System.Windows.Forms.TextBox();
            this.comboMatching = new System.Windows.Forms.ComboBox();
            this.cbLimitPeptides = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.editLibraries = new System.Windows.Forms.Button();
            this.label11 = new System.Windows.Forms.Label();
            this.listLibraries = new System.Windows.Forms.CheckedListBox();
            this.tabModifications = new System.Windows.Forms.TabPage();
            this.btnEditHeavyMods = new System.Windows.Forms.Button();
            this.label10 = new System.Windows.Forms.Label();
            this.listHeavyMods = new System.Windows.Forms.CheckedListBox();
            this.btnEditStaticMods = new System.Windows.Forms.Button();
            this.label8 = new System.Windows.Forms.Label();
            this.listStaticMods = new System.Windows.Forms.CheckedListBox();
            this.tabControl1.SuspendLayout();
            this.tabDigestion.SuspendLayout();
            this.tabPrediction.SuspendLayout();
            this.tabFilter.SuspendLayout();
            this.tabLibrary.SuspendLayout();
            this.panelPick.SuspendLayout();
            this.tabModifications.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(227, 375);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 1;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(308, 375);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabDigestion);
            this.tabControl1.Controls.Add(this.tabPrediction);
            this.tabControl1.Controls.Add(this.tabFilter);
            this.tabControl1.Controls.Add(this.tabLibrary);
            this.tabControl1.Controls.Add(this.tabModifications);
            this.tabControl1.DataBindings.Add(new System.Windows.Forms.Binding("SelectedIndex", global::pwiz.Skyline.Properties.Settings.Default, "PeptideSettingsTab", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.tabControl1.Location = new System.Drawing.Point(12, 12);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = global::pwiz.Skyline.Properties.Settings.Default.PeptideSettingsTab;
            this.tabControl1.Size = new System.Drawing.Size(371, 355);
            this.tabControl1.TabIndex = 0;
            // 
            // tabDigestion
            // 
            this.tabDigestion.Controls.Add(this.label2);
            this.tabDigestion.Controls.Add(this.cbMissedCleavages);
            this.tabDigestion.Controls.Add(this.label1);
            this.tabDigestion.Controls.Add(this.comboEnzyme);
            this.tabDigestion.Controls.Add(this.label15);
            this.tabDigestion.Controls.Add(this.comboBackgroundProteome);
            this.tabDigestion.Location = new System.Drawing.Point(4, 22);
            this.tabDigestion.Name = "tabDigestion";
            this.tabDigestion.Padding = new System.Windows.Forms.Padding(3);
            this.tabDigestion.Size = new System.Drawing.Size(363, 329);
            this.tabDigestion.TabIndex = 0;
            this.tabDigestion.Text = "Digestion";
            this.tabDigestion.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(25, 79);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(117, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "&Max missed cleavages:";
            // 
            // cbMissedCleavages
            // 
            this.cbMissedCleavages.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMissedCleavages.FormattingEnabled = true;
            this.cbMissedCleavages.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9"});
            this.cbMissedCleavages.Location = new System.Drawing.Point(28, 95);
            this.cbMissedCleavages.Name = "cbMissedCleavages";
            this.cbMissedCleavages.Size = new System.Drawing.Size(44, 21);
            this.cbMissedCleavages.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(25, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(47, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Enzyme:";
            // 
            // comboEnzyme
            // 
            this.comboEnzyme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEnzyme.FormattingEnabled = true;
            this.comboEnzyme.Location = new System.Drawing.Point(28, 36);
            this.comboEnzyme.Name = "comboEnzyme";
            this.comboEnzyme.Size = new System.Drawing.Size(169, 21);
            this.comboEnzyme.TabIndex = 1;
            this.comboEnzyme.SelectedIndexChanged += new System.EventHandler(this.enzyme_SelectedIndexChanged);
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(25, 142);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(115, 13);
            this.label15.TabIndex = 4;
            this.label15.Text = "&Background proteome:";
            // 
            // comboBackgroundProteome
            // 
            this.comboBackgroundProteome.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBackgroundProteome.FormattingEnabled = true;
            this.comboBackgroundProteome.Location = new System.Drawing.Point(28, 158);
            this.comboBackgroundProteome.Name = "comboBackgroundProteome";
            this.comboBackgroundProteome.Size = new System.Drawing.Size(169, 21);
            this.comboBackgroundProteome.TabIndex = 5;
            this.comboBackgroundProteome.SelectedIndexChanged += new System.EventHandler(this.comboBackgroundProteome_SelectedIndexChanged);
            // 
            // tabPrediction
            // 
            this.tabPrediction.Controls.Add(this.label14);
            this.tabPrediction.Controls.Add(this.textMeasureRTWindow);
            this.tabPrediction.Controls.Add(this.cbUseMeasuredRT);
            this.tabPrediction.Controls.Add(this.label13);
            this.tabPrediction.Controls.Add(this.comboRetentionTime);
            this.tabPrediction.Controls.Add(this.label9);
            this.tabPrediction.Location = new System.Drawing.Point(4, 22);
            this.tabPrediction.Name = "tabPrediction";
            this.tabPrediction.Size = new System.Drawing.Size(363, 329);
            this.tabPrediction.TabIndex = 3;
            this.tabPrediction.Text = "Prediction";
            this.tabPrediction.UseVisualStyleBackColor = true;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(134, 136);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(43, 13);
            this.label14.TabIndex = 15;
            this.label14.Text = "minutes";
            // 
            // textMeasureRTWindow
            // 
            this.textMeasureRTWindow.Location = new System.Drawing.Point(28, 133);
            this.textMeasureRTWindow.Name = "textMeasureRTWindow";
            this.textMeasureRTWindow.Size = new System.Drawing.Size(100, 20);
            this.textMeasureRTWindow.TabIndex = 14;
            // 
            // cbUseMeasuredRT
            // 
            this.cbUseMeasuredRT.AutoSize = true;
            this.cbUseMeasuredRT.Location = new System.Drawing.Point(31, 86);
            this.cbUseMeasuredRT.Name = "cbUseMeasuredRT";
            this.cbUseMeasuredRT.Size = new System.Drawing.Size(232, 17);
            this.cbUseMeasuredRT.TabIndex = 13;
            this.cbUseMeasuredRT.Text = "&Use measured retention times when present";
            this.cbUseMeasuredRT.UseVisualStyleBackColor = true;
            this.cbUseMeasuredRT.CheckedChanged += new System.EventHandler(this.cbUseMeasuredRT_CheckedChanged);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(28, 116);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(72, 13);
            this.label13.TabIndex = 12;
            this.label13.Text = "Time &window:";
            // 
            // comboRetentionTime
            // 
            this.comboRetentionTime.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRetentionTime.FormattingEnabled = true;
            this.comboRetentionTime.Location = new System.Drawing.Point(28, 36);
            this.comboRetentionTime.Name = "comboRetentionTime";
            this.comboRetentionTime.Size = new System.Drawing.Size(169, 21);
            this.comboRetentionTime.TabIndex = 11;
            this.comboRetentionTime.SelectedIndexChanged += new System.EventHandler(this.comboRetentionTime_SelectedIndexChanged);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(25, 20);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(129, 13);
            this.label9.TabIndex = 10;
            this.label9.Text = "&Retention time regression:";
            // 
            // tabFilter
            // 
            this.tabFilter.Controls.Add(this.cbAutoSelect);
            this.tabFilter.Controls.Add(this.label3);
            this.tabFilter.Controls.Add(this.textExcludeAAs);
            this.tabFilter.Controls.Add(this.cbRaggedEnds);
            this.tabFilter.Controls.Add(this.btnEditExlusions);
            this.tabFilter.Controls.Add(this.listboxExclusions);
            this.tabFilter.Controls.Add(this.label6);
            this.tabFilter.Controls.Add(this.label5);
            this.tabFilter.Controls.Add(this.label4);
            this.tabFilter.Controls.Add(this.textMaxLength);
            this.tabFilter.Controls.Add(this.textMinLength);
            this.tabFilter.Location = new System.Drawing.Point(4, 22);
            this.tabFilter.Name = "tabFilter";
            this.tabFilter.Padding = new System.Windows.Forms.Padding(3);
            this.tabFilter.Size = new System.Drawing.Size(363, 329);
            this.tabFilter.TabIndex = 1;
            this.tabFilter.Text = "Filter";
            this.tabFilter.UseVisualStyleBackColor = true;
            // 
            // cbAutoSelect
            // 
            this.cbAutoSelect.AutoSize = true;
            this.cbAutoSelect.Location = new System.Drawing.Point(28, 297);
            this.cbAutoSelect.Name = "cbAutoSelect";
            this.cbAutoSelect.Size = new System.Drawing.Size(181, 17);
            this.cbAutoSelect.TabIndex = 12;
            this.cbAutoSelect.Text = "&Auto-select all matching peptides";
            this.cbAutoSelect.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(25, 70);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(120, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Exclude &N-terminal AAs:";
            // 
            // textExcludeAAs
            // 
            this.textExcludeAAs.Location = new System.Drawing.Point(29, 86);
            this.textExcludeAAs.Name = "textExcludeAAs";
            this.textExcludeAAs.Size = new System.Drawing.Size(44, 20);
            this.textExcludeAAs.TabIndex = 7;
            // 
            // cbRaggedEnds
            // 
            this.cbRaggedEnds.AutoSize = true;
            this.cbRaggedEnds.Location = new System.Drawing.Point(29, 123);
            this.cbRaggedEnds.Name = "cbRaggedEnds";
            this.cbRaggedEnds.Size = new System.Drawing.Size(169, 17);
            this.cbRaggedEnds.TabIndex = 8;
            this.cbRaggedEnds.Text = "Exclude potential &ragged ends";
            this.cbRaggedEnds.UseVisualStyleBackColor = true;
            // 
            // btnEditExlusions
            // 
            this.btnEditExlusions.Location = new System.Drawing.Point(211, 176);
            this.btnEditExlusions.Name = "btnEditExlusions";
            this.btnEditExlusions.Size = new System.Drawing.Size(75, 23);
            this.btnEditExlusions.TabIndex = 11;
            this.btnEditExlusions.Text = "E&dit list...";
            this.btnEditExlusions.UseVisualStyleBackColor = true;
            this.btnEditExlusions.Click += new System.EventHandler(this.btnEditExlusions_Click);
            // 
            // listboxExclusions
            // 
            this.listboxExclusions.CheckOnClick = true;
            this.listboxExclusions.FormattingEnabled = true;
            this.listboxExclusions.Location = new System.Drawing.Point(28, 176);
            this.listboxExclusions.Name = "listboxExclusions";
            this.listboxExclusions.Size = new System.Drawing.Size(171, 94);
            this.listboxExclusions.TabIndex = 10;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(25, 160);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(143, 13);
            this.label6.TabIndex = 9;
            this.label6.Text = "&Exclude peptides containing:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(112, 20);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(62, 13);
            this.label5.TabIndex = 2;
            this.label5.Text = "Ma&x length:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(26, 20);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(59, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "&Min length:";
            // 
            // textMaxLength
            // 
            this.textMaxLength.Location = new System.Drawing.Point(115, 36);
            this.textMaxLength.Name = "textMaxLength";
            this.textMaxLength.Size = new System.Drawing.Size(45, 20);
            this.textMaxLength.TabIndex = 3;
            // 
            // textMinLength
            // 
            this.textMinLength.Location = new System.Drawing.Point(29, 36);
            this.textMinLength.Name = "textMinLength";
            this.textMinLength.Size = new System.Drawing.Size(45, 20);
            this.textMinLength.TabIndex = 1;
            // 
            // tabLibrary
            // 
            this.tabLibrary.Controls.Add(this.btnBuildLibrary);
            this.tabLibrary.Controls.Add(this.panelPick);
            this.tabLibrary.Controls.Add(this.editLibraries);
            this.tabLibrary.Controls.Add(this.label11);
            this.tabLibrary.Controls.Add(this.listLibraries);
            this.tabLibrary.Location = new System.Drawing.Point(4, 22);
            this.tabLibrary.Name = "tabLibrary";
            this.tabLibrary.Size = new System.Drawing.Size(363, 329);
            this.tabLibrary.TabIndex = 4;
            this.tabLibrary.Text = "Library";
            this.tabLibrary.UseVisualStyleBackColor = true;
            // 
            // btnBuildLibrary
            // 
            this.btnBuildLibrary.Location = new System.Drawing.Point(264, 66);
            this.btnBuildLibrary.Name = "btnBuildLibrary";
            this.btnBuildLibrary.Size = new System.Drawing.Size(75, 23);
            this.btnBuildLibrary.TabIndex = 4;
            this.btnBuildLibrary.Text = "&Build...";
            this.btnBuildLibrary.UseVisualStyleBackColor = true;
            this.btnBuildLibrary.Click += new System.EventHandler(this.btnBuildLibrary_Click);
            // 
            // panelPick
            // 
            this.panelPick.Controls.Add(this.comboRank);
            this.panelPick.Controls.Add(this.labelPeptides);
            this.panelPick.Controls.Add(this.label12);
            this.panelPick.Controls.Add(this.textPeptideCount);
            this.panelPick.Controls.Add(this.comboMatching);
            this.panelPick.Controls.Add(this.cbLimitPeptides);
            this.panelPick.Controls.Add(this.label7);
            this.panelPick.Location = new System.Drawing.Point(3, 136);
            this.panelPick.Name = "panelPick";
            this.panelPick.Size = new System.Drawing.Size(357, 190);
            this.panelPick.TabIndex = 3;
            // 
            // comboRank
            // 
            this.comboRank.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRank.FormattingEnabled = true;
            this.comboRank.Location = new System.Drawing.Point(25, 87);
            this.comboRank.Name = "comboRank";
            this.comboRank.Size = new System.Drawing.Size(143, 21);
            this.comboRank.TabIndex = 3;
            this.comboRank.SelectedIndexChanged += new System.EventHandler(this.comboRank_SelectedIndexChanged);
            // 
            // labelPeptides
            // 
            this.labelPeptides.AutoSize = true;
            this.labelPeptides.Location = new System.Drawing.Point(99, 159);
            this.labelPeptides.Name = "labelPeptides";
            this.labelPeptides.Size = new System.Drawing.Size(48, 13);
            this.labelPeptides.TabIndex = 6;
            this.labelPeptides.Text = "Peptides";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(22, 14);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(120, 13);
            this.label12.TabIndex = 0;
            this.label12.Text = "&Pick peptides matching:";
            // 
            // textPeptideCount
            // 
            this.textPeptideCount.Location = new System.Drawing.Point(25, 156);
            this.textPeptideCount.Name = "textPeptideCount";
            this.textPeptideCount.Size = new System.Drawing.Size(68, 20);
            this.textPeptideCount.TabIndex = 5;
            this.textPeptideCount.TextChanged += new System.EventHandler(this.textPeptideCount_TextChanged);
            // 
            // comboMatching
            // 
            this.comboMatching.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMatching.FormattingEnabled = true;
            this.comboMatching.Items.AddRange(new object[] {
            "Library",
            "Filter",
            "Library and Filter",
            "Library or Filter"});
            this.comboMatching.Location = new System.Drawing.Point(25, 30);
            this.comboMatching.Name = "comboMatching";
            this.comboMatching.Size = new System.Drawing.Size(143, 21);
            this.comboMatching.TabIndex = 1;
            this.comboMatching.SelectedIndexChanged += new System.EventHandler(this.comboMatching_SelectedIndexChanged);
            // 
            // cbLimitPeptides
            // 
            this.cbLimitPeptides.AutoSize = true;
            this.cbLimitPeptides.Location = new System.Drawing.Point(25, 132);
            this.cbLimitPeptides.Name = "cbLimitPeptides";
            this.cbLimitPeptides.Size = new System.Drawing.Size(143, 17);
            this.cbLimitPeptides.TabIndex = 4;
            this.cbLimitPeptides.Text = "&Limit peptides per protein";
            this.cbLimitPeptides.UseVisualStyleBackColor = true;
            this.cbLimitPeptides.CheckedChanged += new System.EventHandler(this.cbLimitPeptides_CheckedChanged);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(22, 71);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(93, 13);
            this.label7.TabIndex = 2;
            this.label7.Text = "&Rank peptides by:";
            // 
            // editLibraries
            // 
            this.editLibraries.Location = new System.Drawing.Point(264, 36);
            this.editLibraries.Name = "editLibraries";
            this.editLibraries.Size = new System.Drawing.Size(75, 23);
            this.editLibraries.TabIndex = 2;
            this.editLibraries.Text = "&Edit list...";
            this.editLibraries.UseVisualStyleBackColor = true;
            this.editLibraries.Click += new System.EventHandler(this.editLibraries_Click);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(25, 20);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(49, 13);
            this.label11.TabIndex = 0;
            this.label11.Text = "&Libraries:";
            // 
            // listLibraries
            // 
            this.listLibraries.CheckOnClick = true;
            this.listLibraries.FormattingEnabled = true;
            this.listLibraries.Location = new System.Drawing.Point(28, 36);
            this.listLibraries.Name = "listLibraries";
            this.listLibraries.Size = new System.Drawing.Size(217, 94);
            this.listLibraries.TabIndex = 1;
            this.listLibraries.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.listLibraries_ItemCheck);
            // 
            // tabModifications
            // 
            this.tabModifications.Controls.Add(this.btnEditHeavyMods);
            this.tabModifications.Controls.Add(this.label10);
            this.tabModifications.Controls.Add(this.listHeavyMods);
            this.tabModifications.Controls.Add(this.btnEditStaticMods);
            this.tabModifications.Controls.Add(this.label8);
            this.tabModifications.Controls.Add(this.listStaticMods);
            this.tabModifications.Location = new System.Drawing.Point(4, 22);
            this.tabModifications.Name = "tabModifications";
            this.tabModifications.Size = new System.Drawing.Size(363, 329);
            this.tabModifications.TabIndex = 2;
            this.tabModifications.Text = "Modifications";
            this.tabModifications.UseVisualStyleBackColor = true;
            // 
            // btnEditHeavyMods
            // 
            this.btnEditHeavyMods.Location = new System.Drawing.Point(211, 169);
            this.btnEditHeavyMods.Name = "btnEditHeavyMods";
            this.btnEditHeavyMods.Size = new System.Drawing.Size(75, 23);
            this.btnEditHeavyMods.TabIndex = 6;
            this.btnEditHeavyMods.Text = "E&dit list...";
            this.btnEditHeavyMods.UseVisualStyleBackColor = true;
            this.btnEditHeavyMods.Click += new System.EventHandler(this.btnEditHeavyMods_Click);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(25, 153);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(41, 13);
            this.label10.TabIndex = 4;
            this.label10.Text = "&Heavy:";
            // 
            // listHeavyMods
            // 
            this.listHeavyMods.CheckOnClick = true;
            this.listHeavyMods.FormattingEnabled = true;
            this.listHeavyMods.Location = new System.Drawing.Point(28, 169);
            this.listHeavyMods.Name = "listHeavyMods";
            this.listHeavyMods.Size = new System.Drawing.Size(177, 94);
            this.listHeavyMods.TabIndex = 5;
            // 
            // btnEditStaticMods
            // 
            this.btnEditStaticMods.Location = new System.Drawing.Point(211, 36);
            this.btnEditStaticMods.Name = "btnEditStaticMods";
            this.btnEditStaticMods.Size = new System.Drawing.Size(75, 23);
            this.btnEditStaticMods.TabIndex = 3;
            this.btnEditStaticMods.Text = "&Edit list...";
            this.btnEditStaticMods.UseVisualStyleBackColor = true;
            this.btnEditStaticMods.Click += new System.EventHandler(this.btnEditStaticMods_Click);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(25, 20);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(37, 13);
            this.label8.TabIndex = 0;
            this.label8.Text = "&Static:";
            // 
            // listStaticMods
            // 
            this.listStaticMods.CheckOnClick = true;
            this.listStaticMods.FormattingEnabled = true;
            this.listStaticMods.Location = new System.Drawing.Point(28, 36);
            this.listStaticMods.Name = "listStaticMods";
            this.listStaticMods.Size = new System.Drawing.Size(177, 94);
            this.listStaticMods.TabIndex = 1;
            // 
            // PeptideSettingsUI
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(395, 410);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PeptideSettingsUI";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Peptide Settings";
            this.tabControl1.ResumeLayout(false);
            this.tabDigestion.ResumeLayout(false);
            this.tabDigestion.PerformLayout();
            this.tabPrediction.ResumeLayout(false);
            this.tabPrediction.PerformLayout();
            this.tabFilter.ResumeLayout(false);
            this.tabFilter.PerformLayout();
            this.tabLibrary.ResumeLayout(false);
            this.tabLibrary.PerformLayout();
            this.panelPick.ResumeLayout(false);
            this.panelPick.PerformLayout();
            this.tabModifications.ResumeLayout(false);
            this.tabModifications.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ToolTip toolTipSettings;
        private System.Windows.Forms.TabPage tabFilter;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textExcludeAAs;
        private System.Windows.Forms.Button btnEditExlusions;
        private System.Windows.Forms.CheckedListBox listboxExclusions;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textMaxLength;
        private System.Windows.Forms.TextBox textMinLength;
        private System.Windows.Forms.TabPage tabDigestion;
        private System.Windows.Forms.CheckBox cbRaggedEnds;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cbMissedCleavages;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboEnzyme;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.CheckBox cbAutoSelect;
        private System.Windows.Forms.TabPage tabModifications;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.CheckedListBox listStaticMods;
        private System.Windows.Forms.Button btnEditStaticMods;
        private System.Windows.Forms.TabPage tabPrediction;
        private System.Windows.Forms.ComboBox comboRetentionTime;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Button btnEditHeavyMods;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.CheckedListBox listHeavyMods;
        private System.Windows.Forms.TabPage tabLibrary;
        private System.Windows.Forms.Button editLibraries;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.CheckedListBox listLibraries;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox comboMatching;
        private System.Windows.Forms.ComboBox comboRank;
        private System.Windows.Forms.CheckBox cbLimitPeptides;
        private System.Windows.Forms.TextBox textPeptideCount;
        private System.Windows.Forms.Label labelPeptides;
        private System.Windows.Forms.Panel panelPick;
        private System.Windows.Forms.Button btnBuildLibrary;
        private System.Windows.Forms.CheckBox cbUseMeasuredRT;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TextBox textMeasureRTWindow;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.ComboBox comboBackgroundProteome;
    }
}