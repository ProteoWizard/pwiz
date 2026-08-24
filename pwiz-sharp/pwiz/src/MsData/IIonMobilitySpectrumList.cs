namespace Pwiz.Data.MsData.Spectra;

/// <summary>
/// Units used to report ion mobility values on a spectrum or in a binary data array.
/// Mirrors cpp <c>SpectrumList_IonMobility::IonMobilityUnits</c>. Ordering preserved
/// so that <see cref="None"/> = 0 (alignment with BiblioSpec's IONMOBILITY_TYPE enum,
/// per cpp's comment on its enum declaration).
/// </summary>
public enum IonMobilityUnits
{
    /// <summary>Waters SONAR — m/z range filtered by the ion-mobility hardware, reported
    /// in the same place IM would appear. Not strictly an IM unit; see
    /// <see cref="IIonMobilitySpectrumList.IsWatersSonar"/>.</summary>
    WatersSonar = -1,
    /// <summary>The spectrum list has no IM data.</summary>
    None = 0,
    /// <summary>Drift time in milliseconds (Waters, Agilent, UIMF, Mobilion).</summary>
    DriftTimeMsec,
    /// <summary>Inverse reduced ion mobility, 1/K0, in V·s/cm² (Bruker TIMS).</summary>
    InverseReducedIonMobilityVsecPerCm2,
    /// <summary>Compensation voltage in V (Thermo FAIMS).</summary>
    CompensationV,
}

/// <summary>
/// Optional capability interface for spectrum lists that carry ion-mobility data.
/// Implemented by vendor <c>SpectrumList_*</c> classes (Waters does today;
/// Agilent / Bruker / UIMF / Mobilion / Thermo plumb in as their workflows land).
/// Consumed by <c>Pwiz.Analysis.SpectrumList_IonMobility</c> to expose a unified
/// IM API across vendors.
/// </summary>
/// <remarks>
/// Lists that don't implement this interface are treated as "no IM data" by the
/// wrapper, which then falls back to an mzML CV-parameter sniff to recover IM-from-
/// mzML round-trips.
/// </remarks>
public interface IIonMobilitySpectrumList
{
    /// <summary>What unit, if any, this list reports IM in.</summary>
    IonMobilityUnits IonMobilityUnits { get; }

    /// <summary>True iff IM is reported as a per-peak array on each spectrum (the
    /// "3-array" mzML representation: m/z + intensity + IM), as opposed to one IM value
    /// per spectrum. Bruker TIMS and Agilent IMS combine-mode emit this format.</summary>
    bool HasCombinedIonMobility { get; }

    /// <summary>True iff this list represents Waters SONAR data — m/z filtering done
    /// by the ion-mobility hardware, reported in the IM slot. False on all non-Waters
    /// lists.</summary>
    bool IsWatersSonar { get; }
}

/// <summary>
/// Capability interface for Waters SONAR data (m/z range filtered by the IM hardware,
/// reported as if it were IM). Implemented by <c>SpectrumList_Waters</c> when sonar
/// functions are detected; consumed by <c>SpectrumList_IonMobility</c>'s sonar helpers.
/// </summary>
public interface IWatersSonarSpectrumList
{
    /// <summary>Given a precursor m/z, returns the (start, end) "drift" bins covering
    /// it within the supplied <paramref name="tolerance"/>. Returns <c>(-1, -1)</c> when
    /// <paramref name="precursorMz"/> falls outside the SONAR range.</summary>
    (int Start, int End) SonarMzToBinRange(double precursorMz, double tolerance);

    /// <summary>Given a "drift" bin, returns the nominal m/z filter value of that bin.
    /// Returns <c>0</c> when <paramref name="bin"/> is outside the SONAR range.</summary>
    double SonarBinToPrecursorMz(int bin);
}
