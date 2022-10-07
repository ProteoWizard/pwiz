using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using SkylineTool;

namespace TestCommandLineInteractiveTool
{
    public class MonitorSelection : AbstractCommand
    {
        private string _originalDocumentPath;
        private static int _skylineProcessId;
        private static object _changeEventObject = new object();
        private static string _selectedPeptideLocator;
        private static string _selectedTransitionLocator;
        private static string _selectedReplicateLocator;

        public MonitorSelection(SkylineToolClient toolClient) : base(toolClient)
        {
        }

        public override void RunCommand()
        {
            try
            {
                _skylineProcessId = SkylineToolClient.GetProcessId();
                SkylineToolClient.DocumentChanged += OnDocumentChanged;
                SkylineToolClient.SelectionChanged += ToolClientOnSelectionChanged;
                ProcessChangeEventsUntilDocumentPathChanges();
            }
            finally
            {
                SkylineToolClient.SelectionChanged -= ToolClientOnSelectionChanged;
                SkylineToolClient.DocumentChanged -= OnDocumentChanged;
            }
        }

        private void ProcessChangeEventsUntilDocumentPathChanges()
        {
            _originalDocumentPath = SkylineToolClient.GetDocumentPath();
            while (true)
            {
                try
                {
                    lock (_changeEventObject)
                    {
                        Monitor.Wait(_changeEventObject, 60000);
                    }

                    try
                    {
                        Process.GetProcessById(_skylineProcessId);
                    }
                    catch (ArgumentException)
                    {
                        Console.Out.WriteLine("Skyline process has disappeared. Exiting");
                    }

                    if (_originalDocumentPath != SkylineToolClient.GetDocumentPath())
                    {
                        Console.Out.WriteLine("Document path has changed. Exiting.");
                        return;
                    }

                    var newSelectedPeptide = SkylineToolClient.GetSelectedElementLocator("Molecule");
                    if (newSelectedPeptide != _selectedPeptideLocator)
                    {
                        _selectedPeptideLocator = newSelectedPeptide;
                        if (_selectedPeptideLocator != null)
                        {
                            Console.Out.WriteLine("Molecule {0} has {1} transitions", _selectedPeptideLocator,
                                CountMoleculeTransitions(_selectedPeptideLocator));
                        }
                    }

                    var newSelectedTransition = SkylineToolClient.GetSelectedElementLocator("Transition");
                    var newSelectedReplicate = SkylineToolClient.GetSelectedElementLocator("Replicate");
                    if (newSelectedTransition != _selectedTransitionLocator ||
                        newSelectedReplicate != _selectedReplicateLocator)
                    {
                        _selectedTransitionLocator = newSelectedTransition;
                        _selectedReplicateLocator = newSelectedReplicate;
                        if (_selectedTransitionLocator != null && _selectedReplicateLocator != null)
                        {
                            var rawTimesTextValue = GetChromatogramTimes(_selectedTransitionLocator,
                                _selectedReplicateLocator).FirstOrDefault();
                            if (rawTimesTextValue == null)
                            {
                                Console.Out.WriteLine("{0} in {1} has no chromatogram",
                                    _selectedTransitionLocator, _selectedReplicateLocator);
                            }
                            else
                            {
                                var times = rawTimesTextValue.Split(',');
                                if (times.Length == 0)
                                {
                                    Console.Out.WriteLine("{0} in {1} has no times",
                                        _selectedTransitionLocator, _selectedReplicateLocator);
                                }
                                else
                                {
                                    Console.Out.WriteLine("{0} in {1} goes from {2} to {3}",
                                        _selectedTransitionLocator, _selectedReplicateLocator, times[0],
                                        times[times.Length - 1]);
                                }
                            }
                        }
                    }

                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("Exception: {0}", exception);
                }
            }
        }

        private static void ToolClientOnSelectionChanged(object sender, EventArgs args)
        {
            lock (_changeEventObject)
            {
                Monitor.Pulse(_changeEventObject);
            }
        }

        private static void OnDocumentChanged(object sender, EventArgs e)
        {
            lock (_changeEventObject)
            {
                Monitor.Pulse(_changeEventObject);
            }
        }

        public IEnumerable<string> GetChromatogramTimes(string transitionLocator, string replicateLocator)
        {
            const string reportName = "TransitionChromatogramTimes";
            var view = new XElement("view",
                new XAttribute("name", reportName),
                new XAttribute("rowsource", "pwiz.Skyline.Model.Databinding.Entities.Transition"),
                new XAttribute("sublist", "Results!*"),
                new XElement("column", new XAttribute("name", "Results!*.Value.Chromatogram.RawData.Times")),
                new XElement("column", new XAttribute("name", "Results!*.Value.Chromatogram.InterpolatedData.Times")));
            if (transitionLocator != null)
            {
                view.Add(new XElement("filter", new XAttribute("column", "Locator"), new XAttribute("opname", "equals"),
                    new XAttribute("operand", transitionLocator)));
            }

            if (replicateLocator != null)
            {
                view.Add(new XElement("filter",
                    new XAttribute("column",
                        "Results!*.Value.PrecursorResult.PeptideResult.ResultFile.Replicate.Locator"),
                    new XAttribute("opname", "equals"), new XAttribute("operand", replicateLocator)));
            }

            var layouts = new XElement("layouts", new XAttribute("viewName", reportName),
                new XAttribute("defaultLayout", "default"),
                new XElement("layout", new XAttribute("name", "default"),
                    new XElement("columnFormat", new XAttribute("column", "RawTimes"), new XAttribute("format", "R")),
                    new XElement("columnFormat", new XAttribute("column", "InterpolatedTimes"), new XAttribute("format", "R"))));
            var views = new XElement("views", view, layouts);
            var report = SkylineToolClient.GetReportFromDefinition(views.ToString());
            return report.Cells.Select(row => string.IsNullOrEmpty(row[0]) ? row[1] : row[0]);
        }

        private int CountMoleculeTransitions(string moleculeLocator)
        {
            var view = new XElement("view",
                new XAttribute("name", "CountMoleculeTransitions"),
                new XAttribute("rowsource", "pwiz.Skyline.Model.Databinding.Entities.Peptide"),
                new XAttribute("sublist", "Precursors!*.Transitions!*"),
                new XElement("column", new XAttribute("name", "Locator")),
                new XElement("column", new XAttribute("name", "Precursors!*.Transitions!*.Locator"))
            );
            if (moleculeLocator != null)
            {
                view.Add(new XElement("filter", new XAttribute("column", "Locator"), new XAttribute("opname", "equals"),
                    new XAttribute("operand", moleculeLocator)));
            }

            var views = new XElement("views", view);
            var report = SkylineToolClient.GetReportFromDefinition(views.ToString());
            return report.Cells.Length;
        }

    }
}
