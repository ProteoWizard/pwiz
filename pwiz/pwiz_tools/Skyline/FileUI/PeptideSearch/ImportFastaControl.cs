/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class ImportFastaControl : UserControl
    {
        public ImportFastaControl(SkylineWindow skylineWindow)
        {
            SkylineWindow = skylineWindow;

            InitializeComponent();

            ImportFastaHelper = new ImportFastaHelper(tbxFasta, tbxError, panelError);
        }

        private SkylineWindow SkylineWindow { get; set; }
        private Form WizardForm { get { return FormEx.GetParentForm(this); } }

        private ImportFastaHelper ImportFastaHelper { get; set; }

        private void browseFastaBtn_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.ImportFastaControl_browseFastaBtn_Click_Open_FASTA,
                InitialDirectory = Settings.Default.FastaDirectory,
                CheckPathExists = true
                // FASTA files often have no extension as well as .fasta and others
            })
            {
                if (dlg.ShowDialog(WizardForm) == DialogResult.OK)
                {
                    SetFastaContent(dlg.FileName);
                }
            }

        }

        private void tbxFasta_TextChanged(object sender, EventArgs e)
        {
            ImportFastaHelper.ClearFastaError();
        }

        public void SetFastaContent(string fastaFilePath)
        {
            tbxFasta.Text += GetFastaFileContent(fastaFilePath);
        }

        private string GetFastaFileContent(string fastaFileName)
        {
            string fastaText = string.Empty;
            try
            {
                using (var readerFasta = new StreamReader(fastaFileName))
                {
                    fastaText = readerFasta.ReadToEnd();
                    readerFasta.Close();
                }
            }
            catch (Exception x)
            {
                MessageDlg.Show(WizardForm, TextUtil.LineSeparate(string.Format(Resources.ImportFastaControl_GetFastaFileContent_Failed_reading_the_file__0__, fastaFileName), x.Message));
            }

            return fastaText;
        }

        private bool VerifyAtLeastOnePrecursorTransition(SrmDocument doc)
        {
            if (!HasPrecursorTransitions(doc))
            {
                MessageDlg.Show(WizardForm, Resources.ImportFastaControl_VerifyAtLeastOnePrecursorTransition_The_document_must_contain_at_least_one_precursor_transition_in_order_to_proceed_);
                return false;
            }

            return true;
        }

        private static bool HasPrecursorTransitions(SrmDocument doc)
        {
            return doc.Transitions.Any(nodeTran => nodeTran.Transition.IonType == IonType.precursor);
        }

        private static bool IsFragmentTypePrecursor(SrmDocument doc)
        {
            return doc.Settings.TransitionSettings.Filter.IonTypes.Contains(IonType.precursor);
        }

        private static SrmDocument AutoSelectAllMatchingTransitions(SrmDocument doc)
        {
            var settings = doc.Settings;
            if (!settings.TransitionSettings.Filter.AutoSelect)
            {
                settings = settings.ChangeTransitionFilter(filter => filter.ChangeAutoSelect(true));
                return doc.ChangeSettings(settings);
            }

            return doc;
        }

        private SrmDocument SwitchToPrecursorTransitions(SrmDocument doc)
        {
            var settings = doc.Settings;
            if (!IsFragmentTypePrecursor(doc))
            {
                settings = settings.ChangeTransitionFilter(filter => filter.ChangeIonTypes(new[] { IonType.precursor }));
                return doc.ChangeSettings(settings);
            }

            return doc;
        }

        private static SrmDocument ChangeAutoManageChildren(SrmDocument doc, PickLevel which, bool autoPick)
        {
            var refine = new RefinementSettings {AutoPickChildrenAll = which, AutoPickChildrenOff = !autoPick };
            return refine.Refine(doc);
        }

        public bool ImportFasta()
        {
            if (string.IsNullOrEmpty(tbxFasta.Text)) // The user didn't specify any FASTA content
            {
                var docCurrent = SkylineWindow.DocumentUI;
                // If the document has precursor transitions already, then just trust the user
                // knows what they are doing, and this document is already set up for MS1 filtering
                if (HasPrecursorTransitions(docCurrent))
                    return true;

                if (docCurrent.PeptideCount == 0)
                {
                    MessageDlg.Show(WizardForm, TextUtil.LineSeparate(Resources.ImportFastaControl_ImportFasta_The_document_does_not_contain_any_peptides_,
                                                                      Resources.ImportFastaControl_ImportFasta_Please_import_FASTA_to_add_peptides_to_the_document_));
                    return false;
                }

                if (MessageBox.Show(WizardForm, TextUtil.LineSeparate(Resources.ImportFastaControl_ImportFasta_The_document_does_not_contain_any_precursor_transitions_,
                                                                      Resources.ImportFastaControl_ImportFasta_Would_you_like_to_change_the_document_settings_to_automatically_pick_the_precursor_transitions_specified_in_the_full_scan_settings_),
                                    Program.Name, MessageBoxButtons.OKCancel) != DialogResult.OK)
                    return false;

                SkylineWindow.ModifyDocument(Resources.ImportFastaControl_ImportFasta_Change_settings_to_add_precursors, doc =>
                    {
                        doc = SwitchToPrecursorTransitions(doc);
                        doc = AutoSelectAllMatchingTransitions(doc);
                        // Turn off auto-manage-children at the precursor level only.
                        return ChangeAutoManageChildren(doc, PickLevel.transitions, true);
                    });
            }
            else // The user specified some FASTA content
            {
                // If the user is about to add any new transitions by importing
                // FASTA, set FragmentType='p' and AutoSelect=true
                SkylineWindow.ModifyDocument(Resources.ImportFastaControl_ImportFasta_Change_settings, doc =>
                    {
                        // First preserve the state of existing document nodes in the tree
                        // Todo: There are better ways to do this than this brute force method; revisit later.
                        if (doc.PeptideGroupCount > 0)
                            doc = ChangeAutoManageChildren(doc, PickLevel.all, false);
                        doc = SwitchToPrecursorTransitions(doc);
                        doc = AutoSelectAllMatchingTransitions(doc);
                        var pick = doc.Settings.PeptideSettings.Libraries.Pick;
                        if (pick != PeptidePick.library && pick != PeptidePick.both)
                            doc = doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(lib => lib.ChangePick(PeptidePick.library)));
                        return doc;
                    });

                var nodeInsert = SkylineWindow.SequenceTree.SelectedNode as SrmTreeNode;
                IdentityPath selectedPath = nodeInsert != null ? nodeInsert.Path : null;
                bool succeeded = false;
                SkylineWindow.ModifyDocument(Resources.ImportFastaControl_ImportFasta_Insert_FASTA,
                    doc =>
                    {
                        var newDocument = ImportFastaHelper.AddFasta(doc, ref selectedPath);
                        if (newDocument == null)
                            return doc;
                        succeeded = true;
                        return newDocument;
                    });

                if (!VerifyAtLeastOnePrecursorTransition(SkylineWindow.Document))
                    return false;
                return succeeded;
            }

            return true;
        }
    }
}
