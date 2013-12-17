using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace TestRunnerLib
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

        public List<Run> Runs;

        public void Save(string file)
        {
            var summary = new XElement("Summary");
            foreach (var run in Runs)
            {
                var runElement = new XElement("Run");
                runElement.Add(new XElement("Date", run.Date));
                runElement.Add(new XElement("Revision", run.Revision));
                runElement.Add(new XElement("RunMinutes", run.RunMinutes));
                runElement.Add(new XElement("TestsRun", run.TestsRun));
                runElement.Add(new XElement("Failures", run.Failures));
                runElement.Add(new XElement("Leaks", run.Leaks));
                runElement.Add(new XElement("ManagedMemory", run.ManagedMemory));
                runElement.Add(new XElement("TotalMemory", run.TotalMemory));
                summary.Add(runElement);
            }

            summary.Save(file);
        }

        public void Load(string file)
        {
            Runs = new List<Run>();
            if (!File.Exists(file))
                return;

            var summary = XElement.Load(file);
            foreach (var runElement in summary.Descendants("Run"))
            {
                var run = new Run();
                foreach (var element in runElement.Descendants())
                {
                    switch (element.Name.ToString().ToLower())
                    {
                        case "date":
                            run.Date = DateTime.Parse(element.Value);
                            break;
                        case "revision":
                            run.Revision = int.Parse(element.Value);
                            break;
                        case "runminutes":
                            run.RunMinutes = int.Parse(element.Value);
                            break;
                        case "testsrun":
                            run.TestsRun = int.Parse(element.Value);
                            break;
                        case "failures":
                            run.Failures = int.Parse(element.Value);
                            break;
                        case "leaks":
                            run.Leaks = int.Parse(element.Value);
                            break;
                        case "managedmemory":
                            run.ManagedMemory = int.Parse(element.Value);
                            break;
                        case "totalmemory":
                            run.TotalMemory = int.Parse(element.Value);
                            break;
                    }
                }

                Runs.Add(run);
            }
        }
    }
}
