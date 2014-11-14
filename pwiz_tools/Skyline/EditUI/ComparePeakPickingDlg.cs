/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NHibernate.Util;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.EditUI
{
    public partial class ComparePeakPickingDlg : FormEx
    {
        private readonly List<Color> _colors = new List<Color> {Color.Blue, Color.Crimson, Color.Cyan, Color.GreenYellow, Color.Gold, Color.Magenta, Color.Gray};
        private ComparePeakBoundariesList _peakBoundaryList;
        private readonly SimpleGridViewDriver<PeakBoundsMatch> _scoreGridViewDriver;
        private readonly SimpleGridViewDriver<PeakBoundsMatchPair> _compareGridViewDriver;
        private double _qValueSig;
        private bool _xAxisFocus;
        private bool _showFpCutoff;
        private bool _showFpCutoffQ;
        private NormalizeType Normalizer;
         
        public SrmDocument Document { get; private set; }

        public enum NormalizeType {total, frac_manual, frac_all}

        public ComparePeakPickingDlg(SrmDocument document)
        {
            InitializeComponent();
            Document = document;
            _peakBoundaryList = new ComparePeakBoundariesList();
            _scoreGridViewDriver = new ComparePeakBoundariesGridViewDriver(dataGridViewScore,
                bindingSourceScore, new SortableBindingList<PeakBoundsMatch>());
            _compareGridViewDriver = new ComparePeakBoundariesPairGridViewDriver(dataGridViewScoreComparison,
                bindingSourceScoreCompare, new SortableBindingList<PeakBoundsMatchPair>());

            // Hide borders for better copy-paste images
            zedGraphRoc.MasterPane.Border.IsVisible = false;
            zedGraphRoc.GraphPane.Border.IsVisible = false;
            zedGraphQq.MasterPane.Border.IsVisible = false;
            zedGraphQq.GraphPane.Border.IsVisible = false;
            zedGraphFiles.MasterPane.Border.IsVisible = false;
            zedGraphFiles.GraphPane.Border.IsVisible = false;

            // Intialize the y-axis selector combo box
            var yAxisOptions = new[]
            {
                Resources.ComparePeakPickingDlg_ComparePeakPickingDlg_Total_Correct_Peaks,
                Resources.ComparePeakPickingDlg_ComparePeakPickingDlg_Fraction_of_Manual_ID_s,
                Resources.ComparePeakPickingDlg_ComparePeakPickingDlg_Fraction_of_Peak_Groups
            };
            comboBoxYAxis.Items.AddRange(yAxisOptions);
            comboBoxYAxis.SelectedItem = Resources.ComparePeakPickingDlg_ComparePeakPickingDlg_Total_Correct_Peaks;
            comboBoxFilesYAxis.Items.AddRange(yAxisOptions);
            comboBoxFilesYAxis.SelectedItem = Resources.ComparePeakPickingDlg_ComparePeakPickingDlg_Total_Correct_Peaks;

            InitializeGraphPanes();
            textBoxFilesQCutoff.Text = 0.05.ToString(CultureInfo.CurrentCulture);
            _showFpCutoff = true;
            _showFpCutoffQ = true;
            UpdateTextBox();
        }

        private void buttonEdit_Click(object sender, EventArgs e)
        {
            EditList();
        }

        public void EditList()
        {
            using (var dlg = new EditListDlg<ComparePeakBoundariesList, ComparePeakBoundaries>(_peakBoundaryList, Document))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _peakBoundaryList = new ComparePeakBoundariesList(dlg.GetAllEdited().ToList());
                    UpdateCheckedList();
                    UpdateAll();
                }
            }
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            Add();
        }

        public void Add()
        {
            using (var dlg = new AddPeakCompareDlg(Document, _peakBoundaryList))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _peakBoundaryList.Add(dlg.BoundaryComparer);
                    UpdateCheckedList();
                    UpdateAll();
                }
            }
        }

        public static double GetScalingFactor(NormalizeType normalizer, IList<PeakBoundsMatch> matches)
        {
            if (normalizer == NormalizeType.total)
            {
                return 1;
            }
            else if (normalizer == NormalizeType.frac_manual)
            {
                return matches.Count(match => !match.IsMissingTruePeak);
            }
            else if (normalizer == NormalizeType.frac_all)
            {
                return matches.Count;
            }
            else
            {
                throw new InvalidDataException("Unrecognized y axis scaling option");  // Not L10N
            }
        }

        public static void RocFromSortedMatches(List<PeakBoundsMatch> matches, NormalizeType normalizer, out PointPairList rocPoints)
        {
            double scalingFactor = GetScalingFactor(normalizer, matches);
            rocPoints = new PointPairList();
            int truePositives = 0;
            int falsePositives = 0;
            foreach (var match in matches)
            {
                if (match.IsFalsePositive)
                    falsePositives++;
                if (match.IsPickedApexBetweenCuratedBoundaries)
                    truePositives++;
                var rocPoint = new PointPair(falsePositives / (double)(truePositives + falsePositives), truePositives / scalingFactor);
                rocPoints.Add(rocPoint);
            }
        }

        public static void MakeRocLists(ComparePeakBoundaries comparer, NormalizeType normalizer, out PointPairList rocPoints)
        {
            var matches = comparer.Matches;
            if (comparer.HasNoScores)
            {
                matches.Sort(PeakBoundsMatch.CompareQValue);
            }
            else
            {
                matches.Sort(PeakBoundsMatch.CompareScore);
            }
            RocFromSortedMatches(matches, normalizer, out rocPoints);
        }

        public static void MakeQValueLists(ComparePeakBoundaries comparer, out PointPairList qqPoints)
        {
            qqPoints = new PointPairList();
            var matches = comparer.Matches;
            matches.Sort(PeakBoundsMatch.CompareQValue);
            int truePositives = 0;
            int falsePositives = 0;
            foreach (var match in matches)
            {
                if (match.IsFalsePositive)
                    falsePositives++;
                if (match.IsPickedApexBetweenCuratedBoundaries)
                    truePositives++;
                // Null qValues get the worst score
                double qValue = match.QValue ?? 1.0;
                var qqPoint = new PointPair(qValue, falsePositives / (double)(truePositives + falsePositives));
                qqPoints.Add(qqPoint);
            }
        }

        public void MakeFilesLists(ComparePeakBoundaries comparer, double falsePositiveCutoff, out PointPairList filesPoints, out List<string> filesNames)
        {
            filesPoints = new PointPairList();
            filesNames = new List<string>();
            var matches = comparer.Matches;
            var matchesByFile = matches.GroupBy(match => match.FileName).OrderBy(group => group.Key);
            int i = 1;
            foreach (var matchGroup in matchesByFile)
            {
                PointPairList rocPoints;
                var matchesInGroup = matchGroup.ToList();
                if (comparer.HasNoScores)
                {
                    matchesInGroup.Sort(PeakBoundsMatch.CompareQValue);
                }
                else
                {
                    matchesInGroup.Sort(PeakBoundsMatch.CompareScore);
                }
                RocFromSortedMatches(matchesInGroup, Normalizer, out rocPoints);
                double correctPeaks = GetCurveThreshold(rocPoints, falsePositiveCutoff);
                filesPoints.Add(i++, correctPeaks);
                filesNames.Add(matchGroup.Key);
            }
        }

        public static double GetCurveThreshold(PointPairList points, double cutoff)
        {
            var peptidesThreshPt = points.LastOrDefault(point => point.X < cutoff);
            return peptidesThreshPt == null ? points.First().Y : peptidesThreshPt.Y;
        }

        public void UpdateAll()
        {
            UpdateGraph();
            UpdateComboBoxes();
            UpdateDetailsGrid();
            UpdateCompareGrid();
        }

        private void comboBoxYAxis_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateYAxis(comboBoxYAxis);
            UpdateGraph();
        }

        private void comboBoxFilesYAxis_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateYAxis(comboBoxFilesYAxis);
            UpdateGraph();
        }

        public void UpdateYAxis(ComboBox comboBox)
        {
            string selectedItem = (string) comboBox.SelectedItem;
            zedGraphRoc.GraphPane.YAxis.Title.Text = selectedItem;
            zedGraphFiles.GraphPane.YAxis.Title.Text = selectedItem;
            Normalizer = (selectedItem == Resources.ComparePeakPickingDlg_ComparePeakPickingDlg_Total_Correct_Peaks) ? NormalizeType.total :
                         (selectedItem == Resources.ComparePeakPickingDlg_ComparePeakPickingDlg_Fraction_of_Manual_ID_s) ? NormalizeType.frac_manual
                                                                                                                        : NormalizeType.frac_all;
            comboBoxYAxis.SelectedItem = selectedItem;
            comboBoxFilesYAxis.SelectedItem = selectedItem;
        }

        private void checkedListCompare_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            UpdateActiveComparers(e.Index, e.NewValue == CheckState.Checked);
            UpdateAll();
        }

        public void UpdateActiveComparers(int index, bool isChecked)
        {
            _peakBoundaryList[index].IsActive = isChecked;
        }

        public void UpdateCheckedList()
        {
            checkedListCompare.Items.Clear();
            int i = 0;
            foreach (var comparer in _peakBoundaryList)
            {
                checkedListCompare.Items.Add(comparer);
                checkedListCompare.SetItemChecked(i, comparer.IsActive);
                ++i;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        #region Grid

        private void comboBoxDetails_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDetailsGrid();
        }

        public void UpdateDetailsGrid()
        {
            _scoreGridViewDriver.Items.Clear();
            var comparer = comboBoxDetails.SelectedItem as ComparePeakBoundaries;
            if (comparer != null)
            {
                foreach (var match in comparer.Matches)
                {
                    _scoreGridViewDriver.Items.Add(match);
                }
            }
        }

        public void UpdateComboBoxes()
        {
            var comboBoxes = new List<ComboBox> {comboBoxDetails, comboBoxCompare1, comboBoxCompare2};
            foreach (var comboBox in comboBoxes)
            {
                comboBox.Items.Clear();
                foreach (var comparer in _peakBoundaryList.Where(comparer => comparer.IsActive))
                {
                    comboBox.Items.Add(comparer);
                }
                comboBox.SelectedIndex = comboBox.Items.Any() ? 0 : -1;
            }
        }

        private void checkBoxConflicts_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCompareGrid();
        }

        private void comboBoxCompare1_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCompareGrid();
        }

        private void comboBoxCompare2_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCompareGrid();
        }

        public void UpdateCompareGrid()
        {
            _compareGridViewDriver.Items.Clear();
            var comparer1 = comboBoxCompare1.SelectedItem as ComparePeakBoundaries;
            var comparer2 = comboBoxCompare2.SelectedItem as ComparePeakBoundaries;
            if (comparer1 != null && comparer2 != null)
            {
                var matchDict1 = comparer1.Matches.ToDictionary(match => match.Key);
                foreach (var match2 in comparer2.Matches)
                {
                    var match1 = matchDict1[match2.Key];
                    var matchPair = new PeakBoundsMatchPair(match1, match2);
                    // Add all match pairs if the check box is not checked. If it is checked, add only those match pairs
                    // where one is correct and the other is incorrect
                    if (!checkBoxConflicts.Checked || matchPair.IsMatch1 != matchPair.IsMatch2)
                    {
                        _compareGridViewDriver.Items.Add(matchPair);
                    }
                }
            }
        }

        #endregion

        #region Graphs
        /// <summary>
        /// Initialize all the graph panes
        /// </summary>
        private void InitializeGraphPanes()
        {
            InitGraphPane(zedGraphRoc.GraphPane, 
                          Resources.ComparePeakPickingDlg_InitializeGraphPanes_Observed_False_Positive_Rate,
                          Resources.ComparePeakPickingDlg_ComparePeakPickingDlg_Total_Correct_Peaks,
                          Resources.ComparePeakPickingDlg_InitializeGraphPanes_Add_models_files_for_comparison_to_see_a_ROC_plot_);
            InitGraphPane(zedGraphQq.GraphPane, 
                          Resources.ComparePeakPickingDlg_InitializeGraphPanes_Expected_False_Positive_Rate, 
                          Resources.ComparePeakPickingDlg_InitializeGraphPanes_Observed_False_Positive_Rate, 
                          Resources.ComparePeakPickingDlg_InitializeGraphPanes_Add_models_files_for_comparison_to_see_a_q_Q_plot);
            InitGraphPane(zedGraphFiles.GraphPane,
                          Resources.ComparePeakPickingDlg_InitializeGraphPanes_Replicate_Name,
                          Resources.ComparePeakPickingDlg_ComparePeakPickingDlg_Total_Correct_Peaks,
                          Resources.ComparePeakPickingDlg_InitializeGraphPanes_Add_models_files_for_comparison_to_see_an_analysis_of_runs
                         );
            zedGraphRoc.Refresh();
            zedGraphQq.Refresh();
            zedGraphFiles.Refresh();
        }

        /// <summary>
        /// Initialize the given Zedgraph pane.
        /// </summary>
        private static void InitGraphPane(GraphPane graphPane, string xAxis, string yAxis, string titleName = null)
        {
            graphPane.Title.Text = titleName ?? string.Empty;
            graphPane.XAxis.Title.Text = xAxis;
            graphPane.YAxis.Title.Text = yAxis;
            graphPane.XAxis.MinSpace = 50;
            graphPane.XAxis.MajorTic.IsAllTics = false;
            graphPane.YAxis.MajorTic.IsAllTics = false;
            graphPane.XAxis.MinorTic.IsAllTics = false;
            graphPane.YAxis.MinorTic.IsAllTics = false;
            graphPane.Chart.Border.IsVisible = false;
            graphPane.Border.Color = Color.LightGray;
            graphPane.XAxis.Title.FontSpec.Size = 14;
            graphPane.YAxis.Title.FontSpec.Size = 14;
            graphPane.IsFontsScaled = false;
        }

        public static void ClearGraphPane(GraphPane graphPane)
        {
            graphPane.CurveList.Clear();
            graphPane.GraphObjList.Clear();
        }

        public const double MARGIN = 1.1;
        public const double MIN_SIG_FP = 5;
        public const double Q_VALUE_SIG = 0.05;
        public const double X_AXIS_FOCUS = 0.1;

        public void UpdateGraph()
        {
            var rocPane = zedGraphRoc.GraphPane;
            var qqPane = zedGraphQq.GraphPane;
            var filesPane = zedGraphFiles.GraphPane;
            ClearGraphPane(rocPane);
            ClearGraphPane(qqPane);
            ClearGraphPane(filesPane);
            if (!_peakBoundaryList.Any(comp => comp.IsActive))
            {
                InitializeGraphPanes();
                return;
            }
            rocPane.Title.Text = Resources.ComparePeakPickingDlg_UpdateGraph_ROC_Plot_Comparison;
            qqPane.Title.Text = Resources.ComparePeakPickingDlg_UpdateGraph_Q_Q_Comparison;
            filesPane.Title.Text = Resources.ComparePeakPickingDlg_UpdateGraph_Replicate_Comparison;
            int i = -1;
            const double minRocY = 0;
            const double minRocX = 0;
            double maxRocY = 0;
            double maxRocX = 0;
            double maxFilesY = 0;
            double maxFilesX = 0;
            double maxQq = 1e-6;
            double minQq = 1;
            var filesNames = new List<string>();
            foreach (var comparer in _peakBoundaryList)
            {
                ++i;
                if (!comparer.IsActive)
                    continue;
                PointPairList rocPoints;
                MakeRocLists(comparer, Normalizer, out rocPoints);
                maxRocY = Math.Max(maxRocY, rocPoints.Select(point => point.Y).Max());
                maxRocX = _xAxisFocus ? X_AXIS_FOCUS : Math.Max(maxRocX, rocPoints.Select(point => point.X).Max());
                var rocCurve = new LineItem(comparer.Name, rocPoints, _colors[i % _colors.Count], SymbolType.None, 3);
                rocPane.CurveList.Add(rocCurve);
                if (_showFpCutoff)
                {
                    PlaceLabelAtCutoff(rocPoints, rocPane, Q_VALUE_SIG);
                }

                PointPairList filesPoints;
                MakeFilesLists(comparer, _qValueSig, out filesPoints, out filesNames);
                maxFilesY = Math.Max(maxFilesY, filesPoints.Select(point => point.Y).Max());
                maxFilesX = Math.Max(maxFilesX, filesPoints.Select(point => point.X).Max());
                filesPane.AddBar(comparer.Name , filesPoints, _colors[i % _colors.Count]);

                if (!comparer.HasNoQValues)
                {
                    PointPairList qqPoints;
                    MakeQValueLists(comparer, out qqPoints);
                    double maxQqPoint = qqPoints.Select(point => Math.Max(point.X, point.Y)).Max();
                    var qqNonzeros = qqPoints.Where(point => point.Y > 0).Select(point => point.Y).ToList();
                    double minQqYPoint = qqNonzeros.Any() ? qqNonzeros.Min() : 0.001;
                    maxQq = Math.Max(maxQq, maxQqPoint);
                    minQq = Math.Min(minQq, minQqYPoint);
                    var qqCurve = new LineItem(comparer.Name, qqPoints, _colors[i % _colors.Count], SymbolType.None, 3);
                    qqPane.CurveList.Add(qqCurve);

                    if (_showFpCutoffQ)
                    {
                        PlaceLabelAtCutoff(qqPoints, qqPane, Q_VALUE_SIG);    
                    }
                }
            }
            var minQqPlot = Math.Min(minQq * MIN_SIG_FP, Q_VALUE_SIG * 0.5);
            var maxQqPlot = Math.Max(maxQq * MARGIN, Q_VALUE_SIG * MARGIN);
            var equalityPoints = new PointPairList { { minQqPlot, minQqPlot }, { 1, 1 } };
            var equalityCurve = new LineItem(Resources.ComparePeakPickingDlg_UpdateGraph_Equality, equalityPoints, Color.Black, SymbolType.None);
            qqPane.CurveList.Add(equalityCurve);
            if (_showFpCutoffQ)
            {
                var significancePoints = new PointPairList { { Q_VALUE_SIG, minQq }, { Q_VALUE_SIG, maxQq } };
                var significanceCurve = new LineItem(Resources.ComparePeakPickingDlg_UpdateGraph__0_05_significance_threshold, significancePoints, Color.Red, SymbolType.None);
                qqPane.CurveList.Add(significanceCurve);    
            }
            if (_showFpCutoff)
            {
                var significancePointsRoc = new PointPairList { { Q_VALUE_SIG, minRocY }, { Q_VALUE_SIG, maxRocY * MARGIN } };
                var significanceCurveRoc = new LineItem(Resources.ComparePeakPickingDlg_UpdateGraph__0_05_observed_false_positive_rate, significancePointsRoc, Color.Red, SymbolType.None);
                rocPane.CurveList.Add(significanceCurveRoc);    
            }

            rocPane.XAxis.Scale.Min = minRocX;
            rocPane.XAxis.Scale.Max = maxRocX * MARGIN;
            rocPane.YAxis.Scale.Min = minRocY;
            rocPane.YAxis.Scale.Max = maxRocY * MARGIN;
            qqPane.XAxis.Scale.Min = minQqPlot;
            qqPane.XAxis.Scale.Max = maxQqPlot;
            qqPane.YAxis.Scale.Min = minQqPlot;
            qqPane.YAxis.Scale.Max = maxQqPlot;
            qqPane.XAxis.Type = AxisType.Log;
            qqPane.YAxis.Type = AxisType.Log;
            filesPane.XAxis.Scale.Min = 0;
            filesPane.XAxis.Scale.Max = maxFilesX + 1;
            filesPane.YAxis.Scale.Min = 0;
            filesPane.YAxis.Scale.Max = maxFilesY * MARGIN;
            zedGraphRoc.AxisChange();
            zedGraphRoc.Refresh();
            zedGraphQq.AxisChange();
            zedGraphQq.Refresh();
            zedGraphFiles.AxisChange();
            zedGraphFiles.Refresh();
            filesPane.XAxis.Scale.TextLabels = filesNames.ToArray();
        }

        private static void PlaceLabelAtCutoff(IEnumerable<PointPair> graphPoints, GraphPane graphPane, double cutoff)
        {
             var closestPointToCutoff = graphPoints.FirstOrDefault(point => point.X > cutoff);
            if (closestPointToCutoff != null)
            {
                double y = closestPointToCutoff.Y;
                TextObj text = new TextObj(y.ToString("0.##"), cutoff, y) // Not L10N
                {
                    FontSpec = {FontColor = Color.Black, StringAlignment = StringAlignment.Center, Size = 11.0F}
                };
                text.FontSpec.Border.Color = Color.Transparent;
                graphPane.GraphObjList.Add(text);
            }
        }

        private void zedGraph_ContextMenuBuilder(ZedGraphControl graphControl, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(graphControl, menuStrip);
        }

        private void checkBoxClipBottom_CheckedChanged(object sender, EventArgs e)
        {
            _xAxisFocus = checkBoxXRange.Checked;
            UpdateGraph();
        }

        private void checkBoxIDLabels_CheckedChanged(object sender, EventArgs e)
        {
            _showFpCutoff = checkBoxIDLabels.Checked;
            UpdateGraph();

        }

        private void checkBoxExpectedFp_CheckedChanged(object sender, EventArgs e)
        {
            _showFpCutoffQ = checkBoxExpectedFp.Checked;
            UpdateGraph();
        }

        private void buttonApply_Click(object sender, EventArgs e)
        {
            UpdateTextBox();
            UpdateGraph();
        }

        public void UpdateTextBox()
        {
            double qCutoff;
            var helper = new MessageBoxHelper(this);
            if (!helper.ValidateDecimalTextBox(textBoxFilesQCutoff, 0.0, 1.0, out qCutoff))
                return;
            _qValueSig = qCutoff;
        }

        #endregion

        #region Functional test support

        public ZedGraphControl ZedGraphRoc { get { return zedGraphRoc; } }
        
        public ZedGraphControl ZedGraphQq { get { return zedGraphQq; } }

        public ZedGraphControl ZedGraphFile {get { return zedGraphFiles; } }

        public int CountRocCurves { get { return zedGraphRoc.GraphPane.CurveList.Count; } }
        
        public int CountQCurves { get { return zedGraphQq.GraphPane.CurveList.Count; } }

        public int CountFileCurves { get { return zedGraphFiles.GraphPane.CurveList.Count; } }

        public int CountDetailsItems { get { return comboBoxDetails.Items.Count; } }

        public int CountCompare1Items { get { return comboBoxCompare1.Items.Count; } }

        public int CountCompare2Items { get { return comboBoxCompare2.Items.Count; } }

        public ComparePeakBoundariesList ComparePeakBoundariesList { get { return _peakBoundaryList; } }

        public bool IsCheckedComparer(int index)
        {
            return checkedListCompare.GetItemChecked(index);
        }

        public void SetCheckedComparer(int index, bool value)
        {
            checkedListCompare.SetItemChecked(index, value);
        }

        public int CountDetailsGridEntries { get { return _scoreGridViewDriver.Items.Count; } }

        public int CountCompareGridEntries { get { return _compareGridViewDriver.Items.Count; } }

        public bool CheckBoxConflicts
        {
            get { return checkBoxConflicts.Checked; }
            set { checkBoxConflicts.Checked = value; }
        }

        public string ComboCompare1Selected
        {
            get { return comboBoxCompare1.SelectedItem.ToString(); }
            set
            {
                foreach (var item in comboBoxCompare1.Items)
                {
                    if (item.ToString() == value)
                    {
                        comboBoxCompare1.SelectedItem = item;
                        return;
                    }
                }
                throw new InvalidDataException(Resources.EditPeakScoringModelDlg_SelectedModelItem_Invalid_Model_Selection);
            }
        }

        public string ComboCompare2Selected
        {
            get { return comboBoxCompare2.SelectedItem.ToString(); }
            set
            {
                foreach (var item in comboBoxCompare2.Items)
                {
                    if (item.ToString() == value)
                    {
                        comboBoxCompare2.SelectedItem = item;
                        return;
                    }
                }
                throw new InvalidDataException(Resources.EditPeakScoringModelDlg_SelectedModelItem_Invalid_Model_Selection);
            }
        }

        public string ComboYAxis
        {
            get { return comboBoxYAxis.SelectedItem.ToString(); }
            set
            {
                foreach (var item in comboBoxYAxis.Items)
                {
                    if (item.ToString() == value)
                    {
                        comboBoxYAxis.SelectedItem = item;
                        return;
                    }
                }
                throw new InvalidDataException("Invalid Y-axis selection");    // Not L10N
            }
        }

        #endregion
    }

    class ComparePeakBoundariesGridViewDriver : SimpleGridViewDriver<PeakBoundsMatch>
    {
        public ComparePeakBoundariesGridViewDriver(DataGridViewEx gridView,
                                         BindingSource bindingSource,
                                         SortableBindingList<PeakBoundsMatch> items)
            : base(gridView, bindingSource, items)
        {
        }

        protected override void DoPaste()
        {
            // No pasting.
        }
    }

    class ComparePeakBoundariesPairGridViewDriver : SimpleGridViewDriver<PeakBoundsMatchPair>
    {
        public ComparePeakBoundariesPairGridViewDriver(DataGridViewEx gridView,
                                         BindingSource bindingSource,
                                         SortableBindingList<PeakBoundsMatchPair> items)
            : base(gridView, bindingSource, items)
        {
        }

        protected override void DoPaste()
        {
            // No pasting.
        }
    }

    public sealed class ComparePeakBoundariesList : List<ComparePeakBoundaries>, IListDefaults<ComparePeakBoundaries>, IItemEditor<ComparePeakBoundaries>, IListEditorSupport
    {
        public ComparePeakBoundariesList()
        {
            
        }

        public ComparePeakBoundariesList(IEnumerable<ComparePeakBoundaries> existing)
            : base(existing)
        {
        }

        public IEnumerable<ComparePeakBoundaries> GetDefaults(int revisionIndex)
        {
            return new List<ComparePeakBoundaries>();
        }

        public int RevisionIndexCurrent {get { return 1; } }

        public int ExcludeDefaults { get { return 0; } }

        public string Title { get { return Resources.ComparePeakBoundariesList_Title_Edit_Peak_Boundary_Comparisons; } }

        public string Label { get { return Resources.ComparePeakBoundariesList_Label__Model_or_File_; } }

        public bool AllowReset { get { return true; } }

        public ComparePeakBoundaries CopyItem(ComparePeakBoundaries item)
        {
            return (ComparePeakBoundaries)item.ChangeName(string.Empty);
        }

        public ComparePeakBoundaries NewItem(Control owner, IEnumerable<ComparePeakBoundaries> existing, object tag)
        {
            return EditItem(owner, default(ComparePeakBoundaries), existing, tag);
        }

        public ComparePeakBoundaries EditItem(Control owner, ComparePeakBoundaries item,
            IEnumerable<ComparePeakBoundaries> existing, object tag)
        {
            var document = tag as SrmDocument;
            using (var dlg = new AddPeakCompareDlg(document, item, existing))
            {
                return dlg.ShowDialog(owner) == DialogResult.OK ?
                    dlg.BoundaryComparer :
                    default(ComparePeakBoundaries);
            }
        }
    }

    public sealed class PeakBoundsMatchPair
    {
        public PeakBoundsMatch Match1 { get; private set; }
        public PeakBoundsMatch Match2 { get; private set; }

        public PeakBoundsMatchPair(PeakBoundsMatch match1, PeakBoundsMatch match2)
        {
           Match1 = match1;
           Match2 = match2;
        }

        public string FileName { get { return Match1.FileName; } }
        public string Sequence { get { return Match1.Sequence; } }
        public int Charge { get { return Match1.Charge; } }
        public double? Score1 { get { return Match1.Score; } }
        public double? Score2 { get { return Match2.Score; } }
        public double? QValue1 { get { return Match1.QValue; } }
        public double? QValue2 { get { return Match2.QValue; } }
        public bool IsMatch1 { get { return Match1.IsPickedApexBetweenCuratedBoundaries; } }
        public bool IsMatch2 { get { return Match2.IsPickedApexBetweenCuratedBoundaries; } }
        public double? Apex1 { get { return Match1.PickedApex; } }
        public double? Apex2 { get { return Match2.PickedApex; } }
        public double? TrueStart { get { return Match1.TrueStartBoundary; } }
        public double? TrueEnd { get { return Match1.TrueEndBoundary; } }
    }
}
