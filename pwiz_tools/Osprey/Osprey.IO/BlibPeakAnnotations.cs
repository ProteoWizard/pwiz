/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Globalization;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// One row of a blib's <c>RefSpectraPeakAnnotations</c> table, as <see cref="BlibLoader"/>
    /// reads it: which peak it names, the ion name, and the charge column.
    /// </summary>
    internal struct BlibAnnotationRow
    {
        public int PeakIndex;
        public string Name;
        public int Charge;
    }

    /// <summary>What <see cref="BlibPeakAnnotations.Apply"/> did across a whole library.</summary>
    internal sealed class BlibAnnotationStats
    {
        /// <summary>Every spectrum read while annotations were being applied, with rows or without.</summary>
        public int NSpectra;

        public int NSpectraAnnotated;
        public int NPeaksAnnotated;
        public int NPeaksUnannotated;
        public int NRejectedName;

        /// <summary>Well-formed a, c, x or z annotations, which cannot be checked against the peak.</summary>
        public int NUncheckedIonType;

        /// <summary>Annotations naming a peak the spectrum lacks, or an ion as long as the peptide.</summary>
        public int NRejectedRange;

        public int NRejectedMz;

        public string Summary()
        {
            return string.Format(OspreyIOResources.BlibAnnotationStats_Summary_Library_fragment_annotations___0_N0__of__1_N0__spectra_typed,
                NSpectraAnnotated, NSpectra, NPeaksAnnotated, NPeaksUnannotated, NRejectedName, NUncheckedIonType, NRejectedRange, NRejectedMz);
        }
    }

    /// <summary>
    /// Types a blib's peaks from its <c>RefSpectraPeakAnnotations</c> table, so a peptide blib
    /// library searches with the same fragment annotations as the DIA-NN TSV it was built from.
    /// Without annotations every blib fragment is <see cref="IonType.Unknown"/>, which leaves
    /// the consecutive-ion feature at 0 and gives generated decoys the target's m/z.
    ///
    /// <para>Grammar of the <c>name</c> column: <c>&lt;ion&gt;&lt;ordinal&gt;[-&lt;loss&gt;]</c>, with
    /// ion a/b/c/x/y/z (any case) and loss <c>H2O</c>, <c>NH3</c>, <c>H3PO4</c> or a finite decimal mass
    /// (for example <c>y7</c>, <c>b3</c>, <c>y7-H2O</c>, <c>y5-97.9769</c>). The fragment charge
    /// comes from the <c>charge</c> column; when that is 0 or missing a <c>^2</c>, <c>++</c> or
    /// <c>+2</c> suffix is accepted. A NIST-style <c>/</c> tail and anything after whitespace are
    /// ignored. Other names (<c>p</c>, <c>?</c>, empty) leave the peak Unknown. A decimal loss
    /// snaps to water, ammonia or phosphoric acid within half its last printed digit (at least
    /// 0.005), so <c>y7-18.0</c> is a water loss; an integer loss snaps by nominal mass.</para>
    ///
    /// <para>Every annotation is checked against the m/z recomputed from the sequence and
    /// modifications (<see cref="PeptideFragmentMass"/>); one that misses the peak by more
    /// than max(0.02 Th, 20 ppm) is ignored and counted. Only b and y ions can be checked, so
    /// a, c, x and z ions are ignored as well, and counted apart from unreadable names.</para>
    /// </summary>
    internal static class BlibPeakAnnotations
    {
        public const string TABLE_NAME = @"RefSpectraPeakAnnotations";

        private const double MZ_TOLERANCE_TH = 0.02;
        private const double MZ_TOLERANCE_PPM = 20.0;
        private const double LOSS_SNAP_TOLERANCE = 0.005;

        private static readonly char[] NAME_TERMINATORS = { ' ', '\t', '/' };

        /// <summary>
        /// Types <paramref name="fragments"/> (in blib peak order) from the annotation rows of
        /// one spectrum, in place. A peak with several annotations takes the b or y ion without
        /// a neutral loss first, then the lowest charge, then the first row.
        /// </summary>
        public static void Apply(string sequence, IReadOnlyList<Modification> modifications,
            LibraryFragment[] fragments, List<BlibAnnotationRow> rows, BlibAnnotationStats stats)
        {
            var chosen = new FragmentAnnotation?[fragments.Length];
            Dictionary<int, double> modMasses = null;
            foreach (var row in rows)
            {
                if (row.PeakIndex < 0 || row.PeakIndex >= fragments.Length)
                {
                    stats.NRejectedRange++;
                    continue;
                }
                if (!TryParseName(row.Name, row.Charge, out var annotation))
                {
                    stats.NRejectedName++;
                    continue;
                }
                if (annotation.IonType != IonType.B && annotation.IonType != IonType.Y)
                {
                    stats.NUncheckedIonType++;
                    continue;
                }
                if (annotation.Ordinal >= sequence.Length)
                {
                    stats.NRejectedRange++;
                    continue;
                }
                modMasses = modMasses ?? PeptideFragmentMass.ModMassesByPosition(modifications);
                double? mz = PeptideFragmentMass.CalculateFragmentMz(annotation.IonType, annotation.Ordinal,
                    annotation.Charge, sequence, modMasses,
                    annotation.HasNeutralLoss ? annotation.NeutralLossMass : null);
                if (!mz.HasValue || !MatchesPeak(mz.Value, fragments[row.PeakIndex].Mz))
                {
                    stats.NRejectedMz++;
                    continue;
                }
                var current = chosen[row.PeakIndex];
                if (!current.HasValue || IsPreferred(annotation, current.Value))
                    chosen[row.PeakIndex] = annotation;
            }

            bool any = false;
            for (int i = 0; i < fragments.Length; i++)
            {
                if (chosen[i].HasValue)
                {
                    fragments[i].Annotation = chosen[i].Value;
                    stats.NPeaksAnnotated++;
                    any = true;
                }
                else
                {
                    stats.NPeaksUnannotated++;
                }
            }
            if (any)
                stats.NSpectraAnnotated++;
        }

        /// <summary>
        /// The <c>name</c> column <see cref="BlibWriter"/> writes for a typed peak, in the grammar
        /// <see cref="TryParseName"/> reads: <c>y5</c>, <c>b3-H2O</c>, <c>y7-44.9977</c>. The charge
        /// goes in the <c>charge</c> column, not the name.
        /// </summary>
        public static string FormatName(FragmentAnnotation annotation)
        {
            string name = char.ToLowerInvariant(annotation.IonType.ToString()[0]) +
                          annotation.Ordinal.ToString(CultureInfo.InvariantCulture);
            switch (annotation.NeutralLoss)
            {
                case NeutralLossCode.None:
                    return name;
                case NeutralLossCode.Custom:
                    return name + @"-" + annotation.CustomLossMass.ToString(@"R", CultureInfo.InvariantCulture);
                default:
                    return name + @"-" + annotation.NeutralLoss;
            }
        }

        /// <summary>
        /// Whether an ion m/z recomputed from the sequence names a peak at <paramref name="peakMz"/>:
        /// within max(0.02 Th, 20 ppm). A peak m/z that is not finite matches nothing, since an
        /// infinite one would widen the ppm tolerance to infinity and pass any annotation.
        /// </summary>
        public static bool MatchesPeak(double ionMz, double peakMz)
        {
            return double.IsFinite(peakMz) &&
                   Math.Abs(ionMz - peakMz) <= Math.Max(MZ_TOLERANCE_TH, peakMz * MZ_TOLERANCE_PPM * 1e-6);
        }

        /// <summary>
        /// Parses an annotation name; see the class summary for the grammar. Any of the six
        /// ion types is read; <see cref="Apply"/> types only b and y ions, the ones it can check
        /// against the peak m/z.
        /// </summary>
        public static bool TryParseName(string name, int chargeColumn, out FragmentAnnotation annotation)
        {
            annotation = default(FragmentAnnotation);
            if (string.IsNullOrWhiteSpace(name))
                return false;
            string text = name.Trim();
            int cut = text.IndexOfAny(NAME_TERMINATORS);
            if (cut >= 0)
                text = text.Substring(0, cut);

            int suffixCharge = 0;
            text = StripChargeSuffix(text, ref suffixCharge);
            int charge = chargeColumn > 0 ? chargeColumn : (suffixCharge > 0 ? suffixCharge : 1);
            if (charge > byte.MaxValue || text.Length < 2)
                return false;

            var ionType = IonTypeExtensions.FromChar(text[0]);
            if (ionType == IonType.Unknown)
                return false;

            int pos = 1;
            while (pos < text.Length && char.IsDigit(text[pos]))
                pos++;
            if (pos == 1 || !int.TryParse(text.Substring(1, pos - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int ordinal) ||
                ordinal < 1 || ordinal > byte.MaxValue)
            {
                return false;
            }

            var lossCode = NeutralLossCode.None;
            double customLoss = 0;
            if (pos < text.Length)
            {
                if (text[pos] != '-' || pos + 1 >= text.Length)
                    return false;
                string lossText = text.Substring(pos + 1);
                (lossCode, customLoss) = NeutralLoss.Parse(lossText);
                if (lossCode == NeutralLossCode.None)
                    return false;
                if (lossCode == NeutralLossCode.Custom)
                {
                    // "NaN" and "Infinity" parse as numbers, and a NaN m/z would pass any
                    // comparison written the wrong way round; no fragment loses either.
                    if (!double.IsFinite(customLoss))
                        return false;
                    lossCode = SnapToKnownLoss(customLoss, lossText);
                }
            }

            annotation = new FragmentAnnotation
            {
                IonType = ionType,
                Ordinal = (byte)ordinal,
                Charge = (byte)charge,
                NeutralLoss = lossCode,
                CustomLossMass = lossCode == NeutralLossCode.Custom ? customLoss : 0,
            };
            return true;
        }

        private static string StripChargeSuffix(string text, ref int charge)
        {
            int caret = text.IndexOf('^');
            if (caret > 0)
            {
                // A suffix that does not parse is not a charge: leave the text whole, so the
                // grammar rejects it rather than typing the ion at the wrong charge.
                if (!int.TryParse(text.Substring(caret + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int caretCharge))
                    return text;
                charge = caretCharge;
                return text.Substring(0, caret);
            }
            int plusCount = 0;
            while (plusCount < text.Length && text[text.Length - 1 - plusCount] == '+')
                plusCount++;
            if (plusCount > 0)
            {
                charge = plusCount;
                return text.Substring(0, text.Length - plusCount);
            }
            int plus = text.LastIndexOf('+');
            if (plus > 0 && int.TryParse(text.Substring(plus + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int value))
            {
                charge = value;
                return text.Substring(0, plus);
            }
            return text;
        }

        /// <summary>
        /// A decimal loss within half its last printed digit (at least 0.005) of water, ammonia or
        /// phosphoric acid is that loss, the rule the modification reader applies to one-decimal
        /// text; an integer one equal to its nominal mass is too (NIST-style <c>-18</c>,
        /// <c>-17</c>, <c>-98</c>). Anything else stays a custom loss of the mass as written.
        /// </summary>
        private static NeutralLossCode SnapToKnownLoss(double mass, string lossText)
        {
            int point = lossText.IndexOf('.');
            bool isNominal = point < 0;
            double tolerance = isNominal ? 0 : Math.Max(LOSS_SNAP_TOLERANCE, 0.5 * Math.Pow(10, -(lossText.Length - point - 1)));
            if (IsLoss(mass, NeutralLoss.H2OMass, isNominal, tolerance))
                return NeutralLossCode.H2O;
            if (IsLoss(mass, NeutralLoss.NH3Mass, isNominal, tolerance))
                return NeutralLossCode.NH3;
            if (IsLoss(mass, NeutralLoss.H3PO4Mass, isNominal, tolerance))
                return NeutralLossCode.H3PO4;
            return NeutralLossCode.Custom;
        }

        private static bool IsLoss(double mass, double knownMass, bool isNominal, double tolerance)
        {
            return isNominal
                ? mass == Math.Round(knownMass)
                : Math.Abs(mass - knownMass) <= tolerance;
        }

        /// <summary>
        /// Whether <paramref name="candidate"/> replaces <paramref name="current"/> as a peak's
        /// annotation: an ion without a neutral loss beats one with a loss, then the lower
        /// charge wins, and on a tie the earlier row stays.
        /// </summary>
        internal static bool IsPreferred(FragmentAnnotation candidate, FragmentAnnotation current)
        {
            if (candidate.HasNeutralLoss != current.HasNeutralLoss)
                return !candidate.HasNeutralLoss;
            return candidate.Charge < current.Charge;
        }
    }
}
