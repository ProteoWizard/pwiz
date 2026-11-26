using System;
using System.Collections.Generic;
using Pwiz.Analysis;
using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.TestHarness;

/// <summary>
/// Checks a vendor spectrum list's <see cref="DetailLevel.InstantMetadata"/> reads against full
/// reads of the same spectra, plain and through vendor peak picking. Skyline asks every spectrum
/// for its ms level and start time at instant metadata before it reads the data, so a reader that
/// decodes the peaks there pays for each spectrum several times per import, and one that returns
/// early must still report what a full read would.
/// </summary>
/// <remarks>
/// Instant metadata is a subset of a full read: the ms level, start time and polarity must be
/// there and match, and a profile/centroid term, when present, must match too. TestHarness does
/// not reference MSTest, so this returns the disagreements for the calling test to assert on.
/// </remarks>
public static class DetailLevelChecks
{
    /// <summary>
    /// Reads <paramref name="path"/> with <paramref name="reader"/> and compares its first
    /// <paramref name="count"/> spectra as <see cref="InstantMetadataMismatches(ISpectrumList, int)"/>
    /// does. Lets <see cref="VendorSupportNotEnabledException"/> through for the caller to report.
    /// </summary>
    public static IReadOnlyList<string> InstantMetadataMismatches(IReader reader, string path, int count)
    {
        using var msd = new MSData();
        reader.Read(path, msd);
        var list = msd.Run.SpectrumList ?? throw new InvalidOperationException(path + " has no spectrum list");
        if (list.Count == 0)
            return new[] { path + " has no spectra to compare" };
        return InstantMetadataMismatches(list, count);
    }

    /// <summary>
    /// Compares the first <paramref name="count"/> spectra of <paramref name="list"/>, and of the
    /// same list wrapped in vendor peak picking, at instant metadata against full data. Returns
    /// one message per disagreement; empty when they all agree.
    /// </summary>
    /// <remarks>
    /// The peak picker falls back to local maxima where the vendor cannot centroid (Agilent
    /// quadrupoles). Only the plain list must leave the arrays out; the picker may read peaks for
    /// a list that keeps its spectra in memory. Non-MS spectra (UV, DAD and PDA traces) have no
    /// ms level and are skipped.
    /// </remarks>
    public static IReadOnlyList<string> InstantMetadataMismatches(ISpectrumList list, int count)
    {
        var problems = new List<string>();
        var centroided = new SpectrumList_PeakPicker(list, new LocalMaximumPeakDetector(3),
            preferVendorPeakPicking: true, msLevelsToPeakPick: "1-");
        count = Math.Min(count, list.Count);
        for (int i = 0; i < count; i++)
        {
            var plainFull = list.GetSpectrum(i, DetailLevel.FullData);
            if (!plainFull.Params.HasCVParam(CVID.MS_ms_level))
                continue;
            var plainInstant = list.GetSpectrum(i, DetailLevel.InstantMetadata);
            if (plainInstant.BinaryDataArrays.Count != 0)
                problems.Add($"plain {plainInstant.Id}: instant metadata carries {plainInstant.BinaryDataArrays.Count} arrays");
            Compare(plainInstant, plainFull, "plain", problems);
            Compare(centroided.GetSpectrum(i, DetailLevel.InstantMetadata), centroided.GetSpectrum(i, DetailLevel.FullData), "centroided", problems);
        }
        return problems;
    }

    private static readonly CVID[] REQUIRED = { CVID.MS_ms_level };
    private static readonly CVID[] MATCH_WHEN_PRESENT =
    {
        CVID.MS_positive_scan, CVID.MS_negative_scan, CVID.MS_profile_spectrum, CVID.MS_centroid_spectrum,
    };

    private static void Compare(Spectrum instant, Spectrum full, string path, List<string> problems)
    {
        string id = $"{path} {full.Id}";
        if (instant.Id != full.Id)
            problems.Add($"{id}: instant id {instant.Id}");

        foreach (var cvid in REQUIRED)
            CompareParam(instant.Params, full.Params, cvid, required: true, id, problems);
        foreach (var cvid in MATCH_WHEN_PRESENT)
            CompareParam(instant.Params, full.Params, cvid, required: false, id, problems);

        // A full read carries the term the instant read left out - or neither has it.
        bool instantHasRepresentation = instant.Params.HasCVParam(CVID.MS_profile_spectrum) ||
                                        instant.Params.HasCVParam(CVID.MS_centroid_spectrum);
        if (instantHasRepresentation &&
            instant.Params.HasCVParam(CVID.MS_centroid_spectrum) != full.Params.HasCVParam(CVID.MS_centroid_spectrum))
        {
            problems.Add($"{id}: representation differs");
        }

        if (instant.ScanList.Scans.Count == 0 || full.ScanList.Scans.Count == 0)
        {
            if (full.ScanList.Scans.Count != 0)
                problems.Add($"{id}: instant metadata has no scan");
            return;
        }
        double instantTime = instant.ScanList.Scans[0].CvParamValueOrDefault(CVID.MS_scan_start_time, double.NaN);
        double fullTime = full.ScanList.Scans[0].CvParamValueOrDefault(CVID.MS_scan_start_time, double.NaN);
        if (!instantTime.Equals(fullTime))
            problems.Add($"{id}: start time {instantTime} vs {fullTime}");
    }

    private static void CompareParam(ParamContainer instant, ParamContainer full, CVID cvid, bool required,
        string id, List<string> problems)
    {
        bool inInstant = instant.HasCVParam(cvid);
        if (!inInstant && !required)
            return;
        var instantParam = instant.CvParam(cvid);
        var fullParam = full.CvParam(cvid);
        if (!inInstant || !Equals(instantParam, fullParam))
            problems.Add($"{id}: {cvid} instant [{instantParam}] vs full [{fullParam}]");
    }
}
