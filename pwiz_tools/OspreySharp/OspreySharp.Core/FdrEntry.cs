/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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

using System;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// An entry in the FDR results table with q-values at multiple levels.
    /// Maps to osprey-core/src/types.rs FdrEntry.
    /// </summary>
    public class FdrEntry
    {
        public uint EntryId { get; set; }
        public uint ParquetIndex { get; set; }
        public bool IsDecoy { get; set; }
        public byte Charge { get; set; }
        public uint ScanNumber { get; set; }
        public double ApexRt { get; set; }
        public double StartRt { get; set; }
        public double EndRt { get; set; }
        public double CoelutionSum { get; set; }
        public double Score { get; set; }
        public double RunPrecursorQvalue { get; set; }
        public double RunPeptideQvalue { get; set; }
        public double RunProteinQvalue { get; set; }
        public double ExperimentPrecursorQvalue { get; set; }
        public double ExperimentPeptideQvalue { get; set; }
        public double ExperimentProteinQvalue { get; set; }
        public double Pep { get; set; }
        public string ModifiedSequence { get; set; }

        /// <summary>
        /// Full PIN feature vector (21 features) computed during coelution scoring.
        /// Used by Percolator FDR. Null if features have not been computed yet
        /// (e.g., for stubs loaded from a Parquet cache without features).
        /// </summary>
        public double[] Features { get; set; }

        public FdrEntry()
        {
            RunPrecursorQvalue = 1.0;
            RunPeptideQvalue = 1.0;
            RunProteinQvalue = 1.0;
            ExperimentPrecursorQvalue = 1.0;
            ExperimentPeptideQvalue = 1.0;
            ExperimentProteinQvalue = 1.0;
            Pep = 1.0;
        }

        /// <summary>
        /// Returns the effective run-level q-value based on the FDR control level.
        /// </summary>
        public double EffectiveRunQvalue(FdrLevel level)
        {
            switch (level)
            {
                case FdrLevel.Precursor:
                    return RunPrecursorQvalue;
                case FdrLevel.Peptide:
                    return RunPeptideQvalue;
                case FdrLevel.Both:
                    return Math.Max(RunPrecursorQvalue, RunPeptideQvalue);
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        /// <summary>
        /// Returns the effective experiment-level q-value based on the FDR control level.
        /// </summary>
        public double EffectiveExperimentQvalue(FdrLevel level)
        {
            switch (level)
            {
                case FdrLevel.Precursor:
                    return ExperimentPrecursorQvalue;
                case FdrLevel.Peptide:
                    return ExperimentPeptideQvalue;
                case FdrLevel.Both:
                    return Math.Max(ExperimentPrecursorQvalue, ExperimentPeptideQvalue);
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
        }
    }
}
