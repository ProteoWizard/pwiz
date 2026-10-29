/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Pipeline;

namespace pwiz.CarafeSharp.Tasks
{
    /// <summary>
    /// The prediction of a precursor table (<see cref="PrecursorTablePredictor"/>) as a task under
    /// the library prediction's name, so a host runs it where it runs
    /// <see cref="PredictLibraryTask"/> and logs it the same. Its TSV has no stamp slot, so the task
    /// declares no output and always predicts; the host keeps its working folder per build.
    /// </summary>
    public sealed class PredictPrecursorTableTask : CarafeTask
    {
        private readonly LibrarySettings _settings;
        private readonly string _tablePath;
        private readonly string _outputPath;

        /// <param name="settings">The models and the prediction settings.</param>
        /// <param name="tablePath">The precursor table.</param>
        /// <param name="outputPath">The TSV to write.</param>
        public PredictPrecursorTableTask(LibrarySettings settings, string tablePath, string outputPath)
        {
            _settings = settings;
            _tablePath = tablePath;
            _outputPath = outputPath;
        }

        public override string Name => PredictLibraryTask.TASK_NAME;

        /// <summary>The TSV the last <see cref="Run"/> wrote.</summary>
        public string OutputPath => _outputPath;

        public override IEnumerable<string> Inputs(TaskContext ctx)
        {
            yield return _tablePath;
            foreach (string input in PredictLibraryTask.Inputs(_settings))
                yield return input;
        }

        public override string ValidityKey(TaskContext ctx)
        {
            return CarafeValidity.Key(@"precursors", _settings, Inputs(ctx));
        }

        public override bool Run(TaskContext ctx)
        {
            new PrecursorTablePredictor(_settings, _tablePath, _outputPath, Context(ctx).Output).Run();
            return true;
        }

        public override string Completion(TimeSpan elapsed)
        {
            return @"Time used for spectral library generation: " + Seconds(elapsed) + @" s.";
        }
    }
}
