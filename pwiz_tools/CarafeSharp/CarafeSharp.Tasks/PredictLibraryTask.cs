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
    /// Carafe's library prediction from <c>-db</c> (<see cref="LibraryGenerator"/>). The
    /// <c>.blib</c> it writes carries the stamp, so a re-run with the same FASTA, models and
    /// settings is skipped; a run that writes a TSV (alone or beside the blib) declares no output
    /// and always predicts.
    /// </summary>
    public sealed class PredictLibraryTask : CarafeTask
    {
        public const string TASK_NAME = @"PredictLibrary";

        private readonly LibrarySettings _settings;
        private readonly EntrapmentFastaSettings _digest;

        public PredictLibraryTask(LibrarySettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// A prediction over a digest made in memory from the protein FASTA
        /// <paramref name="digest"/> names (its decoys and entrapment included), which runs only
        /// when the library is not current: the digest's settings and its foreign FASTA join the
        /// key, so a changed entrapment ratio or seed predicts again.
        /// </summary>
        public PredictLibraryTask(LibrarySettings settings, EntrapmentFastaSettings digest)
            : this(settings)
        {
            _digest = digest;
        }

        public override string Name => TASK_NAME;

        /// <summary>The peptides the last <see cref="Run"/> predicted from an in-memory digest, or null.</summary>
        public LibraryPeptides Peptides { get; private set; }

        public override IEnumerable<string> Inputs(TaskContext ctx)
        {
            foreach (string input in Inputs(_settings))
                yield return input;
            if (_digest?.EntrapmentSourceFasta != null)
                yield return _digest.EntrapmentSourceFasta;
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
                ? LibraryGenerator.BlibOutput(settings, outputDirectory)
                : null;
        }

        public override string ValidityKey(TaskContext ctx)
        {
            string key = CarafeValidity.Key(@"library", _settings, Inputs(ctx));
            return _digest == null ? key : key + @";" + CarafeValidity.Key(@"digest", _digest, Array.Empty<string>());
        }

        public override bool Run(TaskContext ctx)
        {
            // The stamp first, before the run touches a setting, so it carries the key the next
            // run's resume check computes.
            var stamp = OutputStamp(ctx);
            var output = Context(ctx).Output;
            var generator = new LibraryGenerator(_settings, output) { Stamp = stamp };
            if (_digest != null)
                generator.PeptideSource = () => Peptides = LibraryPeptides.Build(_digest, output);
            generator.Run();
            return true;
        }

        public override string Completion(TimeSpan elapsed)
        {
            return @"Time used for spectral library generation: " + Seconds(elapsed) + @" s.";
        }
    }
}
