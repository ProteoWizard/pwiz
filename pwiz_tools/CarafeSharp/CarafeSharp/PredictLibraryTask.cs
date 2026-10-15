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
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Pipeline;

namespace pwiz.CarafeSharp
{
    /// <summary>
    /// Carafe's library prediction from <c>-db</c> (<see cref="LibraryGenerator"/>). The
    /// <c>.blib</c> it writes carries the stamp, so a re-run with the same FASTA, models and
    /// settings is skipped; a run that writes a TSV (alone or beside the blib) declares no output
    /// and always predicts.
    /// </summary>
    internal sealed class PredictLibraryTask : CarafeTask
    {
        public const string TASK_NAME = @"PredictLibrary";

        private readonly LibrarySettings _settings;

        public PredictLibraryTask(LibrarySettings settings)
        {
            _settings = settings;
        }

        public override string Name => TASK_NAME;

        public override IEnumerable<string> Inputs(TaskContext ctx)
        {
            return Inputs(_settings);
        }

        /// <summary>The files a library prediction with <paramref name="settings"/> reads, nulls left out by the key.</summary>
        public static IEnumerable<string> Inputs(LibrarySettings settings)
        {
            yield return settings.Database;
            yield return settings.ModelFile;
            yield return settings.ModelDirectory;
            yield return settings.PairingManifest;
            yield return settings.PretrainedModels;
        }

        public override IEnumerable<string> Outputs(TaskContext ctx)
        {
            string blib = BlibOutput(_settings, _settings.OutputDirectory);
            if (blib != null)
                yield return blib;
        }

        /// <summary>
        /// The <c>.blib</c> a prediction with <paramref name="settings"/> writes into
        /// <paramref name="outputDirectory"/> as its ONLY output, or null: a TSV has no stamp slot,
        /// so a format that writes both declares nothing - a run that committed the blib and then
        /// failed on the TSV must not be skipped as current.
        /// </summary>
        public static string BlibOutput(LibrarySettings settings, string outputDirectory)
        {
            var outputs = LibraryOutputs.FromFormat(settings.LibraryFormat, settings.Fast);
            return outputs.WritesBlib && !outputs.WritesTsv
                ? Path.Combine(outputDirectory, BlibLibraryWriter.FILE_NAME)
                : null;
        }

        public override string ValidityKey(TaskContext ctx)
        {
            return CarafeValidity.Key(@"library", _settings, Inputs(ctx));
        }

        public override bool Run(TaskContext ctx)
        {
            // The stamp first, before the run touches a setting, so it carries the key the next
            // run's resume check computes.
            var stamp = OutputStamp(ctx);
            new LibraryGenerator(_settings, Context(ctx).Output) { Stamp = stamp }.Run();
            return true;
        }

        public override string Completion(TimeSpan elapsed)
        {
            return @"Time used for spectral library generation: " + Seconds(elapsed) + @" s.";
        }
    }
}
