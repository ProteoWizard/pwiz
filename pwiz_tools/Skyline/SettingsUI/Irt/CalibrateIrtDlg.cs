/*
 * Original author: John Chilton <jchilton .at. uw.edu>,
 *                  Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class CalibrateIrtDlg : FormEx
    {
        public const int MIN_STANDARD_PEPTIDES = 5;
        public const int MIN_SUGGESTED_STANDARD_PEPTIDES = 10;

        private readonly CalibrationGridViewDriver _gridViewDriver;
        private bool FireStandardsChanged { get; set; }

        private readonly IrtStandard _standard;
        private readonly DbIrtPeptide[] _standardPeptidesSorted;
        private readonly IEnumerable<IrtStandard> _existing;
        private readonly DbIrtPeptide[] _updatePeptides;

        public CalibrateIrtDlg(IrtStandard standard, IEnumerable<IrtStandard> existing, DbIrtPeptide[] updatePeptides)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _standard = standard;
            _standardPeptidesSorted = standard != null ? standard.Peptides.OrderBy(pep => pep.Irt).ToArray() : new DbIrtPeptide[0];
            _existing = existing;
            _updatePeptides = updatePeptides;

            _gridViewDriver = new CalibrationGridViewDriver(this, gridViewCalibrate, bindingSourceStandard,
                new SortableBindingList<StandardPeptide>());

            FireStandardsChanged = false; // wait until after OnLoad, otherwise ModeUIAwareFormHelper causes it to fire with small molecules

            comboRegression.Items.AddRange(RegressionOption.All(Program.ActiveDocumentUI).ToArray());
            comboRegression.SelectedIndex = 0;

            if (IsRecalibration)
            {
                textName.Text = standard?.Name;
                FormBorderStyle = FormBorderStyle.Fixed3D;
                panelPeptides.Hide();
                btnUseCurrent.Hide();
                Height -= panelPeptides.Height;
                var standardPeptides = _standardPeptidesSorted.Select(pep => pep.PeptideModSeq).ToArray();
                comboMinPeptide.Items.AddRange(standardPeptides);
                comboMaxPeptide.Items.AddRange(standardPeptides);
                if (standardPeptides.Length > 0)
                {
                    // Look for standard peptides with whole number values as the suggested fixed points
                    var iFixed1 = _standardPeptidesSorted.IndexOf(pep => Math.Round(pep.Irt, 8) == Math.Round(pep.Irt));
                    var iFixed2 = _standardPeptidesSorted.LastIndexOf(pep => Math.Round(pep.Irt, 8) == Math.Round(pep.Irt));
                    if (iFixed1 == -1 || iFixed2 == -1)
                    {
                        iFixed1 = 0;
                        iFixed2 = _standardPeptidesSorted.Length - 1;
                    }
                    else if (iFixed1 == iFixed2)
                    {
                        if (iFixed1 < _standardPeptidesSorted.Length / 2)
                        {
                            iFixed2 = _standardPeptidesSorted.Length - 1;
                        }
                        else
                        {
                            iFixed1 = 0;
                        }
                    }
                    comboMinPeptide.SelectedIndex = iFixed1;
                    comboMaxPeptide.SelectedIndex = iFixed2;
                    SetIrtRange(_standardPeptidesSorted[iFixed1].Irt, _standardPeptidesSorted[iFixed2].Irt);
                }
            }
        }

        public IrtStandard IrtStandard { get; private set; }

        public SortableBindingList<StandardPeptide> StandardPeptideList { get { return _gridViewDriver.Items; } }
        public StandardPeptide[] StandardPeptidesSorted => StandardPeptideList.OrderBy(pep => pep.RetentionTime).ToArray();

        public int StandardPeptideCount { get { return StandardPeptideList.Count; } }

        public bool IsRecalibration => _standard != null;

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
            gridViewCalibrate.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void CalibrateIrtDlg_Shown(object sender, EventArgs e)
        {
            FireStandardsChanged = true;
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            if (!helper.ValidateNameTextBox(textName, out var name))
            {
                return;
            }
            else if (_existing.Contains(r => !ReferenceEquals(_standard, r) && Equals(name, r.Name)))
            {
                helper.ShowTextBoxError(textName, Resources.CalibrateIrtDlg_OkDialog_The_iRT_standard__0__already_exists_, name);
                return;
            }

            if (!TryGetRegression(true, out var regression))
            {
                return;
            }

            if (!IsRecalibration)
            {
                IrtStandard = new IrtStandard(name, null, null,
                    StandardPeptideList.Select(pep => new DbIrtPeptide(pep.Target, pep.Irt, true, TimeSource.peak)));
            }
            else
            {
                foreach (var pep in _updatePeptides)
                {
                    pep.Irt = regression.GetY(pep.Irt);
                }
                IrtStandard = new IrtStandard(name, null, null,
                    _standard.Peptides.Select(pep =>
                        new DbIrtPeptide(pep.Target, regression.GetY(pep.Irt), true, TimeSource.peak)));
            }
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnUseCurrent_Click(object sender, EventArgs e)
        {
            UseResults();
        }

        private bool TryGetRegression(bool showErrors, out IIrtRegression regression)
        {
            if (!SelectedRegressionOption.FixedPoint)
            {
                regression = SelectedRegressionOption.Regression;
                return true;
            }

            regression = null;
            var helper = new MessageBoxHelper(this, showErrors);

            if (!helper.ValidateDecimalTextBox(textMinIrt, SelectedRegressionOption.Regression is LogRegression ? (double?) 0.01 : null, null, out var minIrt))
                return false;
            if (!helper.ValidateDecimalTextBox(textMaxIrt, minIrt, null, out var maxIrt))
                return false;
            if (!IsRecalibration && StandardPeptideList.Count == 0)
            {
                if (showErrors)
                {
                    MessageDlg.Show(this, Resources.CalibrateIrtDlg_TryGetLine_Standard_calibration_peptides_are_required_);
                }
                return false;
            }

            var comboMinIdx = comboMinPeptide.SelectedIndex;
            var comboMaxIdx = comboMaxPeptide.SelectedIndex;
            if (comboMinIdx < 0 || comboMaxIdx < 0)
            {
                if (showErrors)
                {
                    MessageDlg.Show(this, Resources.CalibrateIrtDlg_TryGetLine_Invalid_fixed_point_peptides_);
                }
                return false;
            }

            double minRt, maxRt;
            if (!IsRecalibration)
            {
                minRt = StandardPeptidesSorted[comboMinIdx].RetentionTime;
                maxRt = StandardPeptidesSorted[comboMaxIdx].RetentionTime;
            }
            else
            {
                minRt = _standardPeptidesSorted[comboMinIdx].Irt;
                maxRt = _standardPeptidesSorted[comboMaxIdx].Irt;
            }

            if (minRt >= maxRt)
            {
                if (showErrors)
                {
                    MessageDlg.Show(this,
                        Resources.CalibrateIrtDlg_TryGetLine_Maximum_fixed_point_peptide_must_have_a_greater_measured_retention_time_than_the_minimum_fixed_point_peptide_);
                }
                return false;
            }

            regression = SelectedRegressionOption.Regression.ChangePoints(new[] {minRt, maxRt}, new[] {minIrt, maxIrt});
            return true;
        }

        public void UseResults()
        {
            SetCalibrationPeptides();
        }

        public bool SetCalibrationPeptides()
        {
            CheckDisposed();
            var document = Program.ActiveDocumentUI;
            if (!document.Settings.HasResults)
            {
                MessageDlg.Show(this, Resources.CalibrateIrtDlg_UseResults_The_document_must_contain_results_to_calibrate_a_standard);
                return false;
            }
            var targetResolver = TargetResolver.MakeTargetResolver(document);
            _gridViewDriver.TargetResolver = targetResolver;

            var docTargets = document.Molecules.Where(nodePep => nodePep.SchedulingTime.HasValue).Select(nodePep => nodePep.ModifiedTarget).Distinct().ToArray();
            var count = docTargets.Length;

            // If calibrating against another standard, exclude peptides in that standard
            var excludePeps = SelectedRegressionOption?.MatchedRegressionPeptides?.Select(match => match.Item2.ModifiedTarget).ToHashSet();
            var exclude = excludePeps != null && excludePeps.Count > 0;

            if (exclude)
            {
                docTargets = docTargets.Where(target => !excludePeps.Contains(target)).ToArray();
                count = docTargets.Length;
            }

            if (count < MIN_STANDARD_PEPTIDES)
            {
                MessageDlg.Show(this,
                    ModeUIAwareStringFormat(!exclude
                            ? Resources.CalibrateIrtDlg_UseResults_The_document_contains_results_for__0__peptides__which_is_less_than_the_minimum_requirement_of__1__to_calibrate_a_standard_
                            : Resources.CalibrateIrtDlg_SetCalibrationPeptides_The_document_contains_results_for__0__peptide_s__not_in_this_standard__which_is_less_than_the_minimum_requirement_of__1__to_calibrate_a_standard_,
                        count, MIN_STANDARD_PEPTIDES));
                return false;
            }
            else if (count < MIN_SUGGESTED_STANDARD_PEPTIDES)
            {
                if (MultiButtonMsgDlg.Show(this,
                        ModeUIAwareStringFormat(!exclude
                                ? Resources.CalibrateIrtDlg_UseResults_The_document_contains_results_for__0__peptides__but_using_fewer_than__1__standard_peptides_is_not_recommended__Are_you_sure_you_want_to_continue_
                                : Resources.CalibrateIrtDlg_UseResults_The_document_contains_results_for__0__peptides_not_in_this_standard__but_using_fewer_than__1__standard_peptides_is_not_recommended__Are_you_sure_you_want_to_continue_,
                            count, MIN_SUGGESTED_STANDARD_PEPTIDES),
                        MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false) != DialogResult.Yes)
                {
                    return false;
                }
            }
            else if (count > 20)
            {
                using (var dlg = new AddIrtStandardsDlg(count, excludePeps != null && excludePeps.Count > 0))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return false;
                    count = dlg.StandardCount;
                }
            }

            _gridViewDriver.Recalculate(document, count, SelectedRegressionOption, excludePeps);
            return true;
        }

        private void UpdateEquation(object sender, EventArgs e)
        {
            if (!TryGetRegression(false, out var regression))
            {
                lblEquation.Text = string.Empty;
                if (!IsRecalibration)
                {
                    FireStandardsChanged = false;
                    StandardPeptideList.ForEach(pep => pep.Irt = double.NaN);
                    StandardPeptideList.ResetBindings();
                    FireStandardsChanged = true;
                }
                return;
            }
            lblEquation.Text = regression.DisplayEquation;
            if (!IsRecalibration && !SelectedRegressionOption.ForcedIrts)
            {
                FireStandardsChanged = false;
                StandardPeptideList.ForEach(pep => pep.Irt = regression.GetY(pep.RetentionTime));
                StandardPeptideList.ResetBindings();
                FireStandardsChanged = true;
            }
        }

        private void comboRegression_SelectedIndexChanged(object sender, EventArgs e)
        {
            labelMinIrt.Enabled = textMinIrt.Enabled = labelMaxIrt.Enabled = textMaxIrt.Enabled =
                labelMinPeptide.Enabled = comboMinPeptide.Enabled = labelMaxPeptide.Enabled = comboMaxPeptide.Enabled = SelectedRegressionOption.AllowEditEquation;
            if (!SelectedRegressionOption.AllowEditEquation)
            {
                textMinIrt.Clear();
                textMaxIrt.Clear();
                comboMinPeptide.Items.Clear();
                comboMaxPeptide.Items.Clear();
            }
            lblEquation.Visible = calibrateMeasuredRt.Visible = !SelectedRegressionOption.ForcedIrts;
            if (IsRecalibration)
            {
                UpdateEquation(sender, e);
                return;
            }
            calibratePeptides.ReadOnly = calibrateMeasuredRt.ReadOnly = !SelectedRegressionOption.AllowEditGrid;
            gridViewCalibrate.AllowUserToAddRows = gridViewCalibrate.AllowUserToDeleteRows = SelectedRegressionOption.AllowEditGrid;
            if (SelectedRegressionOption.ForcedStandards)
            {
                _gridViewDriver.SetPeptides(SelectedRegressionOption.StandardPeptides.Select(pep => new StandardPeptide(pep)).ToArray());
                return;
            }
            if (SelectedRegressionOption.MatchedPeptideCount == 0) // fixed point
            {
                StandardsChanged(sender, e);
                UpdateEquation(sender, e);
                return;
            }

            // If standard peptide list is empty, or contains over 50% of the selected regression's peptides, use results
            var regressionPeptides = new TargetMap<bool>(SelectedRegressionOption.MatchedRegressionPeptides.Select(match =>
                new KeyValuePair<Target, bool>(match.Item1.ModifiedTarget, true)));
            if (StandardPeptideCount == 0 || StandardPeptideList.Count(pep => regressionPeptides.ContainsKey(pep.Target)) >= regressionPeptides.Count / 2)
            {
                if (!SetCalibrationPeptides())
                {
                    comboRegression.SelectedIndex = 0;
                    return;
                }
            }
            UpdateEquation(sender, e);
        }

        private void UpdatePeptideCount()
        {
            labelStandardCount.Text = StandardPeptideList.Count != 1
                ? ModeUIAwareStringFormat(Resources.CalibrateIrtDlg_StandardsChanged__0__peptides, StandardPeptideCount)
                : ModeUIAwareStringFormat(Resources.CalibrateIrtDlg_StandardsChanged__1_peptide);
        }

        private void StandardsChanged(object sender, EventArgs eventArgs)
        {
            if (!FireStandardsChanged || !_gridViewDriver.ItemsValid)
                return;

            var oldPeps = comboMinPeptide.Items.Cast<string>().ToArray();
            var newPeps = StandardPeptidesSorted.Select(pep => pep.Sequence).ToArray();

            if (oldPeps.SequenceEqual(newPeps))
                return;

            UpdatePeptideCount();

            var curMin = (string)comboMinPeptide.SelectedItem;
            var curMax = (string)comboMaxPeptide.SelectedItem;

            comboMinPeptide.Items.Clear();
            comboMaxPeptide.Items.Clear();

            if (newPeps.Length == 0 || !comboMinPeptide.Enabled)
                return;

            comboMinPeptide.Items.AddRange(newPeps);
            comboMaxPeptide.Items.AddRange(newPeps);

            var newMinIdx = newPeps.IndexOf(pep => pep.Equals(curMin));
            if (newMinIdx < 0)
                newMinIdx = 0;
            var newMaxIdx = newPeps.IndexOf(pep => pep.Equals(curMax));
            if (newMaxIdx < 0)
                newMaxIdx = comboMaxPeptide.Items.Count - 1;

            if (newMinIdx > newMaxIdx)
            {
                var tmp = newMinIdx;
                newMinIdx = newMaxIdx;
                newMaxIdx = tmp;
            }

            comboMinPeptide.SelectedIndex = newMinIdx;
            comboMaxPeptide.SelectedIndex = newMaxIdx;
        }

        private void btnGraph_Click(object sender, EventArgs e)
        {
            GraphRegression();
        }

        public void GraphRegression()
        {
            double[] xValues, yValues;
            Dictionary<int, string> tooltips;
            IIrtRegression regression;

            if (SelectedRegressionOption.FixedPoint)
            {
                if (!TryGetRegression(true, out regression))
                    return;

                double pepMinTime, pepMaxTime;
                if (!IsRecalibration)
                {
                    var pepMin = StandardPeptidesSorted[comboMinPeptide.SelectedIndex];
                    var pepMax = StandardPeptidesSorted[comboMaxPeptide.SelectedIndex];
                    pepMinTime = pepMin.RetentionTime;
                    pepMaxTime = pepMax.RetentionTime;
                    tooltips = new Dictionary<int, string> {{0, pepMin.Sequence}, {1, pepMax.Sequence}};
                }
                else
                {
                    var pepMin = _standardPeptidesSorted[comboMinPeptide.SelectedIndex];
                    var pepMax = _standardPeptidesSorted[comboMaxPeptide.SelectedIndex];
                    pepMinTime = pepMin.Irt;
                    pepMaxTime = pepMax.Irt;
                    tooltips = new Dictionary<int, string> {{0, pepMin.PeptideModSeq}, {1, pepMax.PeptideModSeq}};
                }
                xValues = new[] {pepMinTime, pepMaxTime};
                yValues = xValues.Select(x => regression.GetY(x)).ToArray();
            }
            else
            {
                xValues = new double[SelectedRegressionOption.MatchedPeptideCount];
                yValues = new double[SelectedRegressionOption.MatchedPeptideCount];
                tooltips = new Dictionary<int, string>();
                var i = 0;
                foreach (var (standardPeptide, nodePep) in SelectedRegressionOption.MatchedRegressionPeptides)
                {
                    xValues[i] = nodePep.PercentileMeasuredRetentionTime.Value;
                    yValues[i] = standardPeptide.Irt;
                    tooltips[i] = standardPeptide.PeptideModSeq;
                    i++;
                }
                regression = SelectedRegressionOption.Regression;
            }

            ShowGraph(Resources.CalibrateIrtDlg_btnGraph_Click_Regression_equation_calculation, xValues, yValues, tooltips,
                regression, SelectedRegressionOption.FixedPoint && IsRecalibration);
        }

        private void btnGraphIrts_Click(object sender, EventArgs e)
        {
            GraphIrts();
        }

        public void GraphIrts()
        {
            if (!TryGetRegression(true, out var regressionLine))
            {
                return;
            }
            double[] xValues, yValues;
            var tooltips = new Dictionary<int, string>();
            if (!IsRecalibration)
            {
                xValues = new double[StandardPeptideList.Count];
                yValues = new double[StandardPeptideList.Count];
                var i = 0;
                foreach (var pep in StandardPeptideList)
                {
                    xValues[i] = pep.RetentionTime;
                    yValues[i] = pep.Irt;
                    tooltips[i] = pep.Sequence;
                    i++;
                }
            }
            else
            {
                var peps = _standard.Peptides.Union(_updatePeptides).ToArray();
                xValues = new double[peps.Length];
                yValues = new double[peps.Length];
                var i = 0;
                foreach (var pep in peps)
                {
                    xValues[i] = pep.Irt;
                    yValues[i] = regressionLine.GetY(pep.Irt);
                    tooltips[i] = pep.PeptideModSeq;
                    i++;
                }
            }
            ShowGraph(Resources.CalibrateIrtDlg_btnGraphIrts_Click_Calibrated_iRT_values, xValues, yValues, tooltips,
                regressionLine, IsRecalibration);
        }

        private void ShowGraph(string title, double[] xValues, double[] yValues, Dictionary<int, string> tooltips, IIrtRegression regression, bool xIrt)
        {
            var data = new RegressionGraphData
            {
                Title = title,
                LabelX = !xIrt ? Resources.CalibrateIrtDlg_ShowGraph_Measured : Resources.CalibrateIrtDlg_ShowGraph_Old_iRT,
                LabelY = !xIrt ? Resources.CalibrateIrtDlg_ShowGraph_iRT : Resources.CalibrateIrtDlg_ShowGraph_New_iRT,
                XValues = xValues,
                YValues = yValues,
                Tooltips = tooltips,
                RegressionLine = regression,
            };

            using (var graph = new GraphRegression(new[] { data }) { Width = 800, Height = 600 })
            {
                graph.ShowDialog(this);
            }
        }

        public class RegressionOption
        {
            public string Name { get; }
            public IIrtRegression Regression { get; }
            public bool FixedPoint { get; }
            public List<Tuple<DbIrtPeptide, PeptideDocNode>> MatchedRegressionPeptides { get; }
            public List<StandardPeptide> StandardPeptides { get; }
            public bool AllowEditEquation => FixedPoint;
            public bool AllowEditGrid { get; }
            public bool ForcedStandards => StandardPeptides != null && StandardPeptides.Count > 0;
            public bool ForcedIrts => ForcedStandards && !StandardPeptides.Any(pep => double.IsNaN(pep.Irt));

            public int MatchedPeptideCount => MatchedRegressionPeptides?.Count ?? 0;
            public bool IsCirtDiscovered => Equals(Name, Resources.CalibrationGridViewDriver_CiRT_option_name);

            private RegressionOption(string name, IIrtRegression regression) : this(name, regression, true, null, null, true)
            {
            }

            public RegressionOption(string name, IIrtRegression regression, bool fixedPoint,
                List<Tuple<DbIrtPeptide, PeptideDocNode>> matchedRegressionPeptides, List<StandardPeptide> standardPeptides,
                bool allowEditGrid)
            {
                Name = name;
                Regression = regression;
                FixedPoint = fixedPoint;
                MatchedRegressionPeptides = matchedRegressionPeptides;
                StandardPeptides = standardPeptides;
                AllowEditGrid = allowEditGrid;
            }

            public static IEnumerable<RegressionOption> All(SrmDocument document)
            {
                yield return new RegressionOption(Resources.RegressionOption_All_Fixed_points__linear_, new RegressionLine());
                yield return new RegressionOption(Resources.RegressionOption_All_Fixed_points__logarithmic_, new LogRegression(true));

                var docPeptides = new Dictionary<Target, PeptideDocNode>();
                foreach (var nodePep in document.Peptides.Where(nodePep => nodePep.PercentileMeasuredRetentionTime.HasValue && !nodePep.IsDecoy))
                {
                    if (!docPeptides.ContainsKey(nodePep.ModifiedTarget))
                        docPeptides[nodePep.ModifiedTarget] = nodePep;
                }

                foreach (var standard in Settings.Default.IrtStandardList.Where(standard => !ReferenceEquals(standard, IrtStandard.EMPTY)))
                {
                    var pepMatches = new List<Tuple<DbIrtPeptide, PeptideDocNode>>();
                    foreach (var pep in standard.Peptides)
                    {
                        if (docPeptides.TryGetValue(pep.GetNormalizedModifiedSequence(), out var nodePep))
                        {
                            pepMatches.Add(Tuple.Create(pep, nodePep));
                        }
                    }
                    if (IrtRegression.TryGet<RegressionLine>(
                        pepMatches.Select(pep => (double) pep.Item2.PercentileMeasuredRetentionTime.Value).ToList(),
                        pepMatches.Select(pep => pep.Item1.Irt).ToList(),
                        RCalcIrt.MinStandardCount(standard.Peptides.Count), out var regressionLine))
                    {
                        yield return new RegressionOption(standard.Name, (RegressionLine) regressionLine, false, pepMatches, null, false);
                    }
                }
            }

            public override string ToString()
            {
                return Name;
            }
        }

        private class CalibrationGridViewDriver : PeptideGridViewDriver<StandardPeptide>
        {
            private readonly CalibrateIrtDlg _parent;
            private readonly IrtPeptidePicker _picker;

            public CalibrationGridViewDriver(CalibrateIrtDlg parent, DataGridViewEx gridView, BindingSource bindingSource,
                SortableBindingList<StandardPeptide> items) : base(gridView, bindingSource, items)
            {
                _parent = parent;
                _picker = new IrtPeptidePicker();
                GridView.CellValueChanged += parent.StandardsChanged;
                Items.ListChanged += parent.StandardsChanged;
            }

            public bool ItemsValid => Items.All(item => item.Target != null && !(item.RetentionTime <= 0));

            protected override void DoPaste()
            {
                var standardPeptidesNew = new List<MeasuredPeptide>();
                GridView.DoPaste(MessageParent, ValidateRowWithTime,
                    values => standardPeptidesNew.Add(new MeasuredPeptide
                        {Target = new Target(values[0]), RetentionTime = double.Parse(values[1])}));

                var message = ValidateUniquePeptides(standardPeptidesNew.Select(p => p.Target), null, null);
                if (message != null)
                {
                    MessageDlg.Show(MessageParent, message);
                    return;
                }
                SetPeptides(standardPeptidesNew);
            }

            public List<StandardPeptide> Recalculate(SrmDocument document, int peptideCount, RegressionOption currentRegression, ICollection<Target> exclude)
            {
                var bestPeptides = PickPeptides(document, peptideCount, exclude, currentRegression, out var cirt);
                if (bestPeptides == null)
                    return null;
                if (((currentRegression != null && !currentRegression.FixedPoint) && !currentRegression.IsCirtDiscovered) || cirt == null)
                {
                    SetPeptides(bestPeptides);
                }
                else
                {
                    var existingCirt = _parent.RegressionOptions.IndexOf(opt => opt.Name.Equals(Resources.CalibrationGridViewDriver_CiRT_option_name));
                    if (existingCirt < 0)
                        _parent.comboRegression.Items.Add(cirt);
                    else
                        _parent.comboRegression.Items[existingCirt] = cirt;
                    _parent.SelectedRegressionOption = cirt;
                }
                return Items.ToList();
            }

            public void SetPeptides(IReadOnlyCollection<MeasuredPeptide> peps)
            {
                Items.RaiseListChangedEvents = false;
                try
                {
                    Items.Clear();
                    Items.AddRange(peps.All(pep => pep is StandardPeptide)
                        ? peps.Select(pep => pep as StandardPeptide)
                        : peps.Select(pep => new StandardPeptide {Target = pep.Target, RetentionTime = pep.RetentionTime}));
                }
                finally
                {
                    Items.RaiseListChangedEvents = true;
                }
                Items.ResetBindings();
                _parent.UpdateEquation(null, null);
            }

            private List<MeasuredPeptide> PickPeptides(SrmDocument doc, int count, ICollection<Target> exclude, RegressionOption currentRegression, out RegressionOption cirt)
            {
                cirt = null;

                if (!_picker.HasScoredPeptides)
                {
                    using (var longWaitDlg = new LongWaitDlg {Text = Resources.CalibrationGridViewDriver_FindEvenlySpacedPeptides_Calculating_scores})
                    {
                        longWaitDlg.PerformWork(_parent, 1000, pm => _picker.ScorePeptides(doc, pm));
                        if (longWaitDlg.IsCanceled)
                            return null;
                    }
                }

                RegressionLine cirtRegression = null;
                var useCirt = false;
                var cirtUsePredefined = false;
                if (_picker.TryGetCirtRegression(count, out var tryCirtRegression, out var matchedPeptides))
                {
                    var currentIsCirt = currentRegression != null && currentRegression.IsCirtDiscovered;
                    switch (MultiButtonMsgDlg.Show(_parent, string.Format(
                        Resources.CalibrationGridViewDriver_FindEvenlySpacedPeptides_This_document_contains__0__CiRT_peptides__Would_you_like_to_use__1__of_them_as_your_iRT_standards_,
                        _picker.CirtPeptideCount, count), MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true))
                    {
                        case DialogResult.Yes:
                            cirtRegression = tryCirtRegression;
                            useCirt = true;
                            if ((currentRegression != null && currentRegression.FixedPoint) || currentIsCirt)
                            {
                                switch (MultiButtonMsgDlg.Show(_parent,
                                    Resources.CalibrationGridViewDriver_FindEvenlySpacedPeptides_Would_you_like_to_use_the_predefined_iRT_values_,
                                    Resources.CalibrationGridViewDriver_FindEvenlySpacedPeptides_Predefined_values,
                                    Resources.CalibrationGridViewDriver_FindEvenlySpacedPeptides_Calculate_from_regression,
                                    true))
                                {
                                    case DialogResult.Yes:
                                        cirtUsePredefined = true;
                                        break;
                                    case DialogResult.No:
                                        break;
                                    case DialogResult.Cancel:
                                        return null;
                                }
                            }
                            break;
                        case DialogResult.No:
                            if (currentIsCirt)
                                cirtRegression = tryCirtRegression;
                            break;
                        case DialogResult.Cancel:
                            return null;
                    }
                }

                var bestPeptides = _picker.Pick(count, exclude, useCirt);
                if (cirtRegression != null)
                {
                    var standardPeptides = bestPeptides.Select(pep => new StandardPeptide
                    {
                        Irt = cirtUsePredefined ? _picker.CirtIrt(pep.Target).GetValueOrDefault() : double.NaN,
                        RetentionTime = pep.RetentionTime,
                        Target = pep.Target
                    }).ToList();
                    cirt = new RegressionOption(Resources.CalibrationGridViewDriver_CiRT_option_name, cirtRegression, false,
                        matchedPeptides.ToList(), standardPeptides, false);
                }
                return bestPeptides;
            }
        }

        #region Functional Test Support

        public string StandardName
        {
            get => textName.Text;
            set => textName.Text = value;
        }

        public RegressionOption[] RegressionOptions => comboRegression.Items.Cast<RegressionOption>().ToArray();

        public RegressionOption SelectedRegressionOption
        {
            get => comboRegression.SelectedItem as RegressionOption;
            set => comboRegression.SelectedItem = value;
        }

        public List<StandardPeptide> Recalculate(SrmDocument document, int peptideCount)
        {
            return _gridViewDriver.Recalculate(document, peptideCount, null, null) ?? new List<StandardPeptide>();
        }

        public void SetIrtRange(double min, double max)
        {
            textMinIrt.Text = Math.Round(min, 8).ToString(LocalizationHelper.CurrentCulture);
            textMaxIrt.Text = Math.Round(max, 8).ToString(LocalizationHelper.CurrentCulture);
        }

        public void SetFixedPoints(int one, int two)
        {
            comboMinPeptide.SelectedIndex = one;
            comboMaxPeptide.SelectedIndex = two;
        }
        #endregion
    }

    public class StandardPeptide : MeasuredPeptide
    {
        public double Irt { get; set; }

        public string IrtDisplay => !double.IsNaN(Irt) ? string.Format(CultureInfo.CurrentCulture, @"{0:N2}", Irt) : string.Empty;

        public StandardPeptide()
        {
        }

        public StandardPeptide(StandardPeptide other) : base(other)
        {
            Irt = other.Irt;
        }
    }
}