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
            this.comboCompensationVoltage = new System.Windows.Forms.ComboBox();
            this.label19 = new System.Windows.Forms.Label();
            this.comboOptimizationLibrary = new System.Windows.Forms.ComboBox();
            this.label20 = new System.Windows.Forms.Label();
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
            this.cbExclusionUseDIAWindow = new System.Windows.Forms.CheckBox();
            this.lbMZ = new System.Windows.Forms.Label();
            this.textExclusionWindow = new System.Windows.Forms.TextBox();
            this.lbPrecursorMzWindow = new System.Windows.Forms.Label();
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
            this.textMaxInclusions = new System.Windows.Forms.TextBox();
            this.label21 = new System.Windows.Forms.Label();
            this.label30 = new System.Windows.Forms.Label();
            this.label31 = new System.Windows.Forms.Label();
            this.label33 = new System.Windows.Forms.Label();
            this.label34 = new System.Windows.Forms.Label();
            this.textMaxTime = new System.Windows.Forms.TextBox();
            this.textMinTime = new System.Windows.Forms.TextBox();
            this.label26 = new System.Windows.Forms.Label();
            this.label25 = new System.Windows.Forms.Label();
            this.label24 = new System.Windows.Forms.Label();
            this.cbDynamicMinimum = new System.Windows.Forms.CheckBox();
            this.textMaxTrans = new System.Windows.Forms.TextBox();
            this.label17 = new System.Windows.Forms.Label();
            this.textMzMatchTolerance = new System.Windows.Forms.TextBox();
            this.label16 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.textMaxMz = new System.Windows.Forms.TextBox();
            this.textMinMz = new System.Windows.Forms.TextBox();
            this.tabFullScan = new System.Windows.Forms.TabPage();
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
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // tabControl1
            // 
            resources.ApplyResources(this.tabControl1, "tabControl1");
            this.tabControl1.Controls.Add(this.tabGeneral);
            this.tabControl1.Controls.Add(this.tabFilter);
            this.tabControl1.Controls.Add(this.tabLibrary);
            this.tabControl1.Controls.Add(this.tabInstrument);
            this.tabControl1.Controls.Add(this.tabFullScan);
            this.tabControl1.DataBindings.Add(new System.Windows.Forms.Binding("SelectedIndex", global::pwiz.Skyline.Properties.Settings.Default, "TransitionSettingsTab", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = global::pwiz.Skyline.Properties.Settings.Default.TransitionSettingsTab;
            // 
            // tabGeneral
            // 
            this.tabGeneral.Controls.Add(this.comboCompensationVoltage);
            this.tabGeneral.Controls.Add(this.label19);
            this.tabGeneral.Controls.Add(this.comboOptimizationLibrary);
            this.tabGeneral.Controls.Add(this.label20);
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
            resources.ApplyResources(this.tabGeneral, "tabGeneral");
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.UseVisualStyleBackColor = true;
            // 
            // comboCompensationVoltage
            // 
            this.comboCompensationVoltage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCompensationVoltage.FormattingEnabled = true;
            resources.ApplyResources(this.comboCompensationVoltage, "comboCompensationVoltage");
            this.comboCompensationVoltage.Name = "comboCompensationVoltage";
            this.comboCompensationVoltage.SelectedIndexChanged += new System.EventHandler(this.comboCompensationVoltage_SelectedIndexChanged);
            // 
            // label19
            // 
            resources.ApplyResources(this.label19, "label19");
            this.label19.Name = "label19";
            // 
            // comboOptimizationLibrary
            // 
            this.comboOptimizationLibrary.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOptimizationLibrary.FormattingEnabled = true;
            resources.ApplyResources(this.comboOptimizationLibrary, "comboOptimizationLibrary");
            this.comboOptimizationLibrary.Name = "comboOptimizationLibrary";
            this.comboOptimizationLibrary.SelectedIndexChanged += new System.EventHandler(this.comboOptimizationLibrary_SelectedIndexChanged);
            // 
            // label20
            // 
            resources.ApplyResources(this.label20, "label20");
            this.label20.Name = "label20";
            // 
            // labelOptimizeType
            // 
            resources.ApplyResources(this.labelOptimizeType, "labelOptimizeType");
            this.labelOptimizeType.Name = "labelOptimizeType";
            // 
            // comboOptimizeType
            // 
            this.comboOptimizeType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOptimizeType.FormattingEnabled = true;
            this.comboOptimizeType.Items.AddRange(new object[] {
            resources.GetString("comboOptimizeType.Items"),
            resources.GetString("comboOptimizeType.Items1")});
            resources.ApplyResources(this.comboOptimizeType, "comboOptimizeType");
            this.comboOptimizeType.Name = "comboOptimizeType";
            this.helpTip.SetToolTip(this.comboOptimizeType, resources.GetString("comboOptimizeType.ToolTip"));
            // 
            // cbUseOptimized
            // 
            resources.ApplyResources(this.cbUseOptimized, "cbUseOptimized");
            this.cbUseOptimized.Name = "cbUseOptimized";
            this.helpTip.SetToolTip(this.cbUseOptimized, resources.GetString("cbUseOptimized.ToolTip"));
            this.cbUseOptimized.UseVisualStyleBackColor = true;
            this.cbUseOptimized.CheckedChanged += new System.EventHandler(this.cbUseOptimized_CheckedChanged);
            // 
            // comboDeclusterPotential
            // 
            this.comboDeclusterPotential.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDeclusterPotential.FormattingEnabled = true;
            resources.ApplyResources(this.comboDeclusterPotential, "comboDeclusterPotential");
            this.comboDeclusterPotential.Name = "comboDeclusterPotential";
            this.helpTip.SetToolTip(this.comboDeclusterPotential, resources.GetString("comboDeclusterPotential.ToolTip"));
            this.comboDeclusterPotential.SelectedIndexChanged += new System.EventHandler(this.comboDeclusterPotential_SelectedIndexChanged);
            // 
            // label12
            // 
            resources.ApplyResources(this.label12, "label12");
            this.label12.Name = "label12";
            // 
            // comboCollisionEnergy
            // 
            this.comboCollisionEnergy.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCollisionEnergy.FormattingEnabled = true;
            resources.ApplyResources(this.comboCollisionEnergy, "comboCollisionEnergy");
            this.comboCollisionEnergy.Name = "comboCollisionEnergy";
            this.helpTip.SetToolTip(this.comboCollisionEnergy, resources.GetString("comboCollisionEnergy.ToolTip"));
            this.comboCollisionEnergy.SelectedIndexChanged += new System.EventHandler(this.comboCollisionEnergy_SelectedIndexChanged);
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // comboIonMass
            // 
            this.comboIonMass.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboIonMass.FormattingEnabled = true;
            this.comboIonMass.Items.AddRange(new object[] {
            resources.GetString("comboIonMass.Items"),
            resources.GetString("comboIonMass.Items1")});
            resources.ApplyResources(this.comboIonMass, "comboIonMass");
            this.comboIonMass.Name = "comboIonMass";
            this.helpTip.SetToolTip(this.comboIonMass, resources.GetString("comboIonMass.ToolTip"));
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // comboPrecursorMass
            // 
            this.comboPrecursorMass.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboPrecursorMass.FormattingEnabled = true;
            this.comboPrecursorMass.Items.AddRange(new object[] {
            resources.GetString("comboPrecursorMass.Items"),
            resources.GetString("comboPrecursorMass.Items1")});
            resources.ApplyResources(this.comboPrecursorMass, "comboPrecursorMass");
            this.comboPrecursorMass.Name = "comboPrecursorMass";
            this.helpTip.SetToolTip(this.comboPrecursorMass, resources.GetString("comboPrecursorMass.ToolTip"));
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
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
            resources.ApplyResources(this.tabFilter, "tabFilter");
            this.tabFilter.Name = "tabFilter";
            this.tabFilter.UseVisualStyleBackColor = true;
            // 
            // textIonTypes
            // 
            resources.ApplyResources(this.textIonTypes, "textIonTypes");
            this.textIonTypes.Name = "textIonTypes";
            this.helpTip.SetToolTip(this.textIonTypes, resources.GetString("textIonTypes.ToolTip"));
            // 
            // label8
            // 
            resources.ApplyResources(this.label8, "label8");
            this.label8.Name = "label8";
            // 
            // cbAutoSelect
            // 
            resources.ApplyResources(this.cbAutoSelect, "cbAutoSelect");
            this.cbAutoSelect.Name = "cbAutoSelect";
            this.helpTip.SetToolTip(this.cbAutoSelect, resources.GetString("cbAutoSelect.ToolTip"));
            this.cbAutoSelect.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cbExclusionUseDIAWindow);
            this.groupBox1.Controls.Add(this.lbMZ);
            this.groupBox1.Controls.Add(this.textExclusionWindow);
            this.groupBox1.Controls.Add(this.lbPrecursorMzWindow);
            this.groupBox1.Controls.Add(this.btnEditSpecialTransitions);
            this.groupBox1.Controls.Add(this.label18);
            this.groupBox1.Controls.Add(this.listAlwaysAdd);
            this.groupBox1.Controls.Add(this.comboRangeFrom);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.comboRangeTo);
            this.groupBox1.Controls.Add(this.label4);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // cbExclusionUseDIAWindow
            // 
            resources.ApplyResources(this.cbExclusionUseDIAWindow, "cbExclusionUseDIAWindow");
            this.cbExclusionUseDIAWindow.Name = "cbExclusionUseDIAWindow";
            this.helpTip.SetToolTip(this.cbExclusionUseDIAWindow, resources.GetString("cbExclusionUseDIAWindow.ToolTip"));
            this.cbExclusionUseDIAWindow.UseVisualStyleBackColor = true;
            // 
            // lbMZ
            // 
            resources.ApplyResources(this.lbMZ, "lbMZ");
            this.lbMZ.Name = "lbMZ";
            // 
            // textExclusionWindow
            // 
            resources.ApplyResources(this.textExclusionWindow, "textExclusionWindow");
            this.textExclusionWindow.Name = "textExclusionWindow";
            this.helpTip.SetToolTip(this.textExclusionWindow, resources.GetString("textExclusionWindow.ToolTip"));
            // 
            // lbPrecursorMzWindow
            // 
            resources.ApplyResources(this.lbPrecursorMzWindow, "lbPrecursorMzWindow");
            this.lbPrecursorMzWindow.Name = "lbPrecursorMzWindow";
            // 
            // btnEditSpecialTransitions
            // 
            resources.ApplyResources(this.btnEditSpecialTransitions, "btnEditSpecialTransitions");
            this.btnEditSpecialTransitions.Name = "btnEditSpecialTransitions";
            this.helpTip.SetToolTip(this.btnEditSpecialTransitions, resources.GetString("btnEditSpecialTransitions.ToolTip"));
            this.btnEditSpecialTransitions.UseVisualStyleBackColor = true;
            this.btnEditSpecialTransitions.Click += new System.EventHandler(this.btnEditSpecialTransitions_Click);
            // 
            // label18
            // 
            resources.ApplyResources(this.label18, "label18");
            this.label18.Name = "label18";
            // 
            // listAlwaysAdd
            // 
            this.listAlwaysAdd.CheckOnClick = true;
            this.listAlwaysAdd.FormattingEnabled = true;
            resources.ApplyResources(this.listAlwaysAdd, "listAlwaysAdd");
            this.listAlwaysAdd.Name = "listAlwaysAdd";
            this.helpTip.SetToolTip(this.listAlwaysAdd, resources.GetString("listAlwaysAdd.ToolTip"));
            this.listAlwaysAdd.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.listAlwaysAdd_ItemCheck);
            // 
            // comboRangeFrom
            // 
            this.comboRangeFrom.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRangeFrom.FormattingEnabled = true;
            resources.ApplyResources(this.comboRangeFrom, "comboRangeFrom");
            this.comboRangeFrom.Name = "comboRangeFrom";
            this.helpTip.SetToolTip(this.comboRangeFrom, resources.GetString("comboRangeFrom.ToolTip"));
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // comboRangeTo
            // 
            this.comboRangeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRangeTo.FormattingEnabled = true;
            resources.ApplyResources(this.comboRangeTo, "comboRangeTo");
            this.comboRangeTo.Name = "comboRangeTo";
            this.helpTip.SetToolTip(this.comboRangeTo, resources.GetString("comboRangeTo.ToolTip"));
            this.comboRangeTo.SelectedIndexChanged += new System.EventHandler(this.comboRangeTo_SelectedIndexChanged);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // textIonCharges
            // 
            resources.ApplyResources(this.textIonCharges, "textIonCharges");
            this.textIonCharges.Name = "textIonCharges";
            this.helpTip.SetToolTip(this.textIonCharges, resources.GetString("textIonCharges.ToolTip"));
            // 
            // textPrecursorCharges
            // 
            resources.ApplyResources(this.textPrecursorCharges, "textPrecursorCharges");
            this.textPrecursorCharges.Name = "textPrecursorCharges";
            this.helpTip.SetToolTip(this.textPrecursorCharges, resources.GetString("textPrecursorCharges.ToolTip"));
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // tabLibrary
            // 
            this.tabLibrary.Controls.Add(this.label9);
            this.tabLibrary.Controls.Add(this.panelPick);
            this.tabLibrary.Controls.Add(this.textTolerance);
            this.tabLibrary.Controls.Add(this.cbLibraryPick);
            this.tabLibrary.Controls.Add(this.label13);
            resources.ApplyResources(this.tabLibrary, "tabLibrary");
            this.tabLibrary.Name = "tabLibrary";
            this.tabLibrary.UseVisualStyleBackColor = true;
            // 
            // label9
            // 
            resources.ApplyResources(this.label9, "label9");
            this.label9.Name = "label9";
            // 
            // panelPick
            // 
            this.panelPick.Controls.Add(this.radioAllAndFiltered);
            this.panelPick.Controls.Add(this.radioFiltered);
            this.panelPick.Controls.Add(this.radioAll);
            this.panelPick.Controls.Add(this.label14);
            this.panelPick.Controls.Add(this.label15);
            this.panelPick.Controls.Add(this.textIonCount);
            resources.ApplyResources(this.panelPick, "panelPick");
            this.panelPick.Name = "panelPick";
            // 
            // radioAllAndFiltered
            // 
            resources.ApplyResources(this.radioAllAndFiltered, "radioAllAndFiltered");
            this.radioAllAndFiltered.Name = "radioAllAndFiltered";
            this.radioAllAndFiltered.TabStop = true;
            this.helpTip.SetToolTip(this.radioAllAndFiltered, resources.GetString("radioAllAndFiltered.ToolTip"));
            this.radioAllAndFiltered.UseVisualStyleBackColor = true;
            // 
            // radioFiltered
            // 
            resources.ApplyResources(this.radioFiltered, "radioFiltered");
            this.radioFiltered.Name = "radioFiltered";
            this.radioFiltered.TabStop = true;
            this.helpTip.SetToolTip(this.radioFiltered, resources.GetString("radioFiltered.ToolTip"));
            this.radioFiltered.UseVisualStyleBackColor = true;
            // 
            // radioAll
            // 
            resources.ApplyResources(this.radioAll, "radioAll");
            this.radioAll.Name = "radioAll";
            this.radioAll.TabStop = true;
            this.helpTip.SetToolTip(this.radioAll, resources.GetString("radioAll.ToolTip"));
            this.radioAll.UseVisualStyleBackColor = true;
            // 
            // label14
            // 
            resources.ApplyResources(this.label14, "label14");
            this.label14.Name = "label14";
            // 
            // label15
            // 
            resources.ApplyResources(this.label15, "label15");
            this.label15.Name = "label15";
            // 
            // textIonCount
            // 
            resources.ApplyResources(this.textIonCount, "textIonCount");
            this.textIonCount.Name = "textIonCount";
            this.helpTip.SetToolTip(this.textIonCount, resources.GetString("textIonCount.ToolTip"));
            // 
            // textTolerance
            // 
            resources.ApplyResources(this.textTolerance, "textTolerance");
            this.textTolerance.Name = "textTolerance";
            this.helpTip.SetToolTip(this.textTolerance, resources.GetString("textTolerance.ToolTip"));
            // 
            // cbLibraryPick
            // 
            resources.ApplyResources(this.cbLibraryPick, "cbLibraryPick");
            this.cbLibraryPick.Name = "cbLibraryPick";
            this.helpTip.SetToolTip(this.cbLibraryPick, resources.GetString("cbLibraryPick.ToolTip"));
            this.cbLibraryPick.UseVisualStyleBackColor = true;
            this.cbLibraryPick.CheckedChanged += new System.EventHandler(this.cbLibraryPick_CheckedChanged);
            // 
            // label13
            // 
            resources.ApplyResources(this.label13, "label13");
            this.label13.Name = "label13";
            // 
            // tabInstrument
            // 
            this.tabInstrument.Controls.Add(this.textMaxInclusions);
            this.tabInstrument.Controls.Add(this.label21);
            this.tabInstrument.Controls.Add(this.label30);
            this.tabInstrument.Controls.Add(this.label31);
            this.tabInstrument.Controls.Add(this.label33);
            this.tabInstrument.Controls.Add(this.label34);
            this.tabInstrument.Controls.Add(this.textMaxTime);
            this.tabInstrument.Controls.Add(this.textMinTime);
            this.tabInstrument.Controls.Add(this.label26);
            this.tabInstrument.Controls.Add(this.label25);
            this.tabInstrument.Controls.Add(this.label24);
            this.tabInstrument.Controls.Add(this.cbDynamicMinimum);
            this.tabInstrument.Controls.Add(this.textMaxTrans);
            this.tabInstrument.Controls.Add(this.label17);
            this.tabInstrument.Controls.Add(this.textMzMatchTolerance);
            this.tabInstrument.Controls.Add(this.label16);
            this.tabInstrument.Controls.Add(this.label10);
            this.tabInstrument.Controls.Add(this.label11);
            this.tabInstrument.Controls.Add(this.textMaxMz);
            this.tabInstrument.Controls.Add(this.textMinMz);
            resources.ApplyResources(this.tabInstrument, "tabInstrument");
            this.tabInstrument.Name = "tabInstrument";
            this.tabInstrument.UseVisualStyleBackColor = true;
            // 
            // textMaxInclusions
            // 
            resources.ApplyResources(this.textMaxInclusions, "textMaxInclusions");
            this.textMaxInclusions.Name = "textMaxInclusions";
            this.helpTip.SetToolTip(this.textMaxInclusions, resources.GetString("textMaxInclusions.ToolTip"));
            // 
            // label21
            // 
            resources.ApplyResources(this.label21, "label21");
            this.label21.Name = "label21";
            // 
            // label30
            // 
            resources.ApplyResources(this.label30, "label30");
            this.label30.Name = "label30";
            // 
            // label31
            // 
            resources.ApplyResources(this.label31, "label31");
            this.label31.Name = "label31";
            // 
            // label33
            // 
            resources.ApplyResources(this.label33, "label33");
            this.label33.Name = "label33";
            // 
            // label34
            // 
            resources.ApplyResources(this.label34, "label34");
            this.label34.Name = "label34";
            // 
            // textMaxTime
            // 
            resources.ApplyResources(this.textMaxTime, "textMaxTime");
            this.textMaxTime.Name = "textMaxTime";
            this.helpTip.SetToolTip(this.textMaxTime, resources.GetString("textMaxTime.ToolTip"));
            // 
            // textMinTime
            // 
            resources.ApplyResources(this.textMinTime, "textMinTime");
            this.textMinTime.Name = "textMinTime";
            this.helpTip.SetToolTip(this.textMinTime, resources.GetString("textMinTime.ToolTip"));
            // 
            // label26
            // 
            resources.ApplyResources(this.label26, "label26");
            this.label26.Name = "label26";
            // 
            // label25
            // 
            resources.ApplyResources(this.label25, "label25");
            this.label25.Name = "label25";
            // 
            // label24
            // 
            resources.ApplyResources(this.label24, "label24");
            this.label24.Name = "label24";
            // 
            // cbDynamicMinimum
            // 
            resources.ApplyResources(this.cbDynamicMinimum, "cbDynamicMinimum");
            this.cbDynamicMinimum.Name = "cbDynamicMinimum";
            this.helpTip.SetToolTip(this.cbDynamicMinimum, resources.GetString("cbDynamicMinimum.ToolTip"));
            this.cbDynamicMinimum.UseVisualStyleBackColor = true;
            // 
            // textMaxTrans
            // 
            resources.ApplyResources(this.textMaxTrans, "textMaxTrans");
            this.textMaxTrans.Name = "textMaxTrans";
            this.helpTip.SetToolTip(this.textMaxTrans, resources.GetString("textMaxTrans.ToolTip"));
            // 
            // label17
            // 
            resources.ApplyResources(this.label17, "label17");
            this.label17.Name = "label17";
            // 
            // textMzMatchTolerance
            // 
            resources.ApplyResources(this.textMzMatchTolerance, "textMzMatchTolerance");
            this.textMzMatchTolerance.Name = "textMzMatchTolerance";
            this.helpTip.SetToolTip(this.textMzMatchTolerance, resources.GetString("textMzMatchTolerance.ToolTip"));
            // 
            // label16
            // 
            resources.ApplyResources(this.label16, "label16");
            this.label16.Name = "label16";
            // 
            // label10
            // 
            resources.ApplyResources(this.label10, "label10");
            this.label10.Name = "label10";
            // 
            // label11
            // 
            resources.ApplyResources(this.label11, "label11");
            this.label11.Name = "label11";
            // 
            // textMaxMz
            // 
            resources.ApplyResources(this.textMaxMz, "textMaxMz");
            this.textMaxMz.Name = "textMaxMz";
            this.helpTip.SetToolTip(this.textMaxMz, resources.GetString("textMaxMz.ToolTip"));
            // 
            // textMinMz
            // 
            resources.ApplyResources(this.textMinMz, "textMinMz");
            this.textMinMz.Name = "textMinMz";
            this.helpTip.SetToolTip(this.textMinMz, resources.GetString("textMinMz.ToolTip"));
            // 
            // tabFullScan
            // 
            resources.ApplyResources(this.tabFullScan, "tabFullScan");
            this.tabFullScan.Name = "tabFullScan";
            this.tabFullScan.UseVisualStyleBackColor = true;
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 15000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // TransitionSettingsUI
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TransitionSettingsUI";
            this.ShowInTaskbar = false;
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
        private System.Windows.Forms.Label lbPrecursorMzWindow;
        private System.Windows.Forms.Button btnEditSpecialTransitions;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.Label lbMZ;
        private System.Windows.Forms.TabPage tabFullScan;
        private System.Windows.Forms.Label label30;
        private System.Windows.Forms.Label label31;
        private System.Windows.Forms.Label label33;
        private System.Windows.Forms.Label label34;
        private System.Windows.Forms.TextBox textMaxTime;
        private System.Windows.Forms.TextBox textMinTime;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.TextBox textMaxInclusions;
        private System.Windows.Forms.ComboBox comboOptimizationLibrary;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.CheckBox cbExclusionUseDIAWindow;
        private System.Windows.Forms.ComboBox comboCompensationVoltage;
        private System.Windows.Forms.Label label19;
    }
}
