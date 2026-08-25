using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Diff;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Samples;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;
using DiffImpl = Pwiz.Data.Common.Diff.Diff;

namespace Pwiz.Data.MsData.Diff;

/// <summary>
/// MSData-level diff. Produces a human-readable report describing the first
/// <see cref="DiffConfig.MaxDifferencesToReport"/> differences between two <see cref="MSData"/>
/// documents. Port of <c>pwiz::data::Diff&lt;MSData, DiffConfig&gt;</c>.
/// </summary>
/// <remarks>
/// The C++ <c>Diff</c> builds a symmetric "what's in a-not-b / b-not-a" structure. We instead
/// return a flat report — matches how tests use it (assert the string is empty), while keeping
/// the locator path (e.g. <c>run/spectrumList/spectrum[42]/precursorList/precursor[0]/activation</c>)
/// in each line so mismatches are quick to pinpoint.
/// </remarks>
public static class MSDataDiff
{
    /// <summary>
    /// Returns an empty string when <paramref name="a"/> and <paramref name="b"/> are logically
    /// equal under <paramref name="config"/>; otherwise a multi-line report of differences.
    /// </summary>
    public static string Describe(MSData a, MSData b, DiffConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        config ??= new DiffConfig();
        var ctx = new Context(config);

        DiffRoot(a, b, ctx);

        return ctx.Format();
    }

    /// <summary>
    /// Tolerance mode for the <c>msLevel</c> comparison in <see cref="DescribeSpectraDataOnly"/>.
    /// Captures the well-known lossy defaults each peak-list format applies on read.
    /// </summary>
    public enum LossyMsLevelMode
    {
        /// <summary>Strict equality required.</summary>
        None,
        /// <summary>mzXML: an absent <c>msLevel</c> attribute reads back as 1, so 0→1 is a wash.</summary>
        MzxmlDefault,
        /// <summary>MGF: every parsed spectrum is tagged as MS2, so any aMs ≥ 2 round-trips to bMs == 2.</summary>
        MgfFlatten,
    }

    /// <summary>
    /// Compares only the data-bearing parts of two SpectrumLists: count, ms level, and the
    /// m/z + intensity arrays. Skips all metadata and chromatograms. Used by the vendor
    /// test harness to verify a lossy round-trip (mzXML / MGF) preserved the spectrum data
    /// while ignoring metadata that the format doesn't carry.
    /// </summary>
    /// <param name="a">Reference document (typically the freshly read vendor MSData).</param>
    /// <param name="b">Document under test (typically the round-tripped MSData).</param>
    /// <param name="precision">Absolute tolerance for m/z and intensity comparisons.</param>
    /// <param name="msLevelMode">Selects which format-specific msLevel-default lossiness is
    /// tolerated. <see cref="LossyMsLevelMode.None"/> requires strict equality.</param>
    /// <param name="ignorePeakOrder">Compare the peaks as a set rather than as an ordered list,
    /// pairing the two sides on their values. Needed when the round-tripped format drops the
    /// array that gave the peak order its meaning, which is what happens to a combined ion
    /// mobility spectrum written to MGF or mzMLb - the copy read back is repaired into m/z order
    /// while the vendor-read original is deliberately left alone.</param>
    public static string DescribeSpectraDataOnly(MSData a, MSData b, double precision = 1e-6,
        LossyMsLevelMode msLevelMode = LossyMsLevelMode.MzxmlDefault, bool ignorePeakOrder = false)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var ctx = new Context(new DiffConfig { Precision = precision });

        var listA = a.Run.SpectrumList;
        var listB = b.Run.SpectrumList;
        if (listA is null && listB is null) return string.Empty;
        using var _ = ctx.Push("spectrumList");
        int ca = listA?.Count ?? 0;
        int cb = listB?.Count ?? 0;
        if (ca != cb)
        {
            ctx.Report($"spectrum count: {ca} vs {cb}");
            return ctx.Format();
        }
        if (listA is null || listB is null) return ctx.Format();

        for (int i = 0; i < ca && !ctx.Saturated; i++)
        {
            using var __ = ctx.Push("spectrum[" + i + "]");
            var sa = listA.GetSpectrum(i, getBinaryData: true);
            var sb = listB.GetSpectrum(i, getBinaryData: true);
            DiffSpectrumDataOnly(sa, sb, ctx, precision, msLevelMode, ignorePeakOrder);
        }
        return ctx.Format();
    }

    private static void DiffSpectrumDataOnly(Spectrum a, Spectrum b, Context ctx, double precision,
        LossyMsLevelMode msLevelMode, bool ignorePeakOrder)
    {
        int aMs = a.Params.CvParam(CVID.MS_ms_level).ValueAs<int>();
        int bMs = b.Params.CvParam(CVID.MS_ms_level).ValueAs<int>();
        bool tolerated = msLevelMode switch
        {
            LossyMsLevelMode.MzxmlDefault => aMs == 0 && bMs == 1,
            LossyMsLevelMode.MgfFlatten => aMs >= 2 && bMs == 2,
            _ => false,
        };
        if (aMs != bMs && !tolerated)
            ctx.Report($"ms level: {aMs} vs {bMs}");

        var aMz = a.GetMZArray()?.Data;
        var bMz = b.GetMZArray()?.Data;
        var aInt = a.GetIntensityArray()?.Data;
        var bInt = b.GetIntensityArray()?.Data;
        int aCount = aMz?.Count ?? 0;
        int bCount = bMz?.Count ?? 0;
        if (aCount != bCount)
        {
            ctx.Report($"peak count: {aCount} vs {bCount}");
            return;
        }
        if (aCount == 0 || aMz is null || bMz is null || aInt is null || bInt is null) return;

        // Read each side through its own order so position k names the same peak on both.
        List<int>? aOrder = null, bOrder = null;
        if (ignorePeakOrder && TryPairPeaks(aMz, aInt, bMz, bInt, precision, out var paired, out var bPaired))
        {
            aOrder = paired;
            bOrder = bPaired;
        }

        for (int k = 0; k < aCount; k++)
        {
            int ka = aOrder is null ? k : aOrder[k];
            int kb = bOrder is null ? k : bOrder[k];
            if (Math.Abs(aMz[ka] - bMz[kb]) > precision)
            {
                ctx.Report($"m/z[{k}]: {aMz[ka]} vs {bMz[kb]}");
                return;
            }
            if (Math.Abs(aInt[ka] - bInt[kb]) > precision)
            {
                ctx.Report($"intensity[{k}]: {aInt[ka]} vs {bInt[kb]}");
                return;
            }
        }
    }

    // ---------- top-level ----------

    private static void DiffRoot(MSData a, MSData b, Context ctx)
    {
        DiffStrings(a.Id, b.Id, ctx, "id");
        DiffStrings(a.Accession, b.Accession, ctx, "accession");

        if (!ctx.Config.IgnoreMetadata)
        {
            DiffFileDescription(a.FileDescription, b.FileDescription, ctx);
            DiffListById(a.Software, b.Software, s => s.Id, DiffSoftware, ctx, "softwareList/software");
            if (!ctx.Config.IgnoreDataProcessing)
                DiffListById(a.DataProcessings, b.DataProcessings, d => d.Id, DiffDataProcessing, ctx,
                    "dataProcessingList/dataProcessing");
            DiffListById(a.InstrumentConfigurations, b.InstrumentConfigurations, ic => ic.Id,
                DiffInstrumentConfiguration, ctx, "instrumentConfigurationList/instrumentConfiguration");
            DiffListById(a.Samples, b.Samples, s => s.Id, DiffSample, ctx, "sampleList/sample");
        }

        DiffRun(a.Run, b.Run, ctx);
    }

    // ---------- FileDescription / SourceFile / Software / DataProcessing ----------

    private static void DiffFileDescription(FileDescription a, FileDescription b, Context ctx)
    {
        using var _ = ctx.Push("fileDescription");
        DiffParamContainer(a.FileContent, b.FileContent, ctx, "fileContent");
        DiffListById(a.SourceFiles, b.SourceFiles, s => s.Id, DiffSourceFile, ctx, "sourceFileList/sourceFile");
        DiffContacts(a.Contacts, b.Contacts, ctx);
    }

    /// <summary>
    /// Contacts carry no id, so they are matched by content rather than by position - cpp compares
    /// them as a set, and two files listing the same people in a different order describe the same
    /// thing. Previously they were not compared at all.
    /// </summary>
    private static void DiffContacts(List<Contact> a, List<Contact> b, Context ctx)
    {
        using var _ = ctx.Push("contact");
        if (a.Count != b.Count)
        {
            ctx.Report($"count: {a.Count} vs {b.Count}");
            return;
        }

        var unmatched = new List<Contact>(b);
        foreach (var contact in a)
        {
            int match = unmatched.FindIndex(candidate => ParamSignature(candidate) == ParamSignature(contact));
            if (match < 0)
                ctx.Report($"a-only: {ParamSignature(contact)}");
            else
                unmatched.RemoveAt(match);
        }
        foreach (var leftover in unmatched)
            ctx.Report($"b-only: {ParamSignature(leftover)}");
    }

    private static string ParamSignature(ParamContainer container)
    {
        var parts = container.CVParams.Select(p => $"{p.Cvid}={p.Value}({p.Units})")
            .Concat(container.UserParams.Select(p => $"{p.Name}={p.Value}"))
            .ToList();
        parts.Sort(StringComparer.Ordinal);
        return string.Join(",", parts);
    }

    /// <summary>Sample id, name and params. Samples were counted by DiffListById but never read.</summary>
    private static void DiffSample(Sample a, Sample b, Context ctx)
    {
        DiffStrings(a.Name, b.Name, ctx, "name");
        DiffParamContainer(a, b, ctx);
    }

    private static void DiffSourceFile(SourceFile a, SourceFile b, Context ctx)
    {
        DiffStrings(a.Id, b.Id, ctx, "id");
        DiffStrings(a.Name, b.Name, ctx, "name");
        DiffStrings(a.Location, b.Location, ctx, "location");
        if (ctx.Config.IgnoreSourceFileChecksum)
            DiffParamContainerWithoutCv(a, b, ctx, CVID.MS_SHA_1);
        else
            DiffParamContainerBody(a, b, ctx);
    }

    private static void DiffSoftware(Software a, Software b, Context ctx)
    {
        DiffIds(a.Id, b.Id, ctx, "id");
        if (!ctx.Config.IgnoreVersions)
            DiffStrings(a.Version, b.Version, ctx, "version");
        DiffParamContainerBody(a, b, ctx);
    }

    private static void DiffDataProcessing(DataProcessing a, DataProcessing b, Context ctx)
    {
        DiffIds(a.Id, b.Id, ctx, "id");
        DiffListByIndex(a.ProcessingMethods, b.ProcessingMethods, DiffProcessingMethod, ctx, "processingMethod");
    }

    private static void DiffProcessingMethod(ProcessingMethod a, ProcessingMethod b, Context ctx)
    {
        if (a.Order != b.Order) ctx.Report($"order: {a.Order} vs {b.Order}");
        DiffStrings(a.Software?.Id ?? "", b.Software?.Id ?? "", ctx, "softwareRef");
        DiffParamContainerBody(a, b, ctx);
    }

    // ---------- InstrumentConfiguration / Component ----------

    private static void DiffInstrumentConfiguration(InstrumentConfiguration a, InstrumentConfiguration b, Context ctx)
    {
        DiffStrings(a.Id, b.Id, ctx, "id");
        DiffStrings(a.Software?.Id ?? "", b.Software?.Id ?? "", ctx, "softwareRef");
        DiffListByIndex(a.ComponentList, b.ComponentList, DiffComponent, ctx, "component");
        // The instrument serial number cvParam is emitted only by newer vendor SDKs (e.g.
        // Bruker TIMS SDK >=2.21 emits MS:1000529); older reference mzMLs lack it. Treat it
        // as version-dependent so IgnoreVersions tolerates the asymmetry.
        if (ctx.Config.IgnoreVersions)
            DiffParamContainerWithoutCv(a, b, ctx, CVID.MS_instrument_serial_number);
        else
            DiffParamContainerBody(a, b, ctx);
    }

    private static void DiffParamContainerWithoutCv(ParamContainer a, ParamContainer b, Context ctx, CVID cvidToSkip)
    {
        var aFiltered = new ParamContainer();
        var bFiltered = new ParamContainer();
        foreach (var p in a.CVParams) if (p.Cvid != cvidToSkip) aFiltered.CVParams.Add(p);
        foreach (var p in b.CVParams) if (p.Cvid != cvidToSkip) bFiltered.CVParams.Add(p);
        foreach (var u in a.UserParams) aFiltered.UserParams.Add(u);
        foreach (var u in b.UserParams) bFiltered.UserParams.Add(u);
        foreach (var pg in a.ParamGroups) aFiltered.ParamGroups.Add(pg);
        foreach (var pg in b.ParamGroups) bFiltered.ParamGroups.Add(pg);
        DiffParamContainerBody(aFiltered, bFiltered, ctx);
    }

    private static void DiffComponent(Component a, Component b, Context ctx)
    {
        if (a.Type != b.Type) ctx.Report($"type: {a.Type} vs {b.Type}");
        if (a.Order != b.Order) ctx.Report($"order: {a.Order} vs {b.Order}");
        DiffParamContainerBody(a, b, ctx);
    }

    // ---------- Run ----------

    private static void DiffRun(Run a, Run b, Context ctx)
    {
        using var _ = ctx.Push("run");
        DiffStrings(a.Id, b.Id, ctx, "id");
        if (!ctx.Config.IgnoreStartTimeStamp)
            DiffStrings(a.StartTimeStamp, b.StartTimeStamp, ctx, "startTimeStamp");
        DiffStrings(a.DefaultInstrumentConfiguration?.Id ?? "",
                    b.DefaultInstrumentConfiguration?.Id ?? "",
                    ctx, "defaultInstrumentConfigurationRef");
        DiffStrings(a.DefaultSourceFile?.Id ?? "",
                    b.DefaultSourceFile?.Id ?? "",
                    ctx, "defaultSourceFileRef");
        // Stash defaults for semantic-equivalence checks inside the spectrum list (e.g. a null
        // scan.InstrumentConfiguration should compare equal to the run's default IC).
        ctx.RunDefaultIcA = a.DefaultInstrumentConfiguration?.Id;
        ctx.RunDefaultIcB = b.DefaultInstrumentConfiguration?.Id;
        DiffParamContainerBody(a, b, ctx);

        DiffSpectrumList(a.SpectrumList, b.SpectrumList, ctx);
        if (!ctx.Config.IgnoreChromatograms)
            DiffChromatogramList(a.ChromatogramList, b.ChromatogramList, ctx);
    }

    private static void DiffSpectrumList(ISpectrumList? a, ISpectrumList? b, Context ctx)
    {
        if (a is null && b is null) return;
        using var _ = ctx.Push("spectrumList");
        int ca = a?.Count ?? 0;
        int cb = b?.Count ?? 0;
        if (ca != cb)
        {
            ctx.Report($"spectrum count: {ca} vs {cb}");
            return; // don't compare spectra when lengths differ
        }

        if (a is null || b is null) return;
        for (int i = 0; i < ca && !ctx.Saturated; i++)
        {
            using var __ = ctx.Push("spectrum[" + i + "]");
            var sa = a.GetSpectrum(i, getBinaryData: true);
            var sb = b.GetSpectrum(i, getBinaryData: true);
            DiffSpectrum(sa, sb, ctx);
        }
    }

    private static void DiffChromatogramList(IChromatogramList? a, IChromatogramList? b, Context ctx)
    {
        if (a is null && b is null) return;
        using var _ = ctx.Push("chromatogramList");
        int ca = a?.Count ?? 0;
        int cb = b?.Count ?? 0;
        if (ca != cb)
        {
            ctx.Report($"chromatogram count: {ca} vs {cb}");
            return;
        }
        if (a is null || b is null) return;
        for (int i = 0; i < ca && !ctx.Saturated; i++)
        {
            using var __ = ctx.Push("chromatogram[" + i + "]");
            var ca2 = a.GetChromatogram(i, getBinaryData: true);
            var cb2 = b.GetChromatogram(i, getBinaryData: true);
            DiffChromatogram(ca2, cb2, ctx);
        }
    }

    // ---------- Spectrum / Chromatogram ----------

    private static void DiffSpectrum(Spectrum a, Spectrum b, Context ctx)
    {
        if (!ctx.Config.IgnoreIdentity)
        {
            if (a.Index != b.Index) ctx.Report($"index: {a.Index} vs {b.Index}");
            DiffStrings(a.Id, b.Id, ctx, "id");
        }
        if (a.DefaultArrayLength != b.DefaultArrayLength)
            ctx.Report($"defaultArrayLength: {a.DefaultArrayLength} vs {b.DefaultArrayLength}");
        // The two references a spectrum can carry. Neither was compared, so a spectrum could
        // claim a different source file or a different processing history and still match.
        DiffStrings(a.SourceFile?.Id ?? string.Empty, b.SourceFile?.Id ?? string.Empty,
            ctx, "sourceFileRef");
        if (!ctx.Config.IgnoreDataProcessing)
            DiffStrings(a.DataProcessing?.Id ?? string.Empty, b.DataProcessing?.Id ?? string.Empty,
                ctx, "dataProcessingRef");
        DiffParamContainer(a.Params, b.Params, ctx);
        DiffScanList(a.ScanList, b.ScanList, ctx);
        DiffListByIndex(a.Precursors, b.Precursors, DiffPrecursor, ctx, "precursor");
        DiffListByIndex(a.Products, b.Products, DiffProduct, ctx, "product");
        // Bruker combined-IMS spectra carry an MS_mean_ion_mobility_array alongside the
        // m/z and intensity arrays. The order of duplicate-m/z peaks in those arrays is
        // implementation-defined (depends on the sort algorithm — pwiz C++ uses std::sort,
        // we use Array.Sort), so a position-by-position diff produces noise on duplicate
        // groups even when the underlying peak set is identical. Detect this case and
        // compare as a multiset of (m/z, intensity, mobility) triples.
        if (HasIonMobilityTriples(a) && HasIonMobilityTriples(b))
            DiffMobilityTriples(a, b, ctx);
        else
            DiffBinaryArrays(a.BinaryDataArrays, b.BinaryDataArrays, ctx);
        DiffIntegerArrays(a.IntegerDataArrays, b.IntegerDataArrays, ctx);
    }

    private static bool HasIonMobilityTriples(Spectrum s) =>
        FindArray(s, CVID.MS_m_z_array) is not null
        && FindArray(s, CVID.MS_intensity_array) is not null
        && (FindArray(s, CVID.MS_mean_ion_mobility_array) is not null
            || FindArray(s, CVID.MS_mean_inverse_reduced_ion_mobility_array) is not null);

    private static BinaryDataArray? FindArray(Spectrum s, CVID kind) =>
        s.BinaryDataArrays.FirstOrDefault(b => b.CVParams.Any(p => p.Cvid == kind));

    private static void DiffMobilityTriples(Spectrum a, Spectrum b, Context ctx)
    {
        var mzA = FindArray(a, CVID.MS_m_z_array)!;
        var intA = FindArray(a, CVID.MS_intensity_array)!;
        var mobA = FindArray(a, CVID.MS_mean_ion_mobility_array)
                   ?? FindArray(a, CVID.MS_mean_inverse_reduced_ion_mobility_array)!;
        var mzB = FindArray(b, CVID.MS_m_z_array)!;
        var intB = FindArray(b, CVID.MS_intensity_array)!;
        var mobB = FindArray(b, CVID.MS_mean_ion_mobility_array)
                   ?? FindArray(b, CVID.MS_mean_inverse_reduced_ion_mobility_array)!;

        // Compare the array-level cvParams + userParams normally — only the data is set-based.
        using (var _ = ctx.Push("binaryDataArray[m/z]"))
            DiffArrayMetadata(mzA, mzB, ctx);
        using (var _ = ctx.Push("binaryDataArray[intensity]"))
            DiffArrayMetadata(intA, intB, ctx);
        using (var _ = ctx.Push("binaryDataArray[ion mobility]"))
            DiffArrayMetadata(mobA, mobB, ctx);

        int n = mzA.Data.Count;
        if (n != mzB.Data.Count || n != intA.Data.Count || n != intB.Data.Count
            || n != mobA.Data.Count || n != mobB.Data.Count)
        {
            ctx.Report($"ion-mobility triple array length mismatch: a={mzA.Data.Count}/{intA.Data.Count}/{mobA.Data.Count} b={mzB.Data.Count}/{intB.Data.Count}/{mobB.Data.Count}");
            return;
        }

        // Sort both triple sets by (m/z, intensity, mobility) for set-based comparison.
        var aTriples = SortedTriples(mzA.Data, intA.Data, mobA.Data);
        var bTriples = SortedTriples(mzB.Data, intB.Data, mobB.Data);

        for (int i = 0; i < n && !ctx.Saturated; i++)
        {
            var (amz, aint, amob) = aTriples[i];
            var (bmz, bint, bmob) = bTriples[i];
            if (!DiffImpl.FloatingPointEqual(amz, bmz, ctx.Config)
                || !DiffImpl.FloatingPointEqual(aint, bint, ctx.Config)
                || !DiffImpl.FloatingPointEqual(amob, bmob, ctx.Config))
            {
                ctx.Report($"mobility triple sorted[{i}]: ({amz:G10},{aint:G10},{amob:G10}) vs ({bmz:G10},{bint:G10},{bmob:G10})");
                return;
            }
        }
    }

    private static List<(double Mz, double Intensity, double Mobility)> SortedTriples(
        List<double> mz, List<double> intensity, List<double> mobility)
    {
        var triples = new List<(double, double, double)>(mz.Count);
        for (int i = 0; i < mz.Count; i++)
            triples.Add((mz[i], intensity[i], mobility[i]));
        // Sort by (intensity, mobility-rounded, m/z-as-float). pwiz C++ writes m/z as 32-bit
        // float in the mzML, but two of our full-precision doubles can land on adjacent float
        // buckets after C++'s own internal rounding even when the underlying TIMS index is the
        // same — so a m/z-primary sort can misalign duplicate-m/z groups across sides. Sorting
        // by intensity (exact integer counts) and rounded mobility (TIMS scan-number-to-K0 is
        // deterministic to ~1e-9; round to 1e-7 to absorb that float noise) puts each peak in
        // a stable bucket regardless of which float-bucket its m/z fell into.
        triples.Sort((x, y) =>
        {
            int c = x.Item2.CompareTo(y.Item2);
            if (c != 0) return c;
            double mobX = Math.Round(x.Item3, 7);
            double mobY = Math.Round(y.Item3, 7);
            c = mobX.CompareTo(mobY);
            if (c != 0) return c;
            return ((float)x.Item1).CompareTo((float)y.Item1);
        });
        return triples;
    }

    private static void DiffArrayMetadata(BinaryDataArray a, BinaryDataArray b, Context ctx)
    {
        var aCvParams = a.CVParams.Where(p => !IsBinaryEncodingCv(p.Cvid)).ToList();
        var bCvParams = b.CVParams.Where(p => !IsBinaryEncodingCv(p.Cvid)).ToList();
        DiffCvParamLists(aCvParams, bCvParams, ctx);
        DiffUserParamLists(a.UserParams, b.UserParams, ctx);
    }

    private static void DiffChromatogram(Chromatogram a, Chromatogram b, Context ctx)
    {
        if (!ctx.Config.IgnoreIdentity)
        {
            if (a.Index != b.Index) ctx.Report($"index: {a.Index} vs {b.Index}");
            DiffStrings(a.Id, b.Id, ctx, "id");
        }
        if (a.DefaultArrayLength != b.DefaultArrayLength)
            ctx.Report($"defaultArrayLength: {a.DefaultArrayLength} vs {b.DefaultArrayLength}");
        DiffParamContainer(a.Params, b.Params, ctx);
        // cpp Diff.cpp:584-585 diffs the chromatogram's precursor and product too. Without
        // these the SRM/SIM isolation windows and activation (including collision energy) are
        // invisible to every vendor reference test — which is how the Sciex chromatograms
        // silently stopped emitting `collision energy` while the harness stayed green.
        DiffPrecursor(a.Precursor, b.Precursor, ctx);
        DiffProduct(a.Product, b.Product, ctx);
        DiffBinaryArrays(a.BinaryDataArrays, b.BinaryDataArrays, ctx);
        DiffIntegerArrays(a.IntegerDataArrays, b.IntegerDataArrays, ctx);
    }

    private static void DiffScanList(ScanList a, ScanList b, Context ctx)
    {
        using var _ = ctx.Push("scanList");
        DiffParamContainerBody(a, b, ctx);
        DiffListByIndex(a.Scans, b.Scans, DiffScan, ctx, "scan");
    }

    private static void DiffScan(Scan a, Scan b, Context ctx)
    {
        DiffStrings(a.SpectrumId, b.SpectrumId, ctx, "spectrumRef");
        DiffStrings(a.ExternalSpectrumId, b.ExternalSpectrumId, ctx, "externalSpectrumID");
        // An omitted instrumentConfigurationRef inherits the run's default — treat null as
        // the default IC for semantic equivalence (mzML spec).
        string icA = a.InstrumentConfiguration?.Id ?? ctx.RunDefaultIcA ?? "";
        string icB = b.InstrumentConfiguration?.Id ?? ctx.RunDefaultIcB ?? "";
        DiffStrings(icA, icB, ctx, "instrumentConfigurationRef");
        DiffParamContainerBody(a, b, ctx);
        DiffListByIndex(a.ScanWindows, b.ScanWindows, DiffScanWindow, ctx, "scanWindow");
    }

    private static void DiffScanWindow(ScanWindow a, ScanWindow b, Context ctx) =>
        DiffParamContainerBody(a, b, ctx);

    private static void DiffPrecursor(Precursor a, Precursor b, Context ctx)
    {
        DiffStrings(a.SpectrumId, b.SpectrumId, ctx, "spectrumRef");
        DiffStrings(a.ExternalSpectrumId, b.ExternalSpectrumId, ctx, "externalSpectrumID");
        DiffParamContainerBody(a.IsolationWindow, b.IsolationWindow, ctx, "isolationWindow");
        DiffListByIndex(a.SelectedIons, b.SelectedIons, DiffSelectedIon, ctx, "selectedIon");
        DiffParamContainerBody(a.Activation, b.Activation, ctx, "activation");
        DiffParamContainerBody(a, b, ctx);
    }

    private static void DiffProduct(Product a, Product b, Context ctx) =>
        DiffParamContainerBody(a.IsolationWindow, b.IsolationWindow, ctx, "isolationWindow");

    private static void DiffSelectedIon(SelectedIon a, SelectedIon b, Context ctx) =>
        DiffParamContainerBody(a, b, ctx);

    // ---------- BinaryDataArray helpers ----------

    private static void DiffBinaryArrays(List<BinaryDataArray> a, List<BinaryDataArray> b, Context ctx)
    {
        if (ctx.Config.IgnoreExtraBinaryDataArrays && a.Count != b.Count)
        {
            // compare only arrays that exist in both (matched by primary CV term type)
            var aByType = a.ToDictionary(GetArrayTypeKey);
            var bByType = b.ToDictionary(GetArrayTypeKey);
            foreach (var key in aByType.Keys.Intersect(bByType.Keys))
            {
                using var _ = ctx.Push("binaryDataArray[" + key + "]");
                DiffBinaryArray(aByType[key], bByType[key], ctx);
            }
            return;
        }

        if (a.Count != b.Count)
        {
            ctx.Report($"binaryDataArray count: {a.Count} vs {b.Count}");
            return;
        }
        for (int i = 0; i < a.Count && !ctx.Saturated; i++)
        {
            using var _ = ctx.Push("binaryDataArray[" + i + "]");
            DiffBinaryArray(a[i], b[i], ctx);
        }
    }

    private static void DiffIntegerArrays(List<IntegerDataArray> a, List<IntegerDataArray> b, Context ctx)
    {
        if (ctx.Config.IgnoreExtraBinaryDataArrays && a.Count != b.Count)
            return;
        if (a.Count != b.Count)
        {
            ctx.Report($"integerDataArray count: {a.Count} vs {b.Count}");
            return;
        }
        for (int i = 0; i < a.Count && !ctx.Saturated; i++)
        {
            using var _ = ctx.Push("integerDataArray[" + i + "]");
            DiffIntegerArray(a[i], b[i], ctx);
        }
    }

    private static void DiffBinaryArray(BinaryDataArray a, BinaryDataArray b, Context ctx)
    {
        // Filter out purely-serialization cvParams (32-bit / 64-bit precision, compression type)
        // before diffing — they describe how the array was encoded, not its content.
        var aCvParams = a.CVParams.Where(p => !IsBinaryEncodingCv(p.Cvid)).ToList();
        var bCvParams = b.CVParams.Where(p => !IsBinaryEncodingCv(p.Cvid)).ToList();
        DiffCvParamLists(aCvParams, bCvParams, ctx);
        // UserParams and ParamGroup refs still matter; compare them raw.
        DiffUserParamLists(a.UserParams, b.UserParams, ctx);

        if (a.Data.Count != b.Data.Count)
        {
            ctx.Report($"data length: {a.Data.Count} vs {b.Data.Count}");
            return;
        }
        for (int i = 0; i < a.Data.Count; i++)
        {
            if (!DiffImpl.FloatingPointEqual(a.Data[i], b.Data[i], ctx.Config))
            {
                ctx.Report($"data[{i}]: {a.Data[i]:G17} vs {b.Data[i]:G17}");
                return; // one per array is enough
            }
        }
    }

    private static bool IsBinaryEncodingCv(CVID cvid)
    {
        return cvid is CVID.MS_32_bit_float
                    or CVID.MS_64_bit_float
                    or CVID.MS_32_bit_integer
                    or CVID.MS_64_bit_integer
                    or CVID.MS_no_compression
                    or CVID.MS_zlib_compression
                    or CVID.MS_MS_Numpress_linear_prediction_compression
                    or CVID.MS_MS_Numpress_positive_integer_compression
                    or CVID.MS_MS_Numpress_short_logged_float_compression
                    or CVID.MS_MS_Numpress_linear_prediction_compression_followed_by_zlib_compression
                    or CVID.MS_MS_Numpress_positive_integer_compression_followed_by_zlib_compression
                    or CVID.MS_MS_Numpress_short_logged_float_compression_followed_by_zlib_compression;
    }

    private static void DiffCvParamLists(List<CVParam> a, List<CVParam> b, Context ctx)
    {
        foreach (var p in a)
            if (!b.Any(q => ParamsEqual(p, q, ctx.Config)))
                ctx.Report($"cvParam a-only: {FormatCv(p)}");
        foreach (var p in b)
            if (!a.Any(q => ParamsEqual(p, q, ctx.Config)))
                ctx.Report($"cvParam b-only: {FormatCv(p)}");
    }

    private static void DiffUserParamLists(List<UserParam> a, List<UserParam> b, Context ctx)
    {
        foreach (var u in a)
            if (!b.Any(v => u.Equals(v))) ctx.Report($"userParam a-only: {FormatUp(u)}");
        foreach (var u in b)
            if (!a.Any(v => u.Equals(v))) ctx.Report($"userParam b-only: {FormatUp(u)}");
    }

    private static void DiffIntegerArray(IntegerDataArray a, IntegerDataArray b, Context ctx)
    {
        // Same encoding-CV filtering as BinaryDataArray — int32/int64 choice is serialization.
        var aCvParams = a.CVParams.Where(p => !IsBinaryEncodingCv(p.Cvid)).ToList();
        var bCvParams = b.CVParams.Where(p => !IsBinaryEncodingCv(p.Cvid)).ToList();
        DiffCvParamLists(aCvParams, bCvParams, ctx);
        DiffUserParamLists(a.UserParams, b.UserParams, ctx);

        if (a.Data.Count != b.Data.Count)
        {
            ctx.Report($"data length: {a.Data.Count} vs {b.Data.Count}");
            return;
        }
        for (int i = 0; i < a.Data.Count; i++)
        {
            if (a.Data[i] != b.Data[i])
            {
                ctx.Report($"data[{i}]: {a.Data[i]} vs {b.Data[i]}");
                return;
            }
        }
    }

    /// <summary>
    /// Produces, for each side, the order in which to read its peaks so that position k names the
    /// same peak on both. Returns false when the two sides are not comparable peak-for-peak, in
    /// which case the caller should compare them as they are - a length difference is itself the
    /// finding.
    /// </summary>
    private static bool TryPairPeaks(List<double> aMz, List<double> aIntensity,
                                     List<double> bMz, List<double> bIntensity,
                                     double precision,
                                     out List<int> aPaired, out List<int> bPaired)
    {
        aPaired = null!;
        bPaired = null!;
        int n = aMz.Count;
        if (aIntensity.Count != n || bMz.Count != n || bIntensity.Count != n)
            return false;

        int[] aOrder = StableOrderBy(aMz);
        int[] bOrder = StableOrderBy(bMz);

        // Both sides ascend in m/z and agree within tolerance, so a peak's candidates are a short
        // contiguous run of the other side rather than the whole spectrum.
        var bTaken = new bool[n];
        aPaired = new List<int>(n);
        bPaired = new List<int>(n);

        int windowStart = 0;
        for (int ai = 0; ai < n; ai++)
        {
            int aIndex = aOrder[ai];
            while (windowStart < n &&
                   bMz[bOrder[windowStart]] < aMz[aIndex] &&
                   !WithinPrecision(bMz[bOrder[windowStart]], aMz[aIndex], precision))
                windowStart++;

            int best = -1;
            double bestIntensityDelta = 0;
            for (int bi = windowStart; bi < n; bi++)
            {
                int bIndex = bOrder[bi];
                if (bMz[bIndex] > aMz[aIndex] && !WithinPrecision(bMz[bIndex], aMz[aIndex], precision))
                    break; // ascending, so nothing beyond this can match either
                if (bTaken[bIndex] || !WithinPrecision(bMz[bIndex], aMz[aIndex], precision))
                    continue;
                double delta = Math.Abs(bIntensity[bIndex] - aIntensity[aIndex]);
                if (best < 0 || delta < bestIntensityDelta)
                {
                    best = bIndex;
                    bestIntensityDelta = delta;
                }
            }

            if (best >= 0)
            {
                bTaken[best] = true;
                aPaired.Add(aIndex);
                bPaired.Add(best);
            }
        }

        // Whatever went unpaired, in m/z order so the two sides at least line up positionally.
        var aTaken = new bool[n];
        foreach (int i in aPaired)
            aTaken[i] = true;
        for (int ai = 0; ai < n; ai++)
            if (!aTaken[aOrder[ai]]) aPaired.Add(aOrder[ai]);
        for (int bi = 0; bi < n; bi++)
            if (!bTaken[bOrder[bi]]) bPaired.Add(bOrder[bi]);

        return true;
    }

    /// <summary>
    /// Whether two values agree within the diff's tolerance, by the same relative measure the
    /// comparison applies downstream - so a pair this accepts is a pair that comparison accepts.
    /// </summary>
    private static bool WithinPrecision(double x, double y, double precision)
    {
        double denominator = Math.Min(x, y);
        if (denominator == 0) denominator = 1;
        return Math.Abs(x - y) / denominator <= precision;
    }

    /// <summary>Indices that would sort <paramref name="values"/> ascending, ties keeping their original order.</summary>
    private static int[] StableOrderBy(List<double> values)
    {
        var order = new int[values.Count];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        Array.Sort(order, (x, y) =>
        {
            int compared = values[x].CompareTo(values[y]);
            return compared != 0 ? compared : x.CompareTo(y);
        });
        return order;
    }

    private static string GetArrayTypeKey(BinaryDataArray arr)
    {
        foreach (var p in arr.CVParams)
        {
            // First CV param that names an array type (m/z array, intensity array, time array, etc.).
            if (CvLookup.CvIsA(p.Cvid, CVID.MS_binary_data_array)) return p.Cvid.ToString();
        }
        return arr.CVParams.Count > 0 ? arr.CVParams[0].Cvid.ToString() : "";
    }

    // ---------- ParamContainer helpers ----------

    private static void DiffParamContainer(ParamContainer a, ParamContainer b, Context ctx, string? label = null)
    {
        if (label is not null)
        {
            using var _ = ctx.Push(label);
            DiffParamContainerBody(a, b, ctx);
        }
        else
        {
            DiffParamContainerBody(a, b, ctx);
        }
    }

    private static void DiffParamContainerBody(ParamContainer a, ParamContainer b, Context ctx, string? label = null)
    {
        IDisposable? scope = label is null ? null : ctx.Push(label);
        try
        {
            foreach (var p in a.CVParams)
                if (!b.CVParams.Any(q => ParamsEqual(p, q, ctx.Config)))
                    ctx.Report($"cvParam a-only: {FormatCv(p)}");
            foreach (var p in b.CVParams)
                if (!a.CVParams.Any(q => ParamsEqual(p, q, ctx.Config)))
                    ctx.Report($"cvParam b-only: {FormatCv(p)}");
            foreach (var u in a.UserParams)
                if (!b.UserParams.Any(v => u.Equals(v)))
                    ctx.Report($"userParam a-only: {FormatUp(u)}");
            foreach (var u in b.UserParams)
                if (!a.UserParams.Any(v => u.Equals(v)))
                    ctx.Report($"userParam b-only: {FormatUp(u)}");

            // ParamGroup references: compare by id set
            var aIds = a.ParamGroups.Select(pg => pg.Id).OrderBy(s => s).ToList();
            var bIds = b.ParamGroups.Select(pg => pg.Id).OrderBy(s => s).ToList();
            if (!aIds.SequenceEqual(bIds, StringComparer.Ordinal))
                ctx.Report($"referenceableParamGroupRef: [{string.Join(",", aIds)}] vs [{string.Join(",", bIds)}]");
        }
        finally { scope?.Dispose(); }
    }

    private static bool ParamsEqual(CVParam a, CVParam b, DiffConfig config)
    {
        if (a.Cvid != b.Cvid) return false;
        if (a.Units != b.Units) return false;
        // Numeric comparison at configured precision if both parse as doubles.
        if (TryParseDouble(a.Value, out double da) && TryParseDouble(b.Value, out double db))
            return DiffImpl.FloatingPointEqual(da, db, config);
        return string.Equals(a.Value, b.Value, StringComparison.Ordinal);
    }

    private static bool TryParseDouble(string s, out double d) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d);

    private static string FormatCv(CVParam p)
    {
        string v = string.IsNullOrEmpty(p.Value) ? "" : " = " + p.Value;
        string u = p.Units == CVID.CVID_Unknown ? "" : " (" + p.Units + ")";
        return $"{p.Cvid}{v}{u}";
    }

    private static string FormatUp(UserParam u)
    {
        string v = string.IsNullOrEmpty(u.Value) ? "" : " = " + u.Value;
        return $"{u.Name}{v}";
    }

    // ---------- List helpers ----------

    private static void DiffListById<T>(List<T> a, List<T> b, Func<T, string> keyOf,
        Action<T, T, Context> differ, Context ctx, string label)
        where T : class
    {
        if (a.Count != b.Count)
        {
            ctx.Report($"{label} count: {a.Count} vs {b.Count}");
            return;
        }
        // Pair by id; XML-encoded ids (_xNNNN_) are decoded first so they match raw ids.
        string Key(T item) => DecodeXmlId(keyOf(item));
        var aByKey = a.ToDictionary(Key);
        var bByKey = b.ToDictionary(Key);
        foreach (var k in aByKey.Keys.Except(bByKey.Keys))
            ctx.Report($"{label} a-only id: {k}");
        foreach (var k in bByKey.Keys.Except(aByKey.Keys))
            ctx.Report($"{label} b-only id: {k}");

        foreach (var k in aByKey.Keys.Intersect(bByKey.Keys))
        {
            if (ctx.Saturated) break;
            using var _ = ctx.Push(label + "[" + k + "]");
            differ(aByKey[k], bByKey[k], ctx);
        }
    }

    private static void DiffListByIndex<T>(List<T> a, List<T> b, Action<T, T, Context> differ,
        Context ctx, string label)
        where T : class
    {
        if (a.Count != b.Count)
        {
            ctx.Report($"{label} count: {a.Count} vs {b.Count}");
            return;
        }
        for (int i = 0; i < a.Count && !ctx.Saturated; i++)
        {
            using var _ = ctx.Push(label + "[" + i + "]");
            differ(a[i], b[i], ctx);
        }
    }

    // ---------- primitives ----------

    private static void DiffStrings(string a, string b, Context ctx, string label)
    {
        if (a == b) return;
        // Tolerate pwiz's XML-id encoding (e.g. `_x0032_0percLaser` ↔ `20percLaser`). Reference
        // mzMLs emit _xNNNN_ hex escapes for ids starting with a digit or containing reserved
        // characters; our in-memory ids are raw.
        if (DecodeXmlId(a) == DecodeXmlId(b)) return;
        using var _ = ctx.Push(label);
        ctx.Report($"{Quote(a)} vs {Quote(b)}");
    }

    private static readonly System.Text.RegularExpressions.Regex s_xmlIdEscape =
        new(@"_x([0-9A-Fa-f]{4})_", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string DecodeXmlId(string s) =>
        string.IsNullOrEmpty(s) ? s : s_xmlIdEscape.Replace(s,
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

    private static void DiffIds(string a, string b, Context ctx, string label)
    {
        if (ctx.Config.IgnoreVersions)
        {
            string sa = StripTrailingVersion(a);
            string sb = StripTrailingVersion(b);
            if (sa == sb) return;
        }
        else if (a == b) return;
        using var _ = ctx.Push(label);
        ctx.Report($"{Quote(a)} vs {Quote(b)}");
    }

    private static readonly System.Text.RegularExpressions.Regex s_trailingVersion =
        new(@"[_\-]\d+(\.\d+)+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string StripTrailingVersion(string s) =>
        s_trailingVersion.Replace(s ?? string.Empty, string.Empty);

    private static string Quote(string s) => "\"" + (s ?? "") + "\"";

    // ---------- Context ----------

    private sealed class Context
    {
        private readonly Stack<string> _path = new();
        private readonly List<string> _diffs = new();

        public DiffConfig Config { get; }

        /// <summary>Run's default-IC id on side a (or null), set by <see cref="DiffRun"/>.</summary>
        public string? RunDefaultIcA { get; set; }

        /// <summary>Run's default-IC id on side b (or null), set by <see cref="DiffRun"/>.</summary>
        public string? RunDefaultIcB { get; set; }

        public bool Saturated => _diffs.Count >= Config.MaxDifferencesToReport;

        public Context(DiffConfig config) { Config = config; }

        public IDisposable Push(string segment)
        {
            _path.Push(segment);
            return new PopOnDispose(this);
        }

        public void Report(string detail)
        {
            if (Saturated) return;
            string path = _path.Count == 0 ? "" : string.Join("/", _path.Reverse());
            _diffs.Add(path.Length == 0 ? detail : $"{path}: {detail}");
        }

        public string Format()
        {
            if (_diffs.Count == 0) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.Append("MSData diff: ").Append(_diffs.Count).Append(" difference(s)");
            if (Saturated) sb.Append(" (report capped at ").Append(Config.MaxDifferencesToReport).Append(')');
            sb.AppendLine();
            foreach (var line in _diffs) sb.AppendLine("  " + line);
            return sb.ToString();
        }

        private sealed class PopOnDispose : IDisposable
        {
            private readonly Context _ctx;
            public PopOnDispose(Context ctx) { _ctx = ctx; }
            public void Dispose() { _ctx._path.Pop(); }
        }
    }
}
