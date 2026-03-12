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
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Menus
{
    public partial class TreeNodeContextMenu : SkylineControl
    {
        public TreeNodeContextMenu(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
        }

        public new ContextMenuStrip ContextMenuStrip => contextMenuTreeNode;
        public ToolStripMenuItem SetStandardTypeContextMenuItem => setStandardTypeContextMenuItem;
        public ToolStripMenuItem IrtStandardContextMenuItem => irtStandardContextMenuItem;

        public void ShowTreeNodeContextMenu(System.Drawing.Point pt)
        {
            SkylineWindow.SequenceTree.HideEffects();
            var settings = DocumentUI.Settings;
            // Show the ratios sub-menu when there are results and a choice of
            // internal standard types.
            ratiosContextMenuItem.Visible =
                settings.HasResults &&
                    (settings.HasGlobalStandardArea ||
                    (settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count > 1 &&
                     settings.PeptideSettings.Modifications.HasHeavyModifications));
            contextMenuTreeNode.Show(SkylineWindow.SequenceTree, pt);
        }

        private void contextMenuTreeNode_Opening(object sender, CancelEventArgs e)
        {
            var treeNode = SequenceTree.SelectedNode as TreeNodeMS;
            bool enabled = (SequenceTree.SelectedNode is IClipboardDataProvider && treeNode != null
                && treeNode.IsInSelection);
            copyContextMenuItem.Enabled = enabled;
            cutContextMenuItem.Enabled = enabled;
            deleteContextMenuItem.Enabled = enabled;
            if (SequenceTree.SelectedNodes.Count > 0)
            {
                expandSelectionContextMenuItem.Enabled = true;
                expandSelectionContextMenuItem.Visible = true;
            }
            else
            {
                expandSelectionContextMenuItem.Enabled = false;
                expandSelectionContextMenuItem.Visible = false;
            }
            if (Settings.Default.UIMode == UiModes.PROTEOMIC)
            {
                expandSelectionProteinsContextMenuItem.Text = SeqNodeResources.PeptideGroupTreeNode_Heading_Protein;
                expandSelectionPeptidesContextMenuItem.Text = PeptideDocNode.TITLE;
            }
            else
            {
                expandSelectionProteinsContextMenuItem.Text = SeqNodeResources.PeptideGroupTreeNode_Heading_Molecule_List;
                expandSelectionPeptidesContextMenuItem.Text = PeptideDocNode.TITLE_MOLECULE;
            }
            pickChildrenContextMenuItem.Enabled = SequenceTree.CanPickChildren(SequenceTree.SelectedNode) && enabled;
            editNoteContextMenuItem.Enabled = (SequenceTree.SelectedNode is SrmTreeNode && enabled);
            removePeakContextMenuItem.Visible = (SequenceTree.SelectedNode is TransitionTreeNode && enabled);
            bool enabledModify = SequenceTree.GetNodeOfType<PeptideTreeNode>() != null;
            var transitionTreeNode = SequenceTree.SelectedNode as TransitionTreeNode;
            if (transitionTreeNode != null && transitionTreeNode.DocNode.Transition.IsPrecursor() && transitionTreeNode.DocNode.Transition.IsCustom())
                enabledModify = false; // Don't offer to modify generated custom precursor nodes
            modifyPeptideContextMenuItem.Visible = enabledModify && enabled;
            setStandardTypeContextMenuItem.Visible = (SkylineWindow.HasSelectedTargetPeptides() && enabled);
            // Custom molecule support
            var nodePepGroupTree = SequenceTree.SelectedNode as PeptideGroupTreeNode;
            var nodePepTree = SequenceTree.SelectedNode as PeptideTreeNode;
            addMoleculeContextMenuItem.Visible = enabled && nodePepGroupTree != null &&
                (nodePepGroupTree.DocNode.IsEmpty || nodePepGroupTree.DocNode.IsNonProteomic);
            addSmallMoleculePrecursorContextMenuItem.Visible = enabledModify && nodePepTree != null && !nodePepTree.DocNode.IsProteomic;
            var nodeTranGroupTree = SequenceTree.SelectedNode as TransitionGroupTreeNode;
            addTransitionMoleculeContextMenuItem.Visible = enabled && nodeTranGroupTree != null &&
                nodeTranGroupTree.PepNode.Peptide.IsCustomMolecule;
            editSpectrumFilterContextMenuItem.Visible = SequenceTree.SelectedPaths
                .SelectMany(path => DocumentUI.EnumeratePathsAtLevel(path, SrmDocument.Level.TransitionGroups)).Any();
            var selectedQuantitativeValues = SkylineWindow.SelectedQuantitativeValues();
            if (selectedQuantitativeValues.Length == 0)
            {
                toggleQuantitativeContextMenuItem.Visible = false;
                markTransitionsQuantitativeContextMenuItem.Visible = false;
            }
            else if (selectedQuantitativeValues.Length == 2)
            {
                toggleQuantitativeContextMenuItem.Visible = false;
                markTransitionsQuantitativeContextMenuItem.Visible = true;
            }
            else
            {
                markTransitionsQuantitativeContextMenuItem.Visible = false;

                if (selectedQuantitativeValues[0])
                {
                    toggleQuantitativeContextMenuItem.Checked = true;
                    toggleQuantitativeContextMenuItem.Visible
                        = SequenceTree.SelectedNodes.All(node => node is TransitionTreeNode);
                }
                else
                {
                    toggleQuantitativeContextMenuItem.Checked = false;
                    toggleQuantitativeContextMenuItem.Visible = true;
                }
            }
        }

        private void cutContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.Cut();

        private void copyContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.Copy();

        private void pasteContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.Paste();

        private void deleteContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.EditDelete();

        private void editNoteContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.EditNote();

        private void pickChildrenContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.ShowPickChildrenInternal(true);

        private void expandSelectionNoneContextMenuItem_Click(object sender, EventArgs e)
            => SequenceTree.ExpandSelectionBulk(typeof(SrmTreeNodeParent));

        private void expandSelectionProteinsContextMenuItem_Click(object sender, EventArgs e)
            => SequenceTree.ExpandSelectionBulk(typeof(PeptideGroupTreeNode));

        private void expandSelectionPeptidesContextMenuItem_Click(object sender, EventArgs e)
            => SequenceTree.ExpandSelectionBulk(typeof(PeptideTreeNode));

        private void expandSelectionPrecursorsContextMenuItem_Click(object sender, EventArgs e)
            => SequenceTree.ExpandSelectionBulk(typeof(TransitionGroupTreeNode));

        private void removePeakContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.EditMenu.RemovePeak(true);

        private void modifyPeptideMenuItem_Click(object sender, EventArgs e)
        {
            var nodeTranGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
            var nodeTranTree = SequenceTree.GetNodeOfType<TransitionTreeNode>();
            if (nodeTranTree == null && nodeTranGroupTree != null && nodeTranGroupTree.DocNode.TransitionGroup.IsCustomIon)
            {
                SkylineWindow.ModifySmallMoleculeTransitionGroup();
            }
            else if (nodeTranTree != null && nodeTranTree.DocNode.Transition.IsNonPrecursorNonReporterCustomIon())
            {
                SkylineWindow.ModifyTransition(nodeTranTree);
            }
            else
            {
                SkylineWindow.ModifyPeptide();
            }
        }

        private void setStandardTypeContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var selectedPeptides = SequenceTree.SelectedDocNodes
                .OfType<PeptideDocNode>().ToArray();
            var selectedStandardTypes = selectedPeptides.Select(peptide => peptide.GlobalStandardType)
                .Distinct().ToArray();
            foreach (var menuItemStandardType in GetStandardTypeMenuItems())
            {
                var toolStripMenuItem = menuItemStandardType.Key;
                var standardType = menuItemStandardType.Value;
                if (standardType == StandardType.IRT)
                {
                    // Only show iRT menu item when there is an iRT calculator
                    var rtRegression = Document.Settings.PeptideSettings.Prediction.RetentionTime;
                    toolStripMenuItem.Visible = rtRegression == null || !(rtRegression.Calculator is RCalcIrt);
                    toolStripMenuItem.Enabled = selectedStandardTypes.Contains(StandardType.IRT);
                }
                else
                {
                    toolStripMenuItem.Enabled = selectedPeptides.Length >= 1 &&
                                                !selectedStandardTypes.Contains(StandardType.IRT);
                }
                toolStripMenuItem.Checked = selectedStandardTypes.Length == 1 &&
                                            selectedStandardTypes[0] == standardType;
            }
        }

        private IDictionary<ToolStripMenuItem, StandardType> GetStandardTypeMenuItems()
        {
            var dict = new Dictionary<ToolStripMenuItem, StandardType>
            {
                {noStandardContextMenuItem, null},
                {normStandardContextMenuItem, StandardType.GLOBAL_STANDARD},
                {surrogateStandardContextMenuItem, StandardType.SURROGATE_STANDARD},
                {qcStandardContextMenuItem, StandardType.QC},
                {irtStandardContextMenuItem, StandardType.IRT},
            };
            foreach (var entry in SkylineWindow.EditMenu.GetStandardTypeMenuItems())
            {
                dict.Add(entry.Key, entry.Value);
            }
            return dict;
        }

        private void noStandardMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetStandardType(null);

        private void normStandardMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetStandardType(StandardType.GLOBAL_STANDARD);

        private void surrogateStandardMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetStandardType(StandardType.SURROGATE_STANDARD);

        private void qcStandardMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetStandardType(PeptideDocNode.STANDARD_TYPE_QC);

        private void irtStandardContextMenuItem_Click(object sender, EventArgs e)
        {
            MessageDlg.Show(SkylineWindow, TextUtil.LineSeparate(SkylineResources.SkylineWindow_irtStandardContextMenuItem_Click_The_standard_peptides_for_an_iRT_calculator_can_only_be_set_in_the_iRT_calculator_editor_,
                SkylineResources.SkylineWindow_irtStandardContextMenuItem_Click_In_the_Peptide_Settings___Prediction_tab__click_the_calculator_button_to_edit_the_current_iRT_calculator_));
        }

        private void ratiosContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = ratiosContextMenuItem;
            menu.DropDownItems.Clear();
            var standardTypes = DocumentUI.Settings.PeptideSettings.Modifications.RatioInternalStandardTypes;
            for (int i = 0; i < standardTypes.Count; i++)
            {
                SelectRatioHandler.Create(SkylineWindow, menu, standardTypes[i].Title, NormalizeOption.FromIsotopeLabelType(standardTypes[i]));
            }
            if (DocumentUI.Settings.HasGlobalStandardArea)
            {
                SelectRatioHandler.Create(SkylineWindow, menu, ratiosToGlobalStandardsMenuItem.Text,
                    NormalizeOption.FromNormalizationMethod(NormalizationMethod.GLOBAL_STANDARDS));
            }
        }

        private class SelectRatioHandler
        {
            private readonly SkylineWindow _skyline;
            private readonly NormalizeOption _ratioIndex;

            public SelectRatioHandler(SkylineWindow skyline, NormalizeOption ratioIndex)
            {
                _skyline = skyline;
                _ratioIndex = ratioIndex;
            }

            public void ToolStripMenuItemClick(object sender, EventArgs e)
            {
                _skyline.AreaNormalizeOption = _ratioIndex;
            }

            public static void Create(SkylineWindow skylineWindow, ToolStripMenuItem menu, string text, NormalizeOption i)
            {
                var handler = new SelectRatioHandler(skylineWindow, i);
                var item = new ToolStripMenuItem(text, null, handler.ToolStripMenuItemClick)
                    { Checked = skylineWindow.SequenceTree.NormalizeOption == i };
                menu.DropDownItems.Add(item);
            }
        }

        private void replicatesTreeContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ReplicateDisplay replicate = SequenceTree.ShowReplicate;
            singleReplicateTreeContextMenuItem.Checked = (replicate == ReplicateDisplay.single);
            bestReplicateTreeContextMenuItem.Checked = (replicate == ReplicateDisplay.best);
        }

        private void singleReplicateTreeContextMenuItem_Click(object sender, EventArgs e)
        {
            SequenceTree.ShowReplicate = ReplicateDisplay.single;
        }

        private void bestReplicateTreeContextMenuItem_Click(object sender, EventArgs e)
        {
            SequenceTree.ShowReplicate = ReplicateDisplay.best;

            // Make sure the best result index is active for the current peptide.
            var nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            if (nodePepTree != null)
            {
                int iBest = nodePepTree.DocNode.BestResult;
                if (iBest != -1)
                    SelectedResultsIndex = iBest;
            }
        }

        private void addMoleculeContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.AddSmallMolecule();

        private void addSmallMoleculePrecursorContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.AddSmallMolecule();

        private void addTransitionMoleculeContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.AddSmallMolecule();

        private void toggleQuantitativeContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.MarkQuantitative(!toggleQuantitativeContextMenuItem.Checked);

        private void markTransitionsQuantitativeContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.MarkQuantitative(true);

        private void editSpectrumFilterContextMenuItem_Click(object sender, EventArgs args)
            => SkylineWindow.EditMenu.EditSpectrumFilter();
    }
}
