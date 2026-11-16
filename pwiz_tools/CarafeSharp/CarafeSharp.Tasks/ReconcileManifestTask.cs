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
    /// Carafe's <c>-reconcile_manifest</c>: the pairing manifest reduced to the pair groups
    /// the predicted library holds (<see cref="PairingManifestReconciler"/>). Its output is a
    /// TSV, which carries no stamp, so it declares none and always runs.
    /// </summary>
    internal sealed class ReconcileManifestTask : CarafeTask
    {
        public const string TASK_NAME = @"ReconcileManifest";

        private readonly string _manifestIn;
        private readonly string _library;
        private readonly string _manifestOut;

        public ReconcileManifestTask(string manifestIn, string library, string manifestOut)
        {
            _manifestIn = manifestIn;
            _library = library;
            _manifestOut = manifestOut;
        }

        public override string Name => TASK_NAME;

        public override IEnumerable<string> Inputs(TaskContext ctx)
        {
            yield return _manifestIn;
            yield return _library;
        }

        public override string ValidityKey(TaskContext ctx)
        {
            return CarafeValidity.Key(@"reconcile", new { Output = _manifestOut }, Inputs(ctx));
        }

        public override bool Run(TaskContext ctx)
        {
            PairingManifestReconciler.Run(_manifestIn, _library, _manifestOut, Context(ctx).Output);
            return true;
        }

        public override string Completion(TimeSpan elapsed)
        {
            return @"Manifest reconciliation finished in " + Seconds(elapsed) + @" s.";
        }
    }
}
