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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Osprey.Chromatography;
using pwiz.Quasar.Core.Elution;

namespace pwiz.Quasar.Core.Clustering
{
    /// <summary>The templates and clusters of one isolation window.</summary>
    public sealed class WindowClusterResult
    {
        public WindowClusterResult(IReadOnlyList<ElutionTemplate> templates, IReadOnlyList<Cluster> clusters,
            string profile)
        {
            Templates = templates;
            Clusters = clusters;
            Profile = profile;
        }

        /// <summary>Where the time went, stage by stage, with the counts each stage produced (diagnostic text).</summary>
        public string Profile { get; }

        /// <summary>Every template, known ones included, in the order they were made.</summary>
        public IReadOnlyList<ElutionTemplate> Templates { get; }

        /// <summary>One cluster per template found here (not per known template), accepted or not.</summary>
        public IReadOnlyList<Cluster> Clusters { get; }
    }

    /// <summary>
    /// Template-first clustering of one isolation window (spec Stages 2c to 3d):
    /// <list type="number">
    /// <item>Anchor components: peaks on each anchor trace, found with the Mexican hat at the run's
    /// scale and kept when a template of the run's typical shape explains them (2c).</item>
    /// <item>Grouping: the most intense unused component seeds a template; components whose apex
    /// is within tolerance and whose trace the template explains join; the template is refitted
    /// to all its anchors (the pooled fit) and the members rechecked (2d).</item>
    /// <item>The window fit: every trace is fitted locally to the templates that overlap each
    /// template's support, which gives membership, amplitudes and shared fractions (3a, 3b).</item>
    /// <item>The residual pass: anchors are searched again in what the templates leave
    /// unexplained, for peptides hidden under stronger ones (3c).</item>
    /// <item>Acceptance by anchor and member counts (3d).</item>
    /// </list>
    /// Known templates (Osprey's precursors) take part in every fit but never become clusters.
    /// </summary>
    public sealed class WindowClusterer
    {
        private readonly WindowClusterSettings _settings;
        private readonly EmgShape _typicalShape;

        public WindowClusterer(WindowClusterSettings settings)
        {
            _settings = settings;
            _typicalShape = new EmgShape(0.0, settings.Prior.Sigma, settings.Prior.Tau);
        }

        /// <summary>
        /// Clusters the <paramref name="traces"/> of one window, all on the scan axis
        /// <paramref name="rt"/>. <paramref name="knownShapes"/> are the elution profiles of
        /// precursors already identified (empty in <c>--features all</c> mode).
        /// </summary>
        public WindowClusterResult Run(double[] rt, IReadOnlyList<Trace> traces, IReadOnlyList<EmgShape> knownShapes)
        {
            var templates = knownShapes
                .Select((shape, i) => new ElutionTemplate(i, shape, Array.Empty<int>(), TemplateSource.known))
                .ToList();
            var profile = new StringBuilder();
            var watch = Stopwatch.StartNew();
            var data = new FitData(traces.Select(t => t.Intensities).ToArray(), null);
            for (int pass = 0; pass <= _settings.MaxResidualPasses; pass++)
            {
                if (templates.Count > 0)
                {
                    data = Residuals(rt, traces, new WindowLayout(this, rt, templates));
                    profile.Append($@"residuals {Lap(watch)}; ");
                }

                var components = FindAnchorComponents(rt, traces, data);
                profile.Append($@"pass {pass}: {components.Count} components {Lap(watch)}, ");
                var source = pass == 0 ? TemplateSource.anchors : TemplateSource.residual_pass;
                var added = GroupAnchors(rt, traces, data, components, templates.Count, source);
                profile.Append($@"{added.Count} templates {Lap(watch)}; ");

                // Signal whose apex is within the tolerance of an existing template is co-eluting
                // with it and shares it (spec 2d). A residual there is model mismatch, not a new
                // elution profile, and a near-copy of a template would split its members.
                if (pass > 0)
                {
                    var existing = templates.Select(t => t.Shape.Mode).ToArray();
                    added = added.Where(t => existing.All(m => Math.Abs(t.Shape.Mode - m) > _settings.ApexTolerance))
                        .Select((t, i) => new ElutionTemplate(templates.Count + i, t.Shape, t.AnchorTraceIds, t.Source))
                        .ToList();
                }

                if (added.Count == 0)
                    break;
                templates.AddRange(added);
            }

            var clusters = BuildClusters(rt, traces, new WindowLayout(this, rt, templates));
            profile.Append($@"final fit {Lap(watch)}");
            return new WindowClusterResult(templates, clusters, profile.ToString());
        }

        /// <summary>Seconds since the last lap, as text, restarting the watch.</summary>
        private static string Lap(Stopwatch watch)
        {
            string text = watch.Elapsed.TotalSeconds.ToString(@"F1", CultureInfo.InvariantCulture) + @"s";
            watch.Restart();
            return text;
        }

        /// <summary>
        /// The scan range a template is fitted over: where it is at least
        /// <see cref="TemplateFit.SUPPORT_FRACTION"/> of its peak, padded for the baseline.
        /// </summary>
        public (int Start, int End) Segment(EmgShape shape, double[] rt)
        {
            var (left, right) = shape.HeightCrossings(TemplateFit.SUPPORT_FRACTION);
            int start = LowerBound(rt, left) - _settings.SegmentPadding;
            int end = LowerBound(rt, right) - 1 + _settings.SegmentPadding;
            return (Math.Max(0, start), Math.Min(rt.Length - 1, end));
        }

        /// <summary>Peaks on the anchor traces that a template of the run's typical shape explains.</summary>
        private List<AnchorComponent> FindAnchorComponents(double[] rt, IReadOnlyList<Trace> traces, FitData data)
        {
            var values = data.Values;
            double scale = _settings.CwtScaleScans;
            int radius = Math.Min((int)Math.Ceiling(5.0 * scale), rt.Length / 2);
            var kernel = CwtPeakDetector.MexicanHatKernel(scale, radius);
            var components = new List<AnchorComponent>();
            for (int t = 0; t < traces.Count; t++)
            {
                if (!traces[t].IsAnchor || values[t].All(v => v <= 0.0))
                    continue;

                var cwt = CwtPeakDetector.Convolve(values[t], kernel);
                foreach (var peak in CwtPeakDetector.FindPeaks(cwt, values[t], rt, 0.0))
                {
                    var provisional = _typicalShape.WithModeAt(rt[peak.ApexIndex]);
                    var (start, end) = Segment(provisional, rt);
                    var fit = TemplateFit.Fit(rt, values[t], start, end, new[] { provisional }, _settings.Noise,
                        data.ExplainedOf(t));
                    if (fit.IsMember(0, _settings.MembershipThreshold))
                        components.Add(new AnchorComponent(t, rt[peak.ApexIndex], fit.Amplitudes[0]));
                }
            }

            return components;
        }

        /// <summary>Greedy most-intense-first grouping of anchor components into templates (spec 2d).</summary>
        private List<ElutionTemplate> GroupAnchors(double[] rt, IReadOnlyList<Trace> traces, FitData data,
            List<AnchorComponent> components, int firstId, TemplateSource source)
        {
            var order = Enumerable.Range(0, components.Count)
                .OrderByDescending(i => components[i].Amplitude)
                .ToArray();
            var byApex = new ApexIndex(components);
            var used = new bool[components.Count];
            var templates = new List<ElutionTemplate>();
            foreach (int seed in order)
            {
                if (used[seed])
                    continue;
                used[seed] = true;
                var members = new List<int> { seed };
                var shape = _typicalShape.WithModeAt(components[seed].ApexTime);
                for (int round = 0; round < _settings.MaxGroupingRounds; round++)
                {
                    bool changed = AddExplainedComponents(rt, data, components, byApex, used, members, shape);
                    var refit = Refit(rt, data, components, members, shape);
                    if (refit != null)
                    {
                        changed |= Math.Abs(refit.Mode - shape.Mode) > 0.05 * _settings.CycleTime;
                        shape = refit;
                    }

                    changed |= ReleaseUnexplained(rt, data, components, used, members, shape);
                    if (!changed)
                        break;
                }

                var anchorIds = members.Select(c => traces[components[c].TraceIndex].Id).ToList();
                templates.Add(new ElutionTemplate(firstId + templates.Count, shape, anchorIds, source));
            }

            return templates;
        }

        /// <summary>Adds unused components near the template's apex whose trace it explains.</summary>
        private bool AddExplainedComponents(double[] rt, FitData data, List<AnchorComponent> components,
            ApexIndex byApex, bool[] used, List<int> members, EmgShape shape)
        {
            bool added = false;
            double mode = shape.Mode;
            var (start, end) = Segment(shape, rt);
            var candidates = byApex.Within(mode - _settings.ApexTolerance, mode + _settings.ApexTolerance)
                .OrderByDescending(c => components[c].Amplitude);
            foreach (int candidate in candidates)
            {
                var component = components[candidate];
                if (used[candidate] || members.Any(c => components[c].TraceIndex == component.TraceIndex))
                    continue;

                if (Explains(rt, data, component.TraceIndex, start, end, shape))
                {
                    members.Add(candidate);
                    used[candidate] = true;
                    added = true;
                }
            }

            return added;
        }

        /// <summary>Releases members, other than the seed, that the refitted template no longer explains.</summary>
        private bool ReleaseUnexplained(double[] rt, FitData data, List<AnchorComponent> components,
            bool[] used, List<int> members, EmgShape shape)
        {
            var (start, end) = Segment(shape, rt);
            var released = members.Skip(1)
                .Where(c => !Explains(rt, data, components[c].TraceIndex, start, end, shape))
                .ToList();
            foreach (int c in released)
            {
                members.Remove(c);
                used[c] = false;
            }

            return released.Count > 0;
        }

        /// <summary>The pooled fit of the template to all its members' traces, or null if it fails.</summary>
        private EmgShape Refit(double[] rt, FitData data, List<AnchorComponent> components, List<int> members,
            EmgShape shape)
        {
            var (start, end) = Segment(shape, rt);
            var traceIndices = members.Select(c => components[c].TraceIndex).ToList();
            var memberTraces = traceIndices.Select(t => data.Values[t]).ToList();
            var memberExplained = traceIndices.Select(data.ExplainedOf).ToList();
            return PooledEmgFit.Fit(rt, memberTraces, start, end, _settings.Noise, _settings.Prior, shape,
                memberExplained)?.Shape;
        }

        private bool Explains(double[] rt, FitData data, int traceIndex, int start, int end, EmgShape shape)
        {
            return TemplateFit.Fit(rt, data.Values[traceIndex], start, end, new[] { shape }, _settings.Noise,
                    data.ExplainedOf(traceIndex))
                .IsMember(0, _settings.MembershipThreshold);
        }

        /// <summary>
        /// Each trace minus what every template's local fit explains (spec 3c), with what was taken
        /// out kept for the variances.
        /// </summary>
        private FitData Residuals(double[] rt, IReadOnlyList<Trace> traces, WindowLayout layout)
        {
            var residuals = new double[traces.Count][];
            var explained = new double[traces.Count][];
            for (int t = 0; t < traces.Count; t++)
            {
                var values = traces[t].Intensities;
                var model = new double[values.Length];
                var fits = layout.LocalFits(values);
                for (int k = 0; k < fits.Length; k++)
                {
                    if (fits[k] == null || fits[k].Amplitude <= 0.0)
                        continue;
                    var (start, end) = layout.Segments[k];
                    var shape = layout.Templates[k].Shape;
                    for (int i = start; i <= end; i++)
                        model[i] += fits[k].Amplitude * shape.Density(rt[i]);
                }

                residuals[t] = values.Select((v, i) => v - model[i]).ToArray();
                explained[t] = model;
            }

            return new FitData(residuals, explained);
        }

        /// <summary>Members of every template found here, with shared fractions, and acceptance (3b, 3d).</summary>
        private List<Cluster> BuildClusters(double[] rt, IReadOnlyList<Trace> traces, WindowLayout layout)
        {
            var templates = layout.Templates;
            var members = templates.Select(_ => new List<ClusterMember>()).ToArray();
            foreach (var trace in traces)
            {
                var fits = layout.LocalFits(trace.Intensities);
                double memberTotal = fits.Where(f => f != null && f.IsMember).Sum(f => f.Amplitude);
                for (int k = 0; k < fits.Length; k++)
                {
                    if (fits[k] == null || !fits[k].IsMember)
                        continue;
                    members[k].Add(new ClusterMember(trace, fits[k].Amplitude, fits[k].DeltaChiSquare,
                        fits[k].Amplitude / memberTotal));
                }
            }

            var clusters = new List<Cluster>();
            for (int k = 0; k < templates.Count; k++)
            {
                if (templates[k].Source == TemplateSource.known)
                    continue;
                var ordered = members[k].OrderByDescending(m => m.Amplitude).ToList();
                var rejection = ClusterRejection.none;
                if (ordered.Count(m => m.Trace.IsAnchor) < _settings.MinAnchors)
                    rejection = ClusterRejection.no_anchor_member;
                else if (ordered.Count < _settings.MinMembers)
                    rejection = ClusterRejection.too_few_members;
                clusters.Add(new Cluster(templates[k], ordered, rejection));
            }

            return clusters;
        }

        /// <summary>The index of the first element of sorted <paramref name="values"/> not less than <paramref name="x"/>.</summary>
        private static int LowerBound(double[] values, double x)
        {
            int lo = 0, hi = values.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (values[mid] < x)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        /// <summary>
        /// The values the anchor search and grouping fit: the traces themselves, or their residuals
        /// with the signal the templates already explain (null before any template exists).
        /// </summary>
        private sealed class FitData
        {
            private readonly double[][] _explained;

            public FitData(double[][] values, double[][] explained)
            {
                Values = values;
                _explained = explained;
            }

            public double[][] Values { get; }

            public double[] ExplainedOf(int traceIndex)
            {
                return _explained?[traceIndex];
            }
        }

        /// <summary>Anchor components sorted by apex time, for finding those near a template's apex.</summary>
        private sealed class ApexIndex
        {
            private readonly int[] _order;
            private readonly double[] _apexTimes;

            public ApexIndex(List<AnchorComponent> components)
            {
                _order = Enumerable.Range(0, components.Count).OrderBy(i => components[i].ApexTime).ToArray();
                _apexTimes = _order.Select(i => components[i].ApexTime).ToArray();
            }

            /// <summary>The components whose apex lies in [<paramref name="from"/>, <paramref name="to"/>].</summary>
            public IEnumerable<int> Within(double from, double to)
            {
                for (int i = LowerBound(_apexTimes, from); i < _apexTimes.Length && _apexTimes[i] <= to; i++)
                    yield return _order[i];
            }
        }

        /// <summary>A peak on one anchor trace (spec 2c).</summary>
        private sealed class AnchorComponent
        {
            public AnchorComponent(int traceIndex, double apexTime, double amplitude)
            {
                TraceIndex = traceIndex;
                ApexTime = apexTime;
                Amplitude = amplitude;
            }

            public int TraceIndex { get; }
            public double ApexTime { get; }

            /// <summary>The area a template of typical shape gives the peak; orders the seeding.</summary>
            public double Amplitude { get; }
        }

        /// <summary>One template's local fit of one trace.</summary>
        private sealed class LocalFit
        {
            public LocalFit(double amplitude, double deltaChiSquare, bool isMember)
            {
                Amplitude = amplitude;
                DeltaChiSquare = deltaChiSquare;
                IsMember = isMember;
            }

            public double Amplitude { get; }
            public double DeltaChiSquare { get; }
            public bool IsMember { get; }
        }

        /// <summary>
        /// The templates of a window with each one's segment and the templates overlapping it, so
        /// every trace's local fits are the same small problems.
        /// </summary>
        private sealed class WindowLayout
        {
            private readonly WindowClusterer _clusterer;
            private readonly double[] _rt;
            private readonly int[][] _overlapping;
            private readonly int[] _byStart;
            private readonly int[] _starts;
            private readonly int _maxLength;

            public WindowLayout(WindowClusterer clusterer, double[] rt, IReadOnlyList<ElutionTemplate> templates)
            {
                _clusterer = clusterer;
                _rt = rt;
                Templates = templates;
                Segments = templates.Select(t => clusterer.Segment(t.Shape, rt)).ToArray();
                _byStart = Enumerable.Range(0, templates.Count).OrderBy(k => Segments[k].Start).ToArray();
                _starts = _byStart.Select(k => Segments[k].Start).ToArray();
                _maxLength = Segments.Length == 0 ? 0 : Segments.Max(s => s.End - s.Start);

                // Template k first, so its statistics are at index 0 of its fit.
                _overlapping = new int[templates.Count][];
                for (int k = 0; k < templates.Count; k++)
                {
                    var (start, end) = Segments[k];
                    _overlapping[k] = new[] { k }.Concat(Overlapping(start, end).Where(j => j != k)).ToArray();
                }
            }

            public IReadOnlyList<ElutionTemplate> Templates { get; }

            public (int Start, int End)[] Segments { get; }

            /// <summary>
            /// For each template, the fit of the trace over that template's segment against every
            /// template overlapping it; null where the trace has no signal in the segment. Only the
            /// templates overlapping the trace's non-zero runs are fitted, so a short centroid trace
            /// costs a few fits however many templates the window has.
            /// </summary>
            public LocalFit[] LocalFits(double[] values)
            {
                var settings = _clusterer._settings;
                var fits = new LocalFit[Templates.Count];
                foreach (int k in TemplatesWithSignal(values))
                {
                    var (start, end) = Segments[k];
                    var shapes = _overlapping[k].Select(j => Templates[j].Shape).ToArray();
                    var fit = TemplateFit.Fit(_rt, values, start, end, shapes, settings.Noise, null, 1);
                    fits[k] = new LocalFit(fit.Amplitudes[0], fit.DeltaChiSquare[0],
                        fit.IsMember(0, settings.MembershipThreshold));
                }

                return fits;
            }

            /// <summary>The templates whose segment holds at least one positive point of the trace.</summary>
            private IEnumerable<int> TemplatesWithSignal(double[] values)
            {
                var found = new HashSet<int>();
                int i = 0;
                while (i < values.Length)
                {
                    if (values[i] <= 0.0)
                    {
                        i++;
                        continue;
                    }

                    int runStart = i;
                    while (i < values.Length && values[i] > 0.0)
                        i++;
                    found.UnionWith(Overlapping(runStart, i - 1));
                }

                return found.OrderBy(k => k);
            }

            /// <summary>The templates whose segment intersects scans <paramref name="start"/> to <paramref name="end"/>.</summary>
            private IEnumerable<int> Overlapping(int start, int end)
            {
                for (int i = LowerBound(_starts, start - _maxLength); i < _starts.Length && _starts[i] <= end; i++)
                {
                    int k = _byStart[i];
                    if (Segments[k].End >= start)
                        yield return k;
                }
            }

            private static int LowerBound(int[] values, int x)
            {
                int lo = 0, hi = values.Length;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (values[mid] < x)
                        lo = mid + 1;
                    else
                        hi = mid;
                }

                return lo;
            }
        }
    }
}
