/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SkylineTester
{
    public class Summary
    {
        public class Run
        {
            public DateTime Date { get; set; }
            public string Revision { get; set; }
            public int RunMinutes { get; set; }
            public int TestsRun { get; set; }
            public int Failures { get; set; }
            public int Leaks { get; set; }
            public int ManagedMemory { get; set; }
            public int CommittedMemory { get; set; }
            public int TotalMemory { get; set; }
            public int UserHandles { get; set; }
            public int GdiHandles { get; set; }
        }

        public static Run ParseRunFromStatusLine(string statusLine)
        {
            // Deal with 
            // "[14:38] 2.2 AgilentMseChromatogramTestAsSmallMolecules (zh) 0 failures, 1.25/[4.51/]51.5 MB, 20/40 handles, 0 sec."
            // or
            // "[14:38] 2.2 AgilentMseChromatogramTestAsSmallMolecules (zh) (RunSmallMoleculeTestVersions=False, skipping.) 0 failures, 1.25/[4.51/]51.5 MB, 20/40 handles, 0 sec."
            var line = Regex.Replace(statusLine, @"\s+", " ").Trim();
            line = line.Replace(pwiz.SkylineTestUtil.AbstractUnitTest.MSG_SKIPPING_SMALLMOLECULE_TEST_VERSION, " ");
            line = line.Replace(pwiz.SkylineTestUtil.AbstractUnitTest.MSG_SKIPPING_SLOW_RESHARPER_ANALYSIS_TEST, " ");
            var parts = line.Split(' ');

            var run = new Run { Date = DateTime.Now };
            const int failuresIndex = 4;
            const int memoryIndex = 6;
            const int handlesIndex = 8;
            int failures;
            if (failuresIndex < parts.Length && int.TryParse(parts[failuresIndex], out failures))
                run.Failures = failures;

            int managedMemory, totalMemory, committedMemory;
            if (memoryIndex < parts.Length && ParseMemoryPart(parts[memoryIndex], out managedMemory, out totalMemory, out committedMemory))
            {
                run.ManagedMemory = managedMemory;
                run.CommittedMemory = committedMemory;
                run.TotalMemory = totalMemory;
            }
            int userHandles, gdiHandles, unexpected;
            if (handlesIndex < parts.Length && ParseMemoryPart(parts[handlesIndex], out userHandles, out gdiHandles, out unexpected))
            {
                run.UserHandles = userHandles;
                run.GdiHandles = gdiHandles;
            }
            return run;
        }

        private static bool ParseMemoryPart(string memoryPart, out int firstValue, out int secondValue, out int optionalValue)
        {
            var memoryParts = memoryPart.Split('/');
            double firstPart, secondPart, lastPart;
            if (memoryParts.Length >= 2 &&
                double.TryParse(memoryParts[0], out firstPart) &&
                double.TryParse(memoryParts[1], out secondPart) &&
                double.TryParse(memoryParts[memoryParts.Length - 1], out lastPart))
            {
                firstValue = (int) firstPart;
                optionalValue = memoryParts.Length > 2 ? (int) secondPart : 0;
                secondValue = (int) lastPart;
                return true;
            }

            firstValue = secondValue = optionalValue = 0;
            return false;
        }

        public List<Run> Runs { get; private set; }
        public string SummaryFile { get; private set; }

        public Summary(string summaryFile)
        {
            SummaryFile = summaryFile;
            Load();
        }

        public string GetLogFile(Run run)
        {
            var logFile = "{0}_{1}-{2:D2}-{3:D2}_{4:D2}-{5:D2}-{6:D2}.log".With(
                Environment.MachineName,
                run.Date.Year, run.Date.Month, run.Date.Day, 
                run.Date.Hour, run.Date.Minute, run.Date.Second);
            return Path.Combine(Path.GetDirectoryName(SummaryFile) ?? "", logFile);
        }

        public void Save()
        {
            var summary = new XElement("Summary");
            foreach (var run in Runs)
            {
                var runElement = new XElement("Run");
                runElement.Add(new XElement("Date", run.Date.ToString("G", CultureInfo.InvariantCulture)));
                runElement.Add(new XElement("Revision", run.Revision));
                runElement.Add(new XElement("RunMinutes", run.RunMinutes));
                runElement.Add(new XElement("TestsRun", run.TestsRun));
                runElement.Add(new XElement("Failures", run.Failures));
                runElement.Add(new XElement("Leaks", run.Leaks));
                runElement.Add(new XElement("ManagedMemory", run.ManagedMemory));
                runElement.Add(new XElement("CommittedMemory", run.CommittedMemory));
                runElement.Add(new XElement("TotalMemory", run.TotalMemory));
                runElement.Add(new XElement("UserHandles", run.UserHandles));
                runElement.Add(new XElement("GdiHandles", run.GdiHandles));
                summary.Add(runElement);
            }

            summary.Save(SummaryFile);
        }

        public void Load()
        {
            Runs = new List<Run>();
            if (!File.Exists(SummaryFile))
                return;

            XElement summary;
            try
            {
                summary = XElement.Load(SummaryFile);   
            }
            catch (Exception)
            {
                return;
            }
            foreach (var runElement in summary.Descendants("Run"))
            {
                var run = new Run();
                foreach (var element in runElement.Descendants())
                {
                    switch (element.Name.ToString().ToLowerInvariant())
                    {
                        case "date":
                            run.Date = DateTime.Parse(element.Value, CultureInfo.InvariantCulture);
                            break;
                        case "revision":
                            run.Revision = element.Value;
                            break;
                        case "runminutes":
                            run.RunMinutes = int.Parse(element.Value, CultureInfo.InvariantCulture);
                            break;
                        case "testsrun":
                            run.TestsRun = int.Parse(element.Value, CultureInfo.InvariantCulture);
                            break;
                        case "failures":
                            run.Failures = int.Parse(element.Value, CultureInfo.InvariantCulture);
                            break;
                        case "leaks":
                            run.Leaks = int.Parse(element.Value, CultureInfo.InvariantCulture);
                            break;
                        case "managedmemory":
                            run.ManagedMemory = int.Parse(element.Value, CultureInfo.InvariantCulture);
                            break;
                        case "committedmemory":
                            run.CommittedMemory = int.Parse(element.Value, CultureInfo.InvariantCulture);
                            break;
                        case "totalmemory":
                            run.TotalMemory = int.Parse(element.Value, CultureInfo.InvariantCulture);
                            break;
                        case "userhandles":
                            run.UserHandles = int.Parse(element.Value, CultureInfo.InvariantCulture);
                            break;
                        case "gdihandles":
                            run.GdiHandles = int.Parse(element.Value, CultureInfo.InvariantCulture);
                            break;
                    }
                }

                Runs.Add(run);
            }

            bool runsRemoved = false;

            // Remove runs without a log file.
            for (int i = Runs.Count - 1; i >= 0; i--)
            {
                if (!File.Exists(GetLogFile(Runs[i])))
                {
                    Runs.RemoveAt(i);
                    runsRemoved = true;
                }
            }

            // Show max of 90 runs (3 months or so, or 45 days for double duty machines).
            var MAX_STORED_SUMMARIES = 90;
            if (Runs.Count > MAX_STORED_SUMMARIES)
            {
                Runs.RemoveRange(0, Runs.Count - MAX_STORED_SUMMARIES);
                runsRemoved = true;
            }

            if (runsRemoved)
                Save();
        }
    }
}
