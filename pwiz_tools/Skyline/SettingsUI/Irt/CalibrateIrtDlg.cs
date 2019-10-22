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
using System.Linq;
using System.Text;
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
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class CalibrateIrtDlg : FormEx
    {
        public const int MIN_STANDARD_PEPTIDES = 5;
        public const int MIN_SUGGESTED_STANDARD_PEPTIDES = 10;

        private readonly CalibrationGridViewDriver _gridViewDriver;

        private readonly IrtStandard _standard;
        private readonly IEnumerable<IrtStandard> _existing;
        private readonly DbIrtPeptide[] _updatePeptides;

        public CalibrateIrtDlg(IrtStandard standard, IEnumerable<IrtStandard> existing, DbIrtPeptide[] updatePeptides)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _standard = standard;
            _existing = existing;
            _updatePeptides = updatePeptides;

            _gridViewDriver = new CalibrationGridViewDriver(this, gridViewCalibrate, bindingSourceStandard,
                new SortableBindingList<StandardPeptide>());

            comboRegression.Items.AddRange(RegressionOption.All(Program.ActiveDocumentUI).ToArray());
            comboRegression.SelectedIndex = 0;

            if (IsRecalibration)
            {
                textName.Text = standard.Name;
                FormBorderStyle = FormBorderStyle.Fixed3D;
                panelPeptides.Hide();
                btnUseCurrent.Hide();
                Height -= panelPeptides.Height;
                var standardPeptides = standard.Peptides.Select(pep => pep.PeptideModSeq).ToArray();
                comboMinPeptide.Items.AddRange(standardPeptides);
                comboMaxPeptide.Items.AddRange(standardPeptides);
                if (standardPeptides.Length > 0)
                {
                    // Look for standard peptides with whole number values as the suggested fixed points
                    var iFixed1 = standard.Peptides.IndexOf(pep => Math.Round(pep.Irt, 8) == Math.Round(pep.Irt));
                    var iFixed2 = standard.Peptides.LastIndexOf(pep => Math.Round(pep.Irt, 8) == Math.Round(pep.Irt));
                    if (iFixed1 == -1 || iFixed2 == -1)
                    {
                        iFixed1 = 0;
                        iFixed2 = standardPeptides.Length - 1;
                    }
                    else if (iFixed1 == iFixed2)
                    {
                        if (iFixed1 < standardPeptides.Length / 2)
                        {
                            iFixed2 = standardPeptides.Length - 1;
                        }
                        else
                        {
                            iFixed1 = 0;
                        }
                    }
                    comboMinPeptide.SelectedIndex = iFixed1;
                    comboMaxPeptide.SelectedIndex = iFixed2;
                    SetIrtRange(standard.Peptides[iFixed1].Irt, standard.Peptides[iFixed2].Irt);
                }
            }
        }

        public IrtStandard IrtStandard { get; private set; }

        public SortableBindingList<StandardPeptide> StandardPeptideList { get { return _gridViewDriver.Items; } }

        public int StandardPeptideCount { get { return StandardPeptideList.Count; } }

        public bool IsRecalibration => _standard != null;

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
            gridViewCalibrate.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
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

            if (!TryGetLine(true, out var linearEquation))
            {
                return;
            }

            if (!IsRecalibration)
            {
                IrtStandard = new IrtStandard(name, null,
                    StandardPeptideList.Select(pep =>
                        new DbIrtPeptide(pep.Target, linearEquation.GetY(pep.RetentionTime), true, TimeSource.peak)));
            }
            else
            {
                foreach (var pep in _updatePeptides)
                {
                    pep.Irt = linearEquation.GetY(pep.Irt);
                }
                IrtStandard = new IrtStandard(name, null,
                    _standard.Peptides.Select(pep =>
                        new DbIrtPeptide(pep.Target, linearEquation.GetY(pep.Irt), true, TimeSource.peak)));
            }
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnUseCurrent_Click(object sender, EventArgs e)
        {
            comboRegression.SelectedIndex = 0;
            UseResults();
        }

        private bool TryGetLine(bool showErrors, out RegressionLine line)
        {
            var selected = (RegressionOption) comboRegression.SelectedItem;
            if (!selected.IsFixedPoint)
            {
                line = selected.RegressionLine;
                return true;
            }

            line = null;
            var helper = new MessageBoxHelper(this, showErrors);

            if (!helper.ValidateDecimalTextBox(textMinIrt, null, null, out var minIrt))
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
                minRt = StandardPeptideList[comboMinIdx].RetentionTime;
                maxRt = StandardPeptideList[comboMaxIdx].RetentionTime;
            }
            else
            {
                minRt = _standard.Peptides[comboMinIdx].Irt;
                maxRt = _standard.Peptides[comboMaxIdx].Irt;
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

            var statRt = new Statistics(minRt, maxRt);
            var statIrt = new Statistics(minIrt, maxIrt);
            line = new RegressionLine(statIrt.Slope(statRt), statIrt.Intercept(statRt));
            return true;
        }

        public void UseResults()
        {
            SetCalibrationPeptides(null);
        }

        private bool SetCalibrationPeptides(ICollection<Target> exclude)
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

            var peptides = document.Molecules.Where(nodePep => nodePep.SchedulingTime.HasValue).ToArray();
            var count = peptides.Length;
            if (exclude != null && exclude.Count > 0)
            {
                peptides = peptides.Where(nodePep => !exclude.Contains(nodePep.ModifiedTarget)).ToArray();
                count = peptides.Length;
                if (count < MIN_STANDARD_PEPTIDES)
                {
                    MessageDlg.Show(this,
                        ModeUIAwareStringFormat(
                            Resources.CalibrateIrtDlg_SetCalibrationPeptides_The_document_contains_results_for__0__peptide_s__not_in_this_standard__which_is_less_than_the_minimum_requirement_of__1__to_calibrate_a_standard_,
                            count, MIN_STANDARD_PEPTIDES));
                    return false;
                }
                else if (count < MIN_SUGGESTED_STANDARD_PEPTIDES)
                {
                    if (MultiButtonMsgDlg.Show(this,
                            ModeUIAwareStringFormat(
                                Resources.CalibrateIrtDlg_SetCalibrationPeptides_The_document_only_contains_results_for__0__peptide_s__not_in_this_standard__It_is_recommended_to_use_at_least__1__peptides_to_calibrate_a_standard__Are_you_sure_you_wish_to_continue_,
                                count, MIN_SUGGESTED_STANDARD_PEPTIDES),
                            MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false) != DialogResult.Yes)
                    {
                        return false;
                    }
                }
            }
            if (count > 20)
            {
                using (var dlg = new AddIrtStandardsDlg(count, exclude != null && exclude.Count > 0))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return false;

                    count = dlg.StandardCount;
                }
            }

            _gridViewDriver.Recalculate(document, count, exclude);
            return true;
        }

        private void UpdateControls(object sender, EventArgs e)
        {
            if (!TryGetLine(false, out var line))
            {
                lblEquation.Text = string.Empty;
                if (!IsRecalibration)
                {
                    StandardPeptideList.ForEach(pep => pep.Irt = double.NaN);
                }
                return;
            }
            var equationSb = new StringBuilder();
            equationSb.Append(Resources.CalibrateIrtDlg_UpdateControls_iRT);
            equationSb.Append(@" =");
            var roundedSlope = Math.Round(line.Slope, 4);
            var roundedIntercept = Math.Round(line.Intercept, 4);
            if (roundedSlope != 0 || roundedIntercept != 0)
            {
                if (roundedSlope != 0)
                {
                    if (roundedSlope != 1)
                    {
                        equationSb.Append(string.Format(@" {0:F04} *", roundedSlope));
                    }
                    equationSb.Append(' ');
                    equationSb.Append(Resources.CalibrateIrtDlg_UpdateControls_RT);
                }
                if (roundedIntercept != 0)
                {
                    equationSb.Append(roundedSlope != 0
                        ? string.Format(@" {0} {1:F04}", roundedIntercept >= 0 ? '+' : '-', Math.Abs(roundedIntercept))
                        : string.Format(@" {0:F04}", roundedIntercept));
                }
            }
            else
            {
                equationSb.Append(string.Format(@" {0:F04}", 0));
            }
            lblEquation.Text = equationSb.ToString();
            if (!IsRecalibration)
            {
                StandardPeptideList.ForEach(pep => pep.Irt = line.GetY(pep.RetentionTime));
                StandardPeptideList.ResetBindings();
            }
        }

        private void comboRegression_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selected = (RegressionOption) comboRegression.SelectedItem;
            textMinIrt.Enabled = textMaxIrt.Enabled = comboMinPeptide.Enabled = comboMaxPeptide.Enabled = selected.IsFixedPoint;
            if (IsRecalibration)
            {
                UpdateControls(sender, e);
                return;
            }
            calibratePeptides.ReadOnly = calibrateMeasuredRt.ReadOnly = !selected.IsFixedPoint;
            gridViewCalibrate.AllowUserToAddRows = gridViewCalibrate.AllowUserToDeleteRows = selected.IsFixedPoint;
            bindingSourceStandard.Clear();
            if (selected.IsFixedPoint)
            {
                UpdateControls(sender, e);
                return;
            }

            if (!SetCalibrationPeptides(selected.MatchedPeptides.Select(match => match.Item2.ModifiedTarget).ToHashSet()))
            {
                comboRegression.SelectedIndex = 0;
                return;
            }
            UpdateControls(sender, e);
        }

        private void btnGraph_Click(object sender, EventArgs e)
        {
            GraphRegression();
        }

        public void GraphRegression()
        {
            double[] xValues, yValues;
            Dictionary<int, string> tooltips;
            RegressionLine regressionLine;

            if (comboRegression.SelectedIndex == 0)
            {
                if (!TryGetLine(true, out regressionLine))
                {
                    return;
                }

                double pepMinTime, pepMaxTime;
                if (!IsRecalibration)
                {
                    var pepMin = StandardPeptideList[comboMinPeptide.SelectedIndex];
                    var pepMax = StandardPeptideList[comboMaxPeptide.SelectedIndex];
                    pepMinTime = pepMin.RetentionTime;
                    pepMaxTime = pepMax.RetentionTime;
                    tooltips = new Dictionary<int, string> {{0, pepMin.Sequence}, {1, pepMax.Sequence}};
                }
                else
                {
                    var pepMin = _standard.Peptides[comboMinPeptide.SelectedIndex];
                    var pepMax = _standard.Peptides[comboMaxPeptide.SelectedIndex];
                    pepMinTime = pepMin.Irt;
                    pepMaxTime = pepMax.Irt;
                    tooltips = new Dictionary<int, string> {{0, pepMin.PeptideModSeq}, {1, pepMax.PeptideModSeq}};
                }
                xValues = new[] {pepMinTime, pepMaxTime};
                yValues = xValues.Select(x => regressionLine.GetY(x)).ToArray();
            }
            else
            {
                var selected = (RegressionOption) comboRegression.SelectedItem;
                xValues = new double[selected.MatchedPeptides.Count];
                yValues = new double[selected.MatchedPeptides.Count];
                tooltips = new Dictionary<int, string>();
                var i = 0;
                foreach (var (standardPeptide, nodePep) in selected.MatchedPeptides)
                {
                    xValues[i] = nodePep.PercentileMeasuredRetentionTime.Value;
                    yValues[i] = standardPeptide.Irt;
                    tooltips[i] = standardPeptide.PeptideModSeq;
                    i++;
                }
                regressionLine = selected.RegressionLine;
            }

            ShowGraph(Resources.CalibrateIrtDlg_btnGraph_Click_Linear_equation_calculation, xValues, yValues, tooltips,
                regressionLine, comboRegression.SelectedIndex == 0 && IsRecalibration);
        }

        private void btnGraphIrts_Click(object sender, EventArgs e)
        {
            GraphIrts();
        }

        public void GraphIrts()
        {
            if (!TryGetLine(true, out var regressionLine))
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

        private void ShowGraph(string title, double[] xValues, double[] yValues, Dictionary<int, string> tooltips, RegressionLine line, bool xIrt)
        {
            var data = new RegressionGraphData
            {
                Title = title,
                LabelX = !xIrt ? Resources.CalibrateIrtDlg_ShowGraph_Measured : Resources.CalibrateIrtDlg_ShowGraph_Old_iRT,
                LabelY = !xIrt ? Resources.CalibrateIrtDlg_ShowGraph_iRT : Resources.CalibrateIrtDlg_ShowGraph_New_iRT,
                XValues = xValues,
                YValues = yValues,
                Tooltips = tooltips,
                RegressionLine = line,
            };

            using (var graph = new GraphRegression(new[] { data }) { Width = 800, Height = 600 })
            {
                graph.ShowDialog(this);
            }
        }

        public class RegressionOption
        {
            public string Name { get; }
            public List<Tuple<DbIrtPeptide, PeptideDocNode>> MatchedPeptides { get; }

            private RegressionOption(string name, List<Tuple<DbIrtPeptide, PeptideDocNode>> matchedPeptides, RegressionLine regressionLine)
            {
                Name = name;
                MatchedPeptides = matchedPeptides;
                RegressionLine = regressionLine;
            }

            public bool IsFixedPoint => MatchedPeptides == null || MatchedPeptides.Count == 0;

            public RegressionLine RegressionLine { get; }

            public static IEnumerable<RegressionOption> All(SrmDocument document)
            {
                yield return new RegressionOption(Resources.RegressionOption_All_Fixed_points, null, null);

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
                            pepMatches.Add(new Tuple<DbIrtPeptide, PeptideDocNode>(pep, nodePep));
                        }
                    }
                    if (RCalcIrt.TryGetRegressionLine(
                        pepMatches.Select(pep => (double) pep.Item2.PercentileMeasuredRetentionTime.Value).ToList(),
                        pepMatches.Select(pep => pep.Item1.Irt).ToList(),
                        RCalcIrt.MinStandardCount(standard.Peptides.Count), out var regressionLine))
                    {
                        yield return new RegressionOption(standard.Name, pepMatches, regressionLine);
                    }
                }
            }

            public override string ToString()
            {
                return Name;
            }
        }

        public void StandardsChanged(object sender, EventArgs eventArgs)
        {
            var oldPeps = comboMinPeptide.Items.Cast<string>().ToArray();
            var newPeps = StandardPeptideList.Select(pep => pep.Sequence).ToArray();

            if (oldPeps.SequenceEqual(newPeps))
            {
                return;
            }
            labelStandardCount.Text = StandardPeptideList.Count != 1
                ? ModeUIAwareStringFormat(Resources.CalibrateIrtDlg_StandardsChanged__0__peptides, StandardPeptideCount)
                : ModeUIAwareStringFormat(Resources.CalibrateIrtDlg_StandardsChanged__1_peptide);

            var curMin = (string) comboMinPeptide.SelectedItem;
            var curMax = (string) comboMaxPeptide.SelectedItem;

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

        private class CalibrationGridViewDriver : PeptideGridViewDriver<StandardPeptide>
        {
            private readonly CalibrateIrtDlg _parent;

            public CalibrationGridViewDriver(CalibrateIrtDlg parent, DataGridViewEx gridView, BindingSource bindingSource,
                SortableBindingList<StandardPeptide> items) : base(gridView, bindingSource, items)
            {
                _parent = parent;
                GridView.CellValueChanged += parent.StandardsChanged;
                Items.ListChanged += parent.StandardsChanged;
            }

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

            public List<StandardPeptide> Recalculate(SrmDocument document, int peptideCount, ICollection<Target> exclude)
            {
                var docPeptides = new List<Tuple<MeasuredPeptide, PeptideDocNode>>();
                foreach (var pep in document.Molecules.Where(pep => pep.PercentileMeasuredRetentionTime.HasValue && !pep.IsDecoy && (exclude == null || !exclude.Contains(pep.ModifiedTarget))))
                {
                    var seq = document.Settings.GetModifiedSequence(pep);
                    var time = pep.PercentileMeasuredRetentionTime.Value;
                    docPeptides.Add(new Tuple<MeasuredPeptide, PeptideDocNode>(new MeasuredPeptide(seq, time), pep));
                }
                var bestPeptides = FindEvenlySpacedPeptides(document, docPeptides, peptideCount, out var cirtRegression);
                SetPeptides(bestPeptides);
                if (cirtRegression != null && _parent.SelectedRegressionOption.IsFixedPoint)
                {
                    _parent.comboMinPeptide.SelectedIndex = 0;
                    _parent.comboMaxPeptide.SelectedIndex = _parent.comboMaxPeptide.Items.Count - 1;
                    _parent.SetIrtRange(cirtRegression.GetY(Items.First().RetentionTime), cirtRegression.GetY(Items.Last().RetentionTime));
                }
                return Items.ToList();
            }

            public void SetPeptides(IEnumerable<MeasuredPeptide> peps)
            {
                Items.RaiseListChangedEvents = false;
                try
                {
                    Items.Clear();
                    Items.AddRange(peps.Select(pep => new StandardPeptide {Target = pep.Target, RetentionTime = pep.RetentionTime}));
                }
                finally
                {
                    Items.RaiseListChangedEvents = true;
                }
                Items.ResetBindings();
            }

            /// <summary>
            /// This algorithm will determine a number of evenly spaced retention times for the given document,
            /// and then determine an optimal set of peptides from the document. That is, a set of peptides that
            /// are as close as possible to the chosen retention times.
            /// 
            /// The returned list is guaranteed to be sorted by retention time.
            /// </summary>
            /// <param name="doc">Document containing the peptides to choose from</param>
            /// <param name="docPeptides">Peptides to choose from</param>
            /// <param name="peptideCount">The number of peptides desired</param>
            /// <param name="cirtRegression">If CiRT peptides are used, the regression to the known iRT values; otherwise null</param>
            private List<MeasuredPeptide> FindEvenlySpacedPeptides(SrmDocument doc, List<Tuple<MeasuredPeptide, PeptideDocNode>> docPeptides, int peptideCount, out RegressionLine cirtRegression)
            {
                cirtRegression = null;
                if (docPeptides.Count <= peptideCount)
                    return docPeptides.Select(pep => pep.Item1).OrderBy(pep => pep.RetentionTime).ToList();

                var model = doc.Settings.PeptideSettings.Integration.PeakScoringModel;
                if (model == null || !model.IsTrained)
                    model = LegacyScoringModel.DEFAULT_MODEL;

                var resultsHandler = new MProphetResultsHandler(doc, model);
                using (var longWaitDlg = new LongWaitDlg {Text = Resources.CalibrationGridViewDriver_FindEvenlySpacedPeptides_Calculating_scores})
                {
                    longWaitDlg.PerformWork(_parent, 1000, pm => resultsHandler.ScoreFeatures(pm, true));
                    if (longWaitDlg.IsCanceled)
                        return new List<MeasuredPeptide>();
                }

                var scoredPeptidesDict = new Dictionary<Target, ScoredPeptide>();
                foreach (var (pep, nodePep) in docPeptides)
                {
                    var allStats = doc.MeasuredResults.MSDataFileInfos
                        .Select(info => resultsHandler.GetPeakFeatureStatistics(nodePep.Id.GlobalIndex, info.FileId.GlobalIndex))
                        .Where(stats => stats != null).ToArray();
                    var value = float.MaxValue;
                    if (allStats.Length > 0)
                    {
                        value = model is MProphetPeakScoringModel
                            ? allStats.Select(stats => stats.QValue.Value).Max()
                            : -allStats.Select(stats => stats.BestScore).Min();
                    }
                    if (!scoredPeptidesDict.TryGetValue(pep.Target, out var existing) || value < existing.Score)
                        scoredPeptidesDict[pep.Target] = new ScoredPeptide(pep, value);
                }

                var scoredPeptides = scoredPeptidesDict.Values.OrderBy(pep => pep.Peptide.RetentionTime).ToArray();
                var minRt = scoredPeptides.First().Peptide.RetentionTime;
                var maxRt = scoredPeptides.Last().Peptide.RetentionTime;
                var rtRange = maxRt - minRt;

                PeptideBucket[] buckets = null;
                var bucketBoundaries = new[]
                {
                    minRt + rtRange * 1 / 8,
                    minRt + rtRange * 2 / 8,
                    minRt + rtRange * 4 / 8,
                    minRt + rtRange * 6 / 8,
                    minRt + rtRange * 7 / 8,
                    double.MaxValue
                };

                // Try getting a regression using the known CiRT values
                var cirtPeptidesAll = IrtStandard.CIRT.Peptides.ToDictionary(pep => pep.ModifiedTarget, pep => pep.Irt);
                var scoredCirtPeptides = scoredPeptides.Where(pep => cirtPeptidesAll.ContainsKey(pep.Peptide.Target)).ToList();
                var rts = scoredCirtPeptides.Select(pep => pep.Peptide.RetentionTime).ToList();
                var irts = scoredCirtPeptides.Select(pep => cirtPeptidesAll[pep.Peptide.Target]).ToList();
                var removedValues = new List<Tuple<double, double>>();
                if (RCalcIrt.TryGetRegressionLine(rts, irts, peptideCount, out var regression, removedValues))
                {
                    for (var i = scoredCirtPeptides.Count - 1; i >= 0; i--)
                    {
                        if (removedValues.FirstOrDefault(tuple => tuple.Item1.Equals(rts[i]) && tuple.Item2.Equals(irts[i])) != null)
                            scoredCirtPeptides.RemoveAt(i);
                    }
                    // If we have enough CiRT peptides (after removing outliers) and each bucket contains at least one, prompt to use CiRT peptides
                    var cirtBuckets = PeptideBucket.BucketPeptides(scoredCirtPeptides, bucketBoundaries);
                    if (scoredCirtPeptides.Count >= peptideCount && !cirtBuckets.Any(bucket => bucket.Empty))
                    {
                        switch (MultiButtonMsgDlg.Show(_parent, string.Format(
                            Resources.CalibrationGridViewDriver_FindEvenlySpacedPeptides_This_document_contains__0__CiRT_peptides__Would_you_like_to_use__1__of_them_as_your_iRT_standards_,
                            rts.Count, peptideCount), MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true))
                        {
                            case DialogResult.Yes:
                                cirtRegression = regression;
                                buckets = cirtBuckets;
                                break;
                            case DialogResult.No:
                                break;
                            case DialogResult.Cancel:
                                return new List<MeasuredPeptide>();
                        }
                    }
                }

                if (buckets == null)
                    buckets = PeptideBucket.BucketPeptides(scoredPeptides, bucketBoundaries);
                var endBuckets = new[] {buckets.First(), buckets.Last()};
                var midBuckets = buckets.Skip(1).Take(buckets.Length - 2).ToArray();

                var bestPeptides = new List<MeasuredPeptide>();
                while (bestPeptides.Count < peptideCount && buckets.Any(bucket => !bucket.Empty))
                {
                    bestPeptides.AddRange(PeptideBucket.Pop(endBuckets, endBuckets.Length, true).Take(Math.Min(endBuckets.Length, peptideCount - bestPeptides.Count)));
                    bestPeptides.AddRange(PeptideBucket.Pop(midBuckets, midBuckets.Length, false).Take(Math.Min(midBuckets.Length, peptideCount - bestPeptides.Count)));
                }
                bestPeptides.Sort((x, y) => x.RetentionTime.CompareTo(y.RetentionTime));
                return bestPeptides;
            }
        }

        #region Functional Test Support

        public string StandardName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public RegressionOption[] RegressionOptions => comboRegression.Items.Cast<RegressionOption>().ToArray();

        public RegressionOption SelectedRegressionOption
        {
            get => comboRegression.SelectedItem as RegressionOption;
            set => comboRegression.SelectedItem = value;
        }

        public List<StandardPeptide> Recalculate(SrmDocument document, int peptideCount)
        {
            return _gridViewDriver.Recalculate(document, peptideCount, null);
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

        private class ScoredPeptide
        {
            public MeasuredPeptide Peptide { get; }
            public float Score { get; }

            public ScoredPeptide(MeasuredPeptide peptide, float score)
            {
                Peptide = peptide;
                Score = score;
            }
        }

        private class PeptideBucket
        {
            private readonly double _maxRetentionTime;
            private readonly List<ScoredPeptide> _peptides;

            public bool Empty => _peptides.Count == 0;

            private PeptideBucket(double maxRetentionTime)
            {
                _maxRetentionTime = maxRetentionTime;
                _peptides = new List<ScoredPeptide>();
            }

            public float? Peek()
            {
                return !Empty ? (float?)_peptides.First().Score : null;
            }

            public MeasuredPeptide Pop()
            {
                if (Empty)
                    return null;
                var pep = _peptides.First().Peptide;
                _peptides.RemoveAt(0);
                return pep;
            }

            public static PeptideBucket[] BucketPeptides(IEnumerable<ScoredPeptide> peptides, IEnumerable<double> rtBoundaries)
            {
                // peptides must be sorted by retention time (low to high)
                var buckets = rtBoundaries.OrderBy(x => x).Select(boundary => new PeptideBucket(boundary)).ToArray();
                var curBucketIdx = 0;
                var curBucket = buckets[0];
                foreach (var pep in peptides)
                {
                    if (pep.Peptide.RetentionTime > curBucket._maxRetentionTime)
                        curBucket = buckets[++curBucketIdx];
                    curBucket._peptides.Add(pep);
                }
                buckets.ForEach(bucket => bucket._peptides.Sort((x, y) => x.Score.CompareTo(y.Score)));
                return buckets;
            }

            public static IEnumerable<MeasuredPeptide> Pop(PeptideBucket[] buckets, int num, bool limitOne)
            {
                // buckets must be sorted by score (best to worst)
                var popped = 0;
                while (popped < num)
                {
                    var validBuckets = buckets.Where(bucket => !bucket.Empty).OrderBy(bucket => bucket.Peek().Value).ToArray();
                    foreach (var bucket in validBuckets)
                    {
                        yield return bucket.Pop();
                        if (++popped == num)
                            yield break;
                    }
                    if (validBuckets.Length == 0 || limitOne)
                        yield break;
                }
            }
        }
    }

    public class StandardPeptide : MeasuredPeptide
    {
        public double Irt { get; set; }
    }
}