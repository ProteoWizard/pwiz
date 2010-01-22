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
using System.Diagnostics;
using System.Drawing;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Controls.SeqNode
{
    public class TransitionTreeNode : SrmTreeNode, ITipProvider
    {
        public const string TITLE = "Transition";

        public static TransitionTreeNode CreateInstance(SequenceTree tree, DocNode nodeDoc)
        {
            Debug.Assert(nodeDoc is TransitionDocNode);
            return new TransitionTreeNode(tree, (TransitionDocNode)nodeDoc);
        }

        public TransitionTreeNode(SequenceTree tree, TransitionDocNode ion)
            : base(tree, ion)
        {
        }

        public TransitionDocNode DocNode { get { return (TransitionDocNode)Model; } }

        public override string Heading
        {
            get { return TITLE; }
        }

        protected override void OnModelChanged()
        {
            int typeImageIndex = TypeImageIndex;
            if (typeImageIndex != ImageIndex)
                ImageIndex = SelectedImageIndex = typeImageIndex;
            int peakImageIndex = PeakImageIndex;
            if (peakImageIndex != StateImageIndex)
                StateImageIndex = peakImageIndex;
            string label = GetLabel(DocNode, ResultsText);
            if (!Equals(label, Text))
                Text = label;
        }

        public int TypeImageIndex
        {
            get
            {
                return (int)(DocNode.HasLibInfo ?
                    SequenceTree.ImageId.fragment_lib : SequenceTree.ImageId.fragment);
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
                else if (ratio == 0)
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
                int? rank = DocNode.GetPeakRank(SequenceTree.ResultsIndex);
                string label = (rank.HasValue && rank > 0 ? string.Format("[{0}]", rank) : "");
                int index = SequenceTree.ResultsIndex;
                float? ratio = DocNode.GetPeakAreaRatio(index);
                if (!ratio.HasValue)
                    return label;

                return string.Format("{0} (ratio {1:F02})", label, ratio.Value);
            }
        }

        public static string GetLabel(TransitionDocNode nodeTran, string resultsText)
        {
            Transition tran = nodeTran.Transition;
            string labelPrefix;
            if (tran.IsPrecursor())
                labelPrefix = "precursor";
            else
                labelPrefix = string.Format("{0} [{1}]", tran.AA, tran.FragmentIonName);

            if (!nodeTran.HasLibInfo)
            {
                return string.Format("{0} - {1:F04}{2}{3}{4}",
                                     labelPrefix,
                                     nodeTran.Mz,
                                     Transition.GetChargeIndicator(tran.Charge),
                                     resultsText,
                                     nodeTran.NoteMark);
            }
            else
            {
                return string.Format("{0} - {1:F04}{2} (rank {3}){4}{5}",
                                     labelPrefix,
                                     nodeTran.Mz,
                                     Transition.GetChargeIndicator(tran.Charge),
                                     nodeTran.LibInfo.Rank,
                                     resultsText,
                                     nodeTran.NoteMark);
            }
        }

        #region Implementation of ITipProvider

        public bool HasTip { get { return true; } }

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            var table = new TableDesc();
            using (RenderTools rt = new RenderTools())
            {
                table.AddDetailRow("Ion", DocNode.Transition.IonType.ToString() + DocNode.Transition.Ordinal, rt);
                table.AddDetailRow("Charge", DocNode.Transition.Charge.ToString(), rt);
                table.AddDetailRow("Product m/z", string.Format("{0:F04}", DocNode.Mz), rt);
                if (DocNode.HasLibInfo)
                {
                    table.AddDetailRow("Library rank", DocNode.LibInfo.Rank.ToString(), rt);
                    table.AddDetailRow("Library intensity", string.Format("{0:F0}", DocNode.LibInfo.Intensity), rt);
                }
                if (!string.IsNullOrEmpty(DocNode.Note))
                    table.AddDetailRow("Note", DocNode.Note, rt);

                SizeF size = table.CalcDimensions(g);
                if (draw)
                    table.Draw(g);

                return new Size((int)size.Width + 2, (int)size.Height + 2);
            }
        }

        #endregion
    }
}