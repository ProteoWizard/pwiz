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
using pwiz.Skyline.Util;

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

        public PeptideDocNode PepNode
        {
            get
            {
                return (Parent != null && Parent.Parent != null ?
                    ((PeptideTreeNode) Parent.Parent).DocNode : null);
            }
        }

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
            string label = GetDisplayText(DocNode, PepNode, SequenceTree);
            if (!Equals(label, Text))
                Text = label;
        }
        
        public int TypeImageIndex
        {
            get { return GetTypeImageIndex(DocNode); }
        }

        public static Image GetTypeImage(TransitionDocNode nodeTran, SequenceTree sequenceTree)
        {
            return sequenceTree.ImageList.Images[GetTypeImageIndex(nodeTran)];
        }

        private static int GetTypeImageIndex(TransitionDocNode nodeTran)
        {
            return (int)(nodeTran.HasLibInfo ?
                    SequenceTree.ImageId.fragment_lib : SequenceTree.ImageId.fragment);
        }

        public int PeakImageIndex
        {
            get
            {
                return GetPeakImageIndex(DocNode, PepNode, SequenceTree);
            }
        }

        public static Image GetPeakImage(TransitionDocNode nodeTran,
            PeptideDocNode nodePep, SequenceTree sequenceTree)
        {
            int imageIndex = GetPeakImageIndex(nodeTran, nodePep, sequenceTree);
            return (imageIndex != -1 ? sequenceTree.StateImageList.Images[imageIndex] : null);
        }

        public static int GetPeakImageIndex(TransitionDocNode nodeTran,
            PeptideDocNode nodePep, SequenceTree sequenceTree)
        {
            var settings = sequenceTree.Document.Settings;
            if (!settings.HasResults)
                return -1;

            int index = sequenceTree.GetDisplayResultsIndex(nodePep);

            float? ratio = (nodeTran.HasResults ? nodeTran.GetPeakCountRatio(index) : null);
            if (ratio == null)
            {
                return settings.MeasuredResults.IsChromatogramSetLoaded(index) ?
                    (int)SequenceTree.StateImageId.peak_blank : -1;
            }
            else if (ratio == 0)
                return (int)SequenceTree.StateImageId.no_peak;
            else if (ratio < 1.0)
                return (int)SequenceTree.StateImageId.keep;

            return (int)SequenceTree.StateImageId.peak;
        }

        public static string GetDisplayText(TransitionDocNode nodeTran,
            PeptideDocNode nodePep, SequenceTree sequenceTree)
        {
            return GetLabel(nodeTran, GetResultsText(nodeTran, nodePep, sequenceTree));
        }

        private static string GetResultsText(TransitionDocNode nodeTran,
            PeptideDocNode nodePep, SequenceTree sequenceTree)
        {
            int index = sequenceTree.GetDisplayResultsIndex(nodePep);
            int indexRatio = sequenceTree.RatioIndex;
            int? rank = nodeTran.GetPeakRank(index);
            string label = (rank.HasValue && rank > 0 ? string.Format("[{0}]", rank) : "");
            float? ratio = nodeTran.GetPeakAreaRatio(index, indexRatio);
            if (!ratio.HasValue)
                return label;

            return string.Format("{0} (ratio {1})", label, MathEx.RoundAboveZero(ratio.Value, 2, 4));
        }

        public static string GetLabel(TransitionDocNode nodeTran, string resultsText)
        {
            Transition tran = nodeTran.Transition;
            string labelPrefix;
            if (tran.IsPrecursor())
            {
                labelPrefix = nodeTran.FragmentIonName;
            }
            else
            {
                labelPrefix = string.Format("{0} [{1}]", tran.AA, nodeTran.FragmentIonName);
            }

            if (!nodeTran.HasLibInfo)
            {
                return string.Format("{0} - {1:F04}{2}{3}",
                                     labelPrefix,
                                     nodeTran.Mz,
                                     Transition.GetChargeIndicator(tran.Charge),
                                     resultsText);
            }
            else
            {
                return string.Format("{0} - {1:F04}{2} (rank {3}){4}",
                                     labelPrefix,
                                     nodeTran.Mz,
                                     Transition.GetChargeIndicator(tran.Charge),
                                     nodeTran.LibInfo.Rank,
                                     resultsText);
            }
        }

        #region Implementation of ITipProvider

        public bool HasTip { get { return true; } }

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            return RenderTip(DocNode, g, sizeMax, draw);
        }

        public static Size RenderTip(TransitionDocNode nodeTran, Graphics g, Size sizeMax, bool draw)
        {
            var table = new TableDesc();
            using (RenderTools rt = new RenderTools())
            {
                table.AddDetailRow("Ion", nodeTran.Transition.FragmentIonName, rt);
                table.AddDetailRow("Charge", nodeTran.Transition.Charge.ToString(), rt);
                table.AddDetailRow("Product m/z", string.Format("{0:F04}", nodeTran.Mz), rt);
                if (nodeTran.HasLoss)
                {
                    // If there is only one loss, show its full description
                    var losses = nodeTran.Losses;
                    if (losses.Losses.Count == 1)
                        table.AddDetailRow("Loss", losses.ToStrings()[0], rt);
                    // Otherwise, just show the total mass for multiple losses
                    // followed by individual losses
                    else
                    {
                        table.AddDetailRow("Loss", string.Format("{0:F04}", losses.Mass), rt);
                        table.AddDetailRow("Losses", string.Join("\n", losses.ToStrings()), rt);
                    }
                }
                if (nodeTran.HasLibInfo)
                {
                    table.AddDetailRow("Library rank", nodeTran.LibInfo.Rank.ToString(), rt);
                    float intensity = nodeTran.LibInfo.Intensity;
                    table.AddDetailRow("Library intensity", MathEx.RoundAboveZero(intensity, (intensity < 10 ? 1 : 0), 4).ToString(), rt);
                }
                if (!string.IsNullOrEmpty(nodeTran.Note))
                    table.AddDetailRow("Note", nodeTran.Note, rt);

                SizeF size = table.CalcDimensions(g);
                if (draw)
                    table.Draw(g);

                return new Size((int)size.Width + 2, (int)size.Height + 2);
            }
        }

        #endregion
    }
}