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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using pwiz.MSGraph;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public class SpectrumGraphItem : AbstractMSGraphItem
    {
        private const string FONT_FACE = "Arial";
        private static readonly Color COLOR_A = Color.YellowGreen;
        private static readonly Color COLOR_X = Color.Green;
        private static readonly Color COLOR_B = Color.BlueViolet;
        private static readonly Color COLOR_Y = Color.Blue;
        private static readonly Color COLOR_C = Color.Orange;
        private static readonly Color COLOR_Z = Color.OrangeRed;
        private static readonly Color COLOR_PRECURSOR = Color.DarkCyan;
        private static readonly Color COLOR_NONE = Color.Gray;
        public static readonly Color COLOR_SELECTED = Color.Red;

        private static FontSpec CreateFontSpec(Color color, float size)
        {
            return new FontSpec(FONT_FACE, size, color, false, false, false) {Border = {IsVisible = false}};
        }

        private readonly Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> _ionMatches;

        public SpectrumGraphItem(TransitionGroupDocNode transitionGroupNode, TransitionDocNode transition,
                                 LibraryRankedSpectrumInfo spectrumInfo)
        {
            TransitionGroupNode = transitionGroupNode;
            TransitionNode = transition;
            SpectrumInfo = spectrumInfo;

            _ionMatches = spectrumInfo.PeaksMatched.ToDictionary(rmi => rmi.ObservedMz);

            // Default values
            FontSize = 10;
            LineWidth = 1;
        }

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
        private FontSpec _fontSpecNone;
        private FontSpec FONT_SPEC_NONE { get { return GetFontSpec(COLOR_NONE, ref _fontSpecNone); } }
        private FontSpec _fontSpecSelected;
        private FontSpec FONT_SPEC_SELECTED { get { return GetFontSpec(COLOR_SELECTED, ref _fontSpecSelected); } }
        // ReSharper restore InconsistentNaming

        private FontSpec GetFontSpec(Color color, ref FontSpec fontSpec)
        {
            if (fontSpec == null)
                fontSpec = CreateFontSpec(color, FontSize);
            return fontSpec;
        }

        private TransitionGroupDocNode TransitionGroupNode { get; set; }
        private TransitionDocNode TransitionNode { get; set; }
        private LibraryRankedSpectrumInfo SpectrumInfo { get; set; }

        public ICollection<IonType> ShowTypes { get; set; }
        public ICollection<int> ShowCharges { get; set; }
        public bool ShowRanks { get; set; }
        public bool ShowDuplicates { get; set; }
        public int LineWidth { get; set; }
        public float FontSize { get; set; }

        public string LibraryName { get { return TransitionGroupNode.LibInfo.LibraryName; } }
        public int PeaksCount { get { return SpectrumInfo.Peaks.Count; } }
        public int PeaksMatchedCount { get { return SpectrumInfo.PeaksMatched.Count(); } }
        public int PeaksRankedCount { get { return SpectrumInfo.PeaksRanked.Count(); } }

        public override void CustomizeCurve(CurveItem curveItem)
        {
            ((LineItem)curveItem).Line.Width = LineWidth;
        }

        public override string Title
        {
            get
            {
                string libraryNamePrefix = TransitionGroupNode.LibInfo.LibraryName;
                if (!string.IsNullOrEmpty(libraryNamePrefix))
                    libraryNamePrefix += " - ";

                TransitionGroup transitionGroup = TransitionGroupNode.TransitionGroup;
                string sequence = transitionGroup.Peptide.Sequence;
                int charge = transitionGroup.PrecursorCharge;
                var type = SpectrumInfo.LabelType;
                if (type == IsotopeLabelType.light)
                    return string.Format("{0}{1}, Charge {2}", libraryNamePrefix, sequence, charge);
                else
                    return string.Format("{0}{1}, Charge {2} ({3})", libraryNamePrefix, sequence, charge, type);
            }
        }

        public override IPointList Points
        {
            get
            {
                return new PointPairList(SpectrumInfo.MZs.ToArray(),
                                         SpectrumInfo.Intensities.ToArray());
            }
        }

        public override void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
        {
            // ReSharper disable UseObjectOrCollectionInitializer
            foreach (var rmi in SpectrumInfo.PeaksMatched)
            {
                if (!IsVisibleIon(rmi))
                    continue;

                IonType type = IsVisibleIon(rmi.IonType, rmi.Ordinal, rmi.Charge) ?
                                                                                      rmi.IonType : rmi.IonType2;

                Color color;
                switch (type)
                {
                    default: color = COLOR_NONE; break;
                    case IonType.a: color = COLOR_A; break;
                    case IonType.x: color = COLOR_X; break;
                    case IonType.b: color = COLOR_B; break;
                    case IonType.y: color = COLOR_Y; break;
                    case IonType.c: color = COLOR_C; break;
                    case IonType.z: color = COLOR_Z; break;
                    case IonType.precursor: color = COLOR_PRECURSOR; break;
                }
                if (TransitionNode != null && rmi.PredictedMz == TransitionNode.Mz)
                    color = COLOR_SELECTED;
                double mz = rmi.ObservedMz;
                LineObj stick = new LineObj(color, mz, rmi.Intensity, mz, 0);
                stick.IsClippedToChartRect = true;
                stick.Location.CoordinateFrame = CoordType.AxisXYScale;
                stick.Line.Width = LineWidth + 1;
                annotations.Add(stick);                
            }
            // ReSharper restore UseObjectOrCollectionInitializer
        }

        public override PointAnnotation AnnotatePoint(PointPair point)
        {
            LibraryRankedSpectrumInfo.RankedMI rmi;
            if (!_ionMatches.TryGetValue(point.X, out rmi) || !IsVisibleIon(rmi))
                return null;

            string[] parts = new string[2];
            int i = 0;
            if (IsVisibleIon(rmi.IonType, rmi.Ordinal, rmi.Charge))
                parts[i++] = GetLabel(rmi.IonType, rmi.Ordinal, rmi.Charge, rmi.Rank);
            if (IsVisibleIon(rmi.IonType2, rmi.Ordinal2, rmi.Charge2))
                parts[i] = GetLabel(rmi.IonType2, rmi.Ordinal2, rmi.Charge2, 0);
            StringBuilder sb = new StringBuilder();
            foreach (string part in parts)
            {
                if (part == null)
                    continue;
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append(part);
            }
            FontSpec fontSpec;
            switch (rmi.IonType)
            {
                default: fontSpec = FONT_SPEC_NONE; break;
                case IonType.a: fontSpec = FONT_SPEC_A; break;
                case IonType.x: fontSpec = FONT_SPEC_X; break;
                case IonType.b: fontSpec = FONT_SPEC_B; break;
                case IonType.y: fontSpec = FONT_SPEC_Y; break;
                case IonType.c: fontSpec = FONT_SPEC_C; break;
                case IonType.z: fontSpec = FONT_SPEC_Z; break;
                case IonType.precursor: fontSpec = FONT_SPEC_PRECURSOR; break;
            }
            if (TransitionNode != null && rmi.PredictedMz == TransitionNode.Mz)
                fontSpec = FONT_SPEC_SELECTED;
            return new PointAnnotation(sb.ToString(), fontSpec);
        }

        private string GetLabel(IonType type, int ordinal, int charge, int rank)
        {
            string chargeIndicator = (charge == 1 ? "" : Transition.GetChargeIndicator(charge));
            string label = type.ToString();
            if (!Transition.IsPrecursor(type))
                label = label + ordinal + chargeIndicator;
            if (rank > 0 && ShowRanks)
                label = string.Format("{0} (rank {1})", label, rank);
            return label;
        }

        private bool IsVisibleIon(LibraryRankedSpectrumInfo.RankedMI rmi)
        {
            bool singleIon = (rmi.Ordinal2 == 0);
            if (ShowDuplicates && singleIon)
                return false;
            return IsVisibleIon(rmi.IonType, rmi.Ordinal, rmi.Charge) ||
                   IsVisibleIon(rmi.IonType2, rmi.Ordinal2, rmi.Charge2);
        }

        private bool IsVisibleIon(IonType type, int ordinal, int charge)
        {
            return ordinal > 0 && ShowTypes.Contains(type) && ShowCharges.Contains(charge);
        }
    }

    public sealed class UnavailableMSGraphItem : NoDataMSGraphItem
    {
        public UnavailableMSGraphItem() : base("Spectrum information unavailable")
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
        public abstract IPointList Points { get; }

        public Color Color
        {
            get { return Color.Gray; }
        }

        public virtual void CustomizeCurve(CurveItem curveItem)
        {
            // Do nothing by default            
        }

        public MSGraphItemType GraphItemType
        {
            get { return MSGraphItemType.Spectrum; }
        }

        public MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return MSGraphItemDrawMethod.Stick; }
        }

        public void CustomizeYAxis(Axis axis)
        {
            CustomizeAxis(axis, "Intensity");
        }

        public void CustomizeXAxis(Axis axis)
        {
            CustomizeAxis(axis, "M/Z");
        }

        private static void CustomizeAxis(Axis axis, string title)
        {
            axis.Title.FontSpec.Family = "Arial";
            axis.Title.FontSpec.Size = 14;
            axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
            axis.Title.FontSpec.Border.IsVisible = false;
            axis.Title.Text = title;
        }
    }
}