/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

using System.Collections.Generic;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The library-fragment release (issue #4532): the set arithmetic, plus the ONE predicate
    /// that decides whether a given leg of the pipeline performs it.
    /// <see cref="OspreyEnvironment.ReleaseLibraryFragments"/> carries the rationale and the
    /// safety argument.
    ///
    /// <para>The retained-set builders are pure functions of their inputs so they can be tested
    /// without a pipeline; <see cref="RunsOnThisLeg"/> needs the config and is the single source
    /// of truth for BOTH the call sites and the validity-key suffix. That is deliberate: those
    /// two must never be able to disagree, because the suffix's whole job is to record which
    /// arm produced an output directory.</para>
    /// </summary>
    internal static class LibraryFragmentRelease
    {
        /// <summary>
        /// Whether the release actually executes on THIS leg. Not simply
        /// <see cref="OspreyEnvironment.ReleaseLibraryFragments"/>: the release rides specific
        /// code paths, and a leg that cannot reach one of them keeps the whole library resident
        /// no matter what the flag says.
        ///
        /// <para>Two things gate it: whether the LEG admits a release at all
        /// (<see cref="LegAdmitsRelease"/>, a pure function of the config) and whether the env
        /// arms asked for one. SecondPassFDR is the exception to the second - its release is
        /// its own, so it does not ride <c>OSPREY_FDR_PROJECTION</c>.</para>
        /// </summary>
        public static bool RunsOnThisLeg(PipelineContext ctx)
        {
            if (!OspreyEnvironment.ReleaseLibraryFragments || ctx?.Config == null)
                return false;
            if (!LegAdmitsRelease(ctx.Config))
                return false;
            // SecondPassFdrTask releases against the reported pool, which Stage 5's projection
            // path has no part in; everywhere else the release rides that path.
            return ctx.Config.ExpectReconciledInput || OspreyEnvironment.UseFdrProjection;
        }

        /// <summary>
        /// Cache-validity suffix for the release arm. EMPTY when the release RAN, and equally
        /// EMPTY where <see cref="LegAdmitsRelease"/> is false - on such a leg the two arms are
        /// literally the same run, so a term would invalidate output directories to record a
        /// difference that cannot exist. It is emitted only for the case that matters: a leg
        /// that COULD have released and did not.
        ///
        /// <para>Keyed on <see cref="RunsOnThisLeg"/> rather than on
        /// <see cref="OspreyEnvironment.ReleaseLibraryFragments"/> alone, because the flag is not
        /// what the outputs record. A leg that never released - the flag was on but
        /// <c>OSPREY_FDR_PROJECTION=0</c> put Stage 5 on the resident path, say - would otherwise
        /// stamp the same EMPTY suffix as a leg that did, and a later run WITH the release could
        /// adopt those outputs, skip the work, and report the release arm's memory profile
        /// without ever executing it. That is the self-confirming A/B the suffix exists to
        /// prevent, left open one gate upstream.</para>
        /// </summary>
        public static string ValidityKeySuffix(PipelineContext ctx)
        {
            if (RunsOnThisLeg(ctx))
                return string.Empty;
            return ctx?.Config != null && LegAdmitsRelease(ctx.Config) ? @";libfrag=0" : string.Empty;
        }

        /// <summary>
        /// Whether this leg can perform a release AT ALL, whatever the env arms say. Separate
        /// from <see cref="RunsOnThisLeg"/> because the validity key needs the distinction:
        /// "did not release" is worth recording only where releasing was possible.
        ///
        /// <para><c>StopAfterStage5</c> (<c>--task FirstPassFDR</c>) is excluded for TWO
        /// independent reasons, either alone disqualifying. (1) The gap-fill plan is NOT
        /// populated there - <c>PlanStage6</c> returns early before assigning it - so the
        /// retained set would be survivors ONLY and the release would strip exactly the
        /// gap-fill candidates it exists to protect. (2) There is nothing to free: that path
        /// already loads the library with <c>OmitFragments</c>, and
        /// <see cref="LibraryEntry.ReleaseSpectrum"/> detects an already-released entry by
        /// reference identity rather than by "has a spectrum", so it would swap one shared
        /// singleton for another and report millions released having freed zero bytes - a
        /// fabricated saving printed straight above a [MEM] probe. The worker also exits after
        /// Stage 5, so there is no Stage 6/7 to save memory for.</para>
        ///
        /// <para><c>ExpectReconciledInput</c> (<c>--task SecondPassFDR</c>) releases from
        /// <c>SecondPassFdrTask</c> instead of <c>FirstPassFdrTask</c>, which is excluded from that
        /// leg's pipeline entirely. See
        /// <see cref="BuildRetainedBaseIds(IEnumerable{KeyValuePair{string, List{FdrEntry}}})"/>.</para>
        ///
        /// <para>Everywhere else the release rides <c>FirstPassFdrTask</c>'s projection path and
        /// inherits its config conditions. <c>--fdrbench-pass 1</c> is the one that bites: it
        /// forces the RESIDENT first-pass pool, which computes no surviving base_id set to
        /// release against.</para>
        /// </summary>
        private static bool LegAdmitsRelease(OspreyConfig config)
        {
            if (config.StopAfterStage5)
                return false;
            if (config.ExpectReconciledInput)
                return true;

            bool needsResidentFirstPassPool =
                !string.IsNullOrEmpty(config.OutputFdrBench) &&
                config.FdrBenchPass == OspreyConfig.FDRBENCH_PASS_1;
            return config.FdrMethod.UsesPercolatorFramework() && !needsResidentFirstPassPool;
        }

        /// <summary>
        /// The base_ids whose spectra are still needed after Stage 5: the post-compaction
        /// survivors plus every gap-fill candidate.
        ///
        /// <para>Gap-fill MUST be unioned in. <c>GapFillTargetIdentifier</c> resolves the
        /// MISSING charge states of passing peptides through the library, so by construction it
        /// names entries that did not survive compaction - and Stage 6 then scores them. A
        /// retained set of survivors alone would strip exactly the spectra gap-fill is about to
        /// ask for.</para>
        ///
        /// <paramref name="firstPassBaseIds"/> is already pair-symmetric (a target and its
        /// paired decoy share a base_id), so decoys ride along without being named. It is
        /// required; the caller's null check is what decides whether this leg releases at all,
        /// so a null reaching here is a wiring fault and throws.
        ///
        /// <para><paramref name="gapFillByFile"/> is optional, and NOT because "planning was
        /// skipped" - skipping planning leaves the non-null empty initializer. It is optional
        /// because the bundle-adopt path sources it from the reconciliation envelope, which can
        /// carry nothing.</para>
        /// </summary>
        public static HashSet<uint> BuildRetainedBaseIds(
            HashSet<uint> firstPassBaseIds,
            IReadOnlyDictionary<string, List<GapFillTarget>> gapFillByFile)
        {
            var retained = new HashSet<uint>(firstPassBaseIds);
            if (gapFillByFile == null)
                return retained;

            foreach (var kvp in gapFillByFile)
            {
                if (kvp.Value == null)
                    continue;
                foreach (var g in kvp.Value)
                    retained.Add(g.TargetEntryId & ScoringTaskShared.BASE_ID_MASK);
            }
            return retained;
        }

        /// <summary>
        /// SecondPassFDR's retained set: every base_id present in the final per-file pool.
        ///
        /// <para>SecondPassFDR (<c>--task SecondPassFDR</c>) cannot use the survivors +
        /// gap-fill form above, because <c>FirstPassFdrTask</c> - which computes both halves - is
        /// excluded from that leg's pipeline. It has the post-rescore pool instead, and that is
        /// the tighter statement of the same fact: everything Stage 7 and the blib write can
        /// reach is IN this pool. <c>BlibOutputWriter.PrecompressSpectra</c> reads fragments for
        /// <c>bestByPrecursor</c>, which is derived from it by successive filtering, and nothing
        /// else after Stage 6 reads a spectrum at all - second-pass Percolator reloads FEATURES
        /// from the reconciled parquet, and protein parsimony reads only identity.</para>
        ///
        /// <para>Decoys are included rather than filtered out. Keeping them costs nothing (a
        /// target and its paired decoy share a base_id, so a decoy row adds an entry only where
        /// the target is absent) and it keeps this a statement about the POOL rather than about
        /// what is believed to read it.</para>
        /// </summary>
        public static HashSet<uint> BuildRetainedBaseIds(
            IEnumerable<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            var retained = new HashSet<uint>();
            foreach (var kvp in perFileEntries)
            {
                if (kvp.Value == null)
                    continue;
                foreach (var e in kvp.Value)
                    retained.Add(e.EntryId & ScoringTaskShared.BASE_ID_MASK);
            }
            return retained;
        }

        /// <summary>
        /// Call <see cref="LibraryEntry.ReleaseSpectrum"/> on every entry whose base_id is
        /// outside <paramref name="retainedBaseIds"/> and return how many were released.
        /// Identity fields
        /// are untouched on ALL entries, because protein parsimony and the protein-compact
        /// stratum walk the whole library after this point and read them.
        ///
        /// <para>Also drops <see cref="FragmentMath"/>'s memoized top-6 m/z table, which is
        /// library-derived data of the same order (a <c>double[]</c> per entry Id, keyed over
        /// the WHOLE library rather than the survivors) sitting inside the same floor this
        /// release is measured against. Behavior-neutral in both directions: it is a pure memo,
        /// so a retained entry recomputes the identical value, and dropping it also stops a
        /// released entry's stale cached top-6 from satisfying the prefilter that would
        /// otherwise have tripped the released-spectrum tripwire.</para>
        /// </summary>
        public static int ReleaseFragments(
            IList<LibraryEntry> fullLibrary, HashSet<uint> retainedBaseIds)
        {
            // Both arguments are required. A null library here would be a wiring fault, and
            // returning 0 for it would print "released 0 entries" - indistinguishable from a
            // correct run with nothing to release, in a design whose whole posture is to fail
            // loudly rather than degrade quietly.
            int released = 0;
            foreach (var e in fullLibrary)
            {
                if (retainedBaseIds.Contains(e.Id & ScoringTaskShared.BASE_ID_MASK))
                    continue;
                if (e.ReleaseSpectrum())
                    released++;
            }
            FragmentMath.ClearTop6MzCache();
            return released;
        }
    }
}
