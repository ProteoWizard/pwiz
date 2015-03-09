/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditIsotopeEnrichmentDlg : FormEx
    {
        private const int COL_SYMBOL = 1;
        private const int COL_ENRICHMENT = 2;

        private static readonly IList<KeyValuePair<string, string>> LIST_NAME_SYMBOL =
            new[]
                {
                    new KeyValuePair<string, string>("2H", BioMassCalc.H2), // Not L10N
                    new KeyValuePair<string, string>("13C", BioMassCalc.C13), // Not L10N
                    new KeyValuePair<string, string>("15N", BioMassCalc.N15), // Not L10N
                    new KeyValuePair<string, string>("17O", BioMassCalc.O17), // Not L10N
                    new KeyValuePair<string, string>("18O", BioMassCalc.O18), // Not L10N
                };

        private IsotopeEnrichments _enrichments;
        private readonly IEnumerable<IsotopeEnrichments> _existing;

        public EditIsotopeEnrichmentDlg(IEnumerable<IsotopeEnrichments> existing)
        {
            _existing = existing;

            InitializeComponent();

            foreach (var nameSymbol in LIST_NAME_SYMBOL)
                gridEnrichments.Rows.Add(nameSymbol.Key, nameSymbol.Value);
        }

        public string EnrichmentsName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public IsotopeEnrichments Enrichments
        {
            get { return _enrichments; }

            set
            {
                _enrichments = value;

                textName.Text = _enrichments != null ? _enrichments.Name : string.Empty;
                foreach (DataGridViewRow row in gridEnrichments.Rows)
                {
                    double? enrichmentValue = null;
                    if (_enrichments != null)
                    {
                        string isotopeSymbol = (string) row.Cells[COL_SYMBOL].Value;
                        int iEnrichment = _enrichments.Enrichments.IndexOf(e =>
                            Equals(e.IsotopeSymbol, isotopeSymbol));
                        enrichmentValue = iEnrichment != -1
                            ? _enrichments.Enrichments[iEnrichment].AtomPercentEnrichment
                            : BioMassCalc.GetIsotopeEnrichmentDefault(isotopeSymbol);
                    }
                    row.Cells[COL_ENRICHMENT].Value = enrichmentValue.HasValue
                        ? (enrichmentValue.Value * 100).ToString(LocalizationHelper.CurrentCulture)
                        : string.Empty;
                }
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            if (_existing.Contains(en => !ReferenceEquals(_enrichments, en) && Equals(name, en.Name)))
            {
                helper.ShowTextBoxError(textName, Resources.EditIsotopeEnrichmentDlg_OkDialog_The_isotope_enrichments_named__0__already_exist, name);
                return;
            }

            var listEnrichments = new List<IsotopeEnrichmentItem>();
            foreach (DataGridViewRow row in gridEnrichments.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow))
            {
                double enrichment;
                if (!ValidateEnrichment(row.Cells[COL_ENRICHMENT], out enrichment))
                    return;

                listEnrichments.Add(new IsotopeEnrichmentItem(row.Cells[COL_SYMBOL].Value.ToString(), enrichment/100));
            }

            _enrichments = new IsotopeEnrichments(name, listEnrichments);

            DialogResult = DialogResult.OK;
        }

        private bool ValidateEnrichment(DataGridViewCell cell, out double enrichment)
        {
            if (!ValidateCell(cell, Convert.ToDouble, out enrichment))
                return false;

            const double min = IsotopeEnrichmentItem.MIN_ATOM_PERCENT_ENRICHMENT*100;
            const double max = IsotopeEnrichmentItem.MAX_ATOM_PERCENT_ENRICHMENT*100;
            if (min > enrichment || enrichment > max)
            {
                InvalidCell(cell, Resources.EditIsotopeEnrichmentDlg_ValidateEnrichment_The_enrichment__0__must_be_between__1__and__2__, min, max);
                return false;
            }

            return true;
        }

        private bool ValidateCell<TVal>(DataGridViewCell cell,
            Converter<string, TVal> conv, out TVal valueT)
        {
            valueT = default(TVal);
            string value = cell.Value.ToString();
            try
            {
                valueT = conv(value);
            }
            catch (Exception)
            {
                InvalidCell(cell, Resources.EditIsotopeEnrichmentDlg_ValidateCell_The_entry__0__is_not_valid, value);
                return false;
            }

            return true;            
        }

        private void InvalidCell(DataGridViewCell cell,
            string message, params object[] args)
        {            
            MessageBox.Show(string.Format(message, args));
            gridEnrichments.Focus();
            gridEnrichments.ClearSelection();
            cell.Selected = true;
            gridEnrichments.CurrentCell = cell;
            gridEnrichments.BeginEdit(true);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}
