namespace Pwiz.Data.MsData.Spectra;

/// <summary>
/// Optional capability for spectrum lists that can convert between ion mobility (drift time
/// or 1/K0) and collisional cross section. Mirrors pwiz C++'s
/// <c>SpectrumList_Waters::canConvertIonMobilityAndCCS</c> /
/// <c>ionMobilityToCCS</c> / <c>ccsToIonMobility</c> — used by Skyline and other downstream
/// tools that label IMS peaks with CCS values.
/// </summary>
public interface IIonMobilityCcsConversion
{
    /// <summary>
    /// True if a CCS calibration is available for this file. False for files that have ion
    /// mobility data but no CCS calibration (e.g. Waters SONAR or IMS without
    /// <c>mob_cal.csv</c>).
    /// </summary>
    bool CanConvertIonMobilityAndCcs { get; }

    /// <summary>
    /// Converts an ion-mobility value (drift time in ms for Waters; 1/K0 in Vs/cm² for
    /// Bruker) plus a precursor m/z and charge to a collisional cross section in Å².
    /// </summary>
    double IonMobilityToCcs(double ionMobility, double mz, int charge);

    /// <summary>
    /// Inverse of <see cref="IonMobilityToCcs"/> — predicts the ion mobility from a CCS,
    /// precursor m/z, and charge.
    /// </summary>
    double CcsToIonMobility(double ccs, double mz, int charge);
}
