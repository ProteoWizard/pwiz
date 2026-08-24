using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Pwiz.Analysis;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Vendor.Bruker;
using Pwiz.Vendor.Thermo;
using Pwiz.Vendor.Waters;

namespace Pwiz.Tools.MsBenchmark;

/// <summary>
/// Read-only benchmark for MSData readers. Port of cpp <c>pwiz_tools/examples/msbenchmark</c>.
/// CLI mirrors the cpp tool's positional shape: <c>msbenchmark-sharp &lt;mode&gt; &lt;detail&gt;
/// &lt;file&gt; [--filter SPEC ...]</c>.
/// </summary>
/// <remarks>
/// Designed for comparing reader implementations under realistic filter chains. With e.g.
/// <c>--filter "index [5000,5099]"</c> on a large indexed mzML, a lazy reader only parses
/// 100 spectra; an eager reader parses them all then keeps them in memory for the filter
/// to pick from. The wall + memory delta isolates eager vs lazy cost.
/// </remarks>
public static class Program
{
#pragma warning disable CS1591 // Main is the entry point; no xmldoc needed.
    private const string Usage =
        "Usage: msbenchmark-sharp <spectra|chromatograms|rampadapter|ramp> "
        + "<full-data|full-metadata|fast-metadata|instant-metadata> <filename> "
        + "[--filter <filter name> <options>] [another filter] [optional flags]\n\n"
        + "Iterates over a file's spectra to test reader speed. Detail levels match cpp's:\n"
        + "  full-data        = binary       — full spectrum incl. mz/intensity arrays\n"
        + "  full-metadata    = no-binary    — full metadata, no binary array decode\n"
        + "  fast-metadata                   — partial metadata (skip param-heavy sub-elements)\n"
        + "  instant-metadata                — identity only (id+index, no per-spectrum parse)\n\n"
        + "Optional flags:\n"
        + "  --reverse                  iterate backwards\n"
        + "  --loop                     repeat indefinitely\n"
        + "  --combineIonMobilitySpectra (Bruker / Waters IM combine)\n\n"
        + "Modes other than \"spectra\" are not yet implemented in the C# port — they exist\n"
        + "for CLI parity with cpp msbenchmark; using one yields an error.\n";

    public static int Main(string[] args)
    {
        if (args.Length < 3 || args[0] is "-h" or "--help")
        {
            Console.Error.Write(Usage);
            return args.Length < 3 ? 2 : 0;
        }

        string mode = args[0];
        if (mode is not "spectra")
        {
            // chromatograms / rampadapter / ramp: not yet implemented; surface a clear error
            // rather than silently succeeding so CI-style differential benches catch the gap.
            Console.Error.WriteLine($"[msbenchmark-sharp] mode '{mode}' not implemented (cpp parity only — supported: spectra)");
            return 2;
        }

        DetailLevel detail = args[1] switch
        {
            "binary" or "full-data" => DetailLevel.FullData,
            "no-binary" or "full-metadata" => DetailLevel.FullMetadata,
            "fast-metadata" => DetailLevel.FastMetadata,
            "instant-metadata" => DetailLevel.InstantMetadata,
            _ => DetailLevel.FullData,
        };
        if (args[1] is not ("binary" or "full-data" or "no-binary" or "full-metadata"
                            or "fast-metadata" or "instant-metadata"))
        {
            Console.Error.WriteLine(
                "[msbenchmark-sharp] Second argument must be one of [full-data, full-metadata, fast-metadata, instant-metadata]");
            return 2;
        }

        string filename = args[2];
        if (!File.Exists(filename))
        {
            Console.Error.WriteLine($"input file not found: {filename}");
            return 2;
        }

        var filters = new List<string>();
        bool reverse = false;
        bool loop = false;
        bool combineIms = false;

        for (int i = 3; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--filter":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("--filter requires an argument");
                        return 2;
                    }
                    filters.Add(args[++i]);
                    break;
                case "--reverse": reverse = true; break;
                case "--loop": loop = true; break;
                case "--combineIonMobilitySpectra": combineIms = true; break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 2;
            }
        }

        do
        {
            EnumerateSpectra(filename, filters, detail, reverse, combineIms);
        } while (loop);

        return 0;
    }

    private static void EnumerateSpectra(string filename, IList<string> filters, DetailLevel detail,
                                          bool reverse, bool combineIms)
    {
        Console.Out.WriteLine($"Input:   {filename} ({new FileInfo(filename).Length / (1024L * 1024L)} MiB)");
        if (filters.Count > 0) Console.Out.WriteLine($"Filters: {string.Join(" | ", filters)}");
        Console.Out.WriteLine($"Detail:  {detail}");
        Console.Out.WriteLine();

        var readers = BuildReaderList(combineIms);
        using var proc = Process.GetCurrentProcess();

        // Open + filter setup.
        var swTotal = Stopwatch.StartNew();
        using var msd = new MSData();
        readers.Read(filename, msd);
        if (filters.Count > 0 && msd.Run.SpectrumList is not null)
            SpectrumListFactory.Wrap(msd, filters);
        long tOpen = swTotal.ElapsedMilliseconds;

        var sl = msd.Run.SpectrumList;
        if (sl is null)
        {
            Console.Out.WriteLine("No spectrum list in input.");
            return;
        }
        int count = sl.Count;
        Console.Out.WriteLine($"Time to open + setup filters:   {tOpen,8} ms");
        Console.Out.WriteLine($"Spectra in list after filters: {count}");

        // Instant-metadata mode: only touch SpectrumIdentities. For lazy readers that index
        // up front (mzML, mz5, vendors), this is essentially free; for an eager reader it's
        // also free since the identities were built at construction.
        if (detail == DetailLevel.InstantMetadata)
        {
            var swId = Stopwatch.StartNew();
            int identsRead = 0;
            if (reverse)
                for (int i = count - 1; i >= 0; i--) { _ = sl.SpectrumIdentity(i); identsRead++; }
            else
                for (int i = 0; i < count; i++) { _ = sl.SpectrumIdentity(i); identsRead++; }
            Console.Out.WriteLine($"Time to enumerate {identsRead} identities: {swId.ElapsedMilliseconds,8} ms");
        }
        else
        {
            bool getBinary = detail == DetailLevel.FullData;
            var swFirst = Stopwatch.StartNew();
            long totalPoints = 0;
            int firstIdx = reverse ? count - 1 : 0;
            if (count > 0)
            {
                var first = sl.GetSpectrum(firstIdx, getBinary);
                totalPoints += first.DefaultArrayLength;
            }
            long tFirst = swFirst.ElapsedMilliseconds;
            Console.Out.WriteLine($"Time to first spectrum:         {tFirst,8} ms");

            var swEnum = Stopwatch.StartNew();
            int remaining = 0;
            if (reverse)
                for (int i = count - 2; i >= 0; i--) { var s = sl.GetSpectrum(i, getBinary); totalPoints += s.DefaultArrayLength; remaining++; }
            else
                for (int i = 1; i < count; i++) { var s = sl.GetSpectrum(i, getBinary); totalPoints += s.DefaultArrayLength; remaining++; }
            long tEnum = swEnum.ElapsedMilliseconds;
            Console.Out.WriteLine($"Time to enumerate {remaining,6} more:    {tEnum,8} ms");
            Console.Out.WriteLine($"Total data points:              {totalPoints,8}");
        }

        long tWall = swTotal.ElapsedMilliseconds;
        proc.Refresh();
        Console.Out.WriteLine();
        Console.Out.WriteLine($"Total wall:                     {tWall,8} ms");
        Console.Out.WriteLine($"Peak working set:               {proc.PeakWorkingSet64 / (1024 * 1024),8} MiB");
        Console.Out.WriteLine($"Peak paged memory:              {proc.PeakPagedMemorySize64 / (1024 * 1024),8} MiB");
    }

    private static ReaderList BuildReaderList(bool combineIms)
    {
        var readers = ThermoReaderRegistration.CreateDefaultWithThermo();
        readers.Add(new Reader_Bruker { CombineIonMobilitySpectra = combineIms });
        readers.Add(new Reader_Waters());
        readers.Add(new Pwiz.Vendor.Agilent.Reader_Agilent());
        readers.Add(new Pwiz.Vendor.Sciex.Reader_Sciex());
        readers.Add(new Pwiz.Vendor.Shimadzu.Reader_Shimadzu());
        readers.Add(new Pwiz.Vendor.UNIFI.Reader_UNIFI());
        readers.Add(new Pwiz.Vendor.UIMF.Reader_UIMF());
        readers.Add(new Pwiz.Vendor.Mobilion.Reader_Mobilion());
        return readers;
    }
}
