/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Everything Stage 7 reads off a survivor after the second pass has scored it.
    ///
    /// <para>Extracted so the Stage 7 gates - the blib's peptide and precursor gates, the
    /// second-pass protein FDR, the experiment-q clamp, the per-replicate report, the FDRBench
    /// writer and the model-diagnostics pass-2 cards - can be written ONCE and run over either
    /// the full <see cref="FdrEntry"/> or a lean row that carries only these thirteen members
    /// (issue #4486). Two copies of a gate would leave the byte-identity oracle comparing two
    /// different pieces of code rather than two representations of the same data.</para>
    ///
    /// <para>Today <see cref="FdrEntry"/> is the only implementer; the planned sidecar-derived
    /// lean row is the struct second implementer this seam exists for. Consumers must be
    /// generic - <c>where T : IFdrRow</c> - rather than taking the interface. A generic
    /// constraint compiles to a constrained call that neither boxes the struct nor allocates;
    /// taking <c>IFdrRow</c> directly would box every one of 137 M rows.</para>
    ///
    /// <para>The two experiment q-values are settable because the pre-blib re-clamp raises them
    /// in place (<c>PercolatorEngine.ApplyExperimentQFloors</c>). For a struct that means the
    /// caller must hold an ADDRESSABLE element - an array element or a <c>ref</c> local - since
    /// an <c>IReadOnlyList</c> indexer hands back a copy and the write would be discarded.
    /// Nothing else here is settable: every other field is final by the time Stage 7 has it.
    /// </para>
    /// </summary>
    public interface IFdrRow
    {
        uint EntryId { get; }

        /// <summary>
        /// The peptide identity the gates key on. A lean row holds a CANONICAL instance shared
        /// across every row with that sequence: the parquet reader hands out a fresh string per
        /// row, so an uninterned pool carries one string object per observation rather than one
        /// per distinct peptide.
        /// </summary>
        string ModifiedSequence { get; }

        byte Charge { get; }
        bool IsDecoy { get; }
        double Score { get; }
        double RunPrecursorQvalue { get; }
        double RunPeptideQvalue { get; }
        double ExperimentPrecursorQvalue { get; set; }
        double ExperimentPeptideQvalue { get; set; }

        /// <summary>Peak boundaries and area, for the blib's RefSpectra and peak-boundary rows.</summary>
        double ApexRt { get; }
        double StartRt { get; }
        double EndRt { get; }
        double BoundsArea { get; }
    }

    /// <summary>
    /// The two q-value selections every Stage 7 gate makes, in one place for both row types.
    ///
    /// <para>Extension methods rather than default interface members: Osprey multi-targets
    /// net472, and a default interface implementation needs runtime support .NET Framework does
    /// not have. Generic, so a struct row resolves them without boxing.</para>
    /// </summary>
    public static class FdrRowExtensions
    {
        /// <summary>
        /// Run-level q at the requested granularity. <see cref="FdrLevel.Both"/> is the MAXIMUM
        /// of the two, i.e. "this run passes at both granularities", which is the invariant a
        /// blib ID line represents.
        /// </summary>
        public static double EffectiveRunQvalue<T>(this T row, FdrLevel level) where T : IFdrRow
        {
            switch (level)
            {
                case FdrLevel.Precursor:
                    return row.RunPrecursorQvalue;
                case FdrLevel.Peptide:
                    return row.RunPeptideQvalue;
                case FdrLevel.Both:
                    return Math.Max(row.RunPrecursorQvalue, row.RunPeptideQvalue);
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        /// <summary>
        /// Experiment-level q at the requested granularity, on the same rule as
        /// <see cref="EffectiveRunQvalue{T}"/>.
        /// </summary>
        public static double EffectiveExperimentQvalue<T>(this T row, FdrLevel level) where T : IFdrRow
        {
            switch (level)
            {
                case FdrLevel.Precursor:
                    return row.ExperimentPrecursorQvalue;
                case FdrLevel.Peptide:
                    return row.ExperimentPeptideQvalue;
                case FdrLevel.Both:
                    return Math.Max(row.ExperimentPrecursorQvalue, row.ExperimentPeptideQvalue);
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
        }
    }
}
