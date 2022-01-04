/*
 * Original author: Alex MacLean <alex.maclean2000 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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

using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using Protein = pwiz.ProteomeDatabase.API.Protein;

namespace pwiz.Skyline.FileUI
{
    public partial class ImportTransitionListColumnSelectDlg : ModeUIInvariantFormEx, ITipDisplayer
    {
        public MassListImporter Importer { get; set; }
        public List<LiteDropDownList> ComboBoxes { get; private set; }

        public bool WindowShown { get; private set; }

        private bool showIgnoredCols { get; set; }

        // These are only for error checking
        private readonly SrmDocument _docCurrent;
        private readonly MassListInputs _inputs;
        private readonly IdentityPath _insertPath;

        // For associating proteins
        private string[] _originalLines;
        private readonly NodeTip _proteinTip;
        private int _originalProteinIndex;
        private string[] _originalHeaders;
        // Protein name, FASTA sequence pairs for importing peptides into protein groups
        private Dictionary<string, FastaSequence> _dictNameSeq;
        // Stores the position of proteins for _proteinTip
        private List<Protein> _proteinList;

        // This list stores headers in the order we want to present them to the user along with an identifier denoting which mode they are associated with
        public List<Tuple<string, SrmDocument.DOCUMENT_TYPE>> KnownHeaderTypes = GetKnownHeaderTypes();

        public static List<Tuple<string, SrmDocument.DOCUMENT_TYPE>> GetKnownHeaderTypes()
        {
            return new List<Tuple<string, SrmDocument.DOCUMENT_TYPE>>
            {
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name,SrmDocument.DOCUMENT_TYPE.proteomic),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Description,SrmDocument.DOCUMENT_TYPE.proteomic),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence,SrmDocument.DOCUMENT_TYPE.proteomic),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy,SrmDocument.DOCUMENT_TYPE.proteomic),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT,SrmDocument.DOCUMENT_TYPE.proteomic),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Library_Intensity,SrmDocument.DOCUMENT_TYPE.proteomic),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct,SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Product_Formula,SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Product_Adduct,SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Product_Charge,SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Fragment_Name,SrmDocument.DOCUMENT_TYPE.proteomic),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Product_Name,SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Product_Neutral_Loss,SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time_Window,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Explicit_Declustering_Potential,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_S_Lens,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Cone_Voltage,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_Units,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_High_Energy_Offset,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Cross_Section__sq_A_,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Explicit_Compensation_Voltage,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(Resources.PasteDlg_UpdateMoleculeType_Note,SrmDocument.DOCUMENT_TYPE.mixed),
                Tuple.Create(@"InChiKey",SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(@"CAS",SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(@"HMDB",SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(@"InChi",SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(@"SMILES",SrmDocument.DOCUMENT_TYPE.small_molecules),
                Tuple.Create(@"KEGG",SrmDocument.DOCUMENT_TYPE.small_molecules),
            };
        }

        public static List<Tuple<string, SrmDocument.DOCUMENT_TYPE>> GetKnownHeaderTypesInvariant()
        {
            return LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, GetKnownHeaderTypes);
        }

        // When we switch modes we want to keep the column positions that were set in the mode not being used
        private List<string> smallMolColPositions;
        private List<string> peptideColPositions;

        public ImportTransitionListColumnSelectDlg(MassListImporter importer, SrmDocument docCurrent, MassListInputs inputs, IdentityPath insertPath)
        {
            Importer = importer;
            _docCurrent = docCurrent;
            _inputs = inputs;
            _insertPath = insertPath;
            _originalLines = Importer.RowReader.Lines.ToArray();
            showIgnoredCols = true;

            _proteinTip = new NodeTip(this) { Parent = this };
            previousIndices = new int[Importer.RowReader.Lines[0].ParseDsvFields(Importer.Separator).Length + 1];
            InitializeComponent();

            fileLabel.Text = Importer.Inputs.InputFilename;
            InitializeComboBoxes();
            DisplayData();
            PopulateComboBoxes();
            InitializeRadioButtons();
            checkBoxAssociateProteins.Visible = CheckboxVisible();
            UpdateAssociateProteinsState();
            IgnoreAllEmptyCols();
            //dataGrid.Update();
            ResizeComboBoxes();
        }

        public Rectangle ScreenRect
        {
            get { return Screen.GetBounds(dataGrid); }
        }
        public bool AllowDisplayTip
        {
            get { return true; }
        }
        public Rectangle RectToScreen(Rectangle r)
        {
            return dataGrid.RectangleToScreen(r);
        }

        private void UpdateAssociateProteinsState()
        {
            var isValid = Importer.RowReader.Indices.PeptideColumn != -1;
            checkBoxAssociateProteins.ForeColor =
                isValid ? Color.Black : Color.Gray;
            checkBoxAssociateProteins.Enabled = isValid;

        }
        /// <summary>
        /// Checks whether the associate proteins checkbox should be visible
        /// </summary>
        /// <returns>True if the checkbox should be visible</returns>
        private bool CheckboxVisible()
        {
            return radioPeptide.Checked && !_docCurrent.Settings.PeptideSettings.BackgroundProteome.IsNone;
        }

        /// <summary>
        /// In the event where there are multiple matches for a single peptide, or no matches
        /// for a peptide, let the user decide how to proceed
        /// </summary>
        private string[] ResolveMatchedProteins(int numWithDuplicates, int numUnmatched, int numFiltered, out bool canceled)
        {
            // Show a dialog asking the user how to proceed
            using (var filterDlg = new FilterMatchedPeptidesDlg( numWithDuplicates, numUnmatched, numFiltered, Importer.RowReader.Lines.Count == 1,  
                false)) // We do not support mixed transition lists, so there will never be small molecules
            {
                canceled = false;
                filterDlg.Text = checkBoxAssociateProteins.Text; // This title makes more sense in this context
                if (filterDlg.ShowDialog(this) != DialogResult.OK)
                {
                    // If they cancel do not change the document or the transition list
                    canceled = true;
                } else
                {
                    // Redo the association with the new filter settings, but do not show this dialog again
                    return AssociateProteins(false, out canceled);
                }
            }
            return null;

        }

        /// <summary>
        /// Match peptides to proteins in the background proteome
        /// </summary>
        /// <param name="triggerDialog">Trigger a dialog if there are peptides with multiple matches,
        /// peptides without matches, or peptides not meeting filter settings</param>
        /// /// <param name="canceled">True if the user cancelled the dialog to resolve filtered peptides</param>
        /// <returns>The transition list edited to include the protein names</returns>
        private string[] AssociateProteins(bool triggerDialog, out bool canceled)
        {
            // Initialize variables that are only used when associating proteins
            _dictNameSeq = new Dictionary<string, FastaSequence>();
            _proteinList = new List<Protein>();
            canceled = false;
            IDictionary<string, List<Protein>> dictSequenceProteins = null;
            var lines = new List<string>();

            using (var longWaitDlg = new LongWaitDlg() { Message = checkBoxAssociateProteins.Text })
            {
                longWaitDlg.PerformWork(this, 1000, longWaitBroker =>
                {
                    // If there are headers, add one describing the protein name column we will add
                    if (Importer.RowReader.Indices.Headers != null)
                    {
                        // Add a header we should recognize
                        var newHeaders = new string[_originalHeaders.Length + 1];
                        newHeaders[0] = Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name;
                        for (int i = 0; i < _originalHeaders.Length; i++)
                        {
                            newHeaders[i + 1] = _originalHeaders[i];
                        }
                        Importer.RowReader.Indices.Headers = newHeaders;
                    }

                    // Get a dictionary of peptides under consideration and the proteins they're associated with
                    var peptides = new List<string>(Importer.RowReader.Lines.Count);
                    peptides.AddRange(Importer.RowReader.Lines.Select(line => line.ParseDsvFields(Importer.Separator)).Select(fields => fields[Importer.RowReader.Indices.PeptideColumn]));
                    var stripped = new Dictionary<string, string>();
                    foreach (var pep in peptides)
                    {
                        if (!stripped.ContainsKey(pep))
                        {
                            var peptideSequence = FastaSequence.StripModifications(pep);
                            stripped[pep] = FastaSequence.IsExSequence(peptideSequence) ? peptideSequence : null;
                        }
                    }
                    var proteome = _docCurrent.Settings.PeptideSettings.BackgroundProteome;
                    using (var proteomeDb = proteome.OpenProteomeDb(longWaitBroker.CancellationToken))
                    {
                        dictSequenceProteins = proteomeDb.GetDigestion().GetProteinsWithSequences(stripped.Values, longWaitBroker.CancellationToken);
                    }
                });
            }
            if (dictSequenceProteins == null)
            {
                canceled = true;
                return Importer.RowReader.Lines.ToArray();
            }

            var lines = new List<string>();
            var associateHelper = new PasteDlg.AssociateProteinsHelper(_docCurrent);
            // Get a dictionary of peptides under consideration and the proteins they're associated with
            var peptides = new List<string>(Importer.RowReader.Lines.Count);
            peptides.AddRange(Importer.RowReader.Lines.Select(line => line.ParseDsvFields(Importer.Separator)).Select(fields => fields[Importer.RowReader.Indices.PeptideColumn]));
            var stripped = new Dictionary<string, string>();
            foreach (var pep in peptides)
            {
                if (!stripped.ContainsKey(pep))
                {
                    var peptideSequence = FastaSequence.StripModifications(pep);
                    stripped[pep] = FastaSequence.IsExSequence(peptideSequence) ? peptideSequence : null;
                }
            }
            var proteome = _docCurrent.Settings.PeptideSettings.BackgroundProteome;
            IDictionary<string, List<Protein>> dictSequenceProteins = null;
            using (var longWaitDlg = new LongWaitDlg() {Message = checkBoxAssociateProteins.Text})
            {
                longWaitDlg.PerformWork(this, 1000, longWaitBroker =>
                {
                    using (var proteomeDb = proteome.OpenProteomeDb(longWaitBroker.CancellationToken))
                    {
                        dictSequenceProteins = proteomeDb.GetDigestion().GetProteinsWithSequences(stripped.Values);
                    }
                });
            }

            // Go through each line of the import
            var associateHelper = new PasteDlg.AssociateProteinsHelper(_docCurrent);
            foreach (var line in Importer.RowReader.Lines)
            {
                var fields = line.ParseDsvFields(Importer.Separator);
                var seenPepSeq = new HashSet<string>(); // Peptide sequences we have already seen, for FilterMatchedPepSeq
                var action = associateHelper.DetermineAssociateAction(null, 
                    fields[Importer.RowReader.Indices.PeptideColumn], seenPepSeq, false, dictSequenceProteins);

                if (action == PasteDlg.AssociateProteinsHelper.AssociateAction.all_occurrences)
                {
                    // Add a separate transition for each protein on our list of matches
                    for (var j = 0; j < associateHelper.proteinNames.Count; j++)
                    {
                        AddAssociatedProtein(fields, lines, associateHelper.proteinNames[j], associateHelper.proteins[j], true);
                    }
                } else if (action == PasteDlg.AssociateProteinsHelper.AssociateAction.first_occurrence) {
                    // If we found at least one match, edit the line to include the name
                    AddAssociatedProtein(fields, lines, associateHelper.proteinNames[0], associateHelper.proteins[0], true);
                }
                else if (action == PasteDlg.AssociateProteinsHelper.AssociateAction.do_not_associate || 
                         action == PasteDlg.AssociateProteinsHelper.AssociateAction.throw_exception) 
                {
                    // If there are no matches or the sequence is invalid, add an empty string in place of the protein name
                    // so that spacing is consistent
                    AddAssociatedProtein(fields, lines, string.Empty, null, true);
                }
            }

            // If there are any peptides that don't meet the filter setting, have multiple matches or no matches,
            // show the user a dialog to resolve it. 
            if (associateHelper.numFiltered + associateHelper.numUnmatched + associateHelper.numMultipleMatches > 0 && triggerDialog)
            {
                var resolved = ResolveMatchedProteins(associateHelper.numMultipleMatches, 
                    associateHelper.numUnmatched, associateHelper.numFiltered, out canceled);
                if (canceled)
                {
                    checkBoxAssociateProteins.Checked = false;
                    return Importer.RowReader.Lines.ToArray();
                }
                if (resolved != null)
                {
                    return resolved;
                }
            }

            if (lines.Count == 0) // It's possible the user will set the filter such that there are no peptides remaining
            {
                canceled = true;
                checkBoxAssociateProteins.Checked = false;
                MessageDlg.Show(this, Resources.ImportTransitionListColumnSelectDlg_AssociateProteins_These_filters_cannot_be_applied_as_they_would_result_in_an_empty_transition_list_);
            }
            return lines.ToArray();
        }

        /// <summary>
        /// Edit the transition list to include the new protein. Also update variables for displaying the
        /// protein tip and importing the transition list.
        /// </summary>
        /// <param name="fields">The fields of the transition</param>
        /// <param name="lines">The new list we are creating</param>
        /// <param name="proteinName">Name of the protein to associate the peptide with</param>
        /// <param name="protein">Protein to associate the peptide with</param>
        /// <param name="updateSeqDict">True if we should update our dictionary of sequence protein name pairs</param>
        private void AddAssociatedProtein(string[] fields, List<string> lines, string proteinName, Protein protein, bool updateSeqDict)
        {
            var lineWithName = new string[fields.Length + 1];
            lineWithName[0] = proteinName;
            for (var j = 0; j < fields.Length; j++)
            {
                lineWithName[j + 1] = fields[j];
            }
            lines.Add(string.Join(Importer.Separator.ToString(), lineWithName));
            _proteinList.Add(protein);
            if (updateSeqDict)
            {
                // Add the protein name and matching sequence to a dictionary so that we import it to the correct node
                if (!_dictNameSeq.ContainsKey(proteinName))
                {
                    var fastaSeq = _docCurrent.Settings.PeptideSettings.BackgroundProteome.GetFastaSequence(proteinName);
                    if (fastaSeq != null)
                    {
                        _dictNameSeq.Add(proteinName, fastaSeq);
                    }
                }
            }
        }

        private void DisplayData()
        {
            // The pasted data will be stored as a data table
            var table = new DataTable("TransitionList");

            // Create the first row of columns
            var numColumns = Importer.RowReader.Lines[0].ParseDsvFields(Importer.Separator).Length;
            for (var i = 0; i < numColumns; i++)
                table.Columns.Add().DataType = typeof(string);

            // These dots are a placeholder for where the combo boxes will be
            var dots = Enumerable.Repeat(@"...", numColumns).ToArray();
            // The first row will actually be combo boxes, but we use dots as a placeholder because we can't put combo boxes in a data table
            table.Rows.Add(dots);

            // Add the data
            for (var index = 0; index < Math.Min(100, Importer.RowReader.Lines.Count); index++)
            {
                var line = Importer.RowReader.Lines[index];
                table.Rows.Add(line.ParseDsvFields(Importer.Separator));
            }

            // Don't bother displaying more than 100 lines of data
            if (Importer.RowReader.Lines.Count > 100)
                table.Rows.Add(dots);

            // Set the table as the source for the DataGridView that the user sees.
            dataGrid.DataSource = table;

            var headers = Importer.RowReader.Indices.Headers;
            if (headers != null && headers.Length > 0)
            {
                for (var i = 0; i < numColumns; i++)
                {
                    dataGrid.Columns[i].HeaderText = headers[i];
                    dataGrid.Columns[i].ToolTipText =
                        string.Format(Resources.ImportTransitionListColumnSelectDlg_DisplayData_This_column_is_labeled_with_the_header___0___in_the_input_text__Use_the_dropdown_control_to_assign_its_meaning_for_import_, headers[i]);
                }
                dataGrid.ColumnHeadersVisible = true;
            }
            else
            {
                for (var i = 0; i < numColumns; i++)
                {
                    // In this case when we don't have user provided headers, we still want localized headers that can be translated,
                    // this replaces the auto generated strings with a localized version
                    dataGrid.Columns[i].HeaderText = string.Format(Resources.ImportTransitionListColumnSelectDlg_DisplayData_Column__0_, (i+1));
                    dataGrid.Columns[i].ToolTipText =
                        string.Format(Resources.ImportTransitionListColumnSelectDlg_DisplayData_The_input_text_did_not_appear_to_contain_column_headers__Use_the_dropdown_control_to_assign_column_meanings_for_import_);
                }
            }

            dataGrid.ScrollBars = dataGrid.Rows.Count * dataGrid.Rows[0].Height + dataGrid.ColumnHeadersHeight + SystemInformation.HorizontalScrollBarHeight > dataGrid.Height 
                ? ScrollBars.Both : ScrollBars.Horizontal;
        }

        private void InitializeComboBoxes()
        {
            ComboBoxes = new List<LiteDropDownList>();
            for (var i = 0; i < Importer.RowReader.Lines[0].ParseDsvFields(Importer.Separator).Length; i++)
            {
                var combo = new LiteDropDownList();
                ComboBoxes.Add(combo);
                comboPanelInner.Controls.Add(combo);
                combo.BringToFront();
            }
        }

        SrmDocument.DOCUMENT_TYPE GetRadioType()
        {
            if (radioPeptide.Checked)
            {
                return SrmDocument.DOCUMENT_TYPE.proteomic;
            }
            else
            {
                return SrmDocument.DOCUMENT_TYPE.small_molecules;
            }
        }

        private void PopulateComboBoxes()
        {
            foreach (var comboBox in ComboBoxes)
            {
                UpdateCombo(comboBox);
                comboBox.SelectedIndexChanged += ComboChanged;
            }

            var columns = Importer.RowReader.Indices;

            // It's not unusual to see lines like "744.8 858.39 10 APR.
            // .y7.light 105 40" where protein, peptide, and label are all stuck together,
            // so that all three lay claim to a single column. In such cases, prioritize peptide.
            columns.PrioritizePeptideColumn();

            // Set the combo boxes using the detected columns first. They will be changed if the saved column positions are determined to be correct
            SetComboBoxText(columns.DecoyColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy);
            SetComboBoxText(columns.IrtColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT);
            SetComboBoxText(columns.LabelTypeColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type);
            SetComboBoxText(columns.LibraryColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Library_Intensity);
            SetComboBoxText(columns.MoleculeNameColumn, Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name);
            SetComboBoxText(columns.PeptideColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence);
            SetComboBoxText(columns.PrecursorColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z);
            SetComboBoxText(columns.ProductColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z);
            SetComboBoxText(columns.ProteinColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name);
            SetComboBoxText(columns.FragmentNameColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Fragment_Name);
            SetComboBoxText(columns.ProteinColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name);
            SetComboBoxText(columns.ProductChargeColumn, Resources.PasteDlg_UpdateMoleculeType_Product_Charge);
            SetComboBoxText(columns.MoleculeListNameColumn, Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name);
            SetComboBoxText(columns.ExplicitRetentionTimeColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time);
            SetComboBoxText(columns.ExplicitRetentionTimeWindowColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time_Window);
            SetComboBoxText(columns.ExplicitCollisionCrossSectionColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Cross_Section__sq_A_);
            SetComboBoxText(columns.MolecularFormulaColumn, Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula);
            SetComboBoxText(columns.ExplicitDeclusteringPotentialColumn, Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Explicit_Declustering_Potential);
            SetComboBoxText(columns.ProductNeutralLossColumn, Resources.PasteDlg_UpdateMoleculeType_Product_Neutral_Loss);
            SetComboBoxText(columns.ExplicitCollisionEnergyColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy);
            SetComboBoxText(columns.ProductNameColumn, Resources.PasteDlg_UpdateMoleculeType_Product_Name);
            SetComboBoxText(columns.ProductFormulaColumn, Resources.PasteDlg_UpdateMoleculeType_Product_Formula);
            SetComboBoxText(columns.PrecursorAdductColumn, Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct);
            SetComboBoxText(columns.ProductAdductColumn, Resources.PasteDlg_UpdateMoleculeType_Product_Adduct);
            SetComboBoxText(columns.CASColumn, @"CAS");
            SetComboBoxText(columns.SMILESColumn, @"SMILES");
            SetComboBoxText(columns.HMDBColumn, @"HMDB");
            SetComboBoxText(columns.KEGGColumn, @"KEGG");
            SetComboBoxText(columns.InChiColumn, @"InChi");
            SetComboBoxText(columns.InChiKeyColumn, @"InChiKey");
            SetComboBoxText(columns.PrecursorChargeColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge);
            var headers = Importer.RowReader.Indices.Headers;
            // Checks if the headers of the current list are the same as the headers of the previous list,
            // because if they are then we want to prioritize user headers
            bool sameHeaders = false;
            if (headers != null)
            {
                sameHeaders = headers.ToList().SequenceEqual(Settings.Default.CustomImportTransitionListHeaders);
            }
            // If there are items on our saved column list and the file does not contain headers (or the headers are the same as the previous file),
            // and the number of columns matches the saved column count then we try using the saved columns and apply them if they work
            int savedCount = Settings.Default.CustomImportTransitionListColumnTypesList.Count;
            if (savedCount != 0 && (headers == null || sameHeaders) && savedCount == Importer.RowReader.Lines[0].ParseDsvFields(Importer.Separator).Length)
            {
                UseSavedColumnsIfValid();
            }
        }

        // Applies the saved column positions if they seem to be correct
        private void UseSavedColumnsIfValid()
        {
            // Save the detected columns so if the saved columns are invalid we can revert back
            var detectedColumns = CurrentColumnPositions();

            // Accept invariant column names from settings as well as localized
            var headerTypesInvariant = ImportTransitionListColumnSelectDlg.GetKnownHeaderTypesInvariant();
            var settingsColumns = new List<string>();
            foreach (var col in Settings.Default.CustomImportTransitionListColumnTypesList)
            {
                var index = KnownHeaderTypes.IndexOf(s => s.Item1.Equals(col));
                if (index < 0)
                {
                    index = headerTypesInvariant.IndexOf(s => s.Item1.Equals(col));
                }
                settingsColumns.Add(KnownHeaderTypes[Math.Max(0, index)].Item1);
            }

            // Change the column positions to the saved columns so we can check if they produce valid transitions
            SetColumnPositions(settingsColumns);

            // Make a copy of the current transition list with 100 rows or the length of the current transition list (whichever is smaller)
            var input = new MassListInputs(Importer.RowReader.Lines.Take(100).ToArray());
            // Try importing that list to check for errors
            var insertionParams = new DocumentChecked();
            List<TransitionImportErrorInfo> testErrorList1 = null;
            insertionParams.Document = _docCurrent.ImportMassList(input, Importer, null,
                _insertPath, out insertionParams.SelectPath, out insertionParams.IrtPeptides,
                out insertionParams.LibrarySpectra, out testErrorList1, out insertionParams.PeptideGroups);

            var allError = ReferenceEquals(insertionParams.Document, _docCurrent);
            // If all transitions are errors, reset the columns to the detected columns
            if (allError)
            {
                SetColumnPositions(detectedColumns);
            }
        }

        /// <summary>
        /// Returns the current column positions as a list of strings
        /// </summary>
        public List<string> CurrentColumnPositions()
        {
            return ComboBoxes.Select(combo => combo.Text).ToList();
        }

        /// <summary>
        /// Returns the current column positions as a list of strings in invariant language
        /// </summary>
        public List<string> CurrentColumnPositionsInvariant()
        {
            return ColumnNamesInvariant(CurrentColumnPositions());
        }
        public static List<string> ColumnNamesInvariant(IEnumerable<string> local)
        {
            return local.Select(ColumnNameInvariant).ToList();
        }

        #region testing support

        /// <summary>
        /// For testing purposes only - normally this is done one at a time in UI rather than programatically
        /// </summary>
        public void SetSelectedColumnTypes(params string[] headers)
        {
            var index = 0;
            foreach (var header in headers)
            {
                if (!string.IsNullOrEmpty(header) && ComboBoxes.Count > index)
                {
                    ComboBoxes[index].SelectedIndex = ComboBoxes[1].FindStringExact(header);
                }
                index++;
            }
        }

        public static string ColumnNameInvariant(string local)
        {
            var headerTypesInvariant = ImportTransitionListColumnSelectDlg.GetKnownHeaderTypesInvariant();
            var headerTypes = ImportTransitionListColumnSelectDlg.GetKnownHeaderTypes();
            var index = headerTypes.IndexOf(s => s.Item1.Equals(local));
            if (index < 0)
            {
                index = headerTypesInvariant.IndexOf(s => s.Item1.Equals(local));
            }
            return headerTypesInvariant[Math.Max(0, index)].Item1;
        }
        
        public string[] SupportedColumnTypes => ComboBoxes[1].Items.Select(item=> item.ToString()).ToArray(); // Using 1th as 0th is sometimes generated with a restricted list as AssociateProteins
        public int[] ColumnTypeControlWidths => ComboBoxes.Select(cb => cb.Width).ToArray();

        #endregion

        /// <summary>
        /// Set the combo boxes and column indices given a list of column positions
        /// </summary>
        private void SetColumnPositions(IList<string> columnPositions)
        {
            for (int i = 0; i < columnPositions.Count; i++)
            {
                SetComboBoxText(i, columnPositions[i]);
            }
        }

        public void ResizeComboBoxes()
        {
            const int gridBorderWidth = 1;
            comboPanelOuter.Location = new Point(dataGrid.Location.X + gridBorderWidth,
                dataGrid.Location.Y + (dataGrid.ColumnHeadersVisible ? dataGrid.ColumnHeadersHeight : 1));


            // Only puts columns that we want to show in the layout
            var activeColumnIndexes = new List<int>();
            for (var i = 0; i < dataGrid.Columns.Count; i++)
            {
                if (!(!showIgnoredCols && Equals(ComboBoxes[i].Text,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column)))
                {
                    activeColumnIndexes.Add(i);
                }
            }

            var xOffset = 0;
            var height = 0;
            foreach (var i in activeColumnIndexes)
            {
                var column = dataGrid.Columns[i];
                var comboBox = ComboBoxes[i];
                comboBox.Location = new Point(xOffset, 0);
                comboBox.Width = column.Width + (i < (activeColumnIndexes.Count - 1) ? 1 : 0); // Overlap that 1 pixel border to get a 1 pixel divider between controls
                height = Math.Max(height, comboBox.Height);
                xOffset += column.Width;
            }
            
            var scrollBars = dataGrid.ScrollBars == ScrollBars.Both;
            var scrollWidth = SystemInformation.VerticalScrollBarWidth;
            var gridWidth = dataGrid.Size.Width - (scrollBars ? scrollWidth : 0) - (2 * gridBorderWidth);
            comboPanelOuter.Size = new Size(gridWidth, height);
            comboPanelInner.Size = new Size(xOffset, height);
            comboPanelInner.Location = new Point(-dataGrid.HorizontalScrollingOffset, 0);
        }

        /// <summary>
        /// Sets or hides the text of comboBoxes based on what mode they belong to and what mode we are in
        /// </summary>
        /// <param name="comboBoxIndex"></param>
        /// <param name="text"></param>
        private void SetBoxesForMode(int comboBoxIndex, string text)
        {
            foreach (var item in KnownHeaderTypes)
            {
                string name = item.Item1;
                SrmDocument.DOCUMENT_TYPE type = item.Item2;
                if (name.Equals(text))
                {
                    if (radioPeptide.Checked)
                    {
                        if (type != SrmDocument.DOCUMENT_TYPE.small_molecules)
                        {
                            SetComboBoxText(comboBoxIndex, text);
                        }
                        else
                        {
                            SetComboBoxText(comboBoxIndex, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column);
                        }
                    }
                    else
                    {
                        if (type != SrmDocument.DOCUMENT_TYPE.proteomic)
                        {
                            SetComboBoxText(comboBoxIndex, text);
                        }
                        else
                        {
                            SetComboBoxText(comboBoxIndex, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column);
                        }
                    }

                }

            }

        }

        // Sets the text of a combo box, with error checking
        private void SetComboBoxText(int comboBoxIndex, string text)
        {
            if (comboBoxIndex < 0 || comboBoxIndex >= ComboBoxes.Count)
                return;
            ComboBoxes[comboBoxIndex].Text = text;
            SetColumnColor(ComboBoxes[comboBoxIndex]);
        }

        // Ensures two combo boxes do not have the same value. Usually newSelectedIndex will be zero, because that is IgnoreColumn.
        private void CheckForComboBoxOverlap(int indexOfPreviousComboBox, int newSelectedIndex, int indexOfNewComboBox)
        {
            if (indexOfPreviousComboBox == indexOfNewComboBox || indexOfPreviousComboBox < 0 || indexOfPreviousComboBox >= ComboBoxes.Count)
                return;
            ComboBoxes[indexOfPreviousComboBox].SelectedIndex = newSelectedIndex;
        }

        private void SetColumnColor(LiteDropDownList comboBox)
        {
            var comboBoxIndex = ComboBoxes.IndexOf(comboBox);
            // Grey out any ignored column
            var foreColor = Equals(comboBox.Text, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column)
                ? SystemColors.GrayText
                : dataGrid.ForeColor;
            dataGrid.Columns[comboBoxIndex].DefaultCellStyle.ForeColor = foreColor;
        }

        private void OnColumnsShown(object sender, EventArgs e)
        {
            foreach (var comboBox in ComboBoxes)
            {
                SetColumnColor(comboBox);
            }

            // After initial display, see if we should enable AssociateProteins - which may cause a dialog to appear
            if (checkBoxAssociateProteins.Visible && !WindowShown)
            {
                // If there's a background proteome, use it
                checkBoxAssociateProteins.Checked = true;
            }

            WindowShown = true;
        }

        // Hides columns if the data is not being used and the appropriate setting is selected
        // This is intentionally not called whenever the user changes a column header to avoid essentially punishing
        // the user for making a mistake
        private void SetUnusedColumnVisibility(LiteDropDownList comboBox)
        {
            if (Equals(comboBox.Text, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column))
            {
                var comboBoxIndex = ComboBoxes.IndexOf(comboBox);
                dataGrid.Columns[comboBoxIndex].Visible = showIgnoredCols;
                comboBox.Visible = showIgnoredCols;
            }
        }

        private bool comboBoxChanged;
        private int[] previousIndices;

        // Callback for when a combo box is changed. We use it to update the index of the PeptideColumnIndices and preventing combo boxes from overlapping.
        private void ComboChanged(object sender, EventArgs e)  // CONSIDER(bspratt) no charge state columns? (Seems to be because Skyline infers these and is confused when given explicit values)
        {
            var comboBox = (LiteDropDownList) sender;
            var comboBoxIndex = ComboBoxes.IndexOf(comboBox);
            var columns = Importer.RowReader.Indices;
            comboBoxChanged = true;

            // Grey out any ignored column
            SetColumnColor(comboBox);

            if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy)
            {
                CheckForComboBoxOverlap(columns.DecoyColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.DecoyColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT)
            {
                CheckForComboBoxOverlap(columns.IrtColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.IrtColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type)
            {
                CheckForComboBoxOverlap(columns.LabelTypeColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.LabelTypeColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Library_Intensity)
            {
                CheckForComboBoxOverlap(columns.LibraryColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.LibraryColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name)
            {
                CheckForComboBoxOverlap(columns.MoleculeNameColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.MoleculeNameColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z)
            {
                CheckForComboBoxOverlap(columns.PrecursorColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.PrecursorColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z)
            {
                CheckForComboBoxOverlap(columns.ProductColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ProductColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name)
            {
                // When the user associates proteins and then tries to reassign the protein name column,
                // display a warning and the option to cancel.
                if (checkBoxAssociateProteins.Checked && Importer.RowReader.Indices.ProteinColumn == 0)
                {
                    var dlgResult = MessageDlg.Show(this,
                        Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Reassigning_the_Protein_Name_column_will_prevent_the_peptides_from_being_associated_with_proteins_from_the_background_proteome_,
                        true, MessageBoxButtons.OKCancel);
                    if (dlgResult == DialogResult.Cancel)
                    {
                        comboBox.SelectedIndex = previousIndices[comboBoxIndex];
                        return;
                    }
                }
                CheckForComboBoxOverlap(columns.ProteinColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ProteinColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Fragment_Name)
            {
                CheckForComboBoxOverlap(columns.FragmentNameColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.FragmentNameColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time)
            {
                CheckForComboBoxOverlap(columns.ExplicitRetentionTimeColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ExplicitRetentionTimeColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time_Window)
            {
                CheckForComboBoxOverlap(columns.ExplicitRetentionTimeWindowColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ExplicitRetentionTimeWindowColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy)
            {
                CheckForComboBoxOverlap(columns.ExplicitCollisionEnergyColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ExplicitCollisionEnergyColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Note)
            {
                CheckForComboBoxOverlap(columns.NoteColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.NoteColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_S_Lens)
            {
                CheckForComboBoxOverlap(columns.SLensColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.SLensColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Cone_Voltage)
            {
                CheckForComboBoxOverlap(columns.ConeVoltageColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ConeVoltageColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility)
            {
                CheckForComboBoxOverlap(columns.ExplicitIonMobilityColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ExplicitIonMobilityColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_Units)
            {
                CheckForComboBoxOverlap(columns.ExplicitIonMobilityUnitsColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ExplicitIonMobilityUnitsColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_High_Energy_Offset)
            {
                CheckForComboBoxOverlap(columns.ExplicitIonMobilityHighEnergyOffsetColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ExplicitIonMobilityHighEnergyOffsetColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Explicit_Compensation_Voltage)
            {
                CheckForComboBoxOverlap(columns.ExplicitCompensationVoltageColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ExplicitCompensationVoltageColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Explicit_Declustering_Potential)
            {
                CheckForComboBoxOverlap(columns.ExplicitDeclusteringPotentialColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ExplicitDeclusteringPotentialColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Cross_Section__sq_A_)
            {
                CheckForComboBoxOverlap(columns.ExplicitCollisionCrossSectionColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ExplicitCollisionCrossSectionColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Description)
            {
                CheckForComboBoxOverlap(columns.ProteinDescriptionColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ProteinDescriptionColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct)
            {
                CheckForComboBoxOverlap(columns.PrecursorAdductColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.PrecursorAdductColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge)
            {
                CheckForComboBoxOverlap(columns.PrecursorChargeColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.PrecursorChargeColumn = comboBoxIndex;                
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Product_Name)
            {
                CheckForComboBoxOverlap(columns.ProductNameColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ProductNameColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Product_Formula)
            {
                CheckForComboBoxOverlap(columns.ProductFormulaColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ProductFormulaColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Product_Neutral_Loss)
            {
                CheckForComboBoxOverlap(columns.ProductNeutralLossColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ProductNeutralLossColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Product_Adduct)
            {
                CheckForComboBoxOverlap(columns.ProductAdductColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ProductAdductColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.PasteDlg_UpdateMoleculeType_Product_Charge)
            {
                CheckForComboBoxOverlap(columns.ProductChargeColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ProductChargeColumn = comboBoxIndex;
            }
            else if (comboBox.Text == @"InChiKey")
            {
                CheckForComboBoxOverlap(columns.InChiKeyColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.InChiKeyColumn = comboBoxIndex;
            }
            else if (comboBox.Text == @"CAS")
            {
                CheckForComboBoxOverlap(columns.CASColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.CASColumn = comboBoxIndex;
            }
            else if (comboBox.Text == @"HMDB")
            {
                CheckForComboBoxOverlap(columns.HMDBColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.HMDBColumn = comboBoxIndex;
            }
            else if (comboBox.Text == @"InChi")
            {
                CheckForComboBoxOverlap(columns.InChiColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.InChiColumn = comboBoxIndex;
            }
            else if (comboBox.Text == @"SMILES")
            {
                CheckForComboBoxOverlap(columns.SMILESColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.SMILESColumn = comboBoxIndex;
            }
            else if (comboBox.Text == @"KEGG")
            {
                CheckForComboBoxOverlap(columns.KEGGColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.KEGGColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name)
            {
                CheckForComboBoxOverlap(columns.MoleculeListNameColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.MoleculeListNameColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula)
            {
                CheckForComboBoxOverlap(columns.MolecularFormulaColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.MolecularFormulaColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence)
            {
                CheckForComboBoxOverlap(columns.PeptideColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.PeptideColumn = comboBoxIndex;
            }
            else
            {
                // If any of the columns are set to the index being changed, we want to set their index to -1 now to get them out of the way
                foreach (var property in columns.GetType().GetProperties())
                {
                    if (property.Name.EndsWith(@"Column") && property.PropertyType == typeof(int))
                    {
                        if ((int)property.GetValue(columns, null) == comboBoxIndex)
                        {
                            property.SetValue(columns, -1);
                        }
                    }
                }
            }
            UpdateAssociateProteinsState();
            previousIndices[comboBoxIndex] = comboBox.SelectedIndex;
        }

        // Saves column positions between transition lists
        private void UpdateColumnsList()
        {
            Settings.Default.CustomImportTransitionListColumnTypesList = CurrentColumnPositionsInvariant(); // Save the strings in invariant language for best portability
        }

        // Saves a list of the current document's headers, if any exist, so that they can be compared to those of the next document
        private void UpdateHeadersList()
        {
            var headers = Importer.RowReader.Indices.Headers;
            if (headers != null && headers.Length > 0)
            {
                Settings.Default.CustomImportTransitionListHeaders = headers.ToList();
            }
        }
        private void DataGrid_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            ResizeComboBoxes();
        }
        private void DataGrid_ColumnHeadersHeightChanged(object sender, EventArgs e)
        {
            ResizeComboBoxes();
        }

        private void DataGrid_Scroll(object sender, ScrollEventArgs e)
        {
            comboPanelInner.Location = new Point(-dataGrid.HorizontalScrollingOffset, 0);
        }

        // If a combo box was changed, save the column indices and column count when the OK button is clicked
        private void ButtonOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {

            if (CheckForErrors(true)) // Look for errors, be silent on success
                return;

            Assume.IsNotNull(InsertionParams);
            DialogResult = DialogResult.OK;
        }

        private void ButtonCheckForErrors_Click(object sender, EventArgs e)
        {
            CheckForErrors();
        }

        public void CheckForErrors()
        {
            CheckForErrors(false);
        }

        private static List<string> MissingEssentialColumns { get; set; }
        // If an essential column is missing, add it to a list to display later
        private void CheckEssentialColumn(Tuple<int, string> column)
        {
            if (column.Item1 == -1)
            {
                MissingEssentialColumns.Add(column.Item2);
            }
        }

        private void CheckMoleculeColumns()
        {
            var columns = Importer.RowReader.Indices;
            if (columns.PrecursorAdductColumn == -1 && columns.PrecursorChargeColumn == -1)
            {
                MissingEssentialColumns.Add(Resources.ImportTransitionListColumnSelectDlg_CheckMoleculeColumns_Precursor_Adduct_and_or_Precursor_Charge);
            }

            if (columns.MolecularFormulaColumn == -1 && columns.PrecursorColumn == -1)
            {
                MissingEssentialColumns.Add(Resources.ImportTransitionListColumnSelectDlg_CheckMoleculeColumns_Molecular_Formula_and_or_Precursor_m_z);
            }
        }

        public class DocumentChecked
        {
            public SrmDocument Document;
            public IdentityPath SelectPath;
            public List<MeasuredRetentionTime> IrtPeptides;
            public List<SpectrumMzInfo> LibrarySpectra;
            public List<PeptideGroupDocNode> PeptideGroups;
            public List<string> ColumnHeaderList;
            public bool IsSmallMoleculeList;
        }

        public DocumentChecked InsertionParams { get; private set; }

        /// <summary>
        ///  After the mode is changed this makes sure we are only showing columns relevant to the current mode
        /// </summary>
        /// <param name="comboBox"></param>
        private void UpdateCombo(LiteDropDownList comboBox)
        {
            // Add appropriate headers to the comboBox range based on the user selected mode
            foreach (var item in KnownHeaderTypes)
            {
                string name = item.Item1;
                SrmDocument.DOCUMENT_TYPE type = item.Item2;
                if (type == SrmDocument.DOCUMENT_TYPE.mixed ||
                    (type == SrmDocument.DOCUMENT_TYPE.proteomic && radioPeptide.Checked) ||
                    (type == SrmDocument.DOCUMENT_TYPE.small_molecules && !radioPeptide.Checked))
                {
                    comboBox.Items.Add(name);
                    if (Equals(name, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column))
                    {
                        comboBox.SpecialItems.Add(name); // Special font for this
                    }
                }
            }
        }

        /// <summary>
        /// After we update the range of the comboBoxes we need to re-add appropriate headers
        /// </summary>
        private void RefreshComboText()
        {
            var columns = Importer.RowReader.Indices;
            
            // Set Peptide only columns
            SetBoxesForMode(columns.DecoyColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy);
            SetBoxesForMode(columns.IrtColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT);
            SetBoxesForMode(columns.LibraryColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Library_Intensity);
            SetBoxesForMode(columns.PeptideColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence);
            SetBoxesForMode(columns.ProteinColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name);
            SetBoxesForMode(columns.FragmentNameColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Fragment_Name);
            // Set Small Molecule only columns
            SetBoxesForMode(columns.PrecursorChargeColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge);
            SetBoxesForMode(columns.PrecursorAdductColumn, Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct);
            SetBoxesForMode(columns.ProductNameColumn, Resources.PasteDlg_UpdateMoleculeType_Product_Name);
            SetBoxesForMode(columns.ProductFormulaColumn, Resources.PasteDlg_UpdateMoleculeType_Product_Formula);
            SetBoxesForMode(columns.ProductNeutralLossColumn, Resources.PasteDlg_UpdateMoleculeType_Product_Neutral_Loss);
            SetBoxesForMode(columns.ProductAdductColumn, Resources.PasteDlg_UpdateMoleculeType_Product_Adduct);
            SetBoxesForMode(columns.ProductChargeColumn, Resources.PasteDlg_UpdateMoleculeType_Product_Charge);
            SetBoxesForMode(columns.MoleculeNameColumn, Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name);
            SetBoxesForMode(columns.InChiKeyColumn, @"InChiKey");
            SetBoxesForMode(columns.CASColumn, @"CAS");
            SetBoxesForMode(columns.HMDBColumn, @"HMDB");
            SetBoxesForMode(columns.InChiColumn, @"InChi");
            SetBoxesForMode(columns.SMILESColumn, @"SMILES");
            SetBoxesForMode(columns.KEGGColumn, @"KEGG");
            // Both columns
            SetBoxesForMode(columns.LabelTypeColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type);
            SetBoxesForMode(columns.PrecursorColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z);
            SetBoxesForMode(columns.ProductColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z);
            SetBoxesForMode(columns.ExplicitRetentionTimeColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time);
            SetBoxesForMode(columns.ExplicitRetentionTimeWindowColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time_Window);
            SetBoxesForMode(columns.ExplicitCollisionEnergyColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy);
            SetBoxesForMode(columns.NoteColumn, Resources.PasteDlg_UpdateMoleculeType_Note);
            SetBoxesForMode(columns.SLensColumn, Resources.PasteDlg_UpdateMoleculeType_S_Lens);
            SetBoxesForMode(columns.ConeVoltageColumn, Resources.PasteDlg_UpdateMoleculeType_Cone_Voltage);
            SetBoxesForMode(columns.ExplicitIonMobilityColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility);
            SetBoxesForMode(columns.ExplicitIonMobilityUnitsColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_Units);
            SetBoxesForMode(columns.ExplicitIonMobilityHighEnergyOffsetColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_High_Energy_Offset);
            SetBoxesForMode(columns.ExplicitCompensationVoltageColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Compensation_Voltage);
            SetBoxesForMode(columns.ExplicitDeclusteringPotentialColumn, Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Explicit_Declustering_Potential);
            SetBoxesForMode(columns.ExplicitCollisionCrossSectionColumn, Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Cross_Section__sq_A_);
            SetBoxesForMode(columns.ProteinDescriptionColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Description);
            if (radioPeptide.Checked)
            {
                // Set the column headers to what they were last time we were in peptide mode
                if (peptideColPositions != null)
                {
                    SetColumnPositions(peptideColPositions);
                }
            }
            else
            {
                // Set the column headers to what they were last time we were in small molecule mode
                if (smallMolColPositions != null)
                {
                    SetColumnPositions(smallMolColPositions);
                }
            }
        }

        /// Sets all empty columns to Ignore Column
        private void IgnoreAllEmptyCols()
        {
            foreach (var comboBox in ComboBoxes)
            {
                if (comboBox.Text == string.Empty)
                {
                    comboBox.SelectedIndex = 0;
                }
                
            }
        }

        /// <summary>
        /// This ensures the radio buttons are initially set to reflect the current mode Skyline is in
        /// </summary>
        private void InitializeRadioButtons()
        {
            if (Importer.InputType == SrmDocument.DOCUMENT_TYPE.proteomic)
            {
                radioPeptide.Checked = true;
            }
            else if (Importer.InputType == SrmDocument.DOCUMENT_TYPE.small_molecules)
            {
                radioMolecule.Checked = true;
            }
            else
            {
                radioPeptide.Checked = Settings.Default.TransitionListInsertPeptides;
            }
        }

        /// <summary>
        /// Parse the mass list text, then show a status dialog if:
        ///     errors are found, or
        ///     errors are not found and "silentSuccess" arg is false
        /// Shows a special error message and forces the user to alter their entry if the list is missing Precursor m/z, Product m/z or Peptide Sequence.
        /// Return false if no errors found.
        /// </summary>
        /// <param name="silentSuccess">If true, don't show the confirmation dialog when there are no errors</param>
        /// <returns>True if list contains any errors and user does not elect to ignore them</returns>
        private bool CheckForErrors(bool silentSuccess)
        {
            var insertionParams = new DocumentChecked();
            bool hasHeaders = Importer.RowReader.Indices.Headers != null;
            List<TransitionImportErrorInfo> testErrorList = null;
            var errorCheckCanceled = true;
            insertionParams.ColumnHeaderList = CurrentColumnPositions();
            using (var longWaitDlg = new LongWaitDlg { Text = Resources.ImportTransitionListColumnSelectDlg_CheckForErrors_Checking_for_errors___ })
            {
                longWaitDlg.PerformWork(this, 1000, progressMonitor =>
                {

                    var columns = Importer.RowReader.Indices;
                    MissingEssentialColumns = new List<string>();
                    if (radioPeptide.Checked)
                    {
                        CheckEssentialColumn(new Tuple<int, string>(columns.PeptideColumn,
                            Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence));
                        CheckEssentialColumn(new Tuple<int, string>(columns.PrecursorColumn,
                            Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z));
                        CheckEssentialColumn(new Tuple<int, string>(columns.ProductColumn,
                            Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z));
                    }
                    else
                    {
                        CheckMoleculeColumns();
                    }

                    insertionParams.Document = _docCurrent.ImportMassList(_inputs, Importer, progressMonitor,
                        _insertPath, out insertionParams.SelectPath, out insertionParams.IrtPeptides,
                        out insertionParams.LibrarySpectra, out testErrorList, out insertionParams.PeptideGroups, insertionParams.ColumnHeaderList, GetRadioType(), hasHeaders, 
                        checkBoxAssociateProteins.Checked && Importer.RowReader.Indices.ProteinColumn == 0 ? _dictNameSeq : new Dictionary<string, FastaSequence>());
                    errorCheckCanceled = progressMonitor.IsCanceled;
                });
            }
            var isErrorAll = ReferenceEquals(insertionParams.Document, _docCurrent);

            // If there is at least one valid transition, the document is being imported, and a combo box has been changed,
            // then save the column positions for the next transition list
            if (!isErrorAll && comboBoxChanged && silentSuccess)
            {
                UpdateHeadersList();
                UpdateColumnsList();
            }

            if (errorCheckCanceled)
            {
                return true; // User cancelled, we can't say that there are no errors
            }

            if (testErrorList != null && testErrorList.Any())
            {
                // There are errors, show them to user
                if (MissingEssentialColumns.Count != 0)
                {
                    // If the transition list is missing essential columns, tell the user in a 
                    // readable way
                    MessageDlg.Show(this, TextUtil.SpaceSeparate(Resources.ImportTransitionListErrorDlg_ImportTransitionListErrorDlg_This_transition_list_cannot_be_imported_as_it_does_not_provide_values_for_,
                        TextUtil.LineSeparate(MissingEssentialColumns)),
                        true); // Explicitly prohibit any "peptide"=>"molecule" translation in non-proteomic UI modes
                    return true; // There are errors
                }
                else
                {
                    using (var dlg = new ImportTransitionListErrorDlg(testErrorList, isErrorAll, silentSuccess))
                    {
                        if (dlg.ShowDialog(this) != DialogResult.OK)
                            return true; // There are errors, and user does not want to ignore them
                    }
                }
            }
            else if (!silentSuccess)
            {
                // No errors, confirm this to user
                MessageDlg.Show(this, Resources.PasteDlg_ShowNoErrors_No_errors);
            }
            
            insertionParams.ColumnHeaderList = CurrentColumnPositions();
            insertionParams.IsSmallMoleculeList = !radioPeptide.Checked;
            InsertionParams = insertionParams;
            return false; // No errors
        }

        /// <summary>
        /// A tip for displaying information about proteins that have been associated with peptides
        /// </summary>
        private class ProteinTipProvider : ITipProvider
        {
            private Protein _protein;

            public ProteinTipProvider(Protein protein)
            {
                _protein = protein;
            }
            public bool HasTip
            {
                get { return true; }
            }


            public Size RenderTip(Graphics g, Size sizeMax, bool draw)
            {
                var table = new TableDesc();

                using (var rt = new RenderTools())
                {
                    PeptideGroupTreeNode.GetX80Dimensions(g, rt, sizeMax, out var widthLine, out var heightLine, out var heightMax);
                    table.AddDetailRow(Resources.PeptideGroupTreeNode_RenderTip_Name, _protein.Name, rt); // Draw the name
                    PeptideGroupTreeNode.AddProteinMetadata(table, _protein.ProteinMetadata, rt, g); // Draw any metadata that we have
                    var tableSize = table.CalcDimensions(g);
                    widthLine = Math.Max(widthLine, tableSize.Width);
                    tableSize.Height += TableDesc.TABLE_SPACING;    // Spacing between details and sequence
                    var y = PeptideGroupTreeNode.RenderFastaSeq(g, draw, _protein.Sequence, tableSize.Height, heightLine, heightMax, 
                        new List<DocNode>(), new HashSet<DocNode>(), new Peptide(_protein.Sequence), rt, widthLine);
                    table.Draw(g);
                    return new Size((int)Math.Round(widthLine), (int)Math.Round(y + 2));
                }
            }
        }

        private void dataGrid_MouseMove(object sender, DataGridViewCellMouseEventArgs e)
        {

            // If the mouse is inside a protein name cell, display information about that protein
            if (e.RowIndex >= 0 && e.ColumnIndex == 0 && isAssociated)
            {
                // The row index is one off from the row in the transition list because the first row of the datagrid is covered by combo boxes
                var protein = _proteinList[e.RowIndex - 1];
                if (protein != null)
                {
                    var tipProvider = new ProteinTipProvider(protein);
                    var rect = dataGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                    _proteinTip.SetTipProvider(tipProvider, rect, e.Location);
                }
            }
            else
            {
                _proteinTip.HideTip();
            }
        }

        private void dataGrid_MouseLeave(object sender, EventArgs e)
        {
            _proteinTip.HideTip();
        }

        private void dataGrid_ColumnAdded(object sender, DataGridViewColumnEventArgs e)
        {
            ResizeComboBoxes();
        }

        private void form_Resize(object sender, EventArgs e)
        {
            ResizeComboBoxes();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            showIgnoredCols = CheckShowUnusedColumns.Checked;

            // Goes through each comboBox and sets their visibility if they are an Ignore Column
            foreach (var comboBox in ComboBoxes)
            {
                SetUnusedColumnVisibility(comboBox);
            }

            // Once we have all the settings we want to reorganize the comboBoxes so they line up with where we
            // put the data
            ResizeComboBoxes();
        }

        private void radioPeptide_CheckedChanged(object sender, EventArgs e)
        {
            if (radioPeptide.Checked)
            {
                smallMolColPositions = CurrentColumnPositions();
            }
            else
            {
                // If the user has associated proteins and wants to switch to molecule mode,
                // undo the association as it will not be useful in molecule mode
                if (checkBoxAssociateProteins.Checked)
                {
                    checkBoxAssociateProteins.Checked = false;
                }

                peptideColPositions = CurrentColumnPositions();
            }
            foreach (var comboBox in ComboBoxes)
            {
                comboBox.Items.Clear();
                UpdateCombo(comboBox);
            }
            checkBoxAssociateProteins.Visible = CheckboxVisible();
            RefreshComboText();
            IgnoreAllEmptyCols();
        }

        /// <summary>
        /// Undo the association of peptides to proteins in the background proteome
        /// </summary>
        private void ReverseAssociateProteins()
        {
            var oldPositions = CurrentColumnPositions();
            dataGrid.Columns[0].HeaderText = null;
            // Show the original transition list without the protein names we have added
            Importer.RowReader.Lines = _originalLines;
            Importer.RowReader.Indices.Headers = _originalHeaders;
            UpdateForm();
            oldPositions.RemoveAt(0);
            dataGrid.Columns[0].DefaultCellStyle.Font = new Font(dataGrid.DefaultCellStyle.Font, FontStyle.Regular);
            SetColumnPositions(oldPositions);
            // Make sure we set the "Protein Name" column back to it's original index
            if (_originalProteinIndex != -1 && _originalProteinIndex != 0)
            {
                ComboBoxes[_originalProteinIndex].SelectedIndex = KnownHeaderTypes.IndexOf(new Tuple<string, SrmDocument.DOCUMENT_TYPE>
                    (Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name, SrmDocument.DOCUMENT_TYPE.proteomic));
            }
        }

        private void UpdateForm()
        {
            InitializeComboBoxes();
            DisplayData();
            PopulateComboBoxes();
            ResizeComboBoxes();
        }

        private bool isAssociated; // True if the current transition list contains associated proteins

        private void checkBoxAssociateProteins_CheckedChanged(object sender, EventArgs e)
        {
            var oldPositions = CurrentColumnPositions();
            var proteinName = Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name;
            if (checkBoxAssociateProteins.Checked)
            {
                if (Importer.RowReader.Indices.PeptideColumn == -1)
                {
                    checkBoxAssociateProteins.Checked = false;
                    return;
                }
                _originalHeaders = Importer.RowReader.Indices.Headers;
                if (_originalProteinIndex == 0)
                {
                    _originalProteinIndex = Importer.RowReader.Indices.ProteinColumn;
                }

                // Create a new column with all the protein matches and update the form to display it
                var associatedLines = AssociateProteins(true, out var canceled);
                if (!canceled) // Only use the new list if the user did not cancel the dialog to resolve filtered peptides
                {
                    Importer.RowReader.Lines = associatedLines;
                    UpdateForm();

                    // Remove all options besides "Protein Name" from the new combo box
                    var proteinNameBox = ComboBoxes.First();
                    var removeList = proteinNameBox.Items.Where(item => item.ToString() !=
                        proteinName &&
                        item.ToString() !=
                        Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column).ToList();
                    foreach (var item in removeList)
                    {
                        proteinNameBox.Items.Remove(item);
                    }

                    // Change the text of the new column to italic
                    proteinNameBox.SelectedIndex = 0;
                    var firstColumn = dataGrid.Columns.GetFirstColumn(DataGridViewElementStates.Visible,
                        DataGridViewElementStates.None);
                    if (firstColumn != null)
                    {
                        firstColumn.DefaultCellStyle.Font = new Font(dataGrid.DefaultCellStyle.Font, FontStyle.Italic);
                        firstColumn.HeaderText = proteinName;
                    }

                    // If there was an existing protein name box, set its value to "Ignore Column"
                    if (_originalProteinIndex != -1)
                    {
                        oldPositions[_originalProteinIndex] =
                            Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column;
                    }

                    oldPositions.Insert(0, proteinName);
                    SetColumnPositions(oldPositions);
                    isAssociated = true;
                }
                else
                {
                    // If they canceled the resolve filtered peptides dialog, uncheck the associate proteins
                    // checkbox
                    checkBoxAssociateProteins.Checked = false;
                }

            }
            else if(isAssociated)
            {
                ReverseAssociateProteins();
                isAssociated = false;
            }
        }
    }
}  
