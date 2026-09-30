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

    /// <summary>
    /// Reads a blib's <c>RefSpectraPeakAnnotations</c> table for <see cref="FragmentTypeCheck"/>:
    /// what the library states about each peak, compared with the type Osprey computes from m/z
    /// (<see cref="FragmentTyping"/>). The rows never type a peak - Osprey computes fragment types
    /// for itself, as Skyline does for a peptide library, where the table exists for small
    /// molecules, which have no fragmentation model.
    ///
    /// <para>Grammar of the <c>name</c> column: <c>&lt;ion&gt;&lt;ordinal&gt;[-&lt;loss&gt;]</c>, with
    /// ion a/b/c/x/y/z (any case) and loss <c>H2O</c>, <c>NH3</c>, <c>H3PO4</c> or a finite decimal mass
    /// (for example <c>y7</c>, <c>b3</c>, <c>y7-H2O</c>, <c>y5-97.9769</c>). The fragment charge
    /// comes from the <c>charge</c> column; when that is 0 or missing a <c>^2</c>, <c>++</c> or
    /// <c>+2</c> suffix is accepted. A NIST-style <c>/</c> tail and anything after whitespace are
    /// ignored. Other names (<c>p</c>, <c>?</c>, empty) leave the peak Unknown. A decimal loss
    /// snaps to water, ammonia or phosphoric acid within half its last printed digit (at least
    /// 0.005), so <c>y7-18.0</c> is a water loss; an integer loss snaps by nominal mass.</para>
    /// </summary>
    internal static class BlibPeakAnnotations
    {
        public const string TABLE_NAME = @"RefSpectraPeakAnnotations";

        private const double LOSS_SNAP_TOLERANCE = 0.005;

        private static readonly char[] NAME_TERMINATORS = { ' ', '\t', '/' };

        /// <summary>
        /// Adds one spectrum's annotation rows to <paramref name="check"/>, peak by peak, against
        /// what <see cref="FragmentTyping"/> computes for <paramref name="fragments"/> (in blib
        /// peak order) within <paramref name="tolerance"/>. A row naming a peak the spectrum lacks
        /// names no peak and is passed over.
        /// </summary>
        public static void Check(string sequence, IEnumerable<Modification> modifications, int precursorCharge,
            LibraryFragment[] fragments, FragmentToleranceConfig tolerance, List<BlibAnnotationRow> rows,
            FragmentTypeCheck check)
        {
            var computed = FragmentTyping.Compute(sequence, modifications, precursorCharge, fragments, tolerance);
            var stated = new Dictionary<int, List<FragmentAnnotation>>();
            var unreadable = new HashSet<int>();
            foreach (var row in rows)
            {
                if (row.PeakIndex < 0 || row.PeakIndex >= fragments.Length)
                    continue;
                if (!stated.TryGetValue(row.PeakIndex, out var list))
                {
                    list = new List<FragmentAnnotation>();
                    stated.Add(row.PeakIndex, list);
                }
                if (TryParseName(row.Name, row.Charge, out var annotation))
                    list.Add(annotation);
                else
                    unreadable.Add(row.PeakIndex);
            }
            foreach (var pair in stated)
            {
                check.AddPeak(pair.Value, unreadable.Contains(pair.Key), computed[pair.Key], sequence, precursorCharge,
                    fragments[pair.Key].Mz);
            }
        }

        /// <summary>
        /// Parses an annotation name; see the class summary for the grammar. Any of the six ion
        /// types is read.
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
                var loss = NeutralLoss.Parse(lossText);
                if (!loss.HasValue || loss.Value.Code == NeutralLossCode.None)
                    return false;
                (lossCode, customLoss) = loss.Value;
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
    }
}
