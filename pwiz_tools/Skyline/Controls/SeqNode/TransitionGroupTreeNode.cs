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
using System.Linq;
using System.Text;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.SeqNode
{
    public class TransitionGroupTreeNode : SrmTreeNodeParent, ITipProvider
    {
        public const string TITLE = "Precursor";

        public static bool ExpandDefault { get { return Settings.Default.SequenceTreeExpandPrecursors; } }

        public static TransitionGroupTreeNode CreateInstance(SequenceTree tree, DocNode nodeDoc)
        {
            Debug.Assert(nodeDoc is TransitionGroupDocNode);
            var nodeTree = new TransitionGroupTreeNode(tree, (TransitionGroupDocNode) nodeDoc);

            if (ExpandDefault)
                nodeTree.Expand();

            return nodeTree;
        }

        public TransitionGroupTreeNode(SequenceTree tree, TransitionGroupDocNode nodeGroup)
            : base(tree, nodeGroup)
        {
        }

        public TransitionGroupDocNode DocNode { get { return (TransitionGroupDocNode) Model; } }

        public PeptideDocNode PepNode
        {
            get { return (Parent != null ? ((PeptideTreeNode)Parent).DocNode : null); }
        }

        public override string Heading
        {
            get { return TITLE; }
        }

        public override string ChildHeading
        {
            get { return TransitionTreeNode.TITLE + "s"; }
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
            if (peakImageIndex != StateImageIndex)
                StateImageIndex = peakImageIndex;
            string label = GetDisplayText(DocNode, PepNode, SequenceTree);
            if (!Equals(label, Text))
                Text = label;

            // Make sure children are up to date
            OnUpdateChildren(ExpandDefault);
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
            return (int)(nodeGroup.HasLibInfo ?
                SequenceTree.ImageId.tran_group_lib : SequenceTree.ImageId.tran_group);
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
            {
                return settings.MeasuredResults.IsChromatogramSetLoaded(index) ?
                    (int)SequenceTree.StateImageId.peak_blank : -1;
            }
            else if (ratio < 0.5)
                return (int)SequenceTree.StateImageId.no_peak;
            else if (ratio < 1.0)
                return (int)SequenceTree.StateImageId.keep;

            return (int)SequenceTree.StateImageId.peak;
        }

        public static string GetDisplayText(TransitionGroupDocNode nodeGroup,
            PeptideDocNode nodePep, SequenceTree sequenceTree)
        {
            return GetLabel(nodeGroup.TransitionGroup, nodeGroup.PrecursorMz,
                GetResultsText(nodeGroup, nodePep, sequenceTree));
        }

        private static string GetResultsText(TransitionGroupDocNode nodeTran,
            PeptideDocNode nodePep, SequenceTree sequenceTree)
        {
            int index = sequenceTree.GetDisplayResultsIndex(nodePep);
            int indexRatio = sequenceTree.RatioIndex;

            float? libraryProduct = nodeTran.GetLibraryDotProduct(index);
            float? stdev;
            float? ratio = nodeTran.GetPeakAreaRatio(index, indexRatio, out stdev);
            if (!ratio.HasValue && !libraryProduct.HasValue)
                return "";
            StringBuilder sb = new StringBuilder(" (");
            int len = sb.Length;
            if (libraryProduct.HasValue)
                sb.Append(string.Format("dotp {0:F02}", libraryProduct.Value));
            if (ratio.HasValue)
            {
                if (sb.Length > len)
                    sb.Append(", ");
                double ratioRounded = MathEx.RoundAboveZero(ratio.Value, 2, 4);
                sb.Append(string.Format("total ratio {0}", ratioRounded));

//                if (stdev.HasValue)
//                {
//                    double stdevRounded = RoundAboveZero(stdev.Value, 2, 4);
//                    if (stdevRounded > 0)
//                        sb.Append(string.Format(" ± {0}", stdevRounded));
//                }
            }
            sb.Append(")");
            return sb.ToString();
        }

        protected override void UpdateChildren(bool materialize)
        {
            UpdateNodes<TransitionTreeNode>(SequenceTree, Nodes, DocNode.Children,
                                            materialize, TransitionTreeNode.CreateInstance);
        }

        public static string GetLabel(TransitionGroup tranGroup, double precursorMz,
            string resultsText)
        {
            return string.Format("{0:F04}{1}{2}{3}", precursorMz,
                                 Transition.GetChargeIndicator(tranGroup.PrecursorCharge),
                                 tranGroup.LabelTypeText, resultsText);
        }

        #region IChildPicker Members

        public override string GetPickLabel(DocNode child)
        {
            return TransitionTreeNode.GetDisplayText((TransitionDocNode) child,
                PepNode, SequenceTree);
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
                throw new InvalidOperationException("Invalid attempt to get choices for a node that has not been added to the tree yet.");

            var listChildrenNew = GetChoices(DocNode, DocSettings, nodePep.ExplicitMods, useFilter);
            var nodeGroup = (TransitionGroupDocNode) DocNode.ChangeChildrenChecked(listChildrenNew);
            
            // Make sure any properties that depend on peptide relationships,
            // like ratios get updated.
            nodePep = (PeptideDocNode)nodePep.ReplaceChild(nodeGroup);
            int iGroup = nodePep.Children.IndexOf(nodeGroup);
            nodePep = nodePep.ChangeSettings(DocSettings, SrmSettingsDiff.PROPS);
            nodeGroup = (TransitionGroupDocNode) nodePep.Children[iGroup];
            listChildrenNew = new List<DocNode>(nodeGroup.Children);
            MergeChosen(listChildrenNew, useFilter, node => ((TransitionDocNode)node).Key);
            return listChildrenNew;
        }

        public static IList<DocNode> GetChoices(TransitionGroupDocNode nodeGroup, SrmSettings settings, ExplicitMods mods, bool useFilter)
        {
            TransitionGroup group = nodeGroup.TransitionGroup;

            SpectrumHeaderInfo libInfo = null;
            var transitionRanks = new Dictionary<double, LibraryRankedSpectrumInfo.RankedMI>();
            group.GetLibraryInfo(settings, mods, useFilter, ref libInfo, transitionRanks);

            var listChoices = new List<DocNode>();
            foreach (TransitionDocNode nodeTran in group.GetTransitions(settings, mods,
                    nodeGroup.PrecursorMz, libInfo, transitionRanks, useFilter))
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
                return HasSiblingsToSynch(false) ? "Synchronize isotope label types" : null;
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
        /// </summary>
        private bool HasSiblingsToSynch(bool mustBeInSynch)
        {
            var siblingNodes = Parent.Nodes;
            if (siblingNodes.Count > 1)
            {
                var tranGroupThis = DocNode.TransitionGroup;
                foreach (TransitionGroupTreeNode nodeTree in siblingNodes)
                {
                    var tranGroup = nodeTree.DocNode.TransitionGroup;
                    if (!ReferenceEquals(tranGroupThis, tranGroup) &&
                            tranGroupThis.PrecursorCharge == tranGroup.PrecursorCharge &&
                            !(mustBeInSynch && DocNode.EquivalentChildren(nodeTree.DocNode)))
                        return true;
                }
            }
            return false;
        }
        
        #endregion

        #region ITipProvider Members

        public bool HasTip { get { return true; } }

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            var nodePep = (Parent != null ? ((PeptideTreeNode) Parent).DocNode : null);
            var nodeTranTree = SequenceTree.GetNodeOfType<TransitionTreeNode>();
            var nodeTranSelected = (nodeTranTree != null ? nodeTranTree.DocNode : null);
            return RenderTip(nodePep, DocNode, nodeTranSelected, DocSettings, g, sizeMax, draw);
        }

        public static Size RenderTip(PeptideDocNode nodePep,
                                     TransitionGroupDocNode nodeGroup,
                                     TransitionDocNode nodeTranSelected,
                                     SrmSettings settings,
                                     Graphics g,
                                     Size sizeMax,
                                     bool draw)
        {
            ExplicitMods mods = (nodePep != null ? nodePep.ExplicitMods : null);
            IEnumerable<DocNode> choices = GetChoices(nodeGroup, settings, mods, true).ToArray();
            HashSet<DocNode> chosen = new HashSet<DocNode>(nodeGroup.Children);

            // Make sure all chosen peptides get listed
            HashSet<DocNode> setChoices = new HashSet<DocNode>(choices);
            setChoices.UnionWith(chosen);
            choices = setChoices.ToArray();

            Transition tranSelected = (nodeTranSelected != null ? nodeTranSelected.Transition : null);

            IFragmentMassCalc calc = settings.GetFragmentCalc(nodeGroup.TransitionGroup.LabelType, mods);
            string aa = nodeGroup.TransitionGroup.Peptide.Sequence;
            double[,] masses = calc.GetFragmentIonMasses(aa);

            var filter = settings.TransitionSettings.Filter;

            // Get charges and type pairs, making sure all chosen charges are included
            HashSet<int> setCharges = new HashSet<int>(filter.ProductCharges);
            HashSet<IonType> setTypes = new HashSet<IonType>(filter.IonTypes);
            foreach (TransitionDocNode nodTran in chosen)
            {
                setCharges.Add(nodTran.Transition.Charge);
                setTypes.Add(nodTran.Transition.IonType);
            }
            int[] charges = setCharges.ToArray();
            Array.Sort(charges);
            IonType[] types = Transition.GetTypePairs(setTypes);

            var tableDetails = new TableDesc();
            var table = new TableDesc();

            using (RenderTools rt = new RenderTools())
            {
                var calcPre = settings.GetPrecursorCalc(nodeGroup.TransitionGroup.LabelType, mods);
                string seq = nodeGroup.TransitionGroup.Peptide.Sequence;
                string seqModified = calcPre.GetModifiedSequence(seq, true);
                if (!Equals(seq, seqModified))
                    tableDetails.AddDetailRow("Modified", seqModified, rt);

                var precursorCharge = nodeGroup.TransitionGroup.PrecursorCharge;
                var precursorMz = nodeGroup.PrecursorMz;
                tableDetails.AddDetailRow("Precursor charge", precursorCharge.ToString(), rt);
                tableDetails.AddDetailRow("Precursor m/z", string.Format("{0:F04}", precursorMz), rt);
                tableDetails.AddDetailRow("Precursor m+h", string.Format("{0:F04}", SequenceMassCalc.GetMH(precursorMz, precursorCharge)), rt);
                if (nodeGroup.HasLibInfo)
                {
                    foreach (KeyValuePair<PeptideRankId, string> pair in nodeGroup.LibInfo.RankValues)
                        tableDetails.AddDetailRow(pair.Key.Label, pair.Value, rt);
                }
                if (!string.IsNullOrEmpty(nodeGroup.Note))
                {
                    tableDetails.AddDetailRow("Note", nodeGroup.Note, rt);
                }

                var headers = new RowDesc
                                  {
                                      CreateHead("#", rt),
                                      CreateHead("AA", rt),
                                      CreateHead("#", rt)
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
                                      CreateRowLabel(i == len - 1 ? "" : (i + 1).ToString(), rt),
                                      cellAA,
                                      CreateRowLabel(i == 0 ? "" : (len - i).ToString(), rt)
                                  };

                    foreach (int charge in charges)
                    {
                        foreach (IonType type in types)
                        {
                            CellDesc cell;
                            if (Transition.IsNTerminal(type))
                            {
                                if (i == len - 1)
                                    cell = CreateData("", rt);
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
                                    cell = CreateData("", rt);
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

                int width = (int) Math.Max(sizeDetails.Width, size.Width);
                int height = (int) (sizeDetails.Height + size.Height);
                return new Size(width + 2, height + 2);
            }
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
            CellDesc cell = CreateData(string.Format("{0:F02}", mz), rt);

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
                    else if (!chosen.Contains(nodeTran))
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