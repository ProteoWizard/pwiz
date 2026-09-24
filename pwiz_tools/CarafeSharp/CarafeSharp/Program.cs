/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Diagnostics;
using System.Globalization;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp
{
    /// <summary>
    /// CarafeSharp command-line entry point. M1 takes Carafe's own stage-1 command lines
    /// (<c>-build_entrapment_fasta</c> and <c>-reconcile_manifest</c>, see
    /// <see cref="CarafeCommandLine"/>); the predict and train verbs arrive with milestones M3
    /// and M5.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            CarafeCommandLine commandLine;
            try
            {
                commandLine = CarafeCommandLine.Parse(args);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(CarafeCommandLine.Usage);
                return 1;
            }
            foreach (string warning in commandLine.Warnings)
                Console.Error.WriteLine(warning);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                switch (commandLine.Mode)
                {
                    case CarafeCommandMode.build_entrapment_fasta:
                        new EntrapmentFastaBuilder(commandLine.BuildSettings, Console.Out).Run();
                        Console.Out.WriteLine(@"Entrapment FASTA build finished in " + Seconds(stopwatch) + @" s.");
                        return 0;
                    case CarafeCommandMode.reconcile_manifest:
                        PairingManifestReconciler.Run(commandLine.ReconcileManifestIn, commandLine.ReconcileLibrary,
                            commandLine.ReconcileManifestOut, Console.Out);
                        Console.Out.WriteLine(@"Manifest reconciliation finished in " + Seconds(stopwatch) + @" s.");
                        return 0;
                    default:
                        Console.Out.WriteLine(CarafeCommandLine.Usage);
                        return 0;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(@"ERROR: " + e.Message);
                return 1;
            }
        }

        private static string Seconds(Stopwatch stopwatch)
        {
            return stopwatch.Elapsed.TotalSeconds.ToString(@"F1", CultureInfo.InvariantCulture);
        }
    }
}
