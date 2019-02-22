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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class ChangeIrtPeptidesDlg : FormEx
    {
        private readonly IDictionary<Target, DbIrtPeptide> _dictSequenceToPeptide;
        private IList<DbIrtPeptide> _standardPeptides;

        public ChangeIrtPeptidesDlg(IList<DbIrtPeptide> irtPeptides)
        {
            TargetResolver = new TargetResolver(irtPeptides.Select(p=>p.Target));
            _dictSequenceToPeptide = new Dictionary<Target, DbIrtPeptide>();
            foreach (var peptide in irtPeptides)
            {
                if (!_dictSequenceToPeptide.ContainsKey(peptide.ModifiedTarget))
                    _dictSequenceToPeptide.Add(peptide.ModifiedTarget, peptide);
            }

            InitializeComponent();

            Peptides = irtPeptides.Where(peptide => peptide.Standard).ToArray();
        }

        public TargetResolver TargetResolver { get; private set; }

        public IList<DbIrtPeptide> Peptides
        {
            get { return _standardPeptides; }
            set
            {
                _standardPeptides = value;
                textPeptides.Text = string.Join(Environment.NewLine,
                    _standardPeptides.Select(peptide => TargetResolver.FormatTarget(peptide.ModifiedTarget)).ToArray());
            }
        }

        public string PeptidesText
        {
            get { return textPeptides.Text; }
            set { textPeptides.Text = value; }
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

            _standardPeptides = standardPeptides.ToArray();

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}