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
using System.Diagnostics;
using System.Drawing;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.SeqNode
{
    public class TransitionTreeNode : SrmTreeNode
    {
        public static string TITLE
        {
            get { return Resources.TransitionTreeNode_Title; }
        }
        public static string TITLES
        {
            get { return Resources.TransitionTreeNode_Titles; }
        }

        public static TransitionTreeNode CreateInstance(SequenceTree tree, DocNode nodeDoc)
        {
            Debug.Assert(nodeDoc is TransitionDocNode);
            return new TransitionTreeNode(tree, (TransitionDocNode)nodeDoc);
        }

        public TransitionTreeNode(SequenceTree tree, TransitionDocNode ion)
            : base(tree, ion)
        {
        }

        public TransitionDocNode DocNode => (TransitionDocNode)Model;
        public TransitionGroupDocNode TransitionGroupNode => ((TransitionGroupTreeNode)Parent)?.DocNode;
        public PeptideDocNode PepNode => ((PeptideTreeNode)Parent?.Parent)?.DocNode;
        public PeptideGroupDocNode PepGroupNode => ((PeptideGroupTreeNode)Parent?.Parent?.Parent)?.DocNode;

        public override string Heading
        {
            get { return Resources.TransitionTreeNode_Title; }
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
            string label = DisplayText(SequenceTree.GetDisplaySettings(PepNode), DocNode);
            if (!Equals(label, Text))
                Text = label;
            ForeColor = DocNode.ExplicitQuantitative ? SystemColors.WindowText : SystemColors.GrayText;
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
            if (nodeTran.IsDecoy)
            {
                return (int)(nodeTran.HasLibInfo
                    ? SequenceTree.ImageId.fragment_lib_decoy
                    : SequenceTree.ImageId.fragment_decoy);
            }
            return (int)(nodeTran.HasLibInfo
                ? SequenceTree.ImageId.fragment_lib
                : SequenceTree.ImageId.fragment);
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

            float? ratio = (nodeTran.HasResults ? nodeTran.GetPeakCountRatio(index, settings.TransitionSettings.Integration.IsIntegrateAll) : null);
            if (ratio == null)
                return (int)SequenceTree.StateImageId.peak_blank;
            if (ratio == 0)
                return (int)SequenceTree.StateImageId.no_peak;
            if (ratio < 1.0)
                return (int)SequenceTree.StateImageId.keep;

            return (int)SequenceTree.StateImageId.peak;
        }

        public static string DisplayText(DisplaySettings settings, TransitionDocNode nodeTran)
        {
            return GetLabel(nodeTran, GetResultsText(settings, nodeTran));
        }

        private static string GetResultsText(DisplaySettings displaySettings, TransitionDocNode nodeTran)
        {
            int? rank = nodeTran.GetPeakRankByLevel(displaySettings.ResultsIndex);
            string label = string.Empty;
            if (rank.HasValue && rank > 0)
            {
                // Mark MS1 transition ranks with "i" for isotope
                string rankText = (nodeTran.IsMs1 ? @"i " : string.Empty) + rank;
                label = string.Format(Resources.TransitionTreeNode_GetResultsText__0__, rankText);
            }

            float? ratio = null;
            if (!Equals(displaySettings.NormalizationMethod, NormalizationMethod.NONE))
            {
                ratio = (float?)displaySettings.NormalizedValueCalculator.GetTransitionValue(displaySettings.NormalizationMethod,
                    displaySettings.NodePep, nodeTran,
                    nodeTran.GetChromInfoEntry(displaySettings.ResultsIndex));
            }
            if (!ratio.HasValue)
                return label;

            return string.Format(Resources.TransitionTreeNode_GetResultsText__0__ratio__1__, label, MathEx.RoundAboveZero(ratio.Value, 2, 4));
        }

        public static string GetLabel(TransitionDocNode nodeTran, string resultsText)
        {
            Transition tran = nodeTran.Transition;
            string labelPrefix;
            const string labelPrefixSpacer = " - ";
            if (nodeTran.ComplexFragmentIon.IsCrosslinked)
            {
                labelPrefix = nodeTran.ComplexFragmentIon.GetTargetsTreeLabel() + labelPrefixSpacer;
            }
            else if (tran.IsPrecursor())
            {
                labelPrefix = nodeTran.FragmentIonName + Transition.GetMassIndexText(tran.MassIndex) + labelPrefixSpacer;
            }
            else if (tran.IsCustom())
            {
                if (!string.IsNullOrEmpty(tran.CustomIon.Name))
                    labelPrefix = tran.CustomIon.Name + labelPrefixSpacer;
                else if (!string.IsNullOrEmpty(tran.CustomIon.Formula))
                    labelPrefix = tran.CustomIon.Formula + labelPrefixSpacer;
                else
                    labelPrefix = string.Empty;
            }
            else
            {
                labelPrefix = string.Format(Resources.TransitionTreeNode_GetLabel__0__1__, tran.AA, nodeTran.FragmentIonName) + labelPrefixSpacer;
            }

            if (!nodeTran.HasLibInfo && !nodeTran.HasDistInfo)
            {
                return string.Format(@"{0}{1}{2}{3}",
                                     labelPrefix,
                                     GetMzLabel(nodeTran),
                                     Transition.GetChargeIndicator(tran.Adduct),
                                     resultsText);
            }
            
            string rank = nodeTran.HasDistInfo
                              ? string.Format(Resources.TransitionTreeNode_GetLabel_irank__0__, nodeTran.IsotopeDistInfo.Rank)
                              : string.Format(Resources.TransitionTreeNode_GetLabel_rank__0__, nodeTran.LibInfo.Rank);

            return string.Format(@"{0}{1}{2} ({3}){4}",
                                 labelPrefix,
                                 GetMzLabel(nodeTran),
                                 Transition.GetChargeIndicator(tran.Adduct),
                                 rank,
                                 resultsText);
        }

        private static string GetMzLabel(TransitionDocNode nodeTran)
        {
            int? massShift = nodeTran.Transition.DecoyMassShift;
            double shift = SequenceMassCalc.GetPeptideInterval(massShift);
            return string.Format(@"{0:F04}{1}", nodeTran.Mz - shift,
                Transition.GetDecoyText(massShift));
        }

        #region Implementation of ITipProvider

        public override bool HasTip { get { return base.HasTip || !ShowAnnotationTipOnly; } }

        public override Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            var size = base.RenderTip(g, sizeMax, draw);
            if(ShowAnnotationTipOnly)
                return size;
            if (draw)
                g.TranslateTransform(0, size.Height);
            Size sizeMaxNew = new Size(sizeMax.Width, sizeMax.Height - size.Height);
            var sizeNew = RenderTip(DocNode, g, sizeMaxNew, draw);
            return new Size(Math.Max(size.Width, sizeNew.Width), size.Height + sizeNew.Height);
        }

        public static Size RenderTip(TransitionDocNode nodeTran, Graphics g, Size sizeMax, bool draw)
        {
            var table = new TableDesc();
            using (RenderTools rt = new RenderTools())
            {
                table.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Ion, nodeTran.Transition.FragmentIonName, rt);
                table.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Charge, FormatAdductTip(nodeTran.Transition.Adduct), rt);
                table.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Product_m_z, string.Format(@"{0:F04}", nodeTran.Mz), rt);
                int? decoyMassShift = nodeTran.Transition.DecoyMassShift;
                if (decoyMassShift.HasValue)
                    table.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Decoy_Mass_Shift, decoyMassShift.Value.ToString(LocalizationHelper.CurrentCulture), rt);

                if (nodeTran.HasLoss)
                {
                    // If there is only one loss, show its full description
                    var losses = nodeTran.Losses;
                    if (losses.Losses.Count == 1)
                        table.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Loss, losses.ToStrings()[0], rt);
                    // Otherwise, just show the total mass for multiple losses
                    // followed by individual losses
                    else
                    {
                        table.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Loss, string.Format(@"{0:F04}", losses.Mass), rt);
                        table.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Losses, TextUtil.LineSeparate(losses.ToStrings()), rt);
                    }
                }
                if (nodeTran.HasLibInfo)
                {
                    table.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Library_rank, nodeTran.LibInfo.Rank.ToString(LocalizationHelper.CurrentCulture), rt);
                    float intensity = nodeTran.LibInfo.Intensity;
                    table.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Library_intensity, MathEx.RoundAboveZero(intensity,
                        (intensity < 10 ? 1 : 0), 4).ToString(LocalizationHelper.CurrentCulture), rt);
                }
                if (nodeTran.Transition.IsCustom() && !string.IsNullOrEmpty(nodeTran.Transition.CustomIon.Formula))
                {
                    table.AddDetailRow(Resources.TransitionTreeNode_RenderTip_Formula, nodeTran.Transition.CustomIon.Formula + nodeTran.Transition.Adduct.AdductFormula.ToString(LocalizationHelper.CurrentCulture), rt);
                }

                SizeF size = table.CalcDimensions(g);
                if (draw)
                    table.Draw(g);

                return new Size((int)size.Width + 2, (int)size.Height + 2);
            }
        }

        #endregion


    }
}
