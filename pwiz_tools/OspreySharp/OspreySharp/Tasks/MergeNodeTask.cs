/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;
using pwiz.OspreySharp.Tasks;

namespace pwiz.OspreySharp.MergeNode
{
    /// <summary>
    /// Final merge-node phase of the OspreySharp pipeline (Stage 7 in the
    /// HPC-boundary view from <c>Osprey-workflow.html</c>): persists the
    /// per-file 2nd-pass FDR-score sidecars, runs run-wide protein FDR
    /// (parsimony + picked-protein TDC), and writes the BiblioSpecLite
    /// <c>.blib</c> output. Invoked once per pipeline run on the merge
    /// node — no per-file fan-out beyond the sidecar write loop.
    ///
    /// Phase A scope: the 2nd-pass FDR sidecar block has moved here from
    /// <c>AnalysisPipeline.Run</c>; <see cref="AnalysisPipeline.RunProteinFdr"/>
    /// and <see cref="AnalysisPipeline.WriteBlibOutput"/> still live on
    /// AnalysisPipeline for now and are invoked via internal access. A
    /// follow-up commit can move their bodies into this file once the
    /// framework shape is settled.
    /// </summary>
    internal sealed class MergeNodeTask : OspreyTask
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly List<KeyValuePair<string, List<FdrEntry>>> _perFileEntries;
        private readonly List<LibraryEntry> _fullLibrary;
        private readonly Dictionary<uint, LibraryEntry> _libraryById;
        private readonly Dictionary<string, string> _perFileParquetPaths;

        public MergeNodeTask(
            AnalysisPipeline pipeline,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            Dictionary<uint, LibraryEntry> libraryById,
            Dictionary<string, string> perFileParquetPaths)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _perFileEntries = perFileEntries ?? throw new ArgumentNullException(nameof(perFileEntries));
            _fullLibrary = fullLibrary ?? throw new ArgumentNullException(nameof(fullLibrary));
            _libraryById = libraryById ?? throw new ArgumentNullException(nameof(libraryById));
            _perFileParquetPaths = perFileParquetPaths ?? throw new ArgumentNullException(nameof(perFileParquetPaths));
        }

        public override string Name => @"MergeNode";

        public override bool Run(PipelineContext ctx)
        {
            var config = ctx.Config;

            // Stage 8: Protein FDR (optional)
            if (config.ProteinFdr.HasValue)
            {
                // Persist post-Stage-6 per-file 2nd-pass FDR scores
                // BEFORE RunProteinFdr. The sidecar holds Score +
                // run/experiment precursor/peptide q-values + Pep +
                // RunProteinQvalue (the latter set by
                // RunFirstPassProteinFdr earlier); none of those
                // fields are mutated by RunProteinFdr, which only
                // sets ExperimentProteinQvalue via
                // PropagateProteinQvalues. Writing here lets the
                // OSPREY_STAGE7_PROTEIN_FDR_ONLY early exit (used
                // by stage6 isolation in Test-Regression) leave the
                // sidecar on disk for downstream --join-at-pass=2
                // rehydration. Skipped on --join-at-pass=2 itself
                // (sidecar already loaded; no need to round-trip).
                if (!config.ExpectReconciledInput
                    && _perFileParquetPaths.Count > 0)
                {
                    var inputByFileName = new Dictionary<string, string>();
                    foreach (var inputFile in config.InputFiles)
                        inputByFileName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;

                    int pass2Failures = 0;
                    foreach (var kvp in _perFileEntries)
                    {
                        string fileName = kvp.Key;
                        if (!inputByFileName.TryGetValue(fileName, out string inputFile3))
                            continue;
                        try
                        {
                            FdrScoresSidecar.Write(
                                FdrScoresSidecar.Pass2Path(inputFile3),
                                kvp.Value, FdrScoresSidecar.Pass.SecondPass);
                        }
                        catch (Exception ex)
                        {
                            ctx.LogWarning(string.Format(
                                @"Failed to write 2nd-pass FDR sidecar for {0}: {1}",
                                fileName, ex.Message));
                            pass2Failures++;
                        }
                    }
                    if (pass2Failures == 0)
                    {
                        ctx.LogInfo(string.Format(
                            @"Wrote 2nd-pass FDR sidecars for {0} file(s)",
                            _perFileEntries.Count));
                    }
                }

                ctx.LogInfo(string.Empty);
                ctx.LogInfo(string.Format(@"Running protein-level FDR at {0:P1}...",
                    config.ProteinFdr.Value));
                var swProtein = Stopwatch.StartNew();
                _pipeline.RunProteinFdr(_perFileEntries, _fullLibrary, config);
                swProtein.Stop();
                ctx.LogInfo(string.Format(@"[TIMING] Protein FDR: {0:F1}s",
                    swProtein.Elapsed.TotalSeconds));
            }

            // Stage 9: Write output blib
            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(@"Writing output to {0}...", config.OutputBlib));
            var swBlib = Stopwatch.StartNew();
            _pipeline.WriteBlibOutput(_perFileEntries, _fullLibrary, _libraryById, config);
            swBlib.Stop();
            ctx.LogInfo(string.Format(@"[TIMING] Blib output: {0:F1}s",
                swBlib.Elapsed.TotalSeconds));
            return true;
        }
    }
}
