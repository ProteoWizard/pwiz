using Pwiz.Data.Common.Cv;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Thermo;

/// <summary>
/// Hand-port of pwiz cpp <c>parseInstrumentModelType</c> + <c>translateAsInstrumentModel</c>
/// (see <c>pwiz_aux/msrc/utility/vendor_api/thermo/RawFileTypes.h</c> and
/// <c>pwiz/data/vendor_readers/Thermo/Reader_Thermo_Detail.cpp</c>).
/// </summary>
/// <remarks>
/// The cpp version goes through an intermediate <c>InstrumentModelType</c> enum because it
/// also drives the analyzer / detector / ion-source lookups. Our C# Reader_Thermo doesn't
/// (yet) need those — it derives the analyzer chain from <c>MassAnalyzerType</c> on each scan
/// instead. So the port collapses both cpp steps into a single (string -> CVID) table.
///
/// Match modes mirror cpp: Exact / ExactNoSpaces / Contains / ContainsNoSpaces. StartsWith /
/// EndsWith aren't used by the current cpp table (every entry uses one of the other four), so
/// they're omitted here; add if cpp grows them.
/// </remarks>
public static class ThermoInstrumentModel
{
    /// <summary>How a name table entry compares against the (already upper-cased) input.</summary>
    private enum MatchType
    {
        /// <summary>Whole upper-cased input equals the entry name.</summary>
        Exact,
        /// <summary>Whole upper-cased input with spaces removed equals the entry name.</summary>
        ExactNoSpaces,
        /// <summary>Upper-cased input contains the entry name as a substring.</summary>
        Contains,
        /// <summary>Upper-cased input with spaces removed contains the entry name as a substring.</summary>
        ContainsNoSpaces,
    }

    private readonly record struct Mapping(string Name, CVID Cvid, MatchType Match);

    /// <summary>
    /// Order matters: entries are checked top-to-bottom, first match wins. More specific
    /// names ("Q EXACTIVE PLUS") must precede their prefixes ("Q EXACTIVE", "EXACTIVE").
    /// </summary>
    private static readonly Mapping[] s_table = new Mapping[]
    {
        // Finnigan MAT
        new("MAT253",                          CVID.MS_MAT253,                          MatchType.ExactNoSpaces),
        new("MAT900XP",                        CVID.MS_MAT900XP,                        MatchType.ExactNoSpaces),
        new("MAT900XPTRAP",                    CVID.MS_MAT900XP_Trap,                   MatchType.ExactNoSpaces),
        new("MAT95XP",                         CVID.MS_MAT95XP,                         MatchType.ExactNoSpaces),
        new("MAT95XPTRAP",                     CVID.MS_MAT95XP_Trap,                    MatchType.ExactNoSpaces),
        new("SSQ7000",                         CVID.MS_SSQ_7000,                        MatchType.ExactNoSpaces),
        new("TSQ7000",                         CVID.MS_TSQ_7000,                        MatchType.ExactNoSpaces),
        new("TSQ8000EVO",                      CVID.MS_TSQ_8000_Evo,                    MatchType.ExactNoSpaces),
        new("TSQ9000",                         CVID.MS_TSQ_9000,                        MatchType.ExactNoSpaces),
        new("TSQ",                             CVID.MS_TSQ,                             MatchType.Exact),

        // Thermo Electron
        new("ELEMENT2",                        CVID.MS_Element_2,                       MatchType.ExactNoSpaces),

        // Thermo Finnigan
        new("DELTA PLUSADVANTAGE",             CVID.MS_DELTA_plusAdvantage,             MatchType.Exact),
        new("DELTAPLUSXP",                     CVID.MS_DELTAplusXP,                     MatchType.Exact),
        new("DELTA PLUS IRMS",                 CVID.MS_DeltaPlus_IRMS,                  MatchType.Exact),
        new("LCQ ADVANTAGE",                   CVID.MS_LCQ_Advantage,                   MatchType.Exact),
        new("LCQ CLASSIC",                     CVID.MS_LCQ_Classic,                     MatchType.Exact),
        new("LCQ DECA",                        CVID.MS_LCQ_Deca,                        MatchType.Exact),
        new("LCQ DECA XP",                     CVID.MS_LCQ_Deca_XP_Plus,                MatchType.Exact),
        new("LCQ DECA XP PLUS",                CVID.MS_LCQ_Deca_XP_Plus,                MatchType.Exact),
        new("NEPTUNE",                         CVID.MS_neptune,                         MatchType.Exact),
        new("DSQ",                             CVID.MS_DSQ,                             MatchType.Exact),
        new("POLARISQ",                        CVID.MS_PolarisQ,                        MatchType.Exact),
        new("SURVEYOR MSQ",                    CVID.MS_Surveyor_MSQ,                    MatchType.Exact),
        new("MSQ PLUS",                        CVID.MS_Surveyor_MSQ,                    MatchType.Exact),
        new("TEMPUS TOF",                      CVID.MS_TEMPUS_TOF,                      MatchType.Exact),
        new("TRACE DSQ",                       CVID.MS_TRACE_DSQ,                       MatchType.Exact),
        new("TRITON",                          CVID.MS_TRITON,                          MatchType.Exact),

        // Thermo Scientific — LTQ family
        new("LTQ",                             CVID.MS_LTQ,                             MatchType.Exact),
        new("LTQ XL",                          CVID.MS_LTQ_XL,                          MatchType.Exact),
        new("LTQ FT",                          CVID.MS_LTQ_FT,                          MatchType.Exact),
        new("LTQ-FT",                          CVID.MS_LTQ_FT,                          MatchType.Exact),
        new("LTQ FT ULTRA",                    CVID.MS_LTQ_FT_Ultra,                    MatchType.Exact),
        new("LTQ ORBITRAP",                    CVID.MS_LTQ_Orbitrap,                    MatchType.Exact),
        new("LTQ ORBITRAP CLASSIC",            CVID.MS_LTQ_Orbitrap_Classic,            MatchType.Exact),
        new("LTQ ORBITRAP DISCOVERY",          CVID.MS_LTQ_Orbitrap_Discovery,          MatchType.Exact),
        new("LTQ ORBITRAP XL",                 CVID.MS_LTQ_Orbitrap_XL,                 MatchType.Exact),
        new("ORBITRAP VELOS PRO",              CVID.MS_LTQ_Orbitrap_Velos_Pro,          MatchType.Contains),
        new("LTQ ORBITRAP VELOS/ETD",          CVID.MS_LTQ_Orbitrap_Velos_ETD,          MatchType.Exact),
        new("ORBITRAP VELOS",                  CVID.MS_LTQ_Orbitrap_Velos,              MatchType.Contains),
        new("ORBITRAP ELITE",                  CVID.MS_LTQ_Orbitrap_Elite,              MatchType.Contains),
        new("VELOS PLUS",                      CVID.MS_Velos_Plus,                      MatchType.Contains),
        new("VELOS PRO",                       CVID.MS_Velos_Pro,                       MatchType.Contains),
        new("LTQ VELOS",                       CVID.MS_LTQ_Velos,                       MatchType.Exact),
        new("LTQ VELOS ETD",                   CVID.MS_LTQ_Velos_ETD,                   MatchType.Exact),
        new("LXQ",                             CVID.MS_LXQ,                             MatchType.Exact),
        new("LCQ FLEET",                       CVID.MS_LCQ_Fleet,                       MatchType.Exact),
        new("ITQ 700",                         CVID.MS_ITQ_700,                         MatchType.Exact),
        new("ITQ 900",                         CVID.MS_ITQ_900,                         MatchType.Exact),
        new("ITQ 1100",                        CVID.MS_ITQ_1100,                        MatchType.Exact),
        new("ITQ",                             CVID.MS_ITQ,                             MatchType.Contains),
        new("GC QUANTUM",                      CVID.MS_GC_Quantum,                      MatchType.Exact),
        new("LTQ XL ETD",                      CVID.MS_LTQ_XL_ETD,                      MatchType.Exact),
        new("LTQ ORBITRAP XL ETD",             CVID.MS_LTQ_Orbitrap_XL_ETD,             MatchType.Exact),
        new("DFS",                             CVID.MS_DFS,                             MatchType.Exact),
        new("DSQ II",                          CVID.MS_DSQ_II,                          MatchType.Exact),
        new("ISQ SERIES",                      CVID.MS_ISQ,                             MatchType.Exact),
        new("ISQEC",                           CVID.MS_ISQ,                             MatchType.ContainsNoSpaces),
        new("ISQEM",                           CVID.MS_ISQ,                             MatchType.ContainsNoSpaces),
        new("ISQ 7000",                        CVID.MS_ISQ_7000,                        MatchType.Exact),
        new("ISQ LT",                          CVID.MS_ISQ_LT,                          MatchType.Exact),
        new("MALDI LTQ XL",                    CVID.MS_MALDI_LTQ_XL,                    MatchType.Exact),
        new("MALDI LTQ ORBITRAP",              CVID.MS_MALDI_LTQ_Orbitrap,              MatchType.Exact),

        // TSQ family
        new("TSQ QUANTUM",                     CVID.MS_TSQ_Quantum,                     MatchType.Exact),
        new("TSQ QUANTUM ACCESS MAX",          CVID.MS_TSQ_Quantum_Access_MAX,          MatchType.Contains),
        new("TSQ QUANTUM ACCESS",              CVID.MS_TSQ_Quantum_Access,              MatchType.Contains),
        new("TSQ QUANTUM ULTRA",               CVID.MS_TSQ_Quantum_Ultra,               MatchType.Exact),
        new("TSQ QUANTUM ULTRA AM",            CVID.MS_TSQ_Quantum_Ultra_AM,            MatchType.Exact),
        new("TSQ VANTAGE STANDARD",            CVID.MS_TSQ_Vantage,                     MatchType.Exact),
        new("TSQ VANTAGE EMR",                 CVID.MS_TSQ_Vantage,                     MatchType.Exact),
        new("TSQ VANTAGE AM",                  CVID.MS_TSQ_Vantage,                     MatchType.Exact),
        new("TSQ QUANTIVA",                    CVID.MS_TSQ_Quantiva,                    MatchType.Exact),
        new("TSQ ENDURA",                      CVID.MS_TSQ_Endura,                      MatchType.Exact),
        new("TSQ ALTIS",                       CVID.MS_TSQ_Altis,                       MatchType.Exact),
        new("TSQ ALTIS PLUS",                  CVID.MS_TSQ_Altis_Plus,                  MatchType.Exact),
        new("TSQ QUANTIS",                     CVID.MS_TSQ_Quantis,                     MatchType.Exact),
        new("TSQ CERTIS",                      CVID.MS_TSQ_Certis,                      MatchType.Exact),
        new("TSQ QUANTUM XLS",                 CVID.MS_TSQ_Quantum_XLS,                 MatchType.Exact),
        new("TSQ 8000",                        CVID.MS_TSQ_8000,                        MatchType.Exact),

        // Element / GC
        new("ELEMENT XR",                      CVID.MS_Element_XR,                      MatchType.Exact),
        new("ELEMENT GD",                      CVID.MS_Element_GD,                      MatchType.Exact),
        new("GC ISOLINK",                      CVID.MS_GC_IsoLink,                      MatchType.Exact),

        // Orbitrap (newer)
        new("ORBITRAP ID-X",                   CVID.MS_Orbitrap_ID_X,                   MatchType.Exact),
        new("ORBITRAP IQ-X",                   CVID.MS_Orbitrap_IQ_X,                   MatchType.Exact),

        // Q Exactive / Exactive — order-sensitive (longest-prefix first for the Contains rules)
        new("Q EXACTIVE GC ORBITRAP",          CVID.MS_Q_Exactive_GC_Orbitrap,          MatchType.Contains),
        new("Q EXACTIVE PLUS",                 CVID.MS_Q_Exactive_Plus,                 MatchType.Contains),
        new("Q EXACTIVE HF-X",                 CVID.MS_Q_Exactive_HF_X,                 MatchType.Contains),
        new("Q EXACTIVE HF",                   CVID.MS_Q_Exactive_HF,                   MatchType.Contains),
        new("Q EXACTIVE UHMR",                 CVID.MS_Q_Exactive_UHMR,                 MatchType.Contains),
        new("Q EXACTIVE FOCUS",                CVID.MS_Q_Exactive_Focus,                MatchType.Contains),
        new("Q EXACTIVE",                      CVID.MS_Q_Exactive,                      MatchType.Contains),
        new("EXACTIVE PLUS",                   CVID.MS_Exactive_Plus,                   MatchType.Contains),
        new("EXACTIVE",                        CVID.MS_Exactive,                        MatchType.Contains),

        // Orbitrap Exploris family
        new("ORBITRAP EXPLORIS 120",           CVID.MS_Orbitrap_Exploris_120,           MatchType.Exact),
        new("ORBITRAP EXPLORIS 240",           CVID.MS_Orbitrap_Exploris_240,           MatchType.Exact),
        new("ORBITRAPEXPLORISGC240",           CVID.MS_Orbitrap_Exploris_GC_240,        MatchType.ExactNoSpaces),
        new("ORBITRAPEXPLORIS240GC",           CVID.MS_Orbitrap_Exploris_GC_240,        MatchType.ExactNoSpaces),
        new("ORBITRAPEXPLORISGC",              CVID.MS_Orbitrap_Exploris_GC_MS,         MatchType.ContainsNoSpaces),
        new("ORBITRAP EXPLORIS 480",           CVID.MS_Orbitrap_Exploris_480,           MatchType.Exact),

        // Orbitrap Excedion / GC / Eclipse / Astral / Fusion family
        new("ORBITRAP EXCEDION PRO",           CVID.MS_Orbitrap_Excedion_Pro,           MatchType.Contains),
        new("ORBITRAP EXCEDION",               CVID.MS_Orbitrap_Excedion_Pro,           MatchType.Contains),
        new("ORBITRAP GC",                     CVID.MS_Orbitrap_Exploris_480,           MatchType.Contains),
        new("ECLIPSE",                         CVID.MS_Orbitrap_Eclipse,                MatchType.Contains),
        new("ASTRAL ZOOM",                     CVID.MS_Orbitrap_Astral_Zoom,            MatchType.Contains),
        new("ASTRAL",                          CVID.MS_Orbitrap_Astral,                 MatchType.Contains),

        // MALDI extras
        new("MALDI LTQ ORBITRAP XL",           CVID.MS_MALDI_LTQ_Orbitrap_XL,           MatchType.Exact),
        new("MALDI LTQ ORBITRAP DISCOVERY",    CVID.MS_MALDI_LTQ_Orbitrap_Discovery,    MatchType.Exact),

        // Voyager / Fusion / Ascend / PDA / Stellar
        new("VOYAGER",                         CVID.MS_ThermoQuest_Voyager,             MatchType.Contains),
        new("FUSION ETD",                      CVID.MS_Orbitrap_Fusion_ETD,             MatchType.Contains),
        new("FUSION LUMOS",                    CVID.MS_Orbitrap_Fusion_Lumos,           MatchType.Contains),
        new("FUSION",                          CVID.MS_Orbitrap_Fusion,                 MatchType.Contains),
        new("ASCEND",                          CVID.MS_Orbitrap_Ascend,                 MatchType.Contains),
        new("SURVEYOR PDA",                    CVID.MS_Surveyor_PDA,                    MatchType.Exact),
        new("ACCELA PDA",                      CVID.MS_Accela_PDA,                      MatchType.Exact),
        new("STELLAR",                         CVID.MS_Stellar,                         MatchType.Contains),
    };

    /// <summary>
    /// Translates a Thermo instrument-model string (as reported by the SDK) to its CV term.
    /// Returns <see cref="CVID.MS_Thermo_Electron_instrument_model"/> as the catch-all when
    /// no entry matches (mirrors cpp's <c>InstrumentModelType_Unknown</c> case).
    /// </summary>
    public static CVID Translate(string model)
    {
        if (string.IsNullOrEmpty(model)) return CVID.MS_Thermo_Electron_instrument_model;

        string upper = model.Trim().ToUpperInvariant();
        string upperNoSpaces = upper.Replace(" ", string.Empty, StringComparison.Ordinal);

        foreach (var entry in s_table)
        {
            switch (entry.Match)
            {
                case MatchType.Exact:
                    if (upper == entry.Name) return entry.Cvid;
                    break;
                case MatchType.ExactNoSpaces:
                    if (upperNoSpaces == entry.Name) return entry.Cvid;
                    break;
                case MatchType.Contains:
                    if (upper.Contains(entry.Name, StringComparison.Ordinal)) return entry.Cvid;
                    break;
                case MatchType.ContainsNoSpaces:
                    if (upperNoSpaces.Contains(entry.Name, StringComparison.Ordinal)) return entry.Cvid;
                    break;
            }
        }
        return CVID.MS_Thermo_Electron_instrument_model;
    }

    /// <summary>
    /// Diagnostic / test-only enumerator that yields every (name, expected CVID) entry the
    /// translator knows about. Used by <c>Reader_Thermo_Test</c>-equivalent tests to verify
    /// the table covers every Thermo CV term in the psi-ms hierarchy.
    /// </summary>
    public static IEnumerable<(string Name, CVID Cvid)> EnumerateMappings()
    {
        foreach (var entry in s_table)
            yield return (entry.Name, entry.Cvid);
    }
}
