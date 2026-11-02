/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// One passing (file, precursor) observation, as everything downstream of the blib phase's
    /// two FDR gates actually reads it - a VALUE, not a reference into the survivor pool.
    ///
    /// <para>The blib phase used to carry its passing set as
    /// <c>List&lt;KeyValuePair&lt;string, FdrEntry&gt;&gt;</c>: 16 bytes a row, but every row
    /// pinning a ~263-byte <c>FdrEntry</c> and, through it, that file's whole survivor list. At
    /// 257 CHS files that is 11.7 M rows holding the 40 GB pool alive from the FDR gates through
    /// the last RefSpectra row (#4486). These eight fields are the ENTIRE set its consumers
    /// read - the boundary map, the best-experiment-q map, the precursor facts and the
    /// retention-time rows - so copying them costs ~64 bytes a row, about 750 MB, and releases
    /// each file as the gate walks past it.</para>
    ///
    /// <para>Larger per row than the reference it replaces, and deliberately so: while something
    /// else still holds the pool this is a straight loss, which is why it lands with the
    /// conversions that stop holding it rather than before them.</para>
    ///
    /// <para><see cref="ModifiedSequence"/> is INTERNED against the passing set as these are
    /// built. The parquet reader hands out a fresh string per row, so keeping the row's own
    /// instance would retain 11.7 M strings - more than the records themselves - where the
    /// distinct peptide count is about 40,000.</para>
    /// </summary>
    internal readonly struct PassingObservation
    {
        /// <summary>The per-file key this observation came from, i.e. the run.</summary>
        public readonly string FileName;

        public readonly string ModifiedSequence;
        public readonly byte Charge;

        /// <summary>
        /// <c>FdrEntry.EffectiveRunQvalue(FdrLevel.Both)</c>, resolved once here rather than at
        /// each of the four call sites that used to ask the entry for it.
        /// </summary>
        public readonly double RunQvalue;

        public readonly double ExperimentPrecursorQvalue;
        public readonly double ApexRt;
        public readonly double StartRt;
        public readonly double EndRt;

        public PassingObservation(
            string fileName, string modifiedSequence, byte charge, double runQvalue,
            double experimentPrecursorQvalue, double apexRt, double startRt, double endRt)
        {
            FileName = fileName;
            ModifiedSequence = modifiedSequence;
            Charge = charge;
            RunQvalue = runQvalue;
            ExperimentPrecursorQvalue = experimentPrecursorQvalue;
            ApexRt = apexRt;
            StartRt = startRt;
            EndRt = endRt;
        }

        /// <summary>The (peptide, charge) key every blib-phase aggregate is built on.</summary>
        public (string, byte) PrecursorKey => (ModifiedSequence, Charge);
    }
}
