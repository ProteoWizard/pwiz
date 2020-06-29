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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class ChangeIrtPeptidesDlg : FormEx
    {
        private readonly TargetMap<DbIrtPeptide> _dictSequenceToPeptide;
        private IList<DbIrtPeptide> _standardPeptides;
        private readonly SrmDocument _document;
        private readonly IrtPeptidePicker _picker;

        public ChangeIrtPeptidesDlg(SrmDocument document, IList<DbIrtPeptide> irtPeptides)
        {
            TargetResolver = new TargetResolver(irtPeptides.Select(p=>p.Target));
            _dictSequenceToPeptide = new TargetMap<DbIrtPeptide>(irtPeptides.Select(pep =>
                new KeyValuePair<Target, DbIrtPeptide>(pep.ModifiedTarget, pep)));
            _document = document;
            _picker = new IrtPeptidePicker();

            InitializeComponent();

            comboProteins.Items.Add(new ComboBoxProtein(null));
            comboProteins.Items.AddRange(document.MoleculeGroups.Select(protein => new ComboBoxProtein(protein))
                .Where(protein => protein.PeptideStrings(TargetResolver).Any()).ToArray());
            comboProteins.SelectedIndex = 0;
            if (comboProteins.Items.Count == 1)
                comboProteins.Enabled = false;

            Peptides = irtPeptides.Where(peptide => peptide.Standard).ToArray();

            // Find and select matching protein
            for (var i = 1; i < comboProteins.Items.Count; i++)
            {
                var item = (ComboBoxProtein) comboProteins.Items[i];
                if (item.Protein.PeptideCount != PeptideLines.Length)
                    continue;
                var targets = new TargetMap<bool>(((ComboBoxProtein) comboProteins.Items[i]).Protein.Peptides.Select(pep =>
                    new KeyValuePair<Target, bool>(pep.ModifiedTarget, true)));
                if (PeptideLines.All(line => targets.ContainsKey(new Target(line))))
                {
                    comboProteins.SelectedIndex = i;
                    break;
                }
            }
        }

        public TargetResolver TargetResolver { get; }

        public IList<DbIrtPeptide> Peptides
        {
            get { return _standardPeptides; }
            set
            {
                _standardPeptides = value;
                PeptideLines = _standardPeptides.Select(peptide => TargetResolver.FormatTarget(peptide.ModifiedTarget)).ToArray();
            }
        }

        public PeptideGroupDocNode ReplacementProtein { get; private set; }

        public string PeptidesText
        {
            get { return textPeptides.Text; }
            set { textPeptides.Text = value; }
        }

        public string[] PeptideLines
        {
            get { return textPeptides.Lines; }
            set { textPeptides.Lines = value; }
        }

        public IEnumerable<PeptideGroupDocNode> Proteins => comboProteins.Items.Cast<ComboBoxProtein>()
            .Where(obj => obj.IsNotNull).Select(protein => protein.Protein);

        public PeptideGroupDocNode SelectedProtein
        {
            get
            {
                return ((ComboBoxProtein) comboProteins.SelectedItem).Protein;
            }
            set
            {
                for (var i = 0; i < comboProteins.Items.Count; i++)
                {
                    var protein = (ComboBoxProtein) comboProteins.Items[i];
                    if (!protein.IsNotNull)
                    {
                        if (value == null)
                        {
                            comboProteins.SelectedIndex = i;
                            return;
                        }
                    }
                    else if (ReferenceEquals(protein.Protein, value))
                    {
                        comboProteins.SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        public void OkDialog()
        {
            var reader = new StringReader(textPeptides.Text);
            var standardPeptides = new List<DbIrtPeptide>();
            var invalidLines = new List<string>();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                // Skip blank lines
                if (string.IsNullOrEmpty(line))
                    continue;
                DbIrtPeptide peptide = null;
                var target = TargetResolver.ResolveTarget(line);
                if (target == null || !_dictSequenceToPeptide.TryGetValue(target, out peptide))  // CONSIDER(bspratt) - small molecule equivalent?
                    invalidLines.Add(line);
                standardPeptides.Add(peptide);
            }

            if (invalidLines.Count > 0)
            {
                string message;
                if (invalidLines.Count == 1)
                {
                    message = ModeUIAwareStringFormat(Resources.ChangeIrtPeptidesDlg_OkDialog_The_sequence__0__is_not_currently_in_the_database,
                                            invalidLines[0]);
                    MessageBox.Show(this, message, Program.Name);
                }
                else
                {
                    message = TextUtil.LineSeparate(GetModeUIHelper().Translate(Resources.ChangeIrtPeptidesDlg_OkDialog_The_following_sequences_are_not_currently_in_the_database),
                                                    string.Empty,
                                                    TextUtil.LineSeparate(invalidLines),
                                                    string.Empty,
                                                    GetModeUIHelper().Translate(Resources.ChangeIrtPeptidesDlg_OkDialog_Standard_peptides_must_exist_in_the_database));
                    MessageBox.Show(this, message, Program.Name);
                }
                return;
            }

            ReplacementProtein = null;
            var selected = (ComboBoxProtein) comboProteins.SelectedItem;
            if (selected.IsNotNull)
            {
                var removedPeptides = selected.RemovedPeptides(TargetResolver, textPeptides.Lines).ToArray();
                if (removedPeptides.Any())
                {
                    switch (MultiButtonMsgDlg.Show(this, TextUtil.LineSeparate(
                            Resources.ChangeIrtPeptidesDlg_OkDialog_The_following_peptides_were_removed_,
                            TextUtil.LineSeparate(removedPeptides.Select(nodePep => nodePep.ModifiedSequenceDisplay)),
                            Resources.ChangeIrtPeptidesDlg_OkDialog_Would_you_like_to_remove_them_from_the_document_),
                        MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true))
                    {
                        case DialogResult.Yes:
                            ReplacementProtein = selected.RemovePeptides(TargetResolver, textPeptides.Lines);
                            break;
                        case DialogResult.No:
                            break;
                        case DialogResult.Cancel:
                            return;
                    }
                }
            }

            _standardPeptides = standardPeptides.ToArray();

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void comboProteins_SelectedIndexChanged(object sender, EventArgs e)
        {
            PeptideLines = ((ComboBoxProtein) comboProteins.SelectedItem).PeptideStrings(TargetResolver).ToArray();
        }

        private class ComboBoxProtein
        {
            public PeptideGroupDocNode Protein { get; }

            public bool IsNotNull => Protein != null;

            public ComboBoxProtein(PeptideGroupDocNode protein)
            {
                Protein = protein;
            }

            public IEnumerable<string> PeptideStrings(TargetResolver targetResolver)
            {
                return Protein?.Molecules.Select(pep => targetResolver.FormatTarget(pep.ModifiedTarget)) ?? new string[0];
            }

            private static TargetMap<bool> TextTargets(TargetResolver targetResolver, IEnumerable<string> pepLines)
            {
                return new TargetMap<bool>(pepLines.Select(line => line.Trim())
                    .Where(line => !string.IsNullOrEmpty(line))
                    .Select(line => new KeyValuePair<Target, bool>(targetResolver.ResolveTarget(line), true)));
            }

            public IEnumerable<PeptideDocNode> RemovedPeptides(TargetResolver targetResolver, IEnumerable<string> pepLines)
            {
                var textTargets = TextTargets(targetResolver, pepLines);
                return Protein.Molecules.Where(nodePep => !textTargets.ContainsKey(nodePep.ModifiedTarget));
            }

            public PeptideGroupDocNode RemovePeptides(TargetResolver targetResolver, IEnumerable<string> pepLines)
            {
                var textTargets = TextTargets(targetResolver, pepLines);
                return (PeptideGroupDocNode) Protein.ChangeChildren(Protein.Molecules
                    .Where(molecule => textTargets.ContainsKey(molecule.ModifiedTarget)).Cast<DocNode>().ToList());
            }

            public override string ToString()
            {
                return Protein?.Name ?? string.Empty;
            }
        }

        private void btnUseResults_Click(object sender, EventArgs e)
        {
            UseResults();
        }

        public void UseResults()
        {
            if (!_document.Settings.HasResults)
            {
                MessageDlg.Show(this, Resources.CalibrateIrtDlg_UseResults_The_document_must_contain_results_to_calibrate_a_standard);
                return;
            }

            var count = _document.Molecules.Where(nodePep => nodePep.SchedulingTime.HasValue).Select(nodePep => nodePep.ModifiedTarget).Distinct().Count();
            if (count < CalibrateIrtDlg.MIN_STANDARD_PEPTIDES)
            {
                MessageDlg.Show(this,
                    string.Format(
                        Resources.CalibrateIrtDlg_UseResults_The_document_contains_results_for__0__peptides__which_is_less_than_the_minimum_requirement_of__1__to_calibrate_a_standard_,
                        count, CalibrateIrtDlg.MIN_STANDARD_PEPTIDES));
                return;
            }
            else if (count < CalibrateIrtDlg.MIN_SUGGESTED_STANDARD_PEPTIDES)
            {
                if (MultiButtonMsgDlg.Show(this,
                        string.Format(
                            Resources.CalibrateIrtDlg_UseResults_The_document_contains_results_for__0__peptides__but_using_fewer_than__1__standard_peptides_is_not_recommended__Are_you_sure_you_want_to_continue_,
                            count, CalibrateIrtDlg.MIN_SUGGESTED_STANDARD_PEPTIDES),
                        MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false) != DialogResult.Yes)
                {
                    return;
                }
            }
            else if (count > 20)
            {
                using (var dlg = new AddIrtStandardsDlg(count, false))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;
                    count = dlg.StandardCount;
                }
            }

            if (!_picker.HasScoredPeptides)
            {
                using (var longWaitDlg = new LongWaitDlg { Text = Resources.CalibrationGridViewDriver_FindEvenlySpacedPeptides_Calculating_scores })
                {
                    longWaitDlg.PerformWork(this, 1000, pm => _picker.ScorePeptides(_document, pm));
                    if (longWaitDlg.IsCanceled)
                        return;
                }
            }

            var useCirt = false;
            if (_picker.TryGetCirtRegression(count, out _, out _))
            {
                switch (MultiButtonMsgDlg.Show(this, string.Format(
                    Resources.CalibrationGridViewDriver_FindEvenlySpacedPeptides_This_document_contains__0__CiRT_peptides__Would_you_like_to_use__1__of_them_as_your_iRT_standards_,
                    _picker.CirtPeptideCount, count), MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true))
                {
                    case DialogResult.Yes:
                        useCirt = true;
                        break;
                    case DialogResult.No:
                        break;
                    case DialogResult.Cancel:
                        return;
                }
            }

            comboProteins.SelectedIndex = 0;
            PeptideLines = _picker.Pick(count, null, useCirt).Select(pep => pep.Target.ToString()).ToArray();
        }
    }
}