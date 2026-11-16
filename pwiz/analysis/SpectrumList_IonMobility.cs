using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707 // SpectrumList_IonMobility name matches cpp pwiz

namespace Pwiz.Analysis;

/// <summary>
/// Wraps a <see cref="ISpectrumList"/> and exposes a unified ion-mobility / CCS-conversion
/// API across vendors. Port of cpp <c>pwiz::analysis::SpectrumList_IonMobility</c>.
/// </summary>
/// <remarks>
/// <para>The wrapper dispatches in three layers:</para>
/// <list type="bullet">
///   <item>If the innermost list implements <see cref="IIonMobilitySpectrumList"/>, ask it
///         what units it reports + whether the data is combined / SONAR. This is the
///         normal vendor path (Waters today; other vendors as they plumb in).</item>
///   <item>If it implements <see cref="IIonMobilityCcsConversion"/>, route
///         <see cref="IonMobilityToCcs"/> / <see cref="CcsToIonMobility"/> through it.</item>
///   <item>Otherwise — typical mzML re-read case — probe the first few spectra for IM CV
///         params on <c>Scan</c> and IM array CV-children on the binary arrays, so an
///         mzML round-trip preserves the IM reporting.</item>
/// </list>
/// <para>Vendors that don't yet implement <see cref="IIonMobilitySpectrumList"/> are
/// transparently handled by the mzML sniff path when the file goes through the mzML
/// rebuild route, but won't support direct CCS conversion until they implement
/// <see cref="IIonMobilityCcsConversion"/>.</para>
/// </remarks>
public sealed class SpectrumList_IonMobility : SpectrumListWrapper
{
    private readonly IIonMobilityCcsConversion? _ccs;
    private readonly bool _hasMzMlCombinedIonMobility;

    /// <summary>Creates the wrapper around <paramref name="inner"/>.</summary>
    public SpectrumList_IonMobility(ISpectrumList inner) : base(inner)
    {
        // Walk through wrapping layers to find the underlying vendor list.
        var leaf = Innermost;

        if (leaf is IIonMobilitySpectrumList im)
        {
            IonMobilityUnits = im.IonMobilityUnits;
            HasCombinedIonMobility = im.HasCombinedIonMobility;
            IsWatersSonarData = im.IsWatersSonar;
        }
        else
        {
            // mzML re-read path: sniff the first few scans for IM CV params + 3-array
            // IM binary data arrays. Mirrors cpp's "reading an mzML conversion?" branch.
            (IonMobilityUnits, _hasMzMlCombinedIonMobility) = ProbeMzMl(inner);
            HasCombinedIonMobility = _hasMzMlCombinedIonMobility;
            IsWatersSonarData = false;
        }

        _ccs = leaf as IIonMobilityCcsConversion;
    }

    /// <summary>The units IM is reported in across this list — see
    /// <see cref="Pwiz.Data.MsData.Spectra.IonMobilityUnits"/>.</summary>
    public IonMobilityUnits IonMobilityUnits { get; }

    /// <summary>True when the file's IM is reported per-peak (3-array mzML representation
    /// or vendor combine-mode), not per-spectrum.</summary>
    public bool HasCombinedIonMobility { get; }

    /// <summary>True iff the file is Waters SONAR — the IM hardware is used for m/z
    /// range filtering rather than for actual IM.</summary>
    public bool IsWatersSonarData { get; }

    /// <summary>True if the wrapped list can convert between IM and CCS in the supplied
    /// units. False for files without a CCS calibration, for SONAR data, or for vendors
    /// that don't implement <see cref="IIonMobilityCcsConversion"/> yet.</summary>
    public bool CanConvertIonMobilityAndCcs(IonMobilityUnits units)
    {
        if (_ccs is null || units == IonMobilityUnits.None || units != IonMobilityUnits)
            return false;
        return _ccs.CanConvertIonMobilityAndCcs;
    }

    /// <summary>Converts an ion-mobility value to a collisional cross section.</summary>
    /// <exception cref="InvalidOperationException">The inner list doesn't support CCS
    /// conversion (no <see cref="IIonMobilityCcsConversion"/> implementation, or SONAR
    /// data, or non-IM Thermo FAIMS data).</exception>
    public double IonMobilityToCcs(double ionMobility, double mz, int charge)
    {
        if (_ccs is null)
            throw new InvalidOperationException(
                "[SpectrumList_IonMobility.IonMobilityToCcs] only supported when reading native vendor files with a CCS calibration.");
        return _ccs.IonMobilityToCcs(ionMobility, mz, charge);
    }

    /// <summary>Inverse of <see cref="IonMobilityToCcs"/>.</summary>
    /// <exception cref="InvalidOperationException">See <see cref="IonMobilityToCcs"/>.</exception>
    public double CcsToIonMobility(double ccs, double mz, int charge)
    {
        if (_ccs is null)
            throw new InvalidOperationException(
                "[SpectrumList_IonMobility.CcsToIonMobility] only supported when reading native vendor files with a CCS calibration.");
        return _ccs.CcsToIonMobility(ccs, mz, charge);
    }

    /// <summary>Waters SONAR helper: returns the (start, end) "drift" bins covering
    /// <paramref name="precursorMz"/> ± <paramref name="tolerance"/>.</summary>
    /// <exception cref="InvalidOperationException">The wrapped list is not Waters SONAR.</exception>
    public (int Start, int End) SonarMzToBinRange(double precursorMz, double tolerance)
    {
        if (!IsWatersSonarData || Innermost is not IWatersSonarSpectrumList sonar)
            throw new InvalidOperationException(
                "[SpectrumList_IonMobility.SonarMzToBinRange] only works on Waters SONAR data.");
        return sonar.SonarMzToBinRange(precursorMz, tolerance);
    }

    /// <summary>Legacy pwiz.CLI 4-arg overload - writes start/end bins to out params.</summary>
    public void SonarMzToBinRange(double precursorMz, double tolerance, out int low, out int high)
    {
        var (s, e) = SonarMzToBinRange(precursorMz, tolerance);
        low = s;
        high = e;
    }

    /// <summary>Waters SONAR helper: returns the nominal m/z filter value of
    /// <paramref name="bin"/>.</summary>
    /// <exception cref="InvalidOperationException">The wrapped list is not Waters SONAR.</exception>
    public double SonarBinToPrecursorMz(int bin)
    {
        if (!IsWatersSonarData || Innermost is not IWatersSonarSpectrumList sonar)
            throw new InvalidOperationException(
                "[SpectrumList_IonMobility.SonarBinToPrecursorMz] only works on Waters SONAR data.");
        return sonar.SonarBinToPrecursorMz(bin);
    }

    /// <summary>Legacy pwiz.CLI 2-arg overload - writes mz to out param.</summary>
    public void SonarBinToPrecursorMz(int bin, out double result)
    {
        result = SonarBinToPrecursorMz(bin);
    }

    // mzML re-read fallback: scan the first ~5 spectra for IM CV params and binary-
    // array IM children. Returns (units, hasCombined).
    private static (IonMobilityUnits Units, bool HasCombined) ProbeMzMl(ISpectrumList inner)
    {
        IonMobilityUnits units = IonMobilityUnits.None;
        bool hasCombined = false;

        int n = System.Math.Min(5, inner.Count);
        for (int i = 0; i < n; i++)
        {
            // Probe only reads cvParams (IM units) - binary data isn't needed. Passing
            // getBinaryData=true forces a wrapping PeakPicker to run its detector, which
            // fails on VendorOnlyPeakDetector-wrapped lists.
            var spectrum = inner.GetSpectrum(i, getBinaryData: false);
            if (spectrum is null) continue;

            // Per-scan IM CV params. Spectrum exposes ParamContainer helpers directly via
            // pass-through methods; Scan extends ParamContainer so the list is on it.
            var scan = spectrum.ScanList.Scans.Count > 0 ? spectrum.ScanList.Scans[0] : null;
            if (scan is not null)
            {
                if (scan.HasCVParam(CVID.MS_ion_mobility_drift_time))
                    units = IonMobilityUnits.DriftTimeMsec;
                else if (scan.HasCVParam(CVID.MS_inverse_reduced_ion_mobility))
                    units = IonMobilityUnits.InverseReducedIonMobilityVsecPerCm2;
                else if (spectrum.HasCVParam(CVID.MS_FAIMS_compensation_voltage))
                    units = IonMobilityUnits.CompensationV;
                else if (scan.UserParams.Any(p => p.Name == "drift time"))
                    units = IonMobilityUnits.DriftTimeMsec;
            }

            // 3-array IM (combined) representation: any binary array whose CV child is
            // one of the IM-array kinds.
            foreach (var arr in spectrum.BinaryDataArrays)
            {
                var arrayKindCvid = arr.CvParamChild(CVID.MS_ion_mobility_array).Cvid;
                if (arrayKindCvid == CVID.CVID_Unknown) continue;

                hasCombined = true;
                units = arrayKindCvid is CVID.MS_raw_ion_mobility_array or CVID.MS_mean_ion_mobility_array
                    ? IonMobilityUnits.DriftTimeMsec
                    : IonMobilityUnits.InverseReducedIonMobilityVsecPerCm2;
                return (units, hasCombined); // earliest-wins, mirroring cpp
            }
        }

        return (units, hasCombined);
    }
}
