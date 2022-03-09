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
using System.Threading;
using System.Windows.Forms;
using NHibernate;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public sealed partial class ExportMethodDlg : CreateHandleDebugBase, IMultipleViewProvider
    {
        public static string TRANS_PER_SAMPLE_INJ_TXT { get { return Resources.ExportMethodDlg_TRANS_PER_SAMPLE_INJ_TXT; } }
        public static string CONCUR_TRANS_TXT { get { return Resources.ExportMethodDlg_CONCUR_TRANS_TXT; } }
        public static string PREC_PER_SAMPLE_INJ_TXT { get { return Resources.ExportMethodDlg_PREC_PER_SAMPLE_INJ_TXT; } }
        public static string CONCUR_PREC_TXT { get { return Resources.ExportMethodDlg_CONCUR_PREC_TXT; } }
        public static string RUN_DURATION_TXT { get { return Resources.ExportMethodDlg_RUN_DURATION_TXT; } }
        public static string DWELL_TIME_TXT { get { return Resources.ExportMethodDlg_DWELL_TIME_TXT; } }

        public static string SCHED_NOT_SUPPORTED_ERR_TXT { get { return Resources.ExportMethodDlg_comboTargetType_SelectedIndexChanged_Sched_Not_Supported_Err_Text; } }

        private readonly SrmDocument _document;
        private readonly ExportFileType _fileType;
        private string _instrumentType;

        private readonly ExportDlgProperties _exportProperties;

        private CancellationTokenSource _cancellationTokenSource;

        public ExportMethodDlg(SrmDocument document, ExportFileType fileType)
        {
            InitializeComponent();

            _cancellationTokenSource = new CancellationTokenSource();
            _exportProperties = new ExportDlgProperties(this, _cancellationTokenSource.Token);

            _document = document;
            _fileType = fileType;

            string[] listTypes;
            if (_fileType == ExportFileType.Method)
                listTypes = ExportInstrumentType.METHOD_TYPES;
            else
            {
                if (_fileType == ExportFileType.List)
                {
                    Text = Resources.ExportMethodDlg_ExportMethodDlg_Export_Transition_List;
                    listTypes = ExportInstrumentType.TRANSITION_LIST_TYPES;
                }
                else
                {
                    Text = Resources.ExportMethodDlg_ExportMethodDlg_Export_Isolation_List;
                    listTypes = ExportInstrumentType.ISOLATION_LIST_TYPES;
                    _exportProperties.MultiplexIsolationListCalculationTime = 20;   // Default 20 seconds to search for good multiplexed window ordering.
                }
                
                btnBrowseTemplate.Visible = false;
                labelTemplateFile.Visible = false;
                textTemplateFile.Visible = false;
                Height -= textTemplateFile.Bottom - btnGraph.Bottom;
            }

            comboInstrument.Items.Clear();
            foreach (string typeName in listTypes)
                comboInstrument.Items.Add(typeName);

            // Init dialog values from settings.
            ExportStrategy = Helpers.ParseEnum(Settings.Default.ExportMethodStrategy, ExportStrategy.Single);

            SortByMz = Settings.Default.ExportSortByMz;
            IgnoreProteins = Equals(ExportStrategy, ExportStrategy.Buckets) || Settings.Default.ExportIgnoreProteins;

            // Start with method type as Standard until after instrument type is set
            comboTargetType.Items.Add(ExportMethodType.Standard.GetLocalizedString());
            comboTargetType.Items.Add(ExportMethodType.Scheduled.GetLocalizedString());
            comboTargetType.Items.Add(ExportMethodType.Triggered.GetLocalizedString());
            MethodType = ExportMethodType.Standard;

            // Add optimizable regressions
            comboOptimizing.Items.Add(ExportOptimize.NONE);
            comboOptimizing.Items.Add(ExportOptimize.CE);
            if (document.Settings.TransitionSettings.Prediction.DeclusteringPotential != null)
                comboOptimizing.Items.Add(ExportOptimize.DP);
            comboOptimizing.SelectedIndex = 0;

            // Set instrument type based on CE regression name for the document.
            string cePredictorName = document.Settings.TransitionSettings.Prediction.CollisionEnergy.Name;
            if (cePredictorName != null)
            {
                // Look for the first instrument type with the same prefix as the CE name
                string cePredictorPrefix = cePredictorName.Split(' ')[0];
                // We still may see some CE regressions that begin with ABI or AB, while all instruments
                // have been changed to start with SCIEX
                if (Equals(@"ABI", cePredictorPrefix) || Equals(@"AB", cePredictorPrefix))
                    cePredictorPrefix = ExportInstrumentType.ABI;
                int i = -1;
                if (document.Settings.TransitionSettings.FullScan.IsEnabled)
                {
                    i = listTypes.IndexOf(typeName => typeName.StartsWith(cePredictorPrefix) &&
                        ExportInstrumentType.IsFullScanInstrumentType(typeName));
                }
                if (i == -1)
                {
                    i = listTypes.IndexOf(typeName => typeName.StartsWith(cePredictorPrefix));
                }
                if (i != -1)
                    InstrumentType = listTypes[i];
            }
            // If nothing found based on CE regression name, just use the first instrument in the list
            if (InstrumentType == null)
            {
                var instrumentTypeFirst = listTypes[0];
                // Avoid defaulting to Agilent for DIA when we know it is not supported.
                if (IsDiaFullScan && Equals(instrumentTypeFirst, ExportInstrumentType.AGILENT_TOF) && listTypes.Length > 1)
                    InstrumentType = listTypes[1];
                else
                    InstrumentType = instrumentTypeFirst;
            }

            // Reset method type based on what was used last and what the chosen instrument is capable of
            ExportMethodType mType = Helpers.ParseEnum(Settings.Default.ExportMethodType, ExportMethodType.Standard);
            if (mType == ExportMethodType.Triggered && !CanTrigger)
            {
                mType = ExportMethodType.Scheduled;
            }
            if (mType != ExportMethodType.Standard && !CanSchedule)
            {
                mType = ExportMethodType.Standard;
            }
            MethodType = mType;

            DwellTime = Settings.Default.ExportMethodDwellTime;
            RunLength = Settings.Default.ExportMethodRunLength;

            Helpers.PeptideToMoleculeTextMapper.TranslateForm(this, document.DocumentType); // Use terminology like "Molecule List" instead of "Protein" if appropriate to document

            // For documents with mixed polarity, offer to emit two different lists or just one polarity
            var isMixedPolarity = document.IsMixedPolarity();
            labelPolarityFilter.Visible = comboPolarityFilter.Visible = labelPolarityFilter.Enabled = comboPolarityFilter.Enabled =
                isMixedPolarity;
            comboPolarityFilter.SelectedIndex = (int) (comboPolarityFilter.Enabled ?
                 Helpers.ParseEnum(Settings.Default.ExportPolarityFilterEnum, ExportPolarity.all) :
                 ExportPolarity.all);
            PolarityFilter = TypeSafeEnum.ValidateOrDefault((ExportPolarity)comboPolarityFilter.SelectedIndex, ExportPolarity.all);
            if (isMixedPolarity)
            {
                labelPolarityFilter.Left = labelOptimizing.Left;
                comboPolarityFilter.Left = comboOptimizing.Left;
                var labelPolarityFilterTop = labelOptimizing.Top;
                var comboPolarityFilterTop = comboOptimizing.Top;
                // Put as much vertical space betwween comboPolarityFilter and comboOptimizing as there was between comboOptimizing and method count display
                var polarityFilterVerticalSpacing = comboOptimizing.Bottom - labelMethodNum.Bottom;
                foreach (var controlObj in Controls)
                {
                    var control = controlObj as Control;
                    if ((control != null) && (control.Top >= labelPolarityFilterTop))
                    {
                        control.Top += polarityFilterVerticalSpacing;
                    }
                }
                labelPolarityFilter.Top = labelPolarityFilterTop;
                comboPolarityFilter.Top = comboPolarityFilterTop;
                Height += polarityFilterVerticalSpacing;
            }

            UpdateMaxTransitions();

            cbEnergyRamp.Checked = Settings.Default.ExportThermoEnergyRamp;
            cbTriggerRefColumns.Checked = Settings.Default.ExportThermoTriggerRef;
            cbExportMultiQuant.Checked = Settings.Default.ExportMultiQuant;
            cbSureQuant.Checked = Settings.Default.ExportSureQuant;
            textIntensityThreshold.Text = cbSureQuant.Checked
                ? Settings.Default.IntensityThresholdPercent.ToString(LocalizationHelper.CurrentCulture)
                : Settings.Default.IntensityThresholdValue.ToString(LocalizationHelper.CurrentCulture);
            textIntensityThresholdMin.Text = Settings.Default.IntensityThresholdMin.ToString(LocalizationHelper.CurrentCulture);
            cbExportEdcMass.Checked = Settings.Default.ExportEdcMass;
            textPrimaryCount.Text = Settings.Default.PrimaryTransitionCount.ToString(LocalizationHelper.CurrentCulture);
            textMs1RepetitionTime.Text = Settings.Default.ExportMs1RepetitionTime.ToString(LocalizationHelper.CurrentCulture);
            // Reposition from design layout
            cbSlens.Top = textMaxTransitions.Bottom;
            panelSureQuant.Top = labelMaxTransitions.Top;
            panelThermoColumns.Top = labelDwellTime.Top;
            var panelOffset = panelThermoColumns.Controls.Cast<Control>().Min(c => c.Left);
            foreach (var control in panelThermoColumns.Controls.Cast<Control>())
                control.Left -= panelOffset;
            panelThermoRt.Top = panelThermoColumns.Top - (int)(panelThermoRt.Height*0.8);
            panelAbSciexTOF.Top = textDwellTime.Top + (textDwellTime.Height - panelAbSciexTOF.Height)/2;
            panelTriggered.Top = textDwellTime.Top + (textDwellTime.Height - panelTriggered.Height)/2;
            panelTuneColumns.Top = comboTargetType.Top + (comboTargetType.Height - panelTuneColumns.Height)/2;
            panelSciexTune.Top = labelOptimizing.Top;
            panelWaters.Top = labelDwellTime.Top - panelWaters.Height;
            panelBrukerTimsTof.Top = labelOptimizing.Top;

            foreach (string tuneType in ExportOptimize.CompensationVoltageTuneTypes)
                comboTuning.Items.Add(tuneType);
            comboTuning.SelectedIndex = 0;

            // Further repositioning - for design layout convenience we have many things to the right of the OK button.
            // Find all such items and shift them to the left, then resize the form itself.
            foreach (var controlObj in Controls)
            {
                var control = controlObj as Control;
                if (control != null && control.Left > btnOk.Right)
                {
                    control.Left = cbIgnoreProteins.Left; // Align with a known-good control
                }
            }
            SetBounds(Left, Top, btnOk.Right + 2 * label4.Left, Height);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            _recalcMethodCountStatus = RecalcMethodCountStatus.waiting;
            CalcMethodCount();

            base.OnHandleCreated(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _cancellationTokenSource.Cancel();

            base.OnClosing(e);
        }

        public string InstrumentType
        {
            get { return _instrumentType; }
            set
            {
                _instrumentType = value;

                // If scheduled method selected
                if (!Equals(ExportMethodType.Standard.GetLocalizedString().ToLower(),
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
                   Equals(type, ExportInstrumentType.BRUKER_TOF) ||
                   Equals(type, ExportInstrumentType.BRUKER_TIMSTOF) ||
                   Equals(type, ExportInstrumentType.SHIMADZU) ||
                   Equals(type, ExportInstrumentType.THERMO) ||
                   Equals(type, ExportInstrumentType.THERMO_QUANTIVA) ||
                   Equals(type, ExportInstrumentType.THERMO_ALTIS) ||
                   Equals(type, ExportInstrumentType.THERMO_ENDURA) ||
                   Equals(type, ExportInstrumentType.THERMO_FUSION) ||
                   Equals(type, ExportInstrumentType.THERMO_TSQ) ||
                   Equals(type, ExportInstrumentType.THERMO_LTQ) ||
                   Equals(type, ExportInstrumentType.THERMO_Q_EXACTIVE) ||
                   Equals(type, ExportInstrumentType.THERMO_EXPLORIS) ||
                   Equals(type, ExportInstrumentType.THERMO_FUSION_LUMOS) ||
                   Equals(type, ExportInstrumentType.THERMO_ECLIPSE) ||
                   Equals(type, ExportInstrumentType.WATERS) ||
                   Equals(type, ExportInstrumentType.WATERS_SYNAPT_TRAP) ||
                   Equals(type, ExportInstrumentType.WATERS_SYNAPT_TRANSFER) ||
                   Equals(type, ExportInstrumentType.WATERS_XEVO_TQ) ||
                   Equals(type, ExportInstrumentType.WATERS_XEVO_QTOF) ||
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
                return Equals(type, ExportInstrumentType.SHIMADZU) ||
                       Equals(type, ExportInstrumentType.THERMO_TSQ) ||
                       Equals(type, ExportInstrumentType.THERMO_QUANTIVA) ||
                       Equals(type, ExportInstrumentType.THERMO_ALTIS) ||
                       Equals(type, ExportInstrumentType.THERMO_ENDURA) ||
                       Equals(type, ExportInstrumentType.THERMO_EXPLORIS) ||
                       Equals(type, ExportInstrumentType.THERMO_FUSION_LUMOS) ||
                       Equals(type, ExportInstrumentType.WATERS) ||
                       Equals(type, ExportInstrumentType.WATERS_SYNAPT_TRAP) ||
                       Equals(type, ExportInstrumentType.WATERS_SYNAPT_TRANSFER) ||
                       Equals(type, ExportInstrumentType.WATERS_XEVO_TQ) ||
                       Equals(type, ExportInstrumentType.WATERS_XEVO_QTOF) ||
                       Equals(type, ExportInstrumentType.WATERS_QUATTRO_PREMIER) ||
                       Equals(type, ExportInstrumentType.BRUKER_TOF) ||
                       Equals(type, ExportInstrumentType.BRUKER_TIMSTOF) ||
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

        private bool CanTriggerInstrumentType
        {
            get { return ExportInstrumentType.CanTriggerInstrumentType(InstrumentType); }
        }

        private bool CanTrigger
        {
            get { return CanTriggerReplicate(null); }
        }

        private bool CanTriggerReplicate(int? replicateIndex)
        {
            return ExportInstrumentType.CanTrigger(InstrumentType, _document, replicateIndex);
        }

        private ExportSchedulingAlgorithm SchedulingAlgorithm
        {
            set { _exportProperties.SchedulingAlgorithm = value; }
        }

        private int? SchedulingReplicateNum
        {
            set { _exportProperties.SchedulingReplicateNum = value; }
        }

        public bool IsFullScanInstrument
        {
            get { return ExportInstrumentType.IsFullScanInstrumentType(InstrumentType);  }
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
                if (value.Equals(ExportOptimize.COV_FINE) || value.Equals(ExportOptimize.COV_MEDIUM) ||
                    value.Equals(ExportOptimize.COV_ROUGH))
                {
                    comboOptimizing.SelectedItem = ExportOptimize.COV;
                    comboTuning.SelectedItem = value;
                }
                else
                {
                    comboOptimizing.SelectedItem = value;
                }
                _exportProperties.OptimizeType = Equals(ExportOptimize.NONE, value) ? null : value;
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

        public bool SortByMz
        {
            get { return _exportProperties.SortByMz; }
            set { _exportProperties.SortByMz = cbSortByMz.Checked = value; }
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

        public bool UseSlens
        {
            get { return _exportProperties.UseSlens; }
            set
            {
                _exportProperties.UseSlens = cbSlens.Checked = value;
            }
        }

        public bool WriteCompensationVoltages
        {
            get { return _exportProperties.WriteCompensationVoltages; }
            set { _exportProperties.WriteCompensationVoltages = cbWriteCoV.Checked = value; }
        }

        public ExportPolarity PolarityFilter
        {
            get { return _exportProperties.PolarityFilter; }
            set
            {
                _exportProperties.PolarityFilter = comboPolarityFilter.Enabled ? value : ExportPolarity.all;
                comboPolarityFilter.SelectedIndex = (int) _exportProperties.PolarityFilter;
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

        public bool ExportSureQuant
        {
            get { return _exportProperties.ExportSureQuant; }
            set { _exportProperties.ExportSureQuant = cbSureQuant.Checked = value; }
        }

        public double? IntensityThresholdPercent
        {
            get { return _exportProperties.IntensityThresholdPercent; }
            set { _exportProperties.IntensityThresholdPercent = value; }
        }

        public double? IntensityThresholdValue
        {
            get { return _exportProperties.IntensityThresholdValue; }
            set { _exportProperties.IntensityThresholdValue = value; }
        }

        public double? IntensityThresholdMin
        {
            get { return _exportProperties.IntensityThresholdMin; }
            set { _exportProperties.IntensityThresholdMin = value; }
        }

        public bool ExportEdcMass
        {
            get { return _exportProperties.ExportEdcMass; }
            set { _exportProperties.ExportEdcMass = cbExportEdcMass.Checked = value; }
        }

        private void UpdateThermoColumns(ExportMethodType targetType)
        {
            panelThermoColumns.Visible = targetType == ExportMethodType.Scheduled &&
                InstrumentType == ExportInstrumentType.THERMO;
        }

        private void UpdateAbSciexControls()
        {
            panelAbSciexTOF.Visible = InstrumentType == ExportInstrumentType.ABI_TOF;
        }

        private void UpdateWatersControls()
        {
            switch (InstrumentType)
            {
                case ExportInstrumentType.WATERS_SYNAPT_TRANSFER:
                case ExportInstrumentType.WATERS_SYNAPT_TRAP:
                case ExportInstrumentType.WATERS_XEVO_QTOF:
                    panelWaters.Visible = true;
                    bool exportEdcEnabled = !_document.GetPrecursorsWithoutTopRank(
                        _exportProperties.PrimaryTransitionCount, _exportProperties.SchedulingReplicateNum).Any();
                    cbExportEdcMass.Enabled = exportEdcEnabled;
                    if (!exportEdcEnabled)
                    {
                        ExportEdcMass = false;
                    }
                    break;
                default:
                    panelWaters.Visible = false;
                    break;
            }
        }

        private void UpdateBrukerTimsTofControls()
        {
            panelBrukerTimsTof.Visible = Equals(InstrumentType, ExportInstrumentType.BRUKER_TIMSTOF) &&
                                         _fileType == ExportFileType.Method;
        }

        private void UpdateCovControls()
        {
            bool covInList = comboOptimizing.Items.Contains(ExportOptimize.COV);
            bool canOptimizeCov = _document.Settings.TransitionSettings.Prediction.CompensationVoltage != null &&
                (InstrumentType.Contains(@"SCIEX") ||
                 InstrumentType.Equals(ExportInstrumentType.THERMO_QUANTIVA) ||
                 InstrumentType.Equals(ExportInstrumentType.THERMO_ALTIS));
            if (covInList && !canOptimizeCov)
            {
                if (comboOptimizing.SelectedItem.ToString().Equals(ExportOptimize.COV))
                {
                    OptimizeType = ExportOptimize.NONE;
                }
                comboOptimizing.Items.Remove(ExportOptimize.COV);
            }
            else if (!covInList && canOptimizeCov)
            {
                comboOptimizing.Items.Add(ExportOptimize.COV);
            }
        }

        private void UpdateThermoRtControls(ExportMethodType targetType)
        {
            panelThermoRt.Visible =
                InstrumentType == ExportInstrumentType.THERMO_QUANTIVA ||
                InstrumentType == ExportInstrumentType.THERMO_ALTIS ||
                (targetType != ExportMethodType.Standard && InstrumentType == ExportInstrumentType.THERMO);
            if (panelThermoColumns.Visible)
            {
                panelThermoRt.Top = panelThermoColumns.Top - (int)(panelThermoRt.Height * 0.8);
            }
            else
            {
                panelThermoRt.Top = labelDwellTime.Visible
                    ? labelDwellTime.Top - panelThermoRt.Height
                    : labelDwellTime.Top + (panelThermoRt.Height / 2);
            }
        }

        private void UpdateThermoSLensControl(ExportMethodType targetType)
        {
            cbSlens.Visible = cbSlens.Enabled =
                InstrumentType == ExportInstrumentType.THERMO_QUANTIVA ||
                InstrumentType == ExportInstrumentType.THERMO_ALTIS ||
                InstrumentType == ExportInstrumentType.THERMO;  // TODO bspratt is this specific enough?
        }

        private void UpdateThermoFaimsCvControl()
        {
            var fusionMethod = InstrumentType == ExportInstrumentType.THERMO_FUSION && _fileType == ExportFileType.Method;
            cbWriteCoV.Top = !fusionMethod ? cbSlens.Bottom : panelTuneColumns.Top - cbWriteCoV.Height;
            cbWriteCoV.Left = !fusionMethod ? cbIgnoreProteins.Left : panelTuneColumns.Left + cbTune3.Left;
            cbWriteCoV.Visible = cbWriteCoV.Enabled =
                InstrumentType == ExportInstrumentType.THERMO_QUANTIVA ||
                (InstrumentType == ExportInstrumentType.THERMO_FUSION && !cbSureQuant.Checked) ||
                InstrumentType == ExportInstrumentType.THERMO_ALTIS;
            var optimizing = comboOptimizing.SelectedItem;
            if (optimizing != null && Equals(optimizing.ToString(), ExportOptimize.COV))
            {
                cbWriteCoV.Checked = true;
                cbWriteCoV.Enabled = false;
            }
        }

        private void UpdateThermoSureQuantControls()
        {
            panelSureQuant.Visible =
                _fileType == ExportFileType.Method &&
                (InstrumentType == ExportInstrumentType.THERMO_EXPLORIS ||
                 // InstrumentType == ExportInstrumentType.THERMO_FUSION ||
                 InstrumentType == ExportInstrumentType.THERMO_FUSION_LUMOS||
                 InstrumentType == ExportInstrumentType.THERMO_ECLIPSE);
            if (cbSureQuant.Checked)
            {
                if (!lblIntensityThresholdType.Visible)
                {
                    lblIntensityThresholdType.Show();
                    textIntensityThreshold.Width = textPrimaryCount.Width;
                    textIntensityThreshold.Text = Settings.Default.IntensityThresholdPercent.ToString(CultureInfo.CurrentCulture);
                    helpTip.SetToolTip(textIntensityThreshold, Resources.ExportMethodDlg_UpdateThermoSureQuantControls_Percentage_of_peak_max_height_to_use_as_intensity_threshold_);
                    lblIntensityThresholdMin.Show();
                    textIntensityThresholdMin.Show();
                    textIntensityThresholdMin.Text = Settings.Default.IntensityThresholdMin.ToString(CultureInfo.CurrentCulture);
                }
            }
            else
            {
                if (lblIntensityThresholdType.Visible)
                {
                    lblIntensityThresholdType.Hide();
                    textIntensityThreshold.Width = textIntensityThresholdMin.Width;
                    textIntensityThreshold.Text = Settings.Default.IntensityThresholdValue.ToString(CultureInfo.CurrentCulture);
                    helpTip.SetToolTip(textIntensityThreshold, Resources.ExportMethodDlg_UpdateThermoSureQuantControls_Absolute_relative_intensity_threshold_value_);
                    lblIntensityThresholdMin.Hide();
                    textIntensityThresholdMin.Hide();
                }
            }

            panelTuneColumns.Visible = InstrumentType == ExportInstrumentType.THERMO_FUSION && !cbSureQuant.Checked;
            UpdateThermoFaimsCvControl();
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
                comboTargetType.SelectedItem = _exportProperties.MethodType.GetLocalizedString();
            }
        }

        public int PrimaryCount
        {
            get { return _exportProperties.PrimaryTransitionCount; }
            set
            {
                _exportProperties.PrimaryTransitionCount = value;
                textPrimaryCount.Text = _exportProperties.PrimaryTransitionCount.ToString(LocalizationHelper.CurrentCulture);
            }
        }

        /// <summary>
        /// Specific dwell time in milliseconds for non-scheduled runs
        /// </summary>
        public int DwellTime
        {
            get { return _exportProperties.DwellTime; }
            set
            {
                _exportProperties.DwellTime = value;
                textDwellTime.Text = _exportProperties.DwellTime.ToString(LocalizationHelper.CurrentCulture);
            }
        }

        /// <summary>
        /// Length of run in minutes for non-scheduled runs
        /// </summary>
        public double RunLength
        {
            get { return _exportProperties.RunLength; }
            set
            {
                _exportProperties.RunLength = value;
                textRunLength.Text = _exportProperties.RunLength.ToString(LocalizationHelper.CurrentCulture);
            }
        }

        /// <summary>
        /// Used for maximum transitions/precursors, maximum concurrent transitions/precursors for SRM/full-scan
        /// </summary>
        public int? MaxTransitions
        {
            get { return _exportProperties.MaxTransitions; }
            set
            {
                _exportProperties.MaxTransitions = value;
                textMaxTransitions.Text = (_exportProperties.MaxTransitions.HasValue
                    ? _exportProperties.MaxTransitions.Value.ToString(LocalizationHelper.CurrentCulture)
                    : string.Empty);
            }
        }

        public void OkDialog()
        {
            OkDialog(null);
        }

        public void OkDialog(string outputPath)
        {
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
                        Equals(InstrumentType, ExportInstrumentType.ABI_TOF) || // CONSIDER: Should be possible, but we haven't done methods yet
                        Equals(InstrumentType, ExportInstrumentType.THERMO_LTQ))
                    {
                        helper.ShowTextBoxError(textTemplateFile, Resources.ExportMethodDlg_OkDialog_Export_of_DIA_method_is_not_supported_for__0__, InstrumentType);
                        return;
                    }
                }

                templateName = textTemplateFile.Text;
                if (string.IsNullOrEmpty(templateName))
                {
                    helper.ShowTextBoxError(textTemplateFile, Resources.ExportMethodDlg_OkDialog_A_template_file_is_required_to_export_a_method);
                    return;
                }
                if ((Equals(InstrumentType, ExportInstrumentType.AGILENT6400) ||
                    Equals(InstrumentType, ExportInstrumentType.BRUKER_TOF)) ?
                                                                                 !Directory.Exists(templateName) : !File.Exists(templateName))
                {
                    helper.ShowTextBoxError(textTemplateFile, Resources.ExportMethodDlg_OkDialog_The_template_file__0__does_not_exist, templateName);
                    return;
                }
                if (Equals(InstrumentType, ExportInstrumentType.AGILENT6400) &&
                    !AgilentMethodExporter.IsAgilentMethodPath(templateName))
                {
                    helper.ShowTextBoxError(textTemplateFile,
                                            Resources.ExportMethodDlg_OkDialog_The_folder__0__does_not_appear_to_contain_an_Agilent_QQQ_method_template_The_folder_is_expected_to_have_a_m_extension_and_contain_the_file_qqqacqmethod_xsd,
                                            templateName);
                    return;
                }
                if (Equals(InstrumentType, ExportInstrumentType.BRUKER_TOF) &&
                    !BrukerMethodExporter.IsBrukerMethodPath(templateName))
                {
                    helper.ShowTextBoxError(textTemplateFile,
                                            Resources.ExportMethodDlg_OkDialog_The_folder__0__does_not_appear_to_contain_a_Bruker_TOF_method_template___The_folder_is_expected_to_have_a__m_extension__and_contain_the_file_submethods_xml_,
                                            templateName);
                    return;
                }
            }

            if (Equals(InstrumentType, ExportInstrumentType.AGILENT_TOF) ||
                Equals(InstrumentType, ExportInstrumentType.ABI_TOF))
            {
                // Check that mass analyzer settings are set to TOF.
                if (!CheckAnalyzer(documentExport.Settings.TransitionSettings.FullScan.IsEnabledMs, 
                                   documentExport.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer,
                                   FullScanMassAnalyzerType.tof))
                {
                    MessageDlg.Show(this, string.Format(Resources.ExportMethodDlg_OkDialog_The_precursor_mass_analyzer_type_is_not_set_to__0__in_Transition_Settings_under_the_Full_Scan_tab, Resources.ExportMethodDlg_OkDialog_TOF));
                    return;
                }
                if (!CheckAnalyzer(documentExport.Settings.TransitionSettings.FullScan.IsEnabledMsMs,
                                   documentExport.Settings.TransitionSettings.FullScan.ProductMassAnalyzer,
                                   FullScanMassAnalyzerType.tof))
                {
                    MessageDlg.Show(this, string.Format(Resources.ExportMethodDlg_OkDialog_The_product_mass_analyzer_type_is_not_set_to__0__in_Transition_Settings_under_the_Full_Scan_tab, Resources.ExportMethodDlg_OkDialog_TOF));
                    return;
                }
            }

            if (Equals(InstrumentType, ExportInstrumentType.THERMO_Q_EXACTIVE))
            {
                // Check that mass analyzer settings are set to Orbitrap.
                if (!CheckAnalyzer(documentExport.Settings.TransitionSettings.FullScan.IsEnabledMs,
                                   documentExport.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer,
                                   FullScanMassAnalyzerType.orbitrap))
                {
                    MessageDlg.Show(this, string.Format(Resources.ExportMethodDlg_OkDialog_The_precursor_mass_analyzer_type_is_not_set_to__0__in_Transition_Settings_under_the_Full_Scan_tab, Resources.ExportMethodDlg_OkDialog_Orbitrap));
                    return;
                }
                if (!CheckAnalyzer(documentExport.Settings.TransitionSettings.FullScan.IsEnabledMsMs,
                                   documentExport.Settings.TransitionSettings.FullScan.ProductMassAnalyzer,
                                   FullScanMassAnalyzerType.orbitrap))
                {
                    MessageDlg.Show(this, string.Format(Resources.ExportMethodDlg_OkDialog_The_product_mass_analyzer_type_is_not_set_to__0__in_Transition_Settings_under_the_Full_Scan_tab, Resources.ExportMethodDlg_OkDialog_Orbitrap));
                    return;
                }
            }

            if (IsDia && _document.Settings.TransitionSettings.FullScan.IsolationScheme.FromResults)
            {
                MessageDlg.Show(this, Resources.ExportMethodDlg_OkDialog_The_DIA_isolation_list_must_have_prespecified_windows_);
                return;
            }

            if (!documentExport.HasAllRetentionTimeStandards() &&
                DialogResult.Cancel == MultiButtonMsgDlg.Show(
                    this,
                    TextUtil.LineSeparate(
                        Resources.ExportMethodDlg_OkDialog_The_document_does_not_contain_all_of_the_retention_time_standard_peptides,
                        Resources.ExportMethodDlg_OkDialog_You_will_not_be_able_to_use_retention_time_prediction_with_acquired_results,
                        Resources.ExportMethodDlg_OkDialog_Are_you_sure_you_want_to_continue),
                    Resources.ExportMethodDlg_OkDialog_OK))
            {
                return;
            }

            //This will populate _exportProperties
            if (!ValidateSettings(helper))
            {
                return;
            }

            if (Equals(InstrumentType, ExportInstrumentType.BRUKER_TIMSTOF))
            {
                var missingIonMobility = BrukerTimsTofIsolationListExporter.GetMissingIonMobility(documentExport, _exportProperties);
                if (missingIonMobility.Length > 0)
                {
                    MessageDlg.Show(this,
                        Resources.ExportMethodDlg_OkDialog_All_targets_must_have_an_ion_mobility_value__These_can_be_set_explicitly_or_contained_in_an_ion_mobility_library_or_spectral_library__The_following_ion_mobility_values_are_missing_ +
                        Environment.NewLine + Environment.NewLine +
                        TextUtil.LineSeparate(missingIonMobility.Select(k => k.ToString())));
                    return;
                }
            }

            // Full-scan method building ignores CE and DP regression values
            if (!IsFullScanInstrument)
            {
                // Check to make sure CE and DP match chosen instrument, and offer to use
                // the correct version for the instrument, if not.
                var predict = documentExport.Settings.TransitionSettings.Prediction;
                var ce = predict.CollisionEnergy;
                string ceName = (ce != null ? ce.Name : null);
                string ceNameDefault = _instrumentType.Split(' ')[0];

                // CE prediction should be None for Bruker timsTOF, since the CE is populated by the instrument control software in the method.
                if (Equals(_instrumentType, ExportInstrumentType.BRUKER_TIMSTOF))
                    ceNameDefault = CollisionEnergyList.ELEMENT_NONE;

                bool ceInSynch = IsInSynchPredictor(ceName, ceNameDefault);

                var dp = predict.DeclusteringPotential;
                string dpName = (dp != null ? dp.Name : null);
                string dpNameDefault = _instrumentType.Split(' ')[0];
                bool dpInSynch = true;
                if (_instrumentType == ExportInstrumentType.ABI)
                    dpInSynch = IsInSynchPredictor(dpName, dpNameDefault);
                else
                    dpNameDefault = null; // Ignored for all other types

                if ((!ceInSynch && Settings.Default.CollisionEnergyList.Keys.Any(name => name.StartsWith(ceNameDefault))) ||
                    (!dpInSynch && Settings.Default.DeclusterPotentialList.Keys.Any(name => name.StartsWith(dpNameDefault))))
                {
                    var sb = new StringBuilder(string.Format(Resources.ExportMethodDlg_OkDialog_The_settings_for_this_document_do_not_match_the_instrument_type__0__,
                                                             _instrumentType));
                    sb.AppendLine().AppendLine();
                    if (!ceInSynch)
                        sb.Append(Resources.ExportMethodDlg_OkDialog_Collision_Energy).Append(TextUtil.SEPARATOR_SPACE).AppendLine(ceName);
                    if (!dpInSynch)
                    {
                        sb.Append(Resources.ExportMethodDlg_OkDialog_Declustering_Potential).Append(TextUtil.SEPARATOR_SPACE)
                          .AppendLine(dpName ?? Resources.ExportMethodDlg_OkDialog_None);
                    }
                    sb.AppendLine().Append(Resources.ExportMethodDlg_OkDialog_Would_you_like_to_use_the_defaults_instead);
                    var result = MultiButtonMsgDlg.Show(this, sb.ToString(), MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true);
                    if (result == DialogResult.Yes)
                    {
                        documentExport = ChangeInstrumentTypeSettings(documentExport, ceNameDefault, dpNameDefault);
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        comboInstrument.Focus();
                        return;
                    }
                }
            }

            var covPrediction = documentExport.Settings.TransitionSettings.Prediction.CompensationVoltage != null;
            var writeFaims = cbWriteCoV.Visible && cbWriteCoV.Checked;
            if ((covPrediction || writeFaims) && !Equals(comboOptimizing.SelectedItem.ToString(), ExportOptimize.COV))
            {
                // Show warning if we don't have results for the highest tune level
                string message = null;
                if (!writeFaims)
                {
                    var highestCoV = documentExport.HighestCompensationVoltageTuning();
                    switch (highestCoV)
                    {
                        case CompensationVoltageParameters.Tuning.fine:
                            {
                                var missing = documentExport.GetMissingCompensationVoltages(highestCoV).ToArray();
                                if (missing.Any())
                                {
                                    message = TextUtil.LineSeparate(
                                        Resources.ExportMethodDlg_OkDialog_You_are_missing_fine_tune_optimized_compensation_voltages_for_the_following_,
                                        TextUtil.LineSeparate(missing));
                                }
                                break;
                            }
                        case CompensationVoltageParameters.Tuning.medium:
                            {
                                message = Resources.ExportMethodDlg_OkDialog_You_are_missing_fine_tune_optimized_compensation_voltages_;
                                var missing = documentExport.GetMissingCompensationVoltages(highestCoV).ToArray();
                                if (missing.Any())
                                {
                                    message = TextUtil.LineSeparate(message,
                                        Resources.ExportMethodDlg_OkDialog_You_are_missing_medium_tune_optimized_compensation_voltages_for_the_following_,
                                        TextUtil.LineSeparate(missing));
                                }
                                break;
                            }
                        case CompensationVoltageParameters.Tuning.rough:
                            {
                                message = Resources.ExportMethodDlg_OkDialog_You_have_only_rough_tune_optimized_compensation_voltages_;
                                var missing = documentExport.GetMissingCompensationVoltages(highestCoV).ToArray();
                                if (missing.Any())
                                {
                                    message = TextUtil.LineSeparate(message,
                                        Resources.ExportMethodDlg_OkDialog_You_are_missing_any_optimized_compensation_voltages_for_the_following_,
                                        TextUtil.LineSeparate(missing));
                                }
                                break;
                            }
                        case CompensationVoltageParameters.Tuning.none:
                            {
                                message = Resources.ExportMethodDlg_OkDialog_Your_document_does_not_contain_compensation_voltage_results__but_compensation_voltage_is_set_under_transition_settings_;
                                break;
                            }
                    }
                }
                else
                {
                    var missing = documentExport.GetMissingCompensationVoltages(CompensationVoltageParameters.Tuning.fine).ToArray();
                    if (missing.Any())
                    {
                        message = TextUtil.LineSeparate(
                            Resources.ExportMethodDlg_OkDialog_You_are_missing_compensation_voltages_for_the_following_,
                            TextUtil.LineSeparate(missing),
                            Resources.ExportMethodDlg_OkDialog_You_can_set_explicit_compensation_voltages_for_these__or_add_their_values_to_a_document_optimization_library_in_Transition_Settings_under_the_Prediction_tab_);
                    }
                }

                if (message != null)
                {
                    message = TextUtil.LineSeparate(message, Resources.ExportMethodDlg_OkDialog_Are_you_sure_you_want_to_continue_);
                    if (DialogResult.Cancel == MultiButtonMsgDlg.Show(this, message, Resources.ExportMethodDlg_OkDialog_OK))
                    {
                        return;
                    }
                }
            }

            if (outputPath == null)
            {
                string title = Text;
                string ext = TextUtil.EXT_CSV;
                string filter = Resources.ExportMethodDlg_OkDialog_Method_File;

                switch (_fileType)
                {
                    case ExportFileType.List:
                        filter = Resources.ExportMethodDlg_OkDialog_Transition_List;
                        ext = ExportInstrumentType.TransitionListExtension(_instrumentType);
                        break;

                    case ExportFileType.IsolationList:
                        filter = Resources.ExportMethodDlg_OkDialog_Isolation_List;
                        ext = ExportInstrumentType.IsolationListExtension(_instrumentType);
                        break;

                    case ExportFileType.Method:
                        title = string.Format(Resources.ExportMethodDlg_OkDialog_Export__0__Method, _instrumentType);
                        ext = ExportInstrumentType.MethodExtension(_instrumentType);
                        break;
                }

                using (var dlg = new SaveFileDialog
                    {
                        Title = title,
                        InitialDirectory = Settings.Default.ExportDirectory,
                        OverwritePrompt = true,
                        DefaultExt = ext,
                        Filter = TextUtil.FileDialogFilterAll(filter, ext)
                    })
                {
                    if (dlg.ShowDialog(this) == DialogResult.Cancel)
                    {
                        return;
                    }

                    outputPath = dlg.FileName;
                }
            }
            _exportProperties.PolarityFilter = comboPolarityFilter.Enabled
                ? (ExportPolarity)comboPolarityFilter.SelectedIndex
                : ExportPolarity.all;

            Settings.Default.ExportDirectory = Path.GetDirectoryName(outputPath);

            // Set ShowMessages property on ExportDlgProperties to true
            // so that we see the progress dialog during the export process
            var wasShowMessageValue = _exportProperties.ShowMessages;
            _exportProperties.ShowMessages = true;
            try
            {
                _exportProperties.ExportFile(_instrumentType, _fileType, outputPath, documentExport, templateName);
            }
            catch(UnauthorizedAccessException x)
            {
                MessageDlg.ShowException(this, x);
                _exportProperties.ShowMessages = wasShowMessageValue;
                return;
            }
            catch (IOException x)
            {
                MessageDlg.ShowException(this, x);
                _exportProperties.ShowMessages = wasShowMessageValue;
                return;
            }

            // Successfully completed dialog.  Store the values in settings.
            Settings.Default.ExportInstrumentType = _instrumentType;
            Settings.Default.ExportMethodStrategy = ExportStrategy.ToString();
            Settings.Default.ExportSortByMz = SortByMz;
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
            if (textPrimaryCount.Visible)
                Settings.Default.PrimaryTransitionCount = PrimaryCount;
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
            if (cbSureQuant.Visible)
                Settings.Default.ExportSureQuant = ExportSureQuant;
            if (textIntensityThreshold.Visible)
            {
                if (cbSureQuant.Checked)
                    Settings.Default.IntensityThresholdPercent = IntensityThresholdPercent.GetValueOrDefault();
                else
                    Settings.Default.IntensityThresholdValue = IntensityThresholdValue.GetValueOrDefault();
            }
            if (textIntensityThresholdMin.Visible)
                Settings.Default.IntensityThresholdMin = IntensityThresholdMin.GetValueOrDefault();
            if (cbExportEdcMass.Visible)
                Settings.Default.ExportEdcMass = ExportEdcMass;
            if (comboPolarityFilter.Enabled)
                Settings.Default.ExportPolarityFilterEnum = TypeSafeEnum.ValidateOrDefault((ExportPolarity)comboPolarityFilter.SelectedIndex, ExportPolarity.all).ToString();
            if (textMs1RepetitionTime.Visible)
                Settings.Default.ExportMs1RepetitionTime = _exportProperties.Ms1RepetitionTime;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool IsInSynchPredictor(string name, string namePrefix)
        {
            if (name == null)
                return false;
            if (name.StartsWith(namePrefix))
                return true;
            // SCIEX has had many prefixes
            if (namePrefix.Equals(ExportInstrumentType.ABI.Split(' ')[0]))
                return IsInSynchPredictor(name, @"AB") || IsInSynchPredictor(name, @"ABI");
            return false;
        }

        private static bool CheckAnalyzer(bool enabled, FullScanMassAnalyzerType analyzerType, params FullScanMassAnalyzerType[] analyzerTypesAccepted)
        {
            return !enabled || analyzerType == FullScanMassAnalyzerType.centroided || analyzerTypesAccepted.Contains(analyzerType);
        }

        /// <summary>
        /// This function will validate all the settings required for exporting a method,
        /// placing the values on the ExportDlgProperties _exportProperties. It returns
        /// boolean whether or not it succeeded. It can show MessageBoxes or not based
        /// on a parameter.
        /// </summary>
        public bool ValidateSettings(MessageBoxHelper helper)
        {
            // ReSharper disable ConvertIfStatementToConditionalTernaryExpression
            if (radioSingle.Checked)
                _exportProperties.ExportStrategy = ExportStrategy.Single;
            else if (radioProtein.Checked)
                _exportProperties.ExportStrategy = ExportStrategy.Protein;
            else
                _exportProperties.ExportStrategy = ExportStrategy.Buckets;
            // ReSharper restore ConvertIfStatementToConditionalTernaryExpression

            _exportProperties.SortByMz = cbSortByMz.Checked;
            _exportProperties.IgnoreProteins = cbIgnoreProteins.Checked;
            _exportProperties.FullScans = _document.Settings.TransitionSettings.FullScan.IsEnabledMsMs;
            _exportProperties.AddEnergyRamp = panelThermoColumns.Visible && cbEnergyRamp.Checked;
            _exportProperties.UseSlens = cbSlens.Checked;
            _exportProperties.WriteCompensationVoltages = cbWriteCoV.Checked;
            _exportProperties.AddTriggerReference = panelThermoColumns.Visible && cbTriggerRefColumns.Checked;
            _exportProperties.Tune3 = panelTuneColumns.Visible && cbTune3.Checked;

            _exportProperties.ExportMultiQuant = panelAbSciexTOF.Visible && cbExportMultiQuant.Checked;
            _exportProperties.ExportSureQuant = cbSureQuant.Visible && cbSureQuant.Checked;

            _exportProperties.RetentionStartAndEnd = panelThermoRt.Visible && cbUseStartAndEndRts.Checked;

            _exportProperties.ExportEdcMass = panelWaters.Visible && cbExportEdcMass.Checked;

            _exportProperties.Ms1Scan = _document.Settings.TransitionSettings.FullScan.IsEnabledMs &&
                                        _document.Settings.TransitionSettings.FullScan.IsEnabledMsMs;

            _exportProperties.InclusionList = IsInclusionListMethod;

            _exportProperties.MsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _document.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer);
            _exportProperties.MsMsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _document.Settings.TransitionSettings.FullScan.ProductMassAnalyzer);

            _exportProperties.PrimaryTransitionCount = 0;

            _exportProperties.OptimizeType = null;
            if (comboOptimizing.SelectedItem != null)
            {
                var optimizeTypeCombo = comboOptimizing.SelectedItem.ToString();
                if (!Equals(optimizeTypeCombo, ExportOptimize.NONE))
                    _exportProperties.OptimizeType = optimizeTypeCombo;
            }
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
            else if (Equals(_exportProperties.OptimizeType, ExportOptimize.COV))
            {
                string tuning = comboTuning.SelectedItem.ToString();
                _exportProperties.OptimizeType = tuning;

                var compensationVoltage = prediction.CompensationVoltage;
                var tuneLevel = CompensationVoltageParameters.GetTuneLevel(tuning);

                if (helper.ShowMessages)
                {
                    if (tuneLevel.Equals(CompensationVoltageParameters.Tuning.medium))
                    {
                        var missing = _document.GetMissingCompensationVoltages(CompensationVoltageParameters.Tuning.rough).ToList();
                        if (missing.Any())
                        {
                            missing.Insert(0, Resources.ExportMethodDlg_ValidateSettings_Cannot_export_medium_tune_transition_list__The_following_precursors_are_missing_rough_tune_results_);
                            helper.ShowTextBoxError(comboTuning, TextUtil.LineSeparate(missing));
                            return false;
                        }
                    }
                    else if (tuneLevel.Equals(CompensationVoltageParameters.Tuning.fine))
                    {
                        var missing = _document.GetMissingCompensationVoltages(CompensationVoltageParameters.Tuning.medium).ToList();
                        if (missing.Any())
                        {
                            missing.Insert(0, Resources.ExportMethodDlg_ValidateSettings_Cannot_export_fine_tune_transition_list__The_following_precursors_are_missing_medium_tune_results_);
                            helper.ShowTextBoxError(comboTuning, TextUtil.LineSeparate(missing));
                            return false;
                        }
                    }
                }

                _exportProperties.OptimizeStepSize = compensationVoltage.GetStepSize(tuneLevel);
                _exportProperties.OptimizeStepCount = compensationVoltage.GetStepCount(tuneLevel);
                _exportProperties.PrimaryTransitionCount = 1;
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
                    helper.ShowTextBoxError(textMaxTransitions, Resources.ExportMethodDlg_ValidateSettings__0__must_contain_a_value);
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
            else if (!helper.ValidateNumberTextBox(textMaxTransitions, minTrans, maxInstrumentTrans, out maxVal))
                return false;

            // Make sure all the transitions of all precursors can fit into a single document,
            // but not if this is a full-scan instrument, because then the maximum is refering
            // to precursors and not transitions.
            if (!IsFullScanInstrument && !ValidatePrecursorFit(_document, maxVal, helper.ShowMessages))
                return false;
            _exportProperties.MaxTransitions = maxVal;

            _exportProperties.MethodType = ExportMethodTypeExtension.GetEnum(comboTargetType.SelectedItem.ToString());

            if (textPrimaryCount.Visible)
            {
                int primaryCount;
                if (!helper.ValidateNumberTextBox(textPrimaryCount, AbstractMassListExporter.PRIMARY_COUNT_MIN, AbstractMassListExporter.PRIMARY_COUNT_MAX, out primaryCount))
                    return false;

                _exportProperties.PrimaryTransitionCount = primaryCount;
            }
            if (textDwellTime.Visible)
            {
                int dwellTime;
                if (!helper.ValidateNumberTextBox(textDwellTime, AbstractMassListExporter.DWELL_TIME_MIN, AbstractMassListExporter.DWELL_TIME_MAX, out dwellTime))
                    return false;

                _exportProperties.DwellTime = dwellTime;
            }

            _exportProperties.IntensityThresholdPercent = null;
            _exportProperties.IntensityThresholdValue = null;
            if (textIntensityThreshold.Visible)
            {
                var surequant = cbSureQuant.Checked;
                if (!helper.ValidateDecimalTextBox(textIntensityThreshold, 0, surequant ? (double?) 100 : null, out var intensityThreshold))
                    return false;

                if (surequant)
                    _exportProperties.IntensityThresholdPercent = intensityThreshold;
                else
                    _exportProperties.IntensityThresholdValue = intensityThreshold;
            }
            if (textIntensityThresholdMin.Visible)
            {
                if (!helper.ValidateDecimalTextBox(textIntensityThresholdMin, 0, null, out var intensityThresholdMin))
                    return false;

                _exportProperties.IntensityThresholdMin = intensityThresholdMin;
            }

            if (textRunLength.Visible)
            {
                double runLength;
                if (!helper.ValidateDecimalTextBox(textRunLength, AbstractMassListExporter.RUN_LENGTH_MIN, AbstractMassListExporter.RUN_LENGTH_MAX, out runLength, false))
                    return false;

                _exportProperties.RunLength = runLength;
            }

            if (textMs1RepetitionTime.Visible)
            {
                if (!helper.ValidateDecimalTextBox(textMs1RepetitionTime, 0, null, out var ms1RepetitionTime))
                    return false;

                _exportProperties.Ms1RepetitionTime = ms1RepetitionTime;
            }

            // If export method type is scheduled, and allows multiple scheduling options
            // ask the user which to use.
            if (_exportProperties.MethodType != ExportMethodType.Standard && HasMultipleSchedulingOptions(_document))
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
                    using (var schedulingOptionsDlg = new SchedulingOptionsDlg(_document, i =>
                            _exportProperties.MethodType != ExportMethodType.Triggered || CanTriggerReplicate(i)))
                    {
                        if (schedulingOptionsDlg.ShowDialog(this) != DialogResult.OK)
                            return false;

                        SchedulingAlgorithm = schedulingOptionsDlg.Algorithm;
                        SchedulingReplicateNum = schedulingOptionsDlg.ReplicateNum;
                    }
                }
            }

            if (ExportOptimize.CompensationVoltageTuneTypes.Contains(_exportProperties.OptimizeType))
            {
                var precursorsMissingRanks = _document.GetPrecursorsWithoutTopRank(0, _exportProperties.SchedulingReplicateNum).ToArray();
                if (precursorsMissingRanks.Any())
                {
                    if (helper.ShowMessages)
                    {
                        if (DialogResult.Cancel == MultiButtonMsgDlg.Show(this, TextUtil.LineSeparate(
                            Resources.ExportMethodDlg_OkDialog_Compensation_voltage_optimization_should_be_run_on_one_transition_per_peptide__and_the_best_transition_cannot_be_determined_for_the_following_precursors_,
                            TextUtil.LineSeparate(precursorsMissingRanks),
                            Resources.ExportMethodDlg_OkDialog_Provide_transition_ranking_information_through_imported_results__a_spectral_library__or_choose_only_one_target_transition_per_precursor_),
                            Resources.ExportMethodDlg_OkDialog_OK))
                        {
                            return false;
                        }
                    }
                    _exportProperties.PrimaryTransitionCount = 0;
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
            foreach (var nodeGroup in document.MoleculeTransitionGroups)
            {
                int tranRequired = nodeGroup.Children.Count;

                if ((Equals(OptimizeType, ExportOptimize.COV) || ExportOptimize.CompensationVoltageTuneTypes.Contains(OptimizeType)) && PrimaryCount > 0)
                {
                    tranRequired = PrimaryCount;
                }

                if (OptimizeType != null)
                    tranRequired *= OptimizeStepCount * 2 + 1;
                if (tranRequired > maxTransitions)
                {
                    if (showMessages)
                    {
                        string messageFormat = (OptimizeType == null ?
                            Resources.ExportMethodDlg_ValidatePrecursorFit_The_precursor__0__for__1__has__2__transitions__which_exceeds_the_current_maximum__3__ :
                            Resources.ExportMethodDlg_ValidatePrecursorFit_The_precursor__0__for__1__requires__2__transitions_to_optimize__which_exceeds_the_current_maximum__3__);
                        string targetName = nodeGroup.TransitionGroup.Peptide.TextId;

                        MessageDlg.Show(this, string.Format(messageFormat,
                            SequenceMassCalc.PersistentMZ(nodeGroup.PrecursorMz) + Transition.GetChargeIndicator(nodeGroup.TransitionGroup.PrecursorAdduct),
                            targetName,
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
                return IsFullScanInstrument && IsDiaFullScan;
            }
        }

        private bool IsDiaFullScan
        {
            get
            {
                return _document.Settings.TransitionSettings.FullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA;
            }
        }

        private void StrategyCheckChanged()
        {
            if (IsDia && !radioSingle.Checked)
            {
                MessageDlg.Show(this, Resources.ExportMethodDlg_StrategyCheckChanged_Only_one_method_can_be_exported_in_DIA_mode);
                radioSingle.Checked = true;
            }
            else if (Equals(InstrumentType, ExportInstrumentType.BRUKER_TIMSTOF) && radioBuckets.Checked)
            {
                MessageDlg.Show(this, string.Format(Resources.ExportMethodDlg_StrategyCheckChanged_Multiple_methods_is_not_yet_supported_for__0__, InstrumentType));
                radioSingle.Checked = true;
            }

            textMaxTransitions.Enabled = radioBuckets.Checked;
            if (!textMaxTransitions.Enabled)
                textMaxTransitions.Clear();
            cbIgnoreProteins.Enabled = radioBuckets.Checked;
            if (!radioBuckets.Checked)
                cbIgnoreProteins.Checked = false;
            else if (!Equals(comboTargetType.SelectedItem, ExportMethodType.Standard.GetLocalizedString()))
                cbIgnoreProteins.Checked = true;

            if (radioSingle.Checked)
            {
                labelMethodNum.Text = 1.ToString(LocalizationHelper.CurrentCulture);
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
                MessageDlg.Show(this, string.Format(Resources.ExportMethodDlg_comboInstrument_SelectedIndexChanged_Export_of_DIA_isolation_lists_is_not_yet_supported_for__0__,
                                                    _instrumentType));
                comboInstrument.SelectedItem = ExportInstrumentType.THERMO_Q_EXACTIVE;
                return;
            }

            if (wasFullScanInstrument != IsFullScanInstrument)
                UpdateMaxTransitions();

            MethodTemplateFile templateFile;
            textTemplateFile.Text = Settings.Default.ExportMethodTemplateList.TryGetValue(_instrumentType, out templateFile)
                ? templateFile.FilePath
                : string.Empty;

            var targetType = ExportMethodTypeExtension.GetEnum(comboTargetType.SelectedItem.ToString());
            if (targetType == ExportMethodType.Triggered && !CanTrigger && CanSchedule)
            {
                comboTargetType.SelectedItem = ExportMethodType.Scheduled.GetLocalizedString();
                // Change in target type will update the instrument controls and calc method count
                return;
            }
            if (targetType != ExportMethodType.Standard && !CanSchedule)
            {
                comboTargetType.SelectedItem = ExportMethodType.Standard.GetLocalizedString();
                // Change in target type will update the instrument controls and calc method count
                return;
            }                

            // Always keep the comboTargetType (Method type) enabled. Throw and error if the 
            // user selects "Scheduled" or "Triggered" and it is not supported by the instrument.
            // comboTargetType.Enabled = CanScheduleInstrumentType;
            
            UpdateInstrumentControls(targetType);

            CalcMethodCount();

            UpdateCovControls();
        }

        private void comboPolarityFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            _exportProperties.PolarityFilter = TypeSafeEnum.ValidateOrDefault((ExportPolarity)comboPolarityFilter.SelectedIndex, ExportPolarity.all);
            CalcMethodCount();
        }

        private void comboTargetType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var targetType = ExportMethodTypeExtension.GetEnum(comboTargetType.SelectedItem.ToString());
            bool standard = (targetType == ExportMethodType.Standard);
            bool triggered = (targetType == ExportMethodType.Triggered);
            if (!standard && !VerifySchedulingAllowed(triggered))
            {
                comboTargetType.SelectedItem = ExportMethodType.Standard.GetLocalizedString();
                targetType = ExportMethodType.Standard;
            }

            UpdateInstrumentControls(targetType);

            CalcMethodCount();
        }
        private void cbSureQuant_CheckedChanged(object sender, EventArgs e)
        {
            UpdateThermoSureQuantControls();
        }

        private void UpdateInstrumentControls(ExportMethodType targetType)
        {
            bool standard = (targetType == ExportMethodType.Standard);
            bool triggered = (targetType == ExportMethodType.Triggered);

            btnGraph.Visible = !standard;

            if (!standard && cbIgnoreProteins.Enabled)
            {
                cbIgnoreProteins.Checked = true;
            }
            if (triggered && !(InstrumentType == ExportInstrumentType.ABI || InstrumentType == ExportInstrumentType.ABI_QTRAP))
            {
                comboOptimizing.Enabled = false;
            }
            else
            {
                comboOptimizing.Enabled = !IsFullScanInstrument;
            }
            if (!comboOptimizing.Enabled)
            {
                OptimizeType = ExportOptimize.NONE;
            }

            UpdateTriggerControls(targetType);
            UpdateDwellControls(standard);
            UpdateThermoColumns(targetType);
            UpdateAbSciexControls();
            UpdateWatersControls();
            UpdateBrukerTimsTofControls();
            UpdateThermoRtControls(targetType);
            UpdateThermoSLensControl(targetType);
            UpdateThermoFaimsCvControl();
            UpdateThermoSureQuantControls();
            UpdateMaxLabel(standard);
        }

        private void textPrimaryCount_TextChanged(object sender, EventArgs e)
        {
            CalcMethodCount();
        }

        private bool VerifySchedulingAllowed(bool triggered)
        {
            if (IsDia)
            {
                MessageDlg.Show(this, Resources.ExportMethodDlg_comboTargetType_SelectedIndexChanged_Scheduled_methods_are_not_yet_supported_for_DIA_acquisition);
                return false;
            }
            if (triggered)
            {
                if (!CanTriggerInstrumentType)
                {
                    // Give a clearer message for the Thermo TSQ, since it does actually support triggered acquisition,
                    // but we are unable to export directly to mehtods.
                    if (Equals(InstrumentType, ExportInstrumentType.THERMO_TSQ))
                        MessageDlg.Show(this, TextUtil.LineSeparate(string.Format(Resources.ExportMethodDlg_VerifySchedulingAllowed_The__0__instrument_lacks_support_for_direct_method_export_for_triggered_acquisition_, InstrumentType),
                                                                    string.Format(Resources.ExportMethodDlg_VerifySchedulingAllowed_You_must_export_a__0__transition_list_and_manually_import_it_into_a_method_file_using_vendor_software_, ExportInstrumentType.THERMO)));
                    else
                        MessageDlg.Show(this, string.Format(Resources.ExportMethodDlg_VerifySchedulingAllowed_The_instrument_type__0__does_not_support_triggered_acquisition_, InstrumentType));
                    return false;
                }
                if (!_document.Settings.HasResults && !_document.Settings.HasLibraries)
                {
                    MessageDlg.Show(this, Resources.ExportMethodDlg_VerifySchedulingAllowed_Triggered_acquistion_requires_a_spectral_library_or_imported_results_in_order_to_rank_transitions_);
                    return false;
                }
                if (!CanTrigger)
                {
                    MessageDlg.Show(this, Resources.ExportMethodDlg_VerifySchedulingAllowed_The_current_document_contains_peptides_without_enough_information_to_rank_transitions_for_triggered_acquisition_);
                    return false;
                }
            }
            if (!CanSchedule)
            {
                var prediction = _document.Settings.PeptideSettings.Prediction;

                // The "Method type" combo box is always enabled.  Display error message if the user
                // selects "Scheduled" for an instrument that does not support scheduled methods (e.g LTQ, ABI TOF)
                // However, if we are exporting inclusion lists (MS1 filtering enabled AND MS2 filtering disabled) 
                // the user should be able to select "Scheduled" for LTQ and ABI TOF instruments.
                if (!CanScheduleInstrumentType)
                {
                    MessageDlg.Show(this, SCHED_NOT_SUPPORTED_ERR_TXT);
                }
                else if (prediction.RetentionTime == null)
                {
                    if (prediction.UseMeasuredRTs)
                        MessageDlg.Show(this, Resources.ExportMethodDlg_comboTargetType_SelectedIndexChanged_To_export_a_scheduled_list_you_must_first_choose_a_retention_time_predictor_in_Peptide_Settings_Prediction_or_import_results_for_all_peptides_in_the_document);
                    else
                        MessageDlg.Show(this, Resources.ExportMethodDlg_comboTargetType_SelectedIndexChanged_To_export_a_scheduled_list_you_must_first_choose_a_retention_time_predictor_in_Peptide_Settings_Prediction);
                }
                else if (!prediction.RetentionTime.Calculator.IsUsable)
                {
                    MessageDlg.Show(this, TextUtil.LineSeparate(
                        Resources.ExportMethodDlg_comboTargetType_SelectedIndexChanged_Retention_time_prediction_calculator_is_unable_to_score,
                        Resources.ExportMethodDlg_comboTargetType_SelectedIndexChanged_Check_the_calculator_settings));
                }
                else if (!prediction.RetentionTime.IsUsable)
                {
                    MessageDlg.Show(this, TextUtil.LineSeparate(
                        Resources.ExportMethodDlg_comboTargetType_SelectedIndexChanged_Retention_time_predictor_is_unable_to_auto_calculate_a_regression,
                        Resources.ExportMethodDlg_comboTargetType_SelectedIndexChanged_Check_to_make_sure_the_document_contains_times_for_all_of_the_required_standard_peptides));
                }
                else
                {
                    MessageDlg.Show(this, Resources.ExportMethodDlg_comboTargetType_SelectedIndexChanged_To_export_a_scheduled_list_you_must_first_import_results_for_all_peptides_in_the_document);
                }
                return false;
            }
            return true;
        }

        private void UpdateTriggerControls(ExportMethodType targetType)
        {
            panelTriggered.Visible = (targetType == ExportMethodType.Triggered);
        }

        private void UpdateMaxLabel(bool standard)
        {
            if (standard)
            {
                labelMaxTransitions.Text = IsFullScanInstrument
                    ? PREC_PER_SAMPLE_INJ_TXT
                    : TRANS_PER_SAMPLE_INJ_TXT;
            }
            else
            {
                labelMaxTransitions.Text = IsFullScanInstrument
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
                // Will we split on polarity?
                var polaritiesCount = _exportProperties.PolarityFilter == ExportPolarity.separate ? 2 : 1;
                labelMethodNum.Text = polaritiesCount.ToString(LocalizationHelper.CurrentCulture);
                return;
            }

            if (_recalcMethodCountStatus != RecalcMethodCountStatus.waiting || !IsHandleCreated)
            {
                _recalcMethodCountStatus = RecalcMethodCountStatus.pending;
                return;
            }

            var helper = new MessageBoxHelper(this, false);

            if (!ValidateSettings(helper) || comboInstrument.SelectedItem == null)
            {
                labelMethodNum.Text = string.Empty;
                return;
            }

            if (radioSingle.Checked)
            {
                labelMethodNum.Text = 1.ToString();
                return;
            }

            labelMethodNum.Text = @"...";

            _recalcMethodCountStatus = RecalcMethodCountStatus.running;

            string instrument = comboInstrument.SelectedItem.ToString();
//            var recalcMethodCount = new RecalcMethodCountCaller(RecalcMethodCount);
//            recalcMethodCount.BeginInvoke(_exportProperties, instrument, _fileType, _document, recalcMethodCount.EndInvoke, null);
            ActionUtil.RunAsync(() => RecalcMethodCount(_exportProperties, instrument, _fileType, _document), @"Method Counter");
        }

//        private delegate void RecalcMethodCountCaller(ExportDlgProperties exportProperties,
//            string instrument, ExportFileType fileType, SrmDocument document);

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
            if (exporter != null && exporter.MemoryOutput != null)
                methodCount = exporter.MemoryOutput.Count;
            // Switch back to the UI thread to update the form
            try
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
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
                ? methodCount.Value.ToString(LocalizationHelper.CurrentCulture)
                : string.Empty;

            var recalcMethodCountStatus = _recalcMethodCountStatus;
            _recalcMethodCountStatus = RecalcMethodCountStatus.waiting;
            if (recalcMethodCountStatus == RecalcMethodCountStatus.pending)
                CalcMethodCount();
        }

        private void UpdateDwellControls(bool standard)
        {
            bool showDwell = false;
            bool showRunLength = false;
            if (standard)
            {
                if (!IsSingleDwellInstrument && !IsDia)
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
            labelDwellTime.TabIndex = textRunLength.TabIndex-1;
            textDwellTime.Visible = showDwell;
            textDwellTime.TabIndex = textRunLength.TabIndex;
            textRunLength.Visible = showRunLength;
        }

        private void btnBrowseTemplate_Click(object sender, EventArgs e)
        {
            string templateName = textTemplateFile.Text;
            if (Equals(InstrumentType, ExportInstrumentType.AGILENT6400) ||
                Equals(InstrumentType, ExportInstrumentType.BRUKER_TOF))
            {
                using (var chooseDirDialog = new FolderBrowserDialog
                    {
                        Description = Resources.ExportMethodDlg_btnBrowseTemplate_Click_Method_Template,
                    })
                {
                    if (!string.IsNullOrEmpty(templateName))
                    {
                        chooseDirDialog.SelectedPath = templateName;
                    }

                    if (chooseDirDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        templateName = chooseDirDialog.SelectedPath;
                        if (Equals(InstrumentType, ExportInstrumentType.AGILENT6400) &&
                            !AgilentMethodExporter.IsAgilentMethodPath(templateName))
                        {
                            MessageDlg.Show(this, Resources.ExportMethodDlg_btnBrowseTemplate_Click_The_chosen_folder_does_not_appear_to_contain_an_Agilent_QQQ_method_template_The_folder_is_expected_to_have_a_m_extension_and_contain_the_file_qqqacqmethod_xsd);
                            return;
                        }
                        else if (Equals(InstrumentType, ExportInstrumentType.BRUKER_TOF) &&
                                 !BrukerMethodExporter.IsBrukerMethodPath(templateName))
                        {
                            MessageDlg.Show(this, Resources.ExportMethodDlg_btnBrowseTemplate_Click_The_chosen_folder_does_not_appear_to_contain_a_Bruker_TOF_method_template___The_folder_is_expected_to_have_a__m_extension__and_contain_the_file_submethods_xml_);
                            return;
                        }
                        textTemplateFile.Text = templateName;
                    }
                }

                return;
            }

            using (var openFileDialog = new OpenFileDialog
                {
                    Title = Resources.ExportMethodDlg_btnBrowseTemplate_Click_Method_Template,
                    // Extension based on currently selected type
                    CheckPathExists = true
                })
            {
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
                    listFileTypes.Add(MethodFilter(ExportInstrumentType.EXT_AB_SCIEX));
                }
                else if (Equals(InstrumentType, ExportInstrumentType.BRUKER_TIMSTOF))
                {
                    listFileTypes.Add(MethodFilter(ExportInstrumentType.EXT_BRUKER_TIMSTOF));
                }
                else if (Equals(InstrumentType, ExportInstrumentType.SHIMADZU))
                {
                    listFileTypes.Add(MethodFilter(ExportInstrumentType.EXT_SHIMADZU));
                }
                else if (Equals(InstrumentType, ExportInstrumentType.THERMO_TSQ) ||
                         Equals(InstrumentType, ExportInstrumentType.THERMO_LTQ))
                {
                    listFileTypes.Add(MethodFilter(ExportInstrumentType.EXT_THERMO));
                }
                else if (Equals(InstrumentType, ExportInstrumentType.WATERS_XEVO_TQ) ||
                         Equals(InstrumentType, ExportInstrumentType.WATERS_QUATTRO_PREMIER))
                {
                    listFileTypes.Add(MethodFilter(ExportInstrumentType.EXT_WATERS));
                }
                openFileDialog.Filter = TextUtil.FileDialogFiltersAll(listFileTypes.ToArray());

                if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    textTemplateFile.Text = openFileDialog.FileName;
                }
            }
        }

        private string MethodFilter(string ext)
        {
            return TextUtil.FileDialogFilter(string.Format(Resources.ExportMethodDlg_btnBrowseTemplate_Click__0__Method, InstrumentType), ext);
        }

        private void textMaxTransitions_TextChanged(object sender, EventArgs e)
        {
            int maxTrans;
            if(!int.TryParse(textMaxTransitions.Text, out maxTrans) || maxTrans < 1)
            {
                labelMethodNum.Text = string.Empty;
                return;
            }

            CalcMethodCount();
        }

        private void comboOptimizing_SelectedIndexChanged(object sender, EventArgs e)
        {
            CalcMethodCount();
            panelSciexTune.Visible = comboOptimizing.SelectedItem.ToString().Equals(ExportOptimize.COV);
            UpdateThermoFaimsCvControl();

            // Set the tooltip
            var tooltip = Resources.ExportMethodDlg_comboOptimizing_SelectedIndexChanged_Export_a_method_with_extra_transitions_for_finding_an_optimal_value_;
            int? stepCount = null;
            if (comboOptimizing.SelectedItem.ToString().Equals(ExportOptimize.CE))
            {
                stepCount = _document.Settings.TransitionSettings.Prediction.CollisionEnergy.StepCount;
            }
            else if (comboOptimizing.SelectedItem.ToString().Equals(ExportOptimize.DP))
            {
                stepCount = _document.Settings.TransitionSettings.Prediction.DeclusteringPotential.StepCount;
            }
            else if (comboOptimizing.SelectedItem.ToString().Equals(ExportOptimize.COV))
            {
                stepCount = _document.Settings.TransitionSettings.Prediction.CompensationVoltage.StepCount;
            }

            if (stepCount.HasValue)
            {
                tooltip = TextUtil.LineSeparate(tooltip,
                    string.Format(
                        Resources.ExportMethodDlg_comboOptimizing_SelectedIndexChanged_Optimizing_for__0__will_produce_an_additional__1__transitions_per_transition_,
                        comboOptimizing.SelectedItem, stepCount * 2));
            }
            helpTip.SetToolTip(comboOptimizing, tooltip);
        }

        private void cbIgnoreProteins_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbIgnoreProteins.Checked && radioBuckets.Checked && !Equals(comboTargetType.SelectedItem, ExportMethodType.Standard.GetLocalizedString()))
            {
                cbIgnoreProteins.Checked = true;
                MessageDlg.Show(this, Resources.ExportMethodDlg_cbIgnoreProteins_CheckedChanged_Grouping_peptides_by_protein_has_not_yet_been_implemented_for_scheduled_methods_);
            }
        }
        
        private void btnGraph_Click(object sender, EventArgs e)
        {
            ShowSchedulingGraph();
        }

        public void ShowSchedulingGraph()
        {
            var brukerTemplate = Equals(InstrumentType, ExportInstrumentType.BRUKER_TIMSTOF)
                ? textTemplateFile.Text
                : null;
            BrukerTimsTofMethodExporter.Metrics brukerMetrics = null;

            if (!string.IsNullOrEmpty(brukerTemplate))
            {
                if (!File.Exists(brukerTemplate))
                {
                    MessageDlg.Show(this,
                        string.Format(Resources.ExportMethodDlg_OkDialog_The_template_file__0__does_not_exist,
                            brukerTemplate));
                    return;
                }

                using (var longWait = new LongWaitDlg())
                {
                    longWait.PerformWork(this, 800, progress =>
                    {
                        var exportProperties = new ExportDlgProperties(this, new CancellationToken());
                        exportProperties.MethodType = ExportMethodType.Scheduled;
                        brukerMetrics = BrukerTimsTofMethodExporter.GetSchedulingMetrics(_document, exportProperties, brukerTemplate, progress);
                    });
                }
            }
            
            using (var dlg = new ExportMethodScheduleGraph(_document, brukerTemplate, brukerMetrics))
            {
                if (dlg.Exception != null)
                {
                    MessageDlg.ShowWithException(this, Resources.ExportMethodDlg_btnGraph_Click_An_error_occurred_, dlg.Exception);
                    return;
                }
                dlg.ShowDialog(this);
            }
        }

        #region Functional Test Support

        public class TransitionListView : IFormView { }
        public class IsolationListView : IFormView { }
        public class MethodView : IFormView { }

        public IFormView ShowingFormView
        {
            get
            {
                switch (_fileType)
                {
                    case ExportFileType.List:
                        return new TransitionListView();
                    case ExportFileType.IsolationList:
                        return new IsolationListView();
                    default:
                        return new MethodView();
                }
            }
        }

        public void SetTemplateFile(string templateFile, bool setEndCaret = false)
        {
            textTemplateFile.Text = templateFile;
            if (setEndCaret)
                textTemplateFile.Select(templateFile.Length, 0);
        }

        public void SetInstrument(string instrument)
        {
            if(ExportInstrumentType.TRANSITION_LIST_TYPES.ToList().Find(inst => Equals(inst, instrument)) == default(string))
                return;

            comboInstrument.SelectedText = instrument;
        }

        public void SetMethodType(ExportMethodType type)
        {
            comboTargetType.SelectedItem = type == ExportMethodType.Standard
                                               ? Resources.ExportMethodDlg_SetMethodType_Standard
                                               : Resources.ExportMethodDlg_SetMethodType_Scheduled;
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

        public bool IsPrimaryCountVisible
        {
            get { return textPrimaryCount.Visible; }
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

        public bool IsThermoStartAndEndTime
        {
            get { return cbUseStartAndEndRts.Checked; }
            set { cbUseStartAndEndRts.Checked = value; }
        }

        #endregion
    }

    public class ExportDlgProperties : ExportProperties, IProgressMonitor
    {
        private readonly ExportMethodDlg _dialog;
        private readonly CancellationToken _cancellation;

        public ExportDlgProperties(ExportMethodDlg dialog, CancellationToken cancellationToken)
        {
            _dialog = dialog;
            _cancellation = cancellationToken;
        }

        public bool ShowMessages { get; set; }

        public override void PerformLongExport(Action<IProgressMonitor> performExport)
        {
            if (!ShowMessages)
            {
                performExport(this);
                return;
            }

            using (var longWait = new LongWaitDlg
                    {
                        Text = Resources.ExportDlgProperties_PerformLongExport_Exporting_Methods
                    })
            {
                try
                {
                    var status = longWait.PerformWork(_dialog, 800, performExport);
                    if (status.IsError)
                        MessageDlg.ShowException(_dialog, status.ErrorException);
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(_dialog, TextUtil.LineSeparate(Resources.ExportDlgProperties_PerformLongExport_An_error_occurred_attempting_to_export,
                                                                   x.Message), x);
                }
            }
        }

        public bool IsCanceled
        {
            get { return _cancellation.IsCancellationRequested; }
        }

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            return UpdateProgressResponse.normal;
        }

        public bool HasUI
        {
            get { return false; }
        }
    }
}
