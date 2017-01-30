//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDPicker.DataModel;

namespace IDPicker.Controls
{
    public partial class BasicFilterControl : UserControl
    {
        /// <summary>
        /// Occurs when the user changes the value of a filter control.
        /// </summary>
        public event EventHandler BasicFilterChanged;

        public event EventHandler ApplyFilterChanges;

        public event EventHandler ShowQonverterSettings;

        public event EventHandler CropAssembly;

        /// <summary>
        /// Gets a basic DataFilter from the filter controls or sets the filter controls from a DataFilter.
        /// </summary>
        public DataFilter DataFilter
        {
            get
            {
                double maxQValue;
                if (!Double.TryParse(maxQValueComboBox.Text, out maxQValue))
                    maxQValue = 0;

                pwiz.CLI.chemistry.MZTolerance precursorMzTolerance = null;
                if (!precursorMzToleranceTextBox.Text.IsNullOrEmpty())
                    precursorMzTolerance = new pwiz.CLI.chemistry.MZTolerance(Convert.ToDouble(precursorMzToleranceTextBox.Text),
                                                                              precursorMzToleranceUnitsComboBox.SelectedIndex == 0 ? pwiz.CLI.chemistry.MZTolerance.Units.MZ : pwiz.CLI.chemistry.MZTolerance.Units.PPM);

                return new DataFilter()
                {
                    MaximumQValue = maxQValue / 100,
                    MinimumDistinctPeptides = minDistinctPeptidesTextBox.Text.Length == 0 ? 0 : Convert.ToInt32(minDistinctPeptidesTextBox.Text),
                    //MinDistinctMatchesPerProtein
                    MinimumAdditionalPeptides = minAdditionalPeptidesTextBox.Text.Length == 0 ? 0 : Convert.ToInt32(minAdditionalPeptidesTextBox.Text),
                    MinimumSpectra = minSpectraTextBox.Text.Length == 0 ? 0 : Convert.ToInt32(minSpectraTextBox.Text),
                    GeneLevelFiltering = filterByGeneCheckBox.Checked,
                    PrecursorMzTolerance = precursorMzTolerance,
                    DistinctMatchFormat = new DistinctMatchFormat
                    {
                        IsChargeDistinct = chargeIsDistinctCheckBox.Checked,
                        IsAnalysisDistinct = analysisIsDistinctCheckBox.Checked,
                        AreModificationsDistinct = modificationsAreDistinctCheckbox.Checked,
                        ModificationMassRoundToNearest = modificationRoundToMassTextBox.Text.Length == 0 ? 1.0m : Convert.ToDecimal(modificationRoundToMassTextBox.Text)
                    },
                    MinimumSpectraPerDistinctPeptide = minSpectraPerPeptideTextBox.Text.Length == 0 ? 0 : Convert.ToInt32(minSpectraPerPeptideTextBox.Text),
                    MinimumSpectraPerDistinctMatch = minSpectraPerMatchTextBox.Text.Length == 0 ? 0 : Convert.ToInt32(minSpectraPerMatchTextBox.Text),
                    MaximumProteinGroupsPerPeptide = maxProteinGroupsTextBox.Text.Length == 0 ? 0 : Convert.ToInt32(maxProteinGroupsTextBox.Text),
                };
            }

            set
            {
                settingDataFilter = true;
                int presetIndex = maxQValueComboBox.Items.IndexOf((value.MaximumQValue * 100).ToString());
                maxQValueComboBox.SelectedIndex = presetIndex;
                if (presetIndex == -1)
                    maxQValueComboBox.Text = (value.MaximumQValue * 100).ToString();
                minDistinctPeptidesTextBox.Text = value.MinimumDistinctPeptides.ToString();
                //minDistinctMatches
                minAdditionalPeptidesTextBox.Text = value.MinimumAdditionalPeptides.ToString();
                filterByGeneCheckBox.Checked = value.GeneLevelFiltering;
                if (value.PrecursorMzTolerance != null)
                {
                    precursorMzToleranceTextBox.Text = value.PrecursorMzTolerance.value.ToString();
                    precursorMzToleranceUnitsComboBox.SelectedIndex = value.PrecursorMzTolerance.units == pwiz.CLI.chemistry.MZTolerance.Units.MZ ? 0 : 1;
                }
                else
                    precursorMzToleranceUnitsComboBox.SelectedIndex = 0;
                chargeIsDistinctCheckBox.Checked = value.DistinctMatchFormat.IsChargeDistinct;
                analysisIsDistinctCheckBox.Checked = value.DistinctMatchFormat.IsAnalysisDistinct;
                modificationsAreDistinctCheckbox.Checked = value.DistinctMatchFormat.AreModificationsDistinct;
                modificationRoundToMassTextBox.Text = value.DistinctMatchFormat.ModificationMassRoundToNearest.ToString();
                minSpectraTextBox.Text = value.MinimumSpectra.ToString();
                minSpectraPerPeptideTextBox.Text = value.MinimumSpectraPerDistinctPeptide.ToString();
                minSpectraPerMatchTextBox.Text = value.MinimumSpectraPerDistinctMatch.ToString();
                maxProteinGroupsTextBox.Text = value.MaximumProteinGroupsPerPeptide.ToString();
                settingDataFilter = false;
            }
        }

        bool settingDataFilter = false;

        public BasicFilterControl ()
        {
            InitializeComponent();
        }

        void doubleTextBox_KeyDown (object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Decimal || e.KeyCode == Keys.OemPeriod)
            {
                if ((sender as Control).Text.Length == 0 || (sender as Control).Text.Contains('.'))
                    e.SuppressKeyPress = true;
            }
            else if (!(e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 ||
                    e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 ||
                    e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back ||
                    e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
                e.SuppressKeyPress = true;
        }

        void integerTextBox_KeyDown (object sender, KeyEventArgs e)
        {
            if (!(e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 ||
                e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 ||
                e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back ||
                e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
                e.SuppressKeyPress = true;
        }

        void filterControl_CheckedChanged(object sender, EventArgs e)
        {
            if (!settingDataFilter && BasicFilterChanged != null)
                BasicFilterChanged(this, EventArgs.Empty);
        }

        void filterControl_TextChanged (object sender, EventArgs e)
        {
            if (!settingDataFilter && !String.IsNullOrEmpty((sender as Control).Text) && BasicFilterChanged != null)
                BasicFilterChanged(this, EventArgs.Empty);
        }

        void filterControl_TextChangedOrEmpty(object sender, EventArgs e)
        {
            if (!settingDataFilter && BasicFilterChanged != null)
                BasicFilterChanged(this, EventArgs.Empty);
        }

        void filterControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!settingDataFilter && BasicFilterChanged != null)
                BasicFilterChanged(this, EventArgs.Empty);
        }

        private void CloseLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (!settingDataFilter && ApplyFilterChanges != null)
                ApplyFilterChanges(this, EventArgs.Empty);
        }

        private void QonverterLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (ShowQonverterSettings != null)
                ShowQonverterSettings(null, null);
        }

        private void CropAssemblyLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (!settingDataFilter && CropAssembly != null)
                CropAssembly(this, EventArgs.Empty);
        }
    }
}
