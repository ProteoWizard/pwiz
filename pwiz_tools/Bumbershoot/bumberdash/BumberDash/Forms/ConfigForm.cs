//
// $Id$
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
// The Original Code is the Bumberdash project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Matt Chambers
//
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
        private bool _isCustom = false; //stores whether configuration is temorary
        private Dictionary<Control, string> _templateDefaults; //Stores default values for current template
        private IList<ConfigFile> _myriTemplateList; //List of all templates user has specified for MyriMatch
        private IList<ConfigFile> _DTTemplateList; //List of all templates user has specified for Directag
        private IList<ConfigFile> _TRTemplateList; //List of all templates user has specified for Tagrecon
        private IList<ConfigFile> _pepTemplateList; //List of all templates user has specified for Tagrecon
        private HashSet<Control> _nonTemplate;
        private HashSet<Control> _changedItems;

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
                                {"TagRecon", new List<Control>()},
                                {"Pepitome", new List<Control>()}
                            };
            _nonTemplate = new HashSet<Control>();
            _changedItems = new HashSet<Control>();

            SetInitialValues();
            InitializePane(MyriGenPanel);
            InitializePane(MyriAdvPanel);
            InitializePane(DTGenPanel);
            InitializePane(DTAdvPanel);
            InitializePane(TRGenPanel);
            InitializePane(TRAdvPanel);
            InitializePane(PepGenPanel);
            InitializePane(PepAdvPanel);

            mainTabControl.TabPages.Remove(AdvTab);

            //Add templates to list
            ResetTemplateLists();
        }

        /// <summary>
        /// Create ConfigForm in edit/clone mode
        /// </summary>
        /// <param name="baseConfig"></param>
        /// <param name="baseDirectory"></param>
        /// <param name="defaultName"></param>
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
            _nonTemplate = new HashSet<Control>();
            _changedItems = new HashSet<Control>();

            SetInitialValues();
            switch (baseConfig.DestinationProgram)
            {
                case "MyriMatch":
                    this.Text = "MyriMatch Configuration Editor";
                    _myriTemplateList = templates.Where(x => x.DestinationProgram == "MyriMatch").ToList();
                    InitializePane(MyriGenPanel);
                    InitializePane(MyriAdvPanel);
                    MyriInstrumentList.Items.Add("New");
                    foreach (var item in _myriTemplateList)
                        MyriInstrumentList.Items.Add(item.Name);
                    MyriInstrumentList.Text = "New";
                    break;
                case "DirecTag":
                    this.Text = "DirecTag Configuration Editor";
                    _DTTemplateList = templates.Where(x => x.DestinationProgram == "DirecTag").ToList();
                    InitializePane(DTGenPanel);
                    InitializePane(DTAdvPanel);
                    DTInstrumentList.Items.Add("New");
                    foreach (var item in _DTTemplateList)
                        DTInstrumentList.Items.Add(item.Name);
                    DTInstrumentList.Text = "New";
                    break;
                case "TagRecon":
                    this.Text = "TagRecon Configuration Editor";
                    _TRTemplateList = templates.Where(x => x.DestinationProgram == "TagRecon").ToList();
                    InitializePane(TRGenPanel);
                    InitializePane(TRAdvPanel);
                    TRInstrumentList.Items.Add("New");
                    foreach (var item in _TRTemplateList)
                        TRInstrumentList.Items.Add(item.Name);
                    TRInstrumentList.Text = "New";
                    break;
                case "Pepitome":
                    this.Text = "Pepitome Configuration Editor";
                    _pepTemplateList = templates.Where(x => x.DestinationProgram == "Pepitome").ToList();
                    InitializePane(PepGenPanel);
                    InitializePane(PepAdvPanel);
                    PepInstrumentList.Items.Add("New");
                    foreach (var item in _pepTemplateList)
                        PepInstrumentList.Items.Add(item.Name);
                    PepInstrumentList.Text = "New";
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
        /// <param name="defaultName"></param>
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
            _nonTemplate = new HashSet<Control>();
            _changedItems = new HashSet<Control>();
            SetInitialValues();
            
            switch (configProgram)
            {
                case "MyriMatch":
                    this.Text = "MyriMatch Configuration Editor";
                    _myriTemplateList = templates.Where(x => x.DestinationProgram == "MyriMatch").ToList();
                    InitializePane(MyriGenPanel);
                    InitializePane(MyriAdvPanel);
                    MyriInstrumentList.Items.Add("New");
                    foreach (var item in _myriTemplateList)
                        MyriInstrumentList.Items.Add(item.Name);
                    MyriInstrumentList.Text = "New";
                    break;
                case "DirecTag":
                    this.Text = "DirecTag Configuration Editor";
                    _DTTemplateList = templates.Where(x => x.DestinationProgram == "DirecTag").ToList();
                    InitializePane(DTGenPanel);
                    InitializePane(DTAdvPanel);
                    DTInstrumentList.Items.Add("New");
                    foreach (var item in _DTTemplateList)
                        DTInstrumentList.Items.Add(item.Name);
                    DTInstrumentList.Text = "New";
                    break;
                case "TagRecon":
                    this.Text = "TagRecon Configuration Editor";
                    _TRTemplateList = templates.Where(x => x.DestinationProgram == "TagRecon").ToList();
                    InitializePane(TRGenPanel);
                    InitializePane(TRAdvPanel);
                    TRInstrumentList.Items.Add("New");
                    foreach (var item in _TRTemplateList)
                        TRInstrumentList.Items.Add(item.Name);
                    TRInstrumentList.Text = "New";
                    break;
                case "Pepitome":
                    this.Text = "Pepitome Configuration Editor";
                    _pepTemplateList = templates.Where(x => x.DestinationProgram == "Pepitome").ToList();
                    InitializePane(PepGenPanel);
                    InitializePane(PepAdvPanel);
                    PepInstrumentList.Items.Add("New");
                    foreach (var item in _pepTemplateList)
                        PepInstrumentList.Items.Add(item.Name);
                    PepInstrumentList.Text = "New";
                    break;
            }
            mainTabControl.TabPages.Remove(AdvTab);
        }

        private bool _noPrompt = false;
        private void ResetTemplateLists()
        {
            _noPrompt = true;
            var templateList = _session.QueryOver<ConfigFile>().Where(x => x.FilePath == "Template").List();
            _myriTemplateList = templateList.Where(x => x.DestinationProgram == "MyriMatch").ToList();
            _DTTemplateList = templateList.Where(x => x.DestinationProgram == "DirecTag").ToList();
            _TRTemplateList = templateList.Where(x => x.DestinationProgram == "TagRecon").ToList();
            _pepTemplateList = templateList.Where(x => x.DestinationProgram == "Pepitome").ToList();

            MyriInstrumentList.Items.Clear();
            DTInstrumentList.Items.Clear();
            TRInstrumentList.Items.Clear();
            PepInstrumentList.Items.Clear();

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
            PepInstrumentList.Items.Add("New");
            foreach (var item in _pepTemplateList)
                PepInstrumentList.Items.Add(item.Name);
            PepInstrumentList.Text = "New";

            foreach (var item in _itemList[ProgramModeBox.Text])
                CheckForChange(item, null);
            _noPrompt = false;
        }

        /// <summary>
        /// Sets initial (default) values for DropDownList items (cant be done in editor)
        /// </summary>
        private void SetInitialValues()
        {
            MyriPrecursorMzToleranceRuleBox.Text = "auto";
            MyriMinTerminiCleavagesBox.Text = "Fully-Specific";
            MyriAvgPrecursorMzToleranceUnitsList.Text = "mz";
            MyriMonoPrecursorMzToleranceUnitsList.Text = "ppm";
            MyriFragmentMzToleranceUnitsList.Text = "mz";
            MyriModTypeList.Text = "Static";
            MyriOutputFormatBox.Text = "pepXML";
            DTPrecursorMzToleranceUnitsList.Text = "mz";
            DTFragmentMzToleranceUnitsList.Text = "mz";
            DTDeisotopingModeBox.Text = "Off";
            DTModTypeList.Text = "Static";
            TRUseAvgMassOfSequencesBox.Text = "mono";
            TRMinTerminiCleavagesBox.Text = "Fully-Specific";
            TRPrecursorMzToleranceUnitsList.Text = "mz";
            TRFragmentMzToleranceUnitsList.Text = "mz";
            TRNTerminusMzToleranceUnitsList.Text = "mz";
            TRCTerminusMzToleranceUnitsList.Text = "mz";
            TRModTypeList.Text = "Static";
            TROutputFormatBox.Text = "pepXML";
            PepPrecursorMzToleranceRuleBox.Text = "auto";
            PepMinTerminiCleavagesBox.Text = "Fully-Specific";
            PepAvgPrecursorMzToleranceUnitsList.Text = "mz";
            PepMonoPrecursorMzToleranceUnitsList.Text = "ppm";
            PepFragmentMzToleranceUnitsList.Text = "mz";
            PepModTypeList.Text = "Static";
            PepOutputFormatBox.Text = "pepXML";
            CometInstrumentBox.Text = "High Resolution";
            MSGFInstrumentBox.Text = "High Resolution LTQ";
            MSGFFragmentMethodBox.Text = "CID";

            MyriAvgPrecursorMzToleranceUnitsList.SelectedValueChanged += CheckDualDependenceChange;
            MyriMonoPrecursorMzToleranceUnitsList.SelectedValueChanged += CheckDualDependenceChange;
            MyriFragmentMzToleranceUnitsList.SelectedValueChanged += CheckDualDependenceChange;
            MyriMonoisotopeAdjustmentSet2.ValueChanged += CheckDualDependenceChange;
            PepAvgPrecursorMzToleranceUnitsList.SelectedValueChanged += CheckDualDependenceChange;
            PepMonoPrecursorMzToleranceUnitsList.SelectedValueChanged += CheckDualDependenceChange;
            PepFragmentMzToleranceUnitsList.SelectedValueChanged += CheckDualDependenceChange;
            PepMonoisotopeAdjustmentSet2.ValueChanged += CheckDualDependenceChange;
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
            var uniModSuggestions = new List<string>();
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
            else if(container.Name.StartsWith("TR"))
            {
                prefix = "TR";
                program = "TagRecon";
            }
            else
            {
                prefix = "Pep";
                program = "Pepitome";
            }

            foreach (var item in Util.UnimodLookup.FullUnimodList)
                uniModSuggestions.Add(item.MonoMass + "     " + item.Name);

            foreach (Control item in container.Controls)
            {
                if (!item.Name.StartsWith(prefix))
                    continue;
                var isDual = GetDualDependenceValue(item);

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
                            ((ComboBox)item).SelectedValueChanged += CheckForChange;
                        else if (item is TextBox)
                            item.TextChanged += CheckForChange;
                        else if (item is NumericUpDown)
                            ((NumericUpDown)item).ValueChanged += CheckForChange;
                        else if (item is CheckBox)
                            ((CheckBox)item).CheckedChanged += CheckForChange;
                    }

                    _itemList[program].Add(item);
                    _defaults.Add(item, GetControlValueString(item).Trim('"'));
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
                            ((ComboBox)box).SelectedValueChanged += CheckForChange;
                        else if (box is TextBox)
                            box.TextChanged += CheckForChange;
                        else if (box is NumericUpDown)
                            ((NumericUpDown)box).ValueChanged += CheckForChange;
                        else if (box is CheckBox)
                            ((CheckBox)box).CheckedChanged += CheckForChange;
                    }
                }
                else if (item.Name.EndsWith("Info"))
                {
                    item.Click += OpenHelpFile;
                    item.MouseEnter += Info_MouseEnter;
                    item.MouseLeave += Info_MouseLeave;
                }
                else if (item.Name.EndsWith("ModMassText"))
                {
                    var box = (TextBox) item;
                    var source = new AutoCompleteStringCollection();
                    source.AddRange(uniModSuggestions.ToArray());

                    box.AutoCompleteMode = AutoCompleteMode.Suggest;
                    box.AutoCompleteSource = AutoCompleteSource.CustomSource;
                    box.AutoCompleteCustomSource = source;

                    box.TextChanged += (x, y) =>
                        {
                            if (box.Text.Contains("     "))
                                box.Text = box.Text.Remove(box.Text.IndexOf(' '));
                        };
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
            var value = GetControlValueString(item).Trim('"');
            var isModBox = false;

            if (RootName(item.Name) == "AppliedMod" && value.Length > 0)
            {
                isModBox = true;
                value += "\"";
            }

            double firstNum;
            double secondNum;
            var numbersSame = false;
            if (double.TryParse(value, out firstNum) && double.TryParse(_defaults[item], out secondNum))
            {
                if (Math.Round(firstNum, 6) == Math.Round(secondNum, 6))
                    numbersSame = true;
            }

            if (numbersSame || value == _defaults[item] || (isModBox && ModStringsEqual(value, _defaults[item])))
            {
                _changedItems.Remove(item);
                if (!_templateDefaults.ContainsKey(item)
                    || value == _templateDefaults[item]
                    || (isModBox && ModStringsEqual(value, _templateDefaults[item])))
                    _labelAssociation[item].ForeColor = DefaultForeColor;
                else
                    _labelAssociation[item].ForeColor = Color.Blue;
            }
            else
            {
                _changedItems.Add(item);
                if (!_templateDefaults.ContainsKey(item)
                    || value == _templateDefaults[item]
                    || (isModBox && ModStringsEqual(value, _templateDefaults[item])))
                    _labelAssociation[item].ForeColor = Color.Green;
                else
                    _labelAssociation[item].ForeColor = Color.DarkViolet;
            }

            //Check for changes from template
            if (!_templateDefaults.ContainsKey(item))
            {
                if (value == _defaults[item])
                    _nonTemplate.Remove(item);
                else
                    _nonTemplate.Add(item);
            }
            else
            {
                if (value == _templateDefaults[item]
                    || (isModBox && ModStringsEqual(value, _templateDefaults[item])))
                    _nonTemplate.Remove(item);
                else
                    _nonTemplate.Add(item);
            }
        }

        public bool ContainsNonDefaultConfiguration()
        {
            return _changedItems.Any();
        }

        /// <summary>
        /// Attempts to open help file at location specified by control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OpenHelpFile(object sender, EventArgs e)
        {
            if (sender == DTIntensityScoreWeightInf2 || sender == DTIntensityScoreWeightInf3)
                sender = DTIntensityScoreWeightInfo;
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

        private List<ConfigProperty> GetProperties(List<Control> items, ConfigFile newConfig)
        {
            var propertyList = new List<ConfigProperty>();
            foreach (var item in items)
            {
                var root = RootName(item.Name);
                var isDual = GetDualDependenceValue(item);
                if (isDual != null)
                    propertyList.Add(new ConfigProperty
                        {
                            Name = root,
                            Type = "string",
                            ConfigAssociation = newConfig,
                            Value = isDual
                        });
                else if (root == "AppliedMod")
                {
                    var modProperties = GetModProperties((DataGridView)item);
                    foreach (var property in modProperties)
                        property.ConfigAssociation = newConfig;
                    propertyList.AddRange(modProperties);
                }
                else if (root == "UseAvgMassOfSequences")
                    propertyList.Add(new ConfigProperty
                        {
                            Name = root,
                            Type = "string",
                            ConfigAssociation = newConfig,
                            Value = (((ComboBox)item).SelectedIndex == 1).ToString().ToLower()
                        });
                else if (root == "MinTerminiCleavages" || root == "DeisotopingMode")
                    propertyList.Add(new ConfigProperty
                        {
                            Name = root,
                            Type = "string",
                            ConfigAssociation = newConfig,
                            Value = ((ComboBox)item).SelectedIndex.ToString()
                        });
                else if (root == "UnimodXML" && TRUnimodXMLBox.Text == "Default")
                    propertyList.Add(new ConfigProperty
                        {
                            Name = root,
                            Type = "string",
                            ConfigAssociation = newConfig,
                            Value = Path.Combine(Application.StartupPath, @"lib\Bumbershoot\TagRecon\unimod.xml")
                        });
                else if (root == "Blosum" && TRBlosumBox.Text == "Default")
                    propertyList.Add(new ConfigProperty
                        {
                            Name = root,
                            Type = "string",
                            ConfigAssociation = newConfig,
                            Value = Path.Combine(Application.StartupPath, @"lib\Bumbershoot\TagRecon\blosum62.fas")
                        });
                else if (root == "DeisotopingMode")
                    propertyList.Add(new ConfigProperty
                        {
                            Name = root,
                            Type = "string",
                            ConfigAssociation = newConfig,
                            Value = ((ComboBox)item).SelectedIndex.ToString()
                        });
                else if (item is ComboBox || item is TextBox)
                {
                    double x;
                    propertyList.Add(new ConfigProperty
                        {
                            Name = root,
                            Type = "string",
                            ConfigAssociation = newConfig,
                            Value = double.TryParse(item.Text, out x)
                                        ? item.Text
                                        : string.Format("\"{0}\"", item.Text)
                        });
                }
                if (item is NumericUpDown)
                    propertyList.Add(new ConfigProperty
                        {
                            Name = root,
                            Type = "string",
                            ConfigAssociation = newConfig,
                            Value = ((NumericUpDown) item).Value.ToString()
                        });
                if (item is CheckBox)
                    propertyList.Add(new ConfigProperty
                        {
                            Name = root,
                            Type = "string",
                            ConfigAssociation = newConfig,
                            Value = ((CheckBox) item).Checked.ToString().ToLower()
                        });
            }
            return propertyList;
        }

        /// <summary>
        /// Returns value of "Box" suffix control, taking into account special cases
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private string GetControlValueString(Control item)
        {
            var isDual = GetDualDependenceValue(item);
            if (isDual != null)
                return isDual;

            var root = RootName(item.Name);

            if (root == "AppliedMod")
                return GetModString((DataGridView)item);
            if (root == "UseAvgMassOfSequences")
                return (((ComboBox)item).SelectedIndex == 1).ToString().ToLower();
            if (root ==  "MinTerminiCleavages")
                return ((ComboBox)item).SelectedIndex.ToString();
            if (root == "DeisotopingMode")
                return ((ComboBox)item).SelectedIndex.ToString();
            if (root == "UnimodXML" && TRUnimodXMLBox.Text == "Default")
                return Path.Combine(Application.StartupPath, @"lib\Bumbershoot\TagRecon\unimod.xml");
            if (root == "Blosum" && TRBlosumBox.Text == "Default")
                return Path.Combine(Application.StartupPath, @"lib\Bumbershoot\TagRecon\blosum62.fas");
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

        private string GetDualDependenceValue(Control item)
        {
            //MyriMatch
            if (item == MyriMonoisotopeAdjustmentSetBox)
            {
                return MyriAdjustMassOption.Checked
                           ? string.Format("\"[{0},{1}]\"", MyriMonoisotopeAdjustmentSetBox.Value,
                                           MyriMonoisotopeAdjustmentSet2.Value)
                           : "\"\"";
            }
            if (item == MyriAvgPrecursorMzToleranceBox)
                return "\"" + item.Text + MyriAvgPrecursorMzToleranceUnitsList.Text + "\"";
            if (item == MyriMonoPrecursorMzToleranceBox)
                return "\"" + item.Text + MyriMonoPrecursorMzToleranceUnitsList.Text + "\"";
            if (item == MyriFragmentMzToleranceBox)
                return "\"" + item.Text + MyriFragmentMzToleranceUnitsList.Text + "\"";

            //Pepitome
            if (item == PepMonoisotopeAdjustmentSetBox)
            {
                return PepAdjustMassOption.Checked
                           ? string.Format("\"[{0},{1}]\"", PepMonoisotopeAdjustmentSetBox.Value,
                                           PepMonoisotopeAdjustmentSet2.Value)
                           : "\"\"";
            }
            if (item == PepAvgPrecursorMzToleranceBox)
                return "\"" + item.Text + PepAvgPrecursorMzToleranceUnitsList.Text + "\"";
            if (item == PepMonoPrecursorMzToleranceBox)
                return "\"" + item.Text + PepMonoPrecursorMzToleranceUnitsList.Text + "\"";
            if (item == PepFragmentMzToleranceBox)
                return "\"" + item.Text + PepFragmentMzToleranceUnitsList.Text + "\"";
            return null;
        }

        private void SetDualDependenceValue(Control item, string value)
        {
            string[] splitValue;

            if (item == MyriMonoisotopeAdjustmentSetBox ||
                item == PepMonoisotopeAdjustmentSetBox)
            {
                if (value.Trim('"') == string.Empty)
                {
                    if (item == MyriMonoisotopeAdjustmentSetBox)
                        MyriAdjustMassOption.Checked = false;
                    else
                        PepAdjustMassOption.Checked = false;
                    return;
                }

                int first;
                int second;
                splitValue = value.Trim().Trim('"').Trim().Trim('[').Trim(']').Split(',');
                int.TryParse(splitValue[0], out first);
                int.TryParse(splitValue[1], out second);
                if (item == MyriMonoisotopeAdjustmentSetBox)
                {
                    MyriMonoisotopeAdjustmentSetBox.Value = first;
                    MyriMonoisotopeAdjustmentSet2.Value = second;
                }
                else
                {
                    PepMonoisotopeAdjustmentSetBox.Value = first;
                    PepMonoisotopeAdjustmentSet2.Value = second;
                }
                return;
            }

            splitValue = GetSplitMZToleranceValue(value);
            try
            {
                //MyriMatch
                if (item == MyriAvgPrecursorMzToleranceBox)
                {
                    item.Text = splitValue[0];
                    MyriAvgPrecursorMzToleranceUnitsList.Text = splitValue[1];
                }
                if (item == MyriMonoPrecursorMzToleranceBox)
                {
                    item.Text = splitValue[0];
                    MyriMonoPrecursorMzToleranceUnitsList.Text = splitValue[1];
                }
                if (item == MyriFragmentMzToleranceBox)
                {
                    item.Text = splitValue[0];
                    MyriFragmentMzToleranceUnitsList.Text = splitValue[1];
                }

                //TagRecon
                if (item == TRPrecursorMzToleranceBox)
                {
                    item.Text = splitValue[0];
                    TRPrecursorMzToleranceUnitsList.Text = splitValue[1];
                }
                if (item == TRFragmentMzToleranceBox)
                {
                    item.Text = splitValue[0];
                    TRFragmentMzToleranceUnitsList.Text = splitValue[1];
                }

                //Pepitome
                if (item == PepAvgPrecursorMzToleranceBox)
                {
                    item.Text = splitValue[0];
                    PepAvgPrecursorMzToleranceUnitsList.Text = splitValue[1];
                }
                if (item == PepMonoPrecursorMzToleranceBox)
                {
                    item.Text = splitValue[0];
                    PepMonoPrecursorMzToleranceUnitsList.Text = splitValue[1];
                }
                if (item == PepFragmentMzToleranceBox)
                {
                    item.Text = splitValue[0];
                    PepFragmentMzToleranceUnitsList.Text = splitValue[1];
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("There was an error loading one of the MZ Tolerance values: " + e.Message);
            }
        }

        private void CheckDualDependenceChange(object sender, EventArgs e)
        {
            var item = (Control) sender;

            //MyriMatch
            if (item == MyriMonoisotopeAdjustmentSet2)
                CheckForChange(MyriMonoisotopeAdjustmentSetBox, e);
            else if (item == MyriAvgPrecursorMzToleranceUnitsList)
                CheckForChange(MyriAvgPrecursorMzToleranceBox, e);
            else if (item == MyriMonoPrecursorMzToleranceUnitsList)
                CheckForChange(MyriMonoPrecursorMzToleranceBox, e);
            else if (item == MyriFragmentMzToleranceUnitsList)
                CheckForChange(MyriFragmentMzToleranceBox, e);

            //TagRecon
            else if (item == TRPrecursorMzToleranceUnitsList)
                CheckForChange(TRPrecursorMzToleranceBox, e);
            else if (item == TRFragmentMzToleranceUnitsList)
                CheckForChange(TRFragmentMzToleranceBox, e);

            //Pepitome
            else if (item == PepMonoisotopeAdjustmentSet2)
                CheckForChange(PepMonoisotopeAdjustmentSetBox, e);
            else if (item == PepAvgPrecursorMzToleranceUnitsList)
                CheckForChange(PepAvgPrecursorMzToleranceBox, e);
            else if (item == PepMonoPrecursorMzToleranceUnitsList)
                CheckForChange(PepMonoPrecursorMzToleranceBox, e);
            else if (item == PepFragmentMzToleranceUnitsList)
                CheckForChange(PepFragmentMzToleranceBox, e);
        }

        private string[] GetSplitMZToleranceValue(string value)
        {
            var value1 = "0";
            var value1Match = Regex.Match(value, @"\d*\.?\d*");
            if (value1Match != Match.Empty)
                value1 = value1Match.ToString();
            var value2 = value.Remove(0, value1.Length);
            return new[] {value1, value2};

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
            _nonTemplate = new HashSet<Control>();
            foreach (var control in _itemList[ProgramModeBox.Text])
            {
                var root = RootName(control.Name);
                var value = _templateDefaults.ContainsKey(control)
                                ? _templateDefaults[control]
                                : _defaults[control];

                var isDual = GetDualDependenceValue(control);
                if (isDual != null)
                    SetDualDependenceValue(control, value);
                else if (root == "AppliedMod")
                    SetModString(value);
                else if (root == "UseAvgMassOfSequences")
                    ((ComboBox)control).SelectedIndex = (value == "true") ? 1 : 0;
                else if (root == "MinTerminiCleavages" || root == "DeisotopingMode")
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
                if (root == "MaxMissedCleavages" && decimal.Parse(value) > 90000)
                    value = "-1";
                if (root == "TicCutoffPercentage" || root == "LibTicCutoffPercentage")
                    value = Math.Round(decimal.Parse(value), 2).ToString();
                if (root == "PrecursorAdjustmentStep")
                    value = Math.Round(decimal.Parse(value), 6).ToString();

                var isDual = GetDualDependenceValue(kvp.Key);
                if (isDual != null)
                    SetDualDependenceValue(kvp.Key, value);
                else if (root == "AppliedMod")
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
                else if (root == "MinTerminiCleavages" || root == "DeisotopingMode")
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
            PepGenPanel.Visible = false;
            PepAdvPanel.Visible = false;
        }

        /// <summary>
        /// Removes destination program prefix and control type suffix from name
        /// </summary>
        /// <param name="fullWord"></param>
        /// <returns></returns>
        private string RootName(string fullWord)
        {
            fullWord = fullWord.Remove(0, fullWord.StartsWith("Myri")
                                              ? 4
                                              : (fullWord.StartsWith("Pep")
                                                     ? 3
                                                     : 2));

            if (fullWord.EndsWith("Info") || fullWord.EndsWith("Auto"))
                fullWord = fullWord.Remove(fullWord.Length - 4, 4);
            else if (fullWord.EndsWith("Label"))
                fullWord = fullWord.Remove(fullWord.Length - 5, 5);
            else if (fullWord.EndsWith("Box"))
                fullWord = fullWord.Remove(fullWord.Length - 3, 3);

            return fullWord;
        }
        private List<ConfigProperty> GetModProperties(DataGridView dgv)
        {
            var staticList = new List<string>();
            var dynamicList = new List<string>();
            var ptmList = new List<string>();

            foreach (DataGridViewRow row in dgv.Rows)
            {
                if ((string)row.Cells[2].Value == "Static")
                    staticList.Add(string.Format("{0} {1}", row.Cells[0].Value, row.Cells[1].Value));
                else if ((string)row.Cells[2].Value == "Dynamic")
                    dynamicList.Add(string.Format("{0} * {1}", row.Cells[0].Value, row.Cells[1].Value));
                else
                    ptmList.Add(string.Format("{0} {1}", row.Cells[0].Value, row.Cells[1].Value));
            }
            var configList = new List<ConfigProperty>();
            if (staticList.Any())
                configList.Add(new ConfigProperty
                    {
                        Name = "StaticMods",
                        Type = "string",
                        Value = "\"" + string.Join(" ", staticList.ToArray()) + "\""
                    });
            if (dynamicList.Any())
                configList.Add(new ConfigProperty
                    {
                        Name = "DynamicMods",
                        Type = "string",
                        Value = "\"" + string.Join(" ", dynamicList.ToArray()) + "\""
                    });
            if (ptmList.Any())
                configList.Add(new ConfigProperty
                    {
                        Name = "PreferredDeltaMasses",
                        Type = "string",
                        Value = "\"" + string.Join(" ", ptmList.ToArray()) + "\""
                    });
            return configList;
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
            else if (ProgramModeBox.Text == "TagRecon")
                dgv = TRAppliedModBox;
            else
                dgv = PepAppliedModBox;

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

        internal ConfigFile GetMainConfigFile()
        {
            if (ProgramModeBox.Text == "MyriMatch" && !ProgramSelectMyri.Checked)
                return null;
            var changed = new List<Control>();
            if (_isCustom)
                foreach (var item in _itemList[ProgramModeBox.Text])
                {
                    var value = GetControlValueString(item);
                    var compareValue = value.Trim('"');
                    var root = RootName(item.Name);
                    var isModBox = root == "AppliedMod";

                    if (isModBox && compareValue.Length > 0)
                        compareValue += "\"";

                    if (compareValue != _defaults[item] || (isModBox && !ModStringsEqual(compareValue, _defaults[item])))
                        changed.Add(item);
                }

            var newConfig = new ConfigFile();
            newConfig.DestinationProgram = ProgramModeBox.Text;
            newConfig.Name = "--Custom--";
            newConfig.PropertyList = _isCustom ? GetProperties(changed, newConfig) : null;
            newConfig.FilePath = _isCustom ? "--Custom--" : _filePath;
            return newConfig;
        }

        private string GetConfigString(bool redundant)
        {

            #region Group and order list

            var groupOrder = new List<string>
                                 {
                                     "AvgPrecursorMzTolerance",
                                     "MonoPrecursorMzTolerance",
                                     "PrecursorMzTolerance",
                                     "FragmentMzTolerance",
                                     "NTerminusMzTolerance",
                                     "CTerminusMzTolerance",
                                     Environment.NewLine,
                                     "AdjustPrecursorMass",
                                     "MaxPrecursorAdjustment",
                                     "MinPrecursorAdjustment",
                                     "MonoisotopeAdjustmentSet",
                                     "PrecursorAdjustmentStep",
                                     Environment.NewLine,
                                     "DuplicateSpectra",
                                     "UseChargeStateFromMS",
                                     "NumChargeStates",
                                     "TicCutoffPercentage",
                                     "UseSmartPlusThreeModel",
                                     Environment.NewLine,
                                     "CleavageRules",
                                     "FragmentationRule",
                                     "MinTerminiCleavages",
                                     "MaxMissedCleavages",
                                     "UseAvgMassOfSequences",
                                     "PrecursorMzToleranceRule",
                                     "MinPeptideLength",
                                     "MaxPeptideLength",
                                     Environment.NewLine,
                                     "AppliedMod",
                                     "MaxDynamicMods",
                                     "MaxNumPreferredDeltaMasses",
                                     "MaxAmbResultsForBlindMods",
                                     Environment.NewLine,
                                     "ExplainUnknownMassShiftsAs",
                                     "MaxModificationMassPlus",
                                     "MaxModificationMassMinus",
                                     "UnimodXML",
                                     "Blosum",
                                     "BlosumThreshold",
                                     Environment.NewLine,
                                     "MaxResultRank",
                                     Environment.NewLine,
                                     "ProteinSamplingTime",
                                     Environment.NewLine,
                                     "NumIntensityClasses",
                                     "ClassSizeMultiplier",
                                     Environment.NewLine,
                                     "DeisotopingMode",
                                     "IsotopeMzTolerance",
                                     "ComplementMzTolerance",
                                     Environment.NewLine,
                                     "MinPeptideMass",
                                     "MaxPeptideMass",
                                     Environment.NewLine,
                                     "ComputeXCorr",
                                     "UseNETAdjustment",
                                     "MassReconMode",
                                     "OutputFormat",
                                     Environment.NewLine,
                                     "MaxPeakCount",
                                     "TagLength",
                                     "IntensityScoreWeight",
                                     "MzFidelityScoreWeight",
                                     "ComplementScoreWeight",
                                     "MaxTagCount",
                                     "MaxTagScore",
                                     "DecoyPrefix",
                                     Environment.NewLine,
                                     "CleanLibSpectra",
                                     "FASTARefreshResults",
                                     "LibMaxPeakCount",
                                     "LibTicCutoffPercentage",
                                     "RecalculateLibPepMasses",
                                     Environment.NewLine
                                 };

            #endregion

            #region Redundant items
            var redundantItems = new List<string>
                                     {
                                         "AvgPrecursorMzTolerance",
                                         "MonoPrecursorMzTolerance",
                                         "PrecursorMzTolerance",
                                         "FragmentMzTolerance",
                                         "UseChargeStateFromMS",
                                         "NumChargeStates",
                                         "TicCutoffPercentage",
                                         "CleavageRules",
                                         "MinTerminiCleavages",
                                         "MaxMissedCleavages",
                                         "MaxResults",
                                         "NTerminusMzTolerance",
                                         "CTerminusMzTolerance",
                                         "UnimodXML",
                                         "Blosum"
                                     };
            #endregion

            var changed = new Dictionary<string, string>();
            foreach (var item in _itemList[ProgramModeBox.Text])
            {
                var value = GetControlValueString(item);
                var compareValue = value.Trim('"');
                var root = RootName(item.Name);
                var isModBox = root == "AppliedMod";

                if (isModBox && compareValue.Length > 0)
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
                    Size = new Size(540, 645);
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
                case "Pepitome":
                    Size = new Size(540, 620);
                    PepGenPanel.Visible = true;
                    PepAdvPanel.Visible = true;
                    break;
            }

            //If form has fully loaded update non-template list
            if (_nonTemplate != null)
            {
                _nonTemplate = new HashSet<Control>();
                foreach (var control in _itemList[ProgramModeBox.Text])
                    CheckForChange(control, null);
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
                MyriMaxMissedCleavagesAuto.Visible = MyriMaxMissedCleavagesBox.Value == -1;
            else if (ProgramModeBox.Text == "TagRecon")
                TRMaxMissedCleavagesAuto.Visible = TRMaxMissedCleavagesBox.Value == -1;
            else
                PepMaxMissedCleavagesAuto.Visible = PepMaxMissedCleavagesBox.Value == -1;
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
            else if (ProgramModeBox.Text == "TagRecon")
            {
                ResidueBox = TRResidueText;
                ModMassBox = TRModMassText;
                ModTypeBox = TRModTypeList;
                AppliedModDGV = TRAppliedModBox;
            }
            else
            {
                ResidueBox = PepResidueText;
                ModMassBox = PepModMassText;
                ModTypeBox = PepModTypeList;
                AppliedModDGV = PepAppliedModBox;
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
            else if (ProgramModeBox.Text == "TagRecon")
            {
                ResidueBox = TRResidueText;
                ModMassBox = TRModMassText;
                ModTypeBox = TRModTypeList;
                AppliedModDGV = TRAppliedModBox;
            }
            else
            {
                ResidueBox = PepResidueText;
                ModMassBox = PepModMassText;
                ModTypeBox = PepModTypeList;
                AppliedModDGV = PepAppliedModBox;
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
                MyriAvgPrecursorMzToleranceBox.Enabled = true;
                MyriAvgPrecursorMzToleranceUnitsList.Enabled = true;
                MyriMonoPrecursorMzToleranceBox.Enabled = true;
                MyriMonoPrecursorMzToleranceUnitsList.Enabled = true;
                MyriFragmentMzToleranceBox.Enabled = true;
                MyriFragmentMzToleranceUnitsList.Enabled = true;
                MyriMaxMissedCleavagesBox.Enabled = true;
                MyriMaxMissedCleavagesAuto.Enabled = true;
                DTPrecursorMzToleranceBox.Enabled = true;
                DTPrecursorMzToleranceUnitsList.Enabled = true;
                DTFragmentMzToleranceBox.Enabled = true;
                DTFragmentMzToleranceUnitsList.Enabled = true;
                TRPrecursorMzToleranceBox.Enabled = true;
                TRPrecursorMzToleranceUnitsList.Enabled = true;
                TRFragmentMzToleranceBox.Enabled = true;
                TRFragmentMzToleranceUnitsList.Enabled = true;
                TRNTerminusMzToleranceBox.Enabled = true;
                TRNTerminusMzToleranceUnitsList.Enabled = true;
                TRCTerminusMzToleranceBox.Enabled = true;
                TRCTerminusMzToleranceUnitsList.Enabled = true;
                TRMaxMissedCleavagesBox.Enabled = true;
                TRMaxMissedCleavagesAuto.Enabled = true;
                PepAvgPrecursorMzToleranceBox.Enabled = true;
                PepAvgPrecursorMzToleranceUnitsList.Enabled = true;
                PepMonoPrecursorMzToleranceBox.Enabled = true;
                PepMonoPrecursorMzToleranceUnitsList.Enabled = true;
                PepFragmentMzToleranceBox.Enabled = true;
                PepFragmentMzToleranceUnitsList.Enabled = true;
                PepMaxMissedCleavagesBox.Enabled = true;
                PepMaxMissedCleavagesAuto.Enabled = true;
            }
            else
            {
                mainTabControl.TabPages.Remove(AdvTab);
                MyriAvgPrecursorMzToleranceBox.Enabled = false;
                MyriAvgPrecursorMzToleranceUnitsList.Enabled = false;
                MyriMonoPrecursorMzToleranceBox.Enabled = false;
                MyriMonoPrecursorMzToleranceUnitsList.Enabled = false;
                MyriFragmentMzToleranceBox.Enabled = false;
                MyriFragmentMzToleranceUnitsList.Enabled = false;
                MyriMaxMissedCleavagesBox.Enabled = false;
                MyriMaxMissedCleavagesAuto.Enabled = false;
                DTPrecursorMzToleranceBox.Enabled = false;
                DTPrecursorMzToleranceUnitsList.Enabled = false;
                DTFragmentMzToleranceBox.Enabled = false;
                DTFragmentMzToleranceUnitsList.Enabled = false;
                TRPrecursorMzToleranceBox.Enabled = false;
                TRPrecursorMzToleranceUnitsList.Enabled = false;
                TRFragmentMzToleranceBox.Enabled = false;
                TRFragmentMzToleranceUnitsList.Enabled = false;
                TRNTerminusMzToleranceBox.Enabled = false;
                TRNTerminusMzToleranceUnitsList.Enabled = false;
                TRCTerminusMzToleranceBox.Enabled = false;
                TRCTerminusMzToleranceUnitsList.Enabled = false;
                TRMaxMissedCleavagesBox.Enabled = false;
                TRMaxMissedCleavagesAuto.Enabled = false;
                PepAvgPrecursorMzToleranceBox.Enabled = false;
                PepAvgPrecursorMzToleranceUnitsList.Enabled = false;
                PepMonoPrecursorMzToleranceBox.Enabled = false;
                PepMonoPrecursorMzToleranceUnitsList.Enabled = false;
                PepFragmentMzToleranceBox.Enabled = false;
                PepFragmentMzToleranceUnitsList.Enabled = false;
                PepMaxMissedCleavagesBox.Enabled = false;
                PepMaxMissedCleavagesAuto.Enabled = false;
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
            _isCustom = true;
        }

        internal bool IsTemporaryConfiguration()
        {
            return _isCustom;
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
                case "TagRecon":
                    ModList = TRModList;
                    ResidueBox = TRResidueText;
                    ModMassBox = TRModMassText;
                    ModTypeBox = TRModTypeList;
                    break;
                default:
                    ModList = PepModList;
                    ResidueBox = PepResidueText;
                    ModMassBox = PepModMassText;
                    ModTypeBox = PepModTypeList;
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

        private int _currentInstrument = -1;
        private void InstrumentList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_currentInstrument != ((ComboBox) sender).SelectedIndex - 1)
            {
                DialogResult result;
                if (_nonTemplate.Any() && !_noPrompt)
                    result =
                        MessageBox.Show(
                            "Would you like to change the current instrument template?" +
                            Environment.NewLine + Environment.NewLine +
                            "Yes: Discard all changes and load template" +
                            Environment.NewLine +
                            "No: Keep changes and recolor lables according to selected template for sake of comparison" +
                            Environment.NewLine +
                            "Cancel: Keep changes and colors as they are", "Load template?",
                            MessageBoxButtons.YesNoCancel);
                else
                    result = DialogResult.Yes;

                if (result != DialogResult.Cancel)
                {
                    IList<ConfigFile> currentlist;
                    if (ProgramModeBox.Text == "MyriMatch")
                        currentlist = _myriTemplateList;
                    else if (ProgramModeBox.Text == "DirecTag")
                        currentlist = _DTTemplateList;
                    else if (ProgramModeBox.Text == "TagRecon")
                        currentlist = _TRTemplateList;
                    else
                        currentlist = _pepTemplateList;
                    _currentInstrument = ((ComboBox) sender).SelectedIndex - 1;

                    SetTemplateDefaults(_currentInstrument >= 0
                                            ? currentlist[_currentInstrument]
                                            : new ConfigFile {PropertyList = new List<ConfigProperty>()});
                    if (result == DialogResult.Yes)
                    {
                        LoadTemplate();
                        if ((sender) == MyriInstrumentList)
                        {
                            if (MyriInstrumentList.Text.Contains("Ion"))
                            {
                                CometInstrumentBox.Text = "Ion Trap";
                                MSGFInstrumentBox.Text = "Low Resolution LTQ";
                            }
                            else if (MyriInstrumentList.Text.Contains("TOF"))
                            {
                                CometInstrumentBox.Text = "TOF";
                                MSGFInstrumentBox.Text = "TOF";
                            }
                            else
                            {
                                CometInstrumentBox.Text = "High Resolution";
                                MSGFInstrumentBox.Text = "High Resolution LTQ";
                            }
                            var multiSelect = ((ProgramSelectMyri.Checked ? 1 : 0) + (ProgramSelectComet.Checked ? 1 : 0) +
                                   (ProgramSelectMSGF.Checked ? 1 : 0)) > 1;
                            if (ProgramSelectMyri.Checked && multiSelect && string.IsNullOrEmpty(MyriOutputSuffixBox.Text))
                                MyriOutputSuffixBox.Text = "_MM";
                        }
                    }
                }
                else
                {
                    ((ComboBox) sender).SelectedIndex = _currentInstrument + 1;
                }
            }
        }

        private void SaveTemplateButton_Click(object sender, EventArgs e)
        {
            var parameterType = Util.parameterTypes;
            string prefix;

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
                    prefix = "Myri";
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
                    prefix = "DT";
                }
                else if (ProgramModeBox.Text == "TagRecon")
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
                    prefix = "TR";
                }
                else
                {
                    if (PepInstrumentList.SelectedIndex > 0)
                        currentConfig = _pepTemplateList[PepInstrumentList.SelectedIndex - 1];
                    else
                    {
                        currentConfig = new ConfigFile
                        {
                            DestinationProgram = "Pepitome",
                            FilePath = "Template",
                            PropertyList = new List<ConfigProperty>()
                        };
                        newTemplate = true;
                    }
                    prefix = "Pep";
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
                foreach (var kvp in _labelAssociation
                    .Where(kvp => kvp.Key.Name.StartsWith(prefix))
                    .Where(kvp => kvp.Value.ForeColor != DefaultForeColor
                        && kvp.Value.ForeColor != Color.Blue))
                {
                    currentConfig.PropertyList.Add(new ConfigProperty
                                                       {
                                                           Name = RootName(kvp.Key.Name),
                                                           Value = GetControlValueString(kvp.Key),
                                                           Type = parameterType.ContainsKey(RootName(kvp.Key.Name))
                                                                      ? parameterType[RootName(kvp.Key.Name)]
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
                else if (ProgramModeBox.Text == "TagRecon")
                {
                    if (TRInstrumentList.SelectedIndex > 0)
                        currentConfig = _TRTemplateList[TRInstrumentList.SelectedIndex - 1];
                    else
                        MessageBox.Show("Cannot rename 'New'");
                }
                else
                {
                    if (PepInstrumentList.SelectedIndex > 0)
                        currentConfig = _pepTemplateList[PepInstrumentList.SelectedIndex - 1];
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
                else if (ProgramModeBox.Text == "TagRecon")
                {
                    if (TRInstrumentList.SelectedIndex > 0)
                        currentConfig = _TRTemplateList[TRInstrumentList.SelectedIndex - 1];
                    else
                        MessageBox.Show("Cannot delete'New'");
                }
                else
                {
                    if (PepInstrumentList.SelectedIndex > 0)
                        currentConfig = _pepTemplateList[PepInstrumentList.SelectedIndex - 1];
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
            var folderDialog = new OpenFileDialog { Filter = "Myrimatch Files(.db)|*.db|All files|*.*" };
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
            else if (ProgramModeBox.Text == "TagRecon")
                AppliedModDGV = TRAppliedModBox;
            else
                AppliedModDGV = PepAppliedModBox;

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

        

        private void MinSequenceMassBox_Leave(object sender, EventArgs e)
        {
            var MinSequenceMassBox = (NumericUpDown)sender;

            var MaxSequenceMassBox = ProgramModeBox.Text == "MyriMatch"
                                         ? MyriMaxPeptideMassBox
                                         : (ProgramModeBox.Text == "TagRecon"
                                                ? TRMaxPeptideMassBox
                                                : PepMaxPeptideMassBox);

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
                                         ? MyriMinPeptideMassBox
                                         : (ProgramModeBox.Text == "TagRecon"
                                                ? TRMinPeptideMassBox
                                                : PepMinPeptideMassBox);

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

        private void AdjustMassOption_CheckedChanged(object sender, EventArgs e)
        {
            bool itemChecked;
            Control item;
            if (sender == MyriAdjustMassOption)
            {
                MyriAdjustMassPanel.Enabled = MyriAdjustMassOption.Checked;
                item = MyriMonoisotopeAdjustmentSetBox;
                itemChecked = MyriAdjustMassOption.Checked;
            }
            else
            {
                PepAdjustMassPanel.Enabled = PepAdjustMassOption.Checked;
                item = PepMonoisotopeAdjustmentSetBox;
                itemChecked = PepAdjustMassOption.Checked;
            }

            if ((_defaults[item] == string.Empty) == (!itemChecked))
            {
                if (!_templateDefaults.ContainsKey(item)
                    || ((_templateDefaults[item] == string.Empty) == (!itemChecked)))
                    ((Control)sender).ForeColor = DefaultForeColor;
                else
                    ((Control)sender).ForeColor = Color.Blue;
            }
            else
            {
                if (!_templateDefaults.ContainsKey(item)
                    || ((_templateDefaults[item] == string.Empty) == (!itemChecked)))
                    ((Control)sender).ForeColor = Color.Green;
                else
                    ((Control)sender).ForeColor = Color.DarkViolet;
            }
        }


        public ConfigFile GetCometConfig()
        {
            if (!ProgramSelectComet.Checked)
                return null;
            var newConfig = new ConfigFile
                {
                    DestinationProgram = "Comet",
                    Name = "--Custom--",
                    FilePath = "--Custom--",
                    PropertyList = new List<ConfigProperty>()
                };
            var cometConfig = CometInstrumentBox.SelectedIndex == 0
                                  ? CometParams.GetIonTrapParams()
                                  : CometInstrumentBox.SelectedIndex == 0
                                        ? CometParams.GetTofParams()
                                        : CometParams.GetHighResParams();
            if (CometParams.CleavageAgentOptions.ContainsKey(MyriCleavageRulesBox.Text))
                cometConfig.CleavageAgent = CometParams.CleavageAgentOptions[MyriCleavageRulesBox.Text];
            foreach (DataGridViewRow row in MyriAppliedModBox.Rows)
            {
                if (row.Cells[MyriTypeColumn.Index].Value.ToString() == "Dynamic")
                    cometConfig.DynamicModifications.Add(
                        new CometParams.Modification(row.Cells[MyriMotifColumn.Index].Value.ToString(),
                                                     double.Parse(row.Cells[MyriMassColumn.Index].Value.ToString())));
                else if (row.Cells[MyriMotifColumn.Index].Value.ToString() == "C")
                    cometConfig.StaticCysteineMod = double.Parse(row.Cells[MyriMassColumn.Index].Value.ToString());
            }
            cometConfig.MaxMissedCleavages = (int)MyriMaxMissedCleavagesBox.Value;
            cometConfig.MaxMods = (int)MyriMaxDynamicModsBox.Value;
            cometConfig.OutputSuffix = CometOutputSuffixBox.Text;
            cometConfig.PrecursorTolerance = Double.Parse(MyriMonoPrecursorMzToleranceBox.Text);
            cometConfig.PrecursorUnit = MyriMonoPrecursorMzToleranceUnitsList.SelectedIndex == 0
                                            ? CometParams.PrecursorUnitOptions.Daltons
                                            : CometParams.PrecursorUnitOptions.PPM;
            cometConfig.Specificity = MyriMinTerminiCleavagesBox.SelectedIndex;
            newConfig.PropertyList.Add(new ConfigProperty
                {
                    Name = "config",
                    Type = "string",
                    ConfigAssociation = newConfig,
                    Value = CometHandler.CometParamsToFileContents(cometConfig)
                });
            return newConfig;
        }

        private void ProgramSelect_CheckedChanged(object sender, EventArgs e)
        {
            var multiSelect = ((ProgramSelectMyri.Checked ? 1 : 0) + (ProgramSelectComet.Checked ? 1 : 0) +
                               (ProgramSelectMSGF.Checked ? 1 : 0)) > 1;
            CometGB.Visible = ProgramSelectComet.Checked;
            MSGFGB.Visible = ProgramSelectMSGF.Checked;
            if (ProgramSelectMyri.Checked && multiSelect && string.IsNullOrEmpty(MyriOutputSuffixBox.Text))
                MyriOutputSuffixBox.Text = "_MM";
        }

        public ConfigFile GetMSGFConfig()
        {
            if (!ProgramSelectMSGF.Checked)
                return null;
            var newConfig = new ConfigFile
            {
                DestinationProgram = "MSGF",
                Name = "--Custom--",
                FilePath = "--Custom--",
                PropertyList = new List<ConfigProperty>()
            };
            var msgfConfig = new MSGFParams();
            if (MSGFParams.CleavageAgentOptions.ContainsKey(MyriCleavageRulesBox.Text))
                msgfConfig.CleavageAgent = MSGFParams.CleavageAgentOptions[MyriCleavageRulesBox.Text];
            //msgfConfig.FragmentationMethod = MSGFFragmentMethodBox.SelectedIndex;
            msgfConfig.Instrument = MSGFInstrumentBox.SelectedIndex;
            msgfConfig.OutputSuffix = MSGFOutputSuffixBox.Text;
            msgfConfig.PrecursorTolerance = Double.Parse(MyriMonoPrecursorMzToleranceBox.Text);
            msgfConfig.PrecursorToleranceUnits = MyriMonoPrecursorMzToleranceUnitsList.SelectedIndex == 0
                                            ? MSGFParams.PrecursorToleranceUnitOptions.Daltons
                                            : MSGFParams.PrecursorToleranceUnitOptions.PPM;
            msgfConfig.Protocol = MSGFPhosphoBox.Checked
                                      ? MSGFiTRAQBox.Checked
                                            ? MSGFParams.ProtocolOptions.iTRAQPhospho
                                            : MSGFParams.ProtocolOptions.Phosphorylation
                                      : MSGFiTRAQBox.Checked
                                            ? MSGFParams.ProtocolOptions.iTRAQ
                                            : MSGFParams.ProtocolOptions.NoProtocol;
            msgfConfig.Specificity = MyriMinTerminiCleavagesBox.SelectedIndex;

            var modList = new List<Util.Modification>();
            foreach (DataGridViewRow row in MyriAppliedModBox.Rows)
            {
                modList.Add(new Util.Modification
                    {
                        Mass = double.Parse(row.Cells[MyriMassColumn.Index].Value.ToString()),
                        Residue = row.Cells[MyriMotifColumn.Index].Value.ToString(),
                        Type = row.Cells[MyriTypeColumn.Index].Value.ToString()
                    });
            }

            newConfig.PropertyList.Add(new ConfigProperty
                {
                    Name = "config",
                    Type = "string",
                    ConfigAssociation = newConfig,
                    Value = MSGFHandler.MSGFParamsToOverload(msgfConfig)
                });
            newConfig.PropertyList.Add(new ConfigProperty
                {
                    Name = "mods",
                    Type = "string",
                    ConfigAssociation = newConfig,
                    Value = MSGFHandler.ModListToModString(modList, (int) MyriMaxDynamicModsBox.Value)
                });
            return newConfig;
        }
    }
}
