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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public class SpectrumGraphItem : AbstractSpectrumGraphItem
    {
        public PeptideDocNode PeptideDocNode { get; private set; }
        public TransitionGroupDocNode TransitionGroupNode { get; private set; }
        private TransitionDocNode TransitionNode { get; set; }
        public string LibraryName { get; private set; }

        public SpectrumGraphItem(PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupNode, TransitionDocNode transition,
            LibraryRankedSpectrumInfo spectrumInfo, string libName) : base(spectrumInfo)
        {
            PeptideDocNode = peptideDocNode;
            TransitionGroupNode = transitionGroupNode;
            TransitionNode = transition;
            LibraryName = libName;
        }

        protected override bool IsMatch(double predictedMz)
        {
            return ((TransitionNode != null) && (predictedMz == TransitionNode.Mz));
        }

        public static string GetLibraryPrefix(string libraryName)
        {
            return !string.IsNullOrEmpty(libraryName) ? libraryName + @" - " : string.Empty;
        }

        public static string RemoveLibraryPrefix(string title, string libraryName)
        {
            string libraryNamePrefix = GetLibraryPrefix(libraryName);
            if (!string.IsNullOrEmpty(libraryNamePrefix) && title.StartsWith(libraryNamePrefix))
                return title.Substring(libraryNamePrefix.Length);
            return title;
        }

        public static string GetTitle(string libraryName, PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode, IsotopeLabelType labelType)
        {
            string libraryNamePrefix = GetLibraryPrefix(libraryName);

            TransitionGroup transitionGroup = transitionGroupDocNode.TransitionGroup;
            string sequence;
            if (transitionGroup.Peptide.IsCustomMolecule)
            {
                sequence = transitionGroupDocNode.CustomMolecule.DisplayName;
            }
            else if (peptideDocNode.CrosslinkStructure.HasCrosslinks)
            {
                sequence = peptideDocNode.GetCrosslinkedSequence();
            }
            else
            {
                sequence = transitionGroup.Peptide.Target.Sequence;
            }

            var charge = transitionGroup.PrecursorAdduct.ToString(); // Something like "2" or "-3" for protonation, or "[M+Na]" for small molecules
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

        public override string Title
        {
            get { return GetTitle(LibraryName, PeptideDocNode, TransitionGroupNode, SpectrumInfo.LabelType); }
        }
    }
    
    public abstract class AbstractSpectrumGraphItem : AbstractMSGraphItem
    {
        private const string FONT_FACE = "Arial";
        public static readonly Color COLOR_SELECTED = Color.Red;

        private readonly Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> _ionMatches;
        public LibraryRankedSpectrumInfo SpectrumInfo { get; protected set; }
        public int PeaksCount { get { return SpectrumInfo.Peaks.Count; } }
        public int PeaksMatchedCount { get { return SpectrumInfo.PeaksMatched.Count(); } }
        public int PeaksRankedCount { get { return SpectrumInfo.PeaksRanked.Count(); } }
        public ICollection<IonType> ShowTypes { get; set; }
        public ICollection<int> ShowCharges { get; set; } // List of absolute charge values to display CONSIDER(bspratt): may want finer per-adduct control for small mol use
        public bool ShowRanks { get; set; }
        public bool ShowScores { get; set; }
        public bool ShowMz { get; set; }
        public bool ShowObservedMz { get; set; }
        public bool ShowMassError { get; set; }
        public bool ShowDuplicates { get; set; }
        public ICollection<string> ShowLosses { get; set; }
        public float FontSize { get; set; }
        public bool Invert { get; set; }

        // ReSharper disable InconsistentNaming
        private FontSpec _fontSpecA;
        private FontSpec FONT_SPEC_A { get { return GetFontSpec(IonTypeExtension.GetTypeColor(IonType.a), ref _fontSpecA); } }
        private FontSpec _fontSpecX;
        private FontSpec FONT_SPEC_X { get { return GetFontSpec(IonTypeExtension.GetTypeColor(IonType.x), ref _fontSpecX); } }
        private FontSpec _fontSpecB;
        private FontSpec FONT_SPEC_B { get { return GetFontSpec(IonTypeExtension.GetTypeColor(IonType.b), ref _fontSpecB); } }
        private FontSpec _fontSpecY;
        private FontSpec FONT_SPEC_Y { get { return GetFontSpec(IonTypeExtension.GetTypeColor(IonType.y), ref _fontSpecY); } }
        private FontSpec _fontSpecC;
        private FontSpec FONT_SPEC_C { get { return GetFontSpec(IonTypeExtension.GetTypeColor(IonType.c), ref _fontSpecC); } }
        private FontSpec _fontSpecPrecursor;
        private FontSpec FONT_SPEC_PRECURSOR { get { return GetFontSpec(IonTypeExtension.GetTypeColor(IonType.precursor), ref _fontSpecPrecursor); } }

        private FontSpec _fontSpecZ;
        private FontSpec FONT_SPEC_Z { get { return GetFontSpec(IonTypeExtension.GetTypeColor(IonType.z), ref _fontSpecZ); } }

        private FontSpec _fontSpecZH;
        private FontSpec FONT_SPEC_ZH { get { return GetFontSpec(IonTypeExtension.GetTypeColor(IonType.zh), ref _fontSpecZH); } }

        private FontSpec _fontSpecZHH;
        private FontSpec FONT_SPEC_ZHH { get { return GetFontSpec(IonTypeExtension.GetTypeColor(IonType.zhh), ref _fontSpecZHH); } }


        private FontSpec _fontSpecOtherIons;
        private FontSpec _fontSpecNone;
        private FontSpec FONT_SPEC_NONE { get { return GetFontSpec(IonTypeExtension.GetTypeColor(null), ref _fontSpecNone); } }
        private FontSpec _fontSpecSelected;
        private FontSpec FONT_SPEC_SELECTED { get { return GetFontSpec(COLOR_SELECTED, ref _fontSpecSelected); } }
        // ReSharper restore InconsistentNaming

        private FontSpec GetOtherIonsFontSpec(int rank = 0)
        {
            // Consider the rank of small molecule fragments when selecting the color for the FontSpec
            return GetFontSpec(IonTypeExtension.GetTypeColor(IonType.custom, rank), ref _fontSpecOtherIons);
        }
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
                var intensities = Invert
                    ? SpectrumInfo.Intensities.Select(i => -i).ToArray()
                    : SpectrumInfo.Intensities.ToArray();

                return new PointPairList(SpectrumInfo.MZs.ToArray(), intensities);
            }
        }

        public override void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
        {
            // Do nothing
        }

        private static Color InvertColor(Color color)
        {
            var hsb = HSBColor.FromRGB(color);
            hsb.H += 64;
            return HSBColor.ToRGB(hsb);
        }
        
        public override void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
        {
            // ReSharper disable UseObjectOrCollectionInitializer
            foreach (var rmi in SpectrumInfo.PeaksMatched)
            {
                if (!IsVisibleIon(rmi))
                    continue;

                var matchedIon = rmi.MatchedIons.First(IsVisibleIon);

                Color color = IonTypeExtension.GetTypeColor(matchedIon.IonType, rmi.Rank);

                if (Invert)
                    color = InvertColor(color);

                if (rmi.MatchedIons.Any(mfi => IsMatch(mfi.PredictedMz)))
                {
                    color = COLOR_SELECTED;
                }

                double mz = rmi.ObservedMz;
                var intensity = Invert ? -rmi.Intensity : rmi.Intensity;
                var stick = new LineObj(color, mz, intensity, mz, 0);
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
                case IonType.zh: fontSpec = FONT_SPEC_ZH; break;
                case IonType.zhh: fontSpec = FONT_SPEC_ZHH; break;
                case IonType.custom:
                    {
                    if (rmi.Rank == 0 && !rmi.HasAnnotations)
                        return null; // Small molecule fragments - only force annotation if ranked
                    fontSpec = GetOtherIonsFontSpec(rmi.Rank);
                    }
                    break;
                case IonType.precursor: fontSpec = FONT_SPEC_PRECURSOR; break;
            }
            if (rmi.MatchedIons.Any(mfi => IsMatch(mfi.PredictedMz)))
                fontSpec = FONT_SPEC_SELECTED;
            if (Invert)
            {
                fontSpec = fontSpec.Clone();
                fontSpec.FontColor = InvertColor(fontSpec.FontColor);
            }
                
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

            if (ShowMassError)
            {
                var massError = rmi.MatchedIons.First().PredictedMz - rmi.ObservedMz;
                massError = SequenceMassCalc.GetPpm(rmi.MatchedIons.First().PredictedMz, massError);
                massError = Math.Round(massError, 1);
                sb.AppendLine().Append(string.Format(Resources.GraphSpectrum_MassErrorFormat_ppm, (massError > 0 ? @"+" : string.Empty), massError));
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
            return mfi.Ordinal > 0 && ShowTypes.Contains(mfi.IonType) && mfi.HasVisibleLoss(ShowLosses) && 
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

    public class ExceptionMSGraphItem : NoDataMSGraphItem
    {
        public ExceptionMSGraphItem(Exception exception) : base(exception.Message)
        {
            Exception = exception;
        }

        public Exception Exception { get; }
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
