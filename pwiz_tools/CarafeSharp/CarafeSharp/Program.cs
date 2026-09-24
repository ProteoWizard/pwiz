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
    /// CarafeSharp command-line entry point. It takes Carafe's own command lines (see
    /// <see cref="CarafeCommandLine"/>): the stage-1 modes <c>-build_entrapment_fasta</c> and
    /// <c>-reconcile_manifest</c>, and library prediction from <c>-db</c>
    /// (<see cref="LibraryGenerator"/>), and training on Osprey's training exports (<see cref="ModelTrainer"/>).
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
                    case CarafeCommandMode.train:
                        new ModelTrainer(commandLine.TrainingSettings, Console.Out).Run();
                        Console.Out.WriteLine(@"Time used for training and spectral library generation: " + Seconds(stopwatch) + @" s.");
                        return 0;
                    case CarafeCommandMode.predict_library:
                        new LibraryGenerator(commandLine.LibrarySettings, Console.Out).Run();
                        Console.Out.WriteLine(@"Time used for spectral library generation: " + Seconds(stopwatch) + @" s.");
                        return 0;
                    default:
                        Console.Out.WriteLine(CarafeCommandLine.Usage);
                        return 0;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(@"ERROR: " + e.Message);
                var cause = e.GetBaseException();
                if (!ReferenceEquals(cause, e))
                    Console.Error.WriteLine(@"Caused by: " + cause.Message);
                return 1;
            }
        }

        private static string Seconds(Stopwatch stopwatch)
        {
            return stopwatch.Elapsed.TotalSeconds.ToString(@"F1", CultureInfo.InvariantCulture);
        }
    }
}
