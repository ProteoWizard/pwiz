using System.Globalization;
using System.Text;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Spectra;

// CA1822: Keep Read/Write as instance methods — the public API is instance-based so future additions
// (iteration listeners, progress callbacks, custom encoder config) don't break compatibility.
#pragma warning disable CA1822

namespace Pwiz.Data.MsData.Mgf;

/// <summary>
/// Reads and writes MGF (Mascot Generic Format) — a simple line-based text format for MS/MS spectra.
/// </summary>
/// <remarks>
/// Port of pwiz::msdata::Serializer_MGF + SpectrumList_MGF (eager read — no lazy seek-by-offset yet).
/// Every MGF spectrum is treated as MSn, MS level 2, single precursor, centroided (that's what the format assumes).
/// Only MS2+ spectra are written; MS1 spectra are skipped (MGF has no representation for them).
/// </remarks>
public sealed class MgfSerializer
{
    private static readonly NumberFormatInfo s_invariant = CultureInfo.InvariantCulture.NumberFormat;

    // ---------- Write ----------

    /// <summary>Writes <paramref name="msd"/> as MGF text.</summary>
    public string Write(MSData msd)
    {
        var sb = new StringBuilder();
        using var w = new StringWriter(sb) { NewLine = "\n" };
        Write(msd, w);
        return sb.ToString();
    }

    /// <summary>Writes <paramref name="msd"/> as MGF to the given <see cref="TextWriter"/>.</summary>
    public void Write(MSData msd, TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(msd);
        ArgumentNullException.ThrowIfNull(w);
        if (msd.Run.SpectrumList is null) return;

        int scansWritten = 0;
        for (int i = 0; i < msd.Run.SpectrumList.Count; i++)
        {
            var spec = msd.Run.SpectrumList.GetSpectrum(i, getBinaryData: true);
            if (WriteSpectrum(w, spec)) scansWritten++;
        }
    }

    /// <summary>
    /// True iff <paramref name="spec"/> would be emitted by <see cref="Write(MSData, TextWriter)"/>.
    /// MGF carries only MS2+ peak lists with at least one precursor + selected ion.
    /// </summary>
    public static bool IsMgfWritable(Spectrum spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        int msLevel = spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
        if (msLevel <= 1) return false;
        if (spec.Precursors.Count == 0 || spec.Precursors[0].SelectedIons.Count == 0) return false;
        return true;
    }

    private static bool WriteSpectrum(TextWriter w, Spectrum spec)
    {
        if (!IsMgfWritable(spec)) return false;

        var precursor = spec.Precursors[0];
        var si = precursor.SelectedIons[0];
        var scan = spec.ScanList.Scans.Count > 0 ? spec.ScanList.Scans[0] : null;

        w.WriteLine("BEGIN IONS");

        string title = spec.Params.CvParam(CVID.MS_spectrum_title).Value;
        w.Write("TITLE=");
        w.WriteLine(string.IsNullOrEmpty(title) ? spec.Id : title);

        var scanTimeParam = scan?.CvParam(CVID.MS_scan_start_time);
        if (scanTimeParam is not null && !scanTimeParam.IsEmpty)
        {
            w.Write("RTINSECONDS=");
            w.WriteLine(scanTimeParam.TimeInSeconds().ToString("R", s_invariant));
        }

        var mzParam = si.CvParam(CVID.MS_selected_ion_m_z);
        var intensityParam = si.CvParam(CVID.MS_peak_intensity);
        w.Write("PEPMASS=");
        w.Write(mzParam.ValueFixedNotation());
        if (!intensityParam.IsEmpty)
        {
            w.Write(' ');
            w.Write(intensityParam.ValueFixedNotation());
        }
        w.WriteLine();

        var charge = si.CvParam(CVID.MS_charge_state);
        bool negative = spec.Params.HasCVParam(CVID.MS_negative_scan);
        if (!charge.IsEmpty)
        {
            w.Write("CHARGE=");
            w.Write(charge.Value);
            w.WriteLine(negative ? '-' : '+');
        }
        else
        {
            // Emit all possible charge states if present.
            var possible = si.CVParams.Where(p => p.Cvid == CVID.MS_possible_charge_state).ToList();
            if (possible.Count > 0)
            {
                w.Write("CHARGE=");
                w.WriteLine(string.Join(" and ", possible.Select(p => p.Value + (negative ? '-' : '+'))));
            }
        }

        var mzArr = spec.GetMZArray();
        var intArr = spec.GetIntensityArray();
        if (mzArr is not null && intArr is not null)
        {
            int n = System.Math.Min(mzArr.Data.Count, intArr.Data.Count);
            for (int p = 0; p < n; p++)
            {
                w.Write(mzArr.Data[p].ToString("R", s_invariant));
                w.Write(' ');
                w.WriteLine(intArr.Data[p].ToString("R", s_invariant));
            }
        }

        w.WriteLine("END IONS");
        return true;
    }

    // ---------- Read ----------

    /// <summary>Parses MGF text into a new <see cref="MSData"/>.</summary>
    public MSData Read(string text)
    {
        using var reader = new StringReader(text);
        return Read(reader);
    }

    /// <summary>Parses MGF from a <see cref="TextReader"/> into a new <see cref="MSData"/>.</summary>
    public MSData Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var msd = new MSData();
        msd.CVs.AddRange(MSData.DefaultCVList);

        // MGF always contains MSn centroided spectra.
        msd.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);
        msd.FileDescription.FileContent.Set(CVID.MS_centroid_spectrum);

        var spectrumList = new SpectrumListSimple();
        msd.Run.SpectrumList = spectrumList;

        string? line;
        Spectrum? current = null;
        bool inPeakList = false;
        var mz = new List<double>();
        var intensity = new List<double>();
        SelectedIon? selectedIon = null;
        Scan? scan = null;
        bool negativePolarity = false;

        while ((line = reader.ReadLine()) is not null)
        {
            string trimmed = line.TrimStart();
            if (trimmed.Length == 0) continue;

            // Comment lines outside of BEGIN IONS.
            if (current is null && (trimmed[0] == '#' || trimmed[0] == ';' || trimmed[0] == '!' || trimmed[0] == '/'))
                continue;

            if (trimmed.StartsWith("BEGIN IONS", StringComparison.Ordinal))
            {
                if (current is not null)
                    throw new InvalidDataException("MGF: nested BEGIN IONS without prior END IONS.");
                current = NewSpectrum(spectrumList.Spectra.Count);
                scan = current.ScanList.Scans[0];
                selectedIon = current.Precursors[0].SelectedIons[0];
                mz.Clear();
                intensity.Clear();
                inPeakList = false;
                negativePolarity = false;
                continue;
            }

            if (trimmed.StartsWith("END IONS", StringComparison.Ordinal))
            {
                if (current is null)
                    throw new InvalidDataException("MGF: END IONS without BEGIN IONS.");
                FinalizeSpectrum(current, mz, intensity, negativePolarity);
                spectrumList.Spectra.Add(current);
                current = null;
                continue;
            }

            if (current is null) continue;

            // Inside BEGIN IONS — either a KEY=VALUE tag or a "m/z intensity" peak line.
            int eq = trimmed.IndexOf('=');
            if (!inPeakList && eq > 0)
            {
                string key = trimmed[..eq];
                string value = trimmed[(eq + 1)..].Trim();
                HandleTag(current, scan!, selectedIon!, key, value, ref negativePolarity);
            }
            else
            {
                inPeakList = true;
                if (TryParsePeak(trimmed, out double m, out double v))
                {
                    mz.Add(m);
                    intensity.Add(v);
                }
            }
        }

        if (current is not null)
            throw new InvalidDataException("MGF: EOF inside BEGIN IONS without matching END IONS.");

        return msd;
    }

    private static Spectrum NewSpectrum(int index)
    {
        var spec = new Spectrum
        {
            Index = index,
            Id = $"index={index.ToString(CultureInfo.InvariantCulture)}",
            DefaultArrayLength = 0,
        };
        spec.Params.Set(CVID.MS_MSn_spectrum);
        spec.Params.Set(CVID.MS_ms_level, 2);
        spec.Params.Set(CVID.MS_centroid_spectrum);
        spec.ScanList.Set(CVID.MS_no_combination);
        spec.ScanList.Scans.Add(new Scan());
        var pre = new Precursor();
        pre.SelectedIons.Add(new SelectedIon());
        spec.Precursors.Add(pre);
        return spec;
    }

    private static void HandleTag(Spectrum spec, Scan scan, SelectedIon si, string key, string value, ref bool negativePolarity)
    {
        switch (key)
        {
            case "TITLE":
                spec.Params.Set(CVID.MS_spectrum_title, value);
                break;
            case "PEPMASS":
            {
                int sp = value.IndexOf(' ');
                if (sp >= 0)
                {
                    si.Set(CVID.MS_selected_ion_m_z, value[..sp], CVID.MS_m_z);
                    si.Set(CVID.MS_peak_intensity, value[(sp + 1)..].Trim(), CVID.MS_number_of_detector_counts);
                }
                else
                {
                    si.Set(CVID.MS_selected_ion_m_z, value, CVID.MS_m_z);
                }
                break;
            }
            case "CHARGE":
            {
                string v = value.Trim(' ', '\t', '\r');
                negativePolarity = v.EndsWith('-');
                string[] parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    foreach (var part in parts)
                    {
                        if (part == "and") continue;
                        string trimmed = part.Trim('+', '-');
                        if (int.TryParse(trimmed, NumberStyles.Integer, s_invariant, out int c))
                            si.CVParams.Add(new CVParam(CVID.MS_possible_charge_state, c));
                    }
                }
                else
                {
                    string trimmed = v.Trim('+', '-');
                    if (int.TryParse(trimmed, NumberStyles.Integer, s_invariant, out int c))
                        si.Set(CVID.MS_charge_state, c);
                }
                break;
            }
            case "RTINSECONDS":
                if (double.TryParse(value, NumberStyles.Float, s_invariant, out double rt))
                    scan.Set(CVID.MS_scan_start_time, rt, CVID.UO_second);
                break;
            case "SCANS":
                spec.Params.Set(CVID.MS_peak_list_scans, value);
                break;
        }
    }

    private static bool TryParsePeak(string line, out double mz, out double intensity)
    {
        mz = 0; intensity = 0;
        int sp = line.IndexOf(' ');
        if (sp < 0) sp = line.IndexOf('\t');
        if (sp < 0) return false;

        var mzSpan = line.AsSpan(0, sp);

        int restStart = sp;
        while (restStart < line.Length && (line[restStart] == ' ' || line[restStart] == '\t')) restStart++;

        int restEnd = restStart;
        while (restEnd < line.Length
               && line[restEnd] != ' ' && line[restEnd] != '\t'
               && line[restEnd] != '\r' && line[restEnd] != '\n') restEnd++;

        var intSpan = line.AsSpan(restStart, restEnd - restStart);

        return double.TryParse(mzSpan, NumberStyles.Float, s_invariant, out mz)
               && double.TryParse(intSpan, NumberStyles.Float, s_invariant, out intensity);
    }

    private static void FinalizeSpectrum(Spectrum spec, List<double> mz, List<double> intensity, bool negativePolarity)
    {
        spec.Params.Set(negativePolarity ? CVID.MS_negative_scan : CVID.MS_positive_scan);

        double tic = 0, basePeakMz = 0, basePeakIntensity = 0;
        double lowMz = double.MaxValue, highMz = 0;
        for (int i = 0; i < mz.Count; i++)
        {
            double m = mz[i]; double v = intensity[i];
            tic += v;
            if (v > basePeakIntensity) { basePeakMz = m; basePeakIntensity = v; }
            if (m < lowMz) lowMz = m;
            if (m > highMz) highMz = m;
        }

        spec.DefaultArrayLength = mz.Count;
        if (mz.Count > 0)
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);

        if (mz.Count > 0) spec.Params.Set(CVID.MS_lowest_observed_m_z, lowMz);
        if (mz.Count > 0) spec.Params.Set(CVID.MS_highest_observed_m_z, highMz);
        spec.Params.Set(CVID.MS_total_ion_current, tic);
        spec.Params.Set(CVID.MS_base_peak_m_z, basePeakMz);
        spec.Params.Set(CVID.MS_base_peak_intensity, basePeakIntensity);
    }
}
