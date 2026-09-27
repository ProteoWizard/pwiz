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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Readers;
using pwiz.Osprey.Demux;

namespace pwiz.Osprey.DemuxTool
{
    /// <summary>
    /// Writes a demultiplexed copy of a scanning-quadrupole (SCIEX ZT Scan) mzML, for evaluating
    /// the demultiplexing with any search engine before Osprey reads the data itself.
    /// </summary>
    internal static class Program
    {
        private const string USAGE =
            @"Usage: Osprey.DemuxTool --in <run.mzML> --out <demux.mzML> --kernel <profile.tsv>" +
            @" [--layout centered:5|tiled:5] [--threads N] [--cycles first:last] [--mz low:high] [--ppm P]" +
            @" [--counts-per-ion C] [--unweighted] [--raw]";

        private static int Main(string[] args)
        {
            string input = null, output = null, kernelPath = null;
            var options = new ScanningDemuxOptions();
            for (int i = 0; i < args.Length; i++)
            {
                // Every option but the switches takes a value.
                string option = args[i];
                bool isSwitch = option == @"--raw" || option == @"--unweighted";
                if (!isSwitch && i + 1 >= args.Length)
                {
                    Console.Error.WriteLine(USAGE);
                    return 1;
                }
                string value = isSwitch ? string.Empty : args[++i];
                switch (option)
                {
                    case @"--in":
                        input = value;
                        break;
                    case @"--out":
                        output = value;
                        break;
                    case @"--kernel":
                        kernelPath = value;
                        break;
                    case @"--layout":
                        options.Layout = ScanningLayout.Parse(value);
                        break;
                    case @"--threads":
                        options.Threads = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case @"--ppm":
                        options.Parameters.ChannelTolerancePpm = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case @"--counts-per-ion":
                        options.CountsPerIon = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case @"--cycles":
                        string[] cycles = value.Split(':');
                        options.FirstCycle = int.Parse(cycles[0], CultureInfo.InvariantCulture);
                        options.LastCycle = int.Parse(cycles[1], CultureInfo.InvariantCulture);
                        break;
                    case @"--mz":
                        string[] range = value.Split(':');
                        options.MinMz = double.Parse(range[0], CultureInfo.InvariantCulture);
                        options.MaxMz = double.Parse(range[1], CultureInfo.InvariantCulture);
                        break;
                    case @"--unweighted":
                        options.Parameters.PoissonWeights = false;
                        break;
                    case @"--raw":
                        // The selected spectra as acquired, zeros dropped: the control arm.
                        options.Raw = true;
                        break;
                    default:
                        Console.Error.WriteLine(USAGE);
                        return 1;
                }
            }
            if (input == null || output == null || kernelPath == null)
            {
                Console.Error.WriteLine(USAGE);
                return 1;
            }

            var stopwatch = Stopwatch.StartNew();
            var kernel = ScanningKernel.Load(kernelPath);
            Console.WriteLine(@"Kernel: {0}", kernel.Descriptor);
            Console.WriteLine(@"Layout: {0}{1}", options.Raw ? @"raw" : options.Layout.Name,
                options.Parameters.PoissonWeights ? string.Empty : @", unweighted");

            var msd = new MSData();
            ReaderList.Default.Read(input, msd);
            var demux = new ScanningDemuxSpectrumList(msd.Run.SpectrumList, kernel, options, Console.Out);
            msd.Run.SpectrumList = demux;
            Console.WriteLine(@"Indexed {0} sweeps in {1:F0} s; writing sweeps {2}-{3}, {4} spectra", demux.CycleCount,
                stopwatch.Elapsed.TotalSeconds, demux.FirstCycle, demux.LastCycle, demux.Count);

            var config = new WriteConfig { Format = WriteFormat.Mzml };
            config.EncoderConfig.Precision = BinaryPrecision.Bits32;
            config.EncoderConfig.PrecisionOverrides[CVID.MS_m_z_array] = BinaryPrecision.Bits64;
            config.EncoderConfig.PrecisionOverrides[CVID.MS_time_array] = BinaryPrecision.Bits64;
            config.EncoderConfig.Compression = BinaryCompression.Zlib;
            string partial = output + @".partial";
            MSDataFile.Write(msd, partial, config);
            File.Move(partial, output, true);

            Console.WriteLine(@"Wrote {0} in {1:F0} s: {2:N0} channels, {3:N0} solved; {4:P2} of {5:E3} ions passed through",
                output, stopwatch.Elapsed.TotalSeconds, demux.Channels, demux.ChannelsSolved,
                demux.IonsPassedThrough / Math.Max(demux.IonsIn, 1e-30), demux.IonsIn);
            return 0;
        }
    }
}
