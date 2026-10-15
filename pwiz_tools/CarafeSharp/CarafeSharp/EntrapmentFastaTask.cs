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

namespace pwiz.CarafeSharp
{
    /// <summary>
    /// Carafe's <c>-build_entrapment_fasta</c>: the protein FASTA digested into target, decoy
    /// and entrapment peptides, with the FDRBench pairing manifest
    /// (<see cref="EntrapmentFastaBuilder"/>). Its outputs are a FASTA and a TSV, formats with
    /// no slot for a stamp, so it declares none and always runs; it takes seconds.
    /// </summary>
    internal sealed class EntrapmentFastaTask : CarafeTask
    {
        public const string TASK_NAME = @"BuildEntrapmentFasta";

        private readonly EntrapmentFastaSettings _settings;

        public EntrapmentFastaTask(EntrapmentFastaSettings settings)
        {
            _settings = settings;
        }

        public override string Name => TASK_NAME;

        public override IEnumerable<string> Inputs(TaskContext ctx)
        {
            yield return _settings.InputFasta;
            if (_settings.EntrapmentSourceFasta != null)
                yield return _settings.EntrapmentSourceFasta;
        }

        public override string ValidityKey(TaskContext ctx)
        {
            return CarafeValidity.Key(@"build", _settings, Inputs(ctx));
        }

        public override bool Run(TaskContext ctx)
        {
            new EntrapmentFastaBuilder(_settings, Context(ctx).Output).Run();
            return true;
        }

        public override string Completion(TimeSpan elapsed)
        {
            return @"Entrapment FASTA build finished in " + Seconds(elapsed) + @" s.";
        }
    }
}
