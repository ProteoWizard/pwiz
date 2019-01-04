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
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class CalibrateIrtDlg : FormEx
    {
        public const int MAX_DEFAULT_STANDARD_PEPTIDES = 15;
        public const int MIN_STANDARD_PEPTIDES = 5;
        public const int MIN_SUGGESTED_STANDARD_PEPTIDES = 10;

        private readonly CalibrationGridViewDriver _gridViewDriver;

        public CalibrateIrtDlg()
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _gridViewDriver = new CalibrationGridViewDriver(gridViewCalibrate, bindingSourceStandard,
                                                            new SortableBindingList<StandardPeptide>());
        }

        public IList<DbIrtPeptide> CalibrationPeptides { get; private set; }

        public SortableBindingList<StandardPeptide> StandardPeptideList { get { return _gridViewDriver.Items; } }

        public int StandardPeptideCount { get { return StandardPeptideList.Count; } }

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
            gridViewCalibrate.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        public void OkDialog()
        {
            var irts = IrtStandard.CIRT.Peptides.ToDictionary(p => p.GetNormalizedModifiedSequence(), p => p.Irt);
            var calibrationPeptides = new List<Tuple<DbIrtPeptide, double>>();
            foreach (var pep in StandardPeptideList)
            {
                double irt;
                if (!irts.TryGetValue(SequenceMassCalc.NormalizeModifiedSequence(pep.Target), out irt))
                    break;
                calibrationPeptides.Add(new Tuple<DbIrtPeptide, double>(new DbIrtPeptide(pep.Target, irt, true, TimeSource.peak), pep.RetentionTime));
            }
            if (calibrationPeptides.Count == StandardPeptideList.Count)
            {
                var statStandard = new Statistics(calibrationPeptides.Select(p => p.Item1.Irt));
                var statMeasured = new Statistics(calibrationPeptides.Select(p => p.Item2));
                if (statStandard.R(statMeasured) >= RCalcIrt.MIN_IRT_TO_TIME_CORRELATION)
                {
                    var result = MultiButtonMsgDlg.Show(this,
                        Resources.CalibrateIrtDlg_OkDialog_All_of_these_peptides_are_known_CiRT_peptides__Would_you_like_to_use_the_predefined_iRT_values_,
                        MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true);
                    if (result == DialogResult.Cancel)
                        return;

                    if (result == DialogResult.Yes)
                    {
                        CalibrationPeptides = calibrationPeptides.Select(x => x.Item1).ToList();
                        DialogResult = DialogResult.OK;
                        return;
                    }
                }
            }

            double minIrt;
            double maxIrt;

            var helper = new MessageBoxHelper(this);
            if (!helper.ValidateDecimalTextBox(textMinIrt, null, null, out minIrt))
                return;
            if (!helper.ValidateDecimalTextBox(textMaxIrt, minIrt, null, out maxIrt))
                return;

            int iFixed1 = -1, iFixed2 = -1;
            for (int i = 0; i < StandardPeptideList.Count; i++)
            {
                if (!StandardPeptideList[i].FixedPoint)
                    continue;
                if (iFixed1 == -1)
                    iFixed1 = i;
                else
                    iFixed2 = i;
            }

            if(iFixed1 == -1 || iFixed2 == -1)
            {
                MessageDlg.Show(this, Resources.CalibrateIrtDlg_OkDialog_The_standard_must_have_two_fixed_points);
                return;
            }

            double fixedPt1 = StandardPeptideList[iFixed1].RetentionTime;
            double fixedPt2 = StandardPeptideList[iFixed2].RetentionTime;

            double minRt = Math.Min(fixedPt1, fixedPt2);
            double maxRt = Math.Max(fixedPt1, fixedPt2);

            var statRt = new Statistics(minRt, maxRt);
            var statIrt = new Statistics(minIrt, maxIrt);
            var linearEquation = new RegressionLine(statIrt.Slope(statRt), statIrt.Intercept(statRt));

            CalibrationPeptides = new List<DbIrtPeptide>();
            foreach (var peptide in StandardPeptideList)
            {
                double iRT = linearEquation.GetY(peptide.RetentionTime);
                CalibrationPeptides.Add(new DbIrtPeptide(peptide.Target, iRT, true, TimeSource.peak));
            }

            DialogResult = DialogResult.OK;
        }

        public static double Transform(double value, double startFrom, double endFrom, double startTo, double endTo)
        {
            return ((value - startFrom)/(endFrom - startFrom))*(endTo - startTo) + startTo;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnUseCurrent_Click(object sender, EventArgs e)
        {
            UseResults();
        }

        public void UseResults()
        {
            CheckDisposed();
            var document = Program.ActiveDocumentUI;

            if(!document.Settings.HasResults)
            {
                MessageDlg.Show(this, Resources.CalibrateIrtDlg_UseResults_The_document_must_contain_results_to_calibrate_a_standard);
                return;
            }

            var targetResolver = TargetResolver.MakeTargetResolver(document);
            calibratePeptides.TargetResolver = targetResolver;

            int count = document.Molecules.Count(nodePep => nodePep.SchedulingTime.HasValue);
            if (count > 20)
            {
                using (var dlg = new AddIrtStandardsDlg(count))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    count = dlg.StandardCount;
                }
            }

            _gridViewDriver.Recalculate(document, count);
        }

        private class CalibrationGridViewDriver : PeptideGridViewDriver<StandardPeptide>
        {
            private const int COLUMN_FIXED = 2;

            public CalibrationGridViewDriver(DataGridViewEx gridView,
                                             BindingSource bindingSource,
                                             SortableBindingList<StandardPeptide> items)
                : base(gridView, bindingSource, items)
            {
                GridView.CurrentCellDirtyStateChanged += gridView_CurrentCellDirtyStateChanged;
            }

            private int FixedPointCount
            {
                get { return Items.Count(p => p.FixedPoint); }
            }

            protected override void DoPaste()
            {
                var standardPeptidesNew = new List<StandardPeptide>();
                GridView.DoPaste(MessageParent, ValidateRowWithTime,
                                          values =>
                                          standardPeptidesNew.Add(new StandardPeptide
                                          {
                                              Target = new Target(values[0]),
                                              RetentionTime = double.Parse(values[1])
                                          }));

                string message = ValidateUniquePeptides(standardPeptidesNew.Select(p => p.Target), null, null);
                if (message != null)
                {
                    MessageDlg.Show(MessageParent, message);
                    return;
                }

                if (standardPeptidesNew.Count > 0)
                {
                    standardPeptidesNew[0].FixedPoint = true;
                    standardPeptidesNew[standardPeptidesNew.Count - 1].FixedPoint = true;
                }
                Items.Clear();
                foreach (var peptide in standardPeptidesNew)
                    Items.Add(peptide);
            }

            protected override bool DoRowValidating(int rowIndex)
            {
                if (!base.DoRowValidating(rowIndex))
                    return false;

                if (FixedPointCount < 2 && rowIndex < Items.Count)
                    Items[rowIndex].FixedPoint = true;

                return true;
            }

            private void gridView_CurrentCellDirtyStateChanged(object sender, EventArgs e)
            {
                if (GridView.IsCurrentCellDirty)
                {
                    //It appears that the last condition should be non-inverted, but this event is fired before
                    //the checkbox is checked, so IF the value is false, THEN the user is trying to check the box.
                    var curCell = GridView.CurrentCell;
                    if (!curCell.OwningRow.IsNewRow &&
                            curCell.ColumnIndex == COLUMN_FIXED &&
                            FixedPointCount >= 2 &&
                            (curCell.Value == null || !(bool)curCell.Value))
                    {
                        //If two are checked, uncheck the closest checked cell and check the just-checked cell
                        DataGridViewCell closestCell = null;
                        foreach (DataGridViewRow row in GridView.Rows)
                        {
                            var cell = row.Cells[COLUMN_FIXED];
                            if (cell.RowIndex != curCell.RowIndex && cell.Value != null && (bool)cell.Value && (closestCell == null ||
                                    Math.Abs(cell.RowIndex - curCell.RowIndex) < Math.Abs(closestCell.RowIndex - curCell.RowIndex)))
                                closestCell = cell;
                        }
                        if (closestCell != null)
                            closestCell.Value = false;

                        GridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    }
                }
            }

            public List<MeasuredPeptide> Recalculate(SrmDocument document, int peptideCount)
            {
                var peps = FindBestPeptides(document, peptideCount);

                Items.RaiseListChangedEvents = false;
                try
                {
                    Items.Clear();
                    for (int i = 0; i < peps.Count; i++)
                    {
                        var pep = peps[i];
                        Items.Add(new StandardPeptide
                        {
                            Target = pep.Target,
                            RetentionTime = pep.RetentionTime,
                            FixedPoint = (i == 0 || i == peps.Count - 1)
                        });
                    }
                }
                finally
                {
                    Items.RaiseListChangedEvents = true;
                }
                Items.ResetBindings();

                return peps;
            }

            private static List<MeasuredPeptide> FindBestPeptides(SrmDocument doc, int peptideCount)
            {
                var docPeptides = new List<MeasuredPeptide>();
                var cirtPeptides = new List<MeasuredPeptide>();
                foreach (var pep in doc.Molecules)
                {
                    if (pep.PercentileMeasuredRetentionTime.HasValue && !pep.IsDecoy)
                    {
                        var seq = doc.Settings.GetModifiedSequence(pep);
                        var time = pep.PercentileMeasuredRetentionTime.Value;
                        var measuredPeptide = new MeasuredPeptide(seq, time);
                        if (!IrtStandard.CIRT.Contains(seq))
                            docPeptides.Add(measuredPeptide);
                        else
                            cirtPeptides.Add(measuredPeptide);
                    }
                }

                if (cirtPeptides.Count >= peptideCount)
                    return IrtPeptidePicker.Filter(cirtPeptides, peptideCount).ToList();

                docPeptides.AddRange(cirtPeptides);
                return FindEvenlySpacedPeptides(docPeptides, peptideCount);
            }

            /// <summary>
            /// This algorithm will determine a number of evenly spaced retention times for the given document,
            /// and then determine an optimal set of peptides from the document. That is, a set of peptides that
            /// are as close as possible to the chosen retention times.
            /// 
            /// The returned list is guaranteed to be sorted by retention time.
            /// </summary>
            /// <param name="docPeptides">Peptides to choose from</param>
            /// <param name="peptideCount">The number of peptides desired</param>
            private static List<MeasuredPeptide> FindEvenlySpacedPeptides(List<MeasuredPeptide> docPeptides, int peptideCount)
            {
                docPeptides.Sort((x, y) => x.RetentionTime.CompareTo(y.RetentionTime));
                if (docPeptides.Count == peptideCount)
                    return docPeptides;

                var minRT = docPeptides.First().RetentionTime;
                var maxRT = docPeptides.Last().RetentionTime;

                /*
                 * This algorithm will pick the closest peptide to each "target RT" as defined
                 * by the "length of the gradient" (Last peptide's RT - First peptide's RT) and
                 * the number of peptides asked for.
                 * 
                 * It does this by considering peptides 3 at a time: (prev, current, next) triplets.
                 * When the pointer has shifted so that the "current" peptide is closer than
                 * either of its neighbors, then that peptide is added to the standard and removed
                 * from the search list.
                 */
                List<MeasuredPeptide> standardPeptides = new List<MeasuredPeptide>();
                double gradientLength = maxRT - minRT;
                for (int i = 0; i < peptideCount; i++)
                {
                    double targetRT = minRT + i * (gradientLength / (peptideCount - 1));
                    for (int j = 0; j < docPeptides.Count; j++)
                    {
                        if (j + 1 > docPeptides.Count - 1 ||
                                Math.Abs(docPeptides[j].RetentionTime - targetRT) <
                                Math.Abs(docPeptides[j + 1].RetentionTime - targetRT))
                        {
                            standardPeptides.Add(docPeptides[j]);
                            docPeptides.RemoveAt(j);
                            break;
                        }
                    }
                }

                return standardPeptides;
            }
        }

        #region Functional Test Support

        public List<MeasuredPeptide> Recalculate(SrmDocument document, int peptideCount)
        {
            return _gridViewDriver.Recalculate(document, peptideCount);
        }

        public void SetFixedPoints(int one, int two)
        {
            int count = StandardPeptideList.Count;
            if (one >= two || two >= count)
                return;
            for (int i = 0; i < count; i++)
            {                
                bool fixedPoint = (i == one || i == two);
                var peptide = StandardPeptideList[i];
                if (peptide.FixedPoint != fixedPoint)
                {
                    peptide.FixedPoint = fixedPoint;
                    StandardPeptideList.ResetItem(i);
                }
            }
        }

        #endregion

        public class IrtPeptidePicker
        {
            private List<Tuple<MeasuredPeptide, double>>[] _bins;

            public const int BIN_FACTOR = 4;

            public int BinCount { get { return _bins.Length; } }
            public int PeptideCount { get { return _bins.Sum(bin => bin.Count); } }

            public IEnumerable<MeasuredPeptide> MeasuredPeptides { get { return from bin in _bins from tuple in bin select tuple.Item1; } }
            public IEnumerable<double> Irts { get { return from bin in _bins from tuple in bin select tuple.Item2; } }
            public double R { get { return new Statistics(MeasuredPeptides.Select(p => p.RetentionTime)).R(new Statistics(Irts)); } }

            public IrtPeptidePicker(IEnumerable<MeasuredPeptide> peptides)
            {
                var listMeasured = new List<MeasuredPeptide>();
                var listIrt = new List<double>();

                var irts = IrtStandard.CIRT.Peptides.ToDictionary(p =>
                    p.GetNormalizedModifiedSequence(), p => p.Irt);

                foreach (var peptide in peptides)
                {
                    var normalizedModSeq = SequenceMassCalc.NormalizeModifiedSequence(peptide.Target);
                    double irtValue;
                    if (irts.TryGetValue(normalizedModSeq, out irtValue))
                    {
                        listMeasured.Add(peptide);
                        listIrt.Add(irtValue);
                    }
                }

                _bins = listMeasured.Any()
                    ? Bin(listMeasured, listIrt, listMeasured.Count/BIN_FACTOR)
                    : new List<Tuple<MeasuredPeptide, double>>[0];
            }

            public static IEnumerable<MeasuredPeptide> Filter(IEnumerable<MeasuredPeptide> peptides, int numPeptides)
            {
                var picker = new IrtPeptidePicker(peptides);
                picker.Filter(numPeptides);
                return picker.MeasuredPeptides.OrderBy(p => p.RetentionTime);
            }

            public void Filter(int numPeptides)
            {
                while (PeptideCount > numPeptides)
                {
                    // Find bin with most peptides
                    var bin = 0;
                    var maxBinCount = 0;
                    for (var i = 0; i < _bins.Length; i++)
                    {
                        if (_bins[i].Count > maxBinCount)
                        {
                            bin = i;
                            maxBinCount = _bins[i].Count;
                        }
                    }
                    // Attempt to remove outlier from bin with most peptides
                    var outlier = OutlierCandidate(_bins[bin]);
                    if (outlier == -1)
                    {
                        // If not successful re-bin with fewer bins or break if at 1
                        if (_bins.Length == 1)
                            return;
                        Rebin(_bins.Length - 1);
                        continue;
                    }

                    _bins[bin].RemoveAt(outlier);
                }
            }

            private static int OutlierCandidate(IReadOnlyCollection<Tuple<MeasuredPeptide, double>> bin)
            {
                if (bin.Count == 1)
                    return 0;
                else if (bin.Count == 2)
                    return -1;

                // Drop one value and see which improves the correlation the most
                var candidate = -1;
                var bestCorrelation = double.MinValue;
                for (var i = 0; i < bin.Count; i++)
                {
                    var thisIndex = i;
                    var statMeasured = new Statistics(bin.Select(t => t.Item1).Where((x, j) => j != thisIndex).Select(p => p.RetentionTime));
                    var statIrt = new Statistics(bin.Select(t => t.Item2).Where((x, j) => j != thisIndex));
                    var correlation = statMeasured.R(statIrt);
                    if (correlation > bestCorrelation)
                    {
                        candidate = i;
                        bestCorrelation = correlation;
                    }
                }
                return candidate;
            }

            private static List<Tuple<MeasuredPeptide, double>>[] Bin(IList<MeasuredPeptide> peptides, IList<double> irts, int numBins)
            {
                Assume.IsTrue(peptides.Count.Equals(irts.Count));
                var rtMin = peptides.Min(p => p.RetentionTime);
                var rtRange = peptides.Max(p => p.RetentionTime) - rtMin;

                var bins = new List<Tuple<MeasuredPeptide, double>>[numBins];
                for (var i = 0; i < numBins; i++)
                    bins[i] = new List<Tuple<MeasuredPeptide, double>>();

                for (var i = 0; i < peptides.Count; i++)
                {
                    var bin = (int) (numBins*(peptides[i].RetentionTime - rtMin)/rtRange);
                    if (bin >= numBins)
                        bin = numBins - 1;
                    bins[bin].Add(new Tuple<MeasuredPeptide, double>(peptides[i], irts[i]));
                }
                return bins;
            }

            private void Rebin(int numBins)
            {
                double rtMin = double.MaxValue, rtMax = double.MinValue;
                foreach (var rt in from oldBin in _bins from tuple in oldBin select tuple.Item1.RetentionTime)
                {
                    if (rt < rtMin)
                        rtMin = rt;
                    if (rt > rtMax)
                        rtMax = rt;
                }
                var rtRange = rtMax - rtMin;

                var bins = new List<Tuple<MeasuredPeptide, double>>[numBins];
                for (var i = 0; i < numBins; i++)
                    bins[i] = new List<Tuple<MeasuredPeptide, double>>();

                foreach (var oldBin in _bins)
                {
                    foreach (var tuple in oldBin)
                    {
                        var bin = (int) (numBins*(tuple.Item1.RetentionTime - rtMin)/rtRange);
                        if (bin >= numBins)
                            bin = numBins - 1;
                        bins[bin].Add(tuple);
                    }
                }
                _bins = bins;
            }
        }
    }

    public class StandardPeptide : MeasuredPeptide
    {
        public bool FixedPoint { get; set; }
    }
}