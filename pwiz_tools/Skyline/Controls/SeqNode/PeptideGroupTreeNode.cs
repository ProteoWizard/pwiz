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
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.SeqNode
{
    public class PeptideGroupTreeNode : SrmTreeNodeParent
    {
        //public string PROTEIN_TITLE = "Protein";
        //public const string PEPTIDE_LIST_TITLE = "Peptide List";

        public static PeptideGroupTreeNode CreateInstance(SequenceTree tree, DocNode nodeDoc)
        {
            var nodeTree = new PeptideGroupTreeNode(tree, (PeptideGroupDocNode)nodeDoc);

            if (tree.ExpandProteins)
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
            get 
            {
                return Model.Id is FastaSequence
                    ? Resources.PeptideGroupTreeNode_Heading_Protein
                    : Resources.PeptideGroupTreeNode_Heading_Peptide_List;
            }
        }

        public override string ChildHeading  // TODO(bspratt) distinguish Molecule from Peptide
        {
            get { return string.Format(Resources.PeptideGroupTreeNode_ChildHeading__0__, Text); }
        }

        public override string ChildUndoHeading // TODO(bspratt) distinguish Molecule from Peptide
        {
            get { return string.Format(Resources.PeptideGroupTreeNode_ChildUndoHeading__0__, Text); }
        }

        protected override void OnModelChanged()
        {
            int typeImageIndex = TypeImageIndex;
            if (typeImageIndex != ImageIndex)
                ImageIndex = SelectedImageIndex = typeImageIndex;
            string label = ProteinModalDisplayText((PeptideGroupDocNode) Model);
            if (!string.Equals(label, Text))
                Text = label;

            // Make sure children are up to date
            OnUpdateChildren(SequenceTree.ExpandProteins);
        }

        public int TypeImageIndex
        {
            get
            {
                if (DocNode.IsProtein)
                    return (int) SequenceTree.ImageId.protein; 
                else if (DocNode.IsEmpty)
                    return (int)SequenceTree.ImageId.empty_list;
                else if (DocNode.IsNonProteomic)
                    return (int)SequenceTree.ImageId.molecule_list;
                else if (DocNode.IsDecoy)
                    return (int) SequenceTree.ImageId.protein_decoy;
                else
                    return (int)SequenceTree.ImageId.peptide_list;
            }
        }

        protected override void UpdateChildren(bool materialize)
        {
            UpdateNodes(SequenceTree, Nodes, DocNode.Children, materialize, PeptideTreeNode.CreateInstance);
        }

        public static ProteinDisplayMode ProteinsDisplayMode(string displayProteinsMode)
        {
            return Helpers.ParseEnum(displayProteinsMode, ProteinDisplayMode.ByName); 
        }

        public static string ProteinModalDisplayText(ProteinMetadata metadata, string displayProteinsMode)
        {
            return ProteinModalDisplayText(metadata, ProteinsDisplayMode(displayProteinsMode));
        }

        public static string ProteinModalDisplayText(PeptideGroupDocNode node)
        {
            return ProteinModalDisplayText(node.ProteinMetadata, Settings.Default.ShowPeptidesDisplayMode);
        }

        public static string ProteinModalDisplayText(ProteinMetadata metadata, ProteinDisplayMode displayProteinsMode)
        {
            switch (displayProteinsMode)
            {
                case ProteinDisplayMode.ByAccession:
                case ProteinDisplayMode.ByPreferredName:
                case ProteinDisplayMode.ByGene:
                    break;
                default:
                    return metadata.Name;  
            }

            // If the desired field is not populated because it's not yet searched, say so
            if (metadata.NeedsSearch())
                return Resources.ProteinMetadataManager_LookupProteinMetadata_resolving_protein_details;

            // If the desired field is not populated, return something like "<name: YAL01234>"
            var failsafe = String.Format(Resources.PeptideGroupTreeNode_ProteinModalDisplayText__name___0__, metadata.Name);
            switch (displayProteinsMode)
            {
                case ProteinDisplayMode.ByAccession:
                    return metadata.Accession ?? failsafe;
                case ProteinDisplayMode.ByPreferredName:
                    return metadata.PreferredName ?? failsafe;
                case ProteinDisplayMode.ByGene:
                    return metadata.Gene ?? failsafe;
            }
            return failsafe;
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
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"; // Not L10N

        public override bool HasTip
        {
            get { return base.HasTip || (!ShowAnnotationTipOnly); }
        }

        public override Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            var sizeInitial = base.RenderTip(g, sizeMax, draw);
            if (ShowAnnotationTipOnly)
                return sizeInitial;
            g.TranslateTransform(0, sizeInitial.Height);
            FastaSequence fastaSeq = DocNode.Id as FastaSequence;

            var tableDetails = new TableDesc();
            using (RenderTools rt = new RenderTools())
            {
                SizeF sizeX80 = g.MeasureString(X80, rt.FontNormal);
                float widthLine = sizeX80.Width;
                float heightLine = sizeX80.Height;
                float heightMax = sizeMax.Height;
                tableDetails.AddDetailRow(Resources.PeptideGroupTreeNode_RenderTip_Name, DocNode.Name, rt);
                // If current name isn't the original, show that.
                if (DocNode.PeptideGroup.Name != null && !Equals(DocNode.Name, DocNode.PeptideGroup.Name))
                    tableDetails.AddDetailRow(Resources.PeptideGroupTreeNode_RenderTip_Original_Name, DocNode.OriginalName, rt);
                if (!String.IsNullOrEmpty(DocNode.ProteinMetadata.Accession))
                    tableDetails.AddDetailRow(Resources.PeptideGroupTreeNode_RenderTip_Accession, DocNode.ProteinMetadata.Accession, rt);
                if (!String.IsNullOrEmpty(DocNode.ProteinMetadata.PreferredName))
                    tableDetails.AddDetailRow(Resources.PeptideGroupTreeNode_RenderTip_Preferred_Name, DocNode.ProteinMetadata.PreferredName, rt);
                if (!String.IsNullOrEmpty(DocNode.ProteinMetadata.Gene))
                    tableDetails.AddDetailRow(Resources.PeptideGroupTreeNode_RenderTip_Gene, DocNode.ProteinMetadata.Gene, rt);
                if (!String.IsNullOrEmpty(DocNode.ProteinMetadata.Species))
                    tableDetails.AddDetailRow(Resources.PeptideGroupTreeNode_RenderTip_Species, DocNode.ProteinMetadata.Species, rt);
                if (!String.IsNullOrEmpty(DocNode.ProteinMetadata.DisplaySearchHistory()))
                    tableDetails.AddDetailRow(Resources.PeptideGroupTreeNode_RenderTip_Searched, DocNode.ProteinMetadata.DisplaySearchHistory(), rt);
                if (!String.IsNullOrEmpty(DocNode.ProteinMetadata.Description))
                    tableDetails.AddDetailRowLineWrap(g,Resources.PeptideGroupTreeNode_RenderTip_Description, DocNode.ProteinMetadata.Description, rt);
                if (DocNode.PeptideGroup.Description != null && !Equals(DocNode.Description, DocNode.PeptideGroup.Description))
                    tableDetails.AddDetailRow(Resources.PeptideGroupTreeNode_RenderTip_Original_Description, DocNode.OriginalDescription, rt);
                SizeF sizeDetails = tableDetails.CalcDimensions(g);
                sizeDetails.Height += TableDesc.TABLE_SPACING;    // Spacing between details and fragments
                float heightTotal = sizeDetails.Height;
                widthLine = Math.Max(widthLine, sizeDetails.Width);

                if (draw)
                {
                    tableDetails.Draw(g);
                }

                if (fastaSeq != null)
                {
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
                SizeF sizeElipsis = g.MeasureString(" ...", rt.FontNormal); // Not L10N
                width -= sizeElipsis.Width;
            }

            float widthTotal = 0;
            PointF ptDraw = new PointF(widthTotal, height);
            if (peptideList && aa[start] == 'X') // Not L10N: For amino acid comparison
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
                if (peptideList && aa[i] == 'X') // Not L10N: For amino acid comparison
                    return i + 1;
                string s = aa.Substring(i, 1);
                SizeF sizeAa = g.MeasureString(s, font);
                widthTotal += sizeAa.Width;
                if (widthTotal > width)
                {
                    if (elipsis && draw)
                        g.DrawString(" ...", rt.FontNormal, rt.BrushNormal, ptDraw); // Not L10N
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
            string fastaText = fastaSeq.FastaFileText;
            // If protein has been renamed in the UI
            if (!Equals(DocNode.Name, fastaSeq.Name))
            {
                int oldNameLen = (fastaSeq.Name != null ? fastaSeq.Name.Length : 0);
                fastaText = ">" + DocNode.Name + fastaText.Substring(oldNameLen + 1); // Not L10N
            }
            data.SetData(DataFormats.Text, fastaText);

            var sb = new StringBuilder();
            sb.Append("<b>").Append(DocNode.Name).Append("</b> "); // Not L10N
            sb.Append("<i>"); // Not L10N
            if (string.IsNullOrEmpty(DocNode.Description)) // CONSIDER bspratt - a more complete set of data here, maybe - accession etc
                sb.AppendLine("<br/>"); // Not L10N
            else
            {
                foreach (string desc in Descriptions)
                    sb.Append(desc).AppendLine("<br/>"); // Not L10N
            }
            sb.Append("</i>"); // Not L10N

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
                        {
                            bool chosenNew = peptidesChosen.Contains(nodePep);
                            // Need a new font tag, if the chosen state is changing
                            if (chosenNew != chosen)
                            {
                                sb.Append("</font>"); // Not L10N
                                inPeptide = false;
                            }
                            chosen = chosenNew;
                        }
                    }
                    if (nodePep != null && i >= nodePep.Peptide.Begin)
                    {
                        if (!inPeptide)
                        {
                            sb.Append(chosen
                                          ? "<font style=\"font-weight: bold; color: blue\">" // Not L10N
                                          : "<font style=\"font-weight: bold\">"); // Not L10N
                            inPeptide = true;
                        }
                    }
                    else if (inPeptide)
                    {
                        sb.Append("</font>"); // Not L10N
                        inPeptide = false;
                    }
                }
                sb.Append(aa[i]);
            }
            if (inPeptide)
                sb.Append("</font>"); // Not L10N
            

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