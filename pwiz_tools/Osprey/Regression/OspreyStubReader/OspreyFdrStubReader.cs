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
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Regression
{
    /// <summary>
    /// Reads the run-scope records a run keeps in its stub scores parquets for the regression
    /// harness (Regression\FdrSidecars.ps1), through Osprey.IO as Osprey reads them. A separate
    /// process, because the harness's PowerShell may run on an older .NET than Osprey's.
    /// <para>
    /// Usage: <c>OspreyStubReader info &lt;list&gt; &lt;outDir&gt;</c> or
    /// <c>OspreyStubReader read &lt;list&gt; &lt;outDir&gt; &lt;pass&gt;</c>. The list is a UTF-8
    /// file of one path per line; the answer is <c>&lt;outDir&gt;\result.json</c>, an array with
    /// one object per path in list order. <c>info</c> answers HasScores and ProducedBy for
    /// parquet paths. <c>read</c> answers, for sidecar paths, the Source Osprey reads the records
    /// from and, when that is not the path itself, the records re-encoded as a v9 run-scope image
    /// written to an Image file in outDir, or the Problem that kept them from being read.
    /// </para>
    /// </summary>
    public static class OspreyFdrStubReader
    {
        private const int HEADER_LEN = 32;
        private const int RECORD_LEN = 36;
        private const byte VERSION = 9;
        private static readonly byte[] MAGIC =
            { (byte)'O', (byte)'S', (byte)'P', (byte)'R', (byte)'Y', (byte)'F', (byte)'D', (byte)'R' };

        public static int Main(string[] args)
        {
            bool isInfo = args.Length == 3 && args[0] == @"info";
            bool isRead = args.Length == 4 && args[0] == @"read";
            if (!isInfo && !isRead)
            {
                Console.Error.WriteLine(@"Usage: OspreyStubReader info <list> <outDir> | read <list> <outDir> <pass>");
                return 2;
            }
            string[] paths = File.ReadAllLines(args[1], Encoding.UTF8);
            string outDir = args[2];
            Directory.CreateDirectory(outDir);
            using (var stream = File.Create(Path.Combine(outDir, @"result.json")))
            using (var json = new Utf8JsonWriter(stream))
            {
                json.WriteStartArray();
                if (isInfo)
                    WriteInfo(json, paths);
                else
                    WriteRead(json, paths, outDir, int.Parse(args[3], CultureInfo.InvariantCulture));
                json.WriteEndArray();
            }
            return 0;
        }

        /// The file Osprey reads <paramref name="sidecarPath"/>'s records from: its stub scores
        /// parquet when that holds them, else the path itself.
        public static string ReadablePath(string sidecarPath, int pass)
        {
            return FdrScoresSidecar.ReadablePath(sidecarPath, (FdrScoresSidecar.Pass)pass);
        }

        /// Whether <paramref name="parquetPath"/> is a parquet that carries a run's scores.
        public static bool HasScores(string parquetPath)
        {
            return ParquetScoreCache.HasScores(parquetPath);
        }

        /// The task named in the validity stamp of the parquet at <paramref name="parquetPath"/>,
        /// or null when it has none.
        public static string ProducedBy(string parquetPath)
        {
            var stamp = ParquetScoreCache.ReadStamp(parquetPath);
            return stamp == null ? null : stamp.Task;
        }

        /// The run's records of <paramref name="pass"/>, read as Osprey reads them, encoded as a
        /// v9 run-scope image with no feature sums and no stamp; null with a problem when unreadable.
        public static byte[] ReadAsSidecar(string sidecarPath, int pass, out string problem)
        {
            problem = null;
            var records = new List<FdrScoreRecord>();
            bool ok;
            try
            {
                ok = FdrScoresSidecar.ReadRecords(sidecarPath, (FdrScoresSidecar.Pass)pass, records.Add);
            }
            catch (Exception ex)
            {
                problem = string.Format(@"could not be read: {0}", ex.Message);
                return null;
            }
            if (!ok)
            {
                string source = ReadablePath(sidecarPath, pass);
                problem = File.Exists(source)
                    ? string.Format(@"{0} is not readable as pass-{1} records", Path.GetFileName(source), pass)
                    : @"neither the sidecar nor a stub scores parquet holding its records exists";
                return null;
            }
            var data = new byte[HEADER_LEN + (long)records.Count * RECORD_LEN];
            Array.Copy(MAGIC, data, MAGIC.Length);
            data[8] = VERSION;
            data[9] = (byte)pass;
            BitConverter.GetBytes((ulong)records.Count).CopyTo(data, 16);
            int off = HEADER_LEN;
            foreach (var r in records)
            {
                BitConverter.GetBytes(r.EntryId).CopyTo(data, off);
                BitConverter.GetBytes(r.Score).CopyTo(data, off + 4);
                BitConverter.GetBytes(r.RunPrecursorQvalue).CopyTo(data, off + 12);
                BitConverter.GetBytes(r.RunPeptideQvalue).CopyTo(data, off + 20);
                BitConverter.GetBytes(r.ApexRt).CopyTo(data, off + 28);
                off += RECORD_LEN;
            }
            return data;
        }

        private static void WriteInfo(Utf8JsonWriter json, IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                json.WriteStartObject();
                json.WriteString(@"Path", path);
                json.WriteBoolean(@"HasScores", HasScores(path));
                json.WriteString(@"ProducedBy", ProducedBy(path));
                json.WriteEndObject();
            }
        }

        private static void WriteRead(Utf8JsonWriter json, IList<string> paths, string outDir, int pass)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                string source = ReadablePath(path, pass);
                string image = null;
                string problem = null;
                // The harness reads a binary sidecar Osprey would read as itself, so only a path
                // whose records live elsewhere is read here.
                if (!string.Equals(source, path, StringComparison.OrdinalIgnoreCase))
                {
                    byte[] data = ReadAsSidecar(path, pass, out problem);
                    if (data != null)
                    {
                        image = Path.Combine(outDir, i.ToString(CultureInfo.InvariantCulture) + @".bin");
                        File.WriteAllBytes(image, data);
                    }
                }
                json.WriteStartObject();
                json.WriteString(@"Path", path);
                json.WriteString(@"Source", source);
                json.WriteString(@"Image", image);
                json.WriteString(@"Problem", problem);
                json.WriteEndObject();
            }
        }
    }
}
