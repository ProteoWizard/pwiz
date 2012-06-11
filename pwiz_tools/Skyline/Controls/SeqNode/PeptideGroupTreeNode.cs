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
    public class PeptideGroupTreeNode : SrmTreeNodeParent
    {
        public const string PROTEIN_TITLE = "Protein";
        public const string PEPTIDE_LIST_TITLE = "Peptide List";

        public static bool ExpandDefault { get { return Settings.Default.SequenceTreeExpandProteins; } }

        public static PeptideGroupTreeNode CreateInstance(SequenceTree tree, DocNode nodeDoc)
        {
            Debug.Assert(nodeDoc is PeptideGroupDocNode);
            var nodeTree = new PeptideGroupTreeNode(tree, (PeptideGroupDocNode)nodeDoc);

            if (ExpandDefault)
                nodeTree.Expand();

            return nodeTree;
        }

// ReSharper disable SuggestBaseTypeForParameter
        public PeptideGroupTreeNode(SequenceTree tree, PeptideGroupDocNode group)
// ReSharper restore SuggestBaseTypeForParameter
            : base(tree, group)
        {
        }

        public PeptideGroupDocNode DocNode { get { return (PeptideGroupDocNode) Model; } }

        public override string Heading
        {
            get { return Model.Id is FastaSequence ? PROTEIN_TITLE : PEPTIDE_LIST_TITLE; }
        }

        public override string ChildHeading
        {
            get { return string.Format("{0} {1}s", Text, PeptideTreeNode.TITLE); }
        }

        public override string ChildUndoHeading
        {
            get { return string.Format("{0} {1}s", Text, PeptideTreeNode.TITLE.ToLower()); }
        }

        protected override void OnModelChanged()
        {
            int typeImageIndex = TypeImageIndex;
            if (typeImageIndex != ImageIndex)
                ImageIndex = SelectedImageIndex = typeImageIndex;
            string label = DisplayText((PeptideGroupDocNode) Model, SequenceTree.GetDisplaySettings(null));
            if (!string.Equals(label, Text))
                Text = label;

            // Make sure children are up to date
            OnUpdateChildren(ExpandDefault);
        }

        public int TypeImageIndex
        {
            get { return (int) (DocNode.IsDecoy ? SequenceTree.ImageId.protein_decoy : SequenceTree.ImageId.protein); }
        }

        protected override void UpdateChildren(bool materialize)
        {
            UpdateNodes(SequenceTree, Nodes, DocNode.Children, materialize, PeptideTreeNode.CreateInstance);
        }

        public static string DisplayText(PeptideGroupDocNode node, DisplaySettings settings)
        {
            return node.Name;
        }

        #region IChildPicker Members

        // TODO: GetPickLabel with library information e.g. (2 charges, 34 copies)

        public override bool CanShow
        {
            get { return (!DocNode.IsDecoy && (DocNode.Id is FastaSequence || ChildDocNodes.Count > 0)); }
        }

        public override bool Filtered
        {
            get { return Settings.Default.FilterPeptides; }
            set { Settings.Default.FilterPeptides = value; }
        }

        public override bool DrawPickLabel(DocNode child, Graphics g, Rectangle bounds, ModFontHolder fonts, Color foreColor, Color backColor)
        {
            PeptideTreeNode.DrawPeptideText((PeptideDocNode) child, DocSettings, null,
                g, bounds, fonts, foreColor, backColor);
            return true;
        }

        public override Image GetPickTypeImage(DocNode child)
        {
            return PeptideTreeNode.GetTypeImage((PeptideDocNode) child, SequenceTree);
        }

        public override Image GetPickPeakImage(DocNode child)
        {
            return PeptideTreeNode.GetPeakImage((PeptideDocNode) child, SequenceTree);
        }

        public override ITipProvider GetPickTip(DocNode child)
        {
            return new PickPeptideTip((PeptideDocNode) child, DocSettings);
        }

        private sealed class PickPeptideTip : ITipProvider
        {
            private readonly PeptideDocNode _nodePep;
            private readonly SrmSettings _settings;

            public PickPeptideTip(PeptideDocNode nodePep, SrmSettings settings)
            {
                _nodePep = nodePep;
                _settings = settings;
            }

            public bool HasTip
            {
                get { return PeptideTreeNode.HasPeptideTip(_nodePep, _settings); }
            }

            public Size RenderTip(Graphics g, Size sizeMax, bool draw)
            {
                return PeptideTreeNode.RenderTip(_nodePep, null,
                    _settings, g, sizeMax, draw);
            }
        }

        public override IEnumerable<DocNode> GetChoices(bool useFilter)
        {
            SrmSettings settings = DocSettings;

            List<DocNode> listPeptides = new List<DocNode>();
            foreach (var nodePep in DocNode.GetPeptideNodes(settings, useFilter))
                listPeptides.Add(nodePep.ChangeSettings(settings, SrmSettingsDiff.ALL));

            PeptideRankId rankId = DocSettings.PeptideSettings.Libraries.RankId;
            if (rankId != null && !DocNode.IsPeptideList)
                listPeptides = PeptideGroup.RankPeptides(listPeptides, settings, useFilter).ToList();

            MergeChosen(listPeptides, useFilter, node => ((PeptideDocNode)node).Key);

            return listPeptides;
        }

        protected override int GetPickInsertIndex(DocNode node, IList<DocNode> choices, int iFirst, int iLast)
        {
            var nodePep = (PeptideDocNode) node;
            for (int i = iFirst; i < iLast; i++)
            {
                var nodeNext = (PeptideDocNode) choices[i];
                // If the next node is later in order than the node to insert, then
                // insert before it.
                if (nodePep.Peptide.Begin.HasValue && nodeNext.Peptide.Begin.HasValue &&
                        nodePep.Peptide.Begin.Value < nodeNext.Peptide.Begin.Value)
                {
                    return i;
                }
                // If the next node is the same peptide and has explicit modifications,
                // insert before it.
                if (Equals(nodePep.Peptide, nodeNext.Peptide) && nodeNext.HasExplicitMods)
                {
                    return i;
                }
            }
            // Use the last possible insertion point.
            return iLast;
        }

        private static Peptide PeptideFromChoice(object choice)
        {
            return (choice != null ? ((PeptideDocNode)choice).Peptide : null);
        }

        public override bool ShowAutoManageChildren
        {
            get { return !((PeptideGroupDocNode) Model).IsPeptideList; }
        }

        #endregion

        #region ITipProvider Members

        private const string X80 =
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

        public override bool HasTip
        {
            get { return base.HasTip || (!ShowAnnotationTipOnly && DocNode.Id is FastaSequence); }
        }

        public override Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            var sizeInitial = base.RenderTip(g, sizeMax, draw);
            if (ShowAnnotationTipOnly)
                return sizeInitial;
            g.TranslateTransform(0, sizeInitial.Height);
            FastaSequence fastaSeq = DocNode.Id as FastaSequence;
            if (fastaSeq == null)
                return sizeInitial;

            using (RenderTools rt = new RenderTools())
            {
                SizeF sizeX80 = g.MeasureString(X80, rt.FontNormal);
                float widthLine = sizeX80.Width;
                float heightLine = sizeX80.Height;
                float heightMax = sizeMax.Height;
                float heightTotal = 0f;
                foreach (string description in Descriptions)
                {
                    SizeF sizeDesc = g.MeasureString(description, rt.FontNormal, (int)widthLine);
                    int heightDesc = (int)(sizeDesc.Height + heightLine / 2);
                    // If not enough room for this description, and one line of the
                    // sequence, just end with an elipsis.
                    if (heightTotal + heightDesc + heightLine > heightMax)
                    {
                        if (draw)
                            g.DrawString("...", rt.FontNormal, rt.BrushNormal, 0, heightTotal);
                        return TipSize(widthLine, heightTotal + heightLine);
                    }
                    if (draw)
                    {
                        g.DrawString(description, rt.FontNormal, rt.BrushNormal,
                                     new RectangleF(0, heightTotal, widthLine, heightMax - heightTotal));
                    }
                    heightTotal += heightDesc;
                }

                IList<DocNode> peptidesChoices = GetChoices(true).ToArray();
                HashSet<DocNode> peptidesChosen = new HashSet<DocNode>(Chosen);

                // Make sure all chosen peptides get listed
                HashSet<DocNode> setChoices = new HashSet<DocNode>(peptidesChoices);
                setChoices.UnionWith(peptidesChosen);
                var arrayChoices = setChoices.ToArray();
                Array.Sort(arrayChoices, (choice1, choice2) =>
                                         PeptideFromChoice(choice1).Order - PeptideFromChoice(choice2).Order);
                peptidesChoices = arrayChoices;

                // Get the selected peptide, if there is one
                PeptideTreeNode nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
                Peptide peptideSelected = (nodePepTree != null ? nodePepTree.DocNode.Peptide : null);

                int i = 0;
                string aa = fastaSeq.Sequence;
                const bool peptideList = false;
                while (i < aa.Length)
                {
                    // If this is not the last possible line, just render it.
                    if (heightTotal + heightLine * 2 <= heightMax)
                    {
                        i = RenderAALine(aa, peptideList, i, false, draw,
                                         peptidesChoices, peptidesChosen, peptideSelected,
                                         g, rt, heightTotal, widthLine);
                        heightTotal += heightLine;
                    }
                        // If not drawing, then this is the last possible line, and
                        // it will have content.
                    else if (!draw)
                    {
                        heightTotal += heightLine;
                        break;
                    }
                        // Otherwise, measure first, and then re-render, with an elipsis
                        // if the full sequence cannot be shown.
                    else
                    {
                        RenderAALine(aa, peptideList, i, false, false,
                                     peptidesChoices, peptidesChosen, peptideSelected,
                                     g, rt, heightTotal, widthLine);
                        RenderAALine(aa, peptideList, i, i < aa.Length, true,
                                     peptidesChoices, peptidesChosen, peptideSelected,
                                     g, rt, heightTotal, widthLine);
                        heightTotal += heightLine;
                        break;
                    }
                }

                return TipSize(Math.Max(widthLine, sizeInitial.Width), heightTotal + sizeInitial.Height);
            }
        }

        private IEnumerable<string> Descriptions
        {
            get
            {
                if (!string.IsNullOrEmpty(DocNode.Description))
                {
                    yield return DocNode.Description;
                    FastaSequence fastaSeq = DocNode.Id as FastaSequence;
                    if (fastaSeq != null)
                    {
                        foreach (string altText in fastaSeq.AlternativesText)
                            yield return altText;
                    }
                }
            }
        }

        private static int RenderAALine(string aa, bool peptideList, int start, bool elipsis, bool draw,
                                        IEnumerable<DocNode> peptidesChoices, ICollection<DocNode> peptidesChosen, Peptide peptideSelected,
                                        Graphics g, RenderTools rt, float height, float width)
        {
            IEnumerator<DocNode> peptides = peptidesChoices.GetEnumerator();
            DocNode choice = peptides.MoveNext() ? peptides.Current : null;
            bool chosen = peptidesChosen.Contains(choice);
            Peptide peptide = PeptideFromChoice(choice);

            if (elipsis)
            {
                SizeF sizeElipsis = g.MeasureString(" ...", rt.FontNormal);
                width -= sizeElipsis.Width;
            }

            float widthTotal = 0;
            PointF ptDraw = new PointF(widthTotal, height);
            if (peptideList && aa[start] == 'X')
                start++;
            for (int i = start; i < aa.Length; i++)
            {
                Font font = rt.FontNormal;
                Brush brush = rt.BrushNormal;
                if (peptide != null)
                {
                    while (peptide != null && i >= peptide.End)
                    {
                        choice = peptides.MoveNext() ? peptides.Current : null;
                        chosen = peptidesChosen.Contains(choice);
                        peptide = PeptideFromChoice(choice);
                    }
                    if (peptide != null && i >= peptide.Begin)
                    {
                        font = rt.FontBold;
                        if (Equals(peptide, peptideSelected))
                            brush = rt.BrushSelected;
                        else
                            brush = (chosen ? rt.BrushChosen : rt.BrushChoice);
                    }
                }
                if (peptideList && aa[i] == 'X')
                    return i + 1;
                string s = aa.Substring(i, 1);
                SizeF sizeAa = g.MeasureString(s, font);
                widthTotal += sizeAa.Width;
                if (widthTotal > width)
                {
                    if (elipsis && draw)
                        g.DrawString(" ...", rt.FontNormal, rt.BrushNormal, ptDraw);
                    return i;
                }
                widthTotal -= 4;    // Remove MeasureString padding.
                if (draw)
                {
                    g.DrawString(s, font, brush, ptDraw);
                    ptDraw.X = widthTotal;
                }
            }

            return aa.Length;
        }

        private static Size TipSize(float width, float height)
        {
            return new Size((int)width + 2, (int)height + 2);
        }

        #endregion

        #region IClipboardDataProvider Members

        protected override DataObject GetNodeData()
        {
            FastaSequence fastaSeq = DocNode.Id as FastaSequence;
            if (fastaSeq == null)
                return base.GetNodeData();

            DataObject data = new DataObject();
            data.SetData(DataFormats.Text, fastaSeq.FastaFileText);

            var sb = new StringBuilder();
            sb.Append("<b>").Append(Model.Id).Append("</b> ");
            sb.Append("<i>");
            if (string.IsNullOrEmpty(DocNode.Description))
                sb.AppendLine("<br/>");
            else
            {
                foreach (string desc in Descriptions)
                    sb.Append(desc).AppendLine("<br/>");
            }
            sb.Append("</i>");

            IEnumerator<DocNode> peptides = GetChoices(true).GetEnumerator();
            HashSet<DocNode> peptidesChosen = new HashSet<DocNode>(Chosen);
            PeptideDocNode nodePep = (PeptideDocNode)(peptides.MoveNext() ? peptides.Current : null);
            bool chosen = (nodePep != null && peptidesChosen.Contains(nodePep));

            bool inPeptide = false;
            string aa = fastaSeq.Sequence;
            for (int i = 0; i < aa.Length; i++)
            {
                if (nodePep != null)
                {
                    while (nodePep != null && i >= nodePep.Peptide.End)
                    {
                        nodePep = (PeptideDocNode)(peptides.MoveNext() ? peptides.Current : null);
                        if (nodePep != null)
                            chosen = peptidesChosen.Contains(nodePep);
                    }
                    if (nodePep != null && i >= nodePep.Peptide.Begin)
                    {
                        if (!inPeptide)
                        {
                            sb.Append(chosen
                                          ? "<font style=\"font-weight: bold; color: blue\">"
                                          : "<font style=\"font-weight: bold\">");
                            inPeptide = true;
                        }
                    }
                    else if (inPeptide)
                    {
                        sb.Append("</font>");
                        inPeptide = false;
                    }
                }
                sb.Append(aa[i]);
            }
            sb.Append("</font>");
            

            data.SetData(DataFormats.Html, HtmlFragment.ClipBoardText(sb.ToString()));                

            var transitionListExporter = new AbiMassListExporter(Document, DocNode);
            transitionListExporter.Export(null);
            data.SetData(DataFormats.CommaSeparatedValue,
                         transitionListExporter.MemoryOutput[AbstractMassListExporter.MEMORY_KEY_ROOT].ToString());

            return data;
        }

        #endregion
    }
}