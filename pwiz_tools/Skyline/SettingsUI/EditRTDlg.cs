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
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditRTDlg : Form
    {
        private RetentionTimeRegression _regression;
        private readonly IEnumerable<RetentionTimeRegression> _existing;
        private RetentionTimeStatistics _statistics;

        public EditRTDlg(IEnumerable<RetentionTimeRegression> existing)
        {
            _existing = existing;

            InitializeComponent();

            Icon = Resources.Skyline;

            foreach (string item in RetentionTimeRegression.GetRetentionScoreCalcNames())
                comboCalculator.Items.Add(item);

            ShowPeptides(Settings.Default.EditRTVisible);
        }

        public RetentionTimeRegression Regression
        {
            get { return _regression; }

            set
            {
                _regression = value;
                if (_regression == null)
                {
                    textName.Text = "";
                }
                else
                {
                    textName.Text = _regression.Name;

                    var pepTimes = _regression.PeptideTimes;
                    if (pepTimes.Count > 0)
                    {
                        ShowPeptides(true);
                        foreach (var pepTime in pepTimes)
                        {
                            gridPeptides.Rows.Add(pepTime.PeptideSequence,
                                string.Format("{0:F02}", pepTime.RetentionTime));                            
                        }

                        // Get statistics
                        RecalcRegression(_regression.Calculator.Name);
                    }

                    textSlope.Text = string.Format("{0:F04}", _regression.Conversion.Slope);
                    textIntercept.Text = string.Format("{0:F04}", _regression.Conversion.Intercept);
                    textTimeWindow.Text = string.Format("{0:F04}", _regression.TimeWindow);
                    comboCalculator.SelectedItem = _regression.Calculator.Name;
                }
            }
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(e, textName, out name))
                return;

            if (_existing.Contains(r => !ReferenceEquals(_regression, r) && Equals(name, r.Name)))
            {
                helper.ShowTextBoxError(textName, "The retention time regression '{0}' already exists.", name);
                e.Cancel = true;
                return;
            }

            double slope;
            if (!helper.ValidateDecimalTextBox(e, textSlope, out slope))
                return;

            double intercept;
            if (!helper.ValidateDecimalTextBox(e, textIntercept, out intercept))
                return;

            double window;
            if (!helper.ValidateDecimalTextBox(e, textTimeWindow, out window))
                return;

            if (window <= 0)
            {
                helper.ShowTextBoxError(textTimeWindow, "{0} must be greater than 0.");
                return;
            }

            if (comboCalculator.SelectedIndex == -1)
            {
                MessageBox.Show(this, "Retention time prediction requires a calculator algorithm.", Program.Name);
                comboCalculator.Focus();
                return;
            }
            string calculator = comboCalculator.SelectedItem.ToString();

            var listPeptides = new List<MeasuredRetentionTime>();
            if (!ValidatePeptides(e, listPeptides))
                return;

            RetentionTimeRegression regression =
                new RetentionTimeRegression(name, calculator, slope, intercept, window, listPeptides);

            _regression = regression;

            DialogResult = DialogResult.OK;
        }

        private bool ValidatePeptides(CancelEventArgs e, ICollection<MeasuredRetentionTime> listPeptides)
        {
            string[] fields = new string[2];
            foreach (DataGridViewRow row in gridPeptides.Rows)
            {
                if (row.IsNewRow)
                    continue;

                object val = row.Cells[0].Value;
                fields[0] = (val == null ? "" : val.ToString());
                val = row.Cells[1].Value;
                fields[1] = (val == null ? "" : val.ToString());
                int colError;
                if (!ValidatePeptideCellValues(fields, out colError))
                {
                    gridPeptides.ClearSelection();
                    gridPeptides.CurrentCell = row.Cells[colError];
                    gridPeptides.BeginEdit(true);
                    e.Cancel = true;
                    return false;                    
                }
                listPeptides.Add(new MeasuredRetentionTime(fields[0], double.Parse(fields[1])));
            }
            return true;
        }

        public void ShowPeptides(bool visible)
        {
            bool hasResults = Program.ActiveDocumentUI.Settings.HasResults;

            Control ctlCorner;
            int yExtra = 0;
            if (visible)
            {
                btnCalculate.Text = "&Calculate <<";
                ctlCorner = gridPeptides;
                yExtra = btnUseCurrent.Height + 10;
            }
            else
            {
                btnCalculate.Text = "&Calculate >>";
                ctlCorner = btnCalculate;
            }

            Point ptCorner = new Point(ctlCorner.Right, ctlCorner.Bottom + yExtra);
            ptCorner = PointToScreen(ptCorner);
            ptCorner.Offset(20, 20);

            if (!visible)
            {
                gridPeptides.Anchor &= ~AnchorStyles.Bottom;
                btnUseCurrent.Anchor |= ~AnchorStyles.Top;
                btnUseCurrent.Anchor &= ~AnchorStyles.Bottom;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                Height = ptCorner.Y - Top;
            }

            labelPeptides.Visible = visible;
            labelRValue.Visible = visible;
            btnUseCurrent.Visible = visible;
            btnUseCurrent.Enabled = hasResults;
            gridPeptides.Visible = visible;

            if (visible)
            {
                Height = ptCorner.Y - Top;
                FormBorderStyle = FormBorderStyle.Sizable;
                btnUseCurrent.Anchor |= AnchorStyles.Bottom;
                btnUseCurrent.Anchor &= ~AnchorStyles.Top;
                gridPeptides.Anchor |= AnchorStyles.Bottom;
            }
        }

        private void comboCalculator_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (gridPeptides.Rows.Count > 1 && comboCalculator.SelectedItem != null)
                RecalcRegression(comboCalculator.SelectedItem.ToString());
        }

        private void btnCalculate_Click(object sender, EventArgs e)
        {
            ShowPeptides(!gridPeptides.Visible);
        }

        private void btnUseCurrent_Click(object sender, EventArgs e)
        {
            var document = Program.ActiveDocumentUI;
            if (!document.Settings.HasResults)
                return; // This shouldn't be possible, but just to be safe.
            if (!document.Settings.MeasuredResults.IsLoaded)
            {
                MessageBox.Show(this, "Measured results must be completely loaded before they can be used to create a retention time regression.", Program.Name);
                return;
            }

            var rowsNew = new List<string[]>();

            foreach (PeptideDocNode nodePeptide in document.Peptides)
            {
                if (nodePeptide.AveragePeakCountRatio < 0.5)
                    continue;

                float? retentionTime = nodePeptide.AverageMeasuredRetentionTime;
                if (!retentionTime.HasValue)
                    continue;

                string seq = nodePeptide.Peptide.Sequence;

                rowsNew.Add(new[] { seq, string.Format("{0:F02}", retentionTime)});
            }

            if (rowsNew.Count == 0)
            {
                MessageBox.Show(this, "No usable retention times found.", Program.Name);
                return;
            }

            gridPeptides.SuspendLayout();

            gridPeptides.Rows.Clear();
            foreach (var columns in rowsNew)
                gridPeptides.Rows.Add(columns);

            gridPeptides.ResumeLayout();
            RecalcRegression();
        }

        private void gridPeptides_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl + V for paste
            if (e.KeyCode == Keys.V && e.Control)
            {
                gridPeptides.DoPaste(this, ValidatePeptideCellValues);
                RecalcRegression();
            }
            else if (e.KeyCode == Keys.Delete)
            {
                gridPeptides.DoDelete();
                RecalcRegression();
            }
        }

        private bool ValidatePeptideCellValues(string[] values)
        {
            int colError;
            return ValidatePeptideCellValues(values, out colError);
        }

        private bool ValidatePeptideCellValues(string[] values, out int colError)
        {
            string seq = values[0].Trim();
            if (!FastaSequence.IsSequence(seq))
            {
                string message = string.Format("The sequence {0} is not a valid peptide.", seq);
                MessageBox.Show(this, message, Program.Name);
                colError = 0;
                return false;
            }
/*            else
            {
                char cTerm = seq[seq.Length - 1];
                if (cTerm != 'R' && cTerm != 'K')
                {
                    string message = string.Format("The sequence {0} does not end with R or K.  " +
                                                   "The hydrophobicity calculator is only calibrated for tryptic cleavage.",
                                                   seq);
                    MessageBox.Show(message, Program.Name);
                    return false;
                }
            }
 */

            string rt = (values.Length < 2 ? "" : values[1].Trim());
            try
            {
                double.Parse(rt);
            }
            catch (FormatException)
            {
                string message = string.Format("The value {0} is not a valid retention time.", rt);
                MessageBox.Show(this, message, Program.Name);
                colError = 1;
                return false;
            }
            colError = -1;
            return true;
        }

        private void gridPeptides_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            RecalcRegression();
        }

        private void RecalcRegression()
        {
            RecalcRegression(null);
        }

        private void RecalcRegression(string nameCalc)
        {
            var peptidesTimes = new List<MeasuredRetentionTime>();
            foreach (DataGridViewRow row in gridPeptides.Rows)
            {
                if (row.IsNewRow)
                    continue;

                // Get all the values first, in case anything throws.
                object val = row.Cells[0].Value;
                if (val == null)
                    break;
                string sequence = val.ToString();

                val = row.Cells[1].Value;
                if (val == null)
                    break;

                double rt;
                try
                {
                    rt = double.Parse(val.ToString());
                }
                catch (FormatException)
                {
                    break;
                }

                peptidesTimes.Add(new MeasuredRetentionTime(sequence, rt));
            }

            RetentionTimeStatistics statistics;
            var regression = RetentionTimeRegression.CalcRegression("Recalc", nameCalc,
                peptidesTimes, out statistics);

            double r = 0.0;
            if (regression == null)
            {
                textSlope.Text = "";
                textIntercept.Text = "";
                textTimeWindow.Text = "";
                comboCalculator.SelectedIndex = -1;
            }
            else
            {
                textSlope.Text = string.Format("{0:F04}", regression.Conversion.Slope);
                textIntercept.Text = string.Format("{0:F04}", regression.Conversion.Intercept);
                textTimeWindow.Text = string.Format("{0:F01}", regression.TimeWindow);

                // Select best calculator match.
                comboCalculator.SelectedItem = regression.Calculator.Name;

                // Save statistics to show in RTDetails form.
                _statistics = statistics;
                r = statistics.R;
            }

            labelRValue.Text = string.Format("({0} peptides, R = {1:F02})",
                peptidesTimes.Count, r);
            // Right align with the peptide grid.
            labelRValue.Left = gridPeptides.Right - labelRValue.Width;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void labelRValue_Click(object sender, EventArgs e)
        {
            if (_statistics != null)
            {
                RTDetails dlg = new RTDetails(_statistics);
                dlg.ShowDialog(this);
            }
        }
    }
}
