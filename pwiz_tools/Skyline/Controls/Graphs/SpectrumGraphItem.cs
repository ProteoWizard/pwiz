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
using pwiz.Common.SystemUtil;
using pwiz.MSGraph;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using ZedGraph;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Graphs
{
    public class SpectrumGraphItem : AbstractSpectrumGraphItem
    {
        private TransitionGroupDocNode TransitionGroupNode { get; set; }
        private TransitionDocNode TransitionNode { get; set; }
        public string LibraryName { get; private set; }

        public SpectrumGraphItem(TransitionGroupDocNode transitionGroupNode, TransitionDocNode transition,
                                 LibraryRankedSpectrumInfo spectrumInfo, string libName) : base(spectrumInfo)
        {
            TransitionGroupNode = transitionGroupNode;
            TransitionNode = transition;
            LibraryName = libName;
        }

        protected override bool IsMatch(double predictedMz)
        {
            return ((TransitionNode != null) && (predictedMz == TransitionNode.Mz));
        }

        public override string Title
        {
            get
            {
                string libraryNamePrefix = LibraryName;
                if (!string.IsNullOrEmpty(libraryNamePrefix))
                    libraryNamePrefix += @" - ";

                TransitionGroup transitionGroup = TransitionGroupNode.TransitionGroup;
                string sequence = transitionGroup.Peptide.IsCustomMolecule
                    ? TransitionGroupNode.CustomMolecule.DisplayName
                    : transitionGroup.Peptide.Target.Sequence;
                var charge = transitionGroup.PrecursorAdduct.ToString(); // Something like "2" or "-3" for protonation, or "[M+Na]" for small molecules
                var labelType = SpectrumInfo.LabelType;
                if (transitionGroup.Peptide.IsCustomMolecule)
                {
                    return labelType.IsLight
                        ? string.Format(@"{0}{1}{2}", libraryNamePrefix, transitionGroup.Peptide.CustomMolecule.DisplayName, charge)
                        : string.Format(@"{0}{1}{2} ({3})", libraryNamePrefix, sequence, charge, labelType);
                }
                return labelType.IsLight
                    ? string.Format(Resources.SpectrumGraphItem_Title__0__1__Charge__2__, libraryNamePrefix, sequence, charge)
                    : string.Format(Resources.SpectrumGraphItem_Title__0__1__Charge__2__3__, libraryNamePrefix, sequence, charge, labelType);
            }
        }
    }
    
    public abstract class AbstractSpectrumGraphItem : AbstractMSGraphItem
    {
        private const string FONT_FACE = "Arial";
        private static readonly Color COLOR_A = Color.YellowGreen;
        private static readonly Color COLOR_X = Color.Green;
        private static readonly Color COLOR_B = Color.BlueViolet;
        private static readonly Color COLOR_Y = Color.Blue;
        private static readonly Color COLOR_C = Color.Orange;
        private static readonly Color COLOR_Z = Color.OrangeRed;
        private static readonly Color COLOR_OTHER_IONS = Color.DodgerBlue; // Other ion types, as in small molecule
        private static readonly Color COLOR_PRECURSOR = Color.DarkCyan;
        private static readonly Color COLOR_NONE = Color.Gray;
        public static readonly Color COLOR_SELECTED = Color.Red;

        private readonly Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> _ionMatches;
        protected LibraryRankedSpectrumInfo SpectrumInfo { get; set; }
        public int PeaksCount { get { return SpectrumInfo.Peaks.Count; } }
        public int PeaksMatchedCount { get { return SpectrumInfo.PeaksMatched.Count(); } }
        public int PeaksRankedCount { get { return SpectrumInfo.PeaksRanked.Count(); } }
        public ICollection<IonType> ShowTypes { get; set; }
        public ICollection<int> ShowCharges { get; set; } // List of absolute charge values to display CONSIDER(bspratt): may want finer per-adduct control for small mol use
        public bool ShowRanks { get; set; }
        public bool ShowScores { get; set; }
        public bool ShowMz { get; set; }
        public bool ShowObservedMz { get; set; }
        public bool ShowDuplicates { get; set; }
        public float FontSize { get; set; }

        // ReSharper disable InconsistentNaming
        private FontSpec _fontSpecA;
        private FontSpec FONT_SPEC_A { get { return GetFontSpec(COLOR_A, ref _fontSpecA); } }
        private FontSpec _fontSpecX;
        private FontSpec FONT_SPEC_X { get { return GetFontSpec(COLOR_X, ref _fontSpecX); } }
        private FontSpec _fontSpecB;
        private FontSpec FONT_SPEC_B { get { return GetFontSpec(COLOR_B, ref _fontSpecB); } }
        private FontSpec _fontSpecY;
        private FontSpec FONT_SPEC_Y { get { return GetFontSpec(COLOR_Y, ref _fontSpecY); } }
        private FontSpec _fontSpecC;
        private FontSpec FONT_SPEC_C { get { return GetFontSpec(COLOR_C, ref _fontSpecC); } }
        private FontSpec _fontSpecZ;
        private FontSpec FONT_SPEC_PRECURSOR { get { return GetFontSpec(COLOR_PRECURSOR, ref _fontSpecPrecursor); } }
        private FontSpec _fontSpecPrecursor;
        private FontSpec FONT_SPEC_Z { get { return GetFontSpec(COLOR_Z, ref _fontSpecZ); } }
        private FontSpec _fontSpecOtherIons;
        private FontSpec FONT_SPEC_OTHER_IONS { get { return GetFontSpec(COLOR_OTHER_IONS, ref _fontSpecOtherIons); } } // Small molecule fragments etc
        private FontSpec _fontSpecNone;
        private FontSpec FONT_SPEC_NONE { get { return GetFontSpec(COLOR_NONE, ref _fontSpecNone); } }
        private FontSpec _fontSpecSelected;
        private FontSpec FONT_SPEC_SELECTED { get { return GetFontSpec(COLOR_SELECTED, ref _fontSpecSelected); } }
        // ReSharper restore InconsistentNaming

        protected AbstractSpectrumGraphItem(LibraryRankedSpectrumInfo spectrumInfo)
        {
            SpectrumInfo = spectrumInfo;
            _ionMatches = new Dictionary<double, LibraryRankedSpectrumInfo.RankedMI>();
            foreach (var rmi in spectrumInfo.PeaksMatched)
            {
                _ionMatches[rmi.ObservedMz] = rmi;
            }

            // Default values
            FontSize = 10;
            LineWidth = 1;
        }

        protected abstract bool IsMatch(double predictedMz);

        private static FontSpec CreateFontSpec(Color color, float size)
        {
            return new FontSpec(FONT_FACE, size, color, false, false, false) { Border = { IsVisible = false }, Fill = new Fill(Color.FromArgb(180, Color.White)) };
        }

        private FontSpec GetFontSpec(Color color, ref FontSpec fontSpec)
        {
            return fontSpec ?? (fontSpec = CreateFontSpec(color, FontSize));
        }

        public override void CustomizeCurve(CurveItem curveItem)
        {
            ((LineItem)curveItem).Line.Width = LineWidth;
        }

        public override IPointList Points
        {
            get
            {
                return new PointPairList(SpectrumInfo.MZs.ToArray(),
                                         SpectrumInfo.Intensities.ToArray());
            }
        }

        public override void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
        {
            // Do nothing
        }
        
        public override void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
        {
            // ReSharper disable UseObjectOrCollectionInitializer
            foreach (var rmi in SpectrumInfo.PeaksMatched)
            {
                if (!IsVisibleIon(rmi))
                    continue;

                var matchedIon = rmi.MatchedIons.First(IsVisibleIon);

                Color color;
                switch (matchedIon.IonType)
                {
                    default: color = COLOR_NONE; break;
                    case IonType.a: color = COLOR_A; break;
                    case IonType.x: color = COLOR_X; break;
                    case IonType.b: color = COLOR_B; break;
                    case IonType.y: color = COLOR_Y; break;
                    case IonType.c: color = COLOR_C; break;
                    case IonType.z: color = COLOR_Z; break;
                    case IonType.custom: color = (rmi.Rank > 0) ? COLOR_OTHER_IONS : COLOR_NONE; break; // Small molecule fragments - only color if ranked
                    case IonType.precursor: color = COLOR_PRECURSOR; break;
                }

                if (rmi.MatchedIons.Any(mfi => IsMatch(mfi.PredictedMz)))
                {
                    color = COLOR_SELECTED;
                }

                double mz = rmi.ObservedMz;
                var stick = new LineObj(color, mz, rmi.Intensity, mz, 0);
                stick.IsClippedToChartRect = true;
                stick.Location.CoordinateFrame = CoordType.AxisXYScale;
                stick.Line.Width = LineWidth + 1;
                annotations.Add(stick);
            }
            //ReSharper restore UseObjectOrCollectionInitializer

            if (ShowScores && SpectrumInfo.Score.HasValue)
            {
                var text = new TextObj(
                    string.Format(LocalizationHelper.CurrentCulture, Resources.AbstractSpectrumGraphItem_AddAnnotations_, SpectrumInfo.Score),
                    0.01, 0, CoordType.ChartFraction, AlignH.Left, AlignV.Top)
                {
                    IsClippedToChartRect = true,
                    ZOrder = ZOrder.E_BehindCurves,
                    FontSpec = GraphSummary.CreateFontSpec(Color.Black),
                };
                annotations.Add(text);
            }
        }

        public override PointAnnotation AnnotatePoint(PointPair point)
        {
            LibraryRankedSpectrumInfo.RankedMI rmi;
            if (!_ionMatches.TryGetValue(point.X, out rmi) || !IsVisibleIon(rmi))
                return null;

            var matchedIon = rmi.MatchedIons.First(IsVisibleIon);

            FontSpec fontSpec;
            switch (matchedIon.IonType)
            {
                default: fontSpec = FONT_SPEC_NONE; break;
                case IonType.a: fontSpec = FONT_SPEC_A; break;
                case IonType.x: fontSpec = FONT_SPEC_X; break;
                case IonType.b: fontSpec = FONT_SPEC_B; break;
                case IonType.y: fontSpec = FONT_SPEC_Y; break;
                case IonType.c: fontSpec = FONT_SPEC_C; break;
                case IonType.z: fontSpec = FONT_SPEC_Z; break;
                case IonType.custom:
                    {
                    if (rmi.Rank == 0 && !rmi.HasAnnotations)
                        return null; // Small molecule fragments - only force annotation if ranked
                    fontSpec = FONT_SPEC_OTHER_IONS;
                    }
                    break;
                case IonType.precursor: fontSpec = FONT_SPEC_PRECURSOR; break;
            }
            if (rmi.MatchedIons.Any(mfi => IsMatch(mfi.PredictedMz)))
                fontSpec = FONT_SPEC_SELECTED;
            return new PointAnnotation(GetLabel(rmi), fontSpec, rmi.Rank);
        }

        public IEnumerable<string> IonLabels
        {
            get
            {
                foreach (var rmi in _ionMatches.Values)
                    yield return GetLabel(rmi);
            }
        }
       
        private string GetLabel(LibraryRankedSpectrumInfo.RankedMI rmi)
        {
            // Show the m/z values in the labels, if multiple should be visible, and
            // they have different display values.
            bool showMzInLabel = ShowMz &&
                                 rmi.MatchedIons.Where(IsVisibleIon)
                                     .Select(mfi => GetDisplayMz(mfi.PredictedMz))
                                     .Distinct()
                                     .Count() > 1;
                
            StringBuilder sb = new StringBuilder();
            foreach (var mfi in rmi.MatchedIons.Where(IsVisibleIon))
            {
                if (sb.Length > 0)
                    sb.AppendLine();

                sb.Append(GetLabel(mfi, sb.Length == 0 ? rmi.Rank : 0, showMzInLabel));
            }
            // If predicted m/z should be displayed, but hasn't been yet, then display now.
            if (ShowMz && !showMzInLabel)
            {
                sb.AppendLine().Append(GetDisplayMz(rmi.MatchedIons.First().PredictedMz));
            }
            // If showing observed m/z, and it is different from the predicted m/z, then display it last.
            if (ShowObservedMz)
            {
                sb.AppendLine().Append(GetDisplayMz(rmi.ObservedMz));
            }
            return sb.ToString();
        }

        private string GetLabel(MatchedFragmentIon mfi, int rank, bool showMz)
        {
            var label = new StringBuilder(string.IsNullOrEmpty(mfi.FragmentName) ? mfi.IonType.GetLocalizedString() : mfi.FragmentName);
            if (string.IsNullOrEmpty(mfi.FragmentName) && !Transition.IsPrecursor(mfi.IonType))
                label.Append(mfi.Ordinal.ToString(LocalizationHelper.CurrentCulture));
            if (mfi.Losses != null)
            {
                label.Append(@" -");
                label.Append(Math.Round(mfi.Losses.Mass, 1));
            }
            var chargeIndicator = mfi.Charge.Equals(Adduct.SINGLY_PROTONATED) ? string.Empty : Transition.GetChargeIndicator(mfi.Charge);
            label.Append(chargeIndicator);
            if (showMz)
                label.Append(string.Format(@" = {0:F01}", mfi.PredictedMz));
            if (rank > 0 && ShowRanks)
                label.Append(TextUtil.SEPARATOR_SPACE).Append(string.Format(@"({0})",string.Format(Resources.AbstractSpectrumGraphItem_GetLabel_rank__0__, rank)));
            return label.ToString();
        }

        private double GetDisplayMz(double mz)
        {
            // Try to show enough decimal places to distinguish by tolerance
            int places = 1;
            while (places < 4 && ((int) (SpectrumInfo.Tolerance*Math.Pow(10, places))) == 0)
                places++;
            return Math.Round(mz, places);
        }

        private bool IsVisibleIon(LibraryRankedSpectrumInfo.RankedMI rmi)
        {
            bool singleIon = (rmi.MatchedIons.Count == 1);
            if (ShowDuplicates && singleIon)
                return false;
            return rmi.MatchedIons.Any(IsVisibleIon);
        }

        private bool IsVisibleIon(MatchedFragmentIon mfi)
        {
            // Show precursor ions when they are supposed to be shown, regardless of charge
            // N.B. for fragments, we look at abs value of charge. CONSIDER(bspratt): for small mol libs we may want finer per-adduct control
            return mfi.Ordinal > 0 && ShowTypes.Contains(mfi.IonType) &&
                (mfi.IonType == IonType.precursor || ShowCharges.Contains(Math.Abs(mfi.Charge.AdductCharge)));
        }
    }

    public sealed class UnavailableMSGraphItem : NoDataMSGraphItem
    {
        public UnavailableMSGraphItem() : base(Resources.UnavailableMSGraphItem_UnavailableMSGraphItem_Spectrum_information_unavailable)
        {
        }
    }

    public class NoDataMSGraphItem : AbstractMSGraphItem
    {
        private readonly string _title;

        public NoDataMSGraphItem(string title)
        {
            _title = title;
        }

        public override string Title { get { return _title; } }

        public override PointAnnotation AnnotatePoint(PointPair point)
        {
            return null;
        }

        public override void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
        {
            // Do nothing
        }

        public override void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
        {
            // Do nothing
        }

        public override IPointList Points
        {
            get
            {
                return new PointPairList(new double[0], new double[0]);
            }
        }
    }

    public abstract class AbstractMSGraphItem : IMSGraphItemExtended
    {
        public abstract string Title { get; }
        public abstract PointAnnotation AnnotatePoint(PointPair point);
        public abstract void AddAnnotations(MSGraphPane graphPane, Graphics g,
                                            MSPointList pointList, GraphObjList annotations);
        public abstract void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g,
                                            MSPointList pointList, GraphObjList annotations);
        public abstract IPointList Points { get; }

        public virtual Color Color
        {
            get { return Color.Gray; }
        }

        public float LineWidth { get; set; }

        public virtual void CustomizeCurve(CurveItem curveItem)
        {
            // Do nothing by default            
        }

        public MSGraphItemType GraphItemType
        {
            get { return MSGraphItemType.spectrum; }
        }

        public virtual MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return MSGraphItemDrawMethod.stick; }
        }

        public void CustomizeYAxis(Axis axis)
        {
            CustomizeAxis(axis, Resources.AbstractMSGraphItem_CustomizeYAxis_Intensity);
        }

        public void CustomizeXAxis(Axis axis)
        {
            CustomizeAxis(axis, Resources.AbstractMSGraphItem_CustomizeXAxis_MZ);
        }

        private static void CustomizeAxis(Axis axis, string title)
        {
            axis.Title.FontSpec.Family = @"Arial";
            axis.Title.FontSpec.Size = 14;
            axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
            axis.Title.FontSpec.Border.IsVisible = false;
            SetAxisText(axis, title);
        }

        /// <summary>
        /// Sets the title text of an axis, ensuring that it is italicized, if the text is "m/z".
        /// Someone actually reported a reviewer of a manuscript mentioning that the m/z axis
        /// title should be in italics.
        /// </summary>
        public static void SetAxisText(Axis axis, string title)
        {
            if (string.Equals(title, @"m/z"))
                axis.Title.FontSpec.IsItalic = true;
            axis.Title.Text = title;
        }
    }
}
