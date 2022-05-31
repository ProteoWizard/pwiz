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
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.SeqNode
{
    public class TransitionGroupTreeNode : SrmTreeNodeParent
    {
        /// <summary>
        /// Precursor
        /// </summary>
        public static string TITLE
        {
            get { return Resources.TransitionGroupTreeNode_Title; }
        }

        public static TransitionGroupTreeNode CreateInstance(SequenceTree tree, DocNode nodeDoc)
        {
            Debug.Assert(nodeDoc is TransitionGroupDocNode);
            var nodeTree = new TransitionGroupTreeNode(tree, (TransitionGroupDocNode) nodeDoc);

            if (tree.ExpandPrecursors)
                nodeTree.Expand();

            return nodeTree;
        }

        public TransitionGroupTreeNode(SequenceTree tree, TransitionGroupDocNode nodeGroup)
            : base(tree, nodeGroup)
        {
        }

        public Target ModifiedSequence
        {
            get { return GetModifiedSequence(PepNode, DocNode, SequenceTree.Document.Settings); }
        }

        public TransitionGroupDocNode DocNode => (TransitionGroupDocNode)Model;
        public PeptideDocNode PepNode => ((PeptideTreeNode)Parent)?.DocNode;
        public PeptideGroupDocNode PepGroupNode => ((PeptideGroupTreeNode)Parent?.Parent)?.DocNode;

        public override string Heading
        {
            get { return Resources.TransitionGroupTreeNode_Title; }
        }

        public override string ChildHeading
        {
            get { return TransitionTreeNode.TITLES; }
        }

        public override string ChildUndoHeading
        {
            get { return ChildHeading.ToLower(); }
        }

        protected override void OnModelChanged()
        {
            int typeImageIndex = TypeImageIndex;
            if (typeImageIndex != ImageIndex)
                ImageIndex = SelectedImageIndex = typeImageIndex;
            int peakImageIndex = PeakImageIndex;
// ReSharper disable RedundantCheckBeforeAssignment
            if (peakImageIndex != StateImageIndex)
                StateImageIndex = peakImageIndex;
// ReSharper restore RedundantCheckBeforeAssignment
            string label = DisplayText(DocNode, SequenceTree.GetDisplaySettings(PepNode));
            if (!Equals(label, Text))
                Text = label;

            // Make sure children are up to date
            OnUpdateChildren(SequenceTree.ExpandPrecursors);
            // Refresh the text on the TransitionTreeNodes.
            foreach (var child in Nodes.OfType<TransitionTreeNode>())
            {
                child.Model = child.Model;
            }
        }

        public int TypeImageIndex
        {
            get { return GetTypeImageIndex(DocNode); }            
        }

        public static Image GetTypeImage(TransitionGroupDocNode nodeGroup, SequenceTree sequenceTree)
        {
            return sequenceTree.ImageList.Images[GetTypeImageIndex(nodeGroup)];
        }

        private static int GetTypeImageIndex(TransitionGroupDocNode nodeGroup)
        {
            if (nodeGroup.IsDecoy)
            {
                return (int) (nodeGroup.HasLibInfo
                                  ? SequenceTree.ImageId.tran_group_lib_decoy
                                  : SequenceTree.ImageId.tran_group_decoy);
            }
            return (int) (nodeGroup.HasLibInfo
                              ? SequenceTree.ImageId.tran_group_lib
                              : SequenceTree.ImageId.tran_group);
        }

        public int PeakImageIndex
        {
            get
            {
                return GetPeakImageIndex(DocNode, PepNode, SequenceTree);
            }
        }

        public static Image GetPeakImage(TransitionGroupDocNode nodeGroup,
            PeptideDocNode nodeParent, SequenceTree sequenceTree)
        {
            int imageIndex = GetPeakImageIndex(nodeGroup, nodeParent, sequenceTree);
            return (imageIndex != -1 ? sequenceTree.StateImageList.Images[imageIndex] : null);
        }

        private static int GetPeakImageIndex(TransitionGroupDocNode nodeGroup,
            PeptideDocNode nodeParent, SequenceTree sequenceTree)
        {
            var settings = sequenceTree.Document.Settings;
            if (!settings.HasResults)
                return -1;

            int index = sequenceTree.GetDisplayResultsIndex(nodeParent);

            float? ratio = (nodeGroup.HasResults ? nodeGroup.GetPeakCountRatio(index) : null);
            if (ratio == null)
                return (int)SequenceTree.StateImageId.peak_blank;
            if (ratio < 0.5)
                return (int)SequenceTree.StateImageId.no_peak;
            if (ratio < 1.0)
                return (int)SequenceTree.StateImageId.keep;

            return (int)SequenceTree.StateImageId.peak;
        }
        
        public static string DisplayText(TransitionGroupDocNode nodeGroup, DisplaySettings settings)
        {
            return GetLabel(nodeGroup.TransitionGroup, nodeGroup.PrecursorMz,
                GetResultsText(settings, nodeGroup));
        }

        private const string DOTP_FORMAT = "0.##";
        private const string CS_SEPARATOR = ", ";

        public static string GetResultsText(DisplaySettings displaySettings, TransitionGroupDocNode nodeGroup)
        {
            float? libraryProduct = nodeGroup.GetLibraryDotProduct(displaySettings.ResultsIndex);
            float? isotopeProduct = nodeGroup.GetIsotopeDotProduct(displaySettings.ResultsIndex);
            RatioValue ratio = null;
            if (displaySettings.NormalizationMethod is NormalizationMethod.RatioToLabel ratioToLabel)
            {
                ratio = displaySettings.NormalizedValueCalculator.GetTransitionGroupRatioValue(ratioToLabel,
                    displaySettings.NodePep, nodeGroup, nodeGroup.GetChromInfoEntry(displaySettings.ResultsIndex));
            }
            else if (NormalizationMethod.GLOBAL_STANDARDS.Equals(displaySettings.NormalizationMethod))
            {
                var ratioToGlobalStandards = displaySettings.NormalizedValueCalculator.GetTransitionGroupValue(
                    displaySettings.NormalizationMethod, displaySettings.NodePep, nodeGroup,
                    nodeGroup.GetChromInfoEntry(displaySettings.ResultsIndex));
                if (ratioToGlobalStandards.HasValue)
                {
                    ratio = new RatioValue(ratioToGlobalStandards.Value);
                }
            }
            if (null == ratio && !isotopeProduct.HasValue && !libraryProduct.HasValue)
                return string.Empty;
            StringBuilder sb = new StringBuilder(@" (");
            int len = sb.Length;
            if (isotopeProduct.HasValue)
                sb.Append(string.Format(@"idotp {0}", isotopeProduct.Value.ToString(DOTP_FORMAT)));
            if (libraryProduct.HasValue)
            {
                if (sb.Length > len)
                    sb.Append(CS_SEPARATOR);
                sb.Append(string.Format(@"dotp {0}", libraryProduct.Value.ToString(DOTP_FORMAT)));
            }
            if (ratio != null)
            {
                if (sb.Length > len)
                    sb.Append(CS_SEPARATOR);
                sb.Append(FormatRatioValue(ratio));
            }
            sb.Append(@")");
            return sb.ToString();
        }

        public static string FormatRatioValue(RatioValue ratio)
        {
            StringBuilder sb = new StringBuilder();
            if (ratio.HasDotProduct)
            {
                sb.Append(string.Format(@"rdotp {0}", ratio.DotProduct.ToString(DOTP_FORMAT)));
                sb.Append(CS_SEPARATOR);
            }

            sb.Append(string.Format(Resources.TransitionGroupTreeNode_GetResultsText_total_ratio__0__,
                MathEx.RoundAboveZero(ratio.Ratio, 2, 4)));
            return sb.ToString();
        }

        protected override void UpdateChildren(bool materialize)
        {
            UpdateNodes(SequenceTree, Nodes, DocNode.Children,
                                            materialize, TransitionTreeNode.CreateInstance);
        }

        public static string GetLabel(TransitionGroup tranGroup, double precursorMz,
            string resultsText)
        {
            return string.Format(@"{0}{1}{2}{3}", GetMzLabel(tranGroup, precursorMz),
                                 Transition.GetChargeIndicator(tranGroup.PrecursorAdduct),
                                 tranGroup.LabelTypeText, resultsText);
        }

        private static string GetMzLabel(TransitionGroup tranGroup, double precursorMz)
        {
            int? massShift = tranGroup.DecoyMassShift;
            double shift = SequenceMassCalc.GetPeptideInterval(massShift);
            return string.Format(@"{0:F04}{1}", precursorMz - shift,
                Transition.GetDecoyText(massShift));
        }

        #region IChildPicker Members

        public override bool CanShow
        {
            get
            {
                return (!DocNode.IsDecoy && base.CanShow);
            }
        }

        public override string GetPickLabel(DocNode child)
        {
            return TransitionTreeNode.DisplayText(SequenceTree.GetDisplaySettings(PepNode), (TransitionDocNode) child);
        }

        public override Image GetPickTypeImage(DocNode child)
        {
            return TransitionTreeNode.GetTypeImage((TransitionDocNode)child, SequenceTree);
        }

        public override Image GetPickPeakImage(DocNode child)
        {
            return TransitionTreeNode.GetPeakImage((TransitionDocNode)child,
                PepNode, SequenceTree);
        }

        public override ITipProvider GetPickTip(DocNode child)
        {
            return new PickTransitionTip((TransitionDocNode) child);
        }

        private sealed class PickTransitionTip : ITipProvider
        {
            private readonly TransitionDocNode _nodeTran;

            public PickTransitionTip(TransitionDocNode nodeTran)
            {
                _nodeTran = nodeTran;
            }

            // Not enough useful information in this tip yet
            public bool HasTip { get { return _nodeTran.HasLoss; } }

            public Size RenderTip(Graphics g, Size sizeMax, bool draw)
            {
                return TransitionTreeNode.RenderTip(_nodeTran, g, sizeMax, draw);
            }
        }

        public override bool Filtered
        {
            get { return Settings.Default.FilterTransitions; }
            set { Settings.Default.FilterTransitions = value; }
        }

        public override IEnumerable<DocNode> GetChoices(bool useFilter)
        {
            var nodePep = PepNode;
            if (nodePep == null)
            {
                throw new InvalidOperationException(
                    Resources.TransitionGroupTreeNode_GetChoices_Invalid_attempt_to_get_choices_for_a_node_that_has_not_been_added_to_the_tree_yet);
            }

            var listChildrenNew = DocNode.GetPrecursorChoices(DocSettings, nodePep.ExplicitMods, useFilter);
            // Existing transitions must be part of the first settings change to ensure proper
            // handling of user set peak boundaries.
            MergeChosen(listChildrenNew, useFilter, node => ((TransitionDocNode)node).Key(DocNode));
            var nodeGroup = (TransitionGroupDocNode)DocNode.ChangeChildrenChecked(listChildrenNew);
            var diff = new SrmSettingsDiff(DocSettings, true);
            // Update results on the group to correctly handle user set peak boundaries
            nodeGroup = nodeGroup.UpdateResults(DocSettings, diff, nodePep, DocNode);

            // Make sure any properties that depend on peptide relationships,
            // like ratios get updated.
            nodePep = (PeptideDocNode)nodePep.ReplaceChild(nodeGroup);
            diff = new SrmSettingsDiff(diff, SrmSettingsDiff.PROPS);
            nodePep = nodePep.ChangeSettings(DocSettings, diff);
            var id = nodeGroup.Id;
            int iGroup = nodePep.Children.IndexOf(n => ReferenceEquals(n.Id, id));
            if (iGroup != -1)
                nodeGroup = (TransitionGroupDocNode)nodePep.Children[iGroup];
            listChildrenNew = new List<DocNode>(nodeGroup.Children);
            // Merge with existing transitions again to avoid changes based on the settings
            // updates.
            MergeChosen(listChildrenNew, useFilter, node => ((TransitionDocNode)node).Key(nodeGroup));
            return listChildrenNew;
        }

        public override bool ShowAutoManageChildren
        {
            get { return true; }
        }

        public override string SynchSiblingsLabel
        {
            get
            {
                if (DocSettings.PeptideSettings.Quantification.SimpleRatios)
                {
                    return null;
                }
                return HasSiblingsToSynch(false)
                           ? Resources.TransitionGroupTreeNode_SynchSiblingsLabel_Synchronize_isotope_label_types
                           : null;
            }
        }

        public override bool IsSynchSiblings
        {
            get { return !HasSiblingsToSynch(true) && Settings.Default.SynchronizeIsotopeTypes; }
            set
            {
                // Avoid changing the flag in the settings, if this value is not
                // different from current value
                if (IsSynchSiblings != value)
                    Settings.Default.SynchronizeIsotopeTypes = value;
            }
        }

        /// <summary>
        /// True if this node has siblings with the same charge state, and
        /// if those siblings must be in-synch, then only if they are not.
        /// For small molecules, true only if all transitions are precursor transitions
        /// </summary>
        private bool HasSiblingsToSynch(bool mustBeInSynch)
        {
            var siblingNodes = Parent.Nodes;
            if (siblingNodes.Count > 1)
            {
                // For small molecules, we can only synch precursor transitions,
                // and only if both transition groups are defined by formula, or both by mz
                if (!IsSynchable())
                    return false;
                var tranGroupThis = DocNode.TransitionGroup;
                var adductNoLabels = tranGroupThis.PrecursorAdduct.Unlabeled; // If adduct contains an isotope label, ignore label for comparison purposes
                foreach (TransitionGroupTreeNode nodeTree in siblingNodes)
                {
                    var tranGroup = nodeTree.DocNode.TransitionGroup;
                    if (!ReferenceEquals(tranGroupThis, tranGroup) &&
                        Equals(adductNoLabels, tranGroup.PrecursorAdduct.Unlabeled) &&
                        !(mustBeInSynch && DocNode.EquivalentChildren(nodeTree.DocNode)))
                    {
                        if (!tranGroupThis.IsCustomIon)
                        {
                            return true;
                        }
                        else if (tranGroup.IsCustomIon && nodeTree.IsSynchable() &&
                                 string.IsNullOrEmpty(tranGroupThis.CustomMolecule.Formula) ==
                                 string.IsNullOrEmpty(tranGroup.CustomMolecule.Formula))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool IsSynchable()
        {
            var tranGroupThis = DocNode.TransitionGroup;
            if (tranGroupThis.IsCustomIon)
            {
                return GetChoices(false).Cast<TransitionDocNode>().All(trans => trans.Transition.IsPrecursor());
            }
            return true;
        }
        
        #endregion

        #region ITipProvider Members

        public override bool HasTip { get { return base.HasTip || !ShowAnnotationTipOnly; } }

        public override Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            var size = base.RenderTip(g, sizeMax, draw);
            if(ShowAnnotationTipOnly)
                return size;
            if (draw)
                g.TranslateTransform(0, size.Height);
            Size sizeMaxNew = new Size(sizeMax.Width, sizeMax.Height - size.Height);
            var nodePep = (Parent != null ? ((PeptideTreeNode) Parent).DocNode : null);
            var nodeTranTree = SequenceTree.GetNodeOfType<TransitionTreeNode>();
            var nodeTranSelected = (nodeTranTree != null ? nodeTranTree.DocNode : null);
            var sizeNew = RenderTip(nodePep, DocNode, nodeTranSelected, DocSettings, g, sizeMaxNew, draw);
            return new Size(Math.Min(sizeMax.Width, Math.Max(size.Width, sizeNew.Width)), size.Height + sizeNew.Height);
        }

        public static Size RenderTip(PeptideDocNode nodePep,
                                     TransitionGroupDocNode nodeGroup,
                                     TransitionDocNode nodeTranSelected,
                                     SrmSettings settings,
                                     Graphics g,
                                     Size sizeMax,
                                     bool draw)
        {
            if (nodeGroup.TransitionGroup.IsCustomIon)  // TODO(bspratt) this seems to leave out a lot of detail
            {
                var customTable = new TableDesc();
                using (RenderTools rt = new RenderTools())
                {
                    customTable.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Molecule, nodeGroup.CustomMolecule.Name, rt);
                    customTable.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Precursor_charge,
                        FormatAdductTip(nodeGroup.TransitionGroup.PrecursorAdduct), rt);
                    customTable.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Precursor_mz,
                        string.Format(@"{0:F04}", nodeGroup.PrecursorMz), rt);
                    if (nodeGroup.CustomMolecule.Formula != null)
                    {
                        customTable.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Formula,
                            nodeGroup.CustomMolecule.Formula + nodeGroup.TransitionGroup.PrecursorAdduct.AdductFormula.ToString(LocalizationHelper.CurrentCulture), rt);
                    }
                    SizeF size = customTable.CalcDimensions(g);
                    customTable.Draw(g);
                    return new Size((int) size.Width + 2, (int) size.Height + 2);
                }
            }
            ExplicitMods mods = (nodePep != null ? nodePep.ExplicitMods : null);
            IEnumerable<DocNode> choices = nodeGroup.GetPrecursorChoices(settings, mods, true).ToArray();
            HashSet<DocNode> chosen = new HashSet<DocNode>(nodeGroup.Children);

            // Make sure all chosen peptides get listed
            HashSet<DocNode> setChoices = new HashSet<DocNode>(choices);
            setChoices.UnionWith(chosen);
            choices = setChoices.ToArray();

            Transition tranSelected = (nodeTranSelected != null ? nodeTranSelected.Transition : null);

            IFragmentMassCalc calc = settings.GetFragmentCalc(nodeGroup.TransitionGroup.LabelType, mods);
            var aa = nodeGroup.TransitionGroup.Peptide.Target.Sequence;  // We handled custom ions above, and returned
            var masses = calc.GetFragmentIonMasses(nodeGroup.TransitionGroup.Peptide.Target);

            var filter = settings.TransitionSettings.Filter;

            // Get charges and type pairs, making sure all chosen charges are included
            var setCharges = new HashSet<Adduct>(filter.PeptideProductCharges.Where(charge =>
                Math.Abs(charge.AdductCharge) <= Math.Abs(nodeGroup.TransitionGroup.PrecursorCharge) &&
                Math.Sign(charge.AdductCharge) == Math.Sign(nodeGroup.TransitionGroup.PrecursorCharge)));
            HashSet<IonType> setTypes = new HashSet<IonType>(filter.PeptideIonTypes);
            foreach (TransitionDocNode nodTran in chosen)
            {
                var type = nodTran.Transition.IonType;
                if (!Transition.IsPeptideFragment(type))
                    continue;
                setCharges.Add(nodTran.Transition.Adduct);
                setTypes.Add(type);
            }
            setTypes.RemoveWhere(t => !Transition.IsPeptideFragment(t));
            var charges = setCharges.Where(c => c.IsProteomic).ToArray();
            Array.Sort(charges);
            IonType[] types = Transition.GetTypePairs(setTypes);

            var tableDetails = new TableDesc();
            var table = new TableDesc();

            using (RenderTools rt = new RenderTools())
            {
                var seqModified = GetModifiedSequence(nodePep, nodeGroup, settings);
                if (!Equals(seqModified, nodeGroup.TransitionGroup.Peptide.Target))
                    tableDetails.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Modified, seqModified.Sequence, rt);

                var precursorCharge = nodeGroup.TransitionGroup.PrecursorAdduct;
                var precursorMz = nodeGroup.PrecursorMz;
                tableDetails.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Precursor_charge,
                                          precursorCharge.AdductCharge.ToString(LocalizationHelper.CurrentCulture), rt);
                tableDetails.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Precursor_mz,
                                          string.Format(@"{0:F04}", precursorMz), rt);
                tableDetails.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Precursor_mh,
                                          string.Format(@"{0:F04}", nodeGroup.GetPrecursorIonMass()),
                                          rt);
                int? decoyMassShift = nodeGroup.TransitionGroup.DecoyMassShift;
                if (decoyMassShift.HasValue)
                {
                    tableDetails.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Decoy_Mass_Shift,
                                              decoyMassShift.Value.ToString(LocalizationHelper.CurrentCulture), rt);
                }
                if (nodeGroup.HasLibInfo)
                {
                    foreach (KeyValuePair<PeptideRankId, string> pair in nodeGroup.LibInfo.RankValues)
                        tableDetails.AddDetailRow(pair.Key.Label, pair.Value, rt);
                }

                if (charges.Length > 0 && types.Length > 0)
                {
                    var headers = new RowDesc
                                  {
                                      CreateHead(@"#", rt),
                                      CreateHead(@"AA", rt),
                                      CreateHead(@"#", rt)
                                  };
                    foreach (var charge in charges)
                    {
                        string plusSub = Transition.GetChargeIndicator(charge);
                        foreach (IonType type in types)
                        {
                            CellDesc cell = CreateHead(type.GetLocalizedString().ToLower() + plusSub, rt);
                            if (Transition.IsNTerminal(type))
                                headers.Insert(0, cell);
                            else
                                headers.Add(cell);
                        }
                    }
                    table.Add(headers);

                    int len = aa.Length;
                    for (int i = 0; i < len; i++)
                    {
                        CellDesc cellAA = CreateRowLabel(aa.Substring(i, 1), rt);
                        cellAA.Align = StringAlignment.Center;

                        var row = new RowDesc
                                  {
                                      CreateRowLabel(i == len - 1 ? string.Empty : (i + 1).ToString(CultureInfo.InvariantCulture), rt),
                                      cellAA,
                                      CreateRowLabel(i == 0 ? string.Empty : (len - i).ToString(CultureInfo.InvariantCulture), rt)
                                  };

                        foreach (var charge in charges)
                        {
                            foreach (IonType type in types)
                            {
                                CellDesc cell;
                                if (Transition.IsNTerminal(type))
                                {
                                    if (i == len - 1)
                                        cell = CreateData(string.Empty, rt);
                                    else
                                    {
                                        var massH = masses[type, i];
                                        cell = CreateIon(type, i + 1, massH, charge, choices, chosen, tranSelected, rt);
                                    }
                                    row.Insert(0, cell);
                                }
                                else
                                {
                                    if (i == 0)
                                        cell = CreateData(string.Empty, rt);
                                    else
                                    {
                                        var massH = masses[type, i - 1];
                                        cell = CreateIon(type, len - i, massH, charge, choices, chosen, tranSelected, rt);
                                    }
                                    row.Add(cell);
                                }
                            }
                        }
                        table.Add(row);
                    }
                }

                SizeF sizeDetails = tableDetails.CalcDimensions(g);
                sizeDetails.Height += TableDesc.TABLE_SPACING;    // Spacing between details and fragments
                SizeF size = table.CalcDimensions(g);
                if (draw)
                {
                    tableDetails.Draw(g);
                    g.TranslateTransform(0, sizeDetails.Height);
                    table.Draw(g);
                    g.TranslateTransform(0, -sizeDetails.Height);
                }

                int width = (int) Math.Round(Math.Max(sizeDetails.Width, size.Width));
                int height = (int) Math.Round(sizeDetails.Height + size.Height);
                return new Size(width + 2, height + 2);
            }
        }

        private static Target GetModifiedSequence(PeptideDocNode nodePep,
                                                  TransitionGroupDocNode nodeGroup,
                                                  SrmSettings settings)
        {
            ExplicitMods mods = (nodePep != null ? nodePep.ExplicitMods : null);
            var calcPre = settings.GetPrecursorCalc(nodeGroup.TransitionGroup.LabelType, mods);
            var seq = nodeGroup.TransitionGroup.Peptide.Target;
            return calcPre.GetModifiedSequenceDisplay(seq);            
        }

        private static CellDesc CreateHead(string text, RenderTools rt)
        {
            return new CellDesc(text, rt) { Font = rt.FontBold };
        }

        private static CellDesc CreateRowLabel(string text, RenderTools rt)
        {
            return new CellDesc(text, rt) { Align = StringAlignment.Far };
        }

        private static CellDesc CreateData(string text, RenderTools rt)
        {
            return new CellDesc(text, rt) { Align = StringAlignment.Far };
        }

        private static CellDesc CreateIon(IonType type, int ordinal, TypedMass massH, Adduct charge,
                                          IEnumerable<DocNode> choices, ICollection<DocNode> chosen, Transition tranSelected,
                                          RenderTools rt)
        {
            double mz = SequenceMassCalc.GetMZ(massH, charge);
            CellDesc cell = CreateData(string.Format(@"{0:F02}", mz), rt);

            foreach (TransitionDocNode nodeTran in choices)
            {
                Transition tran = nodeTran.Transition;
                if (tran.IonType == type &&
                    tran.Ordinal == ordinal &&
                    tran.Adduct == charge)
                {
                    cell.Font = rt.FontBold;
                    if (Equals(tran, tranSelected))
                    {
                        cell.Brush = rt.BrushSelected; // Stop after selected
                        break;
                    }
                    if (!chosen.Contains(nodeTran))
                        cell.Brush = rt.BrushChoice;  // Keep looking
                    else
                    {
                        cell.Brush = rt.BrushChosen;  // Stop after chosen
                        break;
                    }
                }
            }

            return cell;
        }

        #endregion
    }
}
