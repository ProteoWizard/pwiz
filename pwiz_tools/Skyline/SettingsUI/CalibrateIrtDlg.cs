/*
 * Original author: John Chilton <jchilton .at. uw.edu>,
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
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;

namespace pwiz.Skyline.SettingsUI
{
    public partial class CalibrateIrtDlg : Form
    {
        public const int MAX_DEFAULT_STANDARD_PEPTIDES = 15;
        public const int MIN_STANDARD_PEPTIDES = 5;
        public const int MIN_SUGGESTED_STANDARD_PEPTIDES = 10;

        private const int COL_CHECKBOX = 2;

        public List<DbIrtPeptide> CalibrationPeptides { get; private set; }

        public int BoxesChecked
        {
            get
            {
                int numChecked = 0;
                foreach (DataGridViewRow row in gridViewCalibrate.Rows)
                {
                    if (row.Cells[COL_CHECKBOX].Value != null && (bool)row.Cells[COL_CHECKBOX].Value)
                        numChecked++;
                }
                return numChecked;
            }
        }

        public CalibrateIrtDlg()
        {
            InitializeComponent();

            var peps = Program.ActiveDocumentUI.Peptides.ToList();
            var count = peps.Count(pep => pep.HasResults);

            textPeptideCount.Text = Math.Min(count, MAX_DEFAULT_STANDARD_PEPTIDES).ToString();
        }

        public List<MeasuredPeptide> Recalculate()
        {
            var document = Program.ActiveDocumentUI;

            if(!document.Settings.HasResults)
            {
                MessageDlg.Show(this, "The document must contain results to calibrate a standard.");
                return null;
            }

            var helper = new MessageBoxHelper(this);
            int peptideCount;
            if (!helper.ValidateNumberTextBox(new CancelEventArgs(), textPeptideCount, MIN_STANDARD_PEPTIDES, null, out peptideCount))
                return null;

            var peps = FindEvenlySpacedPeptides(document, peptideCount);

            gridViewCalibrate.SuspendLayout();
            gridViewCalibrate.Rows.Clear();
            for (int i = 0; i < peps.Count; i++)
            {
                var pep = peps[i];
                int n = gridViewCalibrate.Rows.Add();

                gridViewCalibrate.Rows[n].Cells[0].Value = pep.Sequence;
                gridViewCalibrate.Rows[n].Cells[1].Value = string.Format("{0:F04}", pep.RetentionTimeOrIrt.ToString());
                if (i == 0 || i == peps.Count - 1)
                {
                    gridViewCalibrate.Rows[n].Cells[2].Value = true;
                }
            }
            gridViewCalibrate.ResumeLayout();

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
        public List<MeasuredPeptide> FindEvenlySpacedPeptides(SrmDocument doc, int peptideCount)
        {
            double minRT = float.PositiveInfinity;
            double maxRT = 0;
            List<MeasuredPeptide> docPeptides = new List<MeasuredPeptide>();
            foreach(var pep in doc.Peptides)
            {
                if (pep.SchedulingTime.HasValue)
                {
                    docPeptides.Add(new MeasuredPeptide(pep.Peptide.Sequence, pep.SchedulingTime.Value));

                    if (pep.SchedulingTime.Value < minRT)
                        minRT = pep.SchedulingTime.Value;
                    if (pep.SchedulingTime.Value > maxRT)
                        maxRT = pep.SchedulingTime.Value;
                }
            }

            if (docPeptides.Count < peptideCount)
            {
                MessageDlg.Show(this,
                    String.Format("The document only contains {0} measured peptides. All of them will be used.",
                                  docPeptides.Count)); //*
                textPeptideCount.Text = docPeptides.Count.ToString();
            }

            docPeptides.Sort((one, two) => one.RetentionTimeOrIrt.CompareTo(two.RetentionTimeOrIrt));

            if (docPeptides.Count == peptideCount)
                return docPeptides; //*

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
                for(int j = 0; j < docPeptides.Count; j++)
                {
                    if(j + 1 > docPeptides.Count-1 ||
                       Math.Abs(docPeptides[j].RetentionTimeOrIrt - targetRT) < 
                       Math.Abs(docPeptides[j+1].RetentionTimeOrIrt - targetRT))
                    {
                        standardPeptides.Add(docPeptides[j]);
                        docPeptides.RemoveAt(j);
                        break;
                    }
                }
            }

            return standardPeptides;
        }

        public void OkDialog()
        {
            double minIrt;
            double maxIrt;

            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);
            if (!helper.ValidateDecimalTextBox(e, textMinIrt, null, null, out minIrt))
                return;
            if (!helper.ValidateDecimalTextBox(e, textMaxIrt, minIrt, null, out maxIrt))
                return;

            List<MeasuredPeptide> peptides = new List<MeasuredPeptide>();
            double? fixedPt1 = new double?();
            double? fixedPt2 = new double?();
            
            foreach (DataGridViewRow row in gridViewCalibrate.Rows)
            {
                if (row.IsNewRow)
                    continue;
                if (row.Cells[0].Value == null || row.Cells[1].Value == null)
                    continue;
                if (!EditIrtCalcDlg.ValidateRow(new[] { row.Cells[0].Value.ToString(), row.Cells[1].Value.ToString() }))
                {
                    MessageDlg.Show(this,
                        string.Format("There is an error with the standard peptides: cannot create a record from peptide {0} and measured RT {1}",
                            row.Cells[0].Value.ToString(), row.Cells[1].Value.ToString()));
                    return;
                }

                double iRT = double.Parse(row.Cells[1].Value.ToString());

                if (row.Cells[2].Value != null && (bool)row.Cells[2].Value)
                    if (!fixedPt1.HasValue)
                        fixedPt1 = iRT;
                    else if (!fixedPt2.HasValue)
                        fixedPt2 = iRT;
                    else
                    {
                        MessageDlg.Show(this, "The standard can only have two fixed points.");
                        return;
                    }

                peptides.Add(new MeasuredPeptide(row.Cells[0].Value.ToString(), iRT));
            }

            if(!fixedPt1.HasValue || !fixedPt2.HasValue)
            {
                MessageDlg.Show(this, "The standard must have two fixed points.");
                return;
            }

            double minRt = fixedPt1.Value < fixedPt2.Value ? fixedPt1.Value : fixedPt2.Value;
            double maxRt = fixedPt1.Value < fixedPt2.Value ? fixedPt2.Value : fixedPt1.Value;
            CalibrationPeptides = new List<DbIrtPeptide>();
            foreach (var pep in peptides)
            {
                double measuredRT = pep.RetentionTimeOrIrt;

                CalibrationPeptides.Add(new DbIrtPeptide(pep.Sequence,
                    Map(measuredRT, minRt, maxRt, minIrt, maxIrt), true));
            }

            DialogResult = DialogResult.OK;
        }

        public static double Map(double value, double startFrom, double endFrom, double startTo, double endTo)
        {
            return ((value - startFrom)/(endFrom - startFrom))*(endTo - startTo) + startTo;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnRecalc_Click(object sender, EventArgs e)
        {
            Recalculate();
        }

        private void gridViewCalibrate_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (gridViewCalibrate.IsCurrentCellDirty)
            {
                //It appears that the last condition should be non-inverted, but this event is fired before
                //the checkbox is checked, so IF the value is false, THEN the user is trying to check the box.
                var curCell = gridViewCalibrate.CurrentCell;
                if (curCell.ColumnIndex == 2 && BoxesChecked >= 2 && (curCell.Value == null || !(bool)curCell.Value))
                {
                    //If two are checked, uncheck the closest checked cell and check the just-checked cell
                    DataGridViewCell closestCell = null;
                    foreach (DataGridViewRow row in gridViewCalibrate.Rows)
                    {
                        var cell = row.Cells[2];
                        if(cell.RowIndex != curCell.RowIndex && cell.Value != null && (bool)cell.Value && (closestCell == null ||
                                Math.Abs(cell.RowIndex - curCell.RowIndex) < Math.Abs(closestCell.RowIndex - curCell.RowIndex)))
                            closestCell = cell;
                    }
                    if(closestCell != null)
                        closestCell.Value = false;
                }

                gridViewCalibrate.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        #region Functional Test Support

        public void SetNumPeptides(int count)
        {
            textPeptideCount.Text = count.ToString();
        }

        public void SetBoxesChecked(int one, int two)
        {
            //Get indexes from these
            one--;
            two--;

            //-2: one to get indices, one because of the "New Row"
            if (one == two || one > gridViewCalibrate.RowCount - 2 || two > gridViewCalibrate.RowCount)
                return;
            for (int i = 0; i < gridViewCalibrate.RowCount; i++)
            {
                gridViewCalibrate.Rows[i].Cells[2].Value = false;
                if(i == one || i == two)
                    gridViewCalibrate.Rows[i].Cells[2].Value = true;
            }
        }

        #endregion
    }

    public class StandardPeptide
    {
        public MeasuredPeptide MeasuredPeptide { get; set; }
        public bool FixedPoint { get; set; }
    }

    public class MeasuredPeptide
    {
        public string Sequence { get; set; }
        public double RetentionTimeOrIrt { get; set; }
        public MeasuredPeptide(string seq, double rt)
        {
            Sequence = seq;
            RetentionTimeOrIrt = rt;
        }
    }
}
