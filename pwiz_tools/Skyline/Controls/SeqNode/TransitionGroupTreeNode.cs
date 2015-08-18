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

        public TransitionGroupDocNode DocNode { get { return (TransitionGroupDocNode) Model; } }

        public string ModifiedSequence
        {
            get { return GetModifiedSequence(PepNode, DocNode, SequenceTree.Document.Settings); }
        }

        public PeptideDocNode PepNode
        {
            get { return (Parent != null ? ((PeptideTreeNode)Parent).DocNode : null); }
        }

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
                GetResultsText(nodeGroup, settings.NodePep, settings.Index, settings.RatioIndex));
        }

        private const string DOTP_FORMAT = "0.##"; // Not L10N
        private const string CS_SEPARATOR = ", "; // Not L10N

        public static string GetResultsText(TransitionGroupDocNode nodeGroup,
            PeptideDocNode nodePep, int indexResult, int indexRatio)
        {
            float? libraryProduct = nodeGroup.GetLibraryDotProduct(indexResult);
            float? isotopeProduct = nodeGroup.GetIsotopeDotProduct(indexResult);
            RatioValue ratio = nodeGroup.GetPeakAreaRatio(indexResult, indexRatio);
            if (null == ratio && !isotopeProduct.HasValue && !libraryProduct.HasValue)
                return string.Empty;
            StringBuilder sb = new StringBuilder(" ("); // Not L10N
            int len = sb.Length;
            if (isotopeProduct.HasValue)
                sb.Append(string.Format("idotp {0}", isotopeProduct.Value.ToString(DOTP_FORMAT))); // Not L10N
            if (libraryProduct.HasValue)
            {
                if (sb.Length > len)
                    sb.Append(CS_SEPARATOR);
                sb.Append(string.Format("dotp {0}", libraryProduct.Value.ToString(DOTP_FORMAT))); // Not L10N
            }
            if (ratio != null)
            {
                if (sb.Length > len)
                    sb.Append(CS_SEPARATOR);
                if (!double.IsNaN(ratio.StdDev))
                {
                    sb.Append(string.Format("rdotp {0}", ratio.DotProduct.ToString(DOTP_FORMAT))); // Not L10N
                    sb.Append(CS_SEPARATOR);
                }

                sb.Append(string.Format(Resources.TransitionGroupTreeNode_GetResultsText_total_ratio__0__,
                                        MathEx.RoundAboveZero(ratio.Ratio, 2, 4)));
            }
            sb.Append(")"); // Not L10N
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
            return string.Format("{0}{1}{2}{3}", GetMzLabel(tranGroup, precursorMz), // Not L10N
                                 Transition.GetChargeIndicator(tranGroup.PrecursorCharge),
                                 tranGroup.LabelTypeText, resultsText);
        }

        private static string GetMzLabel(TransitionGroup tranGroup, double precursorMz)
        {
            int? massShift = tranGroup.DecoyMassShift;
            double shift = SequenceMassCalc.GetPeptideInterval(massShift);
            return string.Format("{0:F04}{1}", precursorMz - shift, // Not L10N
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
            return TransitionTreeNode.DisplayText((TransitionDocNode) child, SequenceTree.GetDisplaySettings(PepNode));
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

            var listChildrenNew = GetChoices(DocNode, DocSettings, nodePep.ExplicitMods, useFilter);
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

        public static IList<DocNode> GetChoices(TransitionGroupDocNode nodeGroup, SrmSettings settings, ExplicitMods mods, bool useFilter)
        {
            TransitionGroup group = nodeGroup.TransitionGroup;

            SpectrumHeaderInfo libInfo = null;
            var transitionRanks = new Dictionary<double, LibraryRankedSpectrumInfo.RankedMI>();
            group.GetLibraryInfo(settings, mods, useFilter, ref libInfo, transitionRanks);

            var listChoices = new List<DocNode>();
            foreach (TransitionDocNode nodeTran in nodeGroup.GetTransitions(settings, mods,
                    nodeGroup.PrecursorMz, nodeGroup.IsotopeDist, libInfo, transitionRanks, useFilter))
            {
                listChoices.Add(nodeTran);               
            }
            return listChoices;
        }

        public override bool ShowAutoManageChildren
        {
            get { return true; }
        }

        public override string SynchSiblingsLabel
        {
            get
            {
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
                foreach (TransitionGroupTreeNode nodeTree in siblingNodes)
                {
                    var tranGroup = nodeTree.DocNode.TransitionGroup;
                    if (!ReferenceEquals(tranGroupThis, tranGroup) &&
                        tranGroupThis.PrecursorCharge == tranGroup.PrecursorCharge &&
                        !(mustBeInSynch && DocNode.EquivalentChildren(nodeTree.DocNode)))
                    {
                        if (!tranGroupThis.IsCustomIon)
                        {
                            return true;
                        }
                        else if (tranGroup.IsCustomIon && nodeTree.IsSynchable() &&
                                 string.IsNullOrEmpty(tranGroupThis.CustomIon.Formula) ==
                                 string.IsNullOrEmpty(tranGroup.CustomIon.Formula))
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
            if (nodeGroup.TransitionGroup.IsCustomIon)
            {
                var customTable = new TableDesc();
                using (RenderTools rt = new RenderTools())
                {
                    customTable.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Molecule, nodeGroup.CustomIon.Name, rt);
                    customTable.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Precursor_charge,
                        nodeGroup.TransitionGroup.PrecursorCharge.ToString(LocalizationHelper.CurrentCulture), rt);
                    customTable.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Precursor_mz,
                        string.Format("{0:F04}", nodeGroup.PrecursorMz), rt); // Not L10N
                    if (nodeGroup.CustomIon.Formula != null)
                    {
                        customTable.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Formula,
                            nodeGroup.CustomIon.Formula, rt);
                    }
                    SizeF size = customTable.CalcDimensions(g);
                    customTable.Draw(g);
                    return new Size((int) size.Width + 2, (int) size.Height + 2);
                }
            }
            ExplicitMods mods = (nodePep != null ? nodePep.ExplicitMods : null);
            IEnumerable<DocNode> choices = GetChoices(nodeGroup, settings, mods, true).ToArray();
            HashSet<DocNode> chosen = new HashSet<DocNode>(nodeGroup.Children);

            // Make sure all chosen peptides get listed
            HashSet<DocNode> setChoices = new HashSet<DocNode>(choices);
            setChoices.UnionWith(chosen);
            choices = setChoices.ToArray();

            Transition tranSelected = (nodeTranSelected != null ? nodeTranSelected.Transition : null);

            IFragmentMassCalc calc = settings.GetFragmentCalc(nodeGroup.TransitionGroup.LabelType, mods);
            string aa = nodeGroup.TransitionGroup.Peptide.Sequence;  // We handled custom ions above, and returned
            double[,] masses = calc.GetFragmentIonMasses(aa);

            var filter = settings.TransitionSettings.Filter;

            // Get charges and type pairs, making sure all chosen charges are included
            HashSet<int> setCharges = new HashSet<int>(filter.ProductCharges.Where(charge =>
                Math.Abs(charge) <= Math.Abs(nodeGroup.TransitionGroup.PrecursorCharge) &&
                Math.Sign(charge) == Math.Sign(nodeGroup.TransitionGroup.PrecursorCharge)));
            HashSet<IonType> setTypes = new HashSet<IonType>(filter.IonTypes);
            foreach (TransitionDocNode nodTran in chosen)
            {
                var type = nodTran.Transition.IonType;
                if (type == IonType.precursor)
                    continue;
                setCharges.Add(nodTran.Transition.Charge);
                setTypes.Add(type);
            }
            setTypes.Remove(IonType.precursor);
            int[] charges = setCharges.ToArray();
            Array.Sort(charges);
            IonType[] types = Transition.GetTypePairs(setTypes);

            var tableDetails = new TableDesc();
            var table = new TableDesc();

            using (RenderTools rt = new RenderTools())
            {
                string seqModified = GetModifiedSequence(nodePep, nodeGroup, settings);
                if (!Equals(seqModified, nodeGroup.TransitionGroup.Peptide.Sequence))
                    tableDetails.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Modified, seqModified, rt);

                var precursorCharge = nodeGroup.TransitionGroup.PrecursorCharge;
                var precursorMz = nodeGroup.PrecursorMz;
                tableDetails.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Precursor_charge,
                                          precursorCharge.ToString(LocalizationHelper.CurrentCulture), rt);
                tableDetails.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Precursor_mz,
                                          string.Format("{0:F04}", precursorMz), rt); // Not L10N
                tableDetails.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Precursor_mh,
                                          string.Format("{0:F04}", nodeGroup.GetPrecursorIonMass()), // Not L10N
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
                                      CreateHead("#", rt), // Not L10N
                                      CreateHead("AA", rt), // Not L10N
                                      CreateHead("#", rt) // Not L10N
                                  };
                    foreach (int charge in charges)
                    {
                        string plusSub = Transition.GetChargeIndicator(charge);
                        foreach (IonType type in types)
                        {
                            CellDesc cell = CreateHead(type.ToString().ToLower() + plusSub, rt);
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

                        foreach (int charge in charges)
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
                                        double massH = masses[(int)type, i];
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
                                        double massH = masses[(int)type, i - 1];
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

        private static string GetModifiedSequence(PeptideDocNode nodePep,
                                                  TransitionGroupDocNode nodeGroup,
                                                  SrmSettings settings)
        {
            ExplicitMods mods = (nodePep != null ? nodePep.ExplicitMods : null);
            var calcPre = settings.GetPrecursorCalc(nodeGroup.TransitionGroup.LabelType, mods);
            string seq = nodeGroup.TransitionGroup.Peptide.Sequence;
            return calcPre.GetModifiedSequence(seq, true);            
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

        private static CellDesc CreateIon(IonType type, int ordinal, double massH, int charge,
                                          IEnumerable<DocNode> choices, ICollection<DocNode> chosen, Transition tranSelected,
                                          RenderTools rt)
        {
            double mz = SequenceMassCalc.GetMZ(massH, charge);
            CellDesc cell = CreateData(string.Format("{0:F02}", mz), rt); // Not L10N

            foreach (TransitionDocNode nodeTran in choices)
            {
                Transition tran = nodeTran.Transition;
                if (tran.IonType == type &&
                    tran.Ordinal == ordinal &&
                    tran.Charge == charge)
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