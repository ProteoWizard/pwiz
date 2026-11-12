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
    ///
    /// <para>At the Stage 5 -> 6 boundary, drop <c>LibraryEntry.Fragments</c> for every library
    /// entry that can no longer be scored or written - everything outside the compaction
    /// survivors and the gap-fill candidates. The identity fields (<c>ModifiedSequence</c> /
    /// <c>ProteinIds</c> / m/z / RT) are KEPT on every entry. The library is otherwise held to the
    /// end of Stage 7 to write 37,078 spectra out of 6,275,151 entries - 0.6%. Measured as an A/B
    /// on 4 SEA-AD files against the full 12.7 GB library: Stage 7 peak working set 28.5 -&gt;
    /// 17.7 GB. Few files is the MAXIMUM-saving case - the library is fixed while the retained
    /// set grows with file count.</para>
    ///
    /// <para>Why fragments and not whole entries: <c>ProteinFdr.BuildProteinParsimony</c>
    /// and <c>FirstPassFdrTask.BuildProteinCompactStratum</c> both walk the ENTIRE library
    /// after Stage 5, including entries already judged false. They read only the identity
    /// fields, never the spectra - so dropping entries would silently move protein FDR,
    /// while dropping fragments cannot. The blib write is safe for a separate reason:
    /// <c>BlibOutputWriter.PrepareSpectra</c> reads fragments only for
    /// <c>bestByPrecursor</c>, which is derived from the post-compaction survivors, so
    /// blib-written is a SUBSET of what is retained here.</para>
    ///
    /// <para>The release is output-neutral, so it has no opt-out and no validity-key term: the
    /// <c>OSPREY_RELEASE_LIBRARY_FRAGMENTS=0</c> resident arm that once served as the A/B
    /// byte-identity oracle was retired with the other parity-only paths. Regression mode 6
    /// asserts that it engages.</para>
    ///
    /// <para><see cref="BuildRetainedBaseIds"/> is a pure function of its inputs so it can be
    /// tested without a pipeline; <see cref="RunsOnThisLeg"/> needs the config and is the single
    /// source of truth for the call sites.</para>
    /// </summary>
    internal static class LibraryFragmentRelease
    {
        /// <summary>
        /// Whether the release executes on THIS leg. The release rides specific code paths, and
        /// a leg that cannot reach one of them keeps the whole library resident
        /// (<see cref="LegAdmitsRelease"/>). SecondPassFDR releases against the analysis-wide
        /// summary on disk; every other leg releases on Stage 5's projection path.
        /// </summary>
        public static bool RunsOnThisLeg(PipelineContext ctx)
        {
            return ctx?.Config != null && LegAdmitsRelease(ctx.Config);
        }

        /// <summary>
        /// Whether this leg can perform a release AT ALL.
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
        /// leg's pipeline entirely. It cannot compute the retained set - <c>FirstPassFdrTask</c>
        /// owns both halves of it - so it reads the analysis-wide summary that task left on
        /// disk (<c>ScoringTaskShared.ReadRetainedBaseIdsOrFail</c>).</para>
        ///
        /// <para>Everywhere else the release rides <c>FirstPassFdrTask</c>'s projection path,
        /// which no config condition excludes any more. <c>--fdrbench-pass 1</c> stopped forcing
        /// the resident pool with #4507 (the pass-1 TSV is emitted before compaction from the
        /// sidecars, so the release, which runs after, never touches what it reads), and the
        /// last non-Percolator FDR method, the other exclusion, was deleted with #4543.</para>
        /// </summary>
        private static bool LegAdmitsRelease(OspreyConfig config)
        {
            return !config.StopAfterStage5;
        }

        /// <summary>
        /// The base_ids whose spectra are still needed after Stage 5: the post-compaction
        /// survivors plus every gap-fill candidate.
        ///
        /// <para>Gap-fill is unioned in as a belt, and the braces are that it cannot actually
        /// add anything. This doc used to say gap-fill "resolves the MISSING charge states of
        /// passing peptides ... so by construction it names entries that did not survive
        /// compaction", and that is wrong in a way worth stating, because it is what left the
        /// subset question open in issue #4650. <c>GapFillTargetIdentifier</c> emits a target
        /// for a <c>(modified sequence, charge)</c> that PASSED in a sibling replicate and is
        /// absent from THIS file's rows - the same precursor, not another charge of it. Its key
        /// therefore came out of some file's post-compaction entries, so its base_id is already
        /// in the join-wide first-pass set. "Did not survive compaction" is true of the file's
        /// ROW, never of the base_id.</para>
        ///
        /// <para>The union stays because it costs one pass over a small list and the argument
        /// above is an invariant of another class. What must NOT be built on it is a claim that
        /// this set is strictly larger than the analysis-wide summary: it is not, which is why
        /// Stage 7 can read that summary instead of folding every run's pool to rebuild the
        /// set.</para>
        ///
        /// <para>The argument has ONE dependency worth naming, because nothing else does.
        /// <c>GapFillTargetIdentifier</c> emits <c>libLookup[(modified sequence, charge)]</c>,
        /// not the passing row's own <c>EntryId</c>, and those coincide only because
        /// <c>LibraryDeduplicator</c> groups on exactly that pair - so one id per key. Re-key
        /// dedup (add IsDecoy, protein, or source file) and two targets can share a key: the
        /// lookup keeps one id while the passing rows carry the other, gap-fill emits an id that
        /// is NOT in the summary, and Stage 7 releases a spectrum the blib write still reads.
        /// Whoever changes that grouping key owns this.</para>
        ///
        /// <paramref name="firstPassBaseIds"/> is already pair-symmetric (a target and its
        /// paired decoy share a base_id), so decoys ride along without being named. It is
        /// required; the caller's null check is what decides whether this leg releases at all,
        /// so a null reaching here is a wiring fault and throws.
        ///
        /// <para><paramref name="gapFillByFile"/> is optional, and no production caller passes it:
        /// the planner no longer keeps an all-runs gap-fill map (issue #4664), and by the
        /// argument above every target it would add is already in
        /// <paramref name="firstPassBaseIds"/>. The union is kept, and unit-tested, so that
        /// argument stays checkable rather than assumed.</para>
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

        /// <summary>
        /// Write the machine-channel record of one release, which the regression gate parses to
        /// assert the release ran and what it kept. <paramref name="scope"/> is
        /// <see cref="LogKey.SCOPE_RESCORE_GAP_FILL"/> or
        /// <see cref="LogKey.SCOPE_RETAINED_SUMMARY"/>.
        /// </summary>
        public static void LogRelease(IOspreyLog log, int released, int entries, int retained, string scope)
        {
            log.LogInfo(LogTag.COUNT, LogKey.Format(LogKey.COUNT_LIBRARY_FRAGMENTS_RELEASED,
                @"released={0} entries={1} retained={2} scope={3}", released, entries, retained, scope));
        }
    }
}
