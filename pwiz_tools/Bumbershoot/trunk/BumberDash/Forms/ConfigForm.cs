using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using BumberDash.Model;
using NHibernate;

namespace BumberDash.Forms
{
    public partial class ConfigForm : Form
    {
        /****************************************************************
         *                            Overview
         * 
         * Takes: Single ConfigFile item from which destination program
         * and initial defaults can be discerned, and list of templates
         *          * OR: String containing destination program, and list of templates
         *          * OR: ISession, indicating template mode is active
         *                and allowing database manipulation to occur
         * 
         * Interface: Three panes on each tab, each corresponding to a program.
         * Duplicate parameters should have duplicate input boxes. Imput box tags
         * should indicate if it is always written to output config file ("true" or "false")
         * 
         * Returns: Contains internal function that returns string
         * containing parameters which are different from the current default
         ****************************************************************/

        /*****************************************************************
         *                       Special Cases
         *          
         *   UseAvgMassOfSequences- DropDownListBox for bool value
         *   NumMinTerminiCleavages- DropDownList index == required int value
         *   Static/Dynamic/PT Mods- All within DataGridView
         *   Min/Max PrecursorAdjustment - Each unit represents a dalton
         *   DeisotopingMode- DropDownList index == required int value
         *   Unimod and Blosum- static value on "Default"
         *****************************************************************/

        private readonly ISession _session; //if in template mode this allows session manipulation
        private readonly Dictionary<Control, string> _defaults; //Stores default values for properties
        private readonly Dictionary<Control, Control> _labelAssociation; //Stores label each propertybox is associated with
        private readonly Dictionary<string, List<Control>> _itemList; //stores a list of controls for each destination program
        private readonly string _baseDirectory; //Stores the assigned initial output directory
        private readonly string _defaultName; //Stores the assigned default configuration name
        private string _filePath; //Stores the assigned file path
        private Dictionary<Control, string> _templateDefaults; //Stores default values for current template
        private IList<ConfigFile> _myriTemplateList; //List of all templates user has specified for Myrimatch
        private IList<ConfigFile> _DTTemplateList; //List of all templates user has specified for Directag
        private IList<ConfigFile> _TRTemplateList; //List of all templates user has specified for Tagrecon

        /// <summary>
        /// Create ConfigForm in template mode
        /// </summary>
        /// <param name="newSession"></param>
        public ConfigForm(ISession newSession)
        {
            InitializeComponent();

            _session = newSession;
            ProgramModeBox.Text = "MyriMatch";
            TemplateModePanel.Visible = true;
            ConfigModePanel.Visible = false;

            _defaults = new Dictionary<Control, string>();
            _templateDefaults = new Dictionary<Control, string>(); //starts as blank for comparison purposes
            _labelAssociation = new Dictionary<Control, Control>();
            _itemList = new Dictionary<string, List<Control>>
                            {
                                {"MyriMatch", new List<Control>()},
                                {"DirecTag", new List<Control>()},
                                {"TagRecon", new List<Control>()}
                            };

            SetInitialValues();
            InitializePane(MyriGenPanel);
            InitializePane(MyriAdvPanel);
            InitializePane(DTGenPanel);
            InitializePane(DTAdvPanel);
            InitializePane(TRGenPanel);
            InitializePane(TRAdvPanel);

            mainTabControl.TabPages.Remove(AdvTab);

            //Add templates to list
            ResetTemplateLists();
        }

        /// <summary>
        /// Create ConfigForm in edit/clone mode
        /// </summary>
        /// <param name="baseConfig"></param>
        /// <param name="baseDirectory"></param>
        /// <param name="templates"></param>
        public ConfigForm(ConfigFile baseConfig, string baseDirectory, string defaultName, IEnumerable<ConfigFile> templates)
        {
            InitializeComponent();

            ProgramModeBox.Text = baseConfig.DestinationProgram;
            _defaults = new Dictionary<Control, string>();
            _templateDefaults = new Dictionary<Control, string>(); //starts as blank for comparison purposes
            _labelAssociation = new Dictionary<Control, Control>();
            _itemList = new Dictionary<string, List<Control>>
                            {
                                {baseConfig.DestinationProgram, new List<Control>()}
                            };
            _filePath = baseConfig.FilePath;
            _baseDirectory = baseDirectory;
            _defaultName = defaultName;
            if (!File.Exists(_filePath))
                SaveOverOldButton.Visible = false;

            SetInitialValues();
            switch (baseConfig.DestinationProgram)
            {
                case "MyriMatch":
                    _myriTemplateList = templates.Where(x => x.DestinationProgram == "MyriMatch").ToList();
                    InitializePane(MyriGenPanel);
                    InitializePane(MyriAdvPanel);
                    MyriInstrumentList.Items.Add("New");
                    foreach (var item in _myriTemplateList)
                        MyriInstrumentList.Items.Add(item.Name);
                    MyriInstrumentList.Text = "New";
                    break;
                case "DirecTag":
                    _DTTemplateList = templates.Where(x => x.DestinationProgram == "DirecTag").ToList();
                    InitializePane(DTGenPanel);
                    InitializePane(DTAdvPanel);
                    DTInstrumentList.Items.Add("New");
                    foreach (var item in _DTTemplateList)
                        DTInstrumentList.Items.Add(item.Name);
                    DTInstrumentList.Text = "New";
                    break;
                case "TagRecon":
                    _TRTemplateList = templates.Where(x => x.DestinationProgram == "TagRecon").ToList();
                    InitializePane(TRGenPanel);
                    InitializePane(TRAdvPanel);
                    TRInstrumentList.Items.Add("New");
                    foreach (var item in _TRTemplateList)
                        TRInstrumentList.Items.Add(item.Name);
                    TRInstrumentList.Text = "New";
                    break;
            }

            mainTabControl.TabPages.Remove(AdvTab);
            LoadConfig(baseConfig);
        }

        /// <summary>
        /// Create ConfigForm in new config mode
        /// </summary>
        /// <param name="configProgram"></param>
        /// <param name="baseDirectory"></param>
        /// <param name="templates"></param>
        public ConfigForm(string configProgram, string baseDirectory, string defaultName, IEnumerable<ConfigFile> templates)
        {
            InitializeComponent();

            ProgramModeBox.Text = configProgram;
            _defaults = new Dictionary<Control, string>();
            _templateDefaults = new Dictionary<Control, string>(); //starts as blank for comparison purposes
            _labelAssociation = new Dictionary<Control, Control>();
            _itemList = new Dictionary<string, List<Control>>
                            {
                                {configProgram, new List<Control>()}
                            };

            _baseDirectory = baseDirectory;
            _defaultName = defaultName;
            SaveOverOldButton.Visible = false;
            SetInitialValues();
            
            switch (configProgram)
            {
                case "MyriMatch":
                    _myriTemplateList = templates.Where(x => x.DestinationProgram == "MyriMatch").ToList();
                    InitializePane(MyriGenPanel);
                    InitializePane(MyriAdvPanel);
                    MyriInstrumentList.Items.Add("New");
                    foreach (var item in _myriTemplateList)
                        MyriInstrumentList.Items.Add(item.Name);
                    MyriInstrumentList.Text = "New";
                    break;
                case "DirecTag":
                    _DTTemplateList = templates.Where(x => x.DestinationProgram == "DirecTag").ToList();
                    InitializePane(DTGenPanel);
                    InitializePane(DTAdvPanel);
                    DTInstrumentList.Items.Add("New");
                    foreach (var item in _DTTemplateList)
                        DTInstrumentList.Items.Add(item.Name);
                    DTInstrumentList.Text = "New";
                    break;
                case "TagRecon":
                    _TRTemplateList = templates.Where(x => x.DestinationProgram == "TagRecon").ToList();
                    InitializePane(TRGenPanel);
                    InitializePane(TRAdvPanel);
                    TRInstrumentList.Items.Add("New");
                    foreach (var item in _TRTemplateList)
                        TRInstrumentList.Items.Add(item.Name);
                    TRInstrumentList.Text = "New";
                    break;
            }
            mainTabControl.TabPages.Remove(AdvTab);
        }

        private void ResetTemplateLists()
        {
            var templateList = _session.QueryOver<ConfigFile>().Where(x => x.FilePath == "Template").List();
            _myriTemplateList = templateList.Where(x => x.DestinationProgram == "MyriMatch").ToList();
            _DTTemplateList = templateList.Where(x => x.DestinationProgram == "DirecTag").ToList();
            _TRTemplateList = templateList.Where(x => x.DestinationProgram == "TagRecon").ToList();

            MyriInstrumentList.Items.Clear();
            DTInstrumentList.Items.Clear();
            TRInstrumentList.Items.Clear();

            MyriInstrumentList.Items.Add("New");
            foreach (var item in _myriTemplateList)
                MyriInstrumentList.Items.Add(item.Name);
            MyriInstrumentList.Text = "New";
            DTInstrumentList.Items.Add("New");
            foreach (var item in _DTTemplateList)
                DTInstrumentList.Items.Add(item.Name);
            DTInstrumentList.Text = "New";
            TRInstrumentList.Items.Add("New");
            foreach (var item in _TRTemplateList)
                TRInstrumentList.Items.Add(item.Name);
            TRInstrumentList.Text = "New";

            foreach (var item in _itemList[ProgramModeBox.Text])
                CheckForChange(item, null);
        }

        /// <summary>
        /// Sets initial (default) values for DropDownList items (cant be done in editor)
        /// </summary>
        private void SetInitialValues()
        {
            MyriUseAvgMassOfSequencesBox.Text = "Mono-Isotopic";
            MyriNumMinTerminiCleavagesBox.Text = "Fully-Specific";
            MyriPrecursorMzToleranceUnitsBox.Text = "daltons";
            MyriFragmentMzToleranceUnitsBox.Text = "daltons";
            MyriDeisotopingModeBox.Text = "Off";
            MyriModTypeList.Text = "Static";
            DTPrecursorMzToleranceUnitsList.Text = "daltons";
            DTFragmentMzToleranceUnitsList.Text = "daltons";
            DTNTerminusMassToleranceUnitsList.Text = "daltons";
            DTCTerminusMassToleranceUnitsList.Text = "daltons";
            DTDeisotopingModeBox.Text = "Precursor Adj Only";
            DTModTypeList.Text = "Static";
            TRUseAvgMassOfSequencesBox.Text = "Mono-Isotopic";
            TRNumMinTerminiCleavagesBox.Text = "Fully-Specific";
            TRPrecursorMzToleranceUnitsList.Text = "daltons";
            TRFragmentMzToleranceUnitsList.Text = "daltons";
            TRNTerminusMzToleranceUnitsList.Text = "daltons";
            TRCTerminusMzToleranceUnitsList.Text = "daltons";
            TRDeisotopingModeBox.Text = "Off";
            TRModTypeList.Text = "Static";
        }

        /// <summary>
        /// Takes a container and iterates through controls. 
        /// When finished all associations and events are
        /// taken care of and control dictionary has controls recorded.
        /// </summary>
        /// <param name="container"></param>
        private void InitializePane(Control container)
        {
            var unclamedControlList = new List<Control>();
            var unclamedLabelList = new List<Control>();
            string prefix;
            string program;

            if (container.Name.StartsWith("Myri"))
            {
                prefix = "Myri";
                program = "MyriMatch";
            }
            else if (container.Name.StartsWith("DT"))
            {
                prefix = "DT";
                program = "DirecTag";
            }
            else
            {
                prefix = "TR";
                program = "TagRecon";
            }

            foreach (Control item in container.Controls)
            {
                //If control is container call functiontion recursively
                if (item is GroupBox || item is Panel)
                    InitializePane(item);
                else if (item.Name.EndsWith("Box"))
                {
                    var root = RootName(item.Name);
                    var label = unclamedLabelList.Where
                        (x => x.Name == prefix + root + "Label")
                        .SingleOrDefault();

                    if (label == null)
                        unclamedControlList.Add(item);
                    else
                    {
                        unclamedLabelList.Remove(label);
                        _labelAssociation.Add(item, label);
                        if (item is ComboBox)
                            ((ComboBox) item).SelectedValueChanged += CheckForChange;
                        else if (item is TextBox)
                            item.TextChanged += CheckForChange;
                        else if (item is NumericUpDown)
                            ((NumericUpDown) item).ValueChanged += CheckForChange;
                        else if (item is CheckBox)
                            ((CheckBox) item).CheckedChanged += CheckForChange;
                    }

                    _itemList[program].Add(item);
                    _defaults.Add(item, GetControlValue(item).Trim('"'));
                }
                else if (item.Name.EndsWith("Label"))
                {
                    var root = RootName(item.Name);
                    var box = unclamedControlList.Where
                        (x => x.Name == prefix + root + "Box")
                        .SingleOrDefault();
                    if (box == null)
                        unclamedLabelList.Add(item);
                    else
                    {
                        unclamedControlList.Remove(box);
                        _labelAssociation.Add(box, item);
                        if (box is ComboBox)
                            ((ComboBox) box).SelectedValueChanged += CheckForChange;
                        else if (box is TextBox)
                            box.TextChanged += CheckForChange;
                        else if (box is NumericUpDown)
                            ((NumericUpDown) box).ValueChanged += CheckForChange;
                        else if (box is CheckBox)
                            ((CheckBox) box).CheckedChanged += CheckForChange;
                    }
                }
                else if (item.Name.EndsWith("Info"))
                {
                    item.Click += OpenHelpFile;
                    item.MouseEnter += Info_MouseEnter;
                    item.MouseLeave += Info_MouseLeave;
                }
            }
        }

        /// <summary>
        /// Checks value of Control against defaults and recolors related label accordingly
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void CheckForChange(object sender, EventArgs e)
        {
            var item = (Control)sender;
            var value = GetControlValue(item).Trim('"');
            var isModBox = false;
            var specialCaseItem = item;
            var specialCaseValue = value;

            if (RootName(item.Name) == "AppliedMod" && value.Length > 0)
            {
                isModBox = true;
                value += "\"";
            }

            

            //Handle special case
            switch (item.Name)
            {
                case "MyriPrecursorMzToleranceUnitsBox":
                    item = MyriPrecursorMzToleranceBox;
                    value = GetControlValue(item).Trim('"');
                    break;
                case "MyriFragmentMzToleranceUnitsBox":
                    item = MyriFragmentMzToleranceBox;
                    value = GetControlValue(item).Trim('"');
                    break;
                case "MyriPrecursorMzToleranceBox":
                    specialCaseItem = MyriPrecursorMzToleranceUnitsBox;
                    specialCaseValue = GetControlValue(specialCaseItem).Trim('"');
                    break;
                case "MyriFragmentMzToleranceBox":
                    specialCaseItem = MyriFragmentMzToleranceUnitsBox;
                    specialCaseValue = GetControlValue(specialCaseItem).Trim('"');
                    break;
                default:
                    specialCaseItem = null;
                    break;
            }

            if (value == _defaults[item] || (isModBox && ModStringsEqual(value, _defaults[item])))
            {
                if (!_templateDefaults.ContainsKey(item)
                    || value == _templateDefaults[item]
                    || (isModBox && ModStringsEqual(value, _templateDefaults[item])))
                    _labelAssociation[item].ForeColor = DefaultForeColor;
                else
                    _labelAssociation[item].ForeColor = Color.Blue;
            }
            else
            {
                if (!_templateDefaults.ContainsKey(item)
                    || value == _templateDefaults[item]
                    || (isModBox && ModStringsEqual(value, _templateDefaults[item])))
                    _labelAssociation[item].ForeColor = Color.Green;
                else
                    _labelAssociation[item].ForeColor = Color.DarkViolet;
            }

            //Handle dual dependence
            if (specialCaseItem != null)
            {
                if ((_labelAssociation[item].ForeColor == Color.Blue
                    || _labelAssociation[item].ForeColor == Color.Green)
                    && (_templateDefaults.ContainsKey(specialCaseItem)
                            && specialCaseValue != _templateDefaults[specialCaseItem]))
                    _labelAssociation[item].ForeColor = Color.DarkViolet;
                else if (_labelAssociation[item].ForeColor == DefaultForeColor)
                {
                    if (specialCaseValue == _defaults[specialCaseItem])
                    {
                        if (!_templateDefaults.ContainsKey(specialCaseItem)
                            || specialCaseValue == _templateDefaults[specialCaseItem])
                            _labelAssociation[item].ForeColor = DefaultForeColor;
                        else
                            _labelAssociation[item].ForeColor = Color.Blue;
                    }
                    else
                    {
                        if (!_templateDefaults.ContainsKey(specialCaseItem)
                            || specialCaseValue == _templateDefaults[specialCaseItem])
                            _labelAssociation[item].ForeColor = Color.Green;
                        else
                            _labelAssociation[item].ForeColor = Color.DarkViolet;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to open help file at location specified by control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OpenHelpFile(object sender, EventArgs e)
        {
            var root = RootName(((Control)sender).Name);
            switch (ProgramModeBox.Text)
            {
                case "MyriMatch":
                    try
                    {
                        var runLine = String.Format("\"file:///{0}/lib/MyriMatch.html#{1}\"", Application.StartupPath.Replace("\\", "/"), root);
                        System.Diagnostics.Process.Start(runLine);
                    }
                    catch
                    {
                        MessageBox.Show("Help Page not found");
                    }
                    break;
                case "DirecTag":
                    try
                    {
                        var runLine = String.Format("\"file:///{0}/lib/DirecTag.html#{1}\"", Application.StartupPath.Replace("\\", "/"), root);
                        System.Diagnostics.Process.Start(runLine);
                    }
                    catch
                    {
                        MessageBox.Show("Help Page not found");
                    }
                    break;
                case "TagRecon":
                    try
                    {
                        var runLine = String.Format("\"file:///{0}/lib/TagRecon.html#{1}\"", Application.StartupPath.Replace("\\", "/"), root);
                        System.Diagnostics.Process.Start(runLine);
                    }
                    catch
                    {
                        MessageBox.Show("Help Page not found");
                    }
                    break;
            }
        }

        /// <summary>
        /// Returns value of "Box" suffix control, taking into account special cases
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private string GetControlValue(Control item)
        {
            var root = RootName(item.Name);

            if (root == "AppliedMod")
                return GetModString((DataGridView)item);
            if (root == "UseAvgMassOfSequences")
                return (((ComboBox)item).SelectedIndex == 1).ToString().ToLower();
            if (root ==  "NumMinTerminiCleavages")
                return ((ComboBox)item).SelectedIndex.ToString();
            if (root == "DeisotopingMode")
                return ((ComboBox)item).SelectedIndex.ToString();
            if (root == "UnimodXML" && TRUnimodXMLBox.Text == "Default")
                return Path.Combine(Application.StartupPath, @"lib\Bumbershoot\unimod.xml");
            if (root == "Blosum" && TRBlosumBox.Text == "Default")
                return Path.Combine(Application.StartupPath, @"lib\Bumbershoot\blosum62.fas");
            if (root == "DeisotopingMode")
                return ((ComboBox)item).SelectedIndex.ToString();
            if (item is ComboBox || item is TextBox)
            {
                double x;
                return double.TryParse(item.Text, out x)
                           ? item.Text
                           : string.Format("\"{0}\"", item.Text);
            }
            if (item is NumericUpDown)
                return ((NumericUpDown)item).Value.ToString();
            if (item is CheckBox)
                return ((CheckBox)item).Checked.ToString().ToLower();
            return "Error";
        }

        /// <summary>
        /// Takes a template config and populates the template defaults dictionary
        /// </summary>
        /// <param name="baseConfig"></param>
        private void SetTemplateDefaults(ConfigFile baseConfig)
        {
            _templateDefaults = new Dictionary<Control, string>();
            var configValues = new Dictionary<string, string>();

            foreach (var item in baseConfig.PropertyList)
                if (!configValues.ContainsKey(item.Name))
                    configValues.Add(item.Name, item.Value.Trim('"'));

            if (configValues.ContainsKey("AppliedMod") && configValues["AppliedMod"].Length > 0)
                configValues["AppliedMod"] += "\"";

            //Set values
            foreach (var item in _itemList[ProgramModeBox.Text])
            {
                var root = RootName(item.Name);
                if (configValues.ContainsKey(root))
                    _templateDefaults.Add(item, configValues[root]);
            }

            foreach (var item in _itemList[ProgramModeBox.Text])
                CheckForChange(item, null);
        }

        /// <summary>
        /// Takes the currently selected template and loads all defaults
        /// </summary>
        private void LoadTemplate()
        {
            foreach (var control in _itemList[ProgramModeBox.Text])
            {
                var root = RootName(control.Name);
                var value = _templateDefaults.ContainsKey(control)
                                ? _templateDefaults[control]
                                : _defaults[control];
                if (root == "AppliedMod")
                    SetModString(value);
                if (root == "UseAvgMassOfSequences")
                    ((ComboBox)control).SelectedIndex = (value == "true") ? 1 : 0;
                else if (root == "NumMinTerminiCleavages" || root == "DeisotopingMode")
                    ((ComboBox)control).SelectedIndex = int.Parse(value);
                else if (control is ComboBox || control is TextBox)
                    control.Text = value;
                else if (control is NumericUpDown)
                    ((NumericUpDown)control).Value = decimal.Parse(value);
                else if (control is CheckBox)
                    ((CheckBox)control).Checked = bool.Parse(value);

                CheckForChange(control, null);
            }

        }

        /// <summary>
        /// Sets values of items to values in config
        /// </summary>
        /// <param name="baseConfig"></param>
        private void LoadConfig(ConfigFile baseConfig)
        {
            var configValues = baseConfig.PropertyList
                .ToDictionary(property => property.Name,
                              property => property.Value.Trim('"'));

            foreach (var kvp in _defaults)
            {
                var root = RootName(kvp.Key.Name);
                var value = configValues.ContainsKey(root)
                                ? configValues[root]
                                : kvp.Value;
                if (root == "AppliedMod")
                {
                    var modString = string.Empty;
                    if (configValues.ContainsKey("StaticMods"))
                    {
                        modString += "StaticMods = \"" + configValues["StaticMods"] + "\"" + Environment.NewLine;
                    }
                    if (configValues.ContainsKey("DynamicMods"))
                    {
                        modString += "DynamicMods = \"" + configValues["DynamicMods"] + "\"" + Environment.NewLine;
                    }
                    if (configValues.ContainsKey("PreferredDeltaMasses"))
                    {
                        modString += "PreferredDeltaMasses = \"" + configValues["PreferredDeltaMasses"] + "\"";
                    }
                    if (!string.IsNullOrEmpty(modString))
                    {
                        SetModString(modString.Trim());
                    }
                }
                else if (root == "UseAvgMassOfSequences")
                    ((ComboBox)kvp.Key).SelectedIndex = (value == "true") ? 1 : 0;
                else if (root == "NumMinTerminiCleavages" || root == "DeisotopingMode")
                    ((ComboBox)kvp.Key).SelectedIndex = int.Parse(value);
                else if (kvp.Key is ComboBox || kvp.Key is TextBox)
                    kvp.Key.Text = value;
                else if (kvp.Key is NumericUpDown)
                    ((NumericUpDown)kvp.Key).Value = decimal.Parse(value);
                else if (kvp.Key is CheckBox)
                    ((CheckBox)kvp.Key).Checked = bool.Parse(value);
            }
        }

        /// <summary>
        /// Hides all panels in preperation for changing destination programs
        /// </summary>
        private void HideAllPanels()
        {
            MyriGenPanel.Visible = false;
            MyriAdvPanel.Visible = false;
            DTGenPanel.Visible = false;
            DTAdvPanel.Visible = false;
            TRGenPanel.Visible = false;
            TRAdvPanel.Visible = false;
        }

        /// <summary>
        /// Removes destination program prefix and control type suffix from name
        /// </summary>
        /// <param name="fullWord"></param>
        /// <returns></returns>
        private string RootName(string fullWord)
        {
            fullWord = fullWord.Remove(0, fullWord.StartsWith("Myri") ? 4 : 2);

            if (fullWord.EndsWith("Info") || fullWord.EndsWith("Auto"))
                fullWord = fullWord.Remove(fullWord.Length - 4, 4);
            else if (fullWord.EndsWith("Label"))
                fullWord = fullWord.Remove(fullWord.Length - 5, 5);
            else if (fullWord.EndsWith("Box"))
                fullWord = fullWord.Remove(fullWord.Length - 3, 3);

            return fullWord;
        }

        private string GetModString(DataGridView dgv)
        {
            var staticList = new List<string>();
            var dynamicList = new List<string>();
            var ptmList = new List<string>();

            foreach (DataGridViewRow row in dgv.Rows)
            {
                if ((string) row.Cells[2].Value == "Static")
                    staticList.Add(string.Format("{0} {1}", row.Cells[0].Value, row.Cells[1].Value));
                else if ((string)row.Cells[2].Value == "Dynamic")
                    dynamicList.Add(string.Format("{0} * {1}", row.Cells[0].Value, row.Cells[1].Value));
                else
                    ptmList.Add(string.Format("{0} {1}", row.Cells[0].Value, row.Cells[1].Value));
            }

            return ((staticList.Count > 0
                        ? "StaticMods = \"" + string.Join(" ", staticList.ToArray()) + "\"" + Environment.NewLine
                        : string.Empty) +
                   (dynamicList.Count > 0
                        ? "DynamicMods = \"" + string.Join(" ", dynamicList.ToArray()) + "\"" + Environment.NewLine
                        : string.Empty) +
                   (ptmList.Count > 0
                        ? "PreferredDeltaMasses = \"" + string.Join(" ", ptmList.ToArray()) + "\"" + Environment.NewLine
                        : string.Empty)).Trim();
        }

        private void SetModString(string newModString)
        {
            DataGridView dgv;
            if (ProgramModeBox.Text == "MyriMatch")
                dgv = MyriAppliedModBox;
            else if (ProgramModeBox.Text == "DirecTag")
                dgv = DTAppliedModBox;
            else
                dgv = TRAppliedModBox;

            var modList = ModStringToModList(newModString);

            dgv.Rows.Clear();
            foreach (var item in modList)
                if (item.Split().Count() == 3)
                {
                    var values = new object[3];
                    values[0] = item.Split()[0];
                    values[1] = item.Split()[1];
                    values[2] = item.Split()[2];
                    dgv.Rows.Add(values);
                }
        }

        private bool ModStringsEqual(string mod1, string mod2)
        {
            var modList1 = ModStringToModList(mod1);
            var modList2 = ModStringToModList(mod2);
            modList1.Sort();
            modList2.Sort();
            if (modList1.Count != modList2.Count)
                return false;
            for (var x = 0; x < modList1.Count; x++)
                if (modList1[x] != modList2[x])
                    return false;
            return true;
        }

        private List<string> ModStringToModList(string newModList)
        {
            var modList = new List<string>();
            var propertyLines = newModList.Split(Environment.NewLine.ToCharArray());

            foreach (var line in propertyLines)
            {
                var values = Regex.Match(line, "\".+\"").Value.Trim('"').Split();
                switch (line.Split()[0])
                {
                    case "StaticMods":
                        for (var x = 0; x < values.Length - 1; x += 2)
                            modList.Add(values[x] + " " + values[x + 1] + " " + "Static");
                        break;
                    case "DynamicMods":
                        for (var x = 0; x < values.Length - 2; x += 3)
                            modList.Add(values[x] + " " + values[x + 2] + " " + "Dynamic");
                        break;
                    case "PreferredDeltaMasses":
                        for (var x = 0; x < values.Length - 1; x += 2)
                            modList.Add(values[x] + " " + values[x + 1] + " " + "PreferredPTM");
                        break;
                }
            }

            return modList;
        }

        internal string GetFilePath()
        {
            return _filePath;
        }

        internal string GetConfigString(bool redundant)
        {

            #region Group and order list

            var groupOrder = new List<string>
                                 {
                                     "PrecursorMzTolerance",
                                     "FragmentMzTolerance",
                                     "PrecursorMzToleranceUnits",
                                     "FragmentMzToleranceUnits",
                                     "NTerminusMassTolerance",
                                     "CTerminusMassTolerance",
                                     "NTerminusMzTolerance",
                                     "CTerminusMzTolerance",
                                     Environment.NewLine,
                                     "AdjustPrecursorMass",
                                     "MaxPrecursorAdjustment",
                                     "MinPrecursorAdjustment",
                                     "PrecursorAdjustmentStep",
                                     "NumSearchBestAdjustments",
                                     Environment.NewLine,
                                     "DuplicateSpectra",
                                     "UseChargeStateFromMS",
                                     "NumChargeStates",
                                     "TicCutoffPercentage",
                                     "UseSmartPlusThreeModel",
                                     Environment.NewLine,
                                     "CleavageRules",
                                     "NumMinTerminiCleavages",
                                     "NumMaxMissedCleavages",
                                     "UseAvgMassOfSequences",
                                     "MinCandidateLength",
                                     Environment.NewLine,
                                     "AppliedMod",
                                     "MaxDynamicMods",
                                     "MaxNumPreferredDeltaMasses",
                                     Environment.NewLine,
                                     "ExplainUnknownMassShiftsAs",
                                     "MaxModificationMassPlus",
                                     "MaxModificationMassMinus",
                                     "UnimodXML",
                                     "Blosum",
                                     "BlosumThreshold",
                                     Environment.NewLine,
                                     "MaxResults",
                                     Environment.NewLine,
                                     "ProteinSampleSize",
                                     Environment.NewLine,
                                     "NumIntensityClasses",
                                     "ClassSizeMultiplier",
                                     Environment.NewLine,
                                     "DeisotopingMode",
                                     "IsotopeMzTolerance",
                                     "ComplementMzTolerance",
                                     Environment.NewLine,
                                     "MinSequenceMass",
                                     "MaxSequenceMass",
                                     Environment.NewLine,
                                     "StartSpectraScanNum",
                                     "EndSpectraScanNum",
                                     "StartProteinIndex",
                                     "EndProteinIndex",
                                     Environment.NewLine,
                                     "ComputeXCorr",
                                     "UseNETAdjustment",
                                     "MassReconMode",
                                     Environment.NewLine,
                                     "MaxPeakCount",
                                     "TagLength",
                                     "IntensityScoreWeight",
                                     "MzFidelityScoreWeight",
                                     "ComplementScoreWeight",
                                     "MaxTagCount",
                                     "MaxTagScore",
                                     "DecoyPrefix",
                                     Environment.NewLine
                                 };

            #endregion

            #region Redundant items
            var redundantItems = new List<string>
                                     {
                                         "PrecursorMzTolerance",
                                         "FragmentMzTolerance",
                                         "PrecursorMzToleranceUnits",
                                         "FragmentMzToleranceUnits",
                                         "UseChargeStateFromMS",
                                         "NumChargeStates",
                                         "TicCutoffPercentage",
                                         "CleavageRules",
                                         "NumMinTerminiCleavages",
                                         "NumMaxMissedCleavages",
                                         "MaxResults",
                                         "NTerminusMassTolerance",
                                         "CTerminusMassTolerance",
                                         "NTerminusMzTolerance",
                                         "CTerminusMzTolerance",
                                         "UnimodXML",
                                         "Blosum"
                                     };
            #endregion

            var changed = new Dictionary<string, string>();
            foreach (var item in _itemList[ProgramModeBox.Text])
            {
                var value = GetControlValue(item);
                var compareValue = value.Trim('"');
                var root = RootName(item.Name);
                var isModBox = root == "AppliedMod";

                if (isModBox && compareValue.Length >0)
                    compareValue += "\"";

                if ((compareValue != _defaults[item] || (isModBox && !ModStringsEqual(compareValue, _defaults[item])))
                    || (redundant && redundantItems.Contains(root)))
                    changed.Add(root, value);
            }

            var all = string.Empty;
            var group = string.Empty;
            foreach (var item in groupOrder)
            {
                if (item == Environment.NewLine && !string.IsNullOrEmpty(group))
                {
                    all += group + Environment.NewLine;
                    group = string.Empty;
                }
                else if (changed.ContainsKey(item))
                {
                    if (item == "AppliedMod")
                        group += changed["AppliedMod"] + Environment.NewLine;
                    else
                        group += item + " = " + changed[item] + Environment.NewLine;
                }
            }

            return all.Trim();
        }

        private void SaveToFile(string filepath)
        {
            StreamWriter cout;

            try
            {
                cout = new StreamWriter(filepath);
                Text = filepath.Substring(filepath.LastIndexOf('\\') + 1);
            }
            catch
            {
                MessageBox.Show("Cannot write to file, make sure it is not open");
                _cancelDialogResult = true;
                return;
            }
            _cancelDialogResult = false;

            cout.Write(GetConfigString(true));
            cout.Close();
            cout.Dispose();
        }

        #region Events

        /// <summary>
        /// Changes appearance of form based on destination program.
        /// As a bonus stores the current destination program for use
        /// in other functions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProgramModeBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            HideAllPanels();
            switch (ProgramModeBox.Text)
            {
                case "MyriMatch":
                    Size = new Size(540, 590);
                    MyriGenPanel.Visible = true;
                    MyriAdvPanel.Visible = true;
                    break;
                case "DirecTag":
                    Size = new Size(555, 540);
                    DTGenPanel.Visible = true;
                    DTAdvPanel.Visible = true;
                    break;
                case "TagRecon":
                    Size = new Size(540, 650);
                    TRGenPanel.Visible = true;
                    TRAdvPanel.Visible = true;
                    break;
            }
        }

        /// <summary>
        /// If box is blank set it to default
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TRUnimodXMLBox_Leave(object sender, EventArgs e)
        {
            if (!File.Exists(TRUnimodXMLBox.Text) || string.IsNullOrEmpty(TRUnimodXMLBox.Text))
                TRUnimodXMLBox.Text = "Default";
        }

        /// <summary>
        /// If box is blank set it to default
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TRBlosumBox_Leave(object sender, EventArgs e)
        {
            if (!File.Exists(TRBlosumBox.Text) || string.IsNullOrEmpty(TRBlosumBox.Text))
                TRBlosumBox.Text = "Default";
        }

        /// <summary>
        /// Allow user to search for unimopd XML file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TRUnimodXMLBrowse_Click(object sender, EventArgs e)
        {
            var openFile = new OpenFileDialog
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = "XML Files (.xml)|*.xml",
                RestoreDirectory = true,
                Title = "Open XML file"
            };

            if (openFile.ShowDialog().Equals(DialogResult.OK))
                TRUnimodXMLBox.Text = openFile.FileName;
        }

        /// <summary>
        /// Search for blosum file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TRBlosumBrowse_Click(object sender, EventArgs e)
        {
            var openFile = new OpenFileDialog
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = "FAS Files (.fas)|*.fas",
                RestoreDirectory = true,
                Title = "Open FAS file"
            };

            if (openFile.ShowDialog().Equals(DialogResult.OK))
                TRBlosumBox.Text = openFile.FileName;
        }

        /// <summary>
        /// Displays label when values is -1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NumMaxMissedCleavagesBox_ValueChanged(object sender, EventArgs e)
        {
            if (ProgramModeBox.Text == "MyriMatch")
                MyriNumMaxMissedCleavagesAuto.Visible = MyriNumMaxMissedCleavagesBox.Value == -1;
            else
                TRNumMaxMissedCleavagesAuto.Visible = TRNumMaxMissedCleavagesBox.Value == -1;
        }

        /// <summary>
        /// Displays label when values is -1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EndSpectraScanNumBox_ValueChanged(object sender, EventArgs e)
        {
            if (ProgramModeBox.Text == "MyriMatch")
                MyriEndSpectraScanNumAuto.Visible = MyriEndSpectraScanNumBox.Value == -1;
            else if (ProgramModeBox.Text == "DirecTag")
                DTEndSpectraScanNumAuto.Visible = DTEndSpectraScanNumBox.Value == -1;
            else
                TREndSpectraScanNumAuto.Visible = TREndSpectraScanNumBox.Value == -1;
        }

        /// <summary>
        /// Displays label when values is -1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EndProteinIndexBox_ValueChanged(object sender, EventArgs e)
        {
            if (ProgramModeBox.Text == "MyriMatch")
                MyriEndProteinIndexAuto.Visible = MyriEndProteinIndexBox.Value == -1;
            else
                TREndProteinIndexAuto.Visible = TREndProteinIndexBox.Value == -1;
        }

        private void AppliedModAdd_Click(object sender, EventArgs e)
        {
            var values = new object[3];
            
            #region Box Definition
            TextBox ResidueBox;
            TextBox ModMassBox;
            ComboBox ModTypeBox;
            DataGridView AppliedModDGV;

            if (ProgramModeBox.Text == "MyriMatch")
            {
                ResidueBox = MyriResidueText;
                ModMassBox = MyriModMassText;
                ModTypeBox = MyriModTypeList;
                AppliedModDGV = MyriAppliedModBox;
            }
            else if (ProgramModeBox.Text == "DirecTag")
            {
                ResidueBox = DTResidueText;
                ModMassBox = DTModMassText;
                ModTypeBox = DTModTypeList;
                AppliedModDGV = DTAppliedModBox;
            }
            else
            {
                ResidueBox = TRResidueText;
                ModMassBox = TRModMassText;
                ModTypeBox = TRModTypeList;
                AppliedModDGV = TRAppliedModBox;
            }
            #endregion

            if (IsValidModification(ResidueBox.Text, ModMassBox.Text, ModTypeBox.Text, false) == "All Valid")
            {
                values[0] = ResidueBox.Text;
                values[1] = double.Parse(ModMassBox.Text).ToString();
                values[2] = ModTypeBox.Text;
                if (ModTypeBox.Text == "PreferredPTM")
                {
                    _skipAutomation = true;
                    TRExplainUnknownMassShiftsAsBox.Text = "PreferredPTMs";
                    _skipAutomation = false;
                }
                ResidueBox.Clear();
                ModMassBox.Clear();
                ModTypeBox.SelectedIndex = 0;
                AppliedModDGV.Rows.Add(values);
                AppliedModDGV.ClearSelection();
                CheckForChange(AppliedModDGV, null);
            }

        }

        private void AppliedModRemove_Click(object sender, EventArgs e)
        {
            #region Box Definition
            TextBox ResidueBox;
            TextBox ModMassBox;
            ComboBox ModTypeBox;
            DataGridView AppliedModDGV;

            if (ProgramModeBox.Text == "MyriMatch")
            {
                ResidueBox = MyriResidueText;
                ModMassBox = MyriModMassText;
                ModTypeBox = MyriModTypeList;
                AppliedModDGV = MyriAppliedModBox;
            }
            else if (ProgramModeBox.Text == "DirecTag")
            {
                ResidueBox = DTResidueText;
                ModMassBox = DTModMassText;
                ModTypeBox = DTModTypeList;
                AppliedModDGV = DTAppliedModBox;
            }
            else
            {
                ResidueBox = TRResidueText;
                ModMassBox = TRModMassText;
                ModTypeBox = TRModTypeList;
                AppliedModDGV = TRAppliedModBox;
            }
            #endregion

            if (AppliedModDGV.SelectedRows.Count > 0)
            {
                var keepExplanation = false;
                var selection = AppliedModDGV.SelectedRows[0].Index;

                ResidueBox.Text = AppliedModDGV.Rows[selection].Cells[0].Value.ToString();
                ModMassBox.Text = AppliedModDGV.Rows[selection].Cells[1].Value.ToString();
                ModTypeBox.Text = AppliedModDGV.Rows[selection].Cells[2].Value.ToString();

                AppliedModDGV.Rows.RemoveAt(selection);

                if (ModTypeBox.Text == "PreferredPTM")
                {
                    for (var x = 0; x < AppliedModDGV.Rows.Count; x++)
                    {
                        if (AppliedModDGV.Rows[x].Cells[2].Value.ToString() == "PreferredPTM")
                        {
                            keepExplanation = true;
                            break;
                        }
                    }

                    if (!keepExplanation)
                    {
                        _skipAutomation = true;
                        TRExplainUnknownMassShiftsAsBox.Text = string.Empty;
                        _skipAutomation = false;
                    }
                }
                AppliedModDGV.ClearSelection();
                CheckForChange(AppliedModDGV, null);
            }
        }

        private void AdvModeBox_CheckedChanged(object sender, EventArgs e)
        {

            if (((CheckBox)sender).Checked)
            {
                mainTabControl.TabPages.Add(AdvTab);
                MyriPrecursorMzToleranceBox.Enabled = true;
                MyriPrecursorMzToleranceUnitsBox.Enabled = true;
                MyriFragmentMzToleranceBox.Enabled = true;
                MyriFragmentMzToleranceUnitsBox.Enabled = true;
                MyriNumMaxMissedCleavagesBox.Enabled = true;
                MyriNumMaxMissedCleavagesAuto.Enabled = true;
                DTPrecursorMzToleranceBox.Enabled = true;
                DTPrecursorMzToleranceUnitsList.Enabled = true;
                DTFragmentMzToleranceBox.Enabled = true;
                DTFragmentMzToleranceUnitsList.Enabled = true;
                DTNTerminusMassToleranceBox.Enabled = true;
                DTNTerminusMassToleranceUnitsList.Enabled = true;
                DTCTerminusMassToleranceBox.Enabled = true;
                DTCTerminusMassToleranceUnitsList.Enabled = true;
                TRPrecursorMzToleranceBox.Enabled = true;
                TRPrecursorMzToleranceUnitsList.Enabled = true;
                TRFragmentMzToleranceBox.Enabled = true;
                TRFragmentMzToleranceUnitsList.Enabled = true;
                TRNTerminusMzToleranceBox.Enabled = true;
                TRNTerminusMzToleranceUnitsList.Enabled = true;
                TRCTerminusMzToleranceBox.Enabled = true;
                TRCTerminusMzToleranceUnitsList.Enabled = true;
                TRNumMaxMissedCleavagesBox.Enabled = true;
                TRNumMaxMissedCleavagesAuto.Enabled = true;
            }
            else
            {
                mainTabControl.TabPages.Remove(AdvTab);
                MyriPrecursorMzToleranceBox.Enabled = false;
                MyriPrecursorMzToleranceUnitsBox.Enabled = false;
                MyriFragmentMzToleranceBox.Enabled = false;
                MyriFragmentMzToleranceUnitsBox.Enabled = false;
                MyriNumMaxMissedCleavagesBox.Enabled = false;
                MyriNumMaxMissedCleavagesAuto.Enabled = false;
                DTPrecursorMzToleranceBox.Enabled = false;
                DTPrecursorMzToleranceUnitsList.Enabled = false;
                DTFragmentMzToleranceBox.Enabled = false;
                DTFragmentMzToleranceUnitsList.Enabled = false;
                DTNTerminusMassToleranceBox.Enabled = false;
                DTNTerminusMassToleranceUnitsList.Enabled = false;
                DTCTerminusMassToleranceBox.Enabled = false;
                DTCTerminusMassToleranceUnitsList.Enabled = false;
                TRPrecursorMzToleranceBox.Enabled = false;
                TRPrecursorMzToleranceUnitsList.Enabled = false;
                TRFragmentMzToleranceBox.Enabled = false;
                TRFragmentMzToleranceUnitsList.Enabled = false;
                TRNTerminusMzToleranceBox.Enabled = false;
                TRNTerminusMzToleranceUnitsList.Enabled = false;
                TRCTerminusMzToleranceBox.Enabled = false;
                TRCTerminusMzToleranceUnitsList.Enabled = false;
                TRNumMaxMissedCleavagesBox.Enabled = false;
                TRNumMaxMissedCleavagesAuto.Enabled = false;
            }
        }

        private bool _cancelDialogResult;
        private void ConfigForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_cancelDialogResult)
                e.Cancel = true;
            _cancelDialogResult = false;
        }

        private void SaveAsTemporaryButton_Click(object sender, EventArgs e)
        {
            var text = new TextPromptBox("Custom Configuration", _defaultName);
            if (text.ShowDialog() == DialogResult.OK)
                _filePath = "--Custom--" + text.GetText();
        }

        private void SaveAsNewButton_Click(object sender, EventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                RestoreDirectory = true
            };
            sfd.InitialDirectory = _baseDirectory;
            sfd.CheckPathExists = true;
            sfd.DefaultExt = ".cfg";
            sfd.Filter = "Config File(.cfg)|*.cfg";
            sfd.AddExtension = true;
            sfd.FileName = String.Format("{0}.cfg", ProgramModeBox.Text);


            try
            {
                if (sfd.ShowDialog().Equals(DialogResult.OK))
                {
                    var newCfgFilePath = sfd.FileName;
                    SaveToFile(newCfgFilePath);
                    _filePath = newCfgFilePath;
                }
                else
                {
                    _cancelDialogResult = true;
                }
            }
            catch
            {
                _cancelDialogResult = true;
            }
        }

        private void SaveOverOldButton_Click(object sender, EventArgs e)
        {
            SaveToFile(_filePath);
        }

        private void Info_MouseEnter(object sender, EventArgs e)
        {
            ((Label)sender).Font = new Font("Microsoft Sans Serif", 8, FontStyle.Bold);
        }

        private void Info_MouseLeave(object sender, EventArgs e)
        {
            ((Label)sender).Font = new Font("Microsoft Sans Serif", 7, FontStyle.Regular);
        }

        private void ModList_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListView ModList;
            TextBox ResidueBox;
            TextBox ModMassBox;
            ComboBox ModTypeBox;

            switch (ProgramModeBox.Text)
            {
                case "MyriMatch":
                    ModList = MyriModList;
                    ResidueBox = MyriResidueText;
                    ModMassBox = MyriModMassText;
                    ModTypeBox = MyriModTypeList;
                    break;
                case "DirecTag":
                    ModList = DTModList;
                    ResidueBox = DTResidueText;
                    ModMassBox = DTModMassText;
                    ModTypeBox = DTModTypeList;
                    break;
                default:
                    ModList = TRModList;
                    ResidueBox = TRResidueText;
                    ModMassBox = TRModMassText;
                    ModTypeBox = TRModTypeList;
                    break;
            }

            try
            {
                if (ModList.SelectedItems.Count > 0)
                {
                    ResidueBox.Text = ModList.SelectedItems[0].SubItems[1].Text;
                    ModMassBox.Text = ModList.SelectedItems[0].SubItems[2].Text;
                    ModTypeBox.SelectedIndex = (ResidueBox.Text == "C") ? 0 : 1;
                }
            }
            catch
            {
                MessageBox.Show("Error in reading selected item");
            }
        }

        private void ModMassText_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                AppliedModAdd_Click(0, e);
            }
            NumericTextBox_KeyPress(sender, e);
        }

        private void InstrumentButton_Click(object sender, EventArgs e)
        {
            LoadTemplate();
        }

        private void InstrumentList_SelectedIndexChanged(object sender, EventArgs e)
        {
            IList<ConfigFile> currentlist;
            if (ProgramModeBox.Text == "MyriMatch")
                currentlist = _myriTemplateList;
            else if (ProgramModeBox.Text == "DirecTag")
                currentlist = _DTTemplateList;
            else
                currentlist = _TRTemplateList;

            var index = ((ComboBox) sender).SelectedIndex - 1;

            SetTemplateDefaults(index >= 0
                                    ? currentlist[index]
                                    : new ConfigFile {PropertyList = new List<ConfigProperty>()});
        }

        private void SaveTemplateButton_Click(object sender, EventArgs e)
        {
            var parameterType = lib.Util.parameterTypes;

            var newTemplate = false;
            try
            {
                ConfigFile currentConfig;
                if (ProgramModeBox.Text == "MyriMatch")
                {
                    if (MyriInstrumentList.SelectedIndex > 0)
                        currentConfig = _myriTemplateList[MyriInstrumentList.SelectedIndex - 1];
                    else
                    {
                        currentConfig = new ConfigFile
                                            {
                                                DestinationProgram = "MyriMatch",
                                                FilePath = "Template",
                                                PropertyList = new List<ConfigProperty>()
                                            };
                        newTemplate = true;
                    }
                }
                else if (ProgramModeBox.Text == "DirecTag")
                {
                    if (DTInstrumentList.SelectedIndex > 0)
                        currentConfig = _DTTemplateList[DTInstrumentList.SelectedIndex-1];
                    else
                    {
                        currentConfig = new ConfigFile
                        {
                            DestinationProgram = "DirecTag",
                            FilePath = "Template",
                            PropertyList = new List<ConfigProperty>()
                        };
                        newTemplate = true;
                    }
                }
                else
                {
                    if (TRInstrumentList.SelectedIndex > 0)
                        currentConfig = _TRTemplateList[TRInstrumentList.SelectedIndex-1];
                    else
                    {
                        currentConfig = new ConfigFile
                        {
                            DestinationProgram = "TagRecon",
                            FilePath = "Template",
                            PropertyList = new List<ConfigProperty>()
                        };
                        newTemplate = true;
                    }
                }

                if (newTemplate)
                {
                    var namebox = new TextPromptBox("Instrument Name", string.Empty);
                    if (namebox.ShowDialog() == DialogResult.OK)
                        currentConfig.Name = namebox.GetText();
                    else
                        return;
                }

                currentConfig.PropertyList.Clear();
                foreach (var kvp in _labelAssociation)
                {
                    if (kvp.Value.ForeColor == DefaultForeColor 
                        || kvp.Value.ForeColor == Color.Blue)
                        continue;

                    currentConfig.PropertyList.Add(new ConfigProperty
                                                       {
                                                           Name = RootName(kvp.Key.Name),
                                                           Value = GetControlValue(kvp.Key),
                                                           Type = parameterType.ContainsKey(RootName(kvp.Key.Name))
                                                                      ? parameterType[RootName(kvp.Key.Name)]
                                                                      : "unknown",
                                                           ConfigAssociation = currentConfig
                                                       });

                    //special cases
                    if (kvp.Key.Name == "MyriPrecursorMzToleranceBox")
                        currentConfig.PropertyList.Add(new ConfigProperty
                        {
                            Name = "PrecursorMzToleranceUnits",
                            Value = GetControlValue(MyriPrecursorMzToleranceUnitsBox),
                            Type = parameterType.ContainsKey("PrecursorMzToleranceUnits")
                                       ? parameterType["PrecursorMzToleranceUnits"]
                                       : "unknown",
                            ConfigAssociation = currentConfig
                        });
                    else if (kvp.Key.Name == "MyriFragmentMzToleranceBox")
                        currentConfig.PropertyList.Add(new ConfigProperty
                        {
                            Name = "FragmentMzToleranceUnits",
                            Value = GetControlValue(MyriFragmentMzToleranceUnitsBox),
                            Type = parameterType.ContainsKey("FragmentMzToleranceUnits")
                                       ? parameterType["FragmentMzToleranceUnits"]
                                       : "unknown",
                            ConfigAssociation = currentConfig
                        });
                }

                _session.SaveOrUpdate(currentConfig);
                _session.Flush();

                ResetTemplateLists();

                MessageBox.Show("Save successful");
            }
            catch
            {
                MessageBox.Show("Unable to save");
            }
        }

        private void MoreButton_Click(object sender, EventArgs e)
        {
            MoreContextMenu.Show(MoreButton,new Point(0,0),ToolStripDropDownDirection.AboveRight);
        }

        private void renameInstrumentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                ConfigFile currentConfig = null;
                if (ProgramModeBox.Text == "MyriMatch")
                {
                    if (MyriInstrumentList.SelectedIndex > 0)
                        currentConfig = _myriTemplateList[MyriInstrumentList.SelectedIndex - 1];
                    else
                        MessageBox.Show("Cannot rename 'New'");
                }
                else if (ProgramModeBox.Text == "DirecTag")
                {
                    if (DTInstrumentList.SelectedIndex > 0)
                        currentConfig = _DTTemplateList[DTInstrumentList.SelectedIndex - 1];
                    else
                        MessageBox.Show("Cannot rename 'New'");
                }
                else
                {
                    if (TRInstrumentList.SelectedIndex > 0)
                        currentConfig = _TRTemplateList[TRInstrumentList.SelectedIndex - 1];
                    else
                        MessageBox.Show("Cannot rename 'New'");
                }

                var namebox = new TextPromptBox("Instrument Name", string.Empty);
                if (currentConfig != null && namebox.ShowDialog() == DialogResult.OK)
                {
                    currentConfig.Name = namebox.GetText();
                    _session.SaveOrUpdate(currentConfig);
                    _session.Flush();
                    ResetTemplateLists();

                    MessageBox.Show("Rename successful");
                }
            }
            catch
            {
                MessageBox.Show("Could not rename instrument");
            }
        }

        private void deleteInstrumentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                ConfigFile currentConfig = null;
                if (ProgramModeBox.Text == "MyriMatch")
                {
                    if (MyriInstrumentList.SelectedIndex > 0)
                        currentConfig = _myriTemplateList[MyriInstrumentList.SelectedIndex - 1];
                    else
                        MessageBox.Show("Cannot delete 'New'");
                }
                else if (ProgramModeBox.Text == "DirecTag")
                {
                    if (DTInstrumentList.SelectedIndex > 0)
                        currentConfig = _DTTemplateList[DTInstrumentList.SelectedIndex - 1];
                    else
                        MessageBox.Show("Cannot delete 'New'");
                }
                else
                {
                    if (TRInstrumentList.SelectedIndex > 0)
                        currentConfig = _TRTemplateList[TRInstrumentList.SelectedIndex - 1];
                    else
                        MessageBox.Show("Cannot delete'New'");
                }

                if (currentConfig != null)
                {
                    _session.Delete(currentConfig);
                    _session.Flush();
                    ResetTemplateLists();

                    MessageBox.Show("Delete successful");
                }
            }
            catch
            {
                MessageBox.Show("Could not remove instrument");
            }
        }

        private void exportInstrumentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var folderDialog = new SaveFileDialog {DefaultExt = ".db"};
            string exportFile;
            if (folderDialog.ShowDialog() == DialogResult.OK)
                exportFile = folderDialog.FileName;
            else
                return;

            if (File.Exists(exportFile))
                File.Delete(exportFile);
            var exportForm = new ImportTemplateForm(_session);
            if (!string.IsNullOrEmpty(exportFile) && exportForm.ShowDialog() == DialogResult.OK)
            {
                var configList = exportForm.GetConfigs();
                var manager = SessionManager.CreateSessionFactory(exportFile, true);
                var tempSession = manager.OpenSession();
                foreach (var config in configList)
                    tempSession.SaveOrUpdate(config);
                tempSession.Flush();
                tempSession.Close();
            }
        }

        private void importInstrumentTemplatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var folderDialog = new OpenFileDialog { Filter = "Database Files(.db)|*.db|All files|*.*" };
            string importFile;
            if (folderDialog.ShowDialog() == DialogResult.OK)
                importFile = folderDialog.FileName;
            else
                return;

            var exportForm = new ImportTemplateForm(importFile);
            if (!string.IsNullOrEmpty(importFile) && exportForm.ShowDialog() == DialogResult.OK)
            {
                var configList = exportForm.GetConfigs();
                foreach (var config in configList)
                    _session.SaveOrUpdate(config);
                ResetTemplateLists();
            }
        }

        private void FinishedTemplateButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void AppliedModBox_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            CheckForChange(sender, null);
        }

        private void AppliedModBox_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            CheckForChange(sender, null);
        }

        #endregion

        #region Validation

        private void NumUpDownBox_Leave(object sender, EventArgs e)
        {
            ((NumericUpDown)sender).Value = Math.Round((((NumericUpDown)sender).Value));
        }

        private string IsValidModification(string Residue, string Mass, string Type, bool Passive)
        {
            var statRx = new Regex("^[\\(\\)ACDEFGHIKLMNPQRSTUVWXY]$");
            var dynRx =
                new Regex(
                    @"^(?:\(|\)|(?:\(|{\(})?(?:(?:\[[ACDEFGHIKLMNPQRSTUVWXY]+\])|(?:{[ACDEFGHIKLMNPQRSTUVWXY]+})|(?:[ACDEFGHIKLMNPQRSTUVWXY]))+!?(?:(?:\[[ACDEFGHIKLMNPQRSTUVWXY]+\])|(?:{[ACDEFGHIKLMNPQRSTUVWXY]+})|(?:[ACDEFGHIKLMNPQRSTUVWXY]))*(?:\)|{\)})?)$");
            var mod = Residue;
            var skipOne = Passive;
            double x;
            DataGridView AppliedModDGV;
            if (ProgramModeBox.Text == "MyriMatch")
                AppliedModDGV = MyriAppliedModBox;
            else if (ProgramModeBox.Text == "DirecTag")
                AppliedModDGV = DTAppliedModBox;
            else
                AppliedModDGV = TRAppliedModBox;

            if (Type == "Static")
            {
                if (!statRx.IsMatch(Residue))
                {
                    if (!Passive)
                        MessageBox.Show("Invalid Residue Character");
                    return "Invalid Mod Character";
                }
            }

            if (Type != "Static" && !dynRx.IsMatch(Residue))
            {
                if (!Passive)
                    MessageBox.Show("Invalid Residue Motif");
                return "Invalid Mod Motif";
            }

            for (var item = 0; item < AppliedModDGV.Rows.Count; item++)
            {
                if ((string) AppliedModDGV.Rows[item].Cells[0].Value == mod)
                {
                    if (Type == "Static" && (string)AppliedModDGV.Rows[item].Cells[2].Value == "Static")
                    {
                        if (skipOne)
                            skipOne = false;
                        else
                        {
                            if (!Passive)
                                MessageBox.Show("Static 'Residue Motif' already present in list.");
                            return "Mod Already Present";
                        }
                    }
                    else if (Type != "Static" &&
                            (string)AppliedModDGV.Rows[item].Cells[1].Value == Mass &&
                            (string)AppliedModDGV.Rows[item].Cells[2].Value == Type)
                    {
                        if (skipOne)
                            skipOne = false;
                        else
                        {
                            if (!Passive)
                                MessageBox.Show("Modification already present in list.");
                            return "Mod Already Present";
                        }
                    }

                }
            }

            return double.TryParse(Mass, out x) ? "All Valid" : "Invalid Mod Mass";
        }

        private void StartSpectraScanNumBox_Leave(object sender, EventArgs e)
        {
            var StartSpectraScanNumBox = (NumericUpDown) sender;
            NumericUpDown EndSpectraScanNumBox;

            if (ProgramModeBox.Text == "MyriMatch")
                EndSpectraScanNumBox = MyriEndSpectraScanNumBox;
            else if (ProgramModeBox.Text == "DirecTag")
                EndSpectraScanNumBox = DTEndSpectraScanNumBox;
            else
                EndSpectraScanNumBox = TREndSpectraScanNumBox;

            if (EndSpectraScanNumBox.Value != -1 && StartSpectraScanNumBox.Value > EndSpectraScanNumBox.Value)
            {
                MessageBox.Show("Must be less than End Spectra Scan Number if End Spectra Scan Number is not \"Auto\"");
                StartSpectraScanNumBox.Value = EndSpectraScanNumBox.Value;
            }

            StartSpectraScanNumBox.Value = Math.Round(StartSpectraScanNumBox.Value);
        }

        private void EndSpectraScanNumBox_Leave(object sender, EventArgs e)
        {
            var EndSpectraScanNumBox = (NumericUpDown)sender;
            NumericUpDown StartSpectraScanNumBox;

            if (ProgramModeBox.Text == "MyriMatch")
                StartSpectraScanNumBox = MyriStartSpectraScanNumBox;
            else if (ProgramModeBox.Text == "DirecTag")
                StartSpectraScanNumBox = DTStartSpectraScanNumBox;
            else
                StartSpectraScanNumBox = TRStartSpectraScanNumBox;

            if (EndSpectraScanNumBox.Value != -1 && StartSpectraScanNumBox.Value > EndSpectraScanNumBox.Value)
            {
                MessageBox.Show("Must be either \"Auto\" or greater than Start Spectra Scan Number");
                EndSpectraScanNumBox.Value = StartSpectraScanNumBox.Value;
            }

            EndSpectraScanNumBox.Value = Math.Round(EndSpectraScanNumBox.Value);
        }

        private void StartProteinIndexBox_Leave(object sender, EventArgs e)
        {
            var StartProteinIndexBox = (NumericUpDown)sender;

            var EndProteinIndexBox = ProgramModeBox.Text == "MyriMatch" 
                ? MyriEndProteinIndexBox : TREndProteinIndexBox;

            if (EndProteinIndexBox.Value != -1 && StartProteinIndexBox.Value > EndProteinIndexBox.Value)
            {
                MessageBox.Show("Must be less than End Protein Index if End Protein Index is not \"Auto\"");
                StartProteinIndexBox.Value = EndProteinIndexBox.Value;
            }

            StartProteinIndexBox.Value = Math.Round(StartProteinIndexBox.Value);
        }

        private void EndProteinIndexBox_Leave(object sender, EventArgs e)
        {
            var EndProteinIndexBox = (NumericUpDown)sender;

            var StartProteinIndexBox = ProgramModeBox.Text == "MyriMatch"
                ? MyriStartProteinIndexBox : TRStartProteinIndexBox;

            if (EndProteinIndexBox.Value != -1 && StartProteinIndexBox.Value > EndProteinIndexBox.Value)
            {
                MessageBox.Show("Must be either \"Auto\" or greater than Start Protein Index");
                EndProteinIndexBox.Value = StartProteinIndexBox.Value;
            }

            EndProteinIndexBox.Value = Math.Round(EndProteinIndexBox.Value);
        }

        private void MinSequenceMassBox_Leave(object sender, EventArgs e)
        {
            var MinSequenceMassBox = (NumericUpDown)sender;

            var MaxSequenceMassBox = ProgramModeBox.Text == "MyriMatch" 
                ? MyriMaxSequenceMassBox : TRMaxSequenceMassBox;

            if (MinSequenceMassBox.Value > MaxSequenceMassBox.Value)
            {
                MessageBox.Show("Min Sequence Mass cannot be larger than Max Sequence Mass");
                MinSequenceMassBox.Value = MaxSequenceMassBox.Value;
            }
        }

        private void MaxSequenceMassBox_Leave(object sender, EventArgs e)
        {
            var MaxSequenceMassBox = (NumericUpDown)sender;

            var MinSequenceMassBox = ProgramModeBox.Text == "MyriMatch"
                ? MyriMinSequenceMassBox : TRMinSequenceMassBox;

            if (MaxSequenceMassBox.Value < MinSequenceMassBox.Value)
            {
                MessageBox.Show("Max Sequence Mass cannot be smaller than Min Sequence Mass");
                MaxSequenceMassBox.Value = MinSequenceMassBox.Value;
            }
        }

        private void NumericTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {

            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                if (!(e.KeyChar.Equals('-') && ((TextBox)sender).SelectionStart == 0 && !((TextBox)sender).Text.Contains('-')))
                {
                    if (!(e.KeyChar.Equals('.') && !((TextBox)sender).Text.Contains('.')))
                    {
                        e.Handled = true;
                    }
                }
            }

            if (((TextBox)sender).SelectionStart == 0 && ((TextBox)sender).SelectionLength == 0 && ((TextBox)sender).Text.Contains('-'))
                e.Handled = true;
        }

        private void NumericTextBox_Leave(object sender, EventArgs e)
        {
            try
            {
                ((TextBox)sender).Text = double.Parse(((TextBox)sender).Text).ToString();
            }
            catch
            {
                ((TextBox)sender).Text = string.Empty;
            }
        }

        private bool _skipAutomation;
        private void ExplainUnknownMassShiftsAsBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_skipAutomation)
            {
                var KeepExplanation = false;

                if (TRExplainUnknownMassShiftsAsBox.Text == "PreferredPTMs")
                {
                    for (var x = 0; x < TRAppliedModBox.Rows.Count; x++)
                    {
                        if (TRAppliedModBox.Rows[x].Cells[2].Value.ToString() == "PreferredPTM")
                        {
                            KeepExplanation = true;
                            break;
                        }
                    }

                    if (!KeepExplanation)
                    {
                        _skipAutomation = true;
                        MessageBox.Show("Please select a list of preferred ptms in the general tab before setting to PreferredPTMs mode");
                        TRExplainUnknownMassShiftsAsBox.Text = string.Empty;
                        _skipAutomation = false;
                    }
                }
            }
        }

        private void ResidueText_TextChanged(object sender, EventArgs e)
        {
            var ResidueTextBox = (TextBox)sender;
            var dynRx =
                new Regex(
                    @"^(?:\(|\)|(?:\(|{\(})?(?:(?:\[[ACDEFGHIKLMNPQRSTUVWXY]+\])|(?:{[ACDEFGHIKLMNPQRSTUVWXY]+})|(?:[ACDEFGHIKLMNPQRSTUVWXY]))+!?(?:(?:\[[ACDEFGHIKLMNPQRSTUVWXY]+\])|(?:{[ACDEFGHIKLMNPQRSTUVWXY]+})|(?:[ACDEFGHIKLMNPQRSTUVWXY]))*(?:\)|{\)})?)$");

            if (dynRx.IsMatch(ResidueTextBox.Text) || ResidueTextBox.Text.Length == 0)
                ResidueTextBox.BackColor = Color.White;
            else
                ResidueTextBox.BackColor = Color.LightPink;
        }

        #endregion


    }
}
