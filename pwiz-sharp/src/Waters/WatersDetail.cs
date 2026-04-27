using Pwiz.Data.Common.Cv;

namespace Pwiz.Vendor.Waters;

/// <summary>
/// MassLynx <c>FunctionType</c> values, normalized to a 0-based enum that mirrors pwiz C++
/// <c>PwizFunctionType</c>. The native side keys these off <c>FUNCTION_TYPE_BASE = 200</c>; we
/// strip the base when reading via <see cref="WatersDetail.FromMassLynxFunctionType"/>.
/// </summary>
internal enum WatersFunctionType
{
    Scan = 0,
    SIR = 1,
    Delay = 2,
    Concatenated = 3,
    Off = 4,
    Parents = 5,
    Daughters = 6,
    NeutralLoss = 7,
    NeutralGain = 8,
    MRM = 9,
    Q1F = 10,
    MS2 = 11,
    DiodeArray = 12,
    TOF = 13,
    TOF_PSD = 14,
    TOF_Survey = 15,
    TOF_Daughter = 16,
    MALDI_TOF = 17,
    TOF_MS = 18,
    TOF_Parent = 19,
    VoltageScan = 20,
    MagneticScan = 21,
    VoltageSIR = 22,
    MagneticSIR = 23,
    AutoDaughters = 24,
    AutoSpec_BE_Scan = 25,
    AutoSpec_B2E_Scan = 26,
    AutoSpec_CNL_Scan = 27,
    AutoSpec_MIKES_Scan = 28,
    AutoSpec_MRM = 29,
    AutoSpec_NRMS_Scan = 30,
    AutoSpec_Q_MRM_Quad = 31,
}

/// <summary>
/// MassLynx <c>IonMode</c> values, normalized to a 0-based enum.
/// (Source/polarity bit-pair: even=Positive, odd=Negative.)
/// </summary>
internal enum WatersIonMode
{
    EI_POS = 0,
    EI_NEG = 1,
    CI_POS = 2,
    CI_NEG = 3,
    FB_POS = 4,
    FB_NEG = 5,
    TS_POS = 6,
    TS_NEG = 7,
    ES_POS = 8,
    ES_NEG = 9,
    AI_POS = 10,
    AI_NEG = 11,
    LD_POS = 12,
    LD_NEG = 13,
    Generic = 99,
    Uninitialised = -1,
}

/// <summary>
/// MassLynx <c>MassLynxScanItem</c> values we read for spectrum metadata. Native enum lives
/// at SCAN_ITEM_BASE+1 and onward (see MassLynxRawDefs.h); the integer keys flow through to
/// <c>getScanItemValue</c> as-is.
/// </summary>
internal static class WatersScanItem
{
    private const int Base = 401; // SCAN_ITEM_BASE(400) + 1

    public const int CollisionEnergy = Base + 61;
    public const int SetMass = Base + 76;
    public const int TotalIonCurrent = Base + 251;
    public const int BasePeakMass = Base + 252;
    public const int BasePeakIntensity = Base + 253;
    public const int PeaksInScan = Base + 254;
}

/// <summary>
/// MassLynx <c>MassLynxDDAIndexDetail</c> keys (DDA_TYPE_BASE = 1800). Returned from the
/// DDA processor's GetScanInfo as parameter values.
/// </summary>
internal static class WatersDdaIndex
{
    public const int RT = 1800;
    public const int Function = 1801;
    public const int StartScan = 1802;
    public const int EndScan = 1803;
    public const int ScanType = 1804;
    public const int SetMass = 1805;
    public const int PrecursorMass = 1806;
}

/// <summary>DDAIsolationWindowParameter keys (DDA_ISOLATION_WINDOW_PARAMETER_BASE = 1900).</summary>
internal static class WatersDdaIsolation
{
    public const int LowerOffset = 1900;
    public const int UpperOffset = 1901;
}

/// <summary>DDAParameter keys (DDA_PARAMETER_BASE = 1950).</summary>
internal static class WatersDdaParameter
{
    public const int Centroid = 1950;
}

/// <summary>MassLynxScanType (SCAN_TYPE_BASE = 1850). MS1 = 1850, MS2 = 1851.</summary>
internal static class WatersScanType
{
    public const int Ms1 = 1850;
}

/// <summary>
/// Helpers translating MassLynx-native enums to mzML CV terms. Mirrors
/// <c>Reader_Waters_Detail.cpp</c> + the inline helpers in <c>WatersRawFile.hpp</c>.
/// </summary>
internal static class WatersDetail
{
    /// <summary>Strip the FUNCTION_TYPE_BASE (200) offset to get a 0-based <see cref="WatersFunctionType"/>.</summary>
    public static WatersFunctionType FromMassLynxFunctionType(int rawType) =>
        (WatersFunctionType)(rawType - 200);

    /// <summary>Strip the ION_MODE_BASE (100) offset to get a 0-based <see cref="WatersIonMode"/>.</summary>
    public static WatersIonMode FromMassLynxIonMode(int rawMode) =>
        rawMode == -1 ? WatersIonMode.Uninitialised : (WatersIonMode)(rawMode - 100);

    /// <summary>
    /// Maps a function type to (msLevel, spectrumType CV). Mirrors <c>translateFunctionType</c>
    /// in pwiz C++ Reader_Waters_Detail.cpp. Returns false for "skip this function" types
    /// (Off / VoltageScan / MagneticScan / VoltageSIR / MagneticSIR).
    /// </summary>
    public static bool TranslateFunctionType(WatersFunctionType functionType, out int msLevel, out CVID spectrumType)
    {
        switch (functionType)
        {
            case WatersFunctionType.Daughters:
            case WatersFunctionType.MS2:
            case WatersFunctionType.TOF_Daughter:
            case WatersFunctionType.AutoDaughters:
                msLevel = 2;
                spectrumType = CVID.MS_MSn_spectrum;
                return true;

            case WatersFunctionType.SIR:
                msLevel = 1;
                spectrumType = CVID.MS_SIM_spectrum;
                return true;

            case WatersFunctionType.MRM:
            case WatersFunctionType.AutoSpec_MRM:
            case WatersFunctionType.AutoSpec_Q_MRM_Quad:
            case WatersFunctionType.AutoSpec_MIKES_Scan:
                msLevel = 2;
                spectrumType = CVID.MS_SRM_spectrum;
                return true;

            case WatersFunctionType.NeutralLoss:
                msLevel = 2;
                spectrumType = CVID.MS_constant_neutral_loss_spectrum;
                return true;

            case WatersFunctionType.NeutralGain:
                msLevel = 2;
                spectrumType = CVID.MS_constant_neutral_gain_spectrum;
                return true;

            case WatersFunctionType.Parents:
            case WatersFunctionType.Scan:
            case WatersFunctionType.Q1F:
            case WatersFunctionType.TOF:
            case WatersFunctionType.TOF_MS:
            case WatersFunctionType.TOF_Survey:
            case WatersFunctionType.TOF_Parent:
            case WatersFunctionType.MALDI_TOF:
                msLevel = 1;
                spectrumType = CVID.MS_MS1_spectrum;
                return true;

            case WatersFunctionType.DiodeArray:
                msLevel = 0;
                spectrumType = CVID.MS_EMR_spectrum;
                return true;

            case WatersFunctionType.Off:
            case WatersFunctionType.VoltageScan:
            case WatersFunctionType.MagneticScan:
            case WatersFunctionType.VoltageSIR:
            case WatersFunctionType.MagneticSIR:
                msLevel = 0;
                spectrumType = CVID.CVID_Unknown;
                return false;

            default:
                throw new InvalidOperationException("Unable to translate Waters function type: " + functionType);
        }
    }

    /// <summary>Returns the polarity CV for a Waters ion mode, or <c>CVID_Unknown</c> when neither.</summary>
    public static CVID Polarity(WatersIonMode ionMode)
    {
        switch (ionMode)
        {
            case WatersIonMode.EI_POS:
            case WatersIonMode.CI_POS:
            case WatersIonMode.FB_POS:
            case WatersIonMode.TS_POS:
            case WatersIonMode.ES_POS:
            case WatersIonMode.AI_POS:
            case WatersIonMode.LD_POS:
                return CVID.MS_positive_scan;

            case WatersIonMode.EI_NEG:
            case WatersIonMode.CI_NEG:
            case WatersIonMode.FB_NEG:
            case WatersIonMode.TS_NEG:
            case WatersIonMode.ES_NEG:
            case WatersIonMode.AI_NEG:
            case WatersIonMode.LD_NEG:
                return CVID.MS_negative_scan;

            default:
                return CVID.CVID_Unknown;
        }
    }
}
