/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace TestPerf
{
    /// <summary>
    /// Diagnostic sweep of the dot plot label layout: how many labels survive across window sizes, zoom
    /// levels and labeling rules, and how much of the chart they end up covering. Written for the label
    /// sampling work on PR 4495, where the sampler starved plots whose labeled points sit on a dense
    /// marker cloud.
    ///
    /// The volcano plot and the Relative Abundance plot share <see cref="LabelLayoutRunner"/>, so the sweep
    /// opens exactly one of them at a time. With both open the sampler counts of the two panes interleave
    /// and no row can be attributed to a plot.
    ///
    /// This is a tool, not a regression test. It does nothing unless SKYLINE_LABEL_SWEEP is set, and it
    /// asserts nothing about coverage - it reports a matrix and leaves the judgement to the developer. The
    /// layout invariants (no overlapping visible labels) ARE checked in every combination, so a sweep also
    /// exercises the pruner.
    ///
    /// Needs a DEBUG build: it reads the sampler's own kept count from LabelLayout.SamplerReport, which is
    /// Debug only so it costs the shipped executable nothing. In a Release build the test says so and
    /// returns.
    ///
    ///   SKYLINE_LABEL_SWEEP=1                enable; without it the test returns immediately
    ///   SKYLINE_LABEL_SWEEP_DOC=&lt;path.sky&gt;   document to use; defaults to the PeakBoundaryImputation-DIA
    ///                                        tutorial document, downloaded like any tutorial test data
    ///   SKYLINE_LABEL_SWEEP_OUT=&lt;path.csv&gt;   where to write the matrix; defaults next to the document
    /// </summary>
#if DEBUG
    [TestClass]
    public class LabelLayoutSweep : AbstractFunctionalTestEx
    {
        private const string ENV_ENABLED = "SKYLINE_LABEL_SWEEP";
        private const string ENV_DOCUMENT = "SKYLINE_LABEL_SWEEP_DOC";
        private const string ENV_OUTPUT = "SKYLINE_LABEL_SWEEP_OUT";

        // Bounded waits. Nothing here may fall back to the default 360 second wait: a combination that
        // legitimately yields no labels never satisfies the layout condition, and the sweep would spend
        // six minutes on every such cell.
        private const int READY_WAIT_MS = 60 * 1000;
        private const int SAMPLER_WAIT_MS = 15 * 1000;
        private const int LAYOUT_WAIT_MS = 60 * 1000;
        // Consecutive polls that must see the same layout instance before a measurement is taken
        private const int LAYOUT_STABLE_READS = 5;

        private static readonly Size[] WINDOW_SIZES =
        {
            new Size(1000, 700),
            new Size(1280, 900),
            new Size(1680, 1050),
            new Size(1920, 1200)
        };

        // Fraction of the full axis range to show, centered on the middle of the data
        private static readonly double[] ZOOM_FRACTIONS = { 1.0, 0.5, 0.2 };

        private string _documentPath;
        private readonly List<SweepRow> _rows = new List<SweepRow>();
        private readonly List<SamplerReport> _samplerReports = new List<SamplerReport>();
        private readonly HashSet<string> _foreignPanesWarned = new HashSet<string>();
        private int _samplerIn;
        private int _samplerKept;

        [TestMethod,
         NoNightlyTesting(TestExclusionReason.EXCESSIVE_TIME),
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestLabelLayoutSweep()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_ENABLED)))
                return;     // Diagnostic tool, off unless asked for

            _documentPath = Environment.GetEnvironmentVariable(ENV_DOCUMENT);
            if (string.IsNullOrEmpty(_documentPath))
                TestFilesZipPaths = new[] { "https://skyline.ms/tutorials/PeakBoundaryImputation-DIA.zip" };

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var documentPath = _documentPath;
            if (string.IsNullOrEmpty(documentPath))
                documentPath = TestFilesDirs[0].GetTestPath("ExtracellularVesicalMagNet.sky");

            RunUI(() =>
            {
                Settings.Default.GroupComparisonAvoidLabelOverlap = true;
                // Rank proteins, not peptides - the plot the labeling rules are written against
                Settings.Default.AreaProteinTargets = true;
            });
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForDocumentLoaded();

            // The saved view may restore either plot, and both feed the same runner. Start from a state
            // where neither is open so the first target sweeps alone.
            DestroyPeakAreaGraph();
            RunUI(() =>
            {
                foreach (var form in FormUtil.OpenForms.OfType<FoldChangeForm>().ToArray())
                    form.Close();
            });
            WaitForGraphs();

            // Pruned labels are dropped from the layout, so the sampler's own count has to come from the
            // runner itself to tell the two stages apart. The hook fires on the UI thread, and every read
            // of the list below goes through RunUI or TryWaitForConditionUI, so the list needs no lock.
            LabelLayout.SamplerReport = (pane, candidates, kept) =>
                _samplerReports.Add(new SamplerReport(pane, candidates, kept));
            try
            {
                // One plot at a time, and the volcano first because Relative Abundance is the plot the
                // developer reads last
                SweepPlot(new VolcanoTarget());
                SweepPlot(new RelativeAbundanceTarget());
            }
            finally
            {
                LabelLayout.SamplerReport = null;
            }

            WriteReport(documentPath);
        }

        private void SweepPlot(PlotTarget target)
        {
            if (!target.Open())
            {
                Console.Out.WriteLine(@"Skipping {0}: {1}", target.Name, target.SkipReason);
                return;
            }
            RunUI(() => Console.Out.WriteLine(string.Format(CultureInfo.InvariantCulture,
                @"Sweeping {0}, pane {1}, {2} saved rules, full range x [{3:F2},{4:F2}] y [{5:F2},{6:F2}]",
                target.Name, target.Pane.GetType().Name, target.SavedRules.Count,
                target.FullRange.XMin, target.FullRange.XMax, target.FullRange.YMin, target.FullRange.YMax)));
            try
            {
                foreach (var ruleSet in GetRuleSets(target.SavedRules))
                {
                    target.ApplyRules(ruleSet.Rows);
                    foreach (var windowSize in WINDOW_SIZES)
                    {
                        target.Resize(windowSize);
                        foreach (var zoom in ZOOM_FRACTIONS)
                        {
                            SetZoom(target, zoom);
                            var laidOut = ForceFreshLayout(target);
                            var row = Measure(target, ruleSet.Name, windowSize, zoom, laidOut);
                            _rows.Add(row);
                            // Trace as the sweep goes: a run of 70-odd combinations is long enough that a
                            // failure partway through should still leave the rows before it readable
                            Console.Out.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                @"{0} {1} {2}x{3} zoom={4} created={5} sampled={6}/{7} labeled={8} laidOut={9}",
                                row.Plot, row.RuleSetName, windowSize.Width, windowSize.Height, zoom,
                                row.LabelObjects, row.SamplerKept, row.SamplerIn, row.Labeled, row.LaidOut));
                            VerifyLayoutInvariants(target, ruleSet.Name, windowSize, zoom);
                        }
                    }
                }
                VerifySizeAxisApplied(target);
            }
            finally
            {
                target.Close();
            }
        }

        /// <summary>
        /// A resize that does not take effect is silent - the sweep would report four window sizes that all
        /// measured the same chart - so prove the size axis moved something rather than trusting it.
        /// </summary>
        private void VerifySizeAxisApplied(PlotTarget target)
        {
            var distinctAreas = _rows.Where(r => r.Plot == target.Name)
                .Select(r => Math.Round(r.ChartArea)).Distinct().Count();
            if (distinctAreas <= 1)
            {
                Console.Out.WriteLine(
                    @"WARNING: every window size measured the same chart for {0} - the resize did not take effect",
                    target.Name);
            }
        }

        /// <summary>
        /// Closes the Peak Areas graph for real. <see cref="SkylineWindow.ShowGraphPeakArea(bool)"/> only
        /// hides it, and a hidden <see cref="GraphSummary"/> keeps its pane subscribed to
        /// Settings.Default.PropertyChanged - so every toggle of the overlap setting in
        /// <see cref="ForceFreshLayout"/> would run a full layout on the hidden pane and feed the shared
        /// runner alongside the plot under test.
        /// </summary>
        private static void DestroyPeakAreaGraph()
        {
            RunUI(() =>
            {
                var graph = SkylineWindow.GraphPeakArea;
                if (graph == null)
                    return;
                graph.HideOnClose = false;
                graph.Close();
            });
            WaitForGraphs();
        }

        private sealed class RuleSet
        {
            public RuleSet(string name, IList<MatchRgbHexColor> rows)
            {
                Name = name;
                Rows = rows;
            }

            public string Name { get; }
            public IList<MatchRgbHexColor> Rows { get; }
        }

        /// <summary>
        /// The plot's own saved rules as the baseline - the state the developer sees when opening the
        /// document - then rule sets that widen what gets labeled.
        /// </summary>
        private static IEnumerable<RuleSet> GetRuleSets(IList<MatchRgbHexColor> savedRows)
        {
            yield return new RuleSet(@"saved", savedRows);

            var allLabeled = savedRows
                .Select(row => new MatchRgbHexColor(row.Expression, true, row.Color, row.PointSymbol, row.PointSize))
                .ToList();
            if (allLabeled.Any())
                yield return new RuleSet(@"saved-all-labeled", allLabeled);

            // An empty expression parses to a match expression with no options, which matches every point,
            // so this labels the whole plot
            yield return new RuleSet(@"label-everything",
                new List<MatchRgbHexColor> { new MatchRgbHexColor(string.Empty, true, Color.DarkBlue) });
        }

        /// <summary>
        /// Shows the given fraction of each axis, centered, then lets the graph re-run the layout. A zoom
        /// changes marker density per cell without changing the data, which is the axis the sampler was
        /// most sensitive to.
        ///
        /// The fraction is always measured from the range captured when the plot opened, never from
        /// whatever the previous combination left behind. Zooming relative to the current scale compounds,
        /// and it cannot be undone with ZoomOutAll: the volcano plot pins MinAuto/MaxAuto to false, so
        /// zooming out restores nothing. In the first run this shrank the view monotonically across the
        /// whole sweep - 125 candidates in the opening combination down to 1 by the seventh - and every
        /// row after the first was measuring a different view than its label said.
        /// </summary>
        private void SetZoom(PlotTarget target, double fraction)
        {
            RunUI(() =>
            {
                var pane = target.Pane;
                var full = target.FullRange;
                SetAxisRange(pane.XAxis, full.XMin, full.XMax, fraction);
                SetAxisRange(pane.YAxis, full.YMin, full.YMax, fraction);
                pane.AxisChange(target.GraphControl.CreateGraphics());
                target.GraphControl.Invalidate();
            });
            target.Redraw();
        }

        /// <summary>
        /// Narrows an axis to the given fraction of its full range, centered. A log axis is narrowed in
        /// log space: the Relative Abundance y axis spans 1e4 to 1e12, where taking half of the linear
        /// range leaves only the top decade and calls it a 50% zoom.
        /// </summary>
        private static void SetAxisRange(Axis axis, double min, double max, double fraction)
        {
            var isLog = axis.Scale.IsLog && min > 0 && max > 0;
            if (isLog)
            {
                min = Math.Log10(min);
                max = Math.Log10(max);
            }
            var center = (min + max) / 2;
            var half = (max - min) * fraction / 2;
            axis.Scale.Min = isLog ? Math.Pow(10, center - half) : center - half;
            axis.Scale.Max = isLog ? Math.Pow(10, center + half) : center + half;
            axis.Scale.MinAuto = axis.Scale.MaxAuto = false;
        }

        /// <summary>
        /// Toggling the overlap setting with the pane alive clears the saved layout and forces a fresh
        /// compute, the same trick LabelLayoutTest uses. Without it a combination can inherit the previous
        /// one's placements and the stage counts stay stale.
        ///
        /// Returns false when no layout ran, which is a legitimate outcome rather than a failure: the pane
        /// may offer no label candidates at all, and the runner returns before the annealer when the
        /// sampler keeps none of them.
        /// </summary>
        private bool ForceFreshLayout(PlotTarget target)
        {
            RunUI(() => Settings.Default.GroupComparisonAvoidLabelOverlap = false);
            target.WaitUntilReady();
            _samplerIn = _samplerKept = -1;
            LabelLayout layoutBefore = null;
            RunUI(() =>
            {
                _samplerReports.Clear();
                layoutBefore = target.Pane.Layout;
            });
            RunUI(() => Settings.Default.GroupComparisonAvoidLabelOverlap = true);
            target.WaitUntilReady();

            // The sampler runs on the UI thread as the pane hands its labels to the runner, so a report is
            // in hand well before the annealer finishes
            if (!TryWaitForConditionUI(SAMPLER_WAIT_MS, () => _samplerReports.Any(r => target.OwnsPane(r.Pane))))
            {
                // No report means the runner returned before the sampler. Say which of its guards is the
                // reason, so an unexplained blank row does not go in the report.
                RunUI(() =>
                {
                    var pane = target.Pane;
                    Console.Out.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        @"No sampler report for {0}: chart {1:F0}x{2:F0}, {3} label objects, {4} foreign reports, " +
                        @"x [{5:G6},{6:G6}] y [{7:G6},{8:G6}]",
                        target.Name, pane.Chart.Rect.Width, pane.Chart.Rect.Height,
                        pane.GraphObjList.OfType<TextObj>().Count(), _samplerReports.Count,
                        pane.XAxis.Scale.Min, pane.XAxis.Scale.Max, pane.YAxis.Scale.Min, pane.YAxis.Scale.Max));
                });
                return false;
            }
            RunUI(() =>
            {
                // A report from another pane means some other plot is still alive and feeding the shared
                // runner. The measurement below still reads this target's own pane, so the row stays
                // attributable, but the cross-talk is worth knowing about.
                WarnOfForeignReports(target);
                var report = _samplerReports.Last(r => target.OwnsPane(r.Pane));
                _samplerIn = report.Candidates;
                _samplerKept = report.Kept;
            });
            if (_samplerKept <= 0)
                return false;

            // Wait for THIS run's layout, identified by the pane holding a different LabelLayout instance:
            // the runner builds a new one per run and installs it in the same UI callback that prunes.
            // Waiting merely for a non-empty layout returns the previous run's, and in that window the
            // current run has already re-shown every sampled label at its unplaced position - which reads
            // as a pile of overlapping labels and fails the invariant check for no reason.
            if (!TryWaitForConditionUI(LAYOUT_WAIT_MS,
                    () => target.Pane.Layout != null && !ReferenceEquals(target.Pane.Layout, layoutBefore) &&
                          target.Pane.Layout.PointsLayout.Count > 0))
            {
                return false;
            }
            WaitForLayoutToSettle(target);
            return true;
        }

        /// <summary>
        /// Waits until the pane stops installing new layouts, so a measurement is not taken while a later
        /// run is still in flight. Advisory: a layout has already completed by this point either way.
        /// </summary>
        private void WaitForLayoutToSettle(PlotTarget target)
        {
            LabelLayout lastSeen = null;
            var stableReads = 0;
            var settled = TryWaitForConditionUI(LAYOUT_WAIT_MS, () =>
            {
                var current = target.Pane.Layout;
                stableReads = ReferenceEquals(current, lastSeen) ? stableReads + 1 : 0;
                lastSeen = current;
                return stableReads >= LAYOUT_STABLE_READS;
            });
            if (!settled)
                Console.Out.WriteLine(@"WARNING: {0} kept re-laying out; the next row may be mid-run", target.Name);
        }

        private void WarnOfForeignReports(PlotTarget target)
        {
            var foreign = _samplerReports.Where(r => !target.OwnsPane(r.Pane))
                .Select(r => r.Pane.GetType().Name).Distinct().ToList();
            if (!foreign.Any())
                return;
            if (_foreignPanesWarned.Add(string.Join(@",", foreign)))
            {
                Console.Out.WriteLine(@"WARNING: during the {0} sweep the shared runner also served {1}",
                    target.Name, string.Join(@", ", foreign));
            }
        }

        private sealed class SamplerReport
        {
            public SamplerReport(GraphPane pane, int candidates, int kept)
            {
                Pane = pane;
                Candidates = candidates;
                Kept = kept;
            }

            public GraphPane Pane { get; }
            public int Candidates { get; }
            public int Kept { get; }
        }

        private sealed class SweepRow
        {
            public string Plot;
            public string RuleSetName;
            public Size WindowSize;
            public double Zoom;
            public bool LaidOut;
            public int Candidates;
            public int Labeled;
            public int MarkerPoints;
            public int RuleRows;
            public int LabeledRuleRows;
            public int LabelObjects;
            public int SamplerIn;
            public int SamplerKept;
            public string LabelTexts = string.Empty;
            public double ChartArea;
            public double LabelArea;
            public double CoveragePercent => ChartArea > 0 ? 100 * LabelArea / ChartArea : 0;
        }

        private SweepRow Measure(PlotTarget target, string ruleSetName, Size windowSize, double zoom, bool laidOut)
        {
            var row = new SweepRow
            {
                Plot = target.Name,
                RuleSetName = ruleSetName,
                WindowSize = windowSize,
                Zoom = zoom,
                LaidOut = laidOut,
                SamplerIn = _samplerIn,
                SamplerKept = _samplerKept
            };
            RunUI(() =>
            {
                var pane = target.Pane;
                row.ChartArea = pane.Chart.Rect.Width * pane.Chart.Rect.Height;
                row.MarkerPoints = pane.CurveList.OfType<LineItem>()
                    .Where(c => c.Symbol.Type != SymbolType.None).Sum(c => c.Points.Count);
                // Labels the pane created from the rules, before the sampler and layout see them
                row.LabelObjects = pane.GraphObjList.OfType<TextObj>().Count();
                var rules = target.CurrentRules;
                row.RuleRows = rules.Count;
                row.LabeledRuleRows = rules.Count(r => r.Labeled);
                var layout = pane.Layout;
                if (layout == null)
                    return;
                using (var g = target.GraphControl.CreateGraphics())
                {
                    var points = layout.LabeledPoints.Values.ToList();
                    row.Candidates = points.Count;
                    row.LabelTexts = string.Join(@" ", points.Take(6).Select(lp =>
                        (lp.Label.IsVisible ? string.Empty : @"~") + lp.Label.Text));
                    foreach (var labeledPoint in points.Where(lp => lp.Label.IsVisible))
                    {
                        var rect = pane.GetRectScreen(labeledPoint.Label, g);
                        if (rect.Width <= 0 || rect.Height <= 0)
                            continue;
                        row.Labeled++;
                        row.LabelArea += rect.Width * rect.Height;
                    }
                }
            });
            return row;
        }

        /// <summary>
        /// The pruner's guarantee, checked in every combination: no two visible labels overlap unless both
        /// are selected.
        /// </summary>
        private void VerifyLayoutInvariants(PlotTarget target, string ruleSetName, Size windowSize, double zoom)
        {
            RunUI(() =>
            {
                var pane = target.Pane;
                var layout = pane.Layout;
                if (layout == null)
                    return;
                var where = string.Format(CultureInfo.InvariantCulture, @"plot={0} rules={1} size={2}x{3} zoom={4}",
                    target.Name, ruleSetName, windowSize.Width, windowSize.Height, zoom);
                using (var g = target.GraphControl.CreateGraphics())
                {
                    var entries = new List<KeyValuePair<LabeledPoint, RectangleF>>();
                    foreach (var labeledPoint in layout.LabeledPoints.Values.Where(lp => lp.Label.IsVisible))
                    {
                        var rect = pane.GetRectScreen(labeledPoint.Label, g);
                        if (rect.Width > 0 && rect.Height > 0)
                            entries.Add(new KeyValuePair<LabeledPoint, RectangleF>(labeledPoint, rect));
                    }

                    for (var i = 0; i < entries.Count; i++)
                    {
                        for (var j = i + 1; j < entries.Count; j++)
                        {
                            if (entries[i].Key.IsSelected && entries[j].Key.IsSelected)
                                continue;
                            AssertEx.IsFalse(entries[i].Value.IntersectsWith(entries[j].Value),
                                string.Format(@"Labels '{0}' and '{1}' overlap ({2})",
                                    entries[i].Key.Label.Text, entries[j].Key.Label.Text, where));
                        }
                    }
                }
            });
        }

        private void WriteReport(string documentPath)
        {
            var outPath = Environment.GetEnvironmentVariable(ENV_OUTPUT);
            if (string.IsNullOrEmpty(outPath))
            {
                outPath = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty,
                    @"label-layout-sweep.csv");
            }

            var csv = new StringBuilder();
            csv.AppendLine(@"plot,rules,width,height,zoom,laidOut,ruleRows,labeledRuleRows,markerPoints,candidates,labeled,chartArea,labelArea,coveragePercent,samplerIn,samplerKept,labelObjects,labelTexts");
            var table = new StringBuilder();
            table.AppendLine(string.Format(@"{0,-18} {1,-18} {2,11} {3,5} {4,9} {5,17} {6,8} {7,9}  {8}",
                @"plot", @"rules", @"graph size", @"zoom", @"chart px", @"created/sampled/kept", @"labeled",
                @"coverage", @"first labels"));
            foreach (var row in _rows.OrderBy(r => r.Plot).ThenBy(r => r.CoveragePercent))
            {
                csv.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    @"{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11:F0},{12:F0},{13:F2},{14},{15},{16},{17}",
                    row.Plot, row.RuleSetName, row.WindowSize.Width, row.WindowSize.Height, row.Zoom, row.LaidOut,
                    row.RuleRows, row.LabeledRuleRows, row.MarkerPoints,
                    row.Candidates, row.Labeled, row.ChartArea, row.LabelArea, row.CoveragePercent,
                    row.SamplerIn, row.SamplerKept, row.LabelObjects, row.LabelTexts));
                table.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    @"{0,-18} {1,-18} {2,5}x{3,-5} {4,5:F1} {5,9:F0} {6,6}/{7,-5}/{8,-4} {9,8} {10,8:F2}%  {11}",
                    row.Plot, row.RuleSetName, row.WindowSize.Width, row.WindowSize.Height, row.Zoom, row.ChartArea,
                    row.LabelObjects, row.SamplerIn, row.SamplerKept, row.Labeled, row.CoveragePercent,
                    row.LabelTexts));
            }

            File.WriteAllText(outPath, csv.ToString());
            Console.Out.WriteLine(@"Label layout sweep, worst coverage first within each plot:");
            Console.Out.WriteLine(table.ToString());
            Console.Out.WriteLine(@"Wrote " + outPath);
        }

        private sealed class AxisRange
        {
            public AxisRange(double xMin, double xMax, double yMin, double yMax)
            {
                XMin = xMin;
                XMax = xMax;
                YMin = yMin;
                YMax = yMax;
            }

            public double XMin { get; }
            public double XMax { get; }
            public double YMin { get; }
            public double YMax { get; }
        }

        /// <summary>
        /// One plot under test. Both implementations drive the same shared <see cref="LabelLayoutRunner"/>,
        /// which is why only one of them may be open at a time.
        /// </summary>
        private abstract class PlotTarget
        {
            public abstract string Name { get; }

            /// <summary>
            /// True when the pane the sampler reported belongs to this plot. Only one plot is open at a
            /// time, so this is a check that the sweep really did close the other one.
            /// </summary>
            public abstract bool OwnsPane(GraphPane pane);

            public string SkipReason { get; protected set; }

            /// <summary>The rules the document was saved with, read once when the plot opens.</summary>
            public IList<MatchRgbHexColor> SavedRules { get; protected set; }

            /// <summary>
            /// The auto-scaled axis range as it stood when the plot opened. Every zoom is measured from
            /// this rather than from the live scale, which the sweep has already pinned.
            /// </summary>
            public AxisRange FullRange { get; private set; }

            /// <summary>Call from <see cref="Open"/> once the plot has auto-scaled to its data.</summary>
            protected void CaptureFullRange()
            {
                RunUI(() =>
                {
                    var pane = Pane;
                    FullRange = new AxisRange(pane.XAxis.Scale.Min, pane.XAxis.Scale.Max,
                        pane.YAxis.Scale.Min, pane.YAxis.Scale.Max);
                });
            }

            /// <summary>The rules in force now, which the sweep reports alongside each measurement.</summary>
            public abstract IList<MatchRgbHexColor> CurrentRules { get; }

            /// <summary>UI thread only.</summary>
            public abstract GraphPane Pane { get; }

            /// <summary>UI thread only.</summary>
            public abstract ZedGraphControl GraphControl { get; }

            /// <summary>Opens the plot floating. Returns false with <see cref="SkipReason"/> set when the
            /// document cannot support it.</summary>
            public abstract bool Open();

            public abstract void Close();

            /// <summary>Asks the plot to rebuild, which restarts the label layout.</summary>
            public abstract void Redraw();

            /// <summary>Bounded wait for the plot to finish rebuilding.</summary>
            public abstract void WaitUntilReady();

            public abstract void ApplyRules(IList<MatchRgbHexColor> rows);

            /// <summary>
            /// Sizes the floating frame the plot lives in.
            ///
            /// <see cref="AbstractFunctionalTestEx.ResizeFormOnScreen"/> cannot be used offscreen: it
            /// returns before resizing, because the ForceOnScreen call that follows would drag a
            /// deliberately offscreen window back onto the desktop. The resize itself is valid either way -
            /// offscreen mode only repositions the main window - and window size is one of the three axes
            /// this sweep varies, so it sets the size directly and skips only ForceOnScreen. Without this
            /// an offscreen sweep would silently report four identical sizes.
            /// </summary>
            public void Resize(Size size)
            {
                RunUI(() =>
                {
                    if (!Program.SkylineOffscreen)
                    {
                        ResizeFloatingFrame(Form, size.Width, size.Height);
                        return;
                    }
                    var frame = Form.ParentForm;
                    Assert.IsNotNull(frame, @"Floating plot has no parent frame to size.");
                    frame.Size = size;
                });
                Redraw();
            }

            protected abstract DockableFormEx Form { get; }

            protected static void Float(DockableFormEx form)
            {
                RunUI(() =>
                {
                    if (form.DockState != DockState.Floating)
                        form.Show(form.DockPanel, DockState.Floating);
                });
            }

            protected void WarnIfNotReady(bool ready)
            {
                if (!ready)
                    Console.Out.WriteLine(@"WARNING: {0} did not settle within the wait; the next row may be stale", Name);
            }
        }

        private sealed class VolcanoTarget : PlotTarget
        {
            private FoldChangeVolcanoPlot _volcanoPlot;
            private string _groupComparisonName;

            public override string Name => @"volcano";

            public override bool OwnsPane(GraphPane pane)
            {
                return ReferenceEquals(pane, _volcanoPlot?.GraphControl.GraphPane);
            }

            public override IList<MatchRgbHexColor> CurrentRules => GroupComparisonDef.ColorRows;

            public override GraphPane Pane => _volcanoPlot.GraphControl.GraphPane;

            public override ZedGraphControl GraphControl => _volcanoPlot.GraphControl;

            protected override DockableFormEx Form => _volcanoPlot;

            public override bool Open()
            {
                var groupComparison = SkylineWindow.Document.Settings.DataSettings.GroupComparisonDefs
                    .FirstOrDefault();
                if (groupComparison == null)
                {
                    SkipReason = @"the document defines no group comparison";
                    return false;
                }
                _groupComparisonName = groupComparison.Name;

                RunUI(() => SkylineWindow.ShowGroupComparisonWindow(_groupComparisonName));
                var grid = WaitForOpenForm<FoldChangeGrid>();
                // IsComplete only signals that the column layout is up; the fold changes are computed
                // asynchronously and the plot has nothing to draw until rows arrive
                WaitForConditionUI(READY_WAIT_MS,
                    () => grid.DataboundGridControl.IsComplete && grid.DataboundGridControl.RowCount > 0);
                RunUI(() => grid.ShowVolcanoPlot());
                _volcanoPlot = WaitForOpenForm<FoldChangeVolcanoPlot>();
                Float(_volcanoPlot);
                WaitUntilReady();
                SavedRules = GroupComparisonDef.ColorRows.ToList();
                CaptureFullRange();
                return true;
            }

            public override void Close()
            {
                if (_volcanoPlot != null)
                    ApplyRules(SavedRules);
                RunUI(() =>
                {
                    foreach (var form in FormUtil.OpenForms.OfType<FoldChangeForm>().ToArray())
                        form.Close();
                });
                WaitForConditionUI(READY_WAIT_MS, () => !FormUtil.OpenForms.OfType<FoldChangeForm>().Any());
                _volcanoPlot = null;
            }

            public override void Redraw()
            {
                RunUI(() => _volcanoPlot.QueueUpdateGraph());
                WaitUntilReady();
            }

            public override void WaitUntilReady()
            {
                WarnIfNotReady(TryWaitForConditionUI(READY_WAIT_MS,
                    () => !_volcanoPlot.UpdatePending && _volcanoPlot.IsComplete));
                WaitForGraphs();
            }

            public override void ApplyRules(IList<MatchRgbHexColor> rows)
            {
                // No undo record or audit entry: this is a diagnostic sweep, and the settings log function
                // produces no entry for a formatting-only change, which ModifyDocument asserts on.
                var model = _volcanoPlot.FoldChangeBindingSource.GroupComparisonModel;
                var newDef = GroupComparisonDef.ChangeColorRows(rows);
                RunUI(() => SkylineWindow.ModifyDocumentNoUndo(doc => model.ApplyChangesToDocument(doc, newDef)));
                Redraw();
            }

            private GroupComparisonDef GroupComparisonDef
            {
                get
                {
                    return SkylineWindow.Document.Settings.DataSettings.GroupComparisonDefs
                        .First(def => Equals(def.Name, _groupComparisonName));
                }
            }
        }

        private sealed class RelativeAbundanceTarget : PlotTarget
        {
            public override string Name => @"relative-abundance";

            public override bool OwnsPane(GraphPane pane)
            {
                return pane is SummaryRelativeAbundanceGraphPane;
            }

            public override IList<MatchRgbHexColor> CurrentRules =>
                SkylineWindow.Document.Settings.DataSettings.RelativeAbundanceFormatting.ColorRows.ToList();

            public override GraphPane Pane => FindPane();

            public override ZedGraphControl GraphControl => SkylineWindow.GraphPeakArea.GraphControl;

            protected override DockableFormEx Form => SkylineWindow.GraphPeakArea;

            public override bool Open()
            {
                RunUI(SkylineWindow.ShowPeakAreaRelativeAbundanceGraph);
                RunUI(SkylineWindow.UpdatePeakAreaGraph);
                WaitUntilReady();
                // Float the graph so the size axis controls the chart rectangle directly, rather than
                // letting the docking layout decide how much of the main window the pane gets
                Float(SkylineWindow.GraphPeakArea);
                WaitUntilReady();
                SavedRules = CurrentRules.ToList();
                CaptureFullRange();
                return true;
            }

            public override void Close()
            {
                ApplyRules(SavedRules);
                DestroyPeakAreaGraph();
            }

            public override void Redraw()
            {
                RunUI(SkylineWindow.UpdatePeakAreaGraph);
                WaitUntilReady();
            }

            public override void WaitUntilReady()
            {
                WarnIfNotReady(TryWaitForConditionUI(READY_WAIT_MS, () =>
                {
                    var pane = FindPane();
                    return pane != null && pane.IsSuccessfullyComplete;
                }));
                WaitForGraphs();
            }

            public override void ApplyRules(IList<MatchRgbHexColor> rows)
            {
                RunUI(() => SkylineWindow.ModifyDocumentNoUndo(doc =>
                    doc.ChangeSettings(doc.Settings.ChangeDataSettings(
                        doc.Settings.DataSettings.ChangeRelativeAbundanceFormatting(
                            doc.Settings.DataSettings.RelativeAbundanceFormatting.ChangeColorRows(rows))))));
                Redraw();
            }

            private static SummaryRelativeAbundanceGraphPane FindPane()
            {
                return SkylineWindow.GraphPeakArea?.GraphControl?.MasterPane?.PaneList
                    .OfType<SummaryRelativeAbundanceGraphPane>().FirstOrDefault();
            }
        }
    }
#else
    /// <summary>
    /// Release stand-in. The sweep reads the sampler's own kept count from LabelLayout.SamplerReport,
    /// which is Debug only so it costs the shipped executable nothing, so the tool itself is Debug only.
    /// </summary>
    [TestClass]
    public class LabelLayoutSweep
    {
        [TestMethod]
        public void TestLabelLayoutSweep()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(@"SKYLINE_LABEL_SWEEP")))
                return;     // Diagnostic tool, off unless asked for
            Console.Out.WriteLine(@"TestLabelLayoutSweep needs a Debug build - LabelLayout.SamplerReport " +
                                  @"is compiled out of Release. Rerun with -Configuration Debug.");
        }
    }
#endif

}
