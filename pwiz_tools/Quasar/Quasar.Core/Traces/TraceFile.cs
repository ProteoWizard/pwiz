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

using System.Collections.Generic;
using System.IO;
using pwiz.Quasar.Core.Clustering;

namespace pwiz.Quasar.Core.Traces
{
    /// <summary>
    /// One window's Stage 1 output in a compact binary file: the scan times and every trace,
    /// stored sparsely (only its non-zero points). Lets clustering be rerun, timed and tuned
    /// without reading the run again.
    /// </summary>
    public static class TraceFile
    {
        private const uint MAGIC = 0x51545246; // "QTRF"
        private const int VERSION = 1;

        /// <summary>Writes a window's scan times and traces.</summary>
        public static void Write(string path, double target, double lowerOffset, double upperOffset, double[] rt,
            IReadOnlyList<Trace> traces)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(MAGIC);
                writer.Write(VERSION);
                writer.Write(target);
                writer.Write(lowerOffset);
                writer.Write(upperOffset);
                writer.Write(rt.Length);
                foreach (double t in rt)
                    writer.Write(t);
                writer.Write(traces.Count);
                foreach (var trace in traces)
                {
                    writer.Write(trace.Id);
                    writer.Write(trace.IsAnchor);
                    writer.Write(trace.Mz);
                    writer.Write(trace.Charge);
                    int count = 0;
                    foreach (double v in trace.Intensities)
                    {
                        if (v != 0.0)
                            count++;
                    }

                    writer.Write(count);
                    for (int i = 0; i < trace.Intensities.Length; i++)
                    {
                        if (trace.Intensities[i] == 0.0)
                            continue;
                        writer.Write(i);
                        writer.Write(trace.Intensities[i]);
                    }
                }
            }
        }

        /// <summary>Reads what <see cref="Write"/> wrote.</summary>
        public static (double Target, double LowerOffset, double UpperOffset, double[] Rt, List<Trace> Traces) Read(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != MAGIC || reader.ReadInt32() != VERSION)
                    throw new InvalidDataException(path);
                double target = reader.ReadDouble();
                double lower = reader.ReadDouble();
                double upper = reader.ReadDouble();
                var rt = new double[reader.ReadInt32()];
                for (int i = 0; i < rt.Length; i++)
                    rt[i] = reader.ReadDouble();
                int traceCount = reader.ReadInt32();
                var traces = new List<Trace>(traceCount);
                for (int t = 0; t < traceCount; t++)
                {
                    int id = reader.ReadInt32();
                    bool isAnchor = reader.ReadBoolean();
                    double mz = reader.ReadDouble();
                    int charge = reader.ReadInt32();
                    var intensities = new double[rt.Length];
                    int count = reader.ReadInt32();
                    for (int k = 0; k < count; k++)
                    {
                        int index = reader.ReadInt32();
                        intensities[index] = reader.ReadDouble();
                    }

                    traces.Add(new Trace(id, isAnchor, mz, charge, intensities));
                }

                return (target, lower, upper, rt, traces);
            }
        }
    }
}
