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
                CalibrationPeptides.Add(new DbIrtPeptide(peptide.Sequence, iRT, true, TimeSource.peak));
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

            int count = document.Peptides.Count(nodePep => nodePep.SchedulingTime.HasValue);
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
                                              Sequence = values[0],
                                              RetentionTime = double.Parse(values[1])
                                          }));

                string message = ValidateUniquePeptides(standardPeptidesNew.Select(p => p.Sequence), null, null);
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
                var peps = FindEvenlySpacedPeptides(document, peptideCount);

                Items.RaiseListChangedEvents = false;
                try
                {
                    Items.Clear();
                    for (int i = 0; i < peps.Count; i++)
                    {
                        var pep = peps[i];
                        Items.Add(new StandardPeptide
                        {
                            Sequence = pep.Sequence,
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

            /// <summary>
            /// This algorithm will determine a number of evenly spaced retention times for the given document,
            /// and then determine an optimal set of peptides from the document. That is, a set of peptides that
            /// are as close as possible to the chosen retention times.
            /// 
            /// The returned list is guaranteed to be sorted by retention time.
            /// </summary>
            /// <param name="doc">An SrmDocument to get peptides from</param>
            /// <param name="peptideCount">The number of peptides desired</param>
            private static List<MeasuredPeptide> FindEvenlySpacedPeptides(SrmDocument doc, int peptideCount)
            {
                double minRT = float.PositiveInfinity;
                double maxRT = 0;
                List<MeasuredPeptide> docPeptides = new List<MeasuredPeptide>();
                foreach (var pep in doc.Peptides)
                {
                    if (pep.SchedulingTime.HasValue && !pep.IsDecoy)
                    {
                        docPeptides.Add(new MeasuredPeptide(doc.Settings.GetModifiedSequence(pep), pep.SchedulingTime.Value));

                        if (pep.SchedulingTime.Value < minRT)
                            minRT = pep.SchedulingTime.Value;
                        if (pep.SchedulingTime.Value > maxRT)
                            maxRT = pep.SchedulingTime.Value;
                    }
                }

                docPeptides.Sort((one, two) => one.RetentionTime.CompareTo(two.RetentionTime));

                if (docPeptides.Count == peptideCount)
                    return docPeptides;

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
    }

    public class StandardPeptide : MeasuredPeptide
    {
        public bool FixedPoint { get; set; }
    }
}