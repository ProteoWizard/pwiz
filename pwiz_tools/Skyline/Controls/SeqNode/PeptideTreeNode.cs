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
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.SeqNode
{
    public class PeptideTreeNode : SrmTreeNodeParent, ITipProvider, IClipboardDataProvider
    {
        public const string TITLE = "Peptide";

        public static bool ExpandDefault { get { return Settings.Default.SequenceTreeExpandPeptides; } }

        public static PeptideTreeNode CreateInstance(SequenceTree tree, DocNode nodeDoc)
        {
            Debug.Assert(nodeDoc is PeptideDocNode);
            var nodeTree = new PeptideTreeNode(tree, (PeptideDocNode)nodeDoc);
            if (ExpandDefault)
                nodeTree.Expand();
           return nodeTree;
        }

// ReSharper disable SuggestBaseTypeForParameter
        public PeptideTreeNode(SequenceTree tree, PeptideDocNode nodePeptide)
// ReSharper restore SuggestBaseTypeForParameter
            : base(tree, nodePeptide)
        {

        }

        public PeptideDocNode DocNode { get { return (PeptideDocNode)Model; } }

        public override string Heading
        {
            get { return TITLE; }
        }

        public override string ChildHeading
        {
            get { return string.Format("{0} {1}s", Text, TransitionGroupTreeNode.TITLE); }
        }

        public override string ChildUndoHeading
        {
            get { return string.Format("{0} {1}s", Text, TransitionGroupTreeNode.TITLE.ToLower()); }
        }

        public bool HasLibInfo
        {
            get
            {
                foreach (TransitionGroupDocNode tranGroup in DocNode.Children)
                {
                    if (tranGroup.HasLibInfo)
                        return true;
                }
                return false;
            }
        }

        protected override void OnModelChanged()
        {
            int typeImageIndex = TypeImageIndex;
            if (typeImageIndex != ImageIndex)
                ImageIndex = SelectedImageIndex = typeImageIndex;
            int peakImageIndex = PeakImageIndex;
            if (peakImageIndex != StateImageIndex)
                StateImageIndex = peakImageIndex;
            string label = DocNode + ResultsText;
            if (!string.Equals(label, Text))
                Text = label;
            // Hard to tell what might cause label formatting to change
            _textSequences = null;

            // Make sure children are up to date
            OnUpdateChildren(ExpandDefault);
        }

        public int TypeImageIndex
        {
            get
            {
                return SelectedImageIndex = (int)(HasLibInfo ?
                     SequenceTree.ImageId.peptide_lib : SequenceTree.ImageId.peptide);
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
            get { return ""; } 
        }

        protected override void UpdateChildren(bool materialize)
        {
            UpdateNodes<TransitionGroupTreeNode>(SequenceTree, Nodes, DocNode.Children, materialize,
                                                 TransitionGroupTreeNode.CreateInstance);
        }

        private TextSequence[] _textSequences;

        private const TextFormatFlags FORMAT_TEXT_SEQUENCE = TextFormatFlags.SingleLine |
                                                             TextFormatFlags.NoPadding |
                                                             TextFormatFlags.VerticalCenter;

        private TextSequence[] GetTextSequences(IDeviceContext g)
        {
            if (_textSequences == null)
                _textSequences = CreateTextSequences(g);
            return _textSequences;
        }

        private TextSequence[] CreateTextSequences(IDeviceContext g)
        {
            // Store text and font information for all label types
            bool heavyMods = false;
            var listTypeSequences = new List<TextSequence> { CreateTypeTextSequence(IsotopeLabelType.light) };
            foreach (var labelType in DocSettings.PeptideSettings.Modifications.GetHeavyModificationTypes())
            {
                // Only color for the label types actually measured in this peptide
                if (!DocNode.HasChildType(labelType))
                    continue;

                var textSequence = CreateTypeTextSequence(labelType);                
                listTypeSequences.Add(textSequence);
                heavyMods = (heavyMods || textSequence != null);
            }

            // Calculate text sequence values for the peptide display string
            var listTextSequences = new List<TextSequence>();

            // If no modifications, use a single plain text sequence
            if (!heavyMods && !listTypeSequences[0].Text.Contains("["))
                listTextSequences.Add(CreatePlainTextSequence(Text));
            else
            {
                string pepSequence = DocNode.Peptide.Sequence;
                int startPep = Text.IndexOf(pepSequence);
                int endPep = startPep + pepSequence.Length;

                // Add prefix plain-text if necessary
                if (startPep > 0)
                {
                    string prefix = Text.Substring(0, startPep);
                    listTextSequences.Add(CreatePlainTextSequence(prefix));
                }

                // Enumerate amino acid characters coallescing their font information
                // into text sequences.
                var prevCharFont = new CharFont('.', SequenceTree.Font, Color.Black);
                var indexes = new int[listTypeSequences.Count];

                CharFont charFont;
                var sb = new StringBuilder();
                while ((charFont = GetCharFont(indexes, listTypeSequences)) != null)
                {
                    if (!charFont.IsSameDisplay(prevCharFont) && sb.Length > 0)
                    {
                        listTextSequences.Add(CreateTextSequence(sb, prevCharFont));
                        sb.Remove(0, sb.Length);
                    }
                    sb.Append(charFont.Character);
                    prevCharFont = charFont;
                }
                // Add the last segment
                if (sb.Length > 0)
                    listTextSequences.Add(CreateTextSequence(sb, prevCharFont));

                // Add suffix plain-text if necessary
                if (endPep < Text.Length)
                {
                    string suffix = Text.Substring(endPep);
                    listTextSequences.Add(CreatePlainTextSequence(suffix));
                }
            }

            // Calculate placement for each text sequence
            int textRectWidth = 0;
            foreach (var textSequence in listTextSequences)
            {
                Size sizeMax = new Size(int.MaxValue, int.MaxValue);
                textSequence.Position = textRectWidth;
                textSequence.Width = TextRenderer.MeasureText(g, textSequence.Text,
                    textSequence.Font, sizeMax, FORMAT_TEXT_SEQUENCE).Width;
                textRectWidth += textSequence.Width;
            }

            return listTextSequences.ToArray();            
        }

        /// <summary>
        /// Calculates font information for a single amino acid character in
        /// the peptide, and increments indexes to next amino acid character.
        /// </summary>
        /// <param name="indexes">Index locations of the amino acid in each text sequence</param>
        /// <param name="textSequences">List of text sequences for all label types being considered</param>
        /// <returns>The amino acid character and its font information</returns>
        private CharFont GetCharFont(int[] indexes, IList<TextSequence> textSequences)
        {
            int iChar = indexes[0];
            string text = textSequences[0].Text;
            if (iChar >= text.Length)
                return null;

            char c = text[iChar];
            Font font = SequenceTree.Font;
            Color color = Color.Black;

            string modString = NextAA(0, indexes, textSequences);
            if (modString != null)
                font = textSequences[0].Font;                

            for (int i = 1; i < indexes.Length; i++)
            {
                string modStringHeavy = NextAA(i, indexes, textSequences);
                if (modStringHeavy == null)
                    continue;

                if (Equals(color, Color.Black) && !Equals(modString, modStringHeavy))
                {
                    color = textSequences[i].Color;
                    if (modString == null)
                        font = textSequences[i].Font;
                    else
                        font = SequenceTree.LightAndHeavyModFont;
                }
            }

            return new CharFont(c, font, color);
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
        private TextSequence CreateTypeTextSequence(IsotopeLabelType labelType)
        {
            var calc = DocSettings.GetPrecursorCalc(labelType, DocNode.ExplicitMods);
            if (calc == null)
                return null;

            return new TextSequence
                       {
                           Text = calc.GetModifiedSequence(DocNode.Peptide.Sequence, true),
                           Font = SequenceTree.GetModFont(labelType),
                           Color = SequenceTree.GetModColor(labelType)
                       };
        }

        /// <summary>
        /// Creates a text sequence with normal font.
        /// </summary>
        private TextSequence CreatePlainTextSequence(string text)
        {
            return new TextSequence
                       {
                           Text = text,
                           Font = SequenceTree.Font,
                           Color = Color.Black
                       };
        }

        /// <summary>
        /// Creates a text sequence for a peptide sequence with modifications
        /// </summary>
        private static TextSequence CreateTextSequence(StringBuilder sb, CharFont charFont)
        {
            return new TextSequence
            {
                Text = sb.ToString(),
                Font = charFont.Font,
                Color = charFont.Color
            };
        }

        /// <summary>
        /// Font and color for a single amino acid character in the peptide sequence
        /// </summary>
        private sealed class CharFont
        {
            public CharFont(char character, Font font, Color color)
            {
                Character = character;
                Font = font;
                Color = color;
            }

            public char Character { get; private set; }
            public Font Font { get; private set; }
            public Color Color { get; private set; }

            public bool IsSameDisplay(CharFont charFont)
            {
                return ReferenceEquals(Font, charFont.Font) &&
                       Equals(Color, charFont.Color);
            }
        }

        protected override int WidthCustom
        {
            get
            {
                if (_textSequences == null)
                    return Bounds.Width;

                var lastTextSequence = _textSequences[_textSequences.Length - 1];
                return lastTextSequence.Position + lastTextSequence.Width +
                    TreeViewMS.PADDING*2;
            }
        }

        protected override void DrawTextMS(Graphics g)
        {
            DrawTextBackground(g);

            Rectangle bounds = BoundsMS;
            Rectangle rectDraw = new Rectangle(0, bounds.Y, 0, bounds.Height);
            foreach (var textSequence in GetTextSequences(g))
            {
                rectDraw.X = textSequence.Position + bounds.X + TreeViewMS.PADDING;
                rectDraw.Width = textSequence.Width;
                // Use selection highlight color, if it is in the selection, otherwise
                // use the color from the text sequence format information.
                Color foreColor = (IsInSelection && SequenceTree.Focused ? ForeColorMS : textSequence.Color);
                TextRenderer.DrawText(g, textSequence.Text, textSequence.Font, rectDraw,
                    foreColor, BackColorMS, FORMAT_TEXT_SEQUENCE);
            }

            DrawFocus(g);
            DrawAnnotationIndicator(g);
        }
         

        #region IChildPicker Members

        public override string GetPickLabel(object child)
        {
            // TODO: Library information e.g. (12 copies)
            TransitionGroup group = (TransitionGroup) child;
            double massH = DocSettings.GetPrecursorMass(group.LabelType, group.Peptide.Sequence, DocNode.ExplicitMods);
            return TransitionGroupTreeNode.GetLabel(group, SequenceMassCalc.GetMZ(massH, group.PrecursorCharge), "");
        }

        public override bool Filtered
        {
            get { return Settings.Default.FilterTransitionGroups; }
            set { Settings.Default.FilterTransitionGroups = value; }
        }

        public override IEnumerable<object> GetChoices(bool useFilter)
        {
            var mods = DocNode.ExplicitMods;
            foreach (TransitionGroup group in DocNode.Peptide.GetTransitionGroups(DocSettings, mods, useFilter))
                yield return group;
        }

        public override IPickedList CreatePickedList(IEnumerable<object> chosen, bool autoManageChildren)
        {
            return new TransitionGroupPickedList(DocSettings, DocNode, chosen, autoManageChildren);
        }

        private sealed class TransitionGroupPickedList : AbstractPickedList
        {
            private readonly PeptideDocNode _nodePeptide;

            public TransitionGroupPickedList(SrmSettings settings, PeptideDocNode nodePep,
                    IEnumerable<object> picked, bool autoManageChildren)
                : base(settings, picked, autoManageChildren)
            {
                _nodePeptide = nodePep;
            }

            public override DocNode CreateChildNode(Identity childId)
            {
                TransitionGroup tranGroup = (TransitionGroup) childId;
                ExplicitMods mods = _nodePeptide.ExplicitMods;
                string seq = tranGroup.Peptide.Sequence;
                double massH = Settings.GetPrecursorMass(tranGroup.LabelType, seq, mods);
                RelativeRT relativeRT = Settings.GetRelativeRT(tranGroup.LabelType, seq, mods);
                TransitionDocNode[] transitions = _nodePeptide.GetMatchingTransitions(
                    tranGroup, Settings, mods);

                var nodeGroup = new TransitionGroupDocNode(tranGroup, massH, relativeRT,
                    transitions ?? new TransitionDocNode[0], transitions == null);
                return nodeGroup.ChangeSettings(Settings, mods, SrmSettingsDiff.ALL);
            }

            public override Identity GetId(object pick)
            {
                return (Identity) pick;
            }
        }

        public override bool ShowAutoManageChildren
        {
            get { return true; }
        }

        #endregion

        #region ITipProvider Members

        public bool HasTip
        {
            get
            {
                return DocNode.Peptide.Begin.HasValue ||
                       DocNode.Rank.HasValue ||
                       DocNode.Note != null ||
                       (!IsExpanded && DocNode.Children.Count == 1);
            }
        }

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {


            var table = new TableDesc();
            using (RenderTools rt = new RenderTools())
            {
                Peptide peptide = DocNode.Peptide;

                if (peptide.Begin.HasValue)
                {
                    table.AddDetailRow("Previous", peptide.PrevAA.ToString(), rt);
                    table.AddDetailRow("First", peptide.Begin.ToString(), rt);
                    table.AddDetailRow("Last", (peptide.End.Value - 1).ToString(), rt);
                    table.AddDetailRow("Next", peptide.NextAA.ToString(), rt);
                }
                if (DocNode.Rank.HasValue)
                    table.AddDetailRow("Rank", DocNode.Rank.ToString(), rt);
                if (!string.IsNullOrEmpty(DocNode.Note))
                    table.AddDetailRow("Note", DocNode.Note, rt);

                SizeF size = table.CalcDimensions(g);
                if (draw)
                    table.Draw(g);

                // Render group tip, if there is only one, and this node is collapsed
                if (!IsExpanded && DocNode.Children.Count == 1)
                {
                    var nodeGroup = (TransitionGroupDocNode) DocNode.Children[0];
                    if (size.Height > 0)
                        size.Height += TableDesc.TABLE_SPACING;
                    g.TranslateTransform(0, size.Height);
                    Size sizeMaxGroup = new Size(sizeMax.Width, sizeMax.Height - (int) size.Height);
                    SizeF sizeGroup = TransitionGroupTreeNode.RenderTip(SequenceTree, this,
                                                                        nodeGroup, g, sizeMaxGroup, draw);
                    g.TranslateTransform(0, -size.Height);

                    size.Width = Math.Max(size.Width, sizeGroup.Width);
                    size.Height += sizeGroup.Height;
                }

                return new Size((int)size.Width + 2, (int)size.Height + 2);
            }            
        }

        #endregion

        #region IClipboardDataProvider Members

        public void ProvideData()
        {
            DataObject data = new DataObject();
            data.SetData(DataFormats.Text, DocNode.Peptide.Sequence);

            Clipboard.Clear();
            Clipboard.SetDataObject(data);
        }

        #endregion
    }
}