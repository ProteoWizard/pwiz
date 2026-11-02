/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SkylineNightly
{
    // ReSharper disable LocalizableElement
    public enum InterruptionCause
    {
        windows_update_restart,
        restart,
        unexpected_shutdown,
        crash,
        still_running,
        unknown
    }

    /// <summary>
    /// One Windows event log record, reduced to the parts <see cref="RunInterruption"/> reads. The
    /// properties are the event's insertion strings, which do not depend on the display language the
    /// way the formatted message does. Kept free of System.Diagnostics.Eventing so the classification
    /// can be tested from synthetic records.
    /// </summary>
    public class SystemEventRecord
    {
        public SystemEventRecord(DateTime time, string provider, int id, params string[] properties)
        {
            Time = time;
            Provider = provider;
            Id = id;
            Properties = properties ?? Array.Empty<string>();
        }

        public DateTime Time { get; }
        public string Provider { get; }
        public int Id { get; }
        public IList<string> Properties { get; }

        public string GetProperty(int index)
        {
            return index < Properties.Count ? Properties[index] ?? string.Empty : string.Empty;
        }
    }

    /// <summary>
    /// Explains why a nightly run ended without SkylineNightly writing its closing line, from the
    /// Windows events recorded since the run started. A run lost to a restart or a .NET servicing
    /// update otherwise looks exactly like a software crash: results are missing and nothing on the
    /// machine says why.
    /// </summary>
    public class RunInterruption
    {
        public const string PROVIDER_USER32 = "User32";
        public const string PROVIDER_EVENT_LOG = "EventLog";
        public const string PROVIDER_KERNEL_POWER = "Microsoft-Windows-Kernel-Power";
        public const string PROVIDER_WINDOWS_UPDATE = "Microsoft-Windows-WindowsUpdateClient";
        public const string PROVIDER_DOTNET_RUNTIME = ".NET Runtime";
        public const string PROVIDER_APPLICATION_ERROR = "Application Error";

        public const int ID_RESTART_INITIATED = 1074;
        public const int ID_UNEXPECTED_SHUTDOWN = 6008;
        public const int ID_POWER_LOSS = 41;
        public const int ID_UPDATE_INSTALLED = 19;
        public const int ID_DOTNET_CRASH = 1026;
        public const int ID_APPLICATION_CRASH = 1000;

        public static readonly int[] SYSTEM_EVENT_IDS = { ID_RESTART_INITIATED, ID_UNEXPECTED_SHUTDOWN, ID_POWER_LOSS, ID_UPDATE_INSTALLED };
        public static readonly int[] APPLICATION_EVENT_IDS = { ID_DOTNET_CRASH, ID_APPLICATION_CRASH };

        /// <summary>Starts the nightly log line that records the installed .NET runtimes.</summary>
        public const string RUNTIMES_PREFIX = "Installed .NET runtimes: ";

        private const string NIGHTLY_EXE = "SkylineNightly.exe";

        /// <summary>
        /// Processes Windows Update restarts the computer from (1074 names the initiating process).
        /// </summary>
        private static readonly string[] UPDATE_RESTART_PROCESSES =
            { "TrustedInstaller.exe", "MoUsoCoreWorker.exe", "UsoClient.exe", "wuauclt.exe", "musnotification.exe" };

        /// <summary>
        /// Events at most this long before the log's last write still count as its end: the log is
        /// written on a timer, so its last write can trail the moment the process stopped.
        /// </summary>
        private static readonly TimeSpan CAUSE_SLACK = TimeSpan.FromMinutes(1);

        // Defender definition updates arrive several times a day, and Store apps by Store product id
        // ("9PLM9XGG6VKS-OpenAI.Codex"); neither restarts anything and both would bury what does.
        private static readonly Regex ROUTINE_UPDATE = new Regex(@"Security Intelligence Update|^[0-9A-Z]{12}-", RegexOptions.Compiled);

        /// <summary>
        /// Classifies an interruption.
        /// </summary>
        /// <param name="events">Events recorded since the interrupted run started, times in UTC</param>
        /// <param name="lastActivity">When the interrupted run last wrote its log, in UTC</param>
        /// <param name="stillRunning">An earlier SkylineNightly was still running when this one started</param>
        /// <param name="runtimesAtStart">The interrupted run's <see cref="RUNTIMES_PREFIX"/> line value, or null</param>
        /// <param name="runtimesNow">The installed .NET runtimes now</param>
        public static RunInterruption Diagnose(IEnumerable<SystemEventRecord> events, DateTime lastActivity,
            bool stillRunning, string runtimesAtStart, string runtimesNow)
        {
            var eventList = events.OrderBy(e => e.Time).ToList();
            var causeEvent = eventList.FirstOrDefault(e => e.Time >= lastActivity - CAUSE_SLACK && GetCause(e).HasValue);
            // Without a cause event the run's end is unknown; its log's last write is the latest moment
            // it is known to have been alive, so later updates are not blamed on it
            var cutoff = causeEvent?.Time ?? lastActivity;
            var updates = eventList.Where(e => e.Time <= cutoff && IsReportableUpdate(e))
                .Select(e => e.GetProperty(0)).Distinct().ToList();
            var runtimeChange = !string.IsNullOrEmpty(runtimesAtStart) && !string.IsNullOrEmpty(runtimesNow) &&
                                !Equals(runtimesAtStart, runtimesNow)
                ? string.Format("{0} -> {1}", runtimesAtStart, runtimesNow)
                : null;

            if (causeEvent != null)
                return new RunInterruption(GetCause(causeEvent).Value, causeEvent.Time, Describe(causeEvent), updates, runtimeChange);
            if (stillRunning)
            {
                return new RunInterruption(InterruptionCause.still_running, null,
                    "An earlier SkylineNightly was still running and was stopped by this run (a hang, or an open error or debugger dialog)",
                    updates, runtimeChange);
            }
            return new RunInterruption(InterruptionCause.unknown, null,
                "No restart, shutdown or SkylineNightly crash was recorded in the Windows event logs",
                updates, runtimeChange);
        }

        /// <summary>
        /// The installed runtimes recorded in a nightly log by its <see cref="RUNTIMES_PREFIX"/> line,
        /// or null for a log written before the line existed.
        /// </summary>
        public static string ParseRuntimes(string log)
        {
            var match = Regex.Match(log, Regex.Escape(RUNTIMES_PREFIX) + @"(.*?)\r?\n");
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        private RunInterruption(InterruptionCause cause, DateTime? time, string detail, IList<string> updates,
            string runtimeChange)
        {
            Cause = cause;
            Time = time;
            Detail = detail;
            UpdatesInstalled = updates;
            RuntimeChange = runtimeChange;
        }

        public InterruptionCause Cause { get; }
        public DateTime? Time { get; }
        public string Detail { get; }
        public IList<string> UpdatesInstalled { get; }
        public string RuntimeChange { get; }

        /// <summary>A few words for an alert subject.</summary>
        public string ShortDescription
        {
            get
            {
                switch (Cause)
                {
                    case InterruptionCause.windows_update_restart:
                        return "Windows Update restart";
                    case InterruptionCause.restart:
                        return "restart";
                    case InterruptionCause.unexpected_shutdown:
                        return "unexpected shutdown";
                    case InterruptionCause.crash:
                        return "SkylineNightly crash";
                    case InterruptionCause.still_running:
                        return "still running";
                    default:
                        return "cause unknown";
                }
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Cause: ").Append(ShortDescription);
            if (Time.HasValue)
                sb.Append(" at ").Append(Time.Value.ToLocalTime().ToString("g"));
            sb.AppendLine();
            sb.AppendLine(Detail);
            if (RuntimeChange != null)
            {
                sb.Append(".NET runtimes changed since the run started: ").AppendLine(RuntimeChange);
                sb.AppendLine("A process started before such a change can fail the first time it loads a framework assembly from the removed version.");
            }
            if (UpdatesInstalled.Count > 0)
            {
                sb.AppendLine("Updates installed during the run:");
                foreach (var update in UpdatesInstalled)
                    sb.Append("  ").AppendLine(update);
            }
            return sb.ToString();
        }

        private static InterruptionCause? GetCause(SystemEventRecord e)
        {
            if (e.Id == ID_RESTART_INITIATED && Equals(e.Provider, PROVIDER_USER32))
            {
                var process = Path.GetFileName(GetRestartProcessPath(e));
                return UPDATE_RESTART_PROCESSES.Contains(process, StringComparer.OrdinalIgnoreCase)
                    ? InterruptionCause.windows_update_restart
                    : InterruptionCause.restart;
            }
            if ((e.Id == ID_UNEXPECTED_SHUTDOWN && Equals(e.Provider, PROVIDER_EVENT_LOG)) ||
                (e.Id == ID_POWER_LOSS && Equals(e.Provider, PROVIDER_KERNEL_POWER)))
            {
                return InterruptionCause.unexpected_shutdown;
            }
            if (IsNightlyCrash(e))
                return InterruptionCause.crash;
            return null;
        }

        /// <summary>
        /// The path of the process that initiated a restart. The insertion string can carry the
        /// computer name after it, as in "C:\WINDOWS\servicing\TrustedInstaller.exe (MACHINE)".
        /// </summary>
        private static string GetRestartProcessPath(SystemEventRecord e)
        {
            return Regex.Replace(e.GetProperty(0), @"\s*\([^()]*\)\s*$", string.Empty);
        }

        private static bool IsNightlyCrash(SystemEventRecord e)
        {
            if (e.Id == ID_DOTNET_CRASH && Equals(e.Provider, PROVIDER_DOTNET_RUNTIME))
                return e.GetProperty(0).IndexOf(NIGHTLY_EXE, StringComparison.OrdinalIgnoreCase) >= 0;
            if (e.Id == ID_APPLICATION_CRASH && Equals(e.Provider, PROVIDER_APPLICATION_ERROR))
                return string.Equals(e.GetProperty(0), NIGHTLY_EXE, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private static bool IsReportableUpdate(SystemEventRecord e)
        {
            return e.Id == ID_UPDATE_INSTALLED && Equals(e.Provider, PROVIDER_WINDOWS_UPDATE) &&
                   !ROUTINE_UPDATE.IsMatch(e.GetProperty(0));
        }

        private static string Describe(SystemEventRecord e)
        {
            switch (e.Id)
            {
                case ID_RESTART_INITIATED:
                    // Insertion strings: process, computer, reason, reason code, shutdown type, comment, user
                    return string.Format("{0} initiated a {1} on behalf of {2}: {3}", GetRestartProcessPath(e), e.GetProperty(4),
                        e.GetProperty(6), e.GetProperty(2));
                case ID_UNEXPECTED_SHUTDOWN:
                case ID_POWER_LOSS:
                    return "Windows stopped without a clean shutdown (power loss, hard reset or system crash)";
                case ID_DOTNET_CRASH:
                    // One insertion string: "Application: ...\nCoreCLR Version: ...\n...\nException Info: ..."
                    var lines = e.GetProperty(0).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var exceptionLine = lines.FirstOrDefault(l => l.StartsWith("Exception Info:")) ??
                                        lines.FirstOrDefault(l => l.Contains("Exception"));
                    return string.Format("{0} terminated: {1}", NIGHTLY_EXE, exceptionLine ?? string.Join(" ", lines.Take(3)));
                default:
                    // Application Error insertion strings: application, version, timestamp, module, module
                    // version, module timestamp, exception code
                    return string.Format("{0} crashed in {1} with exception code 0x{2}", NIGHTLY_EXE, e.GetProperty(3),
                        e.GetProperty(6));
            }
        }
    }
    // ReSharper restore LocalizableElement
}
