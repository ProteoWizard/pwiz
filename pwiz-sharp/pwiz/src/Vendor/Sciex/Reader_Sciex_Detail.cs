#if !NO_VENDOR_SUPPORT
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;

namespace Pwiz.Vendor.Sciex;

/// <summary>
/// Translates Sciex SDK metadata (instrument name string, ion source, experiment type,
/// polarity) to pwiz CV terms. Port of pwiz cpp <c>Reader_ABI_Detail.cpp</c>.
/// </summary>
internal static class Reader_Sciex_Detail
{
    /// <summary>
    /// Sciex instrument family. Drives the per-model component-list shape produced by
    /// <see cref="TranslateAsInstrumentConfiguration"/>.
    /// </summary>
    internal enum SciexInstrumentModel
    {
        Unknown,

        // QqQ (triple-quad)
        API100, API100LC, API150MCA, API150EX, API165, API300, API350, API365,
        API2000, API3000, API3200, API4000, API4500, API5000, API5500, API6500, TripleQuad7500,

        // QqLIT (QTRAP — quadrupole + linear ion trap)
        API2000QTrap, API2500QTrap, API3200QTrap, API3500QTrap,
        API4000QTrap, API4500QTrap, API5500QTrap, API6500QTrap, GenericQTrap,

        // QqTOF
        QStar, QStarPulsarI, QStarXL, QStarElite,
        API4600TripleTOF, API5600TripleTOF, API6600TripleTOF,
        X500QTOF, NlxTof, ZenoTOF7600,
    }

    /// <summary>
    /// Maps the Sciex SDK's <c>InstrumentName</c> string (e.g. <c>"QTRAP 6500 High Mass"</c>) to
    /// our <see cref="SciexInstrumentModel"/> enum. Mirrors cpp <c>WiffFileImpl::getInstrumentModel</c>:
    /// uppercase, strip spaces, strip the legacy "API" prefix, then substring-match.
    /// </summary>
    internal static SciexInstrumentModel ParseInstrumentName(string? rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return SciexInstrumentModel.Unknown;
        string n = rawName.ToUpperInvariant().Replace(" ", string.Empty).Replace("API", string.Empty);
        if (n == "UNKNOWN") return SciexInstrumentModel.Unknown;
        if (n.Contains("2000QTRAP", System.StringComparison.Ordinal)) return SciexInstrumentModel.API2000QTrap;
        if (n.Contains("2000", System.StringComparison.Ordinal)) return SciexInstrumentModel.API2000;
        if (n.Contains("2500QTRAP", System.StringComparison.Ordinal)) return SciexInstrumentModel.API2000QTrap;
        if (n.Contains("3000", System.StringComparison.Ordinal)) return SciexInstrumentModel.API3000;
        if (n.Contains("3200QTRAP", System.StringComparison.Ordinal)) return SciexInstrumentModel.API3200QTrap;
        if (n.Contains("3200", System.StringComparison.Ordinal)) return SciexInstrumentModel.API3200;
        if (n.Contains("3500QTRAP", System.StringComparison.Ordinal)) return SciexInstrumentModel.API3500QTrap;
        if (n.Contains("4000QTRAP", System.StringComparison.Ordinal)) return SciexInstrumentModel.API4000QTrap;
        if (n.Contains("4000", System.StringComparison.Ordinal)) return SciexInstrumentModel.API4000;
        if (n.Contains("QTRAP4500", System.StringComparison.Ordinal)) return SciexInstrumentModel.API4500QTrap;
        if (n.Contains("4500", System.StringComparison.Ordinal)) return SciexInstrumentModel.API4500;
        if (n.Contains("5000", System.StringComparison.Ordinal)) return SciexInstrumentModel.API5000;
        if (n.Contains("QTRAP5500", System.StringComparison.Ordinal)) return SciexInstrumentModel.API5500QTrap;
        if (n.Contains("5500", System.StringComparison.Ordinal)) return SciexInstrumentModel.API5500;
        if (n.Contains("QTRAP6500", System.StringComparison.Ordinal)) return SciexInstrumentModel.API6500QTrap;
        if (n.Contains("6500", System.StringComparison.Ordinal)) return SciexInstrumentModel.API6500;
        if (n.Contains("QUAD7500", System.StringComparison.Ordinal)) return SciexInstrumentModel.TripleQuad7500;
        if (n.Contains("QTRAP", System.StringComparison.Ordinal)) return SciexInstrumentModel.GenericQTrap;
        if (n.Contains("QSTARPULSAR", System.StringComparison.Ordinal)) return SciexInstrumentModel.QStarPulsarI;
        if (n.Contains("QSTARXL", System.StringComparison.Ordinal)) return SciexInstrumentModel.QStarXL;
        if (n.Contains("QSTARELITE", System.StringComparison.Ordinal)) return SciexInstrumentModel.QStarElite;
        if (n.Contains("QSTAR", System.StringComparison.Ordinal)) return SciexInstrumentModel.QStar;
        if (n.Contains("TRIPLETOF4600", System.StringComparison.Ordinal)) return SciexInstrumentModel.API4600TripleTOF;
        if (n.Contains("TRIPLETOF5600", System.StringComparison.Ordinal)) return SciexInstrumentModel.API5600TripleTOF;
        if (n.Contains("TRIPLETOF6600", System.StringComparison.Ordinal)) return SciexInstrumentModel.API6600TripleTOF;
        if (n.Contains("NLXTOF", System.StringComparison.Ordinal)) return SciexInstrumentModel.NlxTof;
        if (n.Contains("100LC", System.StringComparison.Ordinal)) return SciexInstrumentModel.API100LC;
        if (n.Contains("100", System.StringComparison.Ordinal)) return SciexInstrumentModel.API100;
        if (n.Contains("150MCA", System.StringComparison.Ordinal)) return SciexInstrumentModel.API150MCA;
        if (n.Contains("150EX", System.StringComparison.Ordinal)) return SciexInstrumentModel.API150EX;
        if (n.Contains("165", System.StringComparison.Ordinal)) return SciexInstrumentModel.API165;
        if (n.Contains("300", System.StringComparison.Ordinal)) return SciexInstrumentModel.API300;
        if (n.Contains("350", System.StringComparison.Ordinal)) return SciexInstrumentModel.API350;
        if (n.Contains("365", System.StringComparison.Ordinal)) return SciexInstrumentModel.API365;
        if (n.Contains("X500QTOF", System.StringComparison.Ordinal)) return SciexInstrumentModel.X500QTOF;
        if (n.Contains("ZENOTOF7600", System.StringComparison.Ordinal)) return SciexInstrumentModel.ZenoTOF7600;
        return SciexInstrumentModel.Unknown;
    }

    /// <summary>Maps a Sciex instrument model to its CV term. Port of cpp <c>translateAsInstrumentModel</c>.</summary>
    internal static CVID TranslateAsInstrumentModel(SciexInstrumentModel m) => m switch
    {
        SciexInstrumentModel.API100 => CVID.MS_API_100,
        SciexInstrumentModel.API100LC => CVID.MS_API_100LC,
        SciexInstrumentModel.API150MCA => CVID.MS_API_150EX,
        SciexInstrumentModel.API150EX => CVID.MS_API_150EX,
        SciexInstrumentModel.API165 => CVID.MS_API_165,
        SciexInstrumentModel.API300 => CVID.MS_API_300,
        SciexInstrumentModel.API350 => CVID.MS_API_350,
        SciexInstrumentModel.API365 => CVID.MS_API_365,
        SciexInstrumentModel.API2000 => CVID.MS_API_2000,
        SciexInstrumentModel.API3000 => CVID.MS_API_3000,
        SciexInstrumentModel.API3200 => CVID.MS_API_3200,
        SciexInstrumentModel.API4000 => CVID.MS_API_4000,
        SciexInstrumentModel.API4500 => CVID.MS_Triple_Quad_4500,
        SciexInstrumentModel.API5000 => CVID.MS_API_5000,
        SciexInstrumentModel.API5500 => CVID.MS_Triple_Quad_5500,
        SciexInstrumentModel.API6500 => CVID.MS_Triple_Quad_6500,
        SciexInstrumentModel.TripleQuad7500 => CVID.MS_Triple_Quad_7500,
        SciexInstrumentModel.API2000QTrap => CVID.MS_2000_QTRAP,
        SciexInstrumentModel.API2500QTrap => CVID.MS_2500_QTRAP,
        SciexInstrumentModel.API3200QTrap => CVID.MS_3200_QTRAP,
        SciexInstrumentModel.API3500QTrap => CVID.MS_3500_QTRAP,
        SciexInstrumentModel.API4000QTrap => CVID.MS_4000_QTRAP,
        SciexInstrumentModel.API4500QTrap => CVID.MS_QTRAP_4500,
        SciexInstrumentModel.API5500QTrap => CVID.MS_QTRAP_5500,
        SciexInstrumentModel.API6500QTrap => CVID.MS_QTRAP_6500,
        SciexInstrumentModel.GenericQTrap => CVID.MS_Q_TRAP,
        SciexInstrumentModel.API4600TripleTOF => CVID.MS_TripleTOF_4600,
        SciexInstrumentModel.API5600TripleTOF => CVID.MS_TripleTOF_5600,
        SciexInstrumentModel.API6600TripleTOF => CVID.MS_TripleTOF_6600,
        SciexInstrumentModel.ZenoTOF7600 => CVID.MS_ZenoTOF_7600,
        SciexInstrumentModel.QStar => CVID.MS_QSTAR,
        SciexInstrumentModel.QStarPulsarI => CVID.MS_QSTAR_Pulsar,
        SciexInstrumentModel.QStarXL => CVID.MS_QSTAR_XL,
        SciexInstrumentModel.QStarElite => CVID.MS_QSTAR_Elite,
        SciexInstrumentModel.NlxTof => CVID.MS_TripleTOF_5600,
        SciexInstrumentModel.X500QTOF => CVID.MS_X500R_QTOF,
        SciexInstrumentModel.Unknown => CVID.MS_Applied_Biosystems_instrument_model,
        _ => CVID.MS_Applied_Biosystems_instrument_model,
    };

    /// <summary>
    /// Builds an <see cref="InstrumentConfiguration"/> for the given model + ion source.
    /// Always emits a 5-component pipeline (source / Q1 / Q2 / Q3-or-LIT-or-TOF / detector);
    /// the last analyzer + detector vary by family. Port of cpp <c>translateAsInstrumentConfiguration</c>.
    /// </summary>
    internal static InstrumentConfiguration TranslateAsInstrumentConfiguration(
        SciexInstrumentModel model, CVID ionSourceCvid)
    {
        var ic = new InstrumentConfiguration("IC1");
        ic.Set(TranslateAsInstrumentModel(model));

        var source = new Component(ionSourceCvid, 1);

        switch (model)
        {
            // QqQ — triple-quadrupole family.
            case SciexInstrumentModel.API150MCA:
            case SciexInstrumentModel.API150EX:
            case SciexInstrumentModel.API2000:
            case SciexInstrumentModel.API3000:
            case SciexInstrumentModel.API3200:
            case SciexInstrumentModel.API4000:
            case SciexInstrumentModel.API4500:
            case SciexInstrumentModel.API5000:
            case SciexInstrumentModel.API5500:
            case SciexInstrumentModel.API6500:
            case SciexInstrumentModel.API100:
            case SciexInstrumentModel.API100LC:
            case SciexInstrumentModel.API165:
            case SciexInstrumentModel.API300:
            case SciexInstrumentModel.API350:
            case SciexInstrumentModel.API365:
            case SciexInstrumentModel.TripleQuad7500:
                ic.ComponentList.Add(source);
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 2));
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 3));
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 4));
                ic.ComponentList.Add(new Component(CVID.MS_electron_multiplier, 5));
                break;

            // QqLIT — QTRAP family (quadrupole + linear ion trap).
            case SciexInstrumentModel.API2000QTrap:
            case SciexInstrumentModel.API2500QTrap:
            case SciexInstrumentModel.API3200QTrap:
            case SciexInstrumentModel.API3500QTrap:
            case SciexInstrumentModel.API4000QTrap:
            case SciexInstrumentModel.API4500QTrap:
            case SciexInstrumentModel.API5500QTrap:
            case SciexInstrumentModel.API6500QTrap:
            case SciexInstrumentModel.GenericQTrap:
                ic.ComponentList.Add(source);
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 2));
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 3));
                ic.ComponentList.Add(new Component(CVID.MS_axial_ejection_linear_ion_trap, 4));
                ic.ComponentList.Add(new Component(CVID.MS_electron_multiplier, 5));
                break;

            // QqTOF — quadrupole + time-of-flight.
            case SciexInstrumentModel.QStar:
            case SciexInstrumentModel.QStarPulsarI:
            case SciexInstrumentModel.QStarXL:
            case SciexInstrumentModel.QStarElite:
            case SciexInstrumentModel.API4600TripleTOF:
            case SciexInstrumentModel.API5600TripleTOF:
            case SciexInstrumentModel.API6600TripleTOF:
            case SciexInstrumentModel.X500QTOF:
            case SciexInstrumentModel.NlxTof:
            case SciexInstrumentModel.ZenoTOF7600:
                ic.ComponentList.Add(source);
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 2));
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 3));
                ic.ComponentList.Add(new Component(CVID.MS_time_of_flight, 4));
                ic.ComponentList.Add(new Component(CVID.MS_electron_multiplier, 5));
                break;

            case SciexInstrumentModel.Unknown:
                // Empty component list — model term alone, like cpp.
                break;
        }

        return ic;
    }

    /// <summary>Maps a <see cref="WiffExperimentType"/> to the spectrum-type CV term.
    /// Port of cpp <c>translateAsSpectrumType</c>. MRM is excluded; callers emit
    /// <see cref="CVID.MS_SRM_chromatogram"/> instead.</summary>
    internal static CVID TranslateAsSpectrumType(WiffExperimentType t) => t switch
    {
        WiffExperimentType.MS => CVID.MS_MS1_spectrum,
        WiffExperimentType.Product => CVID.MS_MSn_spectrum,
        WiffExperimentType.Precursor => CVID.MS_precursor_ion_spectrum,
        WiffExperimentType.NeutralGainOrLoss => CVID.MS_constant_neutral_loss_spectrum,
        WiffExperimentType.SIM => CVID.MS_SIM_spectrum,
        WiffExperimentType.MRM => CVID.MS_SRM_spectrum,
        _ => CVID.CVID_Unknown,
    };
}
#endif
