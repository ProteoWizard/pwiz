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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class ImportFastaControl : UserControl
    {
        private readonly SequenceTree _sequenceTree;

        public ImportFastaControl(IModifyDocumentContainer documentContainer, SequenceTree sequenceTree)
        {
            DocumentContainer = documentContainer;
            _sequenceTree = sequenceTree;

            InitializeComponent();

            ImportFastaHelper = new ImportFastaHelper(tbxFasta, tbxError, panelError, helpTipFasta);

            tbxFastaHeightDifference = Height - tbxFasta.Height;

            _driverEnzyme = new SettingsListComboDriver<Enzyme>(comboEnzyme, Settings.Default.EnzymeList);
            _driverEnzyme.LoadList(DocumentContainer.Document.Settings.PeptideSettings.Enzyme.GetKey());

            MaxMissedCleavages = documentContainer.Document.Settings.PeptideSettings.DigestSettings.MaxMissedCleavages;
            cbDecoyMethod.Items.Add(string.Empty);
            cbDecoyMethod.Items.Add(DecoyGeneration.SHUFFLE_SEQUENCE);
            cbDecoyMethod.Items.Add(DecoyGeneration.REVERSE_SEQUENCE);
            cbDecoyMethod.SelectedIndex = 0;

            tbxFasta.Resize += TbxFasta_Resize;
        }

        private IModifyDocumentContainer DocumentContainer { get; set; }
        private Form WizardForm { get { return FormEx.GetParentForm(this); } }

        private ImportFastaHelper ImportFastaHelper { get; set; }
        private readonly SettingsListComboDriver<Enzyme> _driverEnzyme;

        private const long MAX_FASTA_TEXTBOX_LENGTH = 5 << 20;    // 5 MB
        private bool _fastaFile
        {
            get { return !tbxFasta.Multiline; }
            set
            {
                if (tbxFasta.Multiline != value)
                    return;

                tbxFasta.Multiline = !value;
                if (tbxFasta.Multiline)
                {
                    tbxFasta.Height = Height - tbxFastaHeightDifference;
                    clearBtn.Visible = true;
                }
                else
                {
                    int buttonTextboxOffset = tbxFastaTargets.Location.Y - browseFastaTargetsBtn.Location.Y;
                    tbxFasta.Location = new System.Drawing.Point(tbxFasta.Location.X, browseFastaBtn.Location.Y + buttonTextboxOffset);
                    clearBtn.Visible = false;
                }
            }
        }
        private readonly int tbxFastaHeightDifference;
        private bool _decoyGenerationEnabled;
        private bool _isDdaSearch;

        private void TbxFasta_Resize(object sender, EventArgs e)
        {
            targetFastaPanel.Location = new System.Drawing.Point(targetFastaPanel.Location.X, tbxFasta.Bounds.Bottom + 8);
            targetFastaPanel.Width = Width; // not sure why this is necessary
        }

        public bool ContainsFastaContent { get { return !string.IsNullOrWhiteSpace(tbxFasta.Text); } }

        public bool IsDDASearch
        {
            get => _isDdaSearch;
            set
            {
                _isDdaSearch = value;
                _fastaFile = true;
            }
        }

        public ImportFastaSettings ImportSettings
        {
            get { return new ImportFastaSettings(this); }
        }

        public class ImportFastaSettings
        {
            public ImportFastaSettings(ImportFastaControl control) : this(control.Enzyme, control.MaxMissedCleavages,
                control.FastaFile, control.FastaText, control.FastaImportTargetsFile, control.DecoyGenerationMethod, control.NumDecoys,
                control.AutoTrain)
            {
            }

            public static ImportFastaSettings GetDefault(PeptideSettings peptideSettings)
            {
                return new ImportFastaSettings(peptideSettings.Enzyme,
                    peptideSettings.DigestSettings.MaxMissedCleavages, null, null, null, string.Empty, null, false);
            }

            public ImportFastaSettings(Enzyme enzyme, int maxMissedCleavages, string fastaFile, string fastaText, string fastaImportTargetsFile, string decoyGenerationMethod, double? numDecoys, bool autoTrain)
            {
                Enzyme = enzyme;
                MaxMissedCleavages = maxMissedCleavages;
                FastaFile = AuditLogPath.Create(fastaFile);
                FastaText = fastaText;
                FastaImportTargetsFile = AuditLogPath.Create(fastaImportTargetsFile);
                DecoyGenerationMethod = decoyGenerationMethod;
                NumDecoys = numDecoys;
                AutoTrain = autoTrain;
            }

            private class FastaTextDefault : DefaultValues
            {
                public override bool IsDefault(object obj, object parentObject)
                {
                    return !string.IsNullOrEmpty(((ImportFastaSettings) parentObject).FastaText);
                }
            }

            [Track]
            public Enzyme Enzyme { get; private set; }
            [Track]
            public int MaxMissedCleavages { get; private set; }
            [Track(defaultValues:typeof(DefaultValuesNull))]
            public AuditLogPath FastaFile { get; private set; }
            [Track(defaultValues: typeof(FastaTextDefault))]
            public string FastaText { get; private set; }
            [Track(defaultValues: typeof(DefaultValuesNull))]
            public AuditLogPath FastaImportTargetsFile { get; private set; }
            [Track]
            public string DecoyGenerationMethod { get; private set; }
            [Track]
            public double? NumDecoys { get; private set; }
            [Track]
            public bool AutoTrain { get; private set; }
        }

        public Enzyme Enzyme
        {
            get { return Settings.Default.GetEnzymeByName(comboEnzyme.SelectedItem.ToString()); }
            set { comboEnzyme.SelectedItem = value; }
        }

        public int MaxMissedCleavages
        {
            get { return int.Parse(cbMissedCleavages.SelectedItem.ToString()); }
            set
            {
                cbMissedCleavages.SelectedItem = value.ToString(LocalizationHelper.CurrentCulture);
                if (cbMissedCleavages.SelectedIndex < 0)
                    cbMissedCleavages.SelectedIndex = 0;
            }
        }

        public bool RequirePrecursorTransition { private get; set; }

        public bool DecoyGenerationEnabled
        {
            get { return _decoyGenerationEnabled; }
            set { panelDecoys.Visible = _decoyGenerationEnabled = value; }
        }

        public string DecoyGenerationMethod
        {
            get { return DecoyGenerationEnabled ? cbDecoyMethod.SelectedItem.ToString() : string.Empty; }
            set
            {
                cbDecoyMethod.SelectedItem = value;
                if (cbDecoyMethod.SelectedIndex < 0)
                    cbDecoyMethod.SelectedIndex = 0;
            }
        }

        public double? NumDecoys
        {
            get
            {
                double numDecoys;
                return DecoyGenerationEnabled && double.TryParse(txtNumDecoys.Text, out numDecoys) ? (double?) numDecoys : null;
            }
            set { txtNumDecoys.Text = value.ToString(); }
        }

        public bool AutoTrain
        {
            get { return DecoyGenerationEnabled && cbAutoTrain.Checked; }
            set { cbAutoTrain.Checked = value; }
        }

        public string FastaFile { get; private set; }
        public string FastaText { get; private set; }

        public string FastaImportTargetsFile
        {
            get { return tbxFastaTargets.Text; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    tbxFastaTargets.Text = string.Empty;
                    cbImportFromSeparateFasta.Checked = browseFastaTargetsBtn.Visible = false;
                }
                else
                {
                    tbxFastaTargets.Text = value;
                    cbImportFromSeparateFasta.Checked = browseFastaTargetsBtn.Visible = true;
                }
            }
        }

        private bool browseForFasta(out string fastaFilepath)
        {
            string initialDir = Settings.Default.FastaDirectory;
            if (string.IsNullOrEmpty(initialDir))
            {
                initialDir = Path.GetDirectoryName(DocumentContainer.DocumentFilePath);
            }
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.ImportFastaControl_browseFastaBtn_Click_Open_FASTA,
                InitialDirectory = initialDir,
                CheckPathExists = true,
                Filter = @"FASTA files|*.fasta;*.fa;*.faa|All files|*.*"
            })
            {
                if (dlg.ShowDialog(WizardForm) == DialogResult.OK)
                {
                    fastaFilepath = dlg.FileName;
                    return true;
                }
            }

            fastaFilepath = null;
            return false;
        }

        private void browseFastaBtn_Click(object sender, EventArgs e)
        {
            if (browseForFasta(out string fastaFilepath))
                SetFastaContent(fastaFilepath);
        }

        private void tbxFasta_TextChanged(object sender, EventArgs e)
        {
            ImportFastaHelper.ClearFastaError();
            if (_fastaFile)
            {
                FastaFile = tbxFasta.Text;
                if (!File.Exists(FastaFile))
                    ImportFastaHelper.ShowFastaError(Resources.ToolDescription_RunTool_File_not_found_);
        }
        }

        public void SetFastaContent(string fastaFilePath)
        {
            try
            {
                FastaFile = fastaFilePath;

                var fileInfo = new FileInfo(fastaFilePath);
                if (IsDDASearch || fileInfo.Length > MAX_FASTA_TEXTBOX_LENGTH)
                {
                    _fastaFile = true;
                    tbxFasta.Text = fastaFilePath;
                }
                else
                {
                    _fastaFile = false;
                    tbxFasta.Text = GetFastaFileContent(fastaFilePath);
                }
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(WizardForm, TextUtil.LineSeparate(string.Format(Resources.ImportFastaControl_SetFastaContent_Error_adding_FASTA_file__0__, fastaFilePath), x.Message), x);
            }
        }

        private string GetFastaFileContent(string fastaFileName)
        {
            string fastaText = string.Empty;
            try
            {
                using (var readerFasta = new StreamReader(fastaFileName))
                {
                    var sb = new StringBuilder();
                    string line;
                    while ((line = readerFasta.ReadLine()) != null)
                        sb.AppendLine(line);
                    fastaText = sb.ToString();
                }
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(WizardForm, TextUtil.LineSeparate(string.Format(Resources.ImportFastaControl_GetFastaFileContent_Failed_reading_the_file__0__, fastaFileName), x.Message), x);
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
            return doc.PeptideTransitions.Any(nodeTran => nodeTran.Transition.IonType == IonType.precursor);
        }

        public bool ImportFasta(IrtStandard irtStandard)
        {
            var settings = DocumentContainer.Document.Settings;
            var peptideSettings = settings.PeptideSettings;
            int missedCleavages = MaxMissedCleavages;
            var enzyme = Enzyme;
            if (!Equals(missedCleavages, peptideSettings.DigestSettings.MaxMissedCleavages) || !Equals(enzyme, peptideSettings.Enzyme))
            {
                var digest = new DigestSettings(missedCleavages, peptideSettings.DigestSettings.ExcludeRaggedEnds);
                peptideSettings = peptideSettings.ChangeDigestSettings(digest).ChangeEnzyme(enzyme);
                DocumentContainer.ModifyDocumentNoUndo(doc =>
                    doc.ChangeSettings(settings.ChangePeptideSettings(peptideSettings)));
            }

            if (!string.IsNullOrEmpty(DecoyGenerationMethod))
            {
                if (!NumDecoys.HasValue || NumDecoys <= 0)
                {
                    MessageDlg.Show(WizardForm, Resources.ImportFastaControl_ImportFasta_Please_enter_a_valid_number_of_decoys_per_target_greater_than_0_);
                    txtNumDecoys.Focus();
                    return false;
                }
                else if (Equals(DecoyGenerationMethod, DecoyGeneration.REVERSE_SEQUENCE) && NumDecoys > 1)
                {
                    MessageDlg.Show(WizardForm, Resources.ImportFastaControl_ImportFasta_A_maximum_of_one_decoy_per_target_may_be_generated_when_using_reversed_decoys_);
                    txtNumDecoys.Focus();
                    return false;
                }
            }

            if (!ContainsFastaContent) // The user didn't specify any FASTA content
            {
                var docCurrent = DocumentContainer.Document;
                // If the document has precursor transitions already, then just trust the user
                // knows what they are doing, and this document is already set up for MS1 filtering
                if (HasPrecursorTransitions(docCurrent)&& !IsDDASearch)
                    return true;

                if (docCurrent.PeptideCount == 0|| IsDDASearch)
                {
                    MessageDlg.Show(WizardForm, TextUtil.LineSeparate(Resources.ImportFastaControl_ImportFasta_The_document_does_not_contain_any_peptides_,
                                                                      Resources.ImportFastaControl_ImportFasta_Please_import_FASTA_to_add_peptides_to_the_document_));
                    return false;
                }

                if (MessageBox.Show(WizardForm, TextUtil.LineSeparate(Resources.ImportFastaControl_ImportFasta_The_document_does_not_contain_any_precursor_transitions_,
                                                                      Resources.ImportFastaControl_ImportFasta_Would_you_like_to_change_the_document_settings_to_automatically_pick_the_precursor_transitions_specified_in_the_full_scan_settings_),
                                    Program.Name, MessageBoxButtons.OKCancel) != DialogResult.OK)
                    return false;

                DocumentContainer.ModifyDocumentNoUndo(doc => ImportPeptideSearch.ChangeAutoManageChildren(doc, PickLevel.transitions, true));
            }
            else // The user specified some FASTA content
            {
                // If the user is about to add any new transitions by importing
                // FASTA, set FragmentType='p' and AutoSelect=true
                var docCurrent = DocumentContainer.Document;
                var docNew = ImportPeptideSearch.PrepareImportFasta(docCurrent);

                var nodeInsert = _sequenceTree.SelectedNode as SrmTreeNode;
                IdentityPath selectedPath = nodeInsert != null ? nodeInsert.Path : null;
                var newPeptideGroups = new List<PeptideGroupDocNode>();

                if (!_fastaFile)
                {
                    FastaText = tbxFasta.Text;
                    PasteError error = null;
                    // Import FASTA as content
                    using (var longWaitDlg = new LongWaitDlg(DocumentContainer) {Text = Resources.ImportFastaControl_ImportFasta_Insert_FASTA})
                    {
                        var docImportFasta = docNew;
                        longWaitDlg.PerformWork(WizardForm, 1000, longWaitBroker =>
                        {
                            docImportFasta = ImportFastaHelper.AddFasta(docImportFasta, longWaitBroker, ref selectedPath, out newPeptideGroups, out error);
                        });
                        docNew = docImportFasta;
                    }
                    // Document will be null if there was an error
                    if (docNew == null)
                    {
                        ImportFastaHelper.ShowFastaError(error);
                        return false;
                    }
                }
                else
                {
                    // Import FASTA as file
                    var fastaPath = string.IsNullOrEmpty(FastaImportTargetsFile) ? tbxFasta.Text : FastaImportTargetsFile;
                    try
                    {
                        using (var longWaitDlg = new LongWaitDlg(DocumentContainer) {Text = Resources.ImportFastaControl_ImportFasta_Insert_FASTA})
                        {
                            IdentityPath to = selectedPath;
                            var docImportFasta = docNew;
                            longWaitDlg.PerformWork(WizardForm, 1000, longWaitBroker =>
                            {
                                IdentityPath nextAdd;
                                docImportFasta = ImportPeptideSearch.ImportFasta(docImportFasta, fastaPath, longWaitBroker, to, out selectedPath, out nextAdd, out newPeptideGroups);
                            });
                            docNew = docImportFasta;
                        }
                    }
                    catch (Exception x)
                    {
                        MessageDlg.ShowWithException(this, string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__,
                                                            fastaPath, x.Message), x);
                        return false;
                    }
                }

                if (!newPeptideGroups.Any())
                {
                    MessageDlg.Show(this, Resources.ImportFastaControl_ImportFasta_Importing_the_FASTA_did_not_create_any_target_proteins_);
                    return false;
                }

                // Filter proteins based on number of peptides and add decoys
                using (var dlg = new PeptidesPerProteinDlg(docNew, newPeptideGroups, DecoyGenerationMethod, NumDecoys ?? 0))
                {
                    docNew = dlg.ShowDialog(WizardForm) == DialogResult.OK ? dlg.DocumentFinal : null;
                }

                // Document will be null if user was given option to keep or remove empty proteins and pressed cancel
                if (docNew == null)
                    return false;

                // Add iRT standards if not present
                if (irtStandard != null && irtStandard.HasDocument)
                {
                    var standardMap = new TargetMap<bool>(irtStandard.Peptides.Select(pep => new KeyValuePair<Target, bool>(pep.ModifiedTarget, true)));
                    var docStandards = new TargetMap<bool>(docNew.Peptides
                        .Where(nodePep => standardMap.ContainsKey(nodePep.ModifiedTarget)).Select(nodePep =>
                            new KeyValuePair<Target, bool>(nodePep.ModifiedTarget, true)));
                    if (irtStandard.Peptides.Any(pep => !docStandards.ContainsKey(pep.ModifiedTarget)))
                    {
                        docNew = irtStandard.ImportTo(docNew);
                    }
                }

                if (AutoTrain)
                {
                    if (!docNew.Peptides.Any(pep => pep.IsDecoy))
                    {
                        MessageDlg.Show(this, Resources.ImportFastaControl_ImportFasta_Cannot_automatically_train_mProphet_model_without_decoys__but_decoy_options_resulted_in_no_decoys_being_generated__Please_increase_number_of_decoys_per_target__or_disable_automatic_training_of_mProphet_model_);
                        return false;
                    }
                    docNew = docNew.ChangeSettings(docNew.Settings.ChangePeptideIntegration(integration => integration.ChangeAutoTrain(true)));
                }

                DocumentContainer.ModifyDocumentNoUndo(doc =>
                {
                    if (!ReferenceEquals(doc, docCurrent))
                        throw new InvalidDataException(Resources.SkylineWindow_ImportFasta_Unexpected_document_change_during_operation);
                    return docNew;
                });

                if (RequirePrecursorTransition && !VerifyAtLeastOnePrecursorTransition(DocumentContainer.Document))
                    return false;
            }

            return true;
        }

        private void clearBtn_Click(object sender, EventArgs e)
        {
            _fastaFile = IsDDASearch;
            tbxFasta.Clear();
        }

        private void enzyme_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverEnzyme.SelectedIndexChangedEvent(sender, e);
        }

        private void cbDecoyMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            var decoys = cbDecoyMethod.SelectedIndex != 0;
            txtNumDecoys.Enabled = decoys;
            cbAutoTrain.Enabled = decoys;
            if (!decoys)
            {
                cbAutoTrain.Checked = false;
            }
        }

        private void browseFastaTargetsBtn_Click(object sender, EventArgs e)
        {
            if (browseForFasta(out string fastaFilepath))
                FastaImportTargetsFile = fastaFilepath;
        }

        private void cbImportFromSeparateFasta_CheckedChanged(object sender, EventArgs e)
        {
            browseFastaTargetsBtn.Visible = tbxFastaTargets.Visible = cbImportFromSeparateFasta.Checked;
        }

        private void flowLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}