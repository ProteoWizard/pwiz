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
using System.IO;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Pipeline;

namespace pwiz.CarafeSharp.Tasks
{
    /// <summary>
    /// The prediction of a precursor table (<see cref="LibraryGenerator.PrecursorTablePath"/>) as a
    /// task under the library prediction's name, so a host runs it where it runs
    /// <see cref="PredictLibraryTask"/> and logs it the same. The host (Skyline's Build Library)
    /// predicts into a folder of its own per build and keeps no stamp, so the task declares no
    /// output and always predicts.
    /// </summary>
    public sealed class PredictPrecursorTableTask : CarafeTask
    {
        private readonly LibrarySettings _settings;
        private readonly string _tablePath;
        private readonly string _outputPath;

        /// <param name="settings">The models and the prediction settings.</param>
        /// <param name="tablePath">The precursor table.</param>
        /// <param name="outputPath">The .blib to write.</param>
        public PredictPrecursorTableTask(LibrarySettings settings, string tablePath, string outputPath)
        {
            _settings = settings;
            _tablePath = tablePath;
            _outputPath = outputPath;
        }

        public override string Name => PredictLibraryTask.TASK_NAME;

        /// <summary>The .blib the last <see cref="Run"/> wrote.</summary>
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
            // The library is the output itself; its folder is where the models are looked for when
            // no model folder is named (a saved model unpacks into a temporary folder of its own)
            _settings.OutputFile = _outputPath;
            _settings.OutputDirectory = Path.GetDirectoryName(Path.GetFullPath(_outputPath));
            new LibraryGenerator(_settings, Context(ctx).Output) { PrecursorTablePath = _tablePath }.Run();
            return true;
        }

        public override string Completion(TimeSpan elapsed)
        {
            return @"Time used for spectral library generation: " + Seconds(elapsed) + @" s.";
        }
    }
}
