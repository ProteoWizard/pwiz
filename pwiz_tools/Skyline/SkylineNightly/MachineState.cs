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
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Timers;
using Microsoft.Win32;

namespace SkylineNightly
{
    // ReSharper disable LocalizableElement
    /// <summary>
    /// What Windows can tell SkylineNightly about the machine underneath a run: the events that end
    /// runs, the installed .NET runtimes, and whether a restart is pending.
    /// </summary>
    public static class MachineState
    {
        private static readonly string[] SHARED_FRAMEWORKS = { "Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App" };

        private static readonly string[] REBOOT_PENDING_KEYS =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"
        };

        /// <summary>
        /// The System and Application events <see cref="RunInterruption"/> classifies, recorded since
        /// <paramref name="since"/>.
        /// </summary>
        public static IList<SystemEventRecord> ReadEvents(DateTime since)
        {
            var records = new List<SystemEventRecord>();
            ReadEvents(records, "System", RunInterruption.SYSTEM_EVENT_IDS, since);
            ReadEvents(records, "Application", RunInterruption.APPLICATION_EVENT_IDS, since);
            return records;
        }

        /// <summary>
        /// The shared .NET runtimes installed for all users, e.g. "Microsoft.NETCore.App 10.0.13,
        /// Microsoft.WindowsDesktop.App 10.0.13". SkylineTester and TestRunner run on these, so a
        /// change during a run means Microsoft Update serviced them underneath it.
        /// </summary>
        public static string DescribeInstalledRuntimes()
        {
            var sharedDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared");
            var versions = new List<string>();
            foreach (var framework in SHARED_FRAMEWORKS)
            {
                var frameworkDir = Path.Combine(sharedDir, framework);
                if (!Directory.Exists(frameworkDir))
                    continue;
                versions.AddRange(Directory.GetDirectories(frameworkDir)
                    .Select(Path.GetFileName)
                    .OrderBy(v => v, StringComparer.Ordinal)
                    .Select(v => framework + " " + v));
            }
            return versions.Count > 0 ? string.Join(", ", versions) : "none";
        }

        /// <summary>
        /// True when Windows Update or component servicing is waiting to restart the computer.
        /// </summary>
        public static bool IsRestartPending()
        {
            foreach (var keyName in REBOOT_PENDING_KEYS)
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyName);
                if (key != null)
                    return true;
            }
            return false;
        }

        private static void ReadEvents(List<SystemEventRecord> records, string logName, int[] ids, DateTime since)
        {
            var idFilter = string.Join(" or ", ids.Select(id => "EventID=" + id));
            var xpath = string.Format("*[System[({0}) and TimeCreated[@SystemTime>='{1}']]]", idFilter,
                since.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            using var reader = new EventLogReader(new EventLogQuery(logName, PathType.LogName, xpath));
            for (var record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
            {
                using (record)
                {
                    var properties = record.Properties
                        .Select(p => Convert.ToString(p.Value, CultureInfo.InvariantCulture))
                        .ToArray();
                    records.Add(new SystemEventRecord(record.TimeCreated ?? since, record.ProviderName, record.Id, properties));
                }
            }
        }
    }

    /// <summary>
    /// Watches the machine while a run is in progress and writes to the nightly log when the .NET
    /// runtimes change or a restart becomes pending, so failures after that point can be read as
    /// environmental rather than investigated as software bugs.
    /// </summary>
    public class MachineStateMonitor : IDisposable
    {
        private readonly Action<string> _log;
        private readonly Timer _timer;
        private readonly object _lock = new object();
        private string _runtimes;
        private bool _restartPendingLogged;

        public MachineStateMonitor(Action<string> log, double intervalMinutes = 5)
        {
            _log = log;
            _runtimes = MachineState.DescribeInstalledRuntimes();
            _restartPendingLogged = MachineState.IsRestartPending();
            if (_restartPendingLogged)
                _log("A Windows restart is already pending. This run may be interrupted by it.");
            _timer = new Timer(TimeSpan.FromMinutes(intervalMinutes).TotalMilliseconds);
            _timer.Elapsed += (sender, args) => Check();
            _timer.Start();
        }

        public void Check()
        {
            lock (_lock)
            {
                try
                {
                    var runtimes = MachineState.DescribeInstalledRuntimes();
                    if (!Equals(runtimes, _runtimes))
                    {
                        _log(string.Format(".NET runtimes changed during the run: {0} -> {1}. Failures in already running processes " +
                                           "after this point may be caused by the update rather than by the code under test.",
                            _runtimes, runtimes));
                        _runtimes = runtimes;
                    }
                    if (!_restartPendingLogged && MachineState.IsRestartPending())
                    {
                        _restartPendingLogged = true;
                        _log("Windows is now waiting to restart (updates installed). This run may be interrupted by it.");
                    }
                }
                catch (Exception e)
                {
                    _log("Failed to check machine state: " + e.Message);
                }
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
    // ReSharper restore LocalizableElement
}
