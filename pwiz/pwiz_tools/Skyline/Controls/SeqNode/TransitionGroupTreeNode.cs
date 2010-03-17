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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.SeqNode
{
    public class TransitionGroupTreeNode : SrmTreeNodeParent, ITipProvider, IClipboardDataProvider
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
            string label = GetLabel(DocNode.TransitionGroup, DocNode.PrecursorMz, ResultsText, DocNode.NoteMark);
            if (!Equals(label, Text))
                Text = label;

            // Make sure children are up to date
            OnUpdateChildren(ExpandDefault);
        }

        public int TypeImageIndex
        {
            get
            {
                return (int)(DocNode.HasLibInfo ?
                    SequenceTree.ImageId.tran_group_lib : SequenceTree.ImageId.tran_group);
            }
        }

        public int PeakImageIndex
        {
            get
            {
                if (!DocSettings.HasResults)
                    return -1;

                int index = SequenceTree.ResultsIndex;

                float? ratio = (DocNode.HasResults ? DocNode.GetPeakCountRatio(index) : null);
                if (ratio == null)
                {
                    return DocSettings.MeasuredResults.IsChromatogramSetLoaded(index) ?
                        (int)SequenceTree.StateImageId.peak_blank : -1;
                }
                else if (ratio < 0.5)
                    return (int)SequenceTree.StateImageId.no_peak;
                else if (ratio < 1.0)
                    return (int)SequenceTree.StateImageId.keep;

                return (int)SequenceTree.StateImageId.peak;
            }
        }

        public string ResultsText
        {
            get
            {
                int index = SequenceTree.ResultsIndex;
                float? libraryProduct = DocNode.GetLibraryDotProduct(index);
                float? stdev;
                float? ratio = DocNode.GetPeakAreaRatio(index, out stdev);
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
                    double ratioRounded = RoundAboveZero(ratio.Value, 2, 4);
                    sb.Append(string.Format("total ratio {0}", ratioRounded));

//                    if (stdev.HasValue)
//                    {
//                        double stdevRounded = RoundAboveZero(stdev.Value, 2, 4);
//                        if (stdevRounded > 0)
//                        {
//                            sb.Append(", ");
//                            sb.Append(string.Format("stdev {0}", stdevRounded));
//                        }
//                    }
                }
                sb.Append(")");
                return sb.ToString();
            }
        }

        private static double RoundAboveZero(float value, int startDigits, int mostDigits)
        {
            for (int i = startDigits; i <= mostDigits; i++)
            {
                double rounded = Math.Round(value, i);
                if (rounded > 0)
                    return rounded;
            }
            return 0;
        }

        protected override void UpdateChildren(bool materialize)
        {
            UpdateNodes<TransitionTreeNode>(SequenceTree, Nodes, DocNode.Children,
                                            materialize, TransitionTreeNode.CreateInstance);
        }

        public static string GetLabel(TransitionGroup tranGroup, double precursorMz,
            string resultsText, string noteMark)
        {
            return string.Format("{0:F04}{1}{2}{3}{4}", precursorMz,
                                 Transition.GetChargeIndicator(tranGroup.PrecursorCharge),
                                 tranGroup.LabelTypeText, resultsText, noteMark);
        }

        #region IChildPicker Members

        public override string GetPickLabel(object child)
        {
            return TransitionTreeNode.GetLabel((TransitionDocNode) child, "");
        }

        public override bool Filtered
        {
            get { return Settings.Default.FilterTransitions; }
            set { Settings.Default.FilterTransitions = value; }
        }

        public override bool Equivalent(object choice1, object choice2)
        {
            return Equals(((TransitionDocNode)choice1).Id, ((TransitionDocNode)choice2).Id);
        }

        public static IEnumerable<object> GetChoices(TransitionGroupDocNode nodeGroup, SrmSettings settings, ExplicitMods mods, bool useFilter)
        {
            TransitionGroup group = nodeGroup.TransitionGroup;

            SpectrumHeaderInfo libInfo = null;
            var transitionRanks = new Dictionary<double, LibraryRankedSpectrumInfo.RankedMI>();
            group.GetLibraryInfo(settings, mods, useFilter, ref libInfo, transitionRanks);

            foreach (TransitionDocNode nodeTran in group.GetTransitions(settings, mods,
                    nodeGroup.PrecursorMz, libInfo, transitionRanks, useFilter))
                yield return nodeTran;
        }

        public override IEnumerable<object> GetChoices(bool useFilter)
        {
            PeptideTreeNode nodePepTree = Parent as PeptideTreeNode;
            ExplicitMods mods = (nodePepTree != null ? nodePepTree.DocNode.ExplicitMods : null);
            return GetChoices(DocNode, DocSettings, mods, useFilter);
        }

        public static IEnumerable<object> GetChosen(TransitionGroupDocNode nodeGroup)
        {
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                yield return nodeTran;            
        }

        public override IEnumerable<object> Chosen
        {
            get
            {
                return GetChosen(DocNode);
            }
        }

        public override IPickedList CreatePickedList(IEnumerable<object> chosen, bool autoManageChildren)
        {
            return new TransitionPickedList(chosen, autoManageChildren);
        }

        public override bool ShowAutoManageChildren
        {
            get { return true; }
        }

        private sealed class TransitionPickedList : IPickedList
        {
            private readonly IEnumerable<object> _picked;

            public TransitionPickedList(IEnumerable<object> picked, bool autoManageChildren)
            {
                _picked = picked;
                AutoManageChildren = autoManageChildren;
            }

            public IEnumerable<Identity> Chosen
            {
                get
                {
                    foreach (TransitionDocNode nodeTran in _picked)
                        yield return nodeTran.Id;
                }
            }

            public DocNode CreateChildNode(Identity childId)
            {
                foreach (TransitionDocNode nodeTran in _picked)
                {
                    if (ReferenceEquals(nodeTran.Id, childId))
                        return nodeTran;
                }

                throw new ArgumentException("Requested child not found in picked list.");
            }

            public bool AutoManageChildren { get; private set; }
        }

        #endregion

        #region ITipProvider Members

        public bool HasTip { get { return true; } }

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            return RenderTip(SequenceTree, Parent as PeptideTreeNode, DocNode, g, sizeMax, draw);
        }

        public static Size RenderTip(SequenceTree sequenceTree, PeptideTreeNode nodePepTree,
            TransitionGroupDocNode nodeGroup, Graphics g, Size sizeMax, bool draw)
        {
            SrmSettings settings = sequenceTree.Document.Settings;
            ExplicitMods mods = (nodePepTree != null ? nodePepTree.DocNode.ExplicitMods : null);
            IEnumerable<object> choices = GetChoices(nodeGroup, settings, mods, true).ToArray();
            HashSet<object> chosen = new HashSet<object>(GetChosen(nodeGroup));

            // Make sure all chosen peptides get listed
            HashSet<object> setChoices = new HashSet<object>(choices);
            setChoices.UnionWith(chosen);
            choices = setChoices.ToArray();

            TransitionTreeNode nodeTranTree = sequenceTree.GetNodeOfType<TransitionTreeNode>();
            Transition tranSelected = (nodeTranTree != null ? nodeTranTree.DocNode.Transition : null);

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
                                          IEnumerable<object> choices, ICollection<object> chosen, Transition tranSelected,
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

        #region Implementation of IClipboardDataProvider

        public void ProvideData()
        {
            DataObject data = new DataObject();
            data.SetData(DataFormats.Text, DocNode.TransitionGroup.Peptide.Sequence);

            StringBuilder sb = new StringBuilder();

            // TODO: Render in HTML.

            data.SetData(DataFormats.Html, HtmlFragment.ClipBoardText(sb.ToString()));
            Clipboard.Clear();
            Clipboard.SetDataObject(data);
        }

        #endregion
    }
}