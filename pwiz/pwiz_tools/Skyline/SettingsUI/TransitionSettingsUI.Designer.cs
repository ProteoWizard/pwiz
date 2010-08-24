namespace pwiz.Skyline.SettingsUI
{
    partial class TransitionSettingsUI
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TransitionSettingsUI));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabGeneral = new System.Windows.Forms.TabPage();
            this.labelOptimizeType = new System.Windows.Forms.Label();
            this.comboOptimizeType = new System.Windows.Forms.ComboBox();
            this.cbUseOptimized = new System.Windows.Forms.CheckBox();
            this.comboDeclusterPotential = new System.Windows.Forms.ComboBox();
            this.label12 = new System.Windows.Forms.Label();
            this.comboCollisionEnergy = new System.Windows.Forms.ComboBox();
            this.label7 = new System.Windows.Forms.Label();
            this.comboIonMass = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.comboPrecursorMass = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tabFilter = new System.Windows.Forms.TabPage();
            this.textIonTypes = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.cbAutoSelect = new System.Windows.Forms.CheckBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textExclusionWindow = new System.Windows.Forms.TextBox();
            this.label19 = new System.Windows.Forms.Label();
            this.btnEditSpecialTransitions = new System.Windows.Forms.Button();
            this.label18 = new System.Windows.Forms.Label();
            this.listAlwaysAdd = new System.Windows.Forms.CheckedListBox();
            this.comboRangeFrom = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboRangeTo = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textIonCharges = new System.Windows.Forms.TextBox();
            this.textPrecursorCharges = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.tabLibrary = new System.Windows.Forms.TabPage();
            this.label9 = new System.Windows.Forms.Label();
            this.panelPick = new System.Windows.Forms.Panel();
            this.radioAllAndFiltered = new System.Windows.Forms.RadioButton();
            this.radioFiltered = new System.Windows.Forms.RadioButton();
            this.radioAll = new System.Windows.Forms.RadioButton();
            this.label14 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.textIonCount = new System.Windows.Forms.TextBox();
            this.textTolerance = new System.Windows.Forms.TextBox();
            this.cbLibraryPick = new System.Windows.Forms.CheckBox();
            this.label13 = new System.Windows.Forms.Label();
            this.tabInstrument = new System.Windows.Forms.TabPage();
            this.cbDynamicMinimum = new System.Windows.Forms.CheckBox();
            this.textMaxTrans = new System.Windows.Forms.TextBox();
            this.label17 = new System.Windows.Forms.Label();
            this.textMzMatchTolerance = new System.Windows.Forms.TextBox();
            this.label16 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.textMaxMz = new System.Windows.Forms.TextBox();
            this.textMinMz = new System.Windows.Forms.TextBox();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.tabControl1.SuspendLayout();
            this.tabGeneral.SuspendLayout();
            this.tabFilter.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tabLibrary.SuspendLayout();
            this.panelPick.SuspendLayout();
            this.tabInstrument.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(308, 410);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(227, 410);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 1;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabGeneral);
            this.tabControl1.Controls.Add(this.tabFilter);
            this.tabControl1.Controls.Add(this.tabLibrary);
            this.tabControl1.Controls.Add(this.tabInstrument);
            this.tabControl1.DataBindings.Add(new System.Windows.Forms.Binding("SelectedIndex", global::pwiz.Skyline.Properties.Settings.Default, "TransitionSettingsTab", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.tabControl1.Location = new System.Drawing.Point(12, 12);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = global::pwiz.Skyline.Properties.Settings.Default.TransitionSettingsTab;
            this.tabControl1.Size = new System.Drawing.Size(371, 390);
            this.tabControl1.TabIndex = 0;
            // 
            // tabGeneral
            // 
            this.tabGeneral.Controls.Add(this.labelOptimizeType);
            this.tabGeneral.Controls.Add(this.comboOptimizeType);
            this.tabGeneral.Controls.Add(this.cbUseOptimized);
            this.tabGeneral.Controls.Add(this.comboDeclusterPotential);
            this.tabGeneral.Controls.Add(this.label12);
            this.tabGeneral.Controls.Add(this.comboCollisionEnergy);
            this.tabGeneral.Controls.Add(this.label7);
            this.tabGeneral.Controls.Add(this.comboIonMass);
            this.tabGeneral.Controls.Add(this.label2);
            this.tabGeneral.Controls.Add(this.comboPrecursorMass);
            this.tabGeneral.Controls.Add(this.label1);
            this.tabGeneral.Location = new System.Drawing.Point(4, 22);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Padding = new System.Windows.Forms.Padding(3);
            this.tabGeneral.Size = new System.Drawing.Size(363, 364);
            this.tabGeneral.TabIndex = 0;
            this.tabGeneral.Text = "Prediction";
            this.tabGeneral.UseVisualStyleBackColor = true;
            // 
            // labelOptimizeType
            // 
            this.labelOptimizeType.AutoSize = true;
            this.labelOptimizeType.Location = new System.Drawing.Point(23, 187);
            this.labelOptimizeType.Name = "labelOptimizeType";
            this.labelOptimizeType.Size = new System.Drawing.Size(64, 13);
            this.labelOptimizeType.TabIndex = 9;
            this.labelOptimizeType.Text = "&Optimize by:";
            this.labelOptimizeType.Visible = false;
            // 
            // comboOptimizeType
            // 
            this.comboOptimizeType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOptimizeType.FormattingEnabled = true;
            this.comboOptimizeType.Items.AddRange(new object[] {
            "Precursor",
            "Transition"});
            this.comboOptimizeType.Location = new System.Drawing.Point(23, 206);
            this.comboOptimizeType.Name = "comboOptimizeType";
            this.comboOptimizeType.Size = new System.Drawing.Size(121, 21);
            this.comboOptimizeType.TabIndex = 10;
            this.helpTip.SetToolTip(this.comboOptimizeType, "Specifies if optimization is to be done either for all transitions\r\nin each precu" +
                    "rsor totaled or for each transition separately");
            this.comboOptimizeType.Visible = false;
            // 
            // cbUseOptimized
            // 
            this.cbUseOptimized.AutoSize = true;
            this.cbUseOptimized.Location = new System.Drawing.Point(23, 157);
            this.cbUseOptimized.Name = "cbUseOptimized";
            this.cbUseOptimized.Size = new System.Drawing.Size(204, 17);
            this.cbUseOptimized.TabIndex = 8;
            this.cbUseOptimized.Text = "&Use optimization values when present";
            this.helpTip.SetToolTip(this.cbUseOptimized, "If checked, the any measured CE or DP optimization values will\r\nbe used to select" +
                    " the value exported in methods and transition\r\nlists.  The value producing the m" +
                    "aximum total peak area will be\r\nchosen.");
            this.cbUseOptimized.UseVisualStyleBackColor = true;
            this.cbUseOptimized.CheckedChanged += new System.EventHandler(this.cbUseOptimized_CheckedChanged);
            // 
            // comboDeclusterPotential
            // 
            this.comboDeclusterPotential.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDeclusterPotential.FormattingEnabled = true;
            this.comboDeclusterPotential.Location = new System.Drawing.Point(208, 109);
            this.comboDeclusterPotential.Name = "comboDeclusterPotential";
            this.comboDeclusterPotential.Size = new System.Drawing.Size(121, 21);
            this.comboDeclusterPotential.TabIndex = 7;
            this.helpTip.SetToolTip(this.comboDeclusterPotential, resources.GetString("comboDeclusterPotential.ToolTip"));
            this.comboDeclusterPotential.SelectedIndexChanged += new System.EventHandler(this.comboDeclusterPotential_SelectedIndexChanged);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(208, 93);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(112, 13);
            this.label12.TabIndex = 6;
            this.label12.Text = "&Declustering potential:";
            // 
            // comboCollisionEnergy
            // 
            this.comboCollisionEnergy.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCollisionEnergy.FormattingEnabled = true;
            this.comboCollisionEnergy.Location = new System.Drawing.Point(23, 109);
            this.comboCollisionEnergy.Name = "comboCollisionEnergy";
            this.comboCollisionEnergy.Size = new System.Drawing.Size(121, 21);
            this.comboCollisionEnergy.TabIndex = 5;
            this.helpTip.SetToolTip(this.comboCollisionEnergy, "A linear equation used to predict the optimal collsion energy (CE)\r\nfor each prec" +
                    "ursor from its mass-to-charge ratio.  Isotope labeled\r\npeptides use the predicte" +
                    "d CE for the unlabeled form.");
            this.comboCollisionEnergy.SelectedIndexChanged += new System.EventHandler(this.comboCollisionEnergy_SelectedIndexChanged);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(20, 92);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(83, 13);
            this.label7.TabIndex = 4;
            this.label7.Text = "&Collision energy:";
            // 
            // comboIonMass
            // 
            this.comboIonMass.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboIonMass.FormattingEnabled = true;
            this.comboIonMass.Items.AddRange(new object[] {
            "Monoisotopic",
            "Average"});
            this.comboIonMass.Location = new System.Drawing.Point(208, 39);
            this.comboIonMass.Name = "comboIonMass";
            this.comboIonMass.Size = new System.Drawing.Size(121, 21);
            this.comboIonMass.TabIndex = 3;
            this.helpTip.SetToolTip(this.comboIonMass, "Molecular mass calculation strategy (monoisotopic or average)\r\nto use in calculat" +
                    "ing all product ion masses");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(205, 22);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(91, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Pro&duct ion mass:";
            // 
            // comboPrecursorMass
            // 
            this.comboPrecursorMass.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboPrecursorMass.FormattingEnabled = true;
            this.comboPrecursorMass.Items.AddRange(new object[] {
            "Monoisotopic",
            "Average"});
            this.comboPrecursorMass.Location = new System.Drawing.Point(23, 39);
            this.comboPrecursorMass.Name = "comboPrecursorMass";
            this.comboPrecursorMass.Size = new System.Drawing.Size(121, 21);
            this.comboPrecursorMass.TabIndex = 1;
            this.helpTip.SetToolTip(this.comboPrecursorMass, "Molecular mass calculation strategy (monoisotopic or average)\r\nto use in calculat" +
                    "ing all precursor masses");
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(20, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(82, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Precursor mass:";
            // 
            // tabFilter
            // 
            this.tabFilter.Controls.Add(this.textIonTypes);
            this.tabFilter.Controls.Add(this.label8);
            this.tabFilter.Controls.Add(this.cbAutoSelect);
            this.tabFilter.Controls.Add(this.groupBox1);
            this.tabFilter.Controls.Add(this.textIonCharges);
            this.tabFilter.Controls.Add(this.textPrecursorCharges);
            this.tabFilter.Controls.Add(this.label6);
            this.tabFilter.Controls.Add(this.label5);
            this.tabFilter.Location = new System.Drawing.Point(4, 22);
            this.tabFilter.Name = "tabFilter";
            this.tabFilter.Padding = new System.Windows.Forms.Padding(3);
            this.tabFilter.Size = new System.Drawing.Size(363, 364);
            this.tabFilter.TabIndex = 1;
            this.tabFilter.Text = "Filter";
            this.tabFilter.UseVisualStyleBackColor = true;
            // 
            // textIonTypes
            // 
            this.textIonTypes.Location = new System.Drawing.Point(266, 40);
            this.textIonTypes.Name = "textIonTypes";
            this.textIonTypes.Size = new System.Drawing.Size(76, 20);
            this.textIonTypes.TabIndex = 5;
            this.helpTip.SetToolTip(this.textIonTypes, "A list of comma separated product ion types (a, b, c, x, y or z) to use\r\nfor calc" +
                    "ulating potential product ions");
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(263, 23);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(53, 13);
            this.label8.TabIndex = 4;
            this.label8.Text = "Ion &types:";
            // 
            // cbAutoSelect
            // 
            this.cbAutoSelect.AutoSize = true;
            this.cbAutoSelect.Location = new System.Drawing.Point(21, 332);
            this.cbAutoSelect.Name = "cbAutoSelect";
            this.cbAutoSelect.Size = new System.Drawing.Size(188, 17);
            this.cbAutoSelect.TabIndex = 7;
            this.cbAutoSelect.Text = "&Auto-select all matching transitions";
            this.helpTip.SetToolTip(this.cbAutoSelect, "If checked, tranitions are automatically chosen for peptides\r\nusing specified fil" +
                    "ter and library settings.  Otherwise, all transition\r\nselection must be done by " +
                    "manual document editing.");
            this.cbAutoSelect.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.textExclusionWindow);
            this.groupBox1.Controls.Add(this.label19);
            this.groupBox1.Controls.Add(this.btnEditSpecialTransitions);
            this.groupBox1.Controls.Add(this.label18);
            this.groupBox1.Controls.Add(this.listAlwaysAdd);
            this.groupBox1.Controls.Add(this.comboRangeFrom);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.comboRangeTo);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Location = new System.Drawing.Point(21, 83);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(321, 238);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Product ions";
            // 
            // textExclusionWindow
            // 
            this.textExclusionWindow.Location = new System.Drawing.Point(16, 205);
            this.textExclusionWindow.Name = "textExclusionWindow";
            this.textExclusionWindow.Size = new System.Drawing.Size(76, 20);
            this.textExclusionWindow.TabIndex = 8;
            this.helpTip.SetToolTip(this.textExclusionWindow, "A m/z window around the precursor m/z within which transitions\r\nare excluded, or " +
                    "blank.  A window of 20 will exclude all transitions\r\nwithin 10 on either side of" +
                    " the precursor m/z.");
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(13, 189);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(162, 13);
            this.label19.TabIndex = 7;
            this.label19.Text = "Precursor m/z exclusion window:";
            // 
            // btnEditSpecialTransitions
            // 
            this.btnEditSpecialTransitions.Location = new System.Drawing.Point(209, 103);
            this.btnEditSpecialTransitions.Name = "btnEditSpecialTransitions";
            this.btnEditSpecialTransitions.Size = new System.Drawing.Size(75, 23);
            this.btnEditSpecialTransitions.TabIndex = 6;
            this.btnEditSpecialTransitions.Text = "&Edit List...";
            this.helpTip.SetToolTip(this.btnEditSpecialTransitions, "Edit the list of available special product ions");
            this.btnEditSpecialTransitions.UseVisualStyleBackColor = true;
            this.btnEditSpecialTransitions.Click += new System.EventHandler(this.btnEditSpecialTransitions_Click);
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(13, 87);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(64, 13);
            this.label18.TabIndex = 4;
            this.label18.Text = "Al&ways add:";
            // 
            // listAlwaysAdd
            // 
            this.listAlwaysAdd.CheckOnClick = true;
            this.listAlwaysAdd.FormattingEnabled = true;
            this.listAlwaysAdd.Location = new System.Drawing.Point(16, 103);
            this.listAlwaysAdd.Name = "listAlwaysAdd";
            this.listAlwaysAdd.Size = new System.Drawing.Size(187, 64);
            this.listAlwaysAdd.TabIndex = 5;
            this.helpTip.SetToolTip(this.listAlwaysAdd, "Set of special product ions to measure when present, even outside\r\nthe filtered r" +
                    "ange");
            // 
            // comboRangeFrom
            // 
            this.comboRangeFrom.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRangeFrom.FormattingEnabled = true;
            this.comboRangeFrom.Location = new System.Drawing.Point(16, 44);
            this.comboRangeFrom.Name = "comboRangeFrom";
            this.comboRangeFrom.Size = new System.Drawing.Size(121, 21);
            this.comboRangeFrom.TabIndex = 1;
            this.helpTip.SetToolTip(this.comboRangeFrom, "Starting product ion for a filtered range of ions");
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 27);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(33, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "&From:";
            // 
            // comboRangeTo
            // 
            this.comboRangeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRangeTo.FormattingEnabled = true;
            this.comboRangeTo.Location = new System.Drawing.Point(163, 43);
            this.comboRangeTo.Name = "comboRangeTo";
            this.comboRangeTo.Size = new System.Drawing.Size(121, 21);
            this.comboRangeTo.TabIndex = 3;
            this.helpTip.SetToolTip(this.comboRangeTo, "End point for a filtered range of product ions");
            this.comboRangeTo.SelectedIndexChanged += new System.EventHandler(this.comboRangeTo_SelectedIndexChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(160, 27);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(23, 13);
            this.label4.TabIndex = 2;
            this.label4.Text = "T&o:";
            // 
            // textIonCharges
            // 
            this.textIonCharges.Location = new System.Drawing.Point(148, 40);
            this.textIonCharges.Name = "textIonCharges";
            this.textIonCharges.Size = new System.Drawing.Size(76, 20);
            this.textIonCharges.TabIndex = 3;
            this.helpTip.SetToolTip(this.textIonCharges, "A list of comma separated charge states to use for calculating\r\nproduct ion mass-" +
                    "to-charge ratios");
            // 
            // textPrecursorCharges
            // 
            this.textPrecursorCharges.Location = new System.Drawing.Point(21, 40);
            this.textPrecursorCharges.Name = "textPrecursorCharges";
            this.textPrecursorCharges.Size = new System.Drawing.Size(76, 20);
            this.textPrecursorCharges.TabIndex = 1;
            this.helpTip.SetToolTip(this.textPrecursorCharges, "A list of comma separated charge states to use for calculating\r\nprecursur mass-to" +
                    "-charge ratios");
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(145, 23);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(66, 13);
            this.label6.TabIndex = 2;
            this.label6.Text = "&Ion charges:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(18, 23);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(96, 13);
            this.label5.TabIndex = 0;
            this.label5.Text = "&Precursor charges:";
            // 
            // tabLibrary
            // 
            this.tabLibrary.Controls.Add(this.label9);
            this.tabLibrary.Controls.Add(this.panelPick);
            this.tabLibrary.Controls.Add(this.textTolerance);
            this.tabLibrary.Controls.Add(this.cbLibraryPick);
            this.tabLibrary.Controls.Add(this.label13);
            this.tabLibrary.Location = new System.Drawing.Point(4, 22);
            this.tabLibrary.Name = "tabLibrary";
            this.tabLibrary.Padding = new System.Windows.Forms.Padding(3);
            this.tabLibrary.Size = new System.Drawing.Size(363, 364);
            this.tabLibrary.TabIndex = 3;
            this.tabLibrary.Text = "Library";
            this.tabLibrary.UseVisualStyleBackColor = true;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(18, 23);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(104, 13);
            this.label9.TabIndex = 0;
            this.label9.Text = "&Ion match tolerance:";
            // 
            // panelPick
            // 
            this.panelPick.Controls.Add(this.radioAllAndFiltered);
            this.panelPick.Controls.Add(this.radioFiltered);
            this.panelPick.Controls.Add(this.radioAll);
            this.panelPick.Controls.Add(this.label14);
            this.panelPick.Controls.Add(this.label15);
            this.panelPick.Controls.Add(this.textIonCount);
            this.panelPick.Location = new System.Drawing.Point(3, 117);
            this.panelPick.Name = "panelPick";
            this.panelPick.Size = new System.Drawing.Size(357, 142);
            this.panelPick.TabIndex = 4;
            // 
            // radioAllAndFiltered
            // 
            this.radioAllAndFiltered.AutoSize = true;
            this.radioAllAndFiltered.Location = new System.Drawing.Point(17, 91);
            this.radioAllAndFiltered.Name = "radioAllAndFiltered";
            this.radioAllAndFiltered.Size = new System.Drawing.Size(306, 17);
            this.radioAllAndFiltered.TabIndex = 4;
            this.radioAllAndFiltered.TabStop = true;
            this.radioAllAndFiltered.Text = "From filtered ion charges and types pl&us filtered product ions";
            this.helpTip.SetToolTip(this.radioAllAndFiltered, resources.GetString("radioAllAndFiltered.ToolTip"));
            this.radioAllAndFiltered.UseVisualStyleBackColor = true;
            // 
            // radioFiltered
            // 
            this.radioFiltered.AutoSize = true;
            this.radioFiltered.Location = new System.Drawing.Point(17, 114);
            this.radioFiltered.Name = "radioFiltered";
            this.radioFiltered.Size = new System.Drawing.Size(143, 17);
            this.radioFiltered.TabIndex = 5;
            this.radioFiltered.TabStop = true;
            this.radioFiltered.Text = "From filtered pro&duct ions";
            this.helpTip.SetToolTip(this.radioFiltered, "Apply parameters from the Filter tab to the selection of MS/MS\r\npeaks matching io" +
                    "ns for ranking");
            this.radioFiltered.UseVisualStyleBackColor = true;
            // 
            // radioAll
            // 
            this.radioAll.AutoSize = true;
            this.radioAll.Location = new System.Drawing.Point(17, 68);
            this.radioAll.Name = "radioAll";
            this.radioAll.Size = new System.Drawing.Size(189, 17);
            this.radioAll.TabIndex = 3;
            this.radioAll.TabStop = true;
            this.radioAll.Text = "From filtered ion &charges and types";
            this.helpTip.SetToolTip(this.radioAll, "Apply only the ion charges and types from the Filter tab to\r\nthe selection of MS/" +
                    "MS peaks matching ions for ranking");
            this.radioAll.UseVisualStyleBackColor = true;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(14, 9);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(31, 13);
            this.label14.TabIndex = 0;
            this.label14.Text = "&Pick:";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(90, 28);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(65, 13);
            this.label15.TabIndex = 2;
            this.label15.Text = "product ions";
            // 
            // textIonCount
            // 
            this.textIonCount.Location = new System.Drawing.Point(17, 25);
            this.textIonCount.Name = "textIonCount";
            this.textIonCount.Size = new System.Drawing.Size(67, 20);
            this.textIonCount.TabIndex = 1;
            this.helpTip.SetToolTip(this.textIonCount, "Pick this many transitions for each precursor by library spectrum\r\npeak intensity" +
                    " ranking");
            // 
            // textTolerance
            // 
            this.textTolerance.Location = new System.Drawing.Point(21, 40);
            this.textTolerance.Name = "textTolerance";
            this.textTolerance.Size = new System.Drawing.Size(67, 20);
            this.textTolerance.TabIndex = 1;
            this.helpTip.SetToolTip(this.textTolerance, "Maximum delta allowed when matching predicted product ion\r\nm/z values with measur" +
                    "ed MS/MS spectral library peak m/z values");
            // 
            // cbLibraryPick
            // 
            this.cbLibraryPick.AutoSize = true;
            this.cbLibraryPick.Location = new System.Drawing.Point(20, 94);
            this.cbLibraryPick.Name = "cbLibraryPick";
            this.cbLibraryPick.Size = new System.Drawing.Size(295, 17);
            this.cbLibraryPick.TabIndex = 3;
            this.cbLibraryPick.Text = "If a library &spectrum is available, pick its most intense ions";
            this.helpTip.SetToolTip(this.cbLibraryPick, "If checked, the transition filter is based on MS/MS spectral library peaks\r\nwhene" +
                    "ver peptides are matched to library spectrum.");
            this.cbLibraryPick.UseVisualStyleBackColor = true;
            this.cbLibraryPick.CheckedChanged += new System.EventHandler(this.cbLibraryPick_CheckedChanged);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(94, 43);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(43, 13);
            this.label13.TabIndex = 2;
            this.label13.Text = "Daltons";
            // 
            // tabInstrument
            // 
            this.tabInstrument.Controls.Add(this.cbDynamicMinimum);
            this.tabInstrument.Controls.Add(this.textMaxTrans);
            this.tabInstrument.Controls.Add(this.label17);
            this.tabInstrument.Controls.Add(this.textMzMatchTolerance);
            this.tabInstrument.Controls.Add(this.label16);
            this.tabInstrument.Controls.Add(this.label10);
            this.tabInstrument.Controls.Add(this.label11);
            this.tabInstrument.Controls.Add(this.textMaxMz);
            this.tabInstrument.Controls.Add(this.textMinMz);
            this.tabInstrument.Location = new System.Drawing.Point(4, 22);
            this.tabInstrument.Name = "tabInstrument";
            this.tabInstrument.Size = new System.Drawing.Size(363, 364);
            this.tabInstrument.TabIndex = 2;
            this.tabInstrument.Text = "Instrument";
            this.tabInstrument.UseVisualStyleBackColor = true;
            // 
            // cbDynamicMinimum
            // 
            this.cbDynamicMinimum.AutoSize = true;
            this.cbDynamicMinimum.Location = new System.Drawing.Point(27, 64);
            this.cbDynamicMinimum.Name = "cbDynamicMinimum";
            this.cbDynamicMinimum.Size = new System.Drawing.Size(146, 17);
            this.cbDynamicMinimum.TabIndex = 4;
            this.cbDynamicMinimum.Text = "Dynamic min product m/z";
            this.helpTip.SetToolTip(this.cbDynamicMinimum, "If checked, minimum m/z value for product ions is calculate\r\ndynamically from the" +
                    " precursor m/z using a specific equation\r\nprovided by Thermo-Scientific for LTQ " +
                    "instruments");
            this.cbDynamicMinimum.UseVisualStyleBackColor = true;
            // 
            // textMaxTrans
            // 
            this.textMaxTrans.Location = new System.Drawing.Point(27, 185);
            this.textMaxTrans.Name = "textMaxTrans";
            this.textMaxTrans.Size = new System.Drawing.Size(68, 20);
            this.textMaxTrans.TabIndex = 8;
            this.helpTip.SetToolTip(this.textMaxTrans, "Maximum total number of transitions measurable by the target\r\ninstrument in a sin" +
                    "gle injection or blank if no maximum applies");
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(24, 169);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(80, 13);
            this.label17.TabIndex = 7;
            this.label17.Text = "M&ax transitions:";
            // 
            // textMzMatchTolerance
            // 
            this.textMzMatchTolerance.Location = new System.Drawing.Point(27, 124);
            this.textMzMatchTolerance.Name = "textMzMatchTolerance";
            this.textMzMatchTolerance.Size = new System.Drawing.Size(68, 20);
            this.textMzMatchTolerance.TabIndex = 6;
            this.helpTip.SetToolTip(this.textMzMatchTolerance, "Maximum delta for matching measured transition m/z values\r\nwith predicted transit" +
                    "ion m/z values");
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(24, 108);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(110, 13);
            this.label16.TabIndex = 5;
            this.label16.Text = "M/Z match &tolerance:";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(187, 22);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(51, 13);
            this.label10.TabIndex = 2;
            this.label10.Text = "Ma&x m/z:";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(24, 22);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(48, 13);
            this.label11.TabIndex = 0;
            this.label11.Text = "&Min m/z:";
            // 
            // textMaxMz
            // 
            this.textMaxMz.Location = new System.Drawing.Point(190, 38);
            this.textMaxMz.Name = "textMaxMz";
            this.textMaxMz.Size = new System.Drawing.Size(68, 20);
            this.textMaxMz.TabIndex = 3;
            this.helpTip.SetToolTip(this.textMaxMz, "Maximum measurable m/z value for the target instrument");
            // 
            // textMinMz
            // 
            this.textMinMz.Location = new System.Drawing.Point(27, 38);
            this.textMinMz.Name = "textMinMz";
            this.textMinMz.Size = new System.Drawing.Size(68, 20);
            this.textMinMz.TabIndex = 1;
            this.helpTip.SetToolTip(this.textMinMz, "Minimum measurable m/z value for the target instrument");
            // 
            // TransitionSettingsUI
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(395, 445);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TransitionSettingsUI";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Transition Settings";
            this.tabControl1.ResumeLayout(false);
            this.tabGeneral.ResumeLayout(false);
            this.tabGeneral.PerformLayout();
            this.tabFilter.ResumeLayout(false);
            this.tabFilter.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.tabLibrary.ResumeLayout(false);
            this.tabLibrary.PerformLayout();
            this.panelPick.ResumeLayout(false);
            this.panelPick.PerformLayout();
            this.tabInstrument.ResumeLayout(false);
            this.tabInstrument.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabGeneral;
        private System.Windows.Forms.TabPage tabFilter;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.ComboBox comboIonMass;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboPrecursorMass;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboRangeFrom;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox comboRangeTo;
        private System.Windows.Forms.ComboBox comboCollisionEnergy;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox textIonCharges;
        private System.Windows.Forms.TextBox textPrecursorCharges;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox cbAutoSelect;
        private System.Windows.Forms.TabPage tabInstrument;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox textMaxMz;
        private System.Windows.Forms.TextBox textMinMz;
        private System.Windows.Forms.ComboBox comboDeclusterPotential;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TextBox textIonTypes;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TabPage tabLibrary;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox textTolerance;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.RadioButton radioFiltered;
        private System.Windows.Forms.RadioButton radioAll;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TextBox textIonCount;
        private System.Windows.Forms.CheckBox cbLibraryPick;
        private System.Windows.Forms.Panel panelPick;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox textMzMatchTolerance;
        private System.Windows.Forms.ComboBox comboOptimizeType;
        private System.Windows.Forms.CheckBox cbUseOptimized;
        private System.Windows.Forms.Label labelOptimizeType;
        private System.Windows.Forms.TextBox textMaxTrans;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.CheckBox cbDynamicMinimum;
        private System.Windows.Forms.RadioButton radioAllAndFiltered;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.CheckedListBox listAlwaysAdd;
        private System.Windows.Forms.TextBox textExclusionWindow;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Button btnEditSpecialTransitions;
        private System.Windows.Forms.ToolTip helpTip;
    }
}