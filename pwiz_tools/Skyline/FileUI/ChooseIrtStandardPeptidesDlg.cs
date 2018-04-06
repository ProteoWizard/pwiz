/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class ChooseIrtStandardPeptidesDlg : FormEx
    {
        public SrmDocument Document { get; private set; }
        private readonly string _documentFilePath;

        public string IrtFile { get; private set; }
        private List<MeasuredRetentionTime> _irtPeptides;
        private List<SpectrumMzInfo> _librarySpectra;
        private HashSet<string> _irtPeptideSequences;

        public ChooseIrtStandardPeptidesDlg(SrmDocument document, string documentFilePath, IEnumerable<PeptideGroupDocNode> peptideGroups)
        {
            InitializeComponent();

            Document = document;
            _documentFilePath = documentFilePath;

            _librarySpectra = new List<SpectrumMzInfo>();
            _irtPeptides = new List<MeasuredRetentionTime>();
            _irtPeptideSequences = null;

            PeptideGroupDocNode[] proteinsContainingCommonIrts, proteinsNotContainingCommonIrts;
            CreateIrtCalculatorDlg.SeparateProteinGroups(peptideGroups, out proteinsContainingCommonIrts, out proteinsNotContainingCommonIrts);
            foreach (var protein in proteinsContainingCommonIrts.Concat(proteinsNotContainingCommonIrts))
                comboProteins.Items.Add(new PeptideGroupItem(protein));
            
            if (proteinsContainingCommonIrts.Any())
                comboProteins.SelectedIndex = 0;

            UpdateSelection(this, null);
        }

        public void UpdateLists(List<SpectrumMzInfo> librarySpectra, List<DbIrtPeptide> dbIrtPeptides)
        {
            librarySpectra.AddRange(_librarySpectra);
            dbIrtPeptides.AddRange(_irtPeptides.Select(rt => new DbIrtPeptide(rt.PeptideSequence, rt.RetentionTime, true, TimeSource.scan)));
            if (_irtPeptideSequences != null)
                dbIrtPeptides.ForEach(pep => pep.Standard = _irtPeptideSequences.Contains(pep.PeptideModSeq));
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (radioProtein.Checked)
            {
                if (comboProteins.SelectedIndex == -1)
                {
                    MessageDlg.Show(this, Resources.ChooseIrtStandardPeptidesDlg_OkDialog_Please_select_a_protein_containing_the_list_of_standard_peptides_for_the_iRT_calculator_);
                    comboProteins.Focus();
                    return;
                }
                var peptideGroupItem = comboProteins.SelectedItem as PeptideGroupItem;
                if (peptideGroupItem != null)
                {
                    var protein = peptideGroupItem.PeptideGroup;
                    _irtPeptideSequences = new HashSet<string>();
                    foreach (var peptide in protein.Peptides)
                        _irtPeptideSequences.Add(peptide.ModifiedSequence);
                    if (Document.PeptideGroupCount > 0)
                    {
                        var pathFrom = Document.GetPathTo(Document.FindNodeIndex(protein.Id));
                        var pathTo = Document.GetPathTo(Document.FindNodeIndex(Document.PeptideGroups.First().Id));
                        if (!Equals(pathFrom, pathTo))
                        {
                            IdentityPath newLocation;
                            Document = Document.MoveNode(pathFrom, pathTo, out newLocation);
                        }
                    }
                }
            }
            else
            {
                if (!File.Exists(txtTransitionList.Text))
                {
                    MessageDlg.Show(this, Resources.ChooseIrtStandardPeptides_OkDialog_Transition_list_field_must_contain_a_path_to_a_valid_file_);
                    txtTransitionList.Focus();
                    return;
                }
                try
                {
                    IdentityPath selectPath;
                    List<TransitionImportErrorInfo> errorList;
                    var inputs = new MassListInputs(txtTransitionList.Text);
                    Document = Document.ImportMassList(inputs, new IdentityPath(Document.PeptideGroups.First().Id), out selectPath, out _irtPeptides, out _librarySpectra, out errorList);
                    if (errorList.Any())
                        throw new InvalidDataException(errorList[0].ErrorMessage);
                    IrtFile = txtTransitionList.Text;
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(this, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Error_reading_iRT_standards_transition_list___0_, x.Message), x);
                    return;
                }
            }
            DialogResult = DialogResult.OK;
        }

        private void UpdateSelection(object sender, EventArgs e)
        {
            var protein = radioProtein.Checked;
            comboProteins.Enabled = protein;
            txtTransitionList.Enabled = !protein;
            btnBrowseTransitionList.Enabled = !protein;
        }

        private void btnBrowseTransitionList_Click(object sender, EventArgs e)
        {
            ImportTextFile();
        }

        private void ImportTextFile()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = Resources.ChooseIrtStandardPeptides_ImportTextFile_Import_Transition_List__iRT_standards_,
                InitialDirectory = Path.GetDirectoryName(_documentFilePath),
                DefaultExt = TextUtil.EXT_CSV,
                Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(Resources.ChooseIrtStandardPeptides_ImportTextFile_Transition_List, TextUtil.EXT_CSV, TextUtil.EXT_TSV))
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);
                    SetTransitionListFile(dlg.FileName);
                }
            }
        }

        private void SetTransitionListFile(string filename)
        {
            txtTransitionList.Text = filename;
            txtTransitionList.Focus();
        }

        private class PeptideGroupItem
        {
            public PeptideGroupDocNode PeptideGroup { get; private set; }

            public PeptideGroupItem(PeptideGroupDocNode peptideGroup)
            {
                PeptideGroup = peptideGroup;
            }

            public override string ToString()
            {
                return PeptideGroup.Name;
            }
        }

        #region Functional test support
        public IEnumerable<string> ProteinNames { get { return from PeptideGroupItem protein in comboProteins.Items select protein.PeptideGroup.Name; } }

        public void OkDialogProtein(string proteinName)
        {
            for (var i = 0; i < comboProteins.Items.Count; i++)
            {
                if (((PeptideGroupItem) comboProteins.Items[i]).PeptideGroup.Name.Equals(proteinName))
                {
                    radioProtein.Checked = true;
                    comboProteins.SelectedIndex = i;
                    OkDialog();
                }
            }
        }

        public void OkDialogFile(string filename)
        {
            radioTransitionList.Checked = true;
            SetTransitionListFile(filename);
            OkDialog();
        }
        #endregion
    }
}
