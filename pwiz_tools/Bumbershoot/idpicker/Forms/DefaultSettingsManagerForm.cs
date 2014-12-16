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
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace IDPicker.Forms
{
    public partial class DefaultSettingsManagerForm : Form
    {
        public DefaultSettingsManagerForm()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            var settings = Properties.Settings.Default;

            int presetIndex = maxQValueComboBox.Items.IndexOf((settings.DefaultMaxFDR * 100).ToString());
            maxQValueComboBox.SelectedIndex = presetIndex;
            if (presetIndex == -1)
                maxQValueComboBox.Text = (settings.DefaultMaxFDR * 100).ToString();
            
            presetIndex = maxImportFdrComboBox.Items.IndexOf((settings.DefaultMaxImportFDR * 100).ToString());
            maxImportFdrComboBox.SelectedIndex = presetIndex;
            if (presetIndex == -1)
                maxImportFdrComboBox.Text = (settings.DefaultMaxImportFDR * 100).ToString();

            minSpectraPerMatchTextBox.Text = settings.DefaultMinSpectraPerDistinctMatch.ToString();
            minSpectraPerPeptideTextBox.Text = settings.DefaultMinSpectraPerDistinctPeptide.ToString();
            maxProteinGroupsTextBox.Text = settings.DefaultMaxProteinGroupsPerPeptide.ToString();
            minDistinctPeptidesTextBox.Text = settings.DefaultMinDistinctPeptides.ToString();
            minSpectraTextBox.Text = settings.DefaultMinSpectra.ToString();
            minAdditionalPeptidesTextBox.Text = settings.DefaultMinAdditionalPeptides.ToString();
            defaultDecoyPrefixTextBox.Text = settings.DefaultDecoyPrefix;
            maxImportRankTextBox.Text = settings.DefaultMaxRank.ToString();
            ignoreUnmappedPeptidesCheckBox.Checked = settings.DefaultIgnoreUnmappedPeptides;

            filterByGeneCheckBox.Checked = settings.DefaultGeneLevelFiltering;
            chargeIsDistinctCheckBox.Checked = settings.DefaultChargeIsDistinct;
            analysisIsDistinctCheckBox.Checked = settings.DefaultAnalysisIsDistinct;
            modificationsAreDistinctCheckbox.Checked = settings.DefaultModificationsAreDistinct;
            modificationRoundToMassTextBox.Text = settings.DefaultModificationRoundToNearest.ToString();

            lbFastaPaths.Items.AddRange(settings.FastaPaths.Cast<object>().ToArray());
            lbSourcePaths.Items.AddRange(settings.SourcePaths.Cast<object>().ToArray());

            sourceExtensionsTextBox.Text = settings.SourceExtensions;

            nonFixedDriveWarningCheckBox.Checked = Properties.GUI.Settings.Default.WarnAboutNonFixedDrive;
            embedGeneMetadataWarningCheckBox.Checked = Properties.GUI.Settings.Default.WarnAboutNoGeneMetadata;

            base.OnLoad(e);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            var settings = Properties.Settings.Default;

            settings.DefaultMaxFDR = Convert.ToDouble(maxQValueComboBox.Text) / 100;
            settings.DefaultMaxImportFDR = Convert.ToDouble(maxImportFdrComboBox.Text) / 100;
            settings.DefaultMinSpectraPerDistinctMatch = Convert.ToInt32(minSpectraPerMatchTextBox.Text);
            settings.DefaultMinSpectraPerDistinctPeptide = Convert.ToInt32(minSpectraPerPeptideTextBox.Text);
            settings.DefaultMaxProteinGroupsPerPeptide = Convert.ToInt32(maxProteinGroupsTextBox.Text);
            settings.DefaultMinDistinctPeptides = Convert.ToInt32(minDistinctPeptidesTextBox.Text);
            settings.DefaultMinSpectra = Convert.ToInt32(minSpectraTextBox.Text);
            settings.DefaultMinAdditionalPeptides = Convert.ToInt32(minAdditionalPeptidesTextBox.Text);
            settings.DefaultDecoyPrefix = defaultDecoyPrefixTextBox.Text;
            settings.DefaultMaxRank = Convert.ToInt32(maxImportRankTextBox.Text);
            settings.DefaultIgnoreUnmappedPeptides = ignoreUnmappedPeptidesCheckBox.Checked;

            settings.DefaultGeneLevelFiltering = filterByGeneCheckBox.Checked;
            settings.DefaultChargeIsDistinct = chargeIsDistinctCheckBox.Checked;
            settings.DefaultAnalysisIsDistinct = analysisIsDistinctCheckBox.Checked;
            settings.DefaultModificationsAreDistinct = modificationsAreDistinctCheckbox.Checked;
            settings.DefaultModificationRoundToNearest = Convert.ToDecimal(modificationRoundToMassTextBox.Text);

            settings.FastaPaths.Clear(); settings.FastaPaths.AddRange(lbFastaPaths.Items.OfType<string>().ToArray());
            settings.SourcePaths.Clear(); settings.SourcePaths.AddRange(lbSourcePaths.Items.OfType<string>().ToArray());

            settings.SourceExtensions = sourceExtensionsTextBox.Text;

            Properties.GUI.Settings.Default.WarnAboutNonFixedDrive = nonFixedDriveWarningCheckBox.Checked;
            Properties.GUI.Settings.Default.WarnAboutNoGeneMetadata = embedGeneMetadataWarningCheckBox.Checked;
            Properties.GUI.Settings.Default.Save();

            settings.Save();
        }

        private ListBox ActiveListBox
        {
            get { return searchPathsTabControl.SelectedTab == tabFastaFilepaths ? lbFastaPaths : lbSourcePaths; }
        }

        /// <summary>Only allow selection in one ListBox at a time since the buttons apply to all of them.
        private void lbSearchPaths_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                lbFastaPaths.ClearSelected();
                lbSourcePaths.ClearSelected();
            }
            catch (Exception ex)
            {
                Program.HandleException(ex);
            }
        }

        /// <summary>Clear all items from currently visible ListBox.</summary>
        private void btnClear_Click(object sender, EventArgs e)
        {
            try
            {
                ListBox lbSearchPaths = ActiveListBox;

                if (lbSearchPaths.Items.Count > 0)
                {
                    if (DialogResult.Yes == MessageBox.Show(this, "Are you sure you wish to remove all current paths from this list?", "Options", MessageBoxButtons.YesNo, MessageBoxIcon.Information))
                    {
                        lbSearchPaths.Items.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                Program.HandleException(ex);
            }
        }

        /// <summary>Open browse dialog, add selected directory to the currently visible ListBox.</summary>
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            var fbd = new FolderBrowserDialog
            {
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
                ShowNewFolderButton = false,
                Description = ActiveListBox.Parent.Controls.OfType<Label>().First().Text
            };

            if (fbd.ShowDialog(this) == DialogResult.Cancel)
                return;

            if (!ActiveListBox.Items.Contains(fbd.SelectedPath))
                ActiveListBox.Items.Add(fbd.SelectedPath);
        }

        /// <summary>Add a relative path with macro support to the currently visible ListBox.</summary>
        private void btnAddRelative_Click(object sender, EventArgs e)
        {
            var dialog = new AddPathDialog();
            if (dialog.ShowDialog(this) == DialogResult.Cancel)
                return;

            if (!ActiveListBox.Items.Contains(dialog.Path))
                ActiveListBox.Items.Add(dialog.Path);
        }

        /// <summary>Remove selected path from the currently visible ListBox.</summary>
        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (ActiveListBox.SelectedIndices.Count > 0)
                ActiveListBox.Items.RemoveAt(ActiveListBox.SelectedIndex);
        }

        void doubleTextBox_KeyDown(object sender, KeyEventArgs e)
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

        void integerTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!(e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 ||
                e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 ||
                e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back ||
                e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
                e.SuppressKeyPress = true;
        }

        private void qonverterSettingsButton_Click(object sender, EventArgs e)
        {
            var form = new QonverterSettingsManagerForm { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(this);
        }
    }
}
