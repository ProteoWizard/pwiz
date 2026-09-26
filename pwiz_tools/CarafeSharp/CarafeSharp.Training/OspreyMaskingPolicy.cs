/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/ai/AIGear.java
 *   get_ms2_matches_diann (the per-ion validity rules and the PSM gates) and
 *   src/main/java/dia/DIAIndex.java detect_best_ion (the boundary skew flags)
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
using System.Linq;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;

namespace pwiz.CarafeSharp.Training
{
    /// <summary>Which intensity a kept ion trains on.</summary>
    public enum TrainingIntensitySource
    {
        /// <summary>The apex spectrum's matched peak, as Carafe trains.</summary>
        apex,

        /// <summary>The median polish's interference-resistant estimate <c>exp(row effect)</c>.</summary>
        polish,
    }

    /// <summary>Which correlation the correlation rule reads.</summary>
    public enum MaskingCorrelation
    {
        /// <summary>Correlation with the median polish's elution profile (closest to Carafe on Stellar).</summary>
        polish,

        /// <summary>Correlation with Osprey's reference XIC, its most intense top-6 fragment.</summary>
        reference,
    }

    /// <summary>What happens to an ion outside the run's MS2 scan window.</summary>
    public enum OutOfRangeIons
    {
        /// <summary>Intensity 0 and valid: the model learns the ion is not seen, as Carafe trains.</summary>
        train_as_absent,

        /// <summary>Masked out of the loss, since it could not have been observed.</summary>
        masked,
    }

    /// <summary>
    /// Thresholds for <see cref="OspreyMaskingPolicy"/>. The defaults are the settings of
    /// Carafe's Osprey workflow (<c>-cor 0.8 -n_ion_min 2 -c_ion_min 2 -lf_frag_n_min 2 -nf 4
    /// -min_n 4 -valid -nm</c>).
    /// </summary>
    public sealed class OspreyMaskingSettings
    {
        /// <summary>Ions with a lower ordinal are always masked (Carafe <c>-lf_frag_n_min</c>): b1 and y1 at both charges.</summary>
        public int MinFragmentOrdinal { get; set; } = 2;

        /// <summary>
        /// b ions up to this ordinal (Carafe <c>-n_ion_min</c>) are masked when intense but not
        /// clean; see <see cref="LowOrdinalIntensity"/>. 0 turns the rule off for b ions.
        /// </summary>
        public int LowOrdinalB { get; set; } = 2;

        /// <summary>The same for y ions (Carafe <c>-c_ion_min</c>).</summary>
        public int LowOrdinalY { get; set; } = 2;

        /// <summary>A low-ordinal ion at least this fraction of the top ion needs <see cref="LowOrdinalMinCorrelation"/> and no two-sided skew.</summary>
        public double LowOrdinalIntensity { get; set; } = 0.5;

        /// <summary>The correlation (exclusive) a clean intense low-ordinal ion needs.</summary>
        public double LowOrdinalMinCorrelation { get; set; } = 0.9;

        public OutOfRangeIons OutOfRange { get; set; } = OutOfRangeIons.train_as_absent;

        /// <summary>A matched ion correlating below this with the elution profile is masked (Carafe <c>-cor</c>; inclusive pass).</summary>
        public double MinCorrelation { get; set; } = 0.8;

        public MaskingCorrelation Correlation { get; set; } = MaskingCorrelation.polish;

        /// <summary>Mask a matched ion whose apex peak another confident precursor also matched.</summary>
        public bool MaskSharedApex { get; set; } = true;

        /// <summary>Mask matched ions of one precursor that landed on the same apex peak, as Carafe's shared-peak count does.</summary>
        public bool MaskSelfShared { get; set; } = true;

        /// <summary>Mask an ion a co-eluting confident precursor could also explain (Osprey's co-elution scope).</summary>
        public bool MaskSharedCoelution { get; set; }

        /// <summary>Only mask an ion shared with another precursor when that precursor is the better identification.</summary>
        public bool SharedOnlyWhenBetterClaimant { get; set; }

        /// <summary>Mask a matched ion elevated at both peak boundaries (Carafe's skew rule).</summary>
        public bool MaskBoundarySkew { get; set; } = true;

        /// <summary>A matched ion whose apex residual against the polish exceeds this robust z is masked. Infinity turns it off.</summary>
        public double MaxPolishOutlierZ { get; set; } = double.PositiveInfinity;

        /// <summary>A spectrum needs at least this many matched ions (Carafe <c>-nf</c>).</summary>
        public int MinMatchedIons { get; set; } = 4;

        /// <summary>A spectrum needs at least this many valid ions with intensity (Carafe <c>-min_n</c>).</summary>
        public int MinValidIons { get; set; } = 4;

        /// <summary>A spectrum's top ion must be valid (Carafe <c>-valid</c>).</summary>
        public bool RequireTopIonValid { get; set; } = true;

        public TrainingIntensitySource IntensitySource { get; set; } = TrainingIntensitySource.apex;
    }

    /// <summary>One precursor's training intensities and mask, with what decided them.</summary>
    public sealed class MaskedSpectrum
    {
        public double[] Intensities { get; set; }
        public double[] Invalid { get; set; }
        public int SlotCount { get; set; }
        public int MatchedCount { get; set; }

        /// <summary>Slots with a non-zero training intensity that are valid.</summary>
        public int ValidMatchedCount { get; set; }

        public int MaskedUnmatchedCount { get; set; }

        /// <summary>The top ion's slot, or -1 when no ion above the ordinal floor matched.</summary>
        public int TopSlot { get; set; } = -1;

        /// <summary>Slots each rule masked (a slot counts under every rule that masked it).</summary>
        public Dictionary<string, long> MaskedBy { get; } = new Dictionary<string, long>(StringComparer.Ordinal);

        /// <summary>Why the spectrum is not a training row, or null when it is.</summary>
        public string RejectReason { get; set; }
    }

    /// <summary>
    /// Decides which ladder ions of an Osprey-identified precursor the MS2 model trains on, with
    /// Carafe's rules read from Osprey's evidence instead of Carafe's own XICs.
    /// <list type="bullet">
    /// <item>An unmatched ion trains as intensity 0 (the model learns it is absent); only the
    /// ordinal floor masks unmatched ions. An ion outside the scan window is 0 and valid, or
    /// masked with <see cref="OutOfRangeIons.masked"/>. A charge 2 ion of a 1+ precursor is 0
    /// and valid, as Carafe trains it; an ion without an m/z (a non-standard residue) is
    /// masked.</item>
    /// <item>A matched ion is masked when its apex peak is shared (another confident precursor,
    /// or another ion of this one), when it correlates poorly with the elution profile, when it
    /// is elevated at both peak boundaries, and, for an intense low-ordinal ion, when it is not
    /// clean.</item>
    /// <item>The spectrum is kept with enough matched and valid ions and a valid top ion (the
    /// most intense matched ion above the ordinal floor), and intensities are divided by the
    /// top ion's.</item>
    /// </list>
    /// </summary>
    public sealed class OspreyMaskingPolicy
    {
        public const string RULE_NOT_APPLICABLE = @"not_applicable";
        public const string RULE_ORDINAL = @"ordinal";
        public const string RULE_OUT_OF_RANGE = @"out_of_range";
        public const string RULE_SHARED_APEX = @"shared_apex";
        public const string RULE_SELF_SHARED = @"self_shared";
        public const string RULE_SHARED_COELUTION = @"shared_coelution";
        public const string RULE_CORRELATION = @"correlation";
        public const string RULE_SKEW = @"skew";
        public const string RULE_LOW_ORDINAL = @"low_ordinal";
        public const string RULE_POLISH_OUTLIER = @"polish_outlier";

        public const string REJECT_FEW_MATCHED = @"few_matched";
        public const string REJECT_FEW_VALID = @"few_valid";
        public const string REJECT_TOP_ION_INVALID = @"top_ion_invalid";

        /// <summary>Two matched ions whose observed m/z agree this closely matched the same peak.</summary>
        public const double SAME_PEAK_TOLERANCE = 1e-4;

        private readonly OspreyMaskingSettings _settings;

        public OspreyMaskingPolicy(OspreyMaskingSettings settings)
        {
            _settings = settings;
        }

        public MaskedSpectrum Apply(OspreyTrainingRecord record)
        {
            int slots = record.SlotCount;
            int length = record.Sequence.Length;
            var result = new MaskedSpectrum
            {
                SlotCount = slots,
                Intensities = new double[slots],
                Invalid = new double[slots],
            };
            var ordinals = new int[slots];
            var matched = new bool[slots];
            var inRange = new bool[slots];
            for (int slot = 0; slot < slots; slot++)
            {
                int position = slot / AlphabaseFragmentMz.COLUMN_COUNT;
                ordinals[slot] = IsB(slot) ? position + 1 : length - 1 - position;
                matched[slot] = record.Has(slot, OspreyIonFlags.MATCHED_AT_APEX) && record.ApexIntensity[slot] > 0;
                inRange[slot] = record.Has(slot, OspreyIonFlags.IN_SCAN_RANGE);
                if (matched[slot])
                {
                    result.MatchedCount++;
                    if (ordinals[slot] >= _settings.MinFragmentOrdinal &&
                        (result.TopSlot < 0 || record.ApexIntensity[slot] >= record.ApexIntensity[result.TopSlot]))
                    {
                        result.TopSlot = slot;
                    }
                }
            }
            // Carafe's matched-ion rules only apply to ions it keeps an intensity for.
            var scored = Enumerable.Range(0, slots).Select(s => matched[s] && inRange[s]).ToArray();
            var skew = BoundarySkew(record, scored);
            var selfShared = _settings.MaskSelfShared ? SelfShared(record, matched) : new bool[slots];
            double topIntensity = result.TopSlot >= 0 ? record.ApexIntensity[result.TopSlot] : 0;

            for (int slot = 0; slot < slots; slot++)
            {
                int invalid = 0;
                // A charge 2 ion of a 1+ precursor is a valid zero, as Carafe trains it; only an
                // ion without an m/z (a non-standard residue) is not applicable.
                if (!record.Has(slot, OspreyIonFlags.APPLICABLE))
                {
                    if (!IsNotApplicableByChargeOnly(record, slot))
                        invalid += Mask(result, RULE_NOT_APPLICABLE);
                }
                else if (!inRange[slot] && _settings.OutOfRange == OutOfRangeIons.masked)
                {
                    invalid += Mask(result, RULE_OUT_OF_RANGE);
                }
                if (scored[slot])
                    invalid += MatchedRules(record, slot, ordinals[slot], skew[slot], selfShared[slot], topIntensity, result);
                if (ordinals[slot] < _settings.MinFragmentOrdinal)
                    invalid += Mask(result, RULE_ORDINAL);
                result.Invalid[slot] = invalid;
                if (!scored[slot] && invalid > 0)
                    result.MaskedUnmatchedCount++;
            }

            if (result.TopSlot >= 0)
            {
                double top = TrainingIntensity(record, result.TopSlot);
                for (int slot = 0; slot < slots; slot++)
                {
                    if (scored[slot])
                        result.Intensities[slot] = TrainingIntensity(record, slot) / top;
                }
            }
            for (int slot = 0; slot < slots; slot++)
            {
                if (result.Intensities[slot] > 0 && result.Invalid[slot] == 0)
                    result.ValidMatchedCount++;
            }

            if (result.TopSlot < 0 || result.MatchedCount < _settings.MinMatchedIons)
                result.RejectReason = REJECT_FEW_MATCHED;
            else if (result.ValidMatchedCount < _settings.MinValidIons)
                result.RejectReason = REJECT_FEW_VALID;
            else if (_settings.RequireTopIonValid && result.Invalid[result.TopSlot] > 0)
                result.RejectReason = REJECT_TOP_ION_INVALID;
            return result;
        }

        private int MatchedRules(OspreyTrainingRecord record, int slot, int ordinal, int skew, bool selfShared,
            double topIntensity, MaskedSpectrum result)
        {
            int invalid = 0;
            if (_settings.MaskSharedApex && record.SharedApexCount[slot] > 0 &&
                (!_settings.SharedOnlyWhenBetterClaimant || record.Has(slot, OspreyIonFlags.BETTER_CLAIMANT_APEX)))
            {
                invalid += Mask(result, RULE_SHARED_APEX);
            }
            if (selfShared)
                invalid += Mask(result, RULE_SELF_SHARED);
            if (_settings.MaskSharedCoelution && record.SharedCoeluteCount[slot] > 0 &&
                (!_settings.SharedOnlyWhenBetterClaimant || record.Has(slot, OspreyIonFlags.BETTER_CLAIMANT_COELUTE)))
            {
                invalid += Mask(result, RULE_SHARED_COELUTION);
            }
            // An ion without a correlation (under 3 peak scans) cannot pass.
            float correlation = _settings.Correlation == MaskingCorrelation.reference
                ? record.CorrReference[slot]
                : record.CorrPolish[slot];
            if (!(correlation >= _settings.MinCorrelation))
                invalid += Mask(result, RULE_CORRELATION);
            if (_settings.MaskBoundarySkew && skew >= 2)
                invalid += Mask(result, RULE_SKEW);
            bool lowOrdinal = IsB(slot) ? ordinal <= _settings.LowOrdinalB : ordinal <= _settings.LowOrdinalY;
            if (lowOrdinal && topIntensity > 0 && record.ApexIntensity[slot] / topIntensity >= _settings.LowOrdinalIntensity &&
                !(correlation > _settings.LowOrdinalMinCorrelation && skew <= 1))
            {
                invalid += Mask(result, RULE_LOW_ORDINAL);
            }
            if (record.PolishOutlierZ[slot] > _settings.MaxPolishOutlierZ)
                invalid += Mask(result, RULE_POLISH_OUTLIER);
            return invalid;
        }

        /// <summary>
        /// Carafe's boundary skew flags (DIAIndex.detect_best_ion) on Osprey's peak: over the
        /// scored ions, with M the highest apex intensity and Lmed, Rmed 1.5 times the median
        /// intensity at the first and last peak scan, an ion is skewed on a side when it is above
        /// that side's Lmed / Rmed and above f times its apex (f = 0.10 for an ion at least half of
        /// M, else 0.25). 2 = both sides, 1 = one side.
        /// </summary>
        private static int[] BoundarySkew(OspreyTrainingRecord record, bool[] scored)
        {
            var skew = new int[scored.Length];
            var ions = Enumerable.Range(0, scored.Length).Where(s => scored[s]).ToArray();
            if (ions.Length == 0)
                return skew;
            double max = ions.Max(s => (double)record.ApexIntensity[s]);
            double leftLimit = 1.5 * Median(ions.Select(s => (double)record.XicStart[s]));
            double rightLimit = 1.5 * Median(ions.Select(s => (double)record.XicEnd[s]));
            foreach (int slot in ions)
            {
                double apex = record.ApexIntensity[slot];
                double fraction = apex >= 0.5 * max ? 0.10 : 0.25;
                bool left = record.XicStart[slot] > leftLimit && record.XicStart[slot] > fraction * apex;
                bool right = record.XicEnd[slot] > rightLimit && record.XicEnd[slot] > fraction * apex;
                skew[slot] = left && right ? 2 : left || right ? 1 : 0;
            }
            return skew;
        }

        /// <summary>
        /// Matched ions of this precursor that matched the same apex peak as another of its ions,
        /// found by the observed m/z (theoretical m/z plus the calibrated error).
        /// </summary>
        private static bool[] SelfShared(OspreyTrainingRecord record, bool[] matched)
        {
            var shared = new bool[matched.Length];
            var observed = Enumerable.Range(0, matched.Length)
                .Where(s => matched[s])
                .Select(s => (Slot: s, Mz: record.IonMz[s] + record.ApexMzError[s]))
                .OrderBy(p => p.Mz)
                .ToArray();
            for (int i = 1; i < observed.Length; i++)
            {
                if (observed[i].Mz - observed[i - 1].Mz <= SAME_PEAK_TOLERANCE)
                {
                    shared[observed[i].Slot] = true;
                    shared[observed[i - 1].Slot] = true;
                }
            }
            return shared;
        }

        /// <summary>
        /// The ion's intensity on one scale for the whole spectrum: the polish's row effect when
        /// asked for and the precursor has a fit (a matched ion always has a finite row effect),
        /// the apex intensity otherwise.
        /// </summary>
        private double TrainingIntensity(OspreyTrainingRecord record, int slot)
        {
            if (_settings.IntensitySource == TrainingIntensitySource.polish && record.MedianPolishFitted)
            {
                float rowEffect = record.PolishRowEffect[slot];
                return float.IsNaN(rowEffect) ? 0 : Math.Exp(rowEffect);
            }
            return record.ApexIntensity[slot];
        }

        private static bool IsB(int slot)
        {
            return slot % AlphabaseFragmentMz.COLUMN_COUNT < AlphabaseFragmentMz.Y_Z1;
        }

        /// <summary>
        /// True for a slot that is not applicable only because its fragment charge exceeds the
        /// precursor's: a charge 2 ion of a 1+ precursor whose charge 1 ion is applicable.
        /// </summary>
        private static bool IsNotApplicableByChargeOnly(OspreyTrainingRecord record, int slot)
        {
            int column = slot % AlphabaseFragmentMz.COLUMN_COUNT;
            bool charge2 = column == AlphabaseFragmentMz.B_Z2 || column == AlphabaseFragmentMz.Y_Z2;
            return charge2 && record.Charge < 2 && record.Has(slot - 1, OspreyIonFlags.APPLICABLE);
        }

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
        }

        private static int Mask(MaskedSpectrum result, string rule)
        {
            result.MaskedBy[rule] = (result.MaskedBy.TryGetValue(rule, out long n) ? n : 0) + 1;
            return 1;
        }
    }
}
