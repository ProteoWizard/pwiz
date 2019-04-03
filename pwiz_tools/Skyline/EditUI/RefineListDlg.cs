/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    public partial class RefineListDlg : ModeUIInvariantFormEx,  // This dialog is inherently proteomic, never wants the "peptide"->"molecule" translation
                  IAuditLogModifier<RefineListDlg.RefineListSettings>
    {
        private readonly SrmDocument _document;

        public RefineListDlg(SrmDocument document)
        {
            InitializeComponent();

            _document = document;
        }

        public LibraryKey[] AcceptedPeptides { get; private set; }

        public bool RemoveEmptyProteins
        {
            get { return cbRemoveProteins.Checked; }
            set { cbRemoveProteins.Checked = value; }            
        }

        public bool MatchModified
        {
            get { return cbMatchModified.Checked; }
            set { cbMatchModified.Checked = value; }
        }

        public string PeptidesText
        {
            get { return textPeptides.Text; }
            set { textPeptides.Text = value; }
        }

        public class RefineListSettings : AuditLogOperationSettings<RefineListSettings>, IAuditLogComparable
        {
            public RefineListSettings(string[] acceptedPeptides, bool matchModified, bool removeEmptyProteins, string peptidesText)
            {
                AcceptedPeptides = acceptedPeptides;
                MatchModified = matchModified;
                RemoveEmptyProteins = removeEmptyProteins;
                PeptidesText = peptidesText;
            }

            protected override AuditLogEntry CreateEntry(SrmDocumentPair docPair)
            {
                var entry = AuditLogEntry.CreateCountChangeEntry(MessageType.accepted_peptide,
                        MessageType.accept_peptides, docPair.NewDocumentType, AcceptedPeptides)
                    .ChangeAllInfo(new DetailLogMessage[0]);

                // TODO: if this happens more often, consider adding something like "reverse merge"
                entry = entry.Merge(base.CreateEntry(docPair));
                return entry.ChangeExtraInfo(entry.ExtraInfo + Environment.NewLine + Environment.NewLine + PeptidesText);
            }

            [Track]
            public string[] AcceptedPeptides { get; private set; }
            [Track]
            public bool MatchModified { get; private set; }
            [Track]
            public bool RemoveEmptyProteins { get; private set; }
            public string PeptidesText { get; private set; }

            public object GetDefaultObject(ObjectInfo<object> info)
            {
                return new RefineListSettings(new string[0], false, false, null);
            }
        }

        public RefineListSettings FormSettings
        {
            get
            {
                return new RefineListSettings(AcceptedPeptides.Select(key => key.Target.AuditLogText).ToArray(),
                    MatchModified, RemoveEmptyProteins, PeptidesText);
            }
        }

        public void OkDialog()
        {
            var reader = new StringReader(PeptidesText);
            var invalidLines = new List<string>();
            var notFoundLines = new List<string>();
            var acceptedPeptides = new List<LibraryKey>();
            var peptideSequences = GetPeptideSequences();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;
                int foundAt;
                var charge = Transition.GetChargeFromIndicator(line,
                    TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, out foundAt);
                if (!charge.IsEmpty)
                    line = line.Substring(0, foundAt);
                Target target;
                try
                {
                    // CONSIDER(bspratt) small molecule equivalent?
                    if (!FastaSequence.IsExSequence(line))
                    {
                        invalidLines.Add(line);
                        continue;
                    }
                    line = SequenceMassCalc.NormalizeModifiedSequence(line);
                    target = new Target(line); 
                }
                catch (Exception)
                {
                    invalidLines.Add(line);
                    continue;
                }

                if (!peptideSequences.ContainsKey(target))
                    notFoundLines.Add(line);
                else
                    acceptedPeptides.Add(new LibKey(target, charge).LibraryKey); 
            }

            if (invalidLines.Count > 0)
            {
                if (invalidLines.Count == 1)
                    MessageDlg.Show(this, string.Format(Resources.RefineListDlg_OkDialog_The_sequence__0__is_not_a_valid_peptide, invalidLines[0]));
                else
                    MessageDlg.Show(this, TextUtil.LineSeparate(Resources.RefineListDlg_OkDialog_The_following_sequences_are_not_valid_peptides,string.Empty, TextUtil.LineSeparate(invalidLines)));
                return;
            }
            if (acceptedPeptides.Count == 0)
            {
                MessageDlg.Show(this, Resources.RefineListDlg_OkDialog_None_of_the_specified_peptides_are_in_the_document);
                return;
            }
            if (notFoundLines.Count > 0)
            {
                string message;
                if (notFoundLines.Count == 1)
                {
                    message = string.Format(Resources.RefineListDlg_OkDialog_The_peptide__0__is_not_in_the_document_Do_you_want_to_continue, notFoundLines[0]);
                }
                else if (notFoundLines.Count < 15)
                {
                    message = TextUtil.LineSeparate(Resources.RefineListDlg_OkDialog_The_following_peptides_are_not_in_the_document, string.Empty,
                                                    TextUtil.LineSeparate(notFoundLines),string.Empty,
                                                    Resources.RefineListDlg_OkDialog_Do_you_want_to_continue);
                }
                else
                {
                    message = string.Format(Resources.RefineListDlg_OkDialog_Of_the_specified__0__peptides__1__are_not_in_the_document_Do_you_want_to_continue,
                                            notFoundLines.Count + acceptedPeptides.Count, notFoundLines.Count);
                }
                if (MultiButtonMsgDlg.Show(this, message, MultiButtonMsgDlg.BUTTON_OK) != DialogResult.OK)
                    return;
            }

            AcceptedPeptides = acceptedPeptides.ToArray();
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private TargetMap<PeptideDocNode> GetPeptideSequences()
        {
            return new TargetMap<PeptideDocNode>(_document.Peptides.Select(pep=>new KeyValuePair<Target, PeptideDocNode>(MatchModified ? pep.ModifiedTarget : pep.Target, pep)));
        }

        private void textPeptides_Enter(object sender, EventArgs e)
        {
            AcceptButton = null;
        }

        private void textPeptides_Leave(object sender, EventArgs e)
        {
            AcceptButton = btnOk;
        }
    }
}
