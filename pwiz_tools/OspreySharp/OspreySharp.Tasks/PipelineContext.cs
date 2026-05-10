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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Shared state passed between <see cref="OspreyTask"/> steps.
    ///
    /// In Phase A this is a thin envelope around the
    /// <see cref="OspreyConfig"/> plus the logging callbacks each task
    /// uses for progress messages. As tasks are extracted, the
    /// per-stage state currently held as locals in
    /// <c>AnalysisPipeline.Run</c> (full library, per-file FDR
    /// entries, per-file calibrations, parquet path map, ...) moves
    /// to fields on this context. Tasks read upstream state from the
    /// fields and write downstream state back.
    ///
    /// The context is constructed once at the top of
    /// <c>AnalysisPipeline.Run</c> and lives for the duration of the
    /// pipeline execution. A cloned task in Phase B (resume scenario)
    /// can be re-run against an existing context if its inputs are
    /// still valid.
    /// </summary>
    public sealed class PipelineContext
    {
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private readonly Action<string> _logError;

        /// <summary>
        /// The configuration parsed from CLI args and the input library.
        /// Tasks may shallow-clone the config for per-file scratch
        /// (mirroring <c>OspreyConfig.ShallowClone</c>) but must not
        /// mutate the outer instance — downstream tasks rely on its
        /// post-CLI-parse state for hash-stable
        /// <see cref="OspreyConfig.SearchParameterHash"/> computations.
        /// </summary>
        public OspreyConfig Config { get; }

        public PipelineContext(OspreyConfig config,
            Action<string> logInfo,
            Action<string> logWarning,
            Action<string> logError)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _logInfo = logInfo ?? (_ => { });
            _logWarning = logWarning ?? (_ => { });
            _logError = logError ?? (_ => { });
        }

        public void LogInfo(string message) { _logInfo(message); }
        public void LogWarning(string message) { _logWarning(message); }
        public void LogError(string message) { _logError(message); }
    }
}
