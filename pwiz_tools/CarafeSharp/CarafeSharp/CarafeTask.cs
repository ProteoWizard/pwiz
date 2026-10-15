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
using System.Globalization;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Pipeline;

namespace pwiz.CarafeSharp
{
    /// <summary>
    /// A CarafeSharp stage on the shared <see cref="PipelineTask"/> contract: Carafe's modes,
    /// each wrapping the class that does the work, with its inputs and outputs declared and its
    /// outputs stamped so a re-run with current outputs is skipped. Nothing passes between
    /// CarafeSharp's stages in memory - each reads the files the one before wrote - so
    /// <see cref="Rehydrate"/> has nothing to load.
    /// </summary>
    internal abstract class CarafeTask : PipelineTask
    {
        /// <summary>The task for <paramref name="commandLine"/>'s mode, or null for a mode that is not a task (help, model info).</summary>
        public static CarafeTask Create(CarafeCommandLine commandLine)
        {
            switch (commandLine.Mode)
            {
                case CarafeCommandMode.build_entrapment_fasta:
                    return new EntrapmentFastaTask(commandLine.BuildSettings);
                case CarafeCommandMode.reconcile_manifest:
                    return new ReconcileManifestTask(commandLine.ReconcileManifestIn, commandLine.ReconcileLibrary, commandLine.ReconcileManifestOut);
                case CarafeCommandMode.predict_library:
                    return new PredictLibraryTask(commandLine.LibrarySettings);
                case CarafeCommandMode.train:
                    return new TrainTask(commandLine.TrainingSettings);
                default:
                    return null;
            }
        }

        public sealed override bool Rehydrate(TaskContext ctx)
        {
            return true;
        }

        /// <summary>The line Carafe prints when this mode finishes, with <paramref name="elapsed"/>.</summary>
        public abstract string Completion(TimeSpan elapsed);

        /// <summary>The context as <see cref="CarafeContext"/>, which every CarafeSharp task runs against.</summary>
        protected static CarafeContext Context(TaskContext ctx)
        {
            return (CarafeContext)ctx;
        }

        protected static string Seconds(TimeSpan elapsed)
        {
            return elapsed.TotalSeconds.ToString(@"F1", CultureInfo.InvariantCulture);
        }
    }
}
