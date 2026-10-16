/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.IO;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;

namespace pwiz.CarafeSharp
{
    /// <summary>
    /// CarafeSharp command-line entry point. It takes Carafe's own command lines (see
    /// <see cref="CarafeCommandLine"/>): the stage-1 modes <c>-build_entrapment_fasta</c> and
    /// <c>-reconcile_manifest</c>, library prediction from <c>-db</c> and training on Osprey's
    /// training exports, each a <see cref="CarafeTask"/> run through the shared
    /// <see cref="PipelineDriver"/>, which skips a task whose stamped outputs are current.
    /// <para>
    /// Output goes through the shared <see cref="CommandStatusWriter"/>, as Osprey's and
    /// SkylineCmd's does: progress on standard output, errors and warnings on standard error (as
    /// Carafe), each line stamped by <c>-timestamp</c> / <c>-memstamp</c>, and both streams
    /// redirected to one file by <c>-log_file</c>. An error line starts with
    /// <see cref="CommandStatusWriter.ERROR_MESSAGE_HINT"/>, which is how a log reader (the
    /// workflow scripts, a regression gate) recognizes one, and the exit code is made to agree
    /// with the log (<see cref="ReconcileExitCode"/>).
    /// </para>
    /// </summary>
    internal static class Program
    {
        // Exit codes, as Skyline's and Osprey's command lines define them.
        internal const int EXIT_CODE_SUCCESS = 0;
        internal const int EXIT_CODE_FAILURE_TO_START = 1;
        internal const int EXIT_CODE_RAN_WITH_ERRORS = 2;

        private static readonly string ERROR_PREFIX = CommandStatusWriter.ERROR_MESSAGE_HINT + @" ";

        private static int Main(string[] args)
        {
            return Run(args, Console.Out, Console.Error);
        }

        /// <summary>The command line's work, with progress to <paramref name="output"/> and errors to <paramref name="error"/>; the exit code.</summary>
        internal static int Run(string[] args, TextWriter output, TextWriter error)
        {
            var consoleOut = output as CommandStatusWriter ?? new CommandStatusWriter(output);
            var consoleError = error as CommandStatusWriter ?? new CommandStatusWriter(error);
            CarafeCommandLine commandLine;
            try
            {
                commandLine = CarafeCommandLine.Parse(args);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
            {
                // A usage error: the message is the diagnosis, then where the options are.
                consoleError.WriteLine(ERROR_PREFIX + e.Message);
                consoleError.WriteLine(CarafeCommandLine.UsageHint);
                return ReconcileExitCode(EXIT_CODE_FAILURE_TO_START, consoleError.IsErrorReported);
            }

            // Stamps and the log file apply once the command line has parsed, so a usage error
            // stays on the console and creates no file. With a log file, every line goes to it.
            consoleOut.IsTimeStamped = consoleError.IsTimeStamped = commandLine.TimeStamp;
            consoleOut.IsMemStamped = consoleError.IsMemStamped = commandLine.MemStamp;
            var log = consoleOut;
            var errorLog = consoleError;
            CommandStatusWriter logFile = null;
            if (commandLine.LogFile != null)
            {
                try
                {
                    logFile = new CommandStatusWriter(new StreamWriter(commandLine.LogFile))
                    {
                        IsTimeStamped = commandLine.TimeStamp,
                        IsMemStamped = commandLine.MemStamp
                    };
                }
                catch (Exception e)
                {
                    consoleError.WriteLine(ERROR_PREFIX + string.Format(@"Failed to open log file {0}: {1}", commandLine.LogFile, e.Message));
                    return ReconcileExitCode(EXIT_CODE_FAILURE_TO_START, consoleError.IsErrorReported);
                }
                log = errorLog = logFile;
            }

            // The shared progress reporter writes to the process-wide seam; point it at the same
            // writer the stages log to, so percent lines land between their lines, stamped alike.
            var savedOut = CommandOutput.Out;
            CommandOutput.Out = log;
            try
            {
                return ReconcileExitCode(RunMode(commandLine, log, errorLog), errorLog.IsErrorReported || log.IsErrorReported);
            }
            finally
            {
                CommandOutput.Out = savedOut;
                logFile?.Dispose();
            }
        }

        private static int RunMode(CarafeCommandLine commandLine, TextWriter output, TextWriter error)
        {
            foreach (string warning in commandLine.Warnings)
                error.WriteLine(warning);

            try
            {
                switch (commandLine.Mode)
                {
                    case CarafeCommandMode.model_info:
                        output.Write(CarafeModelFile.Open(commandLine.ModelInfoPath).FormatInfo());
                        return EXIT_CODE_SUCCESS;
                    case CarafeCommandMode.help:
                        CarafeCommandLine.PrintUsage(commandLine.HelpFormat, output);
                        return EXIT_CODE_SUCCESS;
                    default:
                        // One task per mode, run through the shared driver: a task whose outputs
                        // carry a current stamp is skipped, and the rest run as Carafe would.
                        var pipeline = new PipelineTask[] { CarafeTask.Create(commandLine) };
                        var ctx = new CarafeContext(commandLine, pipeline, output, error);
                        return new CarafeDriver().Run(pipeline, ctx);
                }
            }
            catch (Exception e)
            {
                error.WriteLine(ERROR_PREFIX + DescribeFailure(e));
                return EXIT_CODE_FAILURE_TO_START;
            }
        }

        /// <summary>
        /// The text for an exception that reached the top-level sink, following Skyline and
        /// Osprey: an exception whose message is written for the user is reported as that
        /// message, with its root cause on a second line when it has one; a programming defect
        /// is reported whole, type and stack included, so it can be fixed. Besides
        /// <see cref="CommonExceptionUtil.IsProgrammingDefect"/>'s user-actionable types,
        /// CarafeSharp's settings validation reports bad input as <see cref="ArgumentException"/>
        /// and an unported Carafe option as <see cref="NotSupportedException"/>, as Carafe's Java
        /// did, so those two are messages too - the exact types: the BCL's derived
        /// <see cref="ArgumentNullException"/> and <see cref="ArgumentOutOfRangeException"/> are defects.
        /// </summary>
        internal static string DescribeFailure(Exception e)
        {
            var reported = CommonExceptionUtil.UnwrapUserException(e);
            bool userError = !CommonExceptionUtil.IsProgrammingDefect(reported) ||
                             reported.GetType() == typeof(ArgumentException) || reported is NotSupportedException;
            if (userError && !string.IsNullOrEmpty(reported.Message))
            {
                var cause = reported.GetBaseException();
                if (ReferenceEquals(cause, reported))
                    return reported.Message;
                return reported.Message + Environment.NewLine + @"Caused by: " + cause.Message;
            }
            return @"Fatal error: " + e;
        }

        /// <summary>
        /// Make the exit code and the log agree, as Skyline's and Osprey's command lines do: an
        /// error line under a success code becomes <see cref="EXIT_CODE_RAN_WITH_ERRORS"/>. (A
        /// failure code is always written with its error line above, so the other direction
        /// needs no repair.)
        /// </summary>
        internal static int ReconcileExitCode(int exitCode, bool errorReported)
        {
            if (exitCode == EXIT_CODE_SUCCESS && errorReported)
                return EXIT_CODE_RAN_WITH_ERRORS;
            return exitCode;
        }
    }
}
