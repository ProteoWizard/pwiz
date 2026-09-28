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
        public int NSpectraAnnotated;
        public int NPeaksAnnotated;
        public int NPeaksUnannotated;
        public int NRejectedName;

        /// <summary>Annotations naming a peak the spectrum lacks, or an ion as long as the peptide.</summary>
        public int NRejectedRange;

        public int NRejectedMz;

        public string Summary()
        {
            return string.Format(
                @"Library fragment annotations: {0} spectra, {1} peaks typed, {2} peaks without an annotation, " +
                @"{3} annotations with an unreadable name, {4} annotations naming a peak or an ion the spectrum does not have, " +
                @"{5} annotations whose m/z disagrees with the peak",
                NSpectraAnnotated, NPeaksAnnotated, NPeaksUnannotated, NRejectedName, NRejectedRange, NRejectedMz);
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
    /// ignored. Other names (<c>p</c>, <c>?</c>, empty) leave the peak Unknown.</para>
    ///
    /// <para>Every annotation is checked against the m/z recomputed from the sequence and
    /// modifications (<see cref="PeptideFragmentMass"/>); one that misses the peak by more
    /// than max(0.02 Th, 20 ppm) is ignored and counted. Only b and y ions can be checked, so
    /// other ion types are ignored as well.</para>
    /// </summary>
    internal static class BlibPeakAnnotations
    {
        public const string TABLE_NAME = @"RefSpectraPeakAnnotations";

        private const double MZ_TOLERANCE_TH = 0.02;
        private const double MZ_TOLERANCE_PPM = 20.0;
        private const double LOSS_SNAP_TOLERANCE = 0.005;

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
                if (annotation.Ordinal >= sequence.Length)
                {
                    stats.NRejectedRange++;
                    continue;
                }
                modMasses = modMasses ?? PeptideFragmentMass.ModMassesByPosition(modifications);
                double? mz = PeptideFragmentMass.CalculateFragmentMz(annotation.IonType, annotation.Ordinal,
                    annotation.Charge, sequence, modMasses,
                    annotation.HasNeutralLoss ? annotation.NeutralLossMass : null);
                double peakMz = fragments[row.PeakIndex].Mz;
                // Written so that a NaN on either side fails the check rather than passing it.
                if (!mz.HasValue || !(Math.Abs(mz.Value - peakMz) <= Math.Max(MZ_TOLERANCE_TH, peakMz * MZ_TOLERANCE_PPM * 1e-6)))
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
        /// Parses an annotation name; see the class summary for the grammar. Only b and y ions
        /// are accepted, because only they can be checked against the peak m/z.
        /// </summary>
        public static bool TryParseName(string name, int chargeColumn, out FragmentAnnotation annotation)
        {
            annotation = default(FragmentAnnotation);
            if (string.IsNullOrWhiteSpace(name))
                return false;
            string text = name.Trim();
            int cut = text.IndexOfAny(new[] { ' ', '\t', '/' });
            if (cut >= 0)
                text = text.Substring(0, cut);

            int suffixCharge = 0;
            text = StripChargeSuffix(text, ref suffixCharge);
            int charge = chargeColumn > 0 ? chargeColumn : (suffixCharge > 0 ? suffixCharge : 1);
            if (charge > byte.MaxValue || text.Length < 2)
                return false;

            var ionType = IonTypeExtensions.FromChar(text[0]);
            if (ionType != IonType.B && ionType != IonType.Y)
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
                // "NaN" and "Infinity" parse as numbers, and a NaN m/z would pass any
                // comparison written the wrong way round; no fragment loses either.
                if (lossCode == NeutralLossCode.Custom && !double.IsFinite(customLoss))
                    return false;
                if (lossCode == NeutralLossCode.Custom)
                    lossCode = SnapToKnownLoss(customLoss, lossText.IndexOf('.') < 0, ref customLoss);
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
        /// A decimal loss within 0.005 Th of water, ammonia or phosphoric acid is that loss; so is
        /// an integer one equal to its nominal mass (NIST-style <c>-18</c>, <c>-17</c>, <c>-98</c>).
        /// </summary>
        private static NeutralLossCode SnapToKnownLoss(double mass, bool isNominal, ref double customLoss)
        {
            if (IsLoss(mass, NeutralLoss.H2OMass, isNominal))
                return NeutralLossCode.H2O;
            if (IsLoss(mass, NeutralLoss.NH3Mass, isNominal))
                return NeutralLossCode.NH3;
            if (IsLoss(mass, NeutralLoss.H3PO4Mass, isNominal))
                return NeutralLossCode.H3PO4;
            customLoss = mass;
            return NeutralLossCode.Custom;
        }

        private static bool IsLoss(double mass, double knownMass, bool isNominal)
        {
            return isNominal
                ? mass == Math.Round(knownMass)
                : Math.Abs(mass - knownMass) <= LOSS_SNAP_TOLERANCE;
        }

        private static bool IsPreferred(FragmentAnnotation candidate, FragmentAnnotation current)
        {
            if (candidate.HasNeutralLoss != current.HasNeutralLoss)
                return !candidate.HasNeutralLoss;
            return candidate.Charge < current.Charge;
        }
    }
}
