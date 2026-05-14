using System.Globalization;
using System.IO.Compression;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Data.MsData.MSn;

/// <summary>
/// Reads and writes MSn files (MS1/MS2/BMS1/BMS2/CMS1/CMS2). Port of pwiz::msdata::Serializer_MSn
/// + the text-parsing portion of SpectrumList_MSn.
/// </summary>
/// <remarks>
/// <para>Three serialization variants:</para>
/// <list type="bullet">
///   <item><b>Text (MS1/MS2):</b> per-spectrum <c>S</c>/<c>Z</c>/<c>I</c>/<c>D</c> header
///         lines followed by <c>mz intensity</c> peak rows.</item>
///   <item><b>Binary uncompressed (BMS1/BMS2):</b> file header (filetype + version + 16×128
///         char header lines), then per-spectrum: scan-info struct, charge / EZ states,
///         peak structs (double mz + float intensity).</item>
///   <item><b>Binary compressed (CMS1/CMS2):</b> same layout, but each spectrum's m/z and
///         intensity arrays are zlib-compressed (<c>System.IO.Compression.ZLibStream</c>)
///         with a 4-byte length prefix per array.</item>
/// </list>
/// <para>pwiz cpp eagerly indexes binary files for random-access lazy reads; we eagerly
/// parse the whole file into a <see cref="SpectrumListSimple"/>, matching the
/// MGF / BTDX readers.</para>
/// </remarks>
public sealed class SerializerMSn
{
    private const double ProtonMass = 1.00727646677;

    // Fixed-size primitive widths (from cpp Serializer_MSn.cpp). These never change with
    // platform / sizeof — the file format is locked.
    private const int SizeInt = 4;
    private const int SizeFloat = 4;
    private const int SizeDouble = 8;
    private const int SizeChargePair = SizeInt + SizeDouble;   // z + mass
    private const int SizeEzState   = SizeInt + SizeDouble + SizeFloat + SizeFloat; // z + mass + rt + area

    /// <summary>File-format version recorded in binary files. cpp always writes v3.</summary>
    public const int BinaryVersion = 3;

    private static readonly NumberFormatInfo Inv = CultureInfo.InvariantCulture.NumberFormat;

    /// <summary>The MSn file type produced/consumed by this serializer.</summary>
    public MSnType FileType { get; }

    /// <summary>Optional progress listener registry.</summary>
    public IterationListenerRegistry? IterationListenerRegistry { get; set; }

    /// <summary>Creates a serializer bound to a specific file type.</summary>
    public SerializerMSn(MSnType fileType)
    {
        if (fileType == MSnType.Unknown)
            throw new ArgumentException("MSn file type must be specified.", nameof(fileType));
        FileType = fileType;
    }

    // ===================== Write =====================

    /// <summary>Writes <paramref name="msd"/> as the configured MSn file type.</summary>
    public void Write(MSData msd, Stream output)
    {
        ArgumentNullException.ThrowIfNull(msd);
        ArgumentNullException.ThrowIfNull(output);

        if (FileType.IsText())
        {
            using var tw = new StreamWriter(output, new System.Text.UTF8Encoding(false), 4096, leaveOpen: true) { NewLine = "\n" };
            WriteText(msd, tw);
        }
        else
        {
            using var bw = new BinaryWriter(output, System.Text.Encoding.ASCII, leaveOpen: true);
            WriteBinary(msd, bw);
        }
    }

    private void WriteText(MSData msd, TextWriter w)
    {
        // Header lines mirror cpp: CreationDate, Extractor, Extractor version, Source file.
        w.Write("H\tCreationDate ");
        w.WriteLine(DateTime.UtcNow.ToString("ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture));
        w.WriteLine("H\tExtractor\tProteoWizard");
        w.WriteLine("H\tExtractor version\t" + (msd.Software.Count == 0 ? "unknown" : msd.Software[0].Id));
        w.WriteLine("H\tSource file\t" + (msd.FileDescription.SourceFiles.Count == 0 ? "unknown" : msd.FileDescription.SourceFiles[0].Name));

        if (msd.Run.SpectrumList is null) return;
        bool ms1 = FileType.IsMs1();
        int count = msd.Run.SpectrumList.Count;
        for (int i = 0; i < count; i++)
        {
            var spec = msd.Run.SpectrumList.GetSpectrum(i, getBinaryData: true);
            int msLevel = spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
            if (ms1 && msLevel != 1) continue;
            if (!ms1 && (msLevel != 2 || spec.Precursors.Count == 0 || spec.Precursors[0].SelectedIons.Count == 0))
                continue;
            WriteTextSpectrum(spec, w, ms1, msd.Run.SpectrumList);
            IterationListenerRegistry?.Broadcast(new IterationUpdate(i, count, "writing spectra"));
        }
    }

    private static void WriteTextSpectrum(Spectrum spec, TextWriter w, bool ms1, ISpectrumList sl)
    {
        int scanNum = ExtractScanNumber(spec);
        w.Write("S\t");
        w.Write(scanNum.ToString(Inv));
        w.Write('\t');
        w.Write(scanNum.ToString(Inv));
        if (!ms1)
        {
            double mz = spec.Precursors[0].IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>();
            w.Write('\t');
            w.Write(mz.ToString("R", Inv));
        }
        w.WriteLine();

        // NativeID echo line (cpp emits this).
        w.Write("I\tNativeID\t");
        w.WriteLine(spec.Id);

        // Retention time, if available.
        if (spec.ScanList.Scans.Count > 0)
        {
            var rtParam = spec.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
            if (!rtParam.IsEmpty)
            {
                double rtSec = rtParam.TimeInSeconds();
                if (rtSec != 0)
                {
                    w.Write("I\tRTime\t");
                    w.WriteLine((rtSec / 60.0).ToString("R", Inv));
                }
            }
        }

        // Optional BPI / BPM / TIC.
        if (spec.DefaultArrayLength > 0)
        {
            if (spec.Params.HasCVParam(CVID.MS_base_peak_intensity))
            {
                w.Write("I\tBPI\t");
                w.WriteLine(spec.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>().ToString("R", Inv));
            }
            if (spec.Params.HasCVParam(CVID.MS_base_peak_m_z))
            {
                w.Write("I\tBPM\t");
                w.WriteLine(spec.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>().ToString("R", Inv));
            }
            if (spec.Params.HasCVParam(CVID.MS_total_ion_current))
            {
                w.Write("I\tTIC\t");
                w.WriteLine(spec.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>().ToString("R", Inv));
            }
        }

        // Charge / EZ / Z lines.
        if (!ms1)
        {
            var precursor = spec.Precursors[0];
            var charges = new List<int>();
            var masses = new List<double>();
            foreach (var si in precursor.SelectedIons)
                CollectChargeStates(si, charges, masses);

            // EZ lines come first when accurate mass is available.
            var firstSi = precursor.SelectedIons[0];
            bool hasAccurateMass = !firstSi.UserParam("accurate mass").IsEmpty;
            if (hasAccurateMass)
            {
                var precursorIntensity = precursor.CvParam(CVID.MS_peak_intensity).Value;
                for (int i = 0; i < charges.Count; i++)
                {
                    w.Write("I\tEZ\t");
                    w.Write(charges[i].ToString(Inv));
                    w.Write('\t');
                    w.Write(masses[i].ToString("R", Inv));
                    w.Write('\t');
                    w.Write("0"); // rtime
                    w.Write('\t');
                    w.WriteLine(precursorIntensity);
                }
            }

            for (int i = 0; i < charges.Count; i++)
            {
                w.Write("Z\t");
                w.Write(charges[i].ToString(Inv));
                w.Write('\t');
                w.WriteLine(masses[i].ToString("R", Inv));
            }
        }

        var mzArr = spec.GetMZArray();
        var intArr = spec.GetIntensityArray();
        if (mzArr is not null && intArr is not null)
        {
            int n = Math.Min(mzArr.Data.Count, intArr.Data.Count);
            for (int p = 0; p < n; p++)
            {
                w.Write(mzArr.Data[p].ToString("R", Inv));
                w.Write(' ');
                w.WriteLine(intArr.Data[p].ToString("R", Inv));
            }
        }
    }

    private void WriteBinary(MSData msd, BinaryWriter w)
    {
        // Header: file-type int + version int + 16 fixed-size header lines.
        w.Write((int)FileType);
        w.Write(BinaryVersion);
        var header = new MSnHeader();
        header.Lines[0] = "CreationDate " + DateTime.UtcNow.ToString("ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture);
        header.Lines[1] = "Extractor\tProteoWizard";
        header.Lines[2] = "Extractor version\t" + (msd.Software.Count == 0 ? "unknown" : msd.Software[0].Id);
        header.Lines[3] = "Source file\t" + (msd.FileDescription.SourceFiles.Count == 0 ? "unknown" : msd.FileDescription.SourceFiles[0].Name);
        header.Write(w);

        if (msd.Run.SpectrumList is null) return;
        bool ms1 = FileType.IsMs1();
        bool compress = FileType.IsCompressed();
        int count = msd.Run.SpectrumList.Count;
        for (int i = 0; i < count; i++)
        {
            var spec = msd.Run.SpectrumList.GetSpectrum(i, getBinaryData: true);
            int msLevel = spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
            if (ms1 && msLevel != 1) continue;
            if (!ms1 && (msLevel != 2 || spec.Precursors.Count == 0 || spec.Precursors[0].SelectedIons.Count == 0))
                continue;
            WriteBinarySpectrum(spec, w, ms1, compress);
            IterationListenerRegistry?.Broadcast(new IterationUpdate(i, count, "writing spectra"));
        }
    }

    private static void WriteBinarySpectrum(Spectrum spec, BinaryWriter w, bool ms1, bool compress)
    {
        int scanNum = ExtractScanNumber(spec);
        w.Write(scanNum);
        w.Write(scanNum); // duplicated, per cpp

        double precursorMz = 0;
        Precursor? precursor = null;
        SelectedIon? firstSi = null;
        if (!ms1)
        {
            precursor = spec.Precursors[0];
            firstSi = precursor.SelectedIons[0];
            precursorMz = precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>();
        }
        w.Write(precursorMz);

        // Retention time (minutes), v1 only stores rt+chargecount+peakcount; v2+ extras follow.
        float rt = 0;
        if (spec.ScanList.Scans.Count > 0)
        {
            var rtParam = spec.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
            if (!rtParam.IsEmpty)
                rt = (float)(rtParam.TimeInSeconds() / 60.0);
        }
        w.Write(rt);

        // v2 fields: basePeakIntensity, basePeakMz, conversionFactorA, conversionFactorB, TIC, ionInjectionTime
        w.Write((float)spec.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>());
        w.Write(spec.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>());
        w.Write(0.0); // conversionFactorA
        w.Write(0.0); // conversionFactorB
        w.Write(spec.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>());
        w.Write(0f);  // ionInjectionTime

        // Collect charge states.
        var charges = new List<int>();
        var masses = new List<double>();
        if (!ms1)
        {
            foreach (var si in precursor!.SelectedIons)
                CollectChargeStates(si, charges, masses);
        }
        w.Write(charges.Count);

        // v3 field: numEzStates (== numChargeStates if accurate mass present)
        bool hasAccurateMass = firstSi is not null && !firstSi.UserParam("accurate mass").IsEmpty;
        int numEzStates = hasAccurateMass ? charges.Count : 0;
        w.Write(numEzStates);

        int numPeaks = spec.DefaultArrayLength;
        w.Write(numPeaks);

        // Charge/mass pairs.
        for (int i = 0; i < charges.Count; i++)
        {
            w.Write(charges[i]);
            w.Write(masses[i]);
        }

        // EZ entries (only if accurate mass).
        if (hasAccurateMass)
        {
            for (int i = 0; i < charges.Count; i++)
            {
                w.Write(charges[i]);
                w.Write(masses[i]);
                w.Write(0f); // rTime
                w.Write(0f); // area
            }
        }

        var mzArr = spec.GetMZArray();
        var intArr = spec.GetIntensityArray();
        if (compress)
        {
            // Build the peak buffers in the wire format.
            int n = numPeaks;
            byte[] mzBytes = new byte[n * SizeDouble];
            byte[] intBytes = new byte[n * SizeFloat];
            for (int i = 0; i < n; i++)
            {
                double m = mzArr!.Data[i];
                float v = (float)intArr!.Data[i];
                Buffer.BlockCopy(BitConverter.GetBytes(m), 0, mzBytes, i * SizeDouble, SizeDouble);
                Buffer.BlockCopy(BitConverter.GetBytes(v), 0, intBytes, i * SizeFloat, SizeFloat);
            }
            byte[] mzCompressed = ZlibCompress(mzBytes);
            byte[] intCompressed = ZlibCompress(intBytes);
            w.Write(mzCompressed.Length);
            w.Write(intCompressed.Length);
            w.Write(mzCompressed);
            w.Write(intCompressed);
        }
        else
        {
            // Plain BMS1/BMS2: double mz + float intensity per peak.
            int n = numPeaks;
            for (int i = 0; i < n; i++)
            {
                w.Write(mzArr!.Data[i]);
                w.Write((float)intArr!.Data[i]);
            }
        }
    }

    // ===================== Read =====================

    /// <summary>Reads an MSn file from <paramref name="input"/> into <paramref name="msd"/>.</summary>
    public void Read(Stream input, MSData msd)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(msd);

        msd.CVs.Clear();
        msd.CVs.AddRange(MSData.DefaultCVList);
        msd.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);
        msd.FileDescription.FileContent.Set(CVID.MS_centroid_spectrum);
        msd.FileDescription.FileContent.Set(CVID.MS_scan_number_only_nativeID_format);

        var list = new SpectrumListSimple();
        if (FileType.IsText())
            ReadText(input, list);
        else
            ReadBinary(input, list);
        msd.Run.SpectrumList = list;
        msd.Run.ChromatogramList = new ChromatogramListSimple();
    }

    private void ReadText(Stream input, SpectrumListSimple list)
    {
        using var sr = new StreamReader(input, System.Text.Encoding.UTF8, true, 4096, leaveOpen: true);
        bool ms1 = FileType.IsMs1();
        string? line;
        Spectrum? current = null;
        double precursorMz = 0;
        var charges = new List<int>();
        var chargeMassPairs = new List<(int Charge, double Mass)>();
        var mz = new List<double>();
        var intensity = new List<double>();
        bool inPeakList = false;

        void FinalizeCurrent()
        {
            if (current is null) return;
            FinalizeTextSpectrum(current, mz, intensity, charges, chargeMassPairs, precursorMz, ms1);
            list.Spectra.Add(current);
            current = null;
            mz.Clear();
            intensity.Clear();
            charges.Clear();
            chargeMassPairs.Clear();
            inPeakList = false;
            precursorMz = 0;
        }

        while ((line = sr.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            char first = line[0];
            if (first == 'H') continue; // file-level header
            if (first == 'S')
            {
                FinalizeCurrent();
                current = NewMSnSpectrum(list.Spectra.Count, ms1);
                ParseSLine(line, current, ms1, out precursorMz);
                continue;
            }
            if (current is null) continue;
            switch (first)
            {
                case 'Z':
                    if (ms1)
                        throw new InvalidDataException("MSn: Z line found in MS1 file.");
                    ParseZLine(line, current, charges);
                    break;
                case 'I':
                    ParseILine(line, current, chargeMassPairs);
                    break;
                case 'D':
                    // cpp ignores D lines.
                    break;
                default:
                    inPeakList = true;
                    if (TryParsePeak(line, out double m, out double v))
                    {
                        mz.Add(m);
                        intensity.Add(v);
                    }
                    break;
            }
            _ = inPeakList; // suppress unused warning; matches cpp control flow
        }
        FinalizeCurrent();
    }

    private static Spectrum NewMSnSpectrum(int index, bool ms1)
    {
        var spec = new Spectrum
        {
            Index = index,
            DefaultArrayLength = 0,
        };
        spec.Params.Set(CVID.MS_MSn_spectrum);
        spec.Params.Set(CVID.MS_ms_level, ms1 ? 1 : 2);
        spec.Params.Set(CVID.MS_centroid_spectrum);
        if (!ms1)
        {
            var pre = new Precursor();
            pre.SelectedIons.Add(new SelectedIon());
            spec.Precursors.Add(pre);
        }
        return spec;
    }

    private static readonly char[] WhitespaceSplit = { ' ', '\t' };
    private static readonly System.Buffers.SearchValues<char> WhitespaceChars =
        System.Buffers.SearchValues.Create(" \t");

    private static void ParseSLine(string line, Spectrum spec, bool ms1, out double precursorMz)
    {
        precursorMz = 0;
        // Format: S <scan> <scan> [<precursor mz>]
        var parts = line.Split(WhitespaceSplit, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], NumberStyles.Integer, Inv, out int scan))
            throw new InvalidDataException("MSn: malformed S line: " + line);
        spec.Id = "scan=" + scan.ToString(Inv);
        if (!ms1 && parts.Length >= 4
            && double.TryParse(parts[^1], NumberStyles.Float, Inv, out double mz))
        {
            precursorMz = mz;
            spec.Precursors[0].IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, mz, CVID.MS_m_z);
        }
    }

    private static void ParseZLine(string line, Spectrum spec, List<int> charges)
    {
        // Format: Z <charge> <mass>
        var parts = line.Split(WhitespaceSplit, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3
            && int.TryParse(parts[1], NumberStyles.Integer, Inv, out int charge)
            && double.TryParse(parts[2], NumberStyles.Float, Inv, out double mass))
        {
            charges.Add(charge);
            double zMz = CalculateMassOverCharge(mass, charge, 1);
            spec.Params.UserParams.Add(new UserParam(
                "ms2 file charge state",
                charge.ToString(Inv) + " " + zMz.ToString("F4", Inv)));
        }
    }

    private static void ParseILine(string line, Spectrum spec, List<(int Charge, double Mass)> chargeMassPairs)
    {
        // I lines: "I\tRTime\t1.23", "I\tEZ\t2\t1234.56\t1.23\t1000", "I\tNativeID\t...", etc.
        if (line.Contains("RTime", StringComparison.Ordinal))
        {
            int idx = line.IndexOf("RTime", StringComparison.Ordinal);
            // Take the last whitespace-separated token as the value.
            var rest = line[(idx + 5)..].Trim();
            if (double.TryParse(rest, NumberStyles.Float, Inv, out double rt))
            {
                var scan = new Scan();
                scan.Set(CVID.MS_scan_start_time, rt * 60.0, CVID.UO_second);
                spec.ScanList.Scans.Add(scan);
            }
        }
        else if (line.Contains("\tEZ\t", StringComparison.Ordinal) || line.StartsWith("I EZ", StringComparison.Ordinal))
        {
            // I EZ charge mass rt area
            var parts = line.Split(WhitespaceSplit, StringSplitOptions.RemoveEmptyEntries);
            // First two tokens are "I" and "EZ"; charge + mass follow.
            int ezIdx = Array.IndexOf(parts, "EZ");
            if (ezIdx >= 0 && parts.Length >= ezIdx + 3
                && int.TryParse(parts[ezIdx + 1], NumberStyles.Integer, Inv, out int z)
                && double.TryParse(parts[ezIdx + 2], NumberStyles.Float, Inv, out double m))
            {
                chargeMassPairs.Add((z, m));
            }
        }
    }

    private static bool TryParsePeak(string line, out double mz, out double intensity)
    {
        mz = 0; intensity = 0;
        int sp = line.AsSpan().IndexOfAny(WhitespaceChars);
        if (sp < 0) return false;
        var mzSpan = line.AsSpan(0, sp);
        int restStart = sp;
        while (restStart < line.Length && (line[restStart] == ' ' || line[restStart] == '\t')) restStart++;
        int restEnd = restStart;
        while (restEnd < line.Length
               && line[restEnd] != ' ' && line[restEnd] != '\t'
               && line[restEnd] != '\r' && line[restEnd] != '\n') restEnd++;
        return double.TryParse(mzSpan, NumberStyles.Float, Inv, out mz)
               && double.TryParse(line.AsSpan(restStart, restEnd - restStart), NumberStyles.Float, Inv, out intensity);
    }

    private static void FinalizeTextSpectrum(
        Spectrum spec,
        List<double> mz,
        List<double> intensity,
        List<int> charges,
        List<(int Charge, double Mass)> chargeMassPairs,
        double precursorMz,
        bool ms1)
    {
        if (!ms1)
        {
            var precursor = spec.Precursors[0];
            var primary = precursor.SelectedIons[0];
            if (chargeMassPairs.Count == 0)
            {
                foreach (int c in charges)
                    primary.CVParams.Add(new CVParam(CVID.MS_possible_charge_state, c));
                primary.Set(CVID.MS_selected_ion_m_z, precursorMz, CVID.MS_m_z);
            }
            else
            {
                // Re-use the seeded SelectedIon for the first pair, then add one per remaining.
                for (int i = 0; i < chargeMassPairs.Count; i++)
                {
                    var (c, m) = chargeMassPairs[i];
                    SelectedIon target = i == 0 ? primary : new SelectedIon();
                    if (i > 0) precursor.SelectedIons.Add(target);
                    target.CVParams.Add(new CVParam(CVID.MS_charge_state, c));
                    target.UserParams.Add(new UserParam("accurate mass", m.ToString("R", Inv), "xsd:double"));
                    target.Set(CVID.MS_selected_ion_m_z, CalculateMassOverCharge(m, c, 1), CVID.MS_m_z);
                }
            }
        }
        ComputePeakStats(spec, mz, intensity);
    }

    private void ReadBinary(Stream input, SpectrumListSimple list)
    {
        using var br = new BinaryReader(input, System.Text.Encoding.ASCII, leaveOpen: true);
        int fileTypeInt = br.ReadInt32();
        int version = br.ReadInt32();
        var header = MSnHeader.Read(br);
        _ = header; // discarded — we don't currently surface header lines anywhere
        if (version > 3)
            throw new InvalidDataException($"MSn binary version {version} not supported (max 3).");

        bool ms1 = FileType.IsMs1();
        bool compressed = FileType.IsCompressed();

        while (true)
        {
            // Probe: is there at least one more spectrum header to read?
            if (input.Position >= input.Length) break;
            var spec = NewMSnSpectrum(list.Spectra.Count, ms1);
            if (!TryReadBinarySpectrum(br, spec, version, ms1, compressed)) break;
            list.Spectra.Add(spec);
        }
    }

    private static bool TryReadBinarySpectrum(BinaryReader br, Spectrum spec, int version, bool ms1, bool compressed)
    {
        // Detect EOF inside a fragmented header.
        if (br.BaseStream.Position + 2 * SizeInt > br.BaseStream.Length) return false;

        int scanNum = br.ReadInt32();
        br.ReadInt32(); // duplicated scan number
        double precursorMz = br.ReadDouble();
        float rt = br.ReadSingle();

        float basePeakIntensity = 0;
        double basePeakMz = 0;
        double tic = 0;
        if (version >= 2)
        {
            basePeakIntensity = br.ReadSingle();
            basePeakMz = br.ReadDouble();
            br.ReadDouble(); // conversionFactorA
            br.ReadDouble(); // conversionFactorB
            tic = br.ReadDouble();
            br.ReadSingle(); // ionInjectionTime
        }

        int numChargeStates = br.ReadInt32();
        int numEzStates = 0;
        if (version == 3)
            numEzStates = br.ReadInt32();
        int numPeaks = br.ReadInt32();

        spec.Id = "scan=" + scanNum.ToString(Inv);
        if (!ms1)
            spec.Precursors[0].IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, precursorMz, CVID.MS_m_z);

        var charges = new List<int>();
        var masses = new List<double>();
        if (!ms1)
        {
            for (int i = 0; i < numChargeStates; i++)
            {
                charges.Add(br.ReadInt32());
                masses.Add(br.ReadDouble());
            }
            var precursor = spec.Precursors[0];
            var primary = precursor.SelectedIons[0];
            if (numEzStates == 0)
            {
                foreach (int c in charges)
                    primary.CVParams.Add(new CVParam(CVID.MS_possible_charge_state, c));
                primary.Set(CVID.MS_selected_ion_m_z, precursorMz, CVID.MS_m_z);
            }
            else
            {
                var ezCharges = new int[numEzStates];
                var ezMasses = new double[numEzStates];
                for (int i = 0; i < numEzStates; i++)
                {
                    ezCharges[i] = br.ReadInt32();
                    ezMasses[i] = br.ReadDouble();
                    br.ReadSingle(); // pRTime
                    br.ReadSingle(); // pArea
                }
                for (int i = 0; i < numEzStates; i++)
                {
                    SelectedIon target = i == 0 ? primary : new SelectedIon();
                    if (i > 0) precursor.SelectedIons.Add(target);
                    target.CVParams.Add(new CVParam(CVID.MS_charge_state, ezCharges[i]));
                    target.UserParams.Add(new UserParam("accurate mass", ezMasses[i].ToString("R", Inv), "xsd:double"));
                    target.Set(CVID.MS_selected_ion_m_z, CalculateMassOverCharge(ezMasses[i], ezCharges[i], 1), CVID.MS_m_z);
                }
            }
        }
        else
        {
            // For MS1, still drain any charge entries (shouldn't exist for ms1, but be safe).
            for (int i = 0; i < numChargeStates; i++)
            {
                br.ReadInt32();
                br.ReadDouble();
            }
        }

        // Retention time scan.
        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, rt * 60.0, CVID.UO_second);
        spec.ScanList.Scans.Add(scan);

        // Peaks.
        double[] mz = new double[numPeaks];
        double[] intensity = new double[numPeaks];
        if (compressed)
        {
            int mzLen = br.ReadInt32();
            int intLen = br.ReadInt32();
            byte[] mzBuf = br.ReadBytes(mzLen);
            byte[] intBuf = br.ReadBytes(intLen);
            byte[] mzPlain = ZlibDecompress(mzBuf, numPeaks * SizeDouble);
            byte[] intPlain = ZlibDecompress(intBuf, numPeaks * SizeFloat);
            for (int i = 0; i < numPeaks; i++)
            {
                mz[i] = BitConverter.ToDouble(mzPlain, i * SizeDouble);
                intensity[i] = BitConverter.ToSingle(intPlain, i * SizeFloat);
            }
        }
        else
        {
            for (int i = 0; i < numPeaks; i++)
            {
                mz[i] = br.ReadDouble();
                intensity[i] = br.ReadSingle();
            }
        }

        ComputePeakStats(spec, mz, intensity);

        // v2+ writes basePeak/tic into the header; reading them back is informational only
        // since ComputePeakStats already derives them. Use the read values when no peaks
        // (otherwise cpp's reader would leave the params at 0).
        if (numPeaks == 0)
        {
            spec.Params.Set(CVID.MS_base_peak_intensity, basePeakIntensity);
            spec.Params.Set(CVID.MS_base_peak_m_z, basePeakMz);
            spec.Params.Set(CVID.MS_total_ion_current, tic);
        }
        return true;
    }

    private static void ComputePeakStats(Spectrum spec, IReadOnlyList<double> mz, IReadOnlyList<double> intensity)
    {
        double tic = 0;
        double basePeakMz = 0;
        double basePeakIntensity = 0;
        double lowMz = double.MaxValue;
        double highMz = 0;
        int n = Math.Min(mz.Count, intensity.Count);
        for (int i = 0; i < n; i++)
        {
            double m = mz[i];
            double v = intensity[i];
            tic += v;
            if (v > basePeakIntensity) { basePeakMz = m; basePeakIntensity = v; }
            if (m < lowMz) lowMz = m;
            if (m > highMz) highMz = m;
        }
        spec.DefaultArrayLength = n;
        if (n > 0)
        {
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
            spec.Params.Set(CVID.MS_lowest_observed_m_z, lowMz);
            spec.Params.Set(CVID.MS_highest_observed_m_z, highMz);
        }
        else
        {
            spec.SetMZIntensityArrays(Array.Empty<double>(), Array.Empty<double>(), CVID.MS_number_of_detector_counts);
        }
        spec.Params.Set(CVID.MS_total_ion_current, tic);
        spec.Params.Set(CVID.MS_base_peak_m_z, basePeakMz);
        spec.Params.Set(CVID.MS_base_peak_intensity, basePeakIntensity);
    }

    // ===================== Helpers =====================

    private static void CollectChargeStates(SelectedIon si, List<int> charges, List<double> masses)
    {
        var chargeParam = si.CvParam(CVID.MS_charge_state);
        double mz = si.CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>();
        var massParam = si.UserParam("accurate mass");
        if (!chargeParam.IsEmpty)
        {
            int c = chargeParam.ValueAs<int>();
            charges.Add(c);
            masses.Add(massParam.IsEmpty ? CalculateMass(mz, c) : massParam.ValueAs<double>());
        }
        else
        {
            foreach (var p in si.CVParams)
            {
                if (p.Cvid == CVID.MS_possible_charge_state)
                {
                    int c = p.ValueAs<int>();
                    charges.Add(c);
                    masses.Add(CalculateMass(mz, c));
                }
            }
        }
    }

    private static double CalculateMass(double mz, int charge)
        => (mz * charge) - ((charge - 1) * ProtonMass);

    private static double CalculateMassOverCharge(double mass, int charge, int chargesOnMass)
    {
        double neutralMass = mass - (chargesOnMass * ProtonMass);
        return (neutralMass + (charge * ProtonMass)) / charge;
    }

    private static int ExtractScanNumber(Spectrum spec)
    {
        // Try "scan=" name-value pair first.
        string id = spec.Id;
        if (!string.IsNullOrEmpty(id))
        {
            int scanIdx = id.IndexOf("scan=", StringComparison.Ordinal);
            if (scanIdx >= 0)
            {
                int start = scanIdx + 5;
                int end = start;
                while (end < id.Length && char.IsDigit(id[end])) end++;
                if (end > start && int.TryParse(id.AsSpan(start, end - start), NumberStyles.Integer, Inv, out int v))
                    return v;
            }
            // Or "index=N".
            int idxIdx = id.IndexOf("index=", StringComparison.Ordinal);
            if (idxIdx >= 0)
            {
                int start = idxIdx + 6;
                int end = start;
                while (end < id.Length && char.IsDigit(id[end])) end++;
                if (end > start && int.TryParse(id.AsSpan(start, end - start), NumberStyles.Integer, Inv, out int v))
                    return v + 1;
            }
            // Or just a bare integer.
            if (int.TryParse(id, NumberStyles.Integer, Inv, out int direct)) return direct;
        }
        return spec.Index + 1;
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(data, 0, data.Length);
        }
        return ms.ToArray();
    }

    private static byte[] ZlibDecompress(byte[] data, int expectedSize)
    {
        using var src = new MemoryStream(data);
        using var z = new ZLibStream(src, CompressionMode.Decompress);
        var dest = new byte[expectedSize];
        int read = 0;
        while (read < expectedSize)
        {
            int n = z.Read(dest, read, expectedSize - read);
            if (n == 0) break;
            read += n;
        }
        if (read != expectedSize)
            throw new InvalidDataException($"MSn: expected {expectedSize} bytes of decompressed peak data, got {read}.");
        return dest;
    }
}
