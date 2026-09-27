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
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Readers;
using pwiz.Osprey.Demux;

namespace pwiz.Osprey.DemuxTool
{
    /// <summary>
    /// Writes a demultiplexed mzML of a scanning-quadrupole (SCIEX ZT Scan) or stepped staggered
    /// acquisition, read from the vendor file (.wiff2 or .raw, vendor-centroided) or from an mzML,
    /// for evaluating the demultiplexing with any search engine before Osprey reads the data itself.
    /// </summary>
    internal static class Program
    {
        private const string USAGE =
            @"Usage: Osprey.DemuxTool --in <run.wiff2|.raw|.mzML> --out <demux.mzML> [--scheme scanning|staggered]" +
            @" [--kernel <profile.tsv>] [--layout centered:k|tiled:k|framed:k:m] [--threads N] [--cycles first:last]" +
            @" [--mz low:high] [--ppm P] [--counts-per-ion C] [--min-out I] [--apportion H] [--position-mz] [--unweighted]" +
            @" [--raw]";

        private static int Main(string[] args)
        {
            string input = null, output = null, kernelPath = null;
            bool staggered = false;
            var options = new ScanningDemuxOptions();
            for (int i = 0; i < args.Length; i++)
            {
                // Every option but the switches takes a value.
                string option = args[i];
                bool isSwitch = option == @"--raw" || option == @"--unweighted" || option == @"--position-mz";
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
                    case @"--scheme":
                        // scanning: SCIEX ZT Scan (needs --kernel); staggered: stepped overlapping windows.
                        staggered = value == @"staggered";
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
                    case @"--min-out":
                        options.Parameters.MinOutputIons = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case @"--apportion":
                        // Each observed peak scaled by the share of its signal within this many
                        // positions of its own bin; one spectrum per bin, so the layout is centered:1.
                        options.Parameters.ApportionHalfWidth = int.Parse(value, CultureInfo.InvariantCulture);
                        options.Layout = new ScanningLayout(ScanningLayoutKind.centered, 1);
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
                    case @"--position-mz":
                        // Each solved value at the m/z of the peaks it was solved from, in its sweep.
                        options.Parameters.PositionMz = true;
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
            if (input == null || output == null || (kernelPath == null && !staggered))
            {
                Console.Error.WriteLine(USAGE);
                return 1;
            }

            var stopwatch = Stopwatch.StartNew();
            var kernel = staggered ? null : ScanningKernel.Load(kernelPath);
            if (kernel != null)
                Console.WriteLine(@"Kernel: {0}", kernel.Descriptor);
            Console.WriteLine(@"Layout: {0}{1}", staggered ? @"staggered bins" : options.Raw ? @"raw" : options.Layout.Name,
                options.Parameters.PoissonWeights ? string.Empty : @", unweighted");

            // pwiz-sharp's default reader list holds only the open formats; vendor readers are
            // appended, as Osprey's own reader does on load.
            ReaderList.AdditionalReaders.Add(new Pwiz.Vendor.Sciex.Reader_Sciex());
            ReaderList.AdditionalReaders.Add(new Pwiz.Vendor.Thermo.Reader_Thermo());
            var msd = new MSData();
            ReaderList.Default.Read(input, msd);
            var spectra = msd.Run.SpectrumList;
            Console.WriteLine(@"Opened {0} spectra in {1:F0} s", spectra.Count, stopwatch.Elapsed.TotalSeconds);
            if (SpectrumList_PeakPicker.SupportsVendorPeakPicking(input))
            {
                // A vendor file (.wiff2) is read directly, centroided by the vendor library as
                // msconvert's "peakPicking vendor msLevel=1-" does; with no fallback detector a
                // reader that cannot centroid fails rather than handing profile data on.
                spectra = new SpectrumList_PeakPicker(spectra, null, true, @"1-");
                Console.WriteLine(@"Vendor centroiding: {0}", input);
            }
            ScanningDemuxSpectrumList scanning = null;
            StaggeredDemuxSpectrumList stepped = null;
            if (staggered)
            {
                stepped = new StaggeredDemuxSpectrumList(spectra, options, Console.Out);
                msd.Run.SpectrumList = stepped;
                Console.WriteLine(@"Indexed in {0:F0} s; writing {1} spectra", stopwatch.Elapsed.TotalSeconds, stepped.Count);
            }
            else
            {
                scanning = new ScanningDemuxSpectrumList(spectra, kernel, options, Console.Out);
                msd.Run.SpectrumList = scanning;
                Console.WriteLine(@"Indexed {0} sweeps in {1:F0} s; writing sweeps {2}-{3}, {4} spectra",
                    scanning.CycleCount, stopwatch.Elapsed.TotalSeconds, scanning.FirstCycle, scanning.LastCycle,
                    scanning.Count);
            }

            var config = new WriteConfig { Format = WriteFormat.Mzml };
            config.EncoderConfig.Precision = BinaryPrecision.Bits32;
            config.EncoderConfig.PrecisionOverrides[CVID.MS_m_z_array] = BinaryPrecision.Bits64;
            config.EncoderConfig.PrecisionOverrides[CVID.MS_time_array] = BinaryPrecision.Bits64;
            config.EncoderConfig.Compression = BinaryCompression.Zlib;
            string partial = output + @".partial";
            MSDataFile.Write(msd, partial, config);
            File.Move(partial, output, true);

            long channels = stepped?.Channels ?? scanning.Channels;
            long solved = stepped?.ChannelsSolved ?? scanning.ChannelsSolved;
            double ionsIn = stepped?.IonsIn ?? scanning.IonsIn;
            double passed = stepped?.IonsPassedThrough ?? scanning.IonsPassedThrough;
            Console.WriteLine(@"Wrote {0} in {1:F0} s: {2:N0} channels, {3:N0} solved; {4:P2} of {5:E3} ions passed through",
                output, stopwatch.Elapsed.TotalSeconds, channels, solved, passed / Math.Max(ionsIn, 1e-30), ionsIn);
            return 0;
        }
    }
}
