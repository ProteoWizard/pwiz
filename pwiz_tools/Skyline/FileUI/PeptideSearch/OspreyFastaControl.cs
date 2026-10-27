/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    /// <summary>
    /// The FASTA page of Build Library's fine-tune: the protein FASTA the tuning runs are searched
    /// against, and how it is digested (the enzyme and the missed cleavages Osprey predicts the
    /// training library with). The Osprey Search wizard shows the fuller Import FASTA control in
    /// its place, since its FASTA goes on to the Import Peptide Search wizard; this one is the
    /// three settings that page needs, at a size Build Library has room for.
    /// </summary>
    public partial class OspreyFastaControl : UserControl
    {
        private const int MAX_MISSED_CLEAVAGES = 9;

        private readonly SettingsListComboDriver<Enzyme> _driverEnzyme;

        public OspreyFastaControl()
        {
            InitializeComponent();

            comboMissedCleavages.Items.AddRange(Enumerable.Range(0, MAX_MISSED_CLEAVAGES + 1)
                .Select(i => (object)i.ToString(LocalizationHelper.CurrentCulture)).ToArray());
            _driverEnzyme = new SettingsListComboDriver<Enzyme>(comboEnzyme, Settings.Default.EnzymeList);
        }

        private Form WizardForm => FormEx.GetParentForm(this);

        /// <summary>The document, whose folder the Browse dialog opens in when no FASTA folder is remembered.</summary>
        public string DocumentFilePath { get; set; }

        /// <summary>Starts from the document's digest settings, as the Import FASTA page does.</summary>
        public void Initialize(PeptideSettings peptideSettings)
        {
            _driverEnzyme.LoadList(peptideSettings.Enzyme.GetKey());
            MaxMissedCleavages = peptideSettings.DigestSettings.MaxMissedCleavages;
        }

        public string FastaFile
        {
            get => textFasta.Text;
            set => textFasta.Text = value;
        }

        public Enzyme Enzyme
        {
            get => Settings.Default.GetEnzymeByName(comboEnzyme.SelectedItem?.ToString());
            set
            {
                if (value == null)
                    return;
                for (int i = 0; i < comboEnzyme.Items.Count; i++)
                {
                    if (comboEnzyme.Items[i].ToString() == value.Name)
                    {
                        comboEnzyme.SelectedIndex = i;
                        return;
                    }
                }
                // Not listed: reload the combo from the enzyme list, which may have gained it
                _driverEnzyme.LoadList(value.GetKey());
            }
        }

        public int MaxMissedCleavages
        {
            get => int.Parse(comboMissedCleavages.SelectedItem.ToString());
            set
            {
                comboMissedCleavages.SelectedItem = value.ToString(LocalizationHelper.CurrentCulture);
                if (comboMissedCleavages.SelectedIndex < 0)
                    comboMissedCleavages.SelectedIndex = 0;
            }
        }

        private void comboEnzyme_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverEnzyme.SelectedIndexChangedEvent(sender, e);
        }

        private void btnBrowseFasta_Click(object sender, EventArgs e)
        {
            BrowseFasta();
        }

        /// <summary>Browse... chooses the FASTA file, starting in the folder the last one came from.</summary>
        public void BrowseFasta()
        {
            string initialDir = Settings.Default.FastaDirectory;
            if (string.IsNullOrEmpty(initialDir) && !string.IsNullOrEmpty(DocumentFilePath))
                initialDir = Path.GetDirectoryName(DocumentFilePath);
            using var dlg = new OpenFileDialog();
            dlg.Title = PeptideSearchResources.ImportFastaControl_browseFastaBtn_Click_Open_FASTA;
            dlg.InitialDirectory = initialDir;
            dlg.CheckPathExists = true;
            dlg.Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(PeptideSearchResources.OspreyFastaControl_BrowseFasta_FASTA_files, @".fasta", @".fa", @".faa"));
            if (dlg.ShowDialog(WizardForm) != DialogResult.OK)
                return;
            Settings.Default.FastaDirectory = Path.GetDirectoryName(dlg.FileName);
            FastaFile = dlg.FileName;
        }

        /// <summary>
        /// Next from the page: the FASTA must exist, and the enzyme must be one Carafe's table
        /// digests like, since Osprey predicts the training library with it.
        /// </summary>
        public bool ValidatePage()
        {
            if (!File.Exists(FastaFile))
            {
                MessageDlg.Show(WizardForm, PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_A_FASTA_file_is_required_for_an_Osprey_search_);
                textFasta.Focus();
                return false;
            }
            if (!OspreyHelpers.GetCarafeEnzymeIndex(Enzyme).HasValue)
            {
                MessageDlg.Show(WizardForm, string.Format(
                    PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_Osprey_cannot_predict_a_library_for_the_enzyme__0___Choose_an_enzyme_with_a_standard_cleavage_rule_,
                    Enzyme?.Name));
                comboEnzyme.Focus();
                return false;
            }
            return true;
        }
    }
}
