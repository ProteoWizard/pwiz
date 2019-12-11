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
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class ChooseIrtStandardPeptidesDlg : FormEx
    {
        public SrmDocument Document { get; private set; }
        private readonly string _documentFilePath;
        private readonly ICollection<DbIrtPeptide> _dbIrtPeptides;

        public string IrtFile { get; private set; }
        private List<MeasuredRetentionTime> _irtAdd;
        private List<SpectrumMzInfo> _librarySpectra;
        private TargetMap<bool> _irtTargets;

        public ChooseIrtStandardPeptidesDlg(SrmDocument document, string documentFilePath, ICollection<DbIrtPeptide> dbIrtPeptides, IEnumerable<PeptideGroupDocNode> peptideGroups)
        {
            InitializeComponent();

            Document = document;
            _documentFilePath = documentFilePath;
            _dbIrtPeptides = dbIrtPeptides;

            _librarySpectra = new List<SpectrumMzInfo>();
            _irtAdd = new List<MeasuredRetentionTime>();
            _irtTargets = null;

            comboExisting.Items.AddRange(IrtStandard.ALL.Where(standard => !standard.Name.Equals(IrtStandard.EMPTY.Name)).Cast<object>().ToArray());
            comboExisting.SelectedIndex = 0;

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
            dbIrtPeptides.AddRange(_irtAdd.Select(rt => new DbIrtPeptide(rt.PeptideSequence, rt.RetentionTime, true, TimeSource.scan)));
            if (_irtTargets != null)
                dbIrtPeptides.ForEach(pep => pep.Standard = _irtTargets.ContainsKey(pep.ModifiedTarget));
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if ((radioExisting.Checked && ProcessStandard()) ||
                (radioProtein.Checked && ProcessProtein()) ||
                (radioTransitionList.Checked && ProcessTransitionList()))
            {
                DialogResult = DialogResult.OK;
            }
        }

        private bool ProcessStandard()
        {
            var standard = (IrtStandard)comboExisting.SelectedItem;

            if (ReferenceEquals(standard, IrtStandard.CIRT_SHORT))
            {
                var knownIrts = new TargetMap<double>(IrtStandard.CIRT.Peptides.Select(pep => new KeyValuePair<Target, double>(pep.ModifiedTarget, pep.Irt)));
                var cirtPeptides = _dbIrtPeptides.Where(pep => knownIrts.ContainsKey(pep.ModifiedTarget)).ToArray();
                var importCirts = new TargetMap<List<double>>(cirtPeptides.Select(pep =>
                    new KeyValuePair<Target, List<double>>(pep.ModifiedTarget, new List<double>())));
                var numCirts = 0;
                foreach (var pep in cirtPeptides)
                {
                    var list = importCirts[pep.ModifiedTarget];
                    if (list.Count == 0)
                        numCirts++;
                    list.Add(pep.Irt);
                }
                var listX = new List<double>();
                var listY = new List<double>();
                var targets = new Dictionary<int, Target>();
                foreach (var (i, kvp) in importCirts.Where(kvp => kvp.Value.Count > 0).Select((kvp, i) => Tuple.Create(i, kvp)))
                {
                    targets[i] = kvp.Key;
                    listX.Add(new Statistics(kvp.Value).Median());
                    listY.Add(knownIrts[kvp.Key]);
                }
                const int minPoints = RCalcIrt.MIN_PEPTIDES_COUNT;
                if (RCalcIrt.TryGetRegressionLine(listX, listY, minPoints, out var regression, out var removed))
                {
                    var outliers = new HashSet<int>();
                    for (var j = 0; j < listX.Count; j++)
                    {
                        if (removed.Contains(Tuple.Create(listX[j], listY[j])))
                            outliers.Add(j);
                    }
                    var graphData = new RegressionGraphData
                    {
                        Title = Resources.ChooseIrtStandardPeptidesDlg_OkDialog_Linear_regression,
                        LabelX = Resources.ChooseIrtStandardPeptidesDlg_OkDialog_Library_iRTs,
                        LabelY = Resources.ChooseIrtStandardPeptidesDlg_OkDialog_Known_iRTs,
                        XValues = listX.ToArray(),
                        YValues = listY.ToArray(),
                        Tooltips = targets.ToDictionary(target => target.Key, target => target.Value.ToString()),
                        OutlierIndices = outliers,
                        RegressionLine = regression,
                        MinR = RCalcIrt.MIN_IRT_TO_TIME_CORRELATION,
                        MinPoints = minPoints,
                    };
                    var outlierTargets = outliers.Select(idx => targets[idx]).ToHashSet();
                    numCirts -= outlierTargets.Count;
                    using (var dlg = new AddIrtStandardsDlg(numCirts,
                        string.Format(Resources.LibraryBuildNotificationHandler_AddIrts__0__distinct_CiRT_peptides_were_found__How_many_would_you_like_to_use_as_iRT_standards_,
                            numCirts), graphData))
                    {
                        if (dlg.ShowDialog(this) != DialogResult.OK)
                            return false;

                        SetStandards(IrtPeptidePicker.Pick(dlg.StandardCount, cirtPeptides.Where(pep => !outlierTargets.Contains(pep.ModifiedTarget)).ToArray()));
                    }
                    return true;
                }
            }

            if (standard.HasDocument && !standard.InDocument(Document))
            {
                _irtAdd.AddRange(standard.Peptides.Select(pep => new MeasuredRetentionTime(pep.ModifiedTarget, pep.Irt, true, true)));
                Document = standard.ImportTo(Document);
            }
            else
            {
                SetStandards(standard.Peptides.Select(pep => pep.ModifiedTarget));
            }
            return true;
        }

        private bool ProcessProtein()
        {
            if (comboProteins.SelectedIndex == -1)
            {
                MessageDlg.Show(this, Resources.ChooseIrtStandardPeptidesDlg_OkDialog_Please_select_a_protein_containing_the_list_of_standard_peptides_for_the_iRT_calculator_);
                comboProteins.Focus();
                return false;
            }

            var protein = ((PeptideGroupItem)comboProteins.SelectedItem).PeptideGroup;
            SetStandards(protein.Peptides.Select(pep => pep.ModifiedTarget));
            if (Document.PeptideGroupCount > 0)
            {
                var pathFrom = Document.GetPathTo(Document.FindNodeIndex(protein.Id));
                var pathTo = Document.GetPathTo(Document.FindNodeIndex(Document.PeptideGroups.First().Id));
                if (!Equals(pathFrom, pathTo))
                {
                    Document = Document.MoveNode(pathFrom, pathTo, out _);
                }
            }
            return true;
        }

        private bool ProcessTransitionList()
        {
            if (!File.Exists(txtTransitionList.Text))
            {
                MessageDlg.Show(this, Resources.ChooseIrtStandardPeptides_OkDialog_Transition_list_field_must_contain_a_path_to_a_valid_file_);
                txtTransitionList.Focus();
                return false;
            }

            try
            {
                var inputs = new MassListInputs(txtTransitionList.Text);
                Document = Document.ImportMassList(inputs, new IdentityPath(Document.PeptideGroups.First().Id), out _, out _irtAdd, out _librarySpectra, out var errorList);
                if (errorList.Any())
                    throw new InvalidDataException(errorList[0].ErrorMessage);
                IrtFile = txtTransitionList.Text;
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(this, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Error_reading_iRT_standards_transition_list___0_, x.Message), x);
                return false;
            }
            return true;
        }

        private void SetStandards(IEnumerable<Target> targets)
        {
            _irtTargets = new TargetMap<bool>(targets.Select(target => new KeyValuePair<Target, bool>(target, true)));
        }

        private void UpdateSelection(object sender, EventArgs e)
        {
            comboExisting.Enabled = radioExisting.Checked;
            comboProteins.Enabled = radioProtein.Checked;
            txtTransitionList.Enabled = btnBrowseTransitionList.Enabled = radioTransitionList.Checked;
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
