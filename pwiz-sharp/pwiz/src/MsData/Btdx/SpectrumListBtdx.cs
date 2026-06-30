using System.Globalization;
using System.Xml;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Btdx;

/// <summary>
/// Reads the Bruker BioTools DataExchange (BTDX) XML format into an in-memory
/// <see cref="SpectrumListSimple"/>.
/// </summary>
/// <remarks>
/// <para>Port of pwiz::msdata::SpectrumList_BTDX. cpp does lazy seek-by-offset parsing;
/// pwiz-sharp eagerly populates a <see cref="SpectrumListSimple"/>, mirroring the
/// existing MGF / mzML adapters. BTDX files are tiny (one spectrum per MALDI compound)
/// so the simpler approach is fine.</para>
/// <para>Every BTDX spectrum is MSn + centroid + has a (possibly absent) precursor.</para>
/// </remarks>
public static class SpectrumListBtdx
{
    /// <summary>Parses a BTDX XML document from <paramref name="stream"/> into a fresh <see cref="SpectrumListSimple"/>.</summary>
    public static SpectrumListSimple Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var list = new SpectrumListSimple();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            CloseInput = false,
        };
        using var xr = XmlReader.Create(stream, settings);
        ReadDocument(xr, list);
        return list;
    }

    private static void ReadDocument(XmlReader xr, SpectrumListSimple list)
    {
        while (xr.Read())
        {
            if (xr.NodeType == XmlNodeType.Element && xr.LocalName == "cmpd")
                list.Spectra.Add(ReadCompound(xr, list.Spectra.Count));
        }
    }

    private static Spectrum ReadCompound(XmlReader xr, int index)
    {
        var spec = new Spectrum
        {
            Index = index,
            Id = xr.GetAttribute("cmpdnr") ?? string.Empty,
        };
        spec.Params.Set(CVID.MS_MSn_spectrum);
        spec.Params.Set(CVID.MS_centroid_spectrum);
        spec.ScanList.Set(CVID.MS_no_combination);

        // rt + rt_unit go on a Scan as scan-start-time. cpp creates the Scan only when rt is
        // present; we always add one Scan so downstream tools can attach scan windows etc.
        string? rt = xr.GetAttribute("rt");
        string? rtUnit = xr.GetAttribute("rt_unit");
        var scan = new Scan();
        if (!string.IsNullOrEmpty(rt))
        {
            CVID units = rtUnit switch
            {
                "s" => CVID.UO_second,
                "m" => CVID.UO_minute,
                "h" => CVID.UO_hour,
                _ => CVID.CVID_Unknown,
            };
            scan.Set(CVID.MS_scan_start_time, rt, units);
        }
        spec.ScanList.Scans.Add(scan);

        if (xr.IsEmptyElement)
            return spec;

        var mz = new List<double>();
        var intensity = new List<double>();

        // Walk every descendant of <cmpd> until its end tag. We handle ms_spectrum specially
        // by descending into it: ms_peaks (and pk) are physically nested inside <ms_spectrum>,
        // so we can't rely on Skip() — that would jump past the peak data we need.
        while (xr.Read())
        {
            if (xr.NodeType == XmlNodeType.EndElement && xr.LocalName == "cmpd")
                break;
            if (xr.NodeType != XmlNodeType.Element) continue;

            switch (xr.LocalName)
            {
                case "title":
                    // cpp comments "WTF is this?" and ignores it; matching that.
                    SkipElement(xr);
                    break;

                case "precursor":
                    ReadPrecursor(xr, spec);
                    SkipElement(xr);
                    break;

                case "ms_spectrum":
                    // Set ms-level from msms_stage attribute. Don't Skip — its <ms_peaks>
                    // child needs to be visited by the outer loop.
                    ReadMsSpectrum(xr, spec);
                    break;

                case "ms_peaks":
                    ReadMsPeaks(xr, mz, intensity);
                    break;

                default:
                    SkipElement(xr);
                    break;
            }
        }

        FinalizeSpectrum(spec, mz, intensity);
        return spec;
    }

    /// <summary>
    /// Advance the cursor past the current element's end tag without descending into its
    /// content. For empty elements this is a no-op (the cursor stays on the empty element,
    /// and the next outer Read() advances to the next sibling). For non-empty elements we
    /// call Skip() which consumes the EndElement.
    /// </summary>
    private static void SkipElement(XmlReader xr)
    {
        if (xr.IsEmptyElement) return;
        // Walk to the matching end element, then return — leaving the cursor ON the end
        // element. The outer loop's next Read() advances to the next sibling.
        int depth = xr.Depth;
        while (xr.Read())
        {
            if (xr.Depth == depth && xr.NodeType == XmlNodeType.EndElement)
                return;
        }
    }

    private static void ReadPrecursor(XmlReader xr, Spectrum spec)
    {
        string? mz = xr.GetAttribute("mz");
        string? i = xr.GetAttribute("i");
        string? z = xr.GetAttribute("z");
        string? targetPosition = xr.GetAttribute("TargetPosition");
        string? chipPosition = xr.GetAttribute("ChipPosition");

        if (!string.IsNullOrEmpty(targetPosition) && !string.IsNullOrEmpty(chipPosition))
            spec.SpotId = targetPosition + "," + chipPosition;

        double mzValue = ParseDouble(mz);
        double iValue = ParseDouble(i);
        int zValue = ParseInt(z);
        spec.Precursors.Add(new Precursor(mzValue, iValue, zValue, CVID.MS_number_of_detector_counts));
    }

    private static void ReadMsSpectrum(XmlReader xr, Spectrum spec)
    {
        string? msmsStage = xr.GetAttribute("msms_stage");
        // cpp: empty msms_stage => MS level 1, otherwise the attribute value.
        if (string.IsNullOrEmpty(msmsStage))
            spec.Params.Set(CVID.MS_ms_level, 1);
        else if (int.TryParse(msmsStage, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
            spec.Params.Set(CVID.MS_ms_level, level);
        else
            spec.Params.Set(CVID.MS_ms_level, msmsStage);
    }

    private static void ReadMsPeaks(XmlReader xr, List<double> mz, List<double> intensity)
    {
        if (xr.IsEmptyElement) return;
        int outerDepth = xr.Depth;
        while (xr.Read())
        {
            if (xr.NodeType == XmlNodeType.EndElement
                && xr.Depth == outerDepth
                && xr.LocalName == "ms_peaks")
                return;
            if (xr.NodeType != XmlNodeType.Element) continue;
            if (xr.LocalName != "pk")
            {
                SkipElement(xr);
                continue;
            }
            double m = ParseDouble(xr.GetAttribute("mz"));
            double v = ParseDouble(xr.GetAttribute("i"));
            mz.Add(m);
            intensity.Add(v);
            // <pk /> is always empty; no Skip needed.
        }
    }

    private static void FinalizeSpectrum(Spectrum spec, List<double> mz, List<double> intensity)
    {
        double tic = 0;
        double basePeakMz = 0;
        double basePeakIntensity = 0;
        for (int i = 0; i < mz.Count; i++)
        {
            double v = intensity[i];
            tic += v;
            if (v > basePeakIntensity) { basePeakMz = mz[i]; basePeakIntensity = v; }
        }
        spec.DefaultArrayLength = mz.Count;
        if (mz.Count > 0)
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        else
            // cpp sets an empty m/z + intensity array even when there are no peaks.
            spec.SetMZIntensityArrays(Array.Empty<double>(), Array.Empty<double>(), CVID.MS_number_of_detector_counts);

        spec.Params.Set(CVID.MS_total_ion_current, tic);
        spec.Params.Set(CVID.MS_base_peak_m_z, basePeakMz);
        spec.Params.Set(CVID.MS_base_peak_intensity, basePeakIntensity);
    }

    private static double ParseDouble(string? s) =>
        string.IsNullOrEmpty(s) ? 0 : double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int ParseInt(string? s) =>
        string.IsNullOrEmpty(s) ? 0 : int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
}
