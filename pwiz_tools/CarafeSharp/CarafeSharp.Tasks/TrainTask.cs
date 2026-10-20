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
using pwiz.CarafeSharp.Training;
using pwiz.Common.Pipeline;

namespace pwiz.CarafeSharp.Tasks
{
    /// <summary>
    /// Carafe's training on Osprey's results (<see cref="ModelTrainer"/>): the fine-tuned
    /// model as a <c>.carafemodel</c>, and with <c>-db</c> the library predicted from it. Both
    /// carry the stamp, so a re-run on the same exports, models and settings is skipped.
    /// </summary>
    internal sealed class TrainTask : CarafeTask
    {
        public const string TASK_NAME = @"Train";

        private readonly TrainingSettings _settings;
        // The locator scans -i and reads each export's footer; once per task, not once per Inputs enumeration.
        private TrainingExportSelection _selection;

        public TrainTask(TrainingSettings settings)
        {
            _settings = settings;
        }

        public override string Name => TASK_NAME;

        public override IEnumerable<string> Inputs(TaskContext ctx)
        {
            // The exports the locator selects for -i and -ms, as the run will read them.
            _selection = _selection ?? TrainingExportLocator.Find(_settings.Identifications, _settings.MsFiles);
            foreach (string export in _selection.Exports)
                yield return export;
            yield return _settings.BaseModel;
            yield return _settings.Ms2Model;
            yield return _settings.PretrainedModels;
            if (_settings.Library != null)
            {
                foreach (string input in PredictLibraryTask.Inputs(_settings.Library))
                    yield return input;
            }
        }

        public override IEnumerable<string> Outputs(TaskContext ctx)
        {
            yield return Path.Combine(_settings.OutputDirectory, CarafeModelFile.DEFAULT_FILE_NAME);
            // The library goes into the training output folder (ModelTrainer sets that).
            string blib = _settings.Library != null ? PredictLibraryTask.BlibOutput(_settings.Library, _settings.OutputDirectory) : null;
            if (blib != null)
                yield return blib;
        }

        public override string ValidityKey(TaskContext ctx)
        {
            return CarafeValidity.Key(@"training", _settings, Inputs(ctx));
        }

        public override bool Run(TaskContext ctx)
        {
            // The stamp first, before the run touches a setting, so it carries the key the next
            // run's resume check computes.
            var stamp = OutputStamp(ctx);
            new ModelTrainer(_settings, Context(ctx).Output) { Stamp = stamp }.Run();
            return true;
        }

        public override string Completion(TimeSpan elapsed)
        {
            return @"Time used for training and spectral library generation: " + Seconds(elapsed) + @" s.";
        }
    }
}
