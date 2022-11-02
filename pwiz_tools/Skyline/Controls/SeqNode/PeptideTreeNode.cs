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
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.SeqNode
{
    public class PeptideTreeNode : SrmTreeNodeParent
    {
        /// <summary>
        /// Peptide
        /// </summary>
        public static string TITLE { get { return Resources.PeptideTreeNode_Heading_Title; } }

        public static PeptideTreeNode CreateInstance(SequenceTree tree, DocNode nodeDoc)
        {
            Debug.Assert(nodeDoc is PeptideDocNode);
            var nodeTree = new PeptideTreeNode(tree, (PeptideDocNode)nodeDoc);
            if (tree.ExpandPeptides)
                nodeTree.Expand();
           return nodeTree;
        }

// ReSharper disable SuggestBaseTypeForParameter
        public PeptideTreeNode(SequenceTree tree, PeptideDocNode nodePeptide)
// ReSharper restore SuggestBaseTypeForParameter
            : base(tree, nodePeptide)
        {

        }

        public PeptideDocNode DocNode => (PeptideDocNode)Model;
        public PeptideGroupDocNode PepGroupNode => ((PeptideGroupTreeNode)Parent)?.DocNode;

        public override string Heading
        {
            get { return  DocNode.IsProteomic ? Resources.PeptideTreeNode_Heading_Title : Resources.PeptideTreeNode_Heading_Title_Molecule; }
        }

        public override string ChildHeading
        {
            get { return string.Format(Resources.PeptideTreeNode_ChildHeading__0__, Text); }
        }

        public override string ChildUndoHeading
        {
            get { return string.Format(Resources.PeptideTreeNode_ChildUndoHeading__0__, Text); }
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
            var nodePep = (PeptideDocNode) Model;
            string label = DisplayText(nodePep, SequenceTree.GetDisplaySettings(nodePep));
            if (!string.Equals(label, Text))
                Text = label;
            // Hard to tell what might cause label formatting to change
            _textSequences = null;

            // Make sure children are up to date
            OnUpdateChildren(SequenceTree.ExpandPeptides);
        }

        public override bool CanShow
        {
            get
            {
                return (!DocNode.IsDecoy && base.CanShow);
            }
        }

        public int TypeImageIndex
        {
            get { return GetTypeImageIndex(DocNode); }
        }

        public static Image GetTypeImage(PeptideDocNode nodePep, SequenceTree sequenceTree)
        {
            return sequenceTree.ImageList.Images[GetTypeImageIndex(nodePep)];
        }

        // This is the set of node images that have peptide/molecule specific versions
        private static readonly Dictionary<SequenceTree.ImageId, SequenceTree.ImageId> NON_PROTEOMIC_IMAGE_INDEXES =
            new Dictionary<SequenceTree.ImageId, SequenceTree.ImageId>
            {
                {SequenceTree.ImageId.peptide_irt_lib, SequenceTree.ImageId.molecule_irt_lib},
                {SequenceTree.ImageId.peptide_irt, SequenceTree.ImageId.molecule_irt},
                {SequenceTree.ImageId.peptide_standard_lib, SequenceTree.ImageId.molecule_standard_lib},
                {SequenceTree.ImageId.peptide_standard, SequenceTree.ImageId.molecule_standard},
                {SequenceTree.ImageId.peptide_lib, SequenceTree.ImageId.molecule_lib},
                {SequenceTree.ImageId.peptide, SequenceTree.ImageId.molecule}
            };   

        private static int GetTypeImageIndex(PeptideDocNode nodePep)
        {
            SequenceTree.ImageId index;
            if (nodePep.IsDecoy)
            {
                index = nodePep.HasLibInfo
                                  ? SequenceTree.ImageId.peptide_lib_decoy
                                  : SequenceTree.ImageId.peptide_decoy;
            }
            else if (nodePep.GlobalStandardType == StandardType.IRT)
            {
                index = nodePep.HasLibInfo
                                  ? SequenceTree.ImageId.peptide_irt_lib
                                  : SequenceTree.ImageId.peptide_irt;
            }
            else if (nodePep.GlobalStandardType == StandardType.QC)
            {
                index = nodePep.HasLibInfo
                    ? SequenceTree.ImageId.peptide_qc_lib
                    : SequenceTree.ImageId.peptide_qc;
            }
            else if (nodePep.GlobalStandardType == StandardType.GLOBAL_STANDARD 
                  || nodePep.GlobalStandardType == StandardType.SURROGATE_STANDARD)
            {
                index = nodePep.HasLibInfo
                                  ? SequenceTree.ImageId.peptide_standard_lib
                                  : SequenceTree.ImageId.peptide_standard;
            }
            else
            {
                index = nodePep.HasLibInfo
                    ?  SequenceTree.ImageId.peptide_lib
                    :  SequenceTree.ImageId.peptide;
            }

            // If this is a small molecule node, see if there's a special version of its image
            if (!nodePep.IsProteomic && NON_PROTEOMIC_IMAGE_INDEXES.TryGetValue(index, out var smallMolIndex))
            {
                return (int)smallMolIndex;
            }
            return (int)index;
        }

        public int PeakImageIndex
        {
            get { return GetPeakImageIndex(DocNode, SequenceTree); }
        }

        public static Image GetPeakImage(PeptideDocNode nodePep, SequenceTree sequenceTree)
        {
            int imageIndex = GetPeakImageIndex(nodePep, sequenceTree);
            return (imageIndex != -1 ? sequenceTree.StateImageList.Images[imageIndex] : null);
        }

        public static int GetPeakImageIndex(PeptideDocNode nodePep, SequenceTree sequenceTree)
        {
            var settings = sequenceTree.Document.Settings;
            if (!settings.HasResults)
                return -1;

            int index = sequenceTree.GetDisplayResultsIndex(nodePep);

            float? ratio = (nodePep.HasResults ? nodePep.GetPeakCountRatio(index) : null);
            if (ratio == null)
                return (int)SequenceTree.StateImageId.peak_blank;
            if (ratio < 0.5)
                return (int)SequenceTree.StateImageId.no_peak;
            if (ratio < 1.0)
                return (int)SequenceTree.StateImageId.keep;

            return (int)SequenceTree.StateImageId.peak;                            
        }

        public string ResultsText
        {
            get { return string.Empty; } 
        }

        public static string GetLabel(PeptideDocNode nodePep, string resultsText)
        {
            return nodePep + resultsText;
        }

        public override Color? ChromColor
        {
            get { return SequenceTree.GetPeptideGraphInfo(Model).Color; }
        }

        protected override void UpdateChildren(bool materialize)
        {
            UpdateNodes(SequenceTree, Nodes, DocNode.Children, materialize,
                                                 TransitionGroupTreeNode.CreateInstance);
        }

        private TextSequence[] _textSequences;

        private const TextFormatFlags FORMAT_TEXT_SEQUENCE = TextFormatFlags.SingleLine |
                                                             TextFormatFlags.NoPadding |
                                                             TextFormatFlags.VerticalCenter;

        private bool IsMeasured
        {
            get
            {
                return _textSequences != null && ReferenceEquals(_widthText, Text)
                    && _textZoomFactor == Settings.Default.TextZoom 
                    && ReferenceEquals(ModFontHolder.GetModColors(), _groupColors);
            }
        }

        private TextSequence[] GetTextSequences(IDeviceContext g)
        {
            if (!IsMeasured)
            {
                _textSequences = CreateTextSequences(DocNode, DocSettings, Text, g, SequenceTree.ModFonts);
                _widthText = Text;
                _textZoomFactor = Settings.Default.TextZoom;
                _groupColors = ModFontHolder.GetModColors();
            }
            return _textSequences;
        }

        private TextSequence[] CreateTextSequences(IDeviceContext g)
        {
            return CreateTextSequences(DocNode, DocSettings, Text, g, SequenceTree.ModFonts);
        }

        public static TextSequence[] CreateTextSequences(PeptideDocNode nodePep,
            SrmSettings settings, string label, IDeviceContext g, ModFontHolder fonts)
        {
            // Store text and font information for all label types
            bool heavyMods = false;
            var listTypeSequences = new List<TextSequence> { CreateTypeTextSequence(nodePep, settings, IsotopeLabelType.light, fonts) };
            foreach (var labelType in settings.PeptideSettings.Modifications.GetHeavyModificationTypes())
            {
                // Only color for the label types actually measured in this peptide
                if (!nodePep.HasChildType(labelType))
                    continue;

                var textSequence = CreateTypeTextSequence(nodePep, settings, labelType, fonts);                
                listTypeSequences.Add(textSequence);
                heavyMods = (heavyMods || textSequence != null);
            }

            // Calculate text sequence values for the peptide display string
            var listTextSequences = new List<TextSequence>();
            if (nodePep.Peptide.IsCustomMolecule)
                listTextSequences.Add(CreatePlainTextSequence(label, fonts));
            // If no modifications, use a single plain text sequence
            else if (!heavyMods && !listTypeSequences[0].Text.Contains(@"[") && !nodePep.CrosslinkStructure.HasCrosslinks) // For identifying modifications
                listTextSequences.Add(CreatePlainTextSequence(label, fonts));
            else
            {
                var peptideFormatter = PeptideFormatter.MakePeptideFormatter(settings, nodePep, fonts);
                string pepSequence = peptideFormatter.UnmodifiedSequence;
                int startPep = label.IndexOf(pepSequence, StringComparison.Ordinal);
                int endPep = startPep + pepSequence.Length;


                IEnumerable<TextSequence> rawTextSequences = new TextSequence[0];
                // Add prefix plain-text if necessary
                if (startPep > 0)
                {
                    string prefix = label.Substring(0, startPep);
                    rawTextSequences = rawTextSequences.Append(CreatePlainTextSequence(prefix, fonts));
                }
                    
                rawTextSequences = rawTextSequences.Concat(Enumerable.Range(0, pepSequence.Length).Select(aaIndex=>peptideFormatter.GetTextSequenceAtAaIndex(DisplayModificationOption.Current, aaIndex)));

                rawTextSequences = rawTextSequences.Concat(peptideFormatter.GetTextSequencesForLinkedPeptides(DisplayModificationOption.Current));

                if (endPep < label.Length)
                {
                    string suffix = label.Substring(endPep);
                    rawTextSequences = rawTextSequences.Append(CreatePlainTextSequence(suffix, fonts));
                }
                listTextSequences.AddRange(TextSequence.Coalesce(rawTextSequences));
            }

            if (g != null)
            {
                // Calculate placement for each text sequence
                int textRectWidth = 0;
                foreach (var textSequence in listTextSequences)
                {
                    Size sizeMax = new Size(int.MaxValue, int.MaxValue);
                    textSequence.Position = textRectWidth;
                    textSequence.Width = TextRenderer.MeasureText(g, textSequence.Text,
                                                                  textSequence.Font, sizeMax, FORMAT_TEXT_SEQUENCE).
                        Width;
                    textRectWidth += textSequence.Width;
                }
            }

            return listTextSequences.ToArray();            
        }

        /// <summary>
        /// Increments a single amino acid index to the next amino acid in its
        /// text sequence, returning any intervening modification specification.
        /// </summary>
        /// <param name="i">Index of the text sequence being considered</param>
        /// <param name="indexes">Index locations of the amino acid in each text sequence</param>
        /// <param name="textSequences">List of text sequences for all label types being considered</param>
        /// <returns>Modification specification between the starting and ending amino acids,
        ///          or null if none is found</returns>
        private static string NextAA(int i, int[] indexes, IList<TextSequence> textSequences)
        {
            // Return null, if there is no text sequence information for this type
            if (textSequences[i] == null)
                return null;

            // Increment the index, and check for modification string after the amino acid
            int iNext = ++indexes[i];
            string text = textSequences[i].Text;
            if (iNext >= text.Length || text[iNext] != '[')
                return null;

            // Find modification end character
            int iEndMod = text.IndexOf(']', iNext);
            // Be unnecessarily safe, and do something reasonable, if no end found
            if (iEndMod == -1)
                iEndMod = text.Length - 1;
            // Increment to the next AA
            iEndMod++;
            indexes[i] = iEndMod;
            // Return the full modification string
            return text.Substring(iNext, iEndMod - iNext);
        }

        /// <summary>
        /// Creates a text sequence with the fully modified peptide sequence text
        /// and font information for a given label type.
        /// </summary>
        private static TextSequence CreateTypeTextSequence(PeptideDocNode nodePep, SrmSettings settings,
            IsotopeLabelType labelType, ModFontHolder fonts)
        {
            var calc = settings.TryGetPrecursorCalc(labelType, nodePep.ExplicitMods);
            if (calc == null)
                return null;

            return new TextSequence
                       {
                           Text = nodePep.IsProteomic
                               ? calc.GetModifiedSequence(nodePep.Peptide.Target, SequenceModFormatType.mass_diff_narrow, false).Sequence
                               : nodePep.CustomMolecule.DisplayName,
                           Font = fonts.GetModFont(labelType),
                           Color = ModFontHolder.GetModColor(labelType)
                       };
        }

        /// <summary>
        /// Creates a text sequence with normal font.
        /// </summary>
        public static TextSequence CreatePlainTextSequence(string text, ModFontHolder fonts)
        {
            return new TextSequence
                       {
                           Text = text,
                           Font = fonts.Plain,
                           Color = Color.Black,
                           IsPlainText = true
                       };
        }
        
        protected override int WidthCustom
        {
            get
            {
                if (!IsMeasured)
                    return Bounds.Width;

                var lastTextSequence = _textSequences[_textSequences.Length - 1];
                return lastTextSequence.Position + lastTextSequence.Width +
                    TreeViewMS.PADDING*2;
            }
        }

        protected override void EnsureWidthCustom(Graphics g)
        {
            GetTextSequences(g);
        }

        protected override void DrawTextMS(Graphics g)
        {
            DrawTextBackground(g);
            DrawPeptideText(DocNode, DocSettings, GetTextSequences(g), g, BoundsMS,
                SequenceTree.ModFonts, ForeColorMS, BackColorMS);
            DrawFocus(g);
            DrawAnnotationIndicator(g);
        }

        public static void DrawPeptideText(PeptideDocNode nodePep,
                                           SrmSettings settings,
                                           IEnumerable<TextSequence> textSequences,
                                           Graphics g,
                                           Rectangle bounds,
                                           ModFontHolder fonts,
                                           Color foreColor,
                                           Color backColor)
        {
            if (textSequences == null)
                textSequences = CreateTextSequences(nodePep, settings, GetLabel(nodePep, string.Empty), g, fonts);
            Rectangle rectDraw = new Rectangle(0, bounds.Y, 0, bounds.Height);
            foreach (var textSequence in textSequences)
            {
                rectDraw.X = textSequence.Position + bounds.X + TreeViewMS.PADDING;
                rectDraw.Width = textSequence.Width;
                // Use selection highlight color, if the background is highlight.
                if (backColor != SystemColors.Highlight)
                    foreColor = textSequence.Color;
                TextRenderer.DrawText(g, textSequence.Text, textSequence.Font, rectDraw,
                    foreColor, backColor, FORMAT_TEXT_SEQUENCE);
            }
        }
        
        #region IChildPicker Members

        public override string GetPickLabel(DocNode child)
        {
            return TransitionGroupTreeNode.DisplayText((TransitionGroupDocNode)child, 
                SequenceTree.GetDisplaySettings((PeptideDocNode) Model));
        }

        public override Image GetPickTypeImage(DocNode child)
        {
            return TransitionGroupTreeNode.GetTypeImage((TransitionGroupDocNode) child, SequenceTree);
        }

        public override Image GetPickPeakImage(DocNode child)
        {
            return TransitionGroupTreeNode.GetPeakImage((TransitionGroupDocNode) child, DocNode, SequenceTree);
        }

        public override ITipProvider GetPickTip(DocNode child)
        {
            return new PickTransitionGroupTip(DocNode, (TransitionGroupDocNode) child, DocSettings);
        }

        private sealed class PickTransitionGroupTip : ITipProvider
        {
            private readonly PeptideDocNode _nodePep;
            private readonly TransitionGroupDocNode _nodeGroup;
            private readonly SrmSettings _settings;

            public PickTransitionGroupTip(PeptideDocNode nodePep,
                TransitionGroupDocNode nodeGroup, SrmSettings settings)
            {
                _nodePep = nodePep;
                _nodeGroup = nodeGroup;
                _settings = settings;
            }

            public bool HasTip { get { return true; } }

            public Size RenderTip(Graphics g, Size sizeMax, bool draw)
            {
                return TransitionGroupTreeNode.RenderTip(_nodePep, _nodeGroup, null,
                    _settings, g, sizeMax, draw);
            }
        }

        public override bool Filtered
        {
            get { return Settings.Default.FilterTransitionGroups; }
            set { Settings.Default.FilterTransitionGroups = value; }
        }

        public override IEnumerable<DocNode> GetChoices(bool useFilter)
        {
            var mods = DocNode.ExplicitMods;
            var listChildrenNew = new List<DocNode>();
            foreach (TransitionGroup group in DocNode.GetTransitionGroups(DocSettings, mods, useFilter))
            {
                // The maximum allowable precursor charge may be larger than it makes sense to show.
                var charges = group.IsProteomic
                    ? DocSettings.TransitionSettings.Filter.PeptidePrecursorCharges
                    : DocSettings.TransitionSettings.Filter.SmallMoleculePrecursorAdducts;
                if (Math.Abs(group.PrecursorAdduct.AdductCharge) <= TransitionGroup.MAX_PRECURSOR_CHARGE_PICK || charges.Contains(group.PrecursorAdduct))
                {
                    var nodeChoice = CreateChoice(group, mods);
                    if (!useFilter || DocSettings.TransitionSettings.Libraries.HasMinIonCount(nodeChoice))
                        listChildrenNew.Add(nodeChoice);
                }
            }
            var nodePep = (PeptideDocNode)DocNode.ChangeChildren(listChildrenNew);
            nodePep.ChangeSettings(DocSettings, SrmSettingsDiff.PROPS);
            listChildrenNew = new List<DocNode>(nodePep.Children);
            MergeChosen(listChildrenNew, useFilter);
            return listChildrenNew;
        }

        private TransitionGroupDocNode CreateChoice(Identity childId, ExplicitMods mods)
        {
            TransitionGroup tranGroup = (TransitionGroup)childId;
            TransitionDocNode[] transitions = DocNode.GetMatchingTransitions(
                tranGroup, DocSettings, mods);

            var nodeGroup = new TransitionGroupDocNode(tranGroup, transitions);
            return nodeGroup.ChangeSettings(DocSettings, DocNode, mods, SrmSettingsDiff.ALL);
        }

        public override bool ShowAutoManageChildren
        {
            get { return true; }
        }

        public static string DisplayText(PeptideDocNode node, DisplaySettings settings)
        {
            return GetLabel(node, string.Empty);
        }

        #endregion

        #region ITipProvider Members

        public override bool HasTip
        {
            get { return base.HasTip || (!ShowAnnotationTipOnly && HasPeptideTip(DocNode, DocSettings)); }
        }

        public static bool HasPeptideTip(PeptideDocNode nodePep, SrmSettings settings)
        {
            return nodePep.IsDecoy ||
                   !nodePep.IsProteomic ||
                   nodePep.Peptide.Begin.HasValue ||
                   nodePep.Rank.HasValue ||
                   nodePep.Note != null ||
                   // With one child, its tip detail will be appended
                   nodePep.Children.Count == 1 ||
                   // With multiple children, modification sequences may be shown
                   GetTypedModifiedSequences(nodePep, settings).Any();
        }

        public override Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            var size = base.RenderTip(g, sizeMax, draw);
            if(ShowAnnotationTipOnly)
                return size;
            if (draw)
                g.TranslateTransform(0, size.Height);
            Size sizeMaxNew = new Size(sizeMax.Width, sizeMax.Height - size.Height);
            var nodeTranTree = SequenceTree.GetNodeOfType<TransitionTreeNode>();
            var nodeTranSelected = (nodeTranTree != null ? nodeTranTree.DocNode : null);
            var sizeNew = RenderTip(DocNode, nodeTranSelected, DocSettings, g, sizeMaxNew, draw);
            return new Size(Math.Max(size.Width, sizeNew.Width), size.Height + sizeNew.Height);
        }

        public static Size RenderTip(PeptideDocNode nodePep,
                                            TransitionDocNode nodeTranSelected,
                                            SrmSettings settings,
                                            Graphics g,
                                            Size sizeMax,
                                            bool draw)
        {
            var table = new TableDesc();
            using (RenderTools rt = new RenderTools())
            {
                Peptide peptide = nodePep.Peptide;
                SizeF size;
                if (peptide.IsCustomMolecule)
                {
                    table.AddDetailRow(Resources.TransitionGroupTreeNode_RenderTip_Molecule, nodePep.CustomMolecule.Name, rt);
                    table.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Formula, nodePep.CustomMolecule.Formula, rt);
                    table.AddDetailRow(Resources.PeptideTreeNode_RenderTip_Neutral_Mass,
                        nodePep.CustomMolecule.GetMass(settings.TransitionSettings.Prediction.PrecursorMassType).ToString(LocalizationHelper.CurrentCulture), rt);
                    foreach (var id in nodePep.CustomMolecule.AccessionNumbers.AccessionNumbers)
                    {
                        table.AddDetailRow(id.Key, id.Value, rt); // Show InChiKey etc as available
                    }
                    size = table.CalcDimensions(g);
                    table.Draw(g);
                    return new Size((int)Math.Round(size.Width + 2), (int)Math.Round(size.Height + 2));
                }
                if (peptide.IsDecoy)
                {
                    string sourceText = nodePep.SourceTextId
                        .Replace(@".0]", @"]")
                        .Replace(@".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator);
                    table.AddDetailRow(Resources.PeptideTreeNode_RenderTip_Source, sourceText, rt);
                }

                if (nodePep.Children.Count > 1)
                {
                    foreach (var typedModSequence in GetTypedModifiedSequences(nodePep, settings))
                        table.AddDetailRow(typedModSequence.Key.Title, typedModSequence.Value, rt);
                }

                if (peptide.Begin.HasValue)
                {
                    // Add a spacing row, if anything was added
                    if (table.Count > 0)
                        table.AddDetailRow(@" ", @" ", rt);
                    table.AddDetailRow(Resources.PeptideTreeNode_RenderTip_Previous, peptide.PrevAA.ToString(CultureInfo.InvariantCulture), rt);
                    table.AddDetailRow(Resources.PeptideTreeNode_RenderTip_First, (peptide.Begin.Value + 1).ToString(LocalizationHelper.CurrentCulture), rt);
                    table.AddDetailRow(Resources.PeptideTreeNode_RenderTip_Last, (peptide.End ?? 0).ToString(LocalizationHelper.CurrentCulture), rt);
                    table.AddDetailRow(Resources.PeptideTreeNode_RenderTip_Next, peptide.NextAA.ToString(CultureInfo.InvariantCulture), rt);
                }
                if (nodePep.Rank.HasValue)
                    table.AddDetailRow(Resources.PeptideTreeNode_RenderTip_Rank, nodePep.Rank.Value.ToString(LocalizationHelper.CurrentCulture), rt);
               
                size = table.CalcDimensions(g);
                if (draw)
                    table.Draw(g);

                // Render group tip, if there is only one, and this node is collapsed
                if (nodePep.Children.Count == 1)
                {
                    var nodeGroup = (TransitionGroupDocNode)nodePep.Children[0];
                    if (size.Height > 0)
                        size.Height += TableDesc.TABLE_SPACING;
                    if (draw)
                        g.TranslateTransform(0, size.Height);
                    Size sizeMaxGroup = new Size(sizeMax.Width, sizeMax.Height - (int)size.Height);
                    SizeF sizeGroup = TransitionGroupTreeNode.RenderTip(nodePep, nodeGroup,
                        nodeTranSelected, settings, g, sizeMaxGroup, draw);
                    if (draw)
                        g.TranslateTransform(0, -size.Height);

                    size.Width = Math.Max(size.Width, sizeGroup.Width);
                    size.Height += sizeGroup.Height;
                }

                return new Size((int)Math.Round(size.Width + 2), (int)Math.Round(size.Height + 2));
            }
        }

        private static IEnumerable<KeyValuePair<IsotopeLabelType, string>> GetTypedModifiedSequences(
            PeptideDocNode nodePep, SrmSettings settings)
        {
            foreach (var labelType in settings.PeptideSettings.Modifications.GetModificationTypes())
            {
                if (nodePep.Peptide.IsCustomMolecule)
                    continue;
                // Only return the modified sequence, if the peptide actually as a child
                // of this type.
                if (!nodePep.HasChildType(labelType))
                    continue;
                var calc = settings.TryGetPrecursorCalc(labelType, nodePep.ExplicitMods);
                if (calc == null)
                    continue;

                string modSequence = calc.GetModifiedSequence(nodePep.Peptide.Target, true).Sequence; // Never have to worry about this being a custom molecule, we already checked

                // Only return if the modified sequence contains modifications
                if (modSequence.Contains('['))
                    yield return new KeyValuePair<IsotopeLabelType, string>(labelType, modSequence);
            }            
        }

        #endregion

        #region IClipboardDataProvider Members

        protected override DataObject GetNodeData()
        {
            DataObject data = new DataObject();
            data.SetData(DataFormats.Text, Text);

            StringBuilder sb = new StringBuilder();
            TextSequence[] nodeText = _textSequences ?? CreateTextSequences(null);
            foreach (TextSequence text in nodeText)
            {
                if (text.IsPlainText)
                    sb.Append(text.Text);
                else
                {
                    sb.Append(@"<Font");
                    // ReSharper disable LocalizableElement
                    if (text.Font.Bold && text.Font.Underline)
                        sb.Append(" style=\"font-weight: bold; text-decoration: underline\"");
                   else if (text.Font.Bold)
                        sb.Append(" style=\"font-weight: bold\"");
                    else if (text.Font.Underline)
                        sb.Append(" style=\"text-decoration: underline\"");
                    sb.AppendFormat(" color = \"{0}\">{1}", text.Color.ToKnownColor(), text.Text);
                    // ReSharper restore LocalizableElement
                    sb.Append(@"</font>");
                }
            }
            data.SetData(DataFormats.Html, HtmlFragment.ClipBoardText(sb.ToString()));

            return data;
        }

        #endregion

    }
}
