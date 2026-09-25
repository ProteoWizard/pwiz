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
using System.Linq;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// The evidence behind one identified precursor's fragment intensities, for the training
    /// export (docs/22-training-export.md): its whole b/y ladder read from the apex spectrum
    /// and from the XICs over its FINAL boundaries, Osprey's own median-polish fit with every
    /// ladder ion projected onto it, and the other identified precursors that claim the same
    /// peaks. Osprey scores nothing from this; a consumer decides from it which intensities to
    /// trust.
    ///
    /// <para>The fit is Osprey's, not a new one: the core matrix is
    /// <see cref="TopFragmentExtractor.ExtractFragmentXics"/> over the peak's scans and the fit
    /// is <see cref="TukeyMedianPolish.Compute"/> with the scorer's own arguments
    /// (<see cref="TukeyMedianPolish.SCORING_MAX_ITERATIONS"/>,
    /// <see cref="TukeyMedianPolish.SCORING_TOLERANCE"/>), exactly as <c>CoelutionScorer</c>
    /// computes feature <c>median_polish_cosine</c>. The resulting cosine therefore reproduces
    /// the scored value bit for bit, which is the export's built-in consistency check.</para>
    /// </summary>
    public static class TrainingEvidence
    {
        /// <summary>The robust-sigma factor applied to a median absolute residual.</summary>
        public const double MAD_TO_SIGMA = 1.4826;

        /// <summary>Index of <c>median_polish_cosine</c> in the 21-feature PIN vector.</summary>
        private const int MP_COSINE_FEATURE = 15;

        /// <summary>Index of <c>explained_intensity</c> in the 21-feature PIN vector.</summary>
        private const int EXPLAINED_INTENSITY_FEATURE = 8;

        /// <summary>
        /// Compute the evidence for <paramref name="entry"/> identified at <paramref name="row"/>
        /// (its final apex scan and boundaries) in <paramref name="window"/>. Identity, peak and
        /// evidence fields are filled; the q-values, PEP, protein q, file name and entrapment
        /// class are the caller's, which has the sidecars. <paramref name="claimants"/> are the
        /// other identified precursors assigned to the same isolation window (the precursor
        /// itself may be among them and is skipped).
        /// </summary>
        public static TrainingRecord Compute(LibraryEntry entry, FdrEntry row, double runPrecursorQ,
            double score, TrainingEvidenceWindow window, IReadOnlyList<TrainingClaimant> claimants,
            TrainingEvidenceSettings settings)
        {
            var tolerance = settings.SearchConfig.FragmentTolerance;
            string sequence = entry.Sequence ?? string.Empty;
            int length = sequence.Length;
            double[] ladder = FragmentLadder.Build(sequence, entry.Modifications, entry.Charge);
            int nSlots = ladder.Length;

            var record = NewRecord(entry, row, nSlots, ladder);
            record.RunPrecursorQ = runPrecursorQ;
            record.Score = score;

            // The peak: every window scan from the start to the end boundary. The boundaries
            // are window-spectrum retention times, so the range is exact; a boundary pair that
            // does not bracket the apex (never written by Osprey) is widened to include it.
            if (!window.TryGetScanIndex(row.ScanNumber, out int apexIdx))
                throw new ArgumentException(string.Format(@"Scan {0} is not in the isolation window.", row.ScanNumber), nameof(row));
            int startIdx = Math.Min(LowerBound(window.Rts, row.StartRt), apexIdx);
            int endIdx = Math.Max(LowerBoundAbove(window.Rts, row.EndRt) - 1, apexIdx);
            int nPeak = endIdx - startIdx + 1;
            int apexPos = apexIdx - startIdx;
            var peakRts = new double[nPeak];
            Array.Copy(window.Rts, startIdx, peakRts, 0, nPeak);
            record.NPeakScans = nPeak;
            var apexSpectrum = window.Spectra[apexIdx];
            record.IsolationLower = apexSpectrum.IsolationWindow.LowerBound;
            record.IsolationUpper = apexSpectrum.IsolationWindow.UpperBound;
            record.ApexTic = Sum(apexSpectrum.Intensities);

            // Osprey's median polish, recomputed from the core fragments over the final peak.
            var polish = FitCorePolish(entry, window, startIdx, endIdx, peakRts, settings.SearchConfig,
                out int nCore);
            record.MpNCore = nCore;
            record.MpFitted = polish != null;
            record.MpCosine = polish != null ? TukeyMedianPolish.LibCosine(polish, entry.Fragments) : 0.0;
            record.MpCosineParity = row.Features != null && row.Features.Length > MP_COSINE_FEATURE &&
                                    record.MpCosine.Equals(row.Features[MP_COSINE_FEATURE]);
            double residualMad = double.NaN;
            if (polish != null)
            {
                record.MpConverged = polish.Converged;
                record.MpIterations = polish.NIterations;
                record.MpOverall = polish.Overall;
                record.MpNFragmentsUsed = polish.NFragmentsUsed;
                residualMad = MedianAbsoluteResidual(polish);
            }
            else
            {
                record.MpOverall = double.NaN;
            }
            record.MpResidualMad = residualMad;

            MapLibrary(entry, ladder, length, tolerance, record);

            // Every applicable ion: its apex match, its XIC over the peak, and its projection
            // onto the fit.
            var apexPeakIndex = new int[nSlots];
            var tolDa = new double[nSlots];
            var rowEffects = new double[nSlots];
            var boundaryStart = new List<double>();
            var boundaryEnd = new List<double>();
            float[] xicMatrix = settings.WriteXics ? new float[nSlots * nPeak] : null;
            if (xicMatrix != null)
            {
                for (int i = 0; i < xicMatrix.Length; i++)
                    xicMatrix[i] = float.NaN;
            }
            var reference = AlignReference(row, peakRts);
            for (int k = 0; k < nSlots; k++)
            {
                apexPeakIndex[k] = -1;
                rowEffects[k] = double.NaN;
                double mz = ladder[k];
                if (double.IsNaN(mz))
                    continue;
                record.IonFlags[k] |= TrainingIonFlags.APPLICABLE;
                record.NIonsApplicable++;
                if (settings.Ms2ScanWindow == null ||
                    (mz >= settings.Ms2ScanWindow[0] && mz <= settings.Ms2ScanWindow[1]))
                {
                    record.IonFlags[k] |= TrainingIonFlags.IN_SCAN_RANGE;
                }

                tolDa[k] = tolerance.ToleranceDa(mz);
                var xic = new double[nPeak];
                for (int s = 0; s < nPeak; s++)
                {
                    var spectrum = window.Spectra[startIdx + s];
                    int peak = TopFragmentExtractor.FindClosestPeakInWindow(spectrum.Mzs, mz, mz - tolDa[k], mz + tolDa[k]);
                    if (peak < 0)
                        continue;
                    xic[s] = spectrum.Intensities[peak];
                    if (s == apexPos)
                    {
                        apexPeakIndex[k] = peak;
                        record.ApexMzError[k] = (float)(spectrum.Mzs[peak] - mz);
                    }
                }

                record.ApexIntensity[k] = (float)xic[apexPos];
                if (apexPeakIndex[k] >= 0)
                {
                    record.IonFlags[k] |= TrainingIonFlags.MATCHED_AT_APEX;
                    record.NIonsObserved++;
                }
                int nFinite = xic.Count(v => v > 0);
                record.NFiniteScans[k] = (ushort)Math.Min(nFinite, ushort.MaxValue);
                double xicMax = xic.Max();
                record.XicStart[k] = (float)xic[0];
                record.XicEnd[k] = (float)xic[nPeak - 1];
                record.XicMax[k] = (float)xicMax;
                if ((record.IonFlags[k] & TrainingIonFlags.CORE) != 0 && xicMax > 0)
                {
                    boundaryStart.Add(xic[0] / xicMax);
                    boundaryEnd.Add(xic[nPeak - 1] / xicMax);
                }
                if (reference != null)
                    record.CorrReference[k] = (float)ScoringMath.PearsonOverRange(xic, reference, 0, nPeak - 1);
                if (xicMatrix != null)
                {
                    for (int s = 0; s < nPeak; s++)
                        xicMatrix[k * nPeak + s] = (float)xic[s];
                }

                if (polish != null)
                {
                    record.CorrPolish[k] = (float)ScoringMath.PearsonOverRange(xic, polish.ElutionProfileIntensities, 0, nPeak - 1);
                    rowEffects[k] = Project(xic, polish, apexPos, residualMad, k, record);
                }
            }
            record.BoundaryStartRatioMedian = Median(boundaryStart);
            record.BoundaryEndRatioMedian = Median(boundaryEnd);
            SetRelativeIntensities(rowEffects, record);
            if (xicMatrix != null)
            {
                record.XicRts = peakRts;
                record.XicIntensities = xicMatrix;
            }

            AddClaimantEvidence(record, row, runPrecursorQ, score, ladder, apexPeakIndex, tolDa, claimants);
            return record;
        }

        /// <summary>
        /// How many other rows the double-counting dedup would call a collision with
        /// <paramref name="entry"/>: same isolation window (the caller's list), apex within
        /// <paramref name="rtNeighborhood"/>, and top-6 library fragments overlapping per
        /// <see cref="ScoringPipeline.SharesDoubleCountingFragments"/>, each pair asked in the
        /// dedup's order (<see cref="ScoringPipeline.CompareDoubleCountingOrder"/>). Entry ids
        /// are the rows' entry_ids, which a target's library entry shares.
        /// <paramref name="neighbors"/> must be sorted by apex RT.
        /// </summary>
        public static int CountDoubleCountingNeighbors(LibraryEntry entry, double apexRt,
            IReadOnlyList<(LibraryEntry Entry, double ApexRt)> neighbors, double rtNeighborhood,
            double tolerance, ToleranceUnit unit)
        {
            int count = 0;
            int lo = 0, hi = neighbors.Count;
            double from = apexRt - rtNeighborhood;
            while (lo < hi)
            {
                int m = (lo + hi) >> 1;
                if (neighbors[m].ApexRt < from)
                    lo = m + 1;
                else
                    hi = m;
            }
            for (int i = lo; i < neighbors.Count && neighbors[i].ApexRt <= apexRt + rtNeighborhood; i++)
            {
                var other = neighbors[i].Entry;
                if (other.Id == entry.Id)
                    continue;
                // The pair as the dedup tests it: the entry first in its order goes first.
                bool otherFirst = ScoringPipeline.CompareDoubleCountingOrder(neighbors[i].ApexRt, other.Id, apexRt, entry.Id) < 0;
                var first = otherFirst ? other : entry;
                var second = otherFirst ? entry : other;
                if (ScoringPipeline.SharesDoubleCountingFragments(first.Fragments, second.Fragments, tolerance, unit))
                    count++;
            }
            return count;
        }

        private static TrainingRecord NewRecord(LibraryEntry entry, FdrEntry row, int nSlots, double[] ladder)
        {
            var mods = entry.Modifications ?? Array.Empty<Modification>();
            var record = new TrainingRecord
            {
                EntryId = row.EntryId,
                IsDecoy = row.IsDecoy,
                Sequence = entry.Sequence ?? string.Empty,
                ModifiedSequence = entry.ModifiedSequence ?? string.Empty,
                ModPositions = mods.Select(m => m.Position).ToArray(),
                ModMasses = mods.Select(m => m.MassDelta).ToArray(),
                ModUnimodIds = mods.Select(m => m.UnimodId ?? -1).ToArray(),
                Charge = entry.Charge,
                PrecursorMz = entry.PrecursorMz,
                LibraryRt = entry.RetentionTime,
                // The scores parquet's form: empty for an empty list, NULL only for none.
                ProteinIds = entry.ProteinIds != null
                    ? string.Join(@";", entry.ProteinIds)
                    : null,
                ScanNumber = row.ScanNumber,
                ApexRt = row.ApexRt,
                StartRt = row.StartRt,
                EndRt = row.EndRt,
                BoundsArea = row.BoundsArea,
                CoelutionSum = row.CoelutionSum,
                ExplainedIntensity = row.Features != null && row.Features.Length > EXPLAINED_INTENSITY_FEATURE
                    ? row.Features[EXPLAINED_INTENSITY_FEATURE]
                    : double.NaN,
                NSlots = nSlots,
                IonMz = ladder,
                IonFlags = new byte[nSlots],
                ApexIntensity = NaNs(nSlots),
                ApexMzError = NaNs(nSlots),
                LibraryRelIntensity = NaNs(nSlots),
                NFiniteScans = new ushort[nSlots],
                XicStart = NaNs(nSlots),
                XicEnd = NaNs(nSlots),
                XicMax = NaNs(nSlots),
                CorrPolish = NaNs(nSlots),
                CorrReference = NaNs(nSlots),
                PolishRowEffect = NaNs(nSlots),
                PolishR2 = NaNs(nSlots),
                PolishPosResidMax = NaNs(nSlots),
                PolishApexResidual = NaNs(nSlots),
                PolishOutlierZ = NaNs(nSlots),
                PolishApexRatio = NaNs(nSlots),
                PolishRelIntensity = NaNs(nSlots),
                SharedApexN = new byte[nSlots],
                SharedCoeluteN = new byte[nSlots],
                MinClaimantQ = NaNs(nSlots),
                ExperimentPrecursorQ = double.NaN,
                ExperimentPeptideQ = double.NaN,
                ExperimentProteinQ = double.NaN,
                Pep = double.NaN,
                RunPeptideQ = double.NaN,
            };
            return record;
        }

        /// <summary>
        /// The core fit: the library's top-6 fragments' XICs over [start, end], then Osprey's
        /// median polish on them - the same inputs and arguments as <c>CoelutionScorer</c>, so a
        /// peak of fewer than three scans has no fit, as it has no scored one.
        /// </summary>
        private static TukeyMedianPolishResult FitCorePolish(LibraryEntry entry, TrainingEvidenceWindow window,
            int startIdx, int endIdx, double[] peakRts, OspreyConfig searchConfig, out int nCore)
        {
            var xics = TopFragmentExtractor.ExtractFragmentXics(entry, window.Spectra, window.Rts,
                startIdx, endIdx, searchConfig);
            nCore = xics.Count;
            if (peakRts.Length < 3)
                return null;
            var peakXics = new List<KeyValuePair<int, double[]>>(xics.Count);
            foreach (var xic in xics)
                peakXics.Add(new KeyValuePair<int, double[]>(xic.FragmentIndex, xic.Intensities));
            return TukeyMedianPolish.Compute(peakXics, peakRts,
                TukeyMedianPolish.SCORING_MAX_ITERATIONS, TukeyMedianPolish.SCORING_TOLERANCE);
        }

        /// <summary>
        /// Map each library fragment onto its ladder slot - by annotation when it has one, else
        /// the nearest applicable slot within the tolerance - recording the library's relative
        /// intensity there and marking the core fragments.
        /// </summary>
        private static void MapLibrary(LibraryEntry entry, double[] ladder, int length,
            FragmentToleranceConfig tolerance, TrainingRecord record)
        {
            var fragments = entry.Fragments;
            if (fragments == null || fragments.Count == 0)
                return;
            var slotOfFragment = new int[fragments.Count];
            for (int i = 0; i < fragments.Count; i++)
            {
                var fragment = fragments[i];
                var annotation = fragment.Annotation;
                int slot;
                byte flag;
                if (annotation.IonType == IonType.Unknown)
                {
                    slot = NearestSlot(ladder, fragment.Mz, tolerance.ToleranceDa(fragment.Mz));
                    flag = TrainingIonFlags.LIBRARY_MZ_MATCHED;
                }
                else
                {
                    slot = FragmentLadder.SlotOf(annotation, length);
                    if (slot >= 0 && double.IsNaN(ladder[slot]))
                        slot = -1;
                    flag = TrainingIonFlags.LIBRARY_ANNOTATED;
                }
                slotOfFragment[i] = slot;
                if (slot < 0)
                    continue;
                record.IonFlags[slot] |= flag;
                float current = record.LibraryRelIntensity[slot];
                if (float.IsNaN(current) || fragment.RelativeIntensity > current)
                    record.LibraryRelIntensity[slot] = fragment.RelativeIntensity;
            }
            foreach (int core in TopFragmentExtractor.SelectTopFragmentIndices(fragments,
                         TopFragmentExtractor.CAL_TOP_N_FRAGMENTS))
            {
                if (slotOfFragment[core] >= 0)
                    record.IonFlags[slotOfFragment[core]] |= TrainingIonFlags.CORE;
            }
        }

        private static int NearestSlot(double[] ladder, double mz, double tolDa)
        {
            int best = -1;
            double bestDiff = double.MaxValue;
            for (int k = 0; k < ladder.Length; k++)
            {
                if (double.IsNaN(ladder[k]))
                    continue;
                double diff = Math.Abs(ladder[k] - mz);
                if (diff <= tolDa && diff < bestDiff)
                {
                    bestDiff = diff;
                    best = k;
                }
            }
            return best;
        }

        /// <summary>
        /// Project one ion's XIC onto the fit's (overall, column effects): its row effect is the
        /// median over its non-zero scans of <c>ln(x) - overall - col</c>, and its residuals,
        /// R^2, largest positive residual, apex residual, robust apex z and apex ratio follow
        /// from that row effect. For a core ion this is its own row of the fit to within the
        /// fit's convergence tolerance. Returns the row effect (NaN when the ion was never
        /// seen in the peak).
        /// </summary>
        private static double Project(double[] xic, TukeyMedianPolishResult polish, int apexPos,
            double residualMad, int k, TrainingRecord record)
        {
            int n = xic.Length;
            var offsets = new List<double>(n);
            for (int s = 0; s < n; s++)
            {
                if (xic[s] > 0)
                    offsets.Add(Math.Log(xic[s]) - polish.Overall - polish.ColEffects[s]);
            }
            if (offsets.Count == 0)
            {
                record.PolishApexRatio[k] = 0f;
                return double.NaN;
            }
            double rowEffect = Median(offsets);
            var residuals = new double[n];
            double posMax = 0.0;
            for (int s = 0; s < n; s++)
            {
                if (xic[s] > 0)
                {
                    residuals[s] = Math.Log(xic[s]) - (polish.Overall + rowEffect + polish.ColEffects[s]);
                    posMax = Math.Max(posMax, residuals[s]);
                }
                else
                {
                    residuals[s] = double.NaN;
                }
            }
            record.PolishRowEffect[k] = (float)rowEffect;
            record.PolishR2[k] = (float)TukeyMedianPolish.FragmentR2(polish.Overall, rowEffect, polish.ColEffects, residuals);
            record.PolishPosResidMax[k] = (float)posMax;
            double apexResidual = residuals[apexPos];
            record.PolishApexResidual[k] = (float)apexResidual;
            if (!double.IsNaN(apexResidual) && residualMad > 0)
                record.PolishOutlierZ[k] = (float)(apexResidual / (MAD_TO_SIGMA * residualMad));
            double predicted = Math.Exp(polish.Overall + rowEffect + polish.ColEffects[apexPos]);
            record.PolishApexRatio[k] = (float)(xic[apexPos] / predicted);
            return rowEffect;
        }

        /// <summary>
        /// Each ion's fit intensity relative to the most intense ion of the ladder:
        /// <c>exp(rowEffect - max rowEffect)</c>, so the strongest ion is 1.
        /// </summary>
        private static void SetRelativeIntensities(double[] rowEffects, TrainingRecord record)
        {
            double max = double.NegativeInfinity;
            foreach (double r in rowEffects)
            {
                if (!double.IsNaN(r) && r > max)
                    max = r;
            }
            if (double.IsNegativeInfinity(max))
                return;
            for (int k = 0; k < rowEffects.Length; k++)
            {
                if (!double.IsNaN(rowEffects[k]))
                    record.PolishRelIntensity[k] = (float)Math.Exp(rowEffects[k] - max);
            }
        }

        /// <summary>
        /// Shared-peak evidence in the two scopes docs/22 defines. APEX scope, the Carafe
        /// semantics: another claimant whose apex is this same scan and whose ladder matched the
        /// very peak this ion matched. CO-ELUTION scope, Osprey's: another claimant whose final
        /// boundaries contain this apex and whose ladder has an ion within the tolerance of this
        /// one. A claimant of the same peptidoform (another charge state) is not a competitor
        /// and is skipped, as is the precursor itself.
        /// </summary>
        private static void AddClaimantEvidence(TrainingRecord record, FdrEntry row, double runQ,
            double score, double[] ladder, int[] apexPeakIndex, double[] tolDa,
            IReadOnlyList<TrainingClaimant> claimants)
        {
            if (claimants == null)
                return;
            foreach (var claimant in claimants)
            {
                if (claimant.EntryId == row.EntryId ||
                    string.Equals(claimant.ModifiedSequence, record.ModifiedSequence, StringComparison.Ordinal))
                {
                    continue;
                }
                bool coelutes = claimant.StartRt <= row.ApexRt && row.ApexRt <= claimant.EndRt;
                bool sameApex = claimant.ScanNumber == row.ScanNumber;
                if (coelutes)
                    record.NCoelutingClaimants++;
                if (sameApex)
                    record.NSameApexClaimants++;
                if (!coelutes && !sameApex)
                    continue;
                bool better = claimant.RunQ < runQ || (claimant.RunQ.Equals(runQ) && claimant.Score > score);
                for (int k = 0; k < ladder.Length; k++)
                {
                    if (double.IsNaN(ladder[k]))
                        continue;
                    bool shares = false;
                    if (sameApex && apexPeakIndex[k] >= 0 && claimant.ApexPeakIndices.Contains(apexPeakIndex[k]))
                    {
                        shares = true;
                        record.SharedApexN[k] = Increment(record.SharedApexN[k]);
                        if (better)
                            record.IonFlags[k] |= TrainingIonFlags.BETTER_CLAIMANT_APEX;
                    }
                    if (coelutes && claimant.HasLadderIonNear(ladder[k], tolDa[k]))
                    {
                        shares = true;
                        record.SharedCoeluteN[k] = Increment(record.SharedCoeluteN[k]);
                        if (better)
                            record.IonFlags[k] |= TrainingIonFlags.BETTER_CLAIMANT_COELUTE;
                    }
                    if (shares && (float.IsNaN(record.MinClaimantQ[k]) || claimant.RunQ < record.MinClaimantQ[k]))
                        record.MinClaimantQ[k] = (float)claimant.RunQ;
                }
            }
        }

        /// <summary>
        /// The reconciled row's reference XIC aligned to the peak scans by retention time, or
        /// null when it has none. A scan the reference does not cover reads 0.
        /// </summary>
        private static double[] AlignReference(FdrEntry row, double[] peakRts)
        {
            var refRts = row.ReferenceXicRts;
            var refInts = row.ReferenceXicIntensities;
            if (refRts == null || refInts == null || refRts.Length == 0 || refRts.Length != refInts.Length)
                return null;
            var aligned = new double[peakRts.Length];
            int j = 0;
            for (int s = 0; s < peakRts.Length; s++)
            {
                while (j < refRts.Length && refRts[j] < peakRts[s])
                    j++;
                if (j < refRts.Length && refRts[j].Equals(peakRts[s]))
                    aligned[s] = refInts[j];
            }
            return aligned;
        }

        private static double MedianAbsoluteResidual(TukeyMedianPolishResult polish)
        {
            var values = new List<double>();
            foreach (var row in polish.Residuals)
            {
                foreach (double r in row)
                {
                    if (!double.IsNaN(r) && !double.IsInfinity(r))
                        values.Add(Math.Abs(r));
                }
            }
            return Median(values);
        }

        /// <summary>Median of a list (the mean of the middle pair for an even count), or NaN when empty.</summary>
        private static double Median(List<double> values)
        {
            if (values.Count == 0)
                return double.NaN;
            values.Sort(); // Array.Sort OK: a single primitive (double) list sorted only to take its median; tied values are equal
            int mid = values.Count / 2;
            return values.Count % 2 == 0 ? 0.5 * (values[mid - 1] + values[mid]) : values[mid];
        }

        private static byte Increment(byte count)
        {
            return count == byte.MaxValue ? count : (byte)(count + 1);
        }

        private static double Sum(float[] values)
        {
            double sum = 0.0;
            if (values != null)
            {
                foreach (float v in values)
                    sum += v;
            }
            return sum;
        }

        private static float[] NaNs(int n)
        {
            var values = new float[n];
            for (int i = 0; i < n; i++)
                values[i] = float.NaN;
            return values;
        }

        /// <summary>First index whose value is at least <paramref name="value"/>.</summary>
        private static int LowerBound(double[] sorted, double value)
        {
            int lo = 0, hi = sorted.Length;
            while (lo < hi)
            {
                int m = (lo + hi) >> 1;
                if (sorted[m] < value)
                    lo = m + 1;
                else
                    hi = m;
            }
            return lo;
        }

        /// <summary>First index whose value is greater than <paramref name="value"/>.</summary>
        private static int LowerBoundAbove(double[] sorted, double value)
        {
            int lo = 0, hi = sorted.Length;
            while (lo < hi)
            {
                int m = (lo + hi) >> 1;
                if (sorted[m] <= value)
                    lo = m + 1;
                else
                    hi = m;
            }
            return lo;
        }
    }

    /// <summary>
    /// One isolation window's MS2 spectra as the scorer saw them: m/z-calibrated, and sorted by
    /// (retention time, scan) - <c>CoelutionScorer.ScoreWindow</c>'s order - with a scan lookup.
    /// </summary>
    public sealed class TrainingEvidenceWindow
    {
        private readonly Dictionary<uint, int> _indexByScan;

        public TrainingEvidenceWindow(List<Spectrum> calibratedSpectra)
        {
            Spectra = calibratedSpectra;
            Spectra.Sort((a, b) => // Array.Sort OK: (RetentionTime, ScanNumber) is a unique total order, as in CoelutionScorer.ScoreWindow
            {
                int byRt = a.RetentionTime.CompareTo(b.RetentionTime);
                return byRt != 0 ? byRt : a.ScanNumber.CompareTo(b.ScanNumber);
            });
            Rts = new double[Spectra.Count];
            _indexByScan = new Dictionary<uint, int>(Spectra.Count);
            for (int i = 0; i < Spectra.Count; i++)
            {
                Rts[i] = Spectra[i].RetentionTime;
                _indexByScan[Spectra[i].ScanNumber] = i;
            }
        }

        public List<Spectrum> Spectra { get; }

        public double[] Rts { get; }

        public bool TryGetScanIndex(uint scanNumber, out int index)
        {
            return _indexByScan.TryGetValue(scanNumber, out index);
        }
    }

    /// <summary>
    /// Another identified precursor in the same isolation window, reduced to what the
    /// shared-peak evidence asks of it: its peak, its q-value and score, its ladder, and which
    /// peaks of its apex spectrum its ladder matched.
    /// </summary>
    public sealed class TrainingClaimant
    {
        private readonly double[] _sortedLadder;

        public TrainingClaimant(LibraryEntry entry, FdrEntry row, double runQ, double score,
            TrainingEvidenceWindow window, FragmentToleranceConfig tolerance)
        {
            EntryId = row.EntryId;
            ModifiedSequence = entry.ModifiedSequence ?? string.Empty;
            RunQ = runQ;
            Score = score;
            ScanNumber = row.ScanNumber;
            StartRt = row.StartRt;
            EndRt = row.EndRt;
            var ladder = FragmentLadder.Build(entry.Sequence, entry.Modifications, entry.Charge);
            _sortedLadder = ladder.Where(mz => !double.IsNaN(mz)).OrderBy(mz => mz).ToArray();
            ApexPeakIndices = new HashSet<int>();
            if (window.TryGetScanIndex(row.ScanNumber, out int apexIdx))
            {
                var apex = window.Spectra[apexIdx];
                foreach (double mz in _sortedLadder)
                {
                    double tolDa = tolerance.ToleranceDa(mz);
                    int peak = TopFragmentExtractor.FindClosestPeakInWindow(apex.Mzs, mz, mz - tolDa, mz + tolDa);
                    if (peak >= 0)
                        ApexPeakIndices.Add(peak);
                }
            }
        }

        public uint EntryId { get; }
        public string ModifiedSequence { get; }
        public double RunQ { get; }
        public double Score { get; }
        public uint ScanNumber { get; }
        public double StartRt { get; }
        public double EndRt { get; }

        /// <summary>Indices of the apex-spectrum peaks this claimant's ladder matched.</summary>
        public HashSet<int> ApexPeakIndices { get; }

        /// <summary>Whether any applicable ion of this claimant's ladder lies within <paramref name="tolDa"/> of <paramref name="mz"/>.</summary>
        public bool HasLadderIonNear(double mz, double tolDa)
        {
            int idx = ScoringMath.LowerBoundDouble(_sortedLadder, mz - tolDa);
            return idx < _sortedLadder.Length && _sortedLadder[idx] <= mz + tolDa;
        }
    }

    /// <summary>
    /// What <see cref="TrainingEvidence.Compute"/> needs from the run rather than the precursor.
    /// </summary>
    public sealed class TrainingEvidenceSettings
    {
        /// <summary>
        /// The config the scorer searched with: its <see cref="OspreyConfig.FragmentTolerance"/>
        /// must be the MS2-calibrated tolerance, as it is inside the scorer.
        /// </summary>
        public OspreyConfig SearchConfig { get; set; }

        /// <summary>
        /// [lower, upper] MS2 scan window of the isolation window being computed, or null when
        /// the run did not record one (every applicable ion is then in range).
        /// </summary>
        public double[] Ms2ScanWindow { get; set; }

        /// <summary>Also return the per-ion XIC matrix.</summary>
        public bool WriteXics { get; set; }

        /// <summary>These settings with <paramref name="ms2ScanWindow"/> as the scan window.</summary>
        public TrainingEvidenceSettings WithMs2ScanWindow(double[] ms2ScanWindow)
        {
            return new TrainingEvidenceSettings
            {
                SearchConfig = SearchConfig,
                Ms2ScanWindow = ms2ScanWindow,
                WriteXics = WriteXics,
            };
        }
    }
}
