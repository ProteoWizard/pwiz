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
    public class PeptideGroupTreeNode : SrmTreeNodeParent, ITipProvider, IClipboardDataProvider
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
            string label = DocNode.Name + DocNode.NoteMark;
            if (!string.Equals(label, Text))
                Text = label;

            // Make sure children are up to date
            OnUpdateChildren(ExpandDefault);
        }

        public int TypeImageIndex
        {
            get { return (int)SequenceTree.ImageId.protein; }
        }

        protected override void UpdateChildren(bool materialize)
        {
            UpdateNodes<PeptideTreeNode>(SequenceTree, Nodes, DocNode.Children, materialize, PeptideTreeNode.CreateInstance);
        }

        #region IChildPicker Members

        // TODO: GetPickLabel with library information e.g. (2 charges, 34 copies)

        public override bool CanShow
        {
            get
            {
                return DocNode.Id is FastaSequence;
            }
        }

        public override bool Filtered
        {
            get { return Settings.Default.FilterPeptides; }
            set { Settings.Default.FilterPeptides = value; }
        }

        public override bool Equivalent(object choice1, object choice2)
        {
            if (choice1 is PeptideDocNode && choice2 is PeptideDocNode)
                return Equals(((PeptideDocNode)choice1).Id, ((PeptideDocNode)choice2).Id);
            return base.Equivalent(choice1, choice2);
        }

        public override IEnumerable<object> GetChoices(bool useFilter)
        {
            FastaSequence fastaSeq = DocNode.Id as FastaSequence;
            if (fastaSeq != null)
            {
                SrmSettings settings = DocSettings;

                PeptideRankId rankId = DocSettings.PeptideSettings.Libraries.RankId;
                if (rankId == null)
                {
                    foreach (Peptide peptide in fastaSeq.GetPeptides(settings, useFilter))
                        yield return peptide;                    
                }
                else
                {
                    IList<DocNode> listPeptides = new List<DocNode>();
                    foreach (Peptide peptide in fastaSeq.GetPeptides(settings, true))
                    {
                        PeptideDocNode nodePeptide = new PeptideDocNode(peptide, new TransitionGroupDocNode[0]);
                        listPeptides.Add(nodePeptide.ChangeSettings(settings, SrmSettingsDiff.ALL));
                    }
                    listPeptides = PeptideGroupDocNode.RankPeptides(listPeptides, settings, useFilter);

                    // If not filtered, the ranked filtered peptides need to be merged into the
                    // unfiltered list.
                    if (useFilter)
                    {
                        foreach (var nodePeptide in listPeptides)
                            yield return nodePeptide;
                    }
                    else
                    {
                        IEnumerator<DocNode> enumPeptides = listPeptides.GetEnumerator();
                        bool hasNext = enumPeptides.MoveNext();
                        foreach (Peptide peptide in fastaSeq.GetPeptides(settings, false))
                        {
                            if (hasNext && Equals(peptide, enumPeptides.Current.Id))
                            {
                                yield return enumPeptides.Current;
                                hasNext = enumPeptides.MoveNext();
                            }
                            else
                            {
                                yield return new PeptideDocNode(peptide, new TransitionGroupDocNode[0]);
                            }
                        }
                    }
                }
            }
        }

        public override IEnumerable<object> Chosen
        {
            get
            {
                if (DocSettings.PeptideSettings.Libraries.RankId != null)
                    return DocNode.Children.ToArray();
                return base.Chosen;
            }
        }

        private static Peptide PeptideFromChoice(object choice)
        {
            var nodePeptide = choice as PeptideDocNode;
            if (nodePeptide != null)
                return nodePeptide.Peptide;
            return choice as Peptide;
        }

        public override IPickedList CreatePickedList(IEnumerable<object> chosen, bool autoManageChildren)
        {
            return new PeptidePickedList(DocSettings, chosen, autoManageChildren);
        }

        public override bool ShowAutoManageChildren
        {
            get { return !((PeptideGroupDocNode) Model).IsPeptideList; }
        }

        private sealed class PeptidePickedList : AbstractPickedList
        {
            private readonly bool _ranked;

            public PeptidePickedList(SrmSettings settings, IEnumerable<object> picked, bool autoManageChildren)
                : base(settings, picked, autoManageChildren)
            {
                _ranked = (settings.PeptideSettings.Libraries.RankId != null);
            }

            public override DocNode CreateChildNode(Identity childId)
            {
                if (_ranked)
                {
                    foreach (DocNode node in Picked)
                    {
                        if (Equals(childId, node.Id))
                            return node;
                    }
                }

                Peptide peptide = (Peptide) childId;
                var nodePeptide = new PeptideDocNode(peptide, new TransitionGroupDocNode[0]);
                return nodePeptide.ChangeSettings(Settings, SrmSettingsDiff.ALL);
            }

            public override Identity GetId(object pick)
            {
                return _ranked ? ((DocNode) pick).Id : (Identity) pick;
            }
        }

        #endregion

        #region ITipProvider Members

        private const string X80 =
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

        public bool HasTip
        {
            get { return DocNode.Id is FastaSequence; }
        }

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            FastaSequence fastaSeq = DocNode.Id as FastaSequence;
            if (fastaSeq == null)
                return new Size(0, 0);

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

                IEnumerable<object> peptidesChoices = GetChoices(true);
                HashSet<object> peptidesChosen = new HashSet<object>(Chosen);

                // Make sure all chosen peptides get listed
                HashSet<object> setChoices = new HashSet<object>(peptidesChoices);
                setChoices.UnionWith(peptidesChosen);
                var arrayChoices = setChoices.ToArray();
                Array.Sort(arrayChoices, (choice1, choice2) =>
                                         PeptideFromChoice(choice1).Begin.Value - PeptideFromChoice(choice2).Begin.Value);
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
                        if (i < aa.Length)
                        {
                            RenderAALine(aa, peptideList, i, true, true,
                                         peptidesChoices, peptidesChosen, peptideSelected,
                                         g, rt, heightTotal, widthLine);
                        }
                        else
                        {
                            RenderAALine(aa, peptideList, i, false, true,
                                         peptidesChoices, peptidesChosen, peptideSelected,
                                         g, rt, heightTotal, widthLine);
                        }
                        heightTotal += heightLine;
                        break;
                    }
                }

                return TipSize(widthLine, heightTotal);
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
                        foreach (AlternativeProtein alternative in fastaSeq.Alternatives)
                            yield return string.Format("{0} {1}", alternative.Name, alternative.Description);
                    }
                }
            }
        }

        private static int RenderAALine(string aa, bool peptideList, int start, bool elipsis, bool draw,
                                        IEnumerable<object> peptidesChoices, ICollection<object> peptidesChosen, Peptide peptideSelected,
                                        Graphics g, RenderTools rt, float height, float width)
        {
            IEnumerator<object> peptides = peptidesChoices.GetEnumerator();
            object choice = peptides.MoveNext() ? peptides.Current : null;
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

        public void ProvideData()
        {
            DataObject data = new DataObject();
            StringBuilder sb = new StringBuilder();
            FastaSequence fastaSeq = DocNode.Id as FastaSequence;
            if (fastaSeq == null)
            {
                foreach (PeptideDocNode nodePeptide in DocNode.Children)
                    sb.Append(nodePeptide.Peptide.Sequence).AppendLine();
                data.SetData(DataFormats.Text, sb.ToString());
            }
            else
            {
                sb.Append(">").Append(Model.Id).Append(" ");
                foreach (string desc in Descriptions)
                    sb.Append(desc).Append((char)1);

                string aa = fastaSeq.Sequence;
                for (int i = 0; i < aa.Length; i++)
                {
                    if (i % 60 == 0)
                        sb.AppendLine();
                    sb.Append(aa[i]);
                }
                sb.AppendLine("*");
                data.SetData(DataFormats.Text, sb.ToString());

                sb = new StringBuilder();
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

                IEnumerator<object> peptides = GetChoices(true).GetEnumerator();
                HashSet<object> peptidesChosen = new HashSet<object>(Chosen);
                Peptide peptide = (Peptide)(peptides.MoveNext() ? peptides.Current : null);
                bool chosen = peptidesChosen.Contains(peptide);

                bool inPeptide = false;
                for (int i = 0; i < aa.Length; i++)
                {
                    if (peptide != null)
                    {
                        while (peptide != null && i >= peptide.End)
                        {
                            peptide = (Peptide)(peptides.MoveNext() ? peptides.Current : null);
                            chosen = peptidesChosen.Contains(peptide);
                        }
                        if (peptide != null && i >= peptide.Begin)
                        {
                            if (!inPeptide)
                            {
                                if (chosen)
                                    sb.Append("<font style=\"font-weight: bold; color: blue\">");
                                else
                                    sb.Append("<font style=\"font-weight: bold\">");
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
                sb.AppendLine();

                data.SetData(DataFormats.Html, HtmlFragment.ClipBoardText(sb.ToString()));                
            }

            var transitionListExporter = new AbiMassListExporter(Document, DocNode);
            transitionListExporter.Export(null);
            data.SetData(DataFormats.CommaSeparatedValue,
                         transitionListExporter.MemoryOutput[MassListExporter.MEMORY_KEY_ROOT].ToString());

            Clipboard.Clear();
            Clipboard.SetDataObject(data);
        }

        #endregion
    }
}