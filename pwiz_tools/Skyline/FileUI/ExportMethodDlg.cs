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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NHibernate;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public sealed partial class ExportMethodDlg : FormEx
    {
        public const string TRANS_PER_SAMPLE_INJ_TXT = "Ma&x transitions per sample injection:";
        public const string CONCUR_TRANS_TXT = "Ma&x concurrent transitions:";
        public const string PREC_PER_SAMPLE_INJ_TXT = "Ma&x precursors per sample injection:";
        public const string CONCUR_PREC_TXT = "Ma&x concurrent precursors:";
        public const string RUN_DURATION_TXT = "Run &duration (min):";
        public const string DWELL_TIME_TXT = "&Dwell time (ms):";

        public const string SCHED_NOT_SUPPORTED_ERR_TXT = "Scheduled methods are not supported for the selected instrument.";

        private readonly SrmDocument _document;
        private readonly ExportFileType _fileType;
        private string _instrumentType;

        private readonly ExportDlgProperties _exportProperties;

        public ExportMethodDlg(SrmDocument document, ExportFileType fileType)
        {
            InitializeComponent();

            _exportProperties = new ExportDlgProperties(this);

            _document = document;
            _fileType = fileType;

            string[] listTypes;
            if (_fileType == ExportFileType.Method)
                listTypes = ExportInstrumentType.METHOD_TYPES;
            else
            {
                if (_fileType == ExportFileType.List)
                {
                    Text = "Export Transition List";
                    listTypes = ExportInstrumentType.TRANSITION_LIST_TYPES;
                }
                else
                {
                    Text = "Export Isolation List";
                    listTypes = ExportInstrumentType.ISOLATION_LIST_TYPES;
                    _exportProperties.MultiplexIsolationListCalculationTime = 20;   // Default 20 seconds to search for good multiplexed window ordering.
                }
                
                btnBrowseTemplate.Visible = false;
                labelTemplateFile.Visible = false;
                textTemplateFile.Visible = false;
                Height -= textTemplateFile.Bottom - comboTargetType.Bottom;
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
                int i = -1;
                if (document.Settings.TransitionSettings.FullScan.IsEnabled)
                {
                    i = listTypes.IndexOf(typeName => typeName.StartsWith(instrumentTypePrefix) &&
                        ExportInstrumentType.IsFullScanInstrumentType(typeName));
                }
                if (i == -1)
                {
                    i = listTypes.IndexOf(typeName => typeName.StartsWith(instrumentTypePrefix));
                }
                if (i != -1)
                    InstrumentType = listTypes[i];
            }
            if (InstrumentType == null)
                InstrumentType = listTypes[0];

            DwellTime = Settings.Default.ExportMethodDwellTime;
            RunLength = Settings.Default.ExportMethodRunLength;

            UpdateMaxTransitions();

            cbEnergyRamp.Checked = Settings.Default.ExportThermoEnergyRamp;
            cbTriggerRefColumns.Checked = Settings.Default.ExportThermoTriggerRef;
            cbExportMultiQuant.Checked = Settings.Default.ExportMultiQuant;
            // Reposition from design layout
            panelThermoColumns.Top = labelDwellTime.Top;
            panelAbSciexTOF.Top = textDwellTime.Top + (textDwellTime.Height - panelAbSciexTOF.Height)/2;


            // Add optimizable regressions
            comboOptimizing.Items.Add(ExportOptimize.NONE);
            comboOptimizing.Items.Add(ExportOptimize.CE);
            if (document.Settings.TransitionSettings.Prediction.DeclusteringPotential != null)
                comboOptimizing.Items.Add(ExportOptimize.DP);
            comboOptimizing.SelectedIndex = 0;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            _recalcMethodCountStatus = RecalcMethodCountStatus.waiting;
            CalcMethodCount();

            base.OnHandleCreated(e);
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
                    if (ExportInstrumentType.IsSingleWindowInstrumentType(_instrumentType) != IsSingleWindowInstrument && !CanSchedule)
                        MethodType = ExportMethodType.Standard;                        

                }
                comboInstrument.SelectedItem = _instrumentType;
            }
        }

        public bool IsSingleWindowInstrument
        {
            get { return ExportInstrumentType.IsSingleWindowInstrumentType(InstrumentType); }
        }

        public bool IsInclusionListMethod
        {
            get { return IsFullScanInstrument && ExportInstrumentType.IsInclusionListMethod(_document); }
        }

        public bool IsSingleDwellInstrument
        {
            get { return IsSingleDwellInstrumentType(InstrumentType); }
        }

        private static bool IsSingleDwellInstrumentType(string type)
        {
            return Equals(type, ExportInstrumentType.AGILENT_TOF) ||
                   Equals(type, ExportInstrumentType.THERMO) ||
                   Equals(type, ExportInstrumentType.THERMO_TSQ) ||
                   Equals(type, ExportInstrumentType.THERMO_LTQ) ||
                   Equals(type, ExportInstrumentType.THERMO_Q_EXACTIVE) ||
                   Equals(type, ExportInstrumentType.WATERS) ||
                   Equals(type, ExportInstrumentType.WATERS_XEVO) ||
                   Equals(type, ExportInstrumentType.WATERS_QUATTRO_PREMIER) ||
                   // For AbSciex's TOF 5600 and QSTAR instruments, the dwell (accumulation) time
                   // given in the template method is used. So we will not display the 
                   // "Dwell Time" text box.
                   Equals(type, ExportInstrumentType.ABI_TOF);
        }

        private bool IsAlwaysScheduledInstrument
        {
            get
            {
                if (!CanScheduleInstrumentType)
                    return false;

                var type = InstrumentType;
                return Equals(type, ExportInstrumentType.THERMO_TSQ) ||
                       Equals(type, ExportInstrumentType.WATERS) ||
                       Equals(type, ExportInstrumentType.WATERS_XEVO) ||
                       Equals(type, ExportInstrumentType.WATERS_QUATTRO_PREMIER) ||
                       // LTQ can only schedule for inclusion lists, but then it always
                       // requires start and stop times.
                       Equals(type, ExportInstrumentType.THERMO_LTQ);
                       // This will only happen for ABI TOF with inclusion lists, since
                       // MRM-HR cannot yet be scheduled, and ABI TOF can either be scheduled
                       // or unscheduled when exporting inclusion lists.
//                       Equals(type, ExportInstrumentType.ABI_TOF); 
            }
        }

        private bool CanScheduleInstrumentType
        {
            get { return ExportInstrumentType.CanScheduleInstrumentType(InstrumentType, _document); }
        }

        private bool CanSchedule
        {
            get { return ExportInstrumentType.CanSchedule(InstrumentType, _document); }
        }

        private ExportSchedulingAlgorithm SchedulingAlgorithm
        {
            set { _exportProperties.SchedulingAlgorithm = value; }
        }

        private int? SchedulingReplicateNum
        {
            set { _exportProperties.SchedulingReplicateNum = value ?? 0; }
        }

        public bool IsFullScanInstrument
        {
            get { return ExportInstrumentType.IsFullScanInstrumentType(InstrumentType);  }
        }

        public bool IsPrecursorOnlyInstrument
        {
            get { return ExportInstrumentType.IsPrecursorOnlyInstrumentType(InstrumentType); }
        }

        public ExportStrategy ExportStrategy
        {
            get { return _exportProperties.ExportStrategy; }
            set
            {
                _exportProperties.ExportStrategy = value;

                switch (_exportProperties.ExportStrategy)
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
            get { return _exportProperties.OptimizeType; }
            set
            {
                _exportProperties.OptimizeType = value;
                comboOptimizing.SelectedItem = _exportProperties.OptimizeType;
            }
        }

        public double OptimizeStepSize
        {
            get { return _exportProperties.OptimizeStepSize; }
            set
            {
                _exportProperties.OptimizeStepSize = value;
            }
        }

        public int OptimizeStepCount
        {
            get { return _exportProperties.OptimizeStepCount; }
            set
            {
                _exportProperties.OptimizeStepCount = value;
            }
        }

        public bool IgnoreProteins
        {
            get { return _exportProperties.IgnoreProteins; }
            set
            {
                _exportProperties.IgnoreProteins = value && ExportStrategy == ExportStrategy.Buckets;
                cbIgnoreProteins.Checked = _exportProperties.IgnoreProteins;
            }
        }

        public bool AddEnergyRamp
        {
            get { return _exportProperties.AddEnergyRamp; }
            set
            {
                _exportProperties.AddEnergyRamp = cbEnergyRamp.Checked = value;
            }
        }

        public bool AddTriggerReference
        {
            get { return _exportProperties.AddTriggerReference; }
            set
            {
                _exportProperties.AddTriggerReference = cbTriggerRefColumns.Checked = value;
            }
        }

        public bool ExportMultiQuant
        {
            get { return _exportProperties.ExportMultiQuant; }
            set { _exportProperties.ExportMultiQuant = cbExportMultiQuant.Checked = value; }
        }

        private void UpdateThermoColumns(bool standard)
        {
            panelThermoColumns.Visible = !standard && InstrumentType == ExportInstrumentType.THERMO;
        }

        private void UpdateAbSciexControls()
        {
            panelAbSciexTOF.Visible = InstrumentType == ExportInstrumentType.ABI_TOF;
        }

        private void UpdateMaxTransitions()
        {
            try
            {
                string maxTran = IsFullScanInstrument
                                     ? Settings.Default.ExportMethodMaxPrec
                                     : Settings.Default.ExportMethodMaxTran;
                if (string.IsNullOrEmpty(maxTran))
                    MaxTransitions = null;
                else
                    MaxTransitions = int.Parse(maxTran, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                MaxTransitions = null;
            }
        }

        public ExportMethodType MethodType
        {
            get { return _exportProperties.MethodType; }
            set
            {
                _exportProperties.MethodType = value;
                comboTargetType.SelectedItem = _exportProperties.MethodType.ToString();
            }
        }

        /// <summary>
        /// Specific dwell time in milliseconds for non-scheduled runs.
        /// </summary>
        public int DwellTime
        {
            get { return _exportProperties.DwellTime; }
            set
            {
                _exportProperties.DwellTime = value;
                textDwellTime.Text = _exportProperties.DwellTime.ToString(CultureInfo.CurrentCulture);
            }
        }

        public double RunLength
        {
            get { return _exportProperties.RunLength; }
            set
            {
                _exportProperties.RunLength = value;
                textRunLength.Text = _exportProperties.RunLength.ToString(CultureInfo.CurrentCulture);
            }
        }

        public int? MaxTransitions
        {
            get { return _exportProperties.MaxTransitions; }
            set
            {
                _exportProperties.MaxTransitions = value;
                textMaxTransitions.Text = (_exportProperties.MaxTransitions.HasValue
                    ? _exportProperties.MaxTransitions.Value.ToString(CultureInfo.CurrentCulture)
                    : "");
            }
        }

        public void OkDialog()
        {
            OkDialog(null);
        }

        public void OkDialog(string outputPath)
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this, true);

            _instrumentType = comboInstrument.SelectedItem.ToString();

            // Use variable for document to export, since code below may modify the document.
            SrmDocument documentExport = _document;

            string templateName = null;
            if (_fileType == ExportFileType.Method)
            {
                // Check for instruments that cannot do DIA.
                if (IsDia)
                {
                    if (Equals(InstrumentType, ExportInstrumentType.AGILENT_TOF) ||
                        Equals(InstrumentType, ExportInstrumentType.ABI_TOF) ||
                        Equals(InstrumentType, ExportInstrumentType.THERMO_LTQ))
                    {
                        helper.ShowTextBoxError(textTemplateFile, "Export of DIA method is not supported for {0}.", InstrumentType);
                        return;
                    }
                }

                templateName = textTemplateFile.Text;
                if (string.IsNullOrEmpty(templateName))
                {
                    helper.ShowTextBoxError(textTemplateFile, "A template file is required to export a method.");
                    return;
                }
                if (Equals(InstrumentType, ExportInstrumentType.AGILENT6400) ?
                                                                                 !Directory.Exists(templateName) : !File.Exists(templateName))
                {
                    helper.ShowTextBoxError(textTemplateFile, "The template file {0} does not exist.", templateName);
                    return;
                }
                if (Equals(InstrumentType, ExportInstrumentType.AGILENT6400) &&
                    !AgilentMethodExporter.IsAgilentMethodPath(templateName))
                {
                    helper.ShowTextBoxError(textTemplateFile, "The folder {0} does not appear to contain an Agilent QQQ method template.  The folder is expected to have a .m extension, and contain the file qqqacqmethod.xsd.", templateName);
                    return;
                }
            }

            if (Equals(InstrumentType, ExportInstrumentType.AGILENT_TOF) ||
                Equals(InstrumentType, ExportInstrumentType.ABI_TOF))
            {
                // Check that mass analyzer settings are set to TOF.
                if (documentExport.Settings.TransitionSettings.FullScan.IsEnabledMs && 
                    documentExport.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer != FullScanMassAnalyzerType.tof)
                {
                    MessageDlg.Show(this,
                        "The precursor mass analyzer type is not set to TOF in Transition Settings (under the Full Scan tab).");
                    return;
                }
                if (documentExport.Settings.TransitionSettings.FullScan.IsEnabledMsMs &&
                    documentExport.Settings.TransitionSettings.FullScan.ProductMassAnalyzer != FullScanMassAnalyzerType.tof)
                {
                    MessageDlg.Show(this,
                        "The product mass analyzer type is not set to TOF in Transition Settings (under the Full Scan tab).");
                    return;
                }                    
            }

            if (Equals(InstrumentType, ExportInstrumentType.THERMO_Q_EXACTIVE))
            {
                // Check that mass analyzer settings are set to Orbitrap.
                if (documentExport.Settings.TransitionSettings.FullScan.IsEnabledMs &&
                    documentExport.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer != FullScanMassAnalyzerType.orbitrap)
                {
                    MessageDlg.Show(this,
                        "The precursor mass analyzer type is not set to Orbitrap in Transition Settings (under the Full Scan tab).");
                    return;
                }
                if (documentExport.Settings.TransitionSettings.FullScan.IsEnabledMsMs &&
                    documentExport.Settings.TransitionSettings.FullScan.ProductMassAnalyzer != FullScanMassAnalyzerType.orbitrap)
                {
                    MessageDlg.Show(this,
                        "The product mass analyzer type is not set to Orbitrap in Transition Settings (under the Full Scan tab).");
                    return;
                }                    
            }

            if (!documentExport.HasAllRetentionTimeStandards())
            {
                using (var messageDlg = new MultiButtonMsgDlg("The document does not contain all of the retention time standard peptides.\nYou will not be able to use retention time prediction with acquired results.\nAre you sure you want to continue?", "OK"))
                {
                    if (messageDlg.ShowDialog(this) == DialogResult.Cancel)
                        return;
                }
            }

            // Full-scan method building ignores CE and DP regression values
            if (!ExportInstrumentType.IsFullScanInstrumentType(InstrumentType))
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

                if ((!ceInSynch && Settings.Default.CollisionEnergyList.Keys.Any(name => name.StartsWith(ceNameDefault)) ||
                    (!dpInSynch && Settings.Default.DeclusterPotentialList.Keys.Any(name => name.StartsWith(dpNameDefault)))))
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

            //This will populate _exportProperties
            if(!ValidateSettings(e, helper))
            {
                return;
            }

            if (outputPath == null)
            {
                string title = Text;
                string ext = "csv";
                string filter = "Method File";

                switch (_fileType)
                {
                    case ExportFileType.List:
                        filter = "Transition List";
                        break;

                    case ExportFileType.IsolationList:
                        filter = "Isolation List";
                        break;

                    case ExportFileType.Method:
                        title = string.Format("Export {0} Method", _instrumentType);
                        ext = ExportInstrumentType.MethodExtension(_instrumentType);
                        break;
                }

                SaveFileDialog dlg = new SaveFileDialog
                {
                    Title = title,
                    InitialDirectory = Settings.Default.ExportDirectory,
                    OverwritePrompt = true,
                    DefaultExt = ext,
                    Filter = string.Join("|", new[]
                    {
                        string.Format("{0} (*.{1})|*.{1}", filter, ext),
                        "All Files (*.*)|*.*"
                    })
                };

                if (dlg.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }

                outputPath = dlg.FileName;
            }

            Settings.Default.ExportDirectory = Path.GetDirectoryName(outputPath);

            // Set ShowMessages property on ExportDlgProperties to true
            // so that we see the progress dialog during the export process
            var wasShowMessageValue = _exportProperties.ShowMessages;
            _exportProperties.ShowMessages = true;
            try
            {
                _exportProperties.ExportFile(_instrumentType, _fileType, outputPath, documentExport, templateName);
            }
            catch (IOException x)
            {
                MessageBox.Show(this, x.Message, Program.Name);
                e.Cancel = true;
                _exportProperties.ShowMessages = wasShowMessageValue;
                return;
            }

            // Successfully completed dialog.  Store the values in settings.
            Settings.Default.ExportInstrumentType = _instrumentType;
            Settings.Default.ExportMethodStrategy = ExportStrategy.ToString();
            Settings.Default.ExportIgnoreProteins = IgnoreProteins;
            if (IsFullScanInstrument)
            {
                Settings.Default.ExportMethodMaxPrec = (MaxTransitions.HasValue ?
                    MaxTransitions.Value.ToString(CultureInfo.InvariantCulture) : null);                
            }
            else
            {
                Settings.Default.ExportMethodMaxTran = (MaxTransitions.HasValue ?
                    MaxTransitions.Value.ToString(CultureInfo.InvariantCulture) : null);
            }
            Settings.Default.ExportMethodType = _exportProperties.MethodType.ToString();
            if (textDwellTime.Visible)
                Settings.Default.ExportMethodDwellTime = DwellTime;
            if (textRunLength.Visible)
                Settings.Default.ExportMethodRunLength = RunLength;
            if (panelThermoColumns.Visible)
            {
                Settings.Default.ExportThermoEnergyRamp = AddEnergyRamp;
                Settings.Default.ExportThermoTriggerRef = AddTriggerReference;
            }
            if (_fileType == ExportFileType.Method)
                Settings.Default.ExportMethodTemplateList.SetValue(new MethodTemplateFile(_instrumentType, templateName));
            if (cbExportMultiQuant.Visible)
                Settings.Default.ExportMultiQuant = ExportMultiQuant;

            DialogResult = DialogResult.OK;
            Close();
        }


        /// <summary>
        /// This function will validate all the settings required for exporting a method,
        /// placing the values on the ExportDlgProperties _exportProperties. It returns
        /// boolean whether or not it succeeded. It can show MessageBoxes or not based
        /// on a parameter.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="helper"></param>
        /// <returns></returns>
        public bool ValidateSettings(CancelEventArgs e, MessageBoxHelper helper)
        {
            // ReSharper disable ConvertIfStatementToConditionalTernaryExpression
            if (radioSingle.Checked)
                _exportProperties.ExportStrategy = ExportStrategy.Single;
            else if (radioProtein.Checked)
                _exportProperties.ExportStrategy = ExportStrategy.Protein;
            else
                _exportProperties.ExportStrategy = ExportStrategy.Buckets;
            // ReSharper restore ConvertIfStatementToConditionalTernaryExpression

            _exportProperties.IgnoreProteins = cbIgnoreProteins.Checked;
            _exportProperties.FullScans = _document.Settings.TransitionSettings.FullScan.IsEnabledMsMs;
            _exportProperties.AddEnergyRamp = panelThermoColumns.Visible && cbEnergyRamp.Checked;
            _exportProperties.AddTriggerReference = panelThermoColumns.Visible && cbTriggerRefColumns.Checked;

            _exportProperties.ExportMultiQuant = panelAbSciexTOF.Visible && cbExportMultiQuant.Checked;

            _exportProperties.Ms1Scan = _document.Settings.TransitionSettings.FullScan.IsEnabledMs &&
                                        _document.Settings.TransitionSettings.FullScan.IsEnabledMsMs;

            _exportProperties.InclusionList = IsInclusionListMethod;

            _exportProperties.MsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _document.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer);
            _exportProperties.MsMsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _document.Settings.TransitionSettings.FullScan.ProductMassAnalyzer);

            _exportProperties.OptimizeType = comboOptimizing.SelectedItem == null ? ExportOptimize.NONE : comboOptimizing.SelectedItem.ToString();
            var prediction = _document.Settings.TransitionSettings.Prediction;
            if (Equals(_exportProperties.OptimizeType, ExportOptimize.CE))
            {
                var regression = prediction.CollisionEnergy;
                _exportProperties.OptimizeStepSize = regression.StepSize;
                _exportProperties.OptimizeStepCount = regression.StepCount;
            }
            else if (Equals(_exportProperties.OptimizeType, ExportOptimize.DP))
            {
                var regression = prediction.DeclusteringPotential;
                _exportProperties.OptimizeStepSize = regression.StepSize;
                _exportProperties.OptimizeStepCount = regression.StepCount;
            }
            else
            {
                _exportProperties.OptimizeType = null;
                _exportProperties.OptimizeStepSize = _exportProperties.OptimizeStepCount = 0;
            }

            string maxTran = textMaxTransitions.Text;
            if (string.IsNullOrEmpty(maxTran))
            {
                if (_exportProperties.ExportStrategy == ExportStrategy.Buckets)
                {
                    helper.ShowTextBoxError(textMaxTransitions, "{0} must contain a value.");
                    return false;
                }
                _exportProperties.MaxTransitions = null;
            }

            int maxVal;
            // CONSIDER: Better error message when instrument limitation encountered?
            int maxInstrumentTrans = _document.Settings.TransitionSettings.Instrument.MaxTransitions ??
                                        TransitionInstrument.MAX_TRANSITION_MAX;
            int minTrans = IsFullScanInstrument
                               ? AbstractMassListExporter.MAX_TRANS_PER_INJ_MIN
                               : MethodExporter.MAX_TRANS_PER_INJ_MIN_TLTQ;

            if (_exportProperties.ExportStrategy != ExportStrategy.Buckets)
                maxVal = maxInstrumentTrans;
            else if (!helper.ValidateNumberTextBox(e, textMaxTransitions, minTrans, maxInstrumentTrans, out maxVal))
                return false;

            // Make sure all the transitions of all precursors can fit into a single document,
            // but not if this is a full-scan instrument, because then the maximum is refering
            // to precursors and not transitions.
            if (!IsFullScanInstrument && !ValidatePrecursorFit(_document, maxVal, helper.ShowMessages))
                return false;
            _exportProperties.MaxTransitions = maxVal;

            _exportProperties.MethodType = (ExportMethodType)Enum.Parse(typeof(ExportMethodType),
                                                        comboTargetType.SelectedItem.ToString());

            if (textDwellTime.Visible)
            {
                int dwellTime;
                if (!helper.ValidateNumberTextBox(e, textDwellTime, AbstractMassListExporter.DWELL_TIME_MIN, AbstractMassListExporter.DWELL_TIME_MAX, out dwellTime))
                    return false;

                _exportProperties.DwellTime = dwellTime;
            }
            if (textRunLength.Visible)
            {
                double runLength;
                if (!helper.ValidateDecimalTextBox(e, textRunLength, AbstractMassListExporter.RUN_LENGTH_MIN, AbstractMassListExporter.RUN_LENGTH_MAX, out runLength))
                    return false;

                _exportProperties.RunLength = runLength;
            }

            // If export method type is scheduled, and allows multiple scheduling options
            // ask the user which to use.
            if (_exportProperties.MethodType == ExportMethodType.Scheduled && HasMultipleSchedulingOptions(_document))
            {
                if (!helper.ShowMessages)
                {
                    // CONSIDER: Kind of a hack, but pick some reasonable defaults.  The user
                    //           may decide otherwise later, but this is the best we can do
                    //           without asking.
                    if (!_document.Settings.HasResults || Settings.Default.ScheduleAvergeRT)
                        SchedulingAlgorithm = ExportSchedulingAlgorithm.Average;
                    else
                    {
                        SchedulingAlgorithm = ExportSchedulingAlgorithm.Single;
                        SchedulingReplicateNum = _document.Settings.MeasuredResults.Chromatograms.Count - 1;
                    }
                }
                else
                {
                    using (SchedulingOptionsDlg schedulingOptionsDlg = new SchedulingOptionsDlg(_document))
                    {
                        if (schedulingOptionsDlg.ShowDialog(this) != DialogResult.OK)
                            return false;

                        SchedulingAlgorithm = schedulingOptionsDlg.Algorithm;
                        SchedulingReplicateNum = schedulingOptionsDlg.ReplicateNum;
                    }
                }
            }

            return true;
        }

        private static bool HasMultipleSchedulingOptions(SrmDocument document)
        {
            // No scheduling from data, if no data is present
            if (!document.Settings.HasResults || !document.Settings.PeptideSettings.Prediction.UseMeasuredRTs)
                return false;

            // If multipe non-optimization data sets are present, allow user to choose.
            var chromatagrams = document.Settings.MeasuredResults.Chromatograms;
            int sched = chromatagrams.Count(chromatogramSet => chromatogramSet.OptimizationFunction == null);

            if (sched > 1)
                return true;
            // Otherwise, if no non-optimization data is present, but multiple optimization
            // sets are available, allow user to choose from them.
            return (sched == 0 && chromatagrams.Count > 1);
        }

        private bool ValidatePrecursorFit(SrmDocument document, int maxTransitions, bool showMessages)
        {
            string messageFormat = (OptimizeType == null ?
                "The precursor {0} for the peptide {1} has {2} transitions, which exceeds the current maximum {3}." :
                "The precursor {0} for the peptide {1} requires {2} transitions to optimize, which exceeds the current maximum {3}.");
            foreach (var nodeGroup in document.TransitionGroups)
            {
                int tranRequired = nodeGroup.Children.Count;
                if (OptimizeType != null)
                    tranRequired *= OptimizeStepCount * 2 + 1;
                if (tranRequired > maxTransitions)
                {
                    if (showMessages)
                    {
                        MessageDlg.Show(this, string.Format(messageFormat,
                            SequenceMassCalc.PersistentMZ(nodeGroup.PrecursorMz) + Transition.GetChargeIndicator(nodeGroup.TransitionGroup.PrecursorCharge),
                            nodeGroup.TransitionGroup.Peptide.Sequence,
                            tranRequired,
                            maxTransitions));
                    }
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
                    if (ceDefault.Name.StartsWith(ceNameDefault))
                        ce = ceDefault;
                }
            }
            var dpList = Settings.Default.DeclusterPotentialList;
            DeclusteringPotentialRegression dp = null;
            if (dpNameDefault != null && !dpList.TryGetValue(dpNameDefault, out dp))
            {
                foreach (var dpDefault in dpList.GetDefaults())
                {
                    if (dpDefault.Name.StartsWith(dpNameDefault))
                        dp = dpDefault;
                }
            }

            return document.ChangeSettings(document.Settings.ChangeTransitionPrediction(
                predict =>
                    {
                        if (ce != null)
                            predict = predict.ChangeCollisionEnergy(ce);
                        if (dp != null)
                            predict = predict.ChangeDeclusteringPotential(dp);
                        return predict;
                    }));
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog(null);
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

        private bool IsDia
        {
            get
            {
                return IsFullScanInstrument &&
                    _document.Settings.TransitionSettings.FullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA;
            }
        }

        private void StrategyCheckChanged()
        {
            if (IsDia && !radioSingle.Checked)
            {
                MessageDlg.Show(this, "Only one method can be exported in DIA mode.");
                radioSingle.Checked = true;
            }

            textMaxTransitions.Enabled = !radioSingle.Checked;
            cbIgnoreProteins.Enabled = radioBuckets.Checked;
            if (!radioBuckets.Checked)
                cbIgnoreProteins.Checked = false;

            if (radioSingle.Checked)
            {
                labelMethodNum.Text = "1";
            }
            else
            {
                CalcMethodCount();
            }
        }

        private void comboInstrument_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool wasFullScanInstrument = IsFullScanInstrument;

            _instrumentType = comboInstrument.SelectedItem.ToString();

            // Temporary code until we support Agilent export of DIA isolation lists.
            if (Equals(_instrumentType, ExportInstrumentType.AGILENT_TOF) && IsDia)
            {
                MessageDlg.Show(this, string.Format("Export of DIA isolation lists is not yet supported for {0}.", _instrumentType));
                comboInstrument.SelectedItem = ExportInstrumentType.THERMO_Q_EXACTIVE;
                return;
            }

            bool standard = Equals(comboTargetType.SelectedItem.ToString(), ExportMethodType.Standard.ToString());
            if (!standard && !CanSchedule)
            {
                comboTargetType.SelectedItem = ExportMethodType.Standard.ToString();
                return;
            }                

            // Always keep the comboTargetType (Method type) enabled. Throw and error if the 
            // user selects "Scheduled" and it is not supported by the instrument.
            // comboTargetType.Enabled = CanScheduleInstrumentType;
            
            comboOptimizing.Enabled = !IsFullScanInstrument;

            UpdateDwellControls(standard);
            UpdateThermoColumns(standard);
            UpdateAbSciexControls();
            UpdateMaxLabel(standard);
            if (wasFullScanInstrument != IsFullScanInstrument)
                UpdateMaxTransitions();

            MethodTemplateFile templateFile;
            textTemplateFile.Text = Settings.Default.ExportMethodTemplateList.TryGetValue(_instrumentType, out templateFile)
                ? templateFile.FilePath
                : "";

            CalcMethodCount();
        }

        private void comboTargetType_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool standard = Equals(comboTargetType.SelectedItem.ToString(), ExportMethodType.Standard.ToString());
            if (!standard && IsDia)
            {
                MessageDlg.Show(this, "Scheduled methods are not yet supported for DIA acquisition.");
                comboTargetType.SelectedItem = ExportMethodType.Standard.ToString();
                return;
            }
            if (!standard && !CanSchedule)
            {
                var prediction = _document.Settings.PeptideSettings.Prediction;

                // The "Method type" combo box is always enabled.  Display error message if the user
                // selects "Scheduled" for an instrument that does not support scheduled methods (e.g LTQ, ABI TOF)
                // However, if we are exporting inclusion lists (MS1 filtering enabled AND MS2 filtering disabled) 
                // the user should be able to select "Scheduled" for LTQ and ABI TOF instruments.
                if(!CanScheduleInstrumentType)
                {
                    MessageDlg.Show(this, SCHED_NOT_SUPPORTED_ERR_TXT);    
                }
                else if (prediction.RetentionTime == null)
                {
                    if (prediction.UseMeasuredRTs)
                        MessageDlg.Show(this, "To export a scheduled list, you must first choose " +
                                        "a retention time predictor in Peptide Settings / Prediction, or " +
                                        "import results for all peptides in the document.");
                    else
                        MessageDlg.Show(this, "To export a scheduled list, you must first choose " +
                                        "a retention time predictor in Peptide Settings / Prediction.");                    
                }
                else if (!prediction.RetentionTime.Calculator.IsUsable)
                {
                    MessageDlg.Show(this, "Retention time prediction calculator is unable to score.\n" +
                        "Check the calculator settings.");
                }
                else if (!prediction.RetentionTime.IsUsable)
                {
                    MessageDlg.Show(this, "Retention time predictor is unable to auto-calculate a regression.\n" +
                        "Check to make sure the document contains times for all of the required standard peptides.");
                }
                else
                {
                    MessageDlg.Show(this, "To export a scheduled list, you must first " +
                                    "import results for all peptides in the document.");
                }
                comboTargetType.SelectedItem = ExportMethodType.Standard.ToString();
                return;
            }

            UpdateDwellControls(standard);
            UpdateThermoColumns(standard);
            UpdateMaxLabel(standard);

            CalcMethodCount();
        }

        private void UpdateMaxLabel(bool standard)
        {
            if (standard)
            {
                labelMaxTransitions.Text = IsPrecursorOnlyInstrument
                    ? PREC_PER_SAMPLE_INJ_TXT
                    : TRANS_PER_SAMPLE_INJ_TXT;
            }
            else
            {
                labelMaxTransitions.Text = IsPrecursorOnlyInstrument
                    ? CONCUR_PREC_TXT
                    : CONCUR_TRANS_TXT;
            }
        }

        private enum RecalcMethodCountStatus { waiting, running, pending }
        private RecalcMethodCountStatus _recalcMethodCountStatus = RecalcMethodCountStatus.waiting;

        private void CalcMethodCount()
        {
            if (InstrumentType == null)
                return;

            if (IsDia)
            {
                labelMethodNum.Text = "1";
                return;
            }

            if (_recalcMethodCountStatus != RecalcMethodCountStatus.waiting || !IsHandleCreated)
            {
                _recalcMethodCountStatus = RecalcMethodCountStatus.pending;
                return;
            }

            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this, false);

            if (!ValidateSettings(e, helper) || comboInstrument.SelectedItem == null)
            {
                labelMethodNum.Text = "";
                return;
            }

            labelMethodNum.Text = "...";

            _recalcMethodCountStatus = RecalcMethodCountStatus.running;

            var recalcMethodCount = new RecalcMethodCountCaller(RecalcMethodCount);
            string instrument = comboInstrument.SelectedItem.ToString();
            recalcMethodCount.BeginInvoke(_exportProperties, instrument, _fileType, _document, null, null);
        }

        private delegate void RecalcMethodCountCaller(ExportDlgProperties exportProperties,
            string instrument, ExportFileType fileType, SrmDocument document);

        private void RecalcMethodCount(ExportDlgProperties exportProperties,
            string instrument, ExportFileType fileType, SrmDocument document)
        {
            AbstractMassListExporter exporter = null;
            try
            {
                exporter = exportProperties.ExportFile(instrument, fileType, null, document, null);
            }
            catch (IOException)
            {
            }
            catch(ADOException)
            {
            }

            int? methodCount = null;
            if (exporter != null)
                methodCount = exporter.MemoryOutput.Count;
            // Switch back to the UI thread to update the form
            try
            {
                Invoke(new Action<int?>(UpdateMethodCount), methodCount);
            }
// ReSharper disable EmptyGeneralCatchClause
            catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
            {
                // If disposed, then no need to update the UI
            }
        }

        private void UpdateMethodCount(int? methodCount)
        {
            labelMethodNum.Text = methodCount.HasValue
                ? methodCount.Value.ToString(CultureInfo.CurrentCulture)
                : "";

            var recalcMethodCountStatus = _recalcMethodCountStatus;
            _recalcMethodCountStatus = RecalcMethodCountStatus.waiting;
            if (recalcMethodCountStatus == RecalcMethodCountStatus.pending)
                CalcMethodCount();
        }

        private void UpdateDwellControls(bool standard)
        {
            bool showDwell = false;
            bool showRunLength = false;
            if (standard && !IsDia)
            {
                if (!IsSingleDwellInstrument)
                {
                    labelDwellTime.Text = DWELL_TIME_TXT;
                    showDwell = true;
                }
                else if (IsAlwaysScheduledInstrument)
                {
                    labelDwellTime.Text = RUN_DURATION_TXT;
                    showRunLength = true;                    
                }
            }
            labelDwellTime.Visible = showDwell || showRunLength;
            textDwellTime.Visible = showDwell;
            textRunLength.Visible = showRunLength;
        }

        private void btnBrowseTemplate_Click(object sender, EventArgs e)
        {
            string templateName = textTemplateFile.Text;
            if (Equals(InstrumentType, ExportInstrumentType.AGILENT6400))
            {
                var chooseDirDialog = new FolderBrowserDialog
                                          {
                                              Description = "Method Template",
                                          };

                if (!string.IsNullOrEmpty(templateName))
                {
                    chooseDirDialog.SelectedPath = templateName;
                } 
                
                if (chooseDirDialog.ShowDialog(this) == DialogResult.OK)
                {
                    templateName = chooseDirDialog.SelectedPath;
                    if (!AgilentMethodExporter.IsAgilentMethodPath(templateName))
                    {
                        MessageDlg.Show(this, "The chosen folder does not appear to contain an Agilent QQQ method template.  The folder is expected to have a .m extension, and contain the file qqqacqmethod.xsd.");
                        return;
                    }
                    textTemplateFile.Text = templateName;
                }

                return;
            }

            var openFileDialog = new OpenFileDialog
                                     {
                                         Title = "Method Template",
                                         // Extension based on currently selecte type
                                         CheckPathExists = true
                                     };

            if (!string.IsNullOrEmpty(templateName))
            {
                try
                {
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(templateName);
                    openFileDialog.FileName = Path.GetFileName(templateName);
                }
                catch (ArgumentException)
                {
                } // Invalid characters
                catch (PathTooLongException)
                {
                }
            }

            var listFileTypes = new List<string>();
            if (Equals(InstrumentType, ExportInstrumentType.ABI_QTRAP))
            {
                listFileTypes.Add(string.Format("{0} Method (*{1})|*{1}",
                                                InstrumentType, ExportInstrumentType.EXT_AB_SCIEX));
            }
            else if (Equals(InstrumentType, ExportInstrumentType.THERMO_TSQ) ||
                     Equals(InstrumentType, ExportInstrumentType.THERMO_LTQ))
            {
                listFileTypes.Add(string.Format("{0} Method (*{1})|*{1}",
                                                InstrumentType, ExportInstrumentType.EXT_THERMO));
            }
            else if (Equals(InstrumentType, ExportInstrumentType.WATERS_XEVO) ||
                     Equals(InstrumentType, ExportInstrumentType.WATERS_QUATTRO_PREMIER))
            {
                listFileTypes.Add(string.Format("{0} Method (*{1})|*{1}",
                                                InstrumentType, ExportInstrumentType.EXT_WATERS));
            }
            listFileTypes.Add("All Files (*.*)|*.*");
            openFileDialog.Filter = string.Join("|", listFileTypes.ToArray());

            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                textTemplateFile.Text = openFileDialog.FileName;
            }
        }

        private void textMaxTransitions_TextChanged(object sender, EventArgs e)
        {
            int maxTrans;
            if(!int.TryParse(textMaxTransitions.Text, out maxTrans) || maxTrans < 1)
            {
                labelMethodNum.Text = "";
                return;
            }

            CalcMethodCount();
        }

        private void comboOptimizing_SelectedIndexChanged(object sender, EventArgs e)
        {
            CalcMethodCount();
        }

        #region Functional Test Support

        public void SetInstrument(string instrument)
        {
            if(ExportInstrumentType.TRANSITION_LIST_TYPES.ToList().Find(inst => Equals(inst, instrument)) == default(string))
                return;

            comboInstrument.SelectedText = instrument;
        }

        public void SetMethodType(ExportMethodType type)
        {
            comboTargetType.SelectedItem = type == ExportMethodType.Standard ? "Standard" : "Scheduled";
        }

        public bool IsTargetTypeEnabled
        {
            get { return comboTargetType.Enabled; }
        }

        public bool IsOptimizeTypeEnabled
        {
            get {return comboOptimizing.Enabled;}
        }

        public string GetMaxLabelText
        {
           get { return labelMaxTransitions.Text; }
        }

        public bool IsMaxTransitionsEnabled
        {
           get { return textMaxTransitions.Enabled; }
        }

        public string GetDwellTimeLabel
        {
            get { return labelDwellTime.Text; }
        }

        public bool IsDwellTimeVisible
        {
            get { return textDwellTime.Visible; }
        }

        public bool IsRunLengthVisible
        {
            get{ return textRunLength.Visible; }
        }

        public int CalculationTime
        {
            get { return _exportProperties.MultiplexIsolationListCalculationTime; }
            set { _exportProperties.MultiplexIsolationListCalculationTime = value; }
        }

        public bool DebugCycles
        {
            get { return _exportProperties.DebugCycles; }
            set { _exportProperties.DebugCycles = value; }
        }

        #endregion
    }

    public class ExportDlgProperties : ExportProperties
    {
        private readonly ExportMethodDlg _dialog;

        public ExportDlgProperties(ExportMethodDlg dialog)
        {
            _dialog = dialog;
        }

        public bool ShowMessages { get; set; }

        public override void PerformLongExport(Action<IProgressMonitor> performExport)
        {
            if (!ShowMessages)
            {
                performExport(new SilentProgressMonitor());
                return;
            }

            var longWait = new LongWaitDlg { Text = "Exporting Methods" };
            try
            {
                var status = longWait.PerformWork(_dialog, 800, performExport);
                if (status.IsError)
                    MessageDlg.Show(_dialog, status.ErrorException.Message);
            }
            catch (Exception x)
            {
                MessageDlg.Show(_dialog, string.Format("An error occurred attempting to export.\n{0}", x.Message));
            }
        }

        private class SilentProgressMonitor : IProgressMonitor
        {
            public bool IsCanceled { get { return false; } }
            public void UpdateProgress(ProgressStatus status) { }
        }
    }
}
