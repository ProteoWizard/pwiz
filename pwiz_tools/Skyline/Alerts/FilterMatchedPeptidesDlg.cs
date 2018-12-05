/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public sealed partial class FilterMatchedPeptidesDlg : ModeUIInvariantFormEx,  // This dialog is inherently proteomic, never needs to be adapted for small mol or mixed UI mode
              IAuditLogModifier<FilterMatchedPeptidesDlg.FilterMatchedPeptidesSettings>
    {
        public FilterMatchedPeptidesDlg(int numWithDuplicates, int numUnmatched, int numFiltered, bool single, bool hasSmallMolecules)
        {
            InitializeComponent();

            HasSmallMolecules = hasSmallMolecules;
            if (HasSmallMolecules)
            {
                Text = Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg_Filter_Molecules;
                radioKeepFiltered.Text = Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg_Include_all_molecules;
            }

            UnmatchedCount = numUnmatched;
            DuplicateMatchesCount = numWithDuplicates;
            FilteredCount = numWithDuplicates;

            if(ViewLibraryPepMatching.FilterMultipleProteinMatches == BackgroundProteome.DuplicateProteinsFilter.AddToAll)
                radioAddToAll.Checked = true;
            else if (ViewLibraryPepMatching.FilterMultipleProteinMatches == BackgroundProteome.DuplicateProteinsFilter.FirstOccurence)
                radioFirstOccurence.Checked = true;
            else
                radioNoDuplicates.Checked = true;

            radioAddUnmatched.Checked = Settings.Default.LibraryPeptidesAddUnmatched;
            radioFilterUnmatched.Checked = !radioAddUnmatched.Checked;

            radioKeepFiltered.Checked = Settings.Default.LibraryPeptidesKeepFiltered;
            radioDoNotAddFiltered.Checked = !radioKeepFiltered.Checked;
            
            if (numWithDuplicates != 0)
            {
                msgDuplicatePeptides.Text = single
                                                ? Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg_This_peptide_matches_multiple_proteins
                                                : (numWithDuplicates == 1
                                                       ? Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg_1_peptide_matching_multiple_proteins
                                                       : string.Format(Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg__0__peptides_matching_multiple_proteins,
                                                           numWithDuplicates));
            }
            else
            {
                int delta = panelUnmatched.Top - panelMultiple.Top;
                panelMultiple.Hide();
                panelUnmatched.Top -= delta;
                panelFiltered.Top -= delta;
                Height -= delta;
            }
            if (numUnmatched != 0)
            {
                msgUnmatchedPeptides.Text = single
                                                ? Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg_This_peptide_does_not_have_a_matching_protein
                                                : (numUnmatched == 1
                                                       ? Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg_1_peptide_without_a_matching_protein
                                                       : string.Format(Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg__0__peptides_without_matching_proteins,
                                                           numUnmatched));
            }
            else
            {
                int delta = panelFiltered.Top - panelUnmatched.Top;
                panelUnmatched.Hide();
                panelFiltered.Top -= delta;
                Height -= delta;
            }
            if (numFiltered != 0)
            {
                var filterMatchedPeptidesDlgFilterMatchedPeptidesDlgThisPeptideDoesNotMatchTheCurrentFilterSettings = 
                    hasSmallMolecules
                    ? Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg_This_molecule_does_not_match_the_current_filter_settings
                    : Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg_This_peptide_does_not_match_the_current_filter_settings;
                var filterMatchedPeptidesDlgFilterMatchedPeptidesDlg1PeptideNotMatchingTheCurrentFilterSettings = 
                    hasSmallMolecules
                        ?  Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg_1_molecule_not_matching_the_current_filter_settings
                    : Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg_1_peptide_not_matching_the_current_filter_settings;
                var filterMatchedPeptidesDlgFilterMatchedPeptidesDlg0PeptidesNotMatchingTheCurrentFilterSettings = 
                    hasSmallMolecules
                        ? Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg__0__molecules_not_matching_the_current_filter_settings
                    : Resources.FilterMatchedPeptidesDlg_FilterMatchedPeptidesDlg__0__peptides_not_matching_the_current_filter_settings;
                msgFilteredPeptides.Text = single
                                               ? filterMatchedPeptidesDlgFilterMatchedPeptidesDlgThisPeptideDoesNotMatchTheCurrentFilterSettings
                                               : (numFiltered == 1
                                                      ? filterMatchedPeptidesDlgFilterMatchedPeptidesDlg1PeptideNotMatchingTheCurrentFilterSettings
                                                      : string.Format(filterMatchedPeptidesDlgFilterMatchedPeptidesDlg0PeptidesNotMatchingTheCurrentFilterSettings,
                                                          numFiltered));
            }
            else
            {
                int delta = panelFiltered.Bottom - panelFiltered.Top;
                panelFiltered.Hide();
                Height -= delta;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var duplicateProteinsFilter = BackgroundProteome.DuplicateProteinsFilter.AddToAll;
            if (radioNoDuplicates.Checked)
                duplicateProteinsFilter = BackgroundProteome.DuplicateProteinsFilter.NoDuplicates;
            else if (radioFirstOccurence.Checked)
                duplicateProteinsFilter = BackgroundProteome.DuplicateProteinsFilter.FirstOccurence;

            Settings.Default.LibraryPeptidesAddDuplicatesEnum = duplicateProteinsFilter.ToString();

            Settings.Default.LibraryPeptidesAddUnmatched = radioAddUnmatched.Checked;
            Settings.Default.LibraryPeptidesKeepFiltered = radioKeepFiltered.Checked;
            
            DialogResult = DialogResult.OK;
        }

        // For testing.
        public BackgroundProteome.DuplicateProteinsFilter DuplicateProteinsFilter
        {
            get
            {
                if (radioNoDuplicates.Checked)
                    return BackgroundProteome.DuplicateProteinsFilter.NoDuplicates;
                if (radioFirstOccurence.Checked)
                    return BackgroundProteome.DuplicateProteinsFilter.FirstOccurence;
                return BackgroundProteome.DuplicateProteinsFilter.AddToAll;
            }
            set
            {
                if (Equals(value, BackgroundProteome.DuplicateProteinsFilter.NoDuplicates))
                    radioNoDuplicates.Checked = true;
                else if (Equals(value, BackgroundProteome.DuplicateProteinsFilter.FirstOccurence))
                    radioFirstOccurence.Checked = true;
                else
                    radioAddToAll.Checked = true;
            }
        }

        public bool AddUnmatched
        {
            get
            {
                return radioAddUnmatched.Checked;
            }
            set 
            { 
                radioAddUnmatched.Checked = value;
                radioFilterUnmatched.Checked = !radioAddUnmatched.Checked;
            }
        }

        public bool AddFiltered
        {
            get { return radioKeepFiltered.Checked = true; }
            set 
            { 
                radioKeepFiltered.Checked = value;
                radioDoNotAddFiltered.Checked = !radioKeepFiltered.Checked;
            }
        }

        public int DuplicateMatchesCount { get; set; }

        public int UnmatchedCount { get; set; }
        
        public int FilteredCount { get; set; }

        public bool HasSmallMolecules { get; set; }

        public class FilterMatchedPeptidesSettings : AuditLogOperationSettings<FilterMatchedPeptidesSettings>, IAuditLogComparable
        {
            public FilterMatchedPeptidesSettings(bool noDuplicates, bool firstOccurence, bool addToAll,
                bool filterUnmatched, bool addUnmatched, bool doNotAddFiltered, bool keepFiltered)
            {
                NoDuplicates = noDuplicates;
                FirstOccurence = firstOccurence;
                AddToAll = addToAll;
                FilterUnmatched = filterUnmatched;
                AddUnmatched = addUnmatched;
                DoNotAddFiltered = doNotAddFiltered;
                KeepFiltered = keepFiltered;
            }

            [Track]
            public bool NoDuplicates { get; private set; }
            [Track]
            public bool FirstOccurence { get; private set; }
            [Track]
            public bool AddToAll { get; private set; }

            [Track]
            public bool FilterUnmatched { get; private set; }
            [Track]
            public bool AddUnmatched { get; private set; }

            [Track]
            public bool DoNotAddFiltered { get; private set; }
            [Track]
            public bool KeepFiltered { get; private set; }

            public object GetDefaultObject(ObjectInfo<object> info)
            {
                return new FilterMatchedPeptidesSettings(false, false, false, false, false, false, false);
            }
        }

        public FilterMatchedPeptidesSettings FormSettings
        {
            get
            {
                return new FilterMatchedPeptidesSettings(radioNoDuplicates.Checked, radioFirstOccurence.Checked,
                    radioAddToAll.Checked, radioFilterUnmatched.Checked, radioAddUnmatched.Checked,
                    radioDoNotAddFiltered.Checked, radioKeepFiltered.Checked);
            }
        }
    }
}
