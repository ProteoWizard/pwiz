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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// The training runs' retention times on one scale: each run's monotone map from its minutes to the hydrophobic
    /// index (HI) of a reference model (fitted by <see cref="RtMapFit"/>), and the way back to minutes, the pointwise
    /// median of the runs' inverse maps, so a library's minutes are the middle of the training runs at every point of
    /// the gradient. Runs on different gradients have no such middle (<see cref="IsWide"/>); a library then takes one
    /// run's minutes (<see cref="ToRunMinutes"/>). Kept beside the models as <see cref="ModelFiles.RT_MAPS"/>.
    /// </summary>
    public sealed class RtAlignment
    {
        public const string FORMAT = @"carafesharp-rt-maps-1";

        /// <summary>
        /// The 95th-percentile distance of the runs from their median, as a fraction of the median gradient's span, past
        /// which they are taken for different gradients (<see cref="IsWide"/>): 1.1 min on a 22-min span. Replicates of
        /// one method stay well inside it (Astral's three runs 0.087 min, 0.4%), as does a run 5% slower and 0.3 min late;
        /// an 11-min and a 24-min gradient are 6.4 min apart (30%).
        /// </summary>
        public const double WIDE_SPREAD_FRACTION = 0.05;

        public static RtAlignment Read(string path)
        {
            using (var document = JsonDocument.Parse(File.ReadAllBytes(path)))
            {
                var root = document.RootElement;
                string format = root.GetProperty(@"format").GetString();
                if (format != FORMAT)
                    throw new InvalidDataException(string.Format(@"{0} has RT map format {1}; this CarafeSharp reads {2}.", path, format, FORMAT));
                var runs = root.GetProperty(@"runs").EnumerateArray()
                    .Select(r => (r.GetProperty(@"run").GetString(), new MonotoneMap(ReadArray(r, @"minutes"), ReadArray(r, @"hi")),
                        r.TryGetProperty(@"fit", out var fit) ? fit.GetString() : null))
                    .ToArray();
                var median = root.GetProperty(@"hi_to_minutes");
                double rtMax = root.TryGetProperty(@"rt_max", out var normalizer) ? ReadNumber(normalizer) : double.NaN;
                return new RtAlignment(runs, root.GetProperty(@"reference").GetString(), rtMax,
                    new MonotoneMap(ReadArray(median, @"hi"), ReadArray(median, @"minutes")));
            }
        }

        private readonly MonotoneMap[] _hiToRunMinutes;

        /// <param name="runs">Each training run's name, its map from minutes to HI, and how the map was fitted
        /// (<see cref="RtMapFit"/>; null where not recorded).</param>
        /// <param name="reference">What predicted the HI the runs were aligned to (the model and its version).</param>
        /// <param name="rtMax">The normalizer the training divided its rows' minutes by (<see cref="RtMax"/>).</param>
        public RtAlignment(IReadOnlyList<(string Run, MonotoneMap MinutesToHi, string Fit)> runs, string reference, double rtMax = double.NaN)
            : this(runs, reference, rtMax, null)
        {
        }

        private RtAlignment(IReadOnlyList<(string Run, MonotoneMap MinutesToHi, string Fit)> runs, string reference, double rtMax,
            MonotoneMap hiToMinutes)
        {
            if (runs.Count == 0)
                throw new ArgumentException(@"An RT alignment needs at least one run.");
            Runs = runs.ToArray();
            Reference = reference;
            RtMax = rtMax;
            _hiToRunMinutes = runs.Select(r => r.MinutesToHi.Invert()).ToArray();
            HiToMinutes = hiToMinutes ?? MonotoneMap.PointwiseMedian(_hiToRunMinutes);
            (SpreadMax, SpreadP95) = Spread(_hiToRunMinutes, HiToMinutes);
        }

        /// <summary>Each training run's name, its map from minutes to HI, and how the map was fitted (null where not recorded).</summary>
        public IReadOnlyList<(string Run, MonotoneMap MinutesToHi, string Fit)> Runs { get; }

        public string Reference { get; }

        /// <summary>
        /// The normalizer the training divided its rows' minutes by, the largest run's rt_max: a model fine-tuned on the
        /// runs' median minutes predicts them divided by it. NaN when not recorded (maps written before it was).
        /// </summary>
        public double RtMax { get; }

        /// <summary>From HI to minutes: the pointwise median of the runs' inverse maps, extended past their ends.</summary>
        public MonotoneMap HiToMinutes { get; }

        /// <summary>
        /// How far the runs are from the median, in minutes: the largest, and the 95th percentile, of the farthest run's
        /// distance at each point of the HI range every run covers. 0 for one run; NaN when the runs share no HI range.
        /// </summary>
        public double SpreadMax { get; }

        public double SpreadP95 { get; }

        /// <summary>The minutes the median map spans, from its first knot to its last.</summary>
        public double MinutesSpan
        {
            get { return HiToMinutes.Y[HiToMinutes.Y.Count - 1] - HiToMinutes.Y[0]; }
        }

        /// <summary>
        /// The runs are too far from their median (<see cref="WIDE_SPREAD_FRACTION"/> of its span) to be one gradient, or
        /// share no HI range at all, so the median's minutes belong to none of them.
        /// </summary>
        public bool IsWide
        {
            get { return !(SpreadP95 <= WIDE_SPREAD_FRACTION * MinutesSpan); }
        }

        /// <summary>A run's map, by name.</summary>
        public MonotoneMap GetRun(string run)
        {
            return Runs[IndexOf(run)].MinutesToHi;
        }

        /// <summary>
        /// The run <paramref name="name"/> names (<c>-rt_reference</c>): the run of that name, else the one run whose
        /// name ends with it, as <c>_55</c> names <c>..._400-900_55</c>. Throws, listing the runs, for none or several.
        /// </summary>
        public string FindRun(string name)
        {
            return FindRun(Runs.Select(r => r.Run).ToArray(), name);
        }

        /// <summary><see cref="FindRun(string)"/> among run names known before the runs are aligned.</summary>
        public static string FindRun(IReadOnlyList<string> runs, string name)
        {
            if (runs.Contains(name, StringComparer.Ordinal))
                return name;
            var ending = runs.Where(n => n.EndsWith(name, StringComparison.Ordinal)).ToArray();
            if (ending.Length == 1)
                return ending[0];
            throw new ArgumentException(string.Format(@"-rt_reference {0} names {1} of the training runs: {2}.", name,
                ending.Length == 0 ? @"none" : @"several", string.Join(@", ", runs)));
        }

        /// <summary>The median minutes of an HI, extended past the runs' range rather than clipped.</summary>
        public double ToMinutes(double hi)
        {
            return HiToMinutes.Extend(hi);
        }

        /// <summary>One run's minutes of an HI, by the inverse of its own map, extended past its range rather than clipped.</summary>
        public Func<double, double> ToRunMinutes(string run)
        {
            var hiToMinutes = _hiToRunMinutes[IndexOf(run)];
            return hi => hiToMinutes.Extend(hi);
        }

        /// <summary>
        /// From the runs' median minutes, which a model trained on runs of one gradient predicts, to one run's: back to
        /// HI by the median map inverted, then by the run's map inverted, each extended past its range.
        /// </summary>
        public Func<double, double> MedianToRunMinutes(string run)
        {
            var medianToHi = HiToMinutes.Invert();
            var toRunMinutes = ToRunMinutes(run);
            return minutes => toRunMinutes(medianToHi.Extend(minutes));
        }

        public override string ToString()
        {
            string spread = double.IsNaN(SpreadP95)
                ? @"runs that share no HI range"
                : string.Format(CultureInfo.InvariantCulture, @"runs from the median: at most {0:F3} min, 95% within {1:F3} min", SpreadMax, SpreadP95);
            return string.Format(CultureInfo.InvariantCulture, @"{0} runs onto {1}; {2}{3}", Runs.Count, Reference, spread,
                IsWide ? @" (different gradients)" : string.Empty);
        }

        public void Write(string path)
        {
            using (var stream = File.Create(path))
            using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                json.WriteStartObject();
                json.WriteString(@"format", FORMAT);
                json.WriteString(@"reference", Reference);
                WriteNumber(json, @"rt_max", RtMax);
                json.WriteStartArray(@"runs");
                foreach (var (run, map, fit) in Runs)
                {
                    json.WriteStartObject();
                    json.WriteString(@"run", run);
                    if (fit != null)
                        json.WriteString(@"fit", fit);
                    WriteArray(json, @"minutes", map.X);
                    WriteArray(json, @"hi", map.Y);
                    json.WriteEndObject();
                }
                json.WriteEndArray();
                json.WriteStartObject(@"hi_to_minutes");
                WriteArray(json, @"hi", HiToMinutes.X);
                WriteArray(json, @"minutes", HiToMinutes.Y);
                json.WriteEndObject();
                json.WriteStartObject(@"spread_minutes");
                WriteNumber(json, @"max", SpreadMax);
                WriteNumber(json, @"p95", SpreadP95);
                json.WriteEndObject();
                json.WriteEndObject();
            }
        }

        private int IndexOf(string run)
        {
            for (int i = 0; i < Runs.Count; i++)
            {
                if (string.Equals(Runs[i].Run, run, StringComparison.Ordinal))
                    return i;
            }
            throw new KeyNotFoundException(string.Format(@"The RT alignment has no run {0}.", run));
        }

        private static (double Max, double P95) Spread(IReadOnlyList<MonotoneMap> inverses, MonotoneMap median)
        {
            if (inverses.Count < 2)
                return (0, 0);
            double from = inverses.Max(m => m.X[0]), to = inverses.Min(m => m.X[m.X.Count - 1]);
            var distances = new List<double>();
            foreach (double hi in median.X)
            {
                if (hi < from || hi > to)
                    continue;
                double minutes = median.Extend(hi);
                distances.Add(inverses.Max(m => Math.Abs(m.Extend(hi) - minutes)));
            }
            if (distances.Count == 0)
                return (double.NaN, double.NaN);
            distances.Sort();
            return (distances[distances.Count - 1], distances[(int)Math.Min(distances.Count - 1, Math.Ceiling(0.95 * distances.Count) - 1)]);
        }

        /// <summary>A number, or null for NaN, which JSON cannot hold.</summary>
        private static void WriteNumber(Utf8JsonWriter json, string name, double value)
        {
            if (double.IsFinite(value))
                json.WriteNumber(name, value);
            else
                json.WriteNull(name);
        }

        private static double ReadNumber(JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Number ? element.GetDouble() : double.NaN;
        }

        private static void WriteArray(Utf8JsonWriter json, string name, IReadOnlyList<double> values)
        {
            json.WriteStartArray(name);
            foreach (double value in values)
                json.WriteNumberValue(value);
            json.WriteEndArray();
        }

        private static double[] ReadArray(JsonElement element, string name)
        {
            return element.GetProperty(name).EnumerateArray().Select(v => v.GetDouble()).ToArray();
        }
    }
}
