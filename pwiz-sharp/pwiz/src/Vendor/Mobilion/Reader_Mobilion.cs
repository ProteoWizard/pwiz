using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Sources;

#pragma warning disable CA1707  // underscored class name mirrors cpp `Reader_Mobilion`

namespace Pwiz.Vendor.Mobilion;

/// <summary>
/// <see cref="IReader"/> for MOBILion .mbi files (HDF5 IM-MS raw data). C# port of cpp
/// <c>Reader_Mobilion</c> (Reader_Mobilion.cpp).
/// </summary>
/// <remarks>
/// MBI is HDF5 underneath — Identify() does the same magic-byte check + ".mbi"
/// extension test cpp uses (Reader_Mobilion.cpp:41-52). Read() requires the
/// MobilionShim native library and MBI_SDK.dll, which only ship when the build
/// is gated through <c>--i-agree-to-the-vendor-licenses</c>. In NO_VENDOR mode
/// the reader still identifies files but Read() throws.
/// </remarks>
public sealed class Reader_Mobilion : IReader
{
    private static readonly byte[] Hdf5Magic = { 0x89, 0x48, 0x44, 0x46, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <inheritdoc/>
    public string TypeName => "Mobilion";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_Mobilion_MBI_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".mbi" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        if (!filename.EndsWith(".mbi", StringComparison.OrdinalIgnoreCase))
            return CVID.CVID_Unknown;

        // cpp Reader_Mobilion.cpp:44-49: the first 8 bytes must be the HDF5 signature.
        // ReaderList passes `head` decoded as UTF-8, which mangles the 0x89 first byte
        // (it's not a valid UTF-8 lead byte → U+FFFD) and our magic check would
        // false-negative. Always sniff the file directly for the binary signature
        // rather than trusting the decoded string.
        if (!File.Exists(filename)) return CVID.CVID_Unknown;
        try
        {
            using var fs = File.OpenRead(filename);
            Span<byte> buf = stackalloc byte[Hdf5Magic.Length];
            if (fs.Read(buf) < Hdf5Magic.Length) return CVID.CVID_Unknown;
            return buf.SequenceEqual(Hdf5Magic) ? CvType : CVID.CVID_Unknown;
        }
        catch { return CVID.CVID_Unknown; }
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

#if NO_VENDOR_SUPPORT
        throw new VendorSupportNotEnabledException(
            "Mobilion reader requires --i-agree-to-the-vendor-licenses (MBI_SDK.dll).");
#else
        if (Identify(filename, head: null) == CVID.CVID_Unknown)
            throw new InvalidDataException($"Not a Mobilion MBI file: {filename}");

        // cpp Reader_Mobilion.cpp:181-186: Mobilion API rejects Unicode in filepaths.
        // We don't have get_non_unicode_path's 8.3-fallback in C#, so just refuse early
        // with the same diagnostic. (Most MBI files are on local disks where the user
        // controls the path.)
        if (filename.Any(ch => !char.IsAscii(ch)))
            throw new IOException(
                "Mobilion API does not support Unicode in filepaths: " + filename);

        var effectiveConfig = config ?? new ReaderConfig();
        var data = MobilionData.Open(filename);
        try
        {
            FillMetadata(filename, data, result, effectiveConfig);
        }
        catch
        {
            data.Dispose();
            throw;
        }
#endif
    }

#if !NO_VENDOR_SUPPORT
    private static void FillMetadata(string filename, MobilionData data, MSData result, ReaderConfig config)
    {
        result.CVs.AddRange(MSData.DefaultCVList);

        // cpp Reader_Mobilion.cpp:84-95: walk frames once to set MS1 / MSn flags. Stop
        // as soon as both have been observed.
        bool hasMS1 = false, hasMS2 = false;
        int frames = data.NumFrames;
        for (int i = 1; i <= frames && !(hasMS1 && hasMS2); i++)
        {
            using var f = data.GetFrame(i);
            if (f.GetCe(0) > 0) hasMS2 = true; else hasMS1 = true;
        }
        var fc = result.FileDescription.FileContent;
        if (hasMS1) fc.Set(CVID.MS_MS1_spectrum);
        if (hasMS2) fc.Set(CVID.MS_MSn_spectrum);
        fc.Set(CVID.MS_profile_spectrum);
        fc.Set(CVID.MS_TIC_chromatogram);

        var fileName = Path.GetFileName(filename);
        var parentDir = Path.GetDirectoryName(Path.GetFullPath(filename)) ?? string.Empty;
        var sourceFile = new SourceFile(fileName, fileName,
            "file:///" + parentDir.Replace('\\', '/'));
        sourceFile.Set(CVID.MS_Mobilion_MBI_nativeID_format);
        sourceFile.Set(CVID.MS_Mobilion_MBI_format);
        result.FileDescription.SourceFiles.Add(sourceFile);

        result.Id = Path.GetFileNameWithoutExtension(filename);
        result.Run.Id = result.Id;

        var softwareMobilion = new Software("MOBILion");
        softwareMobilion.Set(CVID.MS_acquisition_software);
        softwareMobilion.Version = data.ReadGlobalString(MobilionAttr.ACQ_SOFTWARE_VERSION) ?? string.Empty;
        result.Software.Add(softwareMobilion);

        var pwizSoftware = new Software("pwiz_Reader_Mobilion") { Version = MSData.PwizVersion };
        pwizSoftware.Set(CVID.MS_pwiz);
        result.Software.Add(pwizSoftware);

        var dp = new DataProcessing("pwiz_Reader_Mobilion_conversion");
        var pm = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pm.Set(CVID.MS_Conversion_to_mzML);
        dp.ProcessingMethods.Add(pm);
        result.DataProcessings.Add(dp);

        // cpp Reader_Mobilion.cpp:137-149: instrument config tagged with
        // `MS_Agilent_instrument_model` (MOBILion uses Agilent QTOF hardware), with
        // the SDK's MS-model string carried as a UserParam.
        var ic = new InstrumentConfiguration("IC");
        ic.Set(CVID.MS_Agilent_instrument_model);
        string? msModel = data.ReadGlobalString(MobilionAttr.ACQ_MS_MODEL);
        if (!string.IsNullOrEmpty(msModel))
            ic.UserParams.Add(new UserParam("instrument model", msModel));
        ic.ComponentList.Add(new Component(CVID.MS_electrospray_ionization, 1));
        ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 2));
        ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 3));
        ic.ComponentList.Add(new Component(CVID.MS_time_of_flight, 4));
        ic.ComponentList.Add(new Component(CVID.MS_multichannel_plate, 5));
        result.InstrumentConfigurations.Add(ic);
        result.Run.DefaultInstrumentConfiguration = ic;

        // cpp Reader_Mobilion.cpp:154-164: timestamp comes through as
        // "yyyy-MM-dd HH:mm:ss[.fffff]"; cpp trims the fractional seconds before the
        // boost parse, then encodes as XML datetime. Match — and route through
        // FormatStartTimeStamp so the adjustUnknownTimeZonesToHostTimeZone flag is
        // honored the same way it is for other vendor readers.
        string? timestamp = data.ReadGlobalString(MobilionAttr.ACQ_TIMESTAMP);
        if (!string.IsNullOrEmpty(timestamp))
        {
            int dot = timestamp.IndexOf('.', StringComparison.Ordinal);
            string trimmed = dot >= 0 ? timestamp[..dot] : timestamp;
            if (DateTime.TryParseExact(trimmed, "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            {
                string? startTime = config.FormatStartTimeStamp(dt.ToUniversalTime());
                if (startTime is not null) result.Run.StartTimeStamp = startTime;
            }
        }

        // cpp SpectrumList_Mobilion.cpp:128 sets the per-spectrum scan window from
        // [0, ADC_MASS_SPEC_RANGE]. Hoist the global lookup once here so the spectrum
        // list doesn't re-query the SDK for every spectrum.
        double scanWindowUpper = data.ReadGlobalDouble(MobilionAttr.ADC_MASS_SPEC_RANGE) ?? 0.0;

        var spectrumList = new SpectrumList_Mobilion(
            data,
            defaultInstrumentConfiguration: ic,
            combineIonMobilitySpectra: config.CombineIonMobilitySpectra,
            ignoreZeroIntensityPoints: config.IgnoreZeroIntensityPoints,
            scanWindowUpperMz: scanWindowUpper)
        { Dp = dp };
        var chromatogramList = new ChromatogramList_Mobilion(data) { Dp = dp };

        result.Run.SpectrumList = spectrumList;
        result.Run.ChromatogramList = chromatogramList;
    }
#endif
}
