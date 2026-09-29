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
            @" [--sweep-l1 L] [--sweep-l1-z Z] [--sweep-l1-refit] [--block-support-z Z] [--source-positions] [--source-l1 L] [--min-source-fraction F] [--raw] [--profile] [--centroid vendor|events]" +
            @" [--joint] [--joint-z Z] [--joint-relaxed] [--joint-keep-active] [--joint-param Name=Value] [--group-bins N] [--solve-profile]";

        private static int Main(string[] args)
        {
            string input = null, output = null, kernelPath = null;
            bool staggered = false, profile = false, eventCentroids = false, groupBinsSet = false;
            var options = new ScanningDemuxOptions();
            for (int i = 0; i < args.Length; i++)
            {
                // Every option but the switches takes a value.
                string option = args[i];
                bool isSwitch = option == @"--raw" || option == @"--unweighted" || option == @"--position-mz" ||
                    option == @"--source-positions" || option == @"--sweep-l1-refit" || option == @"--profile" ||
                    option == @"--joint" || option == @"--joint-relaxed" || option == @"--solve-profile" ||
                    option == @"--joint-keep-active";
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
                        options.JointParameters.MinOutputIons = options.Parameters.MinOutputIons;
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
                    case @"--sweep-l1":
                        // A lasso weight on each per-sweep solve.
                        options.Parameters.SweepL1 = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case @"--sweep-l1-z":
                        // A lasso on each per-sweep weighted solve of this many noise standard deviations per position.
                        options.Parameters.SweepL1Z = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case @"--block-support-z":
                        // Each channel's positions chosen once per block by a z-scaled lasso on its summed counts.
                        options.Parameters.BlockSupportZ = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case @"--sweep-l1-refit":
                        // Each lasso solve refitted without the penalty on the positions it kept.
                        options.Parameters.SweepL1Refit = true;
                        break;
                    case @"--source-positions":
                        // Each channel's sources placed once per block, then solved per sweep.
                        options.Parameters.SourcePositions = true;
                        break;
                    case @"--source-l1":
                        options.Parameters.SourceL1 = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case @"--min-source-fraction":
                        // Sources under this fraction of their channel's total are dropped.
                        options.Parameters.MinSourceFraction = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case @"--raw":
                        // The selected spectra as acquired, zeros dropped: the control arm.
                        options.Raw = true;
                        break;
                    case @"--profile":
                        // A vendor file read without vendor centroiding, to see what centroiding keeps.
                        profile = true;
                        break;
                    case @"--joint":
                        // Demultiplex and centroid the profile in one solve; reads the profile.
                        options.Joint = true;
                        profile = true;
                        break;
                    case @"--joint-z":
                        options.JointParameters.L1Z = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case @"--joint-relaxed":
                        options.JointParameters.Relaxed = true;
                        break;
                    case @"--group-bins":
                        // Encoded bins whose output one block owns (each block also solves its context).
                        options.GroupBins = int.Parse(value, CultureInfo.InvariantCulture);
                        groupBinsSet = true;
                        break;
                    case @"--joint-param":
                        // Any scalar setting of the joint solve by its property name, for tuning.
                        if (!SetJointParameter(options.JointParameters, value))
                        {
                            Console.Error.WriteLine(@"Unknown joint setting: {0}", value);
                            return 1;
                        }
                        break;
                    case @"--joint-keep-active":
                        // Keep coefficients that reach zero in the active set for the rest of the round.
                        options.JointParameters.PruneActive = false;
                        break;
                    case @"--solve-profile":
                        // Where the joint solve's time goes, summed over threads, logged at the end.
                        JointDemuxProfile.Enabled = true;
                        break;
                    case @"--centroid":
                        // events: the profile centroided keeping every single ion event; vendor: the
                        // vendor library's centroids (the default for a vendor file).
                        eventCentroids = value == @"events";
                        break;
                    default:
                        Console.Error.WriteLine(USAGE);
                        return 1;
                }
            }
            // The joint solve's cost grows with the positions it solves, context included, and each position
            // is local: larger blocks solve less context per bin kept (48 bins keep 48 of 68 positions, 16
            // keep 16 of 36) and leave fewer block edges.
            if (options.Joint && !groupBinsSet)
                options.GroupBins = 48;
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
            Console.WriteLine(@"Solve: {0}", options.Joint
                ? string.Format(CultureInfo.InvariantCulture, @"joint profile demux and centroiding, L1 z {0}{1}, min-out {2}",
                    options.JointParameters.L1Z, options.JointParameters.Relaxed ? @", relaxed" : string.Empty,
                    options.JointParameters.MinOutputIons)
                : SolveSettings(options.Parameters));

            // pwiz-sharp's default reader list holds only the open formats; vendor readers are
            // appended, as Osprey's own reader does on load.
            ReaderList.AdditionalReaders.Add(new Pwiz.Vendor.Sciex.Reader_Sciex());
            ReaderList.AdditionalReaders.Add(new Pwiz.Vendor.Thermo.Reader_Thermo());
            var msd = new MSData();
            ReaderList.Default.Read(input, msd);
            var spectra = msd.Run.SpectrumList;
            Console.WriteLine(@"Opened {0} spectra in {1:F0} s", spectra.Count, stopwatch.Elapsed.TotalSeconds);
            if (eventCentroids)
            {
                // MS2 from the profile as acquired, each run of adjacent points one peak. MS1 keeps the
                // vendor's centroids: dense survey scans need a peak model, which this grouping lacks.
                if (SpectrumList_PeakPicker.SupportsVendorPeakPicking(input))
                    spectra = new SpectrumList_PeakPicker(spectra, null, true, @"1");
                spectra = new SpectrumList_PeakPicker(spectra, new EventPeakDetector(), false, @"2-");
                Console.WriteLine(@"Event centroiding (MS2; MS1 vendor): {0}", input);
            }
            else if (options.Joint && SpectrumList_PeakPicker.SupportsVendorPeakPicking(input))
            {
                // The joint solve centroids MS2 itself, from the profile; MS1 passes through, so it takes the
                // vendor's centroids, as the channel solve's input has them.
                spectra = new SpectrumList_PeakPicker(spectra, null, true, @"1");
                Console.WriteLine(@"Vendor centroiding (MS1; MS2 profile for the joint solve): {0}", input);
            }
            else if (options.Joint)
            {
                Console.WriteLine(@"Warning: MS1 passes through as read (profile, for profile input); only a vendor file is centroided.");
            }
            else if (!profile && SpectrumList_PeakPicker.SupportsVendorPeakPicking(input))
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
            if (JointDemuxProfile.Enabled)
                Console.WriteLine(JointDemuxProfile.Summary());
            return 0;
        }

        /// <summary>Sets a scalar property of the joint solve's settings from Name=Value.</summary>
        private static bool SetJointParameter(JointDemuxParams parameters, string assignment)
        {
            int equals = assignment.IndexOf('=');
            if (equals <= 0)
                return false;
            var property = typeof(JointDemuxParams).GetProperty(assignment.Substring(0, equals));
            if (property == null || !property.CanWrite || property.PropertyType.IsArray)
                return false;
            property.SetValue(parameters, Convert.ChangeType(assignment.Substring(equals + 1), property.PropertyType,
                CultureInfo.InvariantCulture));
            Console.WriteLine(@"Joint setting: {0}", assignment);
            return true;
        }

        /// <summary>The solve's settings, so each run's log records what produced its file.</summary>
        private static string SolveSettings(ScanningDemuxParams parameters)
        {
            var settings = string.Format(CultureInfo.InvariantCulture, @"min-out {0}{1}", parameters.MinOutputIons,
                parameters.PositionMz ? @", position m/z" : string.Empty);
            if (parameters.SourcePositions)
            {
                settings += string.Format(CultureInfo.InvariantCulture, @", source positions (L1 {0}, min fraction {1})",
                    parameters.SourceL1, parameters.MinSourceFraction);
            }
            else if (parameters.SweepL1 > 0 || parameters.SweepL1Z > 0)
            {
                settings += string.Format(CultureInfo.InvariantCulture, @", sweep L1 {0}, L1 z {1}{2}", parameters.SweepL1,
                    parameters.SweepL1Z, parameters.SweepL1Refit ? @", refit" : string.Empty);
            }
            if (parameters.BlockSupportZ > 0 && !parameters.SourcePositions)
                settings += string.Format(CultureInfo.InvariantCulture, @", block support z {0}", parameters.BlockSupportZ);
            return settings;
        }
    }
}
