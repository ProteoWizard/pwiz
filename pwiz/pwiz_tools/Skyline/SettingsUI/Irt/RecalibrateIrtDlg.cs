/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class RecalibrateIrtDlg : FormEx
    {
        private readonly IEnumerable<DbIrtPeptide> _irtPeptides;

        public RecalibrateIrtDlg(IList<DbIrtPeptide> irtPeptides)
        {
            _irtPeptides = irtPeptides;
            
            InitializeComponent();

            var standardPeptides = irtPeptides.Where(peptide => peptide.Standard)
                                              .OrderBy(peptide => peptide.Irt)
                                              .ToArray();
            // Look for standard peptides with whole number values as the suggested fixed points
            int iFixed1 = standardPeptides.IndexOf(peptide => Math.Round(peptide.Irt, 8) == Math.Round(peptide.Irt));
            int iFixed2 = standardPeptides.LastIndexOf(peptide => Math.Round(peptide.Irt, 8) == Math.Round(peptide.Irt));
            if (iFixed1 == -1 || iFixed2 == -1)
            {
                iFixed1 = 0;
                iFixed2 = standardPeptides.Length - 1;
            }
            else if (iFixed1 == iFixed2)
            {
                if (iFixed1 < standardPeptides.Length / 2)
                    iFixed2 = standardPeptides.Length - 1;
                else
                    iFixed1 = 0;
            }
            comboFixedPoint1.Items.AddRange(standardPeptides.Cast<object>().ToArray());
            comboFixedPoint1.SelectedIndex = iFixed1;
            comboFixedPoint2.Items.AddRange(standardPeptides.Cast<object>().ToArray());
            comboFixedPoint2.SelectedIndex = iFixed2;
        }

        public RegressionLine LinearEquation { get; private set; }

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

            var peptide1 = (DbIrtPeptide) comboFixedPoint1.SelectedItem;
            var peptide2 = (DbIrtPeptide) comboFixedPoint2.SelectedItem;

            double minCurrent = Math.Min(peptide1.Irt, peptide2.Irt);
            double maxCurrent = Math.Max(peptide1.Irt, peptide2.Irt);

            var statX = new Statistics(minCurrent, maxCurrent);
            var statY = new Statistics(minIrt, maxIrt);

            LinearEquation = new RegressionLine(statY.Slope(statX), statY.Intercept(statX));

            // Convert all of the peptides to the new scale.
            foreach (var peptide in _irtPeptides)
            {
                peptide.Irt = LinearEquation.GetY(peptide.Irt);
            }

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void comboFixedPoint1_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateMinMax();
        }

        private void comboFixedPoint2_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateMinMax();
        }

        private void UpdateMinMax()
        {
            double irt1 = comboFixedPoint1.SelectedItem != null
                              ? Math.Round(((DbIrtPeptide) comboFixedPoint1.SelectedItem).Irt, 2)
                              : 0;
            double irt2 = comboFixedPoint2.SelectedItem != null
                              ? Math.Round(((DbIrtPeptide) comboFixedPoint2.SelectedItem).Irt, 2)
                              : 100;
            textMinIrt.Text = Math.Min(irt1, irt2).ToString(CultureInfo.CurrentCulture);
            textMaxIrt.Text = Math.Max(irt1, irt2).ToString(CultureInfo.CurrentCulture);
        }
    }
}