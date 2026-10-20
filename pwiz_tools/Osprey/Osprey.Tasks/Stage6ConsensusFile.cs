/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.IO;
using Newtonsoft.Json;
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Everything a run's Stage 6 plan reads from outside that run: the cross-run consensus
    /// retention times and the two sets of precursors that passed first-pass FDR somewhere in the
    /// experiment. All three are O(distinct peptides or precursors), never O(runs x entries).
    /// </summary>
    internal sealed class Stage6Consensus
    {
        /// <summary>Targets then decoys, each ordinal by modified sequence, as
        /// <see cref="ConsensusRts.Accumulator"/> builds it. Empty when no peptide was detected
        /// in enough runs, or the analysis has one run.</summary>
        public IReadOnlyList<PeptideConsensusRT> Consensus { get; set; }

        /// <summary>(base_id, charge) of every target passing the consensus FDR in any run.</summary>
        public HashSet<(uint, byte)> PassingBaseIds { get; set; }

        /// <summary>(modified sequence, charge) of every target passing the consensus FDR in any
        /// run.</summary>
        public HashSet<(string ModifiedSequence, byte Charge)> PassingPrecursors { get; set; }
    }

    /// <summary>
    /// The analysis-wide <c>&lt;blib-stem&gt;.1st-pass.consensus.json</c>: the
    /// <see cref="Stage6Consensus"/> FirstPassFDR computes, written once, at the end of the phase
    /// that computes it, for every PerFileRescoring node to plan its own runs from.
    ///
    /// <para>This file is what lets planning leave the join. Refitting a run's calibration,
    /// choosing its reconciliation actions and finding its gap-fill targets each read only that
    /// run plus these cross-run reductions, so with the reductions on disk the per-run half
    /// runs on the rescore node that already holds the run, instead of on the one node every
    /// HPC run waits for.</para>
    ///
    /// <para>JSON with the validity stamp as its first property, like the model file. Doubles
    /// use Json.NET's own round-trip format, so a reloaded consensus is bit-identical to the one
    /// computed - including the nullable MAD, which a <c>JsonConverter&lt;double&gt;</c> would not
    /// see, and a non-finite value, which <see cref="RoundtripDoubleConverter"/> refuses and which
    /// must not fail the whole first pass. The passing sets are written sorted, so the file is a
    /// function of its contents and not of the order runs were scanned in.</para>
    /// </summary>
    internal static class Stage6ConsensusFile
    {
        public const string EXT = @"." + FdrScoresSidecar.LABEL_FIRST_PASS + @".consensus.json";

        private const int SCHEMA_VERSION = 1;

        private sealed class ConsensusDto
        {
            /// <summary>The writing task's <see cref="ArtifactStamp"/>, serialized first.</summary>
            [JsonProperty(ArtifactStamp.JSON_PROPERTY, Order = -100)]
            public string OspreyValidity { get; set; }

            public int SchemaVersion { get; set; }
            public List<PeptideConsensusRT> Consensus { get; set; }

            /// <summary>Parallel arrays, ascending by (base_id, charge).</summary>
            public uint[] PassingBaseIds { get; set; }
            public int[] PassingBaseIdCharges { get; set; }

            /// <summary>Parallel arrays, ascending by (ordinal sequence, charge).</summary>
            public string[] PassingSequences { get; set; }
            public int[] PassingSequenceCharges { get; set; }
        }

        /// <summary>
        /// <c>&lt;blib-stem&gt;.1st-pass.consensus.json</c> in the directory the per-run artifacts
        /// resolve to, or null when the configuration has no output blib or no input.
        /// </summary>
        public static string PathFor(OspreyConfig config)
        {
            return ScoringTaskShared.AnalysisArtifactPath(config, EXT);
        }

        /// <summary>Write <paramref name="consensus"/> to <paramref name="path"/> through
        /// <see cref="SimpleFileSaver"/>.</summary>
        public static void Save(string path, Stage6Consensus consensus, ArtifactStamp stamp)
        {
            if (consensus == null)
                throw new ArgumentNullException(nameof(consensus));
            if (stamp == null)
                throw new ArgumentNullException(nameof(stamp));

            var baseIds = new List<(uint, byte)>(consensus.PassingBaseIds);
            baseIds.Sort(); // Array.Sort OK: distinct (base_id, charge) keys, so the comparison never ties
            var precursors = new List<(string ModifiedSequence, byte Charge)>(consensus.PassingPrecursors);
            precursors.Sort(ComparePrecursors); // Array.Sort OK: distinct (sequence, charge) keys, so the comparison never ties

            var dto = new ConsensusDto
            {
                OspreyValidity = stamp.ToString(),
                SchemaVersion = SCHEMA_VERSION,
                Consensus = new List<PeptideConsensusRT>(consensus.Consensus),
                PassingBaseIds = new uint[baseIds.Count],
                PassingBaseIdCharges = new int[baseIds.Count],
                PassingSequences = new string[precursors.Count],
                PassingSequenceCharges = new int[precursors.Count],
            };
            for (int i = 0; i < baseIds.Count; i++)
            {
                dto.PassingBaseIds[i] = baseIds[i].Item1;
                dto.PassingBaseIdCharges[i] = baseIds[i].Item2;
            }
            for (int i = 0; i < precursors.Count; i++)
            {
                dto.PassingSequences[i] = precursors[i].ModifiedSequence;
                dto.PassingSequenceCharges[i] = precursors[i].Charge;
            }

            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            string json = RoundtripDoubleConverter.NormalizeJson(
                JsonConvert.SerializeObject(dto, Formatting.None));
            using (var saver = new SimpleFileSaver(path))
            {
                File.WriteAllText(saver.SafeName, json);
                saver.Commit();
            }
        }

        /// <summary>
        /// Read the analysis's consensus (<see cref="PathFor"/>). Throws
        /// <see cref="InvalidDataException"/> naming the file when it is missing or unreadable: a
        /// rescore node cannot plan without it, and planning a run as though no peptide had a
        /// consensus would write a plausible-looking plan with no reconciliation in it. Throws
        /// the same when its stamp fails
        /// <see cref="ScoringTaskShared.FirstPassProductStampProblem"/>, because a consensus
        /// from another build or another FirstPassFDR run plans every run from the wrong
        /// analysis and the outputs still look current.
        /// </summary>
        public static Stage6Consensus Load(OspreyConfig config)
        {
            string path = PathFor(config);
            ConsensusDto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<ConsensusDto>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.Stage6ConsensusFile_Load_Failed_to_read_the_cross_run_consensus__0____1_, path, ex.Message), ex);
            }
            if (dto == null || dto.SchemaVersion != SCHEMA_VERSION || dto.Consensus == null ||
                dto.PassingBaseIds == null || dto.PassingBaseIdCharges == null ||
                dto.PassingBaseIds.Length != dto.PassingBaseIdCharges.Length ||
                dto.PassingSequences == null || dto.PassingSequenceCharges == null ||
                dto.PassingSequences.Length != dto.PassingSequenceCharges.Length)
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.Stage6ConsensusFile_Load_Failed_to_read_the_cross_run_consensus__0____1_, path,
                    OspreyTasksResources.Stage6ConsensusFile_Load_the_file_is_incomplete_or_from_another_build));
            }
            string stampProblem = ScoringTaskShared.FirstPassProductStampProblem(config, path);
            if (stampProblem != null)
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.Stage6ConsensusFile_Load_The_cross_run_consensus__0__cannot_be_used___1___To_rebuild_it,
                    path, stampProblem, FdrScoresSidecar.FIRST_PASS_FILE_PATTERN));
            }
            var result = new Stage6Consensus
            {
                Consensus = dto.Consensus,
                PassingBaseIds = new HashSet<(uint, byte)>(),
                PassingPrecursors = new HashSet<(string ModifiedSequence, byte Charge)>(),
            };
            for (int i = 0; i < dto.PassingBaseIds.Length; i++)
                result.PassingBaseIds.Add((dto.PassingBaseIds[i], (byte)dto.PassingBaseIdCharges[i]));
            for (int i = 0; i < dto.PassingSequences.Length; i++)
                result.PassingPrecursors.Add((dto.PassingSequences[i], (byte)dto.PassingSequenceCharges[i]));
            return result;
        }

        private static int ComparePrecursors((string ModifiedSequence, byte Charge) a,
            (string ModifiedSequence, byte Charge) b)
        {
            int c = string.CompareOrdinal(a.ModifiedSequence, b.ModifiedSequence);
            return c != 0 ? c : a.Charge.CompareTo(b.Charge);
        }
    }
}
