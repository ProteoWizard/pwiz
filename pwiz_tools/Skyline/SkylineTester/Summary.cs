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
using System.Xml.Linq;

namespace SkylineTester
{
    public class Summary
    {
        public class Run
        {
            public DateTime Date;
            public int Revision;
            public int RunMinutes;
            public int TestsRun;
            public int Failures;
            public int Leaks;
            public int ManagedMemory;
            public int TotalMemory;
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
                runElement.Add(new XElement("TotalMemory", run.TotalMemory));
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
                            run.Revision = int.Parse(element.Value, CultureInfo.InvariantCulture);
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
                        case "totalmemory":
                            run.TotalMemory = int.Parse(element.Value, CultureInfo.InvariantCulture);
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

            // Show max of 30 runs.
            if (Runs.Count > 30)
            {
                Runs.RemoveRange(0, Runs.Count - 30);
                runsRemoved = true;
            }

            if (runsRemoved)
                Save();
        }
    }
}
