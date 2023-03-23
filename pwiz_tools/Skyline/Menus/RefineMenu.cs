/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Menus
{
    public partial class RefineMenu : SkylineControl
    {
        public RefineMenu(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
            DropDownItems = ImmutableList.ValueOf(refineToolStripMenuItem.DropDownItems.Cast<ToolStripItem>());
        }

        public IEnumerable<ToolStripItem> DropDownItems { get; }

        private void reintegrateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowReintegrateDialog();
        }

        public void ShowReintegrateDialog()
        {
            var documentOrig = DocumentUI;
            if (!documentOrig.Settings.HasResults)
            {
                MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_ShowReintegrateDialog_The_document_must_have_imported_results_);
                return;
            }
            if (documentOrig.MoleculeCount == 0)
            {
                MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_ShowReintegrateDialog_The_document_must_have_targets_in_order_to_reintegrate_chromatograms_);
                return;
            }
            if (!documentOrig.IsLoaded)
            {
                MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_ShowReintegrateDialog_The_document_must_be_fully_loaded_before_it_can_be_re_integrated_);
                return;
            }
            using (var dlg = new ReintegrateDlg(documentOrig))
            {
                if (dlg.ShowDialog(SkylineWindow) == DialogResult.Cancel)
                    return;
                ModifyDocument(Resources.SkylineWindow_ShowReintegrateDialog_Reintegrate_peaks, doc =>
                {
                    if (!ReferenceEquals(documentOrig, doc))
                        throw new InvalidDataException(
                            Resources.SkylineWindow_ShowReintegrateDialog_Unexpected_document_change_during_operation_);

                    return dlg.Document;
                }, dlg.FormSettings.EntryCreator.Create);
            }
        }

        private void generateDecoysMenuItem_Click(object sender, EventArgs e)
        {
            if (DocumentUI.PeptideCount == 0)
            {
                MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_generateDecoysMenuItem_Click_The_document_must_contain_peptides_to_generate_decoys_);
                return;
            }
            if (DocumentUI.PeptideGroups.Any(nodePeptideGroup => nodePeptideGroup.IsDecoy))
            {
                var message = TextUtil.LineSeparate(Resources.SkylineWindow_generateDecoysMenuItem_Click_This_operation_will_replace_the_existing_decoys,
                                                    Resources.SkylineWindow_generateDecoysMenuItem_Click_Are_you_sure_you_want_to_continue);
                // Warn about removing existing decoys
                var result = MultiButtonMsgDlg.Show(SkylineWindow, message, MessageBoxButtons.OKCancel);
                if (result == DialogResult.Cancel)
                    return;
            }

            ShowGenerateDecoysDlg();
        }

        public bool ShowGenerateDecoysDlg(IWin32Window owner = null)
        {
            using (var decoysDlg = new GenerateDecoysDlg(DocumentUI))
            {
                if (decoysDlg.ShowDialog(owner ?? SkylineWindow) == DialogResult.OK)
                {
                    var refinementSettings = new RefinementSettings { NumberOfDecoys = decoysDlg.NumDecoys, DecoysMethod = decoysDlg.DecoysMethod };
                    ModifyDocument(Resources.SkylineWindow_ShowGenerateDecoysDlg_Generate_Decoys, refinementSettings.GenerateDecoys,
                        docPair =>
                        {
                            var plural = refinementSettings.NumberOfDecoys > 1;
                            return AuditLogEntry.CreateSingleMessageEntry(new MessageInfo(
                                plural ? MessageType.added_peptide_decoys : MessageType.added_peptide_decoy,
                                DocumentUI.DocumentType,
                                refinementSettings.NumberOfDecoys, refinementSettings.DecoysMethod));
                        });

                    var nodePepGroup = DocumentUI.PeptideGroups.First(nodePeptideGroup => nodePeptideGroup.IsDecoy);
                    SelectedPath = DocumentUI.GetPathTo((int)SrmDocument.Level.MoleculeGroups, DocumentUI.FindNodeIndex(nodePepGroup.Id));
                    return true;
                }
            }
            return false;
        }

        private void compareModelsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowCompareModelsDlg();
        }

        public void ShowCompareModelsDlg()
        {
            var document = DocumentUI;
            if (!document.Settings.HasResults)
            {
                MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_ShowReintegrateDialog_The_document_must_have_imported_results_);
                return;
            }
            if (document.MoleculeCount == 0)
            {
                MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_ShowCompareModelsDlg_The_document_must_have_targets_in_order_to_compare_model_peak_picking_);
                return;
            }
            if (!document.IsLoaded)
            {
                MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_ShowCompareModelsDlg_The_document_must_be_fully_loaded_in_order_to_compare_model_peak_picking_);
                return;
            }
            var dlg = new ComparePeakPickingDlg(document);
            dlg.Show(SkylineWindow);
        }

        private void removeMissingResultsMenuItem_Click(object sender, EventArgs e)
        {
            RemoveMissingResults();
        }

        public void RemoveMissingResults()
        {
            var refinementSettings = new RefinementSettings { RemoveMissingResults = true };
            ModifyDocument(Resources.SkylineWindow_RemoveMissingResults_Remove_missing_results, refinementSettings.Refine,
                docPair => AuditLogEntry.CreateSimpleEntry(MessageType.removed_missing_results, docPair.NewDocumentType));
        }

        private void acceptProteinsMenuItem_Click(object sender, EventArgs e)
        {
            AcceptProteins();
        }

        public void AcceptProteins()
        {
            using (var dlg = new RefineProteinListDlg(DocumentUI))
            {
                if (dlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                {
                    var refinementSettings = new RefinementSettings
                    {
                        AcceptedProteins = dlg.AcceptedProteins,
                        AcceptProteinType = dlg.ProteinSpecType
                    };
                    ModifyDocument(Resources.SkylineWindow_acceptPeptidesMenuItem_Click_Accept_peptides, refinementSettings.Refine, dlg.FormSettings.EntryCreator.Create);
                }
            }
        }

        private AuditLogEntry CreateRemoveNodesEntry(SrmDocumentPair docPair, MessageType singular, MessageType plural)
        {
            var count = SkylineWindow.CountNodeDiff(docPair);
            return AuditLogEntry.CreateSimpleEntry(count == 1 ? singular : plural, docPair.NewDocumentType, count);
        }

        private void removeEmptyProteinsMenuItem_Click(object sender, EventArgs e)
        {
            var refinementSettings = new RefinementSettings { MinPeptidesPerProtein = 1 };
            ModifyDocument(Resources.SkylineWindow_removeEmptyProteinsMenuItem_Click_Remove_empty_proteins,
                refinementSettings.Refine, docPair => CreateRemoveNodesEntry(docPair, MessageType.removed_empty_protein, MessageType.removed_empty_proteins));
        }

        private void removeEmptyPeptidesMenuItem_Click(object sender, EventArgs e)
        {
            var refinementSettings = new RefinementSettings { MinPrecursorsPerPeptide = 1 };
            ModifyDocument(Resources.SkylineWindow_removeEmptyPeptidesMenuItem_Click_Remove_empty_peptides,
                refinementSettings.Refine, docPair => CreateRemoveNodesEntry(docPair, MessageType.removed_empty_peptide, MessageType.removed_empty_peptides));
        }

        private void removeDuplicatePeptidesMenuItem_Click(object sender, EventArgs e)
        {
            var refinementSettings = new RefinementSettings { RemoveDuplicatePeptides = true };
            ModifyDocument(Resources.SkylineWindow_removeDuplicatePeptidesMenuItem_Click_Remove_duplicate_peptides,
                refinementSettings.Refine, docPair => CreateRemoveNodesEntry(docPair, MessageType.removed_duplicate_peptide, MessageType.removed_duplicate_peptides));
        }

        private void removeRepeatedPeptidesMenuItem_Click(object sender, EventArgs e)
        {
            var refinementSettings = new RefinementSettings { RemoveRepeatedPeptides = true };
            ModifyDocument(Resources.SkylineWindow_removeRepeatedPeptidesMenuItem_Click_Remove_repeated_peptides,
                refinementSettings.Refine, docPair => CreateRemoveNodesEntry(docPair, MessageType.removed_repeated_peptide, MessageType.removed_repeated_peptides));
        }
        private void associateFASTAMenuItem_Click(object sender, EventArgs e)
        {
            ShowAssociateProteinsDlg();
        }

        public void ShowAssociateProteinsDlg(IWin32Window owner = null)
        {
            using (var associateProteinsDlg = new AssociateProteinsDlg(DocumentUI))
            {
                if (associateProteinsDlg.ShowDialog(owner ?? SkylineWindow) == DialogResult.OK)
                {
                    ModifyDocument(Resources.AssociateProteinsDlg_ApplyChanges_Associated_proteins,
                        current => associateProteinsDlg.DocumentFinal,
                        associateProteinsDlg.FormSettings.EntryCreator.Create);
                }
            }
        }

        private void renameProteinsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowRenameProteinsDlg();
        }

        public void ShowRenameProteinsDlg()
        {
            using (RenameProteinsDlg dlg = new RenameProteinsDlg(DocumentUI))
            {
                if (dlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                {
                    ModifyDocument(Resources.SkylineWindow_ShowRenameProteinsDlg_Rename_proteins,
                        doc => RenameProtein(doc, dlg), dlg.FormSettings.EntryCreator.Create);
                }
            }
        }

        private SrmDocument RenameProtein(SrmDocument doc, RenameProteinsDlg dlg)
        {
            foreach (var name in dlg.DictNameToName.Keys)
            {
                PeptideGroupDocNode node = Document.MoleculeGroups.FirstOrDefault(peptideGroup => Equals(name, peptideGroup.Name));
                if (node != null)
                {
                    var renameProtein = new RenameProteinsDlg.RenameProteins { CurrentName = name, NewName = dlg.DictNameToName[name] };
                    if (renameProtein.CurrentName != renameProtein.NewName)
                    {
                        doc = (SrmDocument)doc.ReplaceChild(node.ChangeName(renameProtein.NewName));
                    }
                }

            }
            return doc;
        }

        public void sortProteinsByNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PerformSort(Resources.SkylineWindow_sortProteinsMenuItem_Click_Sort_proteins_by_name, PeptideGroupDocNode.CompareNames, MessageType.sort_protein_name);
        }

        public void sortProteinsByAccessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PerformSort(Resources.SkylineWindow_sortProteinsByAccessionToolStripMenuItem_Click_Sort_proteins_by_accession, PeptideGroupDocNode.CompareAccessions, MessageType.sort_protein_accession);
        }

        public void sortProteinsByPreferredNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PerformSort(Resources.SkylineWindow_sortProteinsByPreferredNameToolStripMenuItem_Click_Sort_proteins_by_preferred_name, PeptideGroupDocNode.ComparePreferredNames, MessageType.sort_protein_preferred_name);
        }

        public void sortProteinsByGeneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PerformSort(Resources.SkylineWindow_sortProteinsByGeneToolStripMenuItem_Click_Sort_proteins_by_gene, PeptideGroupDocNode.CompareGenes, MessageType.sort_protein_gene);
        }

        private void PerformSort(string title, Comparison<PeptideGroupDocNode> comparison, MessageType type)
        {
            ModifyDocument(title, doc =>
            {
                var listIrt = new List<PeptideGroupDocNode>();
                var listProteins = new List<PeptideGroupDocNode>(doc.Children.Count);
                var listDecoy = new List<PeptideGroupDocNode>();
                foreach (var nodePepGroup in doc.MoleculeGroups)
                {
                    if (nodePepGroup.IsDecoy)
                    {
                        listDecoy.Add(nodePepGroup);
                    }
                    else if (nodePepGroup.PeptideCount > 0 && nodePepGroup.Peptides.All(nodePep => nodePep.GlobalStandardType == StandardType.IRT))
                    {
                        listIrt.Add(nodePepGroup);
                    }
                    else
                    {
                        listProteins.Add(nodePepGroup);
                    }
                }
                listIrt.Sort(comparison);
                listProteins.Sort(comparison);
                listDecoy.Sort(comparison);
                return (SrmDocument)doc.ChangeChildrenChecked(listIrt.Concat(listProteins).Concat(listDecoy).ToArray());
            }, docPair => AuditLogEntry.CreateSingleMessageEntry(new MessageInfo(type, docPair.NewDocumentType)));
        }

        private void acceptPeptidesMenuItem_Click(object sender, EventArgs e)
        {
            AcceptPeptides();
        }

        public void AcceptPeptides()
        {
            using (var dlg = new RefineListDlg(DocumentUI))
            {
                if (dlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                {
                    var refinementSettings = new RefinementSettings
                    {
                        AcceptedPeptides = dlg.AcceptedPeptides,
                        AcceptModified = dlg.MatchModified
                    };
                    if (dlg.RemoveEmptyProteins)
                        refinementSettings.MinPeptidesPerProtein = 1;

                    ModifyDocument(Resources.SkylineWindow_acceptPeptidesMenuItem_Click_Accept_peptides, refinementSettings.Refine, dlg.FormSettings.EntryCreator.Create);
                }
            }
        }

        private void permuteIsotopeModificationsMenuItem_Click(object sender, EventArgs e)
        {
            ShowPermuteIsotopeModificationsDlg();
        }

        public void ShowPermuteIsotopeModificationsDlg()
        {
            using (var dlg = new PermuteIsotopeModificationsDlg(SkylineWindow))
            {
                dlg.ShowDialog(SkylineWindow);
            }
        }

        private void refineMenuItem_Click(object sender, EventArgs e)
        {
            ShowRefineDlg();
        }

        public void ShowRefineDlg()
        {
            using (var refineDlg = new RefineDlg(SkylineWindow))
            {
                if (refineDlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                {
                    ModifyDocument(Resources.SkylineWindow_ShowRefineDlg_Refine,
                        doc =>
                        {
                            using (var longWaitDlg = new LongWaitDlg(SkylineWindow))
                            {
                                longWaitDlg.Message = Resources.SkylineWindow_ShowRefineDlg_Refining_document;
                                longWaitDlg.PerformWork(refineDlg, 1000, progressMonitor =>
                                {
                                    var srmSettingsChangeMonitor =
                                        new SrmSettingsChangeMonitor(progressMonitor, Resources.SkylineWindow_ShowRefineDlg_Refining_document, SkylineWindow, doc);

                                    doc = refineDlg.RefinementSettings.Refine(doc, srmSettingsChangeMonitor);
                                });
                            }

                            return doc;
                        }, refineDlg.FormSettings.EntryCreator.Create);
                }
            }
        }

        private void optimizePeptideTransitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var optimizeTransitionsForm = FormUtil.OpenForms.OfType<OptimizeTransitionsForm>().FirstOrDefault();
            if (optimizeTransitionsForm != null)
            {
                optimizeTransitionsForm.Activate();
                return;
            }

            optimizeTransitionsForm = new OptimizeTransitionsForm(SkylineWindow);
            optimizeTransitionsForm.Show(SkylineWindow);
        }

        private void optimizedDocumentTransitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var optimizeTransitionsDlg = FormUtil.OpenForms.OfType<OptimizeDocumentTransitionsForm>().FirstOrDefault();
            if (optimizeTransitionsDlg != null)
            {
                optimizeTransitionsDlg.Activate();
                return;
            }
            optimizeTransitionsDlg = new OptimizeDocumentTransitionsForm(SkylineWindow);
            optimizeTransitionsDlg.Show(SkylineWindow);
        }

        private void optimizeTransitionsMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowOptimizeTransitionsForm();
        }
    }
}
