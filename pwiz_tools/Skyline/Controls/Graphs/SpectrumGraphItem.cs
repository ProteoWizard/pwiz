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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Identifies a specific ion series: one ion type at one charge state, optionally
    /// with a neutral loss. Used to track which individual series are pinned or hovered.
    /// </summary>
    public readonly struct IonSeriesKey : IEquatable<IonSeriesKey>
    {
        public readonly IonType IonType;
        /// <summary>Signed adduct charge.</summary>
        public readonly int Charge;
        /// <summary>Neutral loss applied to this series; null for non-loss ions.</summary>
        public readonly TransitionLosses Losses;

        public IonSeriesKey(IonType ionType, int charge, TransitionLosses losses = null)
        {
            IonType = ionType;
            Charge = charge;
            Losses = losses;
        }

        /// <summary>
        /// Returns the group key this series belongs to. Non-loss ions of the same
        /// direction+charge share a group; each neutral-loss series gets its own group.
        /// </summary>
        public RulerGroupKey GroupKey => Losses == null
            ? new RulerGroupKey(IonType.IsNTerminal(), Charge, null, null)
            : new RulerGroupKey(IonType.IsNTerminal(), Charge, Losses, IonType);

        public bool Equals(IonSeriesKey other) =>
            IonType == other.IonType && Charge == other.Charge && Equals(Losses, other.Losses);
        public override bool Equals(object obj) => obj is IonSeriesKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int h = ((int)IonType * 397) ^ Charge;
                return (h * 397) ^ (Losses?.GetHashCode() ?? 0);
            }
        }
        public override string ToString() => Losses == null
            ? @$"{IonType} z={Charge}"
            : @$"{IonType}-{Math.Round(Losses.Mass, 1)} z={Charge}";
    }

    /// <summary>
    /// Identifies a ruler group. Non-loss groups merge all ion types of the same direction
    /// and charge (Losses == null, LossIonType == null). Each neutral-loss series is its
    /// own group, keyed by (direction, charge, losses, ion type).
    /// </summary>
    public readonly struct RulerGroupKey : IEquatable<RulerGroupKey>
    {
        /// <summary>True for b/a/c series; false for y/x/z series.</summary>
        public readonly bool IsNTerminal;

        /// <summary>Signed adduct charge (e.g. +1, +2, -1).</summary>
        public readonly int Charge;

        /// <summary>Neutral loss for this group; null for the shared non-loss group.</summary>
        public readonly TransitionLosses Losses;

        /// <summary>Ion type for loss groups; null for the shared non-loss group.</summary>
        public readonly IonType? LossIonType;

        public RulerGroupKey(bool isNTerminal, int charge, TransitionLosses losses, IonType? lossIonType)
        {
            IsNTerminal = isNTerminal;
            Charge = charge;
            Losses = losses;
            LossIonType = lossIonType;
        }

        public bool Equals(RulerGroupKey other) =>
            IsNTerminal == other.IsNTerminal && Charge == other.Charge
            && Equals(Losses, other.Losses) && LossIonType == other.LossIonType;

        public override bool Equals(object obj) =>
            obj is RulerGroupKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (IsNTerminal.GetHashCode() * 397) ^ Charge;
                h = (h * 397) ^ (Losses?.GetHashCode() ?? 0);
                return (h * 397) ^ (LossIonType?.GetHashCode() ?? 0);
            }
        }

        public override string ToString() =>
            string.Format(GraphsResources.RulerGroupKey_ToString__0__terminal_z__1_,
                IsNTerminal ? @"N" : @"C", Charge);
    }

    public class SpectrumGraphItem : AbstractSpectrumGraphItem
    {
        public PeptideDocNode PeptideDocNode { get; private set; }
        public TransitionGroupDocNode TransitionGroupNode { get; private set; }
        private TransitionDocNode TransitionNode { get; set; }
        public string LibraryName { get; private set; }
        public SrmSettings SrmSettings { get; set; }

        /// <summary>The specific ion series currently highlighted by mouse-over; null = no ruler shown.</summary>
        public IonSeriesKey? HoveredSeriesKey { get; set; }

        /// <summary>Individual ion series pinned by the user, in order of pinning.</summary>
        public IReadOnlyList<IonSeriesKey> PinnedSeriesKeys { get; set; } = Array.Empty<IonSeriesKey>();

        /// <summary>
        /// In a mirror-spectrum display, the SpectrumGraphItem rendering the inverted
        /// (negative-intensity) panel below the x-axis. The single ruler is always drawn
        /// from the top item; setting MirrorItem on the top item lets its drop lines also
        /// consider matched peaks on the mirror side when picking where to stop.
        /// </summary>
        public SpectrumGraphItem MirrorItem { get; set; }

        /// <summary>
        /// Whether amino-acid sequence rulers apply to this spectrum. False for small
        /// molecules (no defined sequence), crosslinked peptides, or when settings are
        /// unavailable — in those cases no ruler is drawn, hovered, or offered for pinning.
        /// </summary>
        public bool RulersApplicable =>
            PeptideDocNode != null && PeptideDocNode.IsProteomic &&
            !PeptideDocNode.CrosslinkStructure.HasCrosslinks && SrmSettings != null;

        /// <summary>
        /// Resolves an observed peak to the ion series whose ruler should be shown when the
        /// peak is hovered. Picks the matched ion with the smallest absolute mass error —
        /// the best explanation for the peak — and returns its <see cref="IonSeriesKey"/>,
        /// or null when the peak has no matched ions. The chosen ion's losses (if any) become
        /// part of the key so neutral-loss series get their own ruler. Shared by all three
        /// spectrum hosts (GraphFullScan, GraphSpectrum, ViewLibraryDlg) so mouse-over maps a
        /// peak to a ruler series identically everywhere.
        /// </summary>
        public static IonSeriesKey? GetBestSeriesKey(LibraryRankedSpectrumInfo.RankedMI peakRmi)
        {
            if (peakRmi?.MatchedIons == null || peakRmi.MatchedIons.Count == 0)
                return null;

            MatchedFragmentIon bestIon = null;
            double bestError = double.MaxValue;
            foreach (var mfi in peakRmi.MatchedIons)
            {
                // Skip ions that don't have a positional sequence ladder. Precursor and
                // custom ion types aren't fragment series, so picking one as the hover
                // key would set HoveredSeriesKey to something the renderer can't draw
                // and leave the user with no visible ruler on a hovered peak.
                if (mfi.IonType == IonType.precursor || mfi.IonType == IonType.custom)
                    continue;
                double error = Math.Abs(SequenceMassCalc.GetPpm(mfi.PredictedMz,
                    mfi.PredictedMz - peakRmi.ObservedMz));
                // On an exact-tie ppm error, fall back to a deterministic secondary key
                // (IonType, Ordinal, |charge|) so the chosen ruler doesn't depend on the
                // MatchedIons insertion order.
                if (bestIon == null || error < bestError ||
                    (error == bestError && CompareIonTieBreak(mfi, bestIon) < 0))
                {
                    bestError = error;
                    bestIon = mfi;
                }
            }
            return bestIon == null
                ? (IonSeriesKey?)null
                : new IonSeriesKey(bestIon.IonType, bestIon.Charge.AdductCharge, bestIon.Losses);
        }

        private static int CompareIonTieBreak(MatchedFragmentIon a, MatchedFragmentIon b)
        {
            int c = a.IonType.CompareTo(b.IonType);
            if (c != 0) return c;
            c = a.Ordinal.CompareTo(b.Ordinal);
            if (c != 0) return c;
            return Math.Abs(a.Charge.AdductCharge).CompareTo(Math.Abs(b.Charge.AdductCharge));
        }

        /// <summary>
        /// Whether the spectrum sequence-ruler feature is turned on. Persisted as a global
        /// user preference so the Enable/Disable context-menu toggle applies across all
        /// spectrum hosts and sessions. When false, no ruler is rendered, hovered, or offered
        /// for pinning (only the Enable Rulers menu item remains). Independent of
        /// <see cref="RulersApplicable"/>, which gates on whether the spectrum type supports
        /// rulers at all.
        /// </summary>
        public static bool RulersEnabled
        {
            get { return Settings.Default.SpectrumSequenceRulersEnabled; }
            set { Settings.Default.SpectrumSequenceRulersEnabled = value; }
        }

        /// <summary>
        /// Label for the master ruler on/off context-menu item, reflecting the current state:
        /// "Disable Rulers" when enabled, "Enable Rulers" when disabled.
        /// </summary>
        public static string RulerToggleMenuText =>
            RulersEnabled
                ? GraphsResources.SequenceRulerMenu_DisableRulers
                : GraphsResources.SequenceRulerMenu_EnableRulers;

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

        protected override bool IsProteomic()
        {
            return PeptideDocNode.IsProteomic;
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
                ? string.Format(GraphsResources.SpectrumGraphItem_Title__0__1__Charge__2__, libraryNamePrefix, sequence, charge)
                : string.Format(GraphsResources.SpectrumGraphItem_Title__0__1__Charge__2__3__, libraryNamePrefix, sequence, charge, labelType);
        }

        public override string Title
        {
            get
            {
                var title = GetTitle(LibraryName, PeptideDocNode, TransitionGroupNode, SpectrumInfo.LabelType);
                if (PeaksCount == 0)
                {
                    title += SettingsUIResources.SpectrumGraphItem_library_entry_provides_only_precursor_values;
                }
                return title;
            }
        }

        private const float RULER_SLOT_HEIGHT = 0.07f;

        public override void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
        {
            if (!RulersApplicable || !RulersEnabled)
                return;

            // Build groups from pinned series, preserving order of first appearance.
            // A group = all pinned series sharing the same (direction, charge).
            var groupOrder  = new List<RulerGroupKey>();
            var groupIonTypes = new Dictionary<RulerGroupKey, List<IonType>>();

            foreach (var key in PinnedSeriesKeys)
            {
                var gk = key.GroupKey;
                if (!groupIonTypes.ContainsKey(gk))
                {
                    groupOrder.Add(gk);
                    groupIonTypes[gk] = new List<IonType>();
                }
                if (!groupIonTypes[gk].Contains(key.IonType))
                    groupIonTypes[gk].Add(key.IonType);
            }

            // Add the hovered series to its group (or create a new last-slot group).
            if (HoveredSeriesKey.HasValue)
            {
                var hk = HoveredSeriesKey.Value;
                var gk = hk.GroupKey;
                if (!groupIonTypes.ContainsKey(gk))
                {
                    groupOrder.Add(gk);
                    groupIonTypes[gk] = new List<IonType>();
                }
                if (!groupIonTypes[gk].Contains(hk.IonType))
                    groupIonTypes[gk].Add(hk.IonType);
            }

            if (groupOrder.Count == 0)
                return;

            // Pre-compute shared ion mass data
            var calc = SrmSettings.GetFragmentCalc(
                TransitionGroupNode.TransitionGroup.LabelType, PeptideDocNode.ExplicitMods);
            var plainTarget = PeptideDocNode.Peptide.Target;
            var ionMasses = calc.GetFragmentIonMasses(plainTarget);
            int nSeq = ionMasses.GetLength(1);
            var precursorMass = calc.GetPrecursorFragmentMass(plainTarget);
            var allResidues = AminoAcidLadderObj.ParseModifiedSequenceResidues(
                PeptideDocNode.ModifiedSequenceDisplay);

            // Build matched-peak intensity lookup keyed by ion identity including losses,
            // so non-loss and loss series resolve to their own matched peaks independently.
            var intensityLookup = BuildIntensityLookup(SpectrumInfo);
            // For mirror-spectrum displays, also look up matches in the mirror panel so the
            // drop lines can choose the topmost endpoint between the two spectra.
            var mirrorIntensityLookup = MirrorItem != null
                ? BuildIntensityLookup(MirrorItem.SpectrumInfo)
                : null;

            for (int slot = 0; slot < groupOrder.Count; slot++)
            {
                var gk = groupOrder[slot];
                float yLine = 0.04f + slot * RULER_SLOT_HEIGHT;
                var ladder = BuildGroupLadder(gk, groupIonTypes[gk], allResidues, ionMasses,
                    nSeq, precursorMass, intensityLookup, mirrorIntensityLookup, yLine);
                if (ladder != null)
                    annotations.Add(ladder);
            }
        }

        private static Dictionary<(IonType, int, int, TransitionLosses), double>
            BuildIntensityLookup(LibraryRankedSpectrumInfo spectrumInfo)
        {
            var lookup = new Dictionary<(IonType, int, int, TransitionLosses), double>();
            foreach (var rmi in spectrumInfo.PeaksMatched)
            {
                if (rmi.MatchedIons == null) continue;
                foreach (var mfi in rmi.MatchedIons)
                {
                    lookup[(mfi.IonType, mfi.Charge.AdductCharge, mfi.Ordinal, mfi.Losses)] = rmi.Intensity;
                }
            }
            return lookup;
        }

        private AminoAcidLadderObj BuildGroupLadder(
            RulerGroupKey key,
            List<IonType> ionTypes,
            string[] allResidues,
            IonTable<TypedMass> ionMasses,
            int nSeq,
            TypedMass precursorMass,
            Dictionary<(IonType ionType, int charge, int ordinal, TransitionLosses losses), double> intensityLookup,
            Dictionary<(IonType ionType, int charge, int ordinal, TransitionLosses losses), double> mirrorIntensityLookup,
            float yLine)
        {
            // Subtract the loss mass from each boundary so loss-ion rulers sit at the
            // correct m/z positions (e.g. y-18 is 18 Da lower than y).
            double lossMass = key.Losses?.Mass ?? 0;

            var seriesList = new List<IonSeriesData>();
            foreach (var ionType in ionTypes)
            {
                var boundaries = new double[nSeq + 1];
                bool valid = true;
                for (int k = 1; k <= nSeq; k++)
                {
                    var mass = ionMasses.GetIonValue(ionType, k);
                    if (mass <= 0) { valid = false; break; }
                    boundaries[k - 1] = SequenceMassCalc.GetMZ(mass - lossMass, key.Charge);
                }
                if (!valid) continue;
                boundaries[nSeq] = SequenceMassCalc.GetMZ(precursorMass - lossMass, key.Charge);

                var peakIntensities = new Dictionary<int, double>();
                Dictionary<int, double> mirrorPeakIntensities = null;
                for (int ordinal = 1; ordinal <= nSeq; ordinal++)
                {
                    if (intensityLookup.TryGetValue((ionType, key.Charge, ordinal, key.Losses), out double intensity))
                        peakIntensities[ordinal] = intensity;
                    if (mirrorIntensityLookup != null &&
                        mirrorIntensityLookup.TryGetValue((ionType, key.Charge, ordinal, key.Losses), out double mirrorIntensity))
                    {
                        mirrorPeakIntensities = mirrorPeakIntensities ?? new Dictionary<int, double>();
                        mirrorPeakIntensities[ordinal] = mirrorIntensity;
                    }
                }

                seriesList.Add(new IonSeriesData(ionType, boundaries, peakIntensities, mirrorPeakIntensities));
            }

            if (seriesList.Count == 0)
                return null;

            // Reference series for label positions: b for N-terminal, y for C-terminal
            var refIonType = key.IsNTerminal ? IonType.b : IonType.y;
            var refSeries = seriesList.FirstOrDefault(s => s.IonType == refIonType) ?? seriesList[0];

            // Residue labels
            var labels = new string[nSeq];
            if (key.IsNTerminal)
            {
                for (int i = 0; i < nSeq; i++)
                    labels[i] = i + 1 < allResidues.Length ? allResidues[i + 1] : string.Empty;
            }
            else
            {
                for (int i = 0; i < nSeq; i++)
                {
                    int ri = nSeq - i - 1;
                    labels[i] = ri >= 0 && ri < allResidues.Length ? allResidues[ri] : string.Empty;
                }
            }

            // Loss text shown in the group label, e.g. "-18" or "-98.1".
            string lossText = key.Losses == null ? string.Empty :
                @"-" + Math.Round(key.Losses.Mass, 1);

            return new AminoAcidLadderObj(labels, refSeries.Boundaries, seriesList,
                yLine, yLine, FontSize * 0.7f, key.Charge, lossText);
        }
    }
    
    public abstract class AbstractSpectrumGraphItem : AbstractMSGraphItem
    {
        protected const string FONT_FACE = "Arial";
        public static readonly Color COLOR_SELECTED = Color.Red;

        private readonly Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> _ionMatches;
        public LibraryRankedSpectrumInfo SpectrumInfo { get; protected set; }
        public int PeaksCount { get { return SpectrumInfo.Peaks.Count; } }
        public int PeaksMatchedCount { get { return SpectrumInfo.PeaksMatched.Count(); } }
        public int PeaksRankedCount { get { return SpectrumInfo.PeaksRanked.Count(); } }
        public ICollection<IonType> ShowTypes { get; set; }
        public ICollection<int> ShowCharges { get; set; } // List of absolute charge values to display CONSIDER(bspratt): may want finer per-adduct control for small mol use
        public bool ShowRanks { get; set; }
        public bool ShowMz { get; set; }
        public bool ShowObservedMz { get; set; }
        public bool ShowMassError { get; set; }
        public bool ShowDuplicates { get; set; }
        public float FontSize { get; set; }
        public bool Invert { get; set; }
        public ICollection<string> ShowLosses { get; set; }

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
        protected abstract bool IsProteomic();

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

                var matchedIon = rmi.MatchedIonsSorted.First(IsVisibleIon);

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
        }

        public override PointAnnotation AnnotatePoint(PointPair point)
        {
            LibraryRankedSpectrumInfo.RankedMI rmi;
            if (!_ionMatches.TryGetValue(point.X, out rmi) || !IsVisibleIon(rmi))
                return null;

            var matchedIon = rmi.MatchedIonsSorted.First(IsVisibleIon);

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
                    if (rmi.Rank == 0 && !rmi.HasAnnotations && !IsProteomic())
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
       
        public string GetLabel(LibraryRankedSpectrumInfo.RankedMI rmi)
        {
            // Show the m/z values in the labels, if multiple should be visible, and
            // they have different display values.
            bool showMzInLabel = ShowMz &&
                                 rmi.MatchedIons.Where(IsVisibleIon)
                                     .Select(mfi => GetDisplayMz(mfi.PredictedMz))
                                     .Distinct()
                                     .Count() > 1;
                
            StringBuilder sb = new StringBuilder();
            foreach (var mfi in rmi.MatchedIonsSorted.Where(IsVisibleIon))
            {
                if (sb.Length > 0)
                    sb.AppendLine();

                sb.Append(GetLabel(mfi, sb.Length == 0 ? rmi.Rank : 0, showMzInLabel, ShowRanks));
            }
            // If predicted m/z should be displayed, but hasn't been yet, then display now.
            if (ShowMz && !showMzInLabel)
            {
                sb.AppendLine().Append(GetDisplayMz(rmi.MatchedIonsSorted.First().PredictedMz));
            }
            // If showing observed m/z, and it is different from the predicted m/z, then display it last.
            if (ShowObservedMz)
            {
                sb.AppendLine().Append(GetDisplayMz(rmi.ObservedMz));
            }

            if (ShowMassError)
            {
                sb.AppendLine().Append(GetMassErrorString(rmi, rmi.MatchedIonsSorted.First()));
            }
            return sb.ToString();
        }

        public static string GetMassErrorString(LibraryRankedSpectrumInfo.RankedMI rmi, MatchedFragmentIon mfi)
        {
            var massError = mfi.PredictedMz - rmi.ObservedMz;
            massError = SequenceMassCalc.GetPpm(mfi.PredictedMz, massError);
            massError = Math.Round(massError, 1);
            return string.Format(Resources.GraphSpectrum_MassErrorFormat_ppm, (massError > 0 ? @"+" : string.Empty), massError);
        }

        public static string GetLabel(MatchedFragmentIon mfi, int rank, bool showMz, bool showRank)
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
            if (rank > 0 && showRank)
                label.Append(TextUtil.SEPARATOR_SPACE).Append(string.Format(@"({0})",string.Format(Resources.AbstractSpectrumGraphItem_GetLabel_rank__0__, rank)));
            return label.ToString();
        }

        private double GetDisplayMz(double mz)
        {
            // Try to show enough decimal places to distinguish by tolerance
            int places = 1;
            while (places < 4 && ((int) (SpectrumInfo.Tolerance.GetMzTolerance(mz)*Math.Pow(10, places))) == 0)
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
            return ((mfi.Ordinal > 0 || IonType.custom.Equals(mfi.IonType)) && ShowTypes.Contains(mfi.IonType)) && mfi.HasVisibleLoss(ShowLosses) &&
                (mfi.IonType == IonType.precursor || ShowCharges.Contains(Math.Abs(mfi.Charge.AdductCharge)));
        }
    }

    public sealed class UnavailableMSGraphItem : NoDataMSGraphItem
    {
        public UnavailableMSGraphItem() : base(GraphsResources.UnavailableMSGraphItem_UnavailableMSGraphItem_Spectrum_information_unavailable)
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
            CustomizeAxis(axis, GraphsResources.AbstractMSGraphItem_CustomizeYAxis_Intensity);
        }

        public void CustomizeXAxis(Axis axis)
        {
            CustomizeAxis(axis, GraphsResources.AbstractMSGraphItem_CustomizeXAxis_MZ);
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
