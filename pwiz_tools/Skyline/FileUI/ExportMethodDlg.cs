/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public sealed partial class ExportMethodDlg : Form
    {
        private readonly SrmDocument _document;
        private readonly ExportFileType _fileType;
        private string _instrumentType;
        private ExportStrategy _exportStrategy;
        private ExportMethodType _methodType;
        private string _optimizeType;
        private double _optimizeStepSize;
        private int _optimizeStepCount;
        private int _dwellTime;
        private double _runLength;
        private int? _maxTransitions;
        private bool _ignoreProteins;
        private bool _addEnergyRamp;    // Thermo scheduled only

        private static readonly string[] METHOD_TYPES =
            {
                ExportInstrumentType.Thermo_TSQ,
                ExportInstrumentType.Thermo_LTQ,
                ExportInstrumentType.Waters_Xevo,
                ExportInstrumentType.Waters_Quattro_Premier,
            };

        private static readonly string[] TRANSITION_LIST_TYPES =
            {
                ExportInstrumentType.ABI,
                ExportInstrumentType.Agilent,
                ExportInstrumentType.Thermo,
                ExportInstrumentType.Waters
            };

        public ExportMethodDlg(SrmDocument document, ExportFileType fileType)
        {
            InitializeComponent();

            _document = document;
            _fileType = fileType;

            string[] listTypes;
            if (_fileType == ExportFileType.Method)
                listTypes = METHOD_TYPES;
            else
            {
                Text = "Export Transition List";
                btnBrowseTemplate.Visible = false;
                labelTemplateFile.Visible = false;
                textTemplateFile.Visible = false;
                Height -= textTemplateFile.Bottom - comboTargetType.Bottom;

                listTypes = TRANSITION_LIST_TYPES;
            }

            comboInstrument.Items.Clear();
            foreach (string typeName in listTypes)
                comboInstrument.Items.Add(typeName);

            // Init dialog values from settings.
            try
            {
                ExportStrategy = (ExportStrategy)
                    Enum.Parse(typeof(ExportStrategy), Settings.Default.ExportMethodStrategy);
            }
            catch (ArgumentException)
            {
                ExportStrategy = ExportStrategy.Single;
            }

            IgnoreProteins = Settings.Default.ExportIgnoreProteins;

            try
            {
                ExportMethodType mType = (ExportMethodType)
                    Enum.Parse(typeof(ExportMethodType), Settings.Default.ExportMethodType);
                if (mType == ExportMethodType.Scheduled && !CanSchedule)
                    mType = ExportMethodType.Standard;

                MethodType = mType;
            }
            catch (ArgumentException)
            {
                MethodType = ExportMethodType.Standard;
            }

            // Instrument type may force method type to standard, so it must
            // be calculated after method type.
            string instrumentTypeName = document.Settings.TransitionSettings.Prediction.CollisionEnergy.Name;
            if (instrumentTypeName != null)
            {
                // Look for the first instrument type with the same prefix as the CE name
                string instrumentTypePrefix = instrumentTypeName.Split(' ')[0];
                var listTypePrefixes = new List<string>(listTypes).ConvertAll(t => t.Split(' ')[0]);
                int i = listTypePrefixes.IndexOf(instrumentTypePrefix);
                if (i != -1)
                    InstrumentType = listTypes[i];
            }
            if (InstrumentType == null)
                InstrumentType = listTypes[0];

            DwellTime = Settings.Default.ExportMethodDwellTime;
            RunLength = Settings.Default.ExportMethodRunLength;

            try
            {
                string maxTran = Settings.Default.ExportMethodMaxTran;
                if (!string.IsNullOrEmpty(maxTran))
                    MaxTransitions = int.Parse(maxTran);
            }
            catch (FormatException)
            {
                MaxTransitions = null;
            }

            cbEnergyRamp.Checked = Settings.Default.ExportThermoEnergyRamp;
            // Reposition from design layout
            cbEnergyRamp.Top = textDwellTime.Top + (textDwellTime.Height - cbEnergyRamp.Height)/2;

            // Add optimizable regressions
            comboOptimizing.Items.Add(ExportOptimize.NONE);
            comboOptimizing.Items.Add(ExportOptimize.CE);
            if (document.Settings.TransitionSettings.Prediction.DeclusteringPotential != null)
                comboOptimizing.Items.Add(ExportOptimize.DP);
            comboOptimizing.SelectedIndex = 0;
        }

        public string InstrumentType
        {
            get { return _instrumentType; }
            set
            {
                _instrumentType = value;

                // If scheduled method selected
                if (Equals(ExportMethodType.Scheduled.ToString().ToLower(),
                        comboTargetType.SelectedItem.ToString().ToLower()))
                {
                    // If single window state changing, and it is no longer possible to
                    // schedule, then switch back to a standard method.
                    if (MassListExporter.IsSingleWindowInstrumentType(_instrumentType) != IsSingleWindowInstrument && !CanSchedule)
                        MethodType = ExportMethodType.Standard;                        

                }
                comboInstrument.SelectedItem = _instrumentType;
            }
        }

        public bool IsSingleWindowInstrument
        {
            get { return MassListExporter.IsSingleWindowInstrumentType(InstrumentType); }
        }

        public bool IsSingleDwellInstrument
        {
            get { return IsSingleDwellInstrumentType(InstrumentType); }
        }

        private static bool IsSingleDwellInstrumentType(string type)
        {
            return Equals(type, ExportInstrumentType.Thermo) ||
                   Equals(type, ExportInstrumentType.Thermo_TSQ) ||
                   Equals(type, ExportInstrumentType.Thermo_LTQ) ||
                   Equals(type, ExportInstrumentType.Waters) ||
                   Equals(type, ExportInstrumentType.Waters_Xevo) ||
                   Equals(type, ExportInstrumentType.Waters_Quattro_Premier);
        }

        public bool IsAlwaysScheduledInstrument
        {
            get { return IsAlwaysScheduledInstrumentType(InstrumentType); }
        }

        private static bool IsAlwaysScheduledInstrumentType(string type)
        {
            return Equals(type, ExportInstrumentType.Thermo_TSQ) ||
                   Equals(type, ExportInstrumentType.Waters) ||
                   Equals(type, ExportInstrumentType.Waters_Xevo) ||
                   Equals(type, ExportInstrumentType.Waters_Quattro_Premier);
        }

        public bool CanScheduleInstrument
        {
            get { return CanScheduleInstrumentType(InstrumentType); }
        }

        private static bool CanScheduleInstrumentType(string type)
        {
            return !Equals(type, ExportInstrumentType.Thermo_LTQ);
        }

        private bool CanSchedule
        {
            get
            {
                return CanScheduleInstrument &&
                    _document.Settings.PeptideSettings.Prediction.CanSchedule(_document, IsSingleWindowInstrument);
            }
        }

        public ExportStrategy ExportStrategy
        {
            get { return _exportStrategy; }
            set
            {
                _exportStrategy = value;
                switch (_exportStrategy)
                {
                    case ExportStrategy.Single:
                        radioSingle.Checked = true;
                        break;
                    case ExportStrategy.Protein:
                        radioProtein.Checked = true;
                        break;
                    case ExportStrategy.Buckets:
                        radioBuckets.Checked = true;
                        break;
                }
            }
        }

        public string OptimizeType
        {
            get { return _optimizeType; }
            set
            {
                _optimizeType = value;
                comboOptimizing.SelectedItem = _optimizeType;
            }
        }

        public double OptimizeStepSize
        {
            get { return _optimizeStepSize; }
        }

        public int OptimizeStepCount
        {
            get { return _optimizeStepCount; }
        }

        public bool IgnoreProteins
        {
            get { return _ignoreProteins; }
            set
            {
                _ignoreProteins = value && ExportStrategy == ExportStrategy.Buckets;
                cbIgnoreProteins.Checked = _ignoreProteins;
            }
        }

        public bool AddEnergyRamp
        {
            get { return _addEnergyRamp; }
            set
            {
                _addEnergyRamp = cbEnergyRamp.Checked = value;
            }
        }

        private void UpdateEnergyRamp(bool standard)
        {
            cbEnergyRamp.Visible = !standard &&
                InstrumentType == ExportInstrumentType.Thermo;            
        }

        public ExportMethodType MethodType
        {
            get { return _methodType; }
            set
            {
                _methodType = value;
                comboTargetType.SelectedItem = _methodType.ToString();
            }
        }

        /// <summary>
        /// Specific dwell time in milliseconds for non-scheduled runs.
        /// </summary>
        public int DwellTime
        {
            get { return _dwellTime; }
            set
            {
                _dwellTime = value;
                textDwellTime.Text = _dwellTime.ToString();
            }
        }

        public double RunLength
        {
            get { return _runLength; }
            set
            {
                _runLength = value;
                textRunLength.Text = _runLength.ToString();
            }
        }

        public int? MaxTransitions
        {
            get { return _maxTransitions; }
            set
            {
                _maxTransitions = value;
                textMaxTransitions.Text = (_maxTransitions == null ?
                    "" : _maxTransitions.ToString());
            }
        }

        public void OkDialog(string outputPath)
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            _instrumentType = comboInstrument.SelectedItem.ToString();

            // Use variable for document to export, since code below may modify to document.
            SrmDocument documentExport = _document;

            string templateName = null;
            if (_fileType == ExportFileType.Method)
            {
                templateName = textTemplateFile.Text;
                if (string.IsNullOrEmpty(templateName))
                {
                    MessageDlg.Show(this, "A template file is required to export a method.");
                    return;
                }
                else if (!File.Exists(templateName))
                {
                    MessageDlg.Show(this, string.Format("The template file {0} does not exist.", templateName));
                    return;
                }
            }

            // Thermo LTQ method building ignores CE and DP regression values
            if (!Equals(InstrumentType, ExportInstrumentType.Thermo_LTQ))
            {
                // Check to make sure CE and DP match chosen instrument, and offer to use
                // the correct version for the instrument, if not.
                var predict = documentExport.Settings.TransitionSettings.Prediction;
                var ce = predict.CollisionEnergy;
                string ceName = (ce != null ? ce.Name : null);
                string ceNameDefault = _instrumentType;
                if (ceNameDefault.IndexOf(' ') != -1)
                    ceNameDefault = ceNameDefault.Substring(0, ceNameDefault.IndexOf(' '));
                bool ceInSynch = ceName != null && ceName.StartsWith(ceNameDefault);

                var dp = predict.DeclusteringPotential;
                string dpName = (dp != null ? dp.Name : null);
                string dpNameDefault = _instrumentType;
                if (dpNameDefault.IndexOf(' ') != -1)
                    dpNameDefault = dpNameDefault.Substring(0, dpNameDefault.IndexOf(' '));
                bool dpInSynch = true;
                if (_instrumentType == ExportInstrumentType.ABI)
                    dpInSynch = dpName != null && dpName.StartsWith(dpNameDefault);
                else
                    dpNameDefault = null; // Ignored for all other types

                if ((!ceInSynch && Settings.Default.CollisionEnergyList.ContainsKey(ceNameDefault)) ||
                    (!dpInSynch && Settings.Default.DeclusterPotentialList.ContainsKey(dpNameDefault)))
                {
                    var sb =
                        new StringBuilder("The settings for this document do not match the instrument type ").Append(
                            _instrumentType).Append(":\n\n");
                    if (!ceInSynch)
                        sb.Append("Collision Energy: ").Append(ceName).Append("\n");
                    if (!dpInSynch)
                        sb.Append("Declustering Potential: ").Append(dpName ?? "None").Append("\n");
                    sb.Append("\nWould you like to use the defaults instead?");
                    var result = MessageBox.Show(this, sb.ToString(), Program.Name, MessageBoxButtons.YesNoCancel);
                    if (result == DialogResult.Yes)
                    {
                        documentExport = ChangeInstrumentTypeSettings(documentExport, ceNameDefault, dpNameDefault);
                    }
                    if (result == DialogResult.Cancel)
                    {
                        comboInstrument.Focus();
                        e.Cancel = true;
                        return;
                    }
                }
            }

            // ReSharper disable ConvertIfStatementToConditionalTernaryExpression
            if (radioSingle.Checked)
                _exportStrategy = ExportStrategy.Single;
            else if (radioProtein.Checked)
                _exportStrategy = ExportStrategy.Protein;
            else
                _exportStrategy = ExportStrategy.Buckets;
            // ReSharper restore ConvertIfStatementToConditionalTernaryExpression

            _ignoreProteins = cbIgnoreProteins.Checked;
            _addEnergyRamp = cbEnergyRamp.Visible && cbEnergyRamp.Checked;

            _optimizeType = comboOptimizing.SelectedItem.ToString();
            var prediction = _document.Settings.TransitionSettings.Prediction;
            if (Equals(_optimizeType, ExportOptimize.NONE))
                _optimizeType = null;
            else if (Equals(_optimizeType, ExportOptimize.CE))
            {
                var regression = prediction.CollisionEnergy;
                _optimizeStepSize = regression.StepSize;
                _optimizeStepCount = regression.StepCount;
            }
            else if (Equals(_optimizeType, ExportOptimize.DP))
            {
                var regression = prediction.DeclusteringPotential;
                _optimizeStepSize = regression.StepSize;
                _optimizeStepCount = regression.StepCount;
            }

            string maxTran = textMaxTransitions.Text;
            if (string.IsNullOrEmpty(maxTran))
            {
                if (_exportStrategy == ExportStrategy.Buckets)
                {
                    helper.ShowTextBoxError(textMaxTransitions, "{0} must contain a value.");
                    return;
                }
                _maxTransitions = null;                
            }
            else
            {
                int maxVal;
                // CONSIDER: Better error message when instrument limitation encountered?
                int maxInstrumentTrans = documentExport.Settings.TransitionSettings.Instrument.MaxTransitions ??
                                         TransitionInstrument.MAX_TRANSITION_MAX;
                if (!helper.ValidateNumberTextBox(e, textMaxTransitions, 10, maxInstrumentTrans, out maxVal))
                    return;
                // Make sure all the precursors can fit into a single document
                if (!ValidatePrecursorFit(documentExport, maxVal))
                    return;
                _maxTransitions = maxVal;
            }

            _methodType = (ExportMethodType) Enum.Parse(typeof (ExportMethodType),
                                                        comboTargetType.SelectedItem.ToString());

            if (textDwellTime.Visible)
            {
                if (!helper.ValidateNumberTextBox(e, textDwellTime, 1, 1000, out _dwellTime))
                    return;
            }
            if (textRunLength.Visible)
            {
                if (!helper.ValidateDecimalTextBox(e, textRunLength, 5, 500, out _runLength))
                    return;
            }

            if (outputPath == null)
            {
                SaveFileDialog dlg = new SaveFileDialog
                {
                    Title = "Export Transition List",
                    InitialDirectory = Settings.Default.ExportDirectory,
                    OverwritePrompt = true,
                    DefaultExt = "csv",
                    Filter = string.Join("|", new[]
                    {
                        "Transition List (*.csv)|*.csv",
                        "All Files (*.*)|*.*"
                    })
                };

                if (_fileType != ExportFileType.List)
                {
                    dlg.Title = string.Format("Export {0} Method", _instrumentType);

                    if (Equals(_instrumentType, ExportInstrumentType.Thermo_TSQ) ||
                        Equals(_instrumentType, ExportInstrumentType.Thermo_LTQ))
                    {
                        dlg.DefaultExt = "meth";
                    }
                    else if (Equals(_instrumentType, ExportInstrumentType.Waters_Xevo) ||
                        Equals(_instrumentType, ExportInstrumentType.Waters_Quattro_Premier))
                    {
                        dlg.DefaultExt = "exp";
                    }
                    dlg.Filter = string.Join("|", new[]
                                     {
                                         string.Format("Method File (*.{0})|*.{0}", dlg.DefaultExt),
                                         "All Files (*.*)|*.*"
                                     });
                }

                if (dlg.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }

                outputPath = dlg.FileName;
            }

            Settings.Default.ExportDirectory = Path.GetDirectoryName(outputPath);
            try
            {
                switch (_instrumentType)
                {
                    case ExportInstrumentType.ABI:
                        ExportAbiCsv(documentExport, outputPath);
                        break;
                    case ExportInstrumentType.Agilent:
                        ExportAgilentCsv(documentExport, outputPath);
                        break;
                    case ExportInstrumentType.Thermo:
                    case ExportInstrumentType.Thermo_TSQ:
                        if (_fileType == ExportFileType.List)
                            ExportThermoCsv(documentExport, outputPath);
                        else
                            ExportThermoMethod(documentExport, outputPath, templateName);
                        break;
                    case ExportInstrumentType.Thermo_LTQ:
                        _optimizeType = null;
                        ExportThermoLtqMethod(documentExport, outputPath, templateName);
                        break;
                    case ExportInstrumentType.Waters:
                    case ExportInstrumentType.Waters_Xevo:
                        if (_fileType == ExportFileType.List)
                            ExportWatersCsv(documentExport, outputPath);
                        else
                            ExportWatersMethod(documentExport, outputPath, templateName);
                        break;
                    case ExportInstrumentType.Waters_Quattro_Premier:
                        ExportWatersQMethod(documentExport, outputPath, templateName);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (IOException x)
            {
                MessageBox.Show(this, x.Message, Program.Name);
                e.Cancel = true;
                return;
            }

            // Successfully completed dialog.  Store the values in settings.
            Settings.Default.ExportInstrumentType = _instrumentType;
            Settings.Default.ExportMethodStrategy = _exportStrategy.ToString();
            Settings.Default.ExportIgnoreProteins = _ignoreProteins;
            Settings.Default.ExportMethodMaxTran = (_maxTransitions != null ?
                _maxTransitions.ToString() : null);
            Settings.Default.ExportMethodType = _methodType.ToString();
            if (textDwellTime.Visible)
                Settings.Default.ExportMethodDwellTime = DwellTime;
            if (textRunLength.Visible)
                Settings.Default.ExportMethodRunLength = RunLength;
            if (cbEnergyRamp.Visible)
                Settings.Default.ExportThermoEnergyRamp = _addEnergyRamp;
            if (_fileType == ExportFileType.Method)
                Settings.Default.ExportMethodTemplateList.SetValue(new MethodTemplateFile(_instrumentType, templateName));

            DialogResult = DialogResult.OK;
            Close();
        }

        private bool ValidatePrecursorFit(SrmDocument document, int maxTransitions)
        {
            string messageFormat = (_optimizeType == null ?
                "The precursor {0} for the peptide {1} has {2} transitions, which exceeds the current maximum {3}." :
                "The precursor {0} for the peptide {1} requires {2} transitions to optimize, which exceeds the current maximum {3}.");
            foreach (var nodeGroup in document.TransitionGroups)
            {
                int tranRequired = nodeGroup.Children.Count;
                if (_optimizeType != null)
                    tranRequired *= _optimizeStepCount * 2 + 1;
                if (tranRequired > maxTransitions)
                {
                    MessageDlg.Show(this, string.Format(messageFormat,
                        SequenceMassCalc.PersistentMZ(nodeGroup.PrecursorMz) + Transition.GetChargeIndicator(nodeGroup.TransitionGroup.PrecursorCharge),
                        nodeGroup.TransitionGroup.Peptide.Sequence,
                        tranRequired,
                        maxTransitions));
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Changes collision energy and declustering potential settings to their
        /// default values for an instrument type.
        /// </summary>
        /// <param name="document">Document to change</param>
        /// <param name="ceNameDefault">Default name for CE</param>
        /// <param name="dpNameDefault">Default name for DP</param>
        private static SrmDocument ChangeInstrumentTypeSettings(SrmDocument document, string ceNameDefault, string dpNameDefault)
        {
            var ceList = Settings.Default.CollisionEnergyList;
            CollisionEnergyRegression ce;
            if (!ceList.TryGetValue(ceNameDefault, out ce))
            {
                foreach (var ceDefault in ceList.GetDefaults())
                {
                    if (Equals(ceNameDefault, ceDefault.Name))
                        ce = ceDefault;
                }
                Debug.Assert(ce != null);
            }
            var dpList = Settings.Default.DeclusterPotentialList;
            DeclusteringPotentialRegression dp = null;
            if (dpNameDefault != null && !dpList.TryGetValue(dpNameDefault, out dp))
            {
                foreach (var dpDefault in dpList.GetDefaults())
                {
                    if (Equals(dpNameDefault, dpDefault.Name))
                        dp = dpDefault;
                }
                Debug.Assert(dp != null);                
            }

            return document.ChangeSettings(document.Settings.ChangeTransitionPrediction(
                predict => predict.ChangeCollisionEnergy(ce).ChangeDeclusteringPotential(dp)));
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog(null);
        }

        private void ExportAbiCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new AbiMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            exporter.Export(fileName);
        }

        private void ExportAgilentCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new AgilentMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            exporter.Export(fileName);
        }

        private void ExportThermoCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new ThermoMassListExporter(document));
            exporter.AddEnergyRamp = AddEnergyRamp;
            exporter.Export(fileName);
        }

        private void ExportThermoMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));
        }

        private void ExportThermoLtqMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoLtqMethodExporter(document));

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));
        }

        private void ExportWatersCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new WatersMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            exporter.Export(fileName);
        }

        private void ExportWatersMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new WatersMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));
        }

        private void ExportWatersQMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new WatersMethodExporter(document)
                                            {
                                                MethodInstrumentType = ExportInstrumentType.Waters_Quattro_Premier
                                            });
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));
        }

        private void PerformLongExport(Action<IProgressMonitor> performExport)
        {
            var longWait = new LongWaitDlg { Text = "Exporting Methods" };
            try
            {
                var status = longWait.PerformWork(this, 800, performExport);
                if (status.IsError)
                    MessageBox.Show(this, status.ErrorException.Message);
            }
            catch (Exception x)
            {
                MessageBox.Show(this, string.Format("An error occurred attempting to export.\n{0}", x.Message), Program.Name);
            }
        }

        private T InitExporter<T>(T exporter)
            where T : MassListExporter
        {
            exporter.Strategy = ExportStrategy;
            exporter.IgnoreProteins = IgnoreProteins;
            exporter.MaxTransitions = MaxTransitions;
            exporter.MethodType = MethodType;
            exporter.OptimizeType = OptimizeType;
            exporter.OptimizeStepSize = OptimizeStepSize;
            exporter.OptimizeStepCount = OptimizeStepCount;
            return exporter;
        }

        private void radioSingle_CheckedChanged(object sender, EventArgs e)
        {
            StrategyCheckChanged();
        }

        private void radioProtein_CheckedChanged(object sender, EventArgs e)
        {
            StrategyCheckChanged();
        }

        private void radioBuckets_CheckedChanged(object sender, EventArgs e)
        {
            StrategyCheckChanged();
        }

        private void StrategyCheckChanged()
        {
            textMaxTransitions.Enabled = !radioSingle.Checked;
            cbIgnoreProteins.Enabled = radioBuckets.Checked;
            if (!radioBuckets.Checked)
                cbIgnoreProteins.Checked = false;
        }

        private void comboInstrument_SelectedIndexChanged(object sender, EventArgs e)
        {
            _instrumentType = comboInstrument.SelectedItem.ToString();

            bool standard = Equals(comboTargetType.SelectedItem.ToString(), ExportMethodType.Standard.ToString());
            if (!standard && !CanSchedule)
                comboTargetType.SelectedItem = ExportMethodType.Standard.ToString();
            comboTargetType.Enabled = CanScheduleInstrument;

            comboOptimizing.Enabled = !Equals(_instrumentType, ExportInstrumentType.Thermo_LTQ);

            UpdateDwellControls(standard);
            UpdateEnergyRamp(standard);

            MethodTemplateFile templateFile;
            if (Settings.Default.ExportMethodTemplateList.TryGetValue(_instrumentType, out templateFile))
                textTemplateFile.Text = templateFile.FilePath;
            else
                textTemplateFile.Text = "";
        }

        private void comboTargetType_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool standard = Equals(comboTargetType.SelectedItem.ToString(), ExportMethodType.Standard.ToString());
            if (!standard && !CanSchedule)
            {
                var prediction = _document.Settings.PeptideSettings.Prediction;
                if (prediction.RetentionTime == null)
                {
                    if (prediction.UseMeasuredRTs)
                        MessageBox.Show(this, "To export a scheduled list, you must first choose " +
                                        "a retention time regression in Transition Settings / Prediction, or " +
                                        "import results for all peptides in the document.", Program.Name);
                    else
                        MessageBox.Show(this, "To export a scheduled list, you must first choose " +
                                        "a retention time regression in Transition Settings / Prediction.", Program.Name);                    
                }
                else
                {
                    MessageBox.Show(this, "To export a scheduled list, you must first " +
                                    "import results for all peptides in the document.", Program.Name);
                }
                comboTargetType.SelectedItem = ExportMethodType.Standard.ToString();
                return;
            }

            UpdateDwellControls(standard);
            UpdateEnergyRamp(standard);
            if (standard)
                labelMaxTransitions.Text = "Ma&x transitions per sample injection:";
            else
                labelMaxTransitions.Text = "Ma&x concurrent transitions:";
        }

        private void UpdateDwellControls(bool standard)
        {
            bool showDwell = false;
            bool showRunLength = false;
            if (standard)
            {
                if (!IsSingleDwellInstrument)
                {
                    labelDwellTime.Text = "&Dwell time (ms):";
                    showDwell = true;
                }
                else if (IsAlwaysScheduledInstrument)
                {
                    labelDwellTime.Text = "Run &duration (min):";
                    showRunLength = true;                    
                }
            }
            labelDwellTime.Visible = showDwell || showRunLength;
            textDwellTime.Visible = showDwell;
            textRunLength.Visible = showRunLength;
        }

        private void btnBrowseTemplate_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Method Template",
                // Extension based on currently selecte type
                CheckPathExists = true
            };
            
            string templateName = textTemplateFile.Text;
            if (!string.IsNullOrEmpty(templateName))
            {
                try
                {
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(templateName);
                    openFileDialog.FileName = Path.GetFileName(templateName);
                }
                catch (ArgumentException) {} // Invalid characters
                catch (PathTooLongException) {}
            }

            var listFileTypes = new List<string>();
            if (Equals(InstrumentType, ExportInstrumentType.Thermo_TSQ) ||
                Equals(InstrumentType, ExportInstrumentType.Thermo_LTQ))
            {
                listFileTypes.Add(InstrumentType + " Method (*.meth)|*.meth");
            }
            else if (Equals(InstrumentType, ExportInstrumentType.Waters_Xevo) ||
                Equals(InstrumentType, ExportInstrumentType.Waters_Quattro_Premier))
            {
                listFileTypes.Add(InstrumentType + " Method (*.exp)|*.exp");
            }
            listFileTypes.Add("All Files (*.*)|*.*");
            openFileDialog.Filter = string.Join("|", listFileTypes.ToArray());

            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                textTemplateFile.Text = openFileDialog.FileName;
            }
        }
    }
}
