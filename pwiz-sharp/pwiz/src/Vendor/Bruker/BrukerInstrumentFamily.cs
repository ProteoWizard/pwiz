using System.Globalization;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Bruker instrument families. Port of the <c>InstrumentFamily</c> enum in
/// <c>CompassDataEnums.hpp:45-60</c>; the values match the C++ enum, which is <b>not</b> the same
/// numbering as the raw <c>InstrumentFamily</c> code stored in the analysis metadata — see
/// <see cref="BrukerInstrumentFamilyCodes.FromGlobalMetadata"/>.
/// </summary>
/// <remarks>
/// The first eight values (<see cref="Trap"/>…<see cref="Maxis"/>) plus <see cref="Unknown"/> are
/// exactly what CompassXtract's <c>EDAL.InstrumentFamily</c> reports, which is why the
/// CompassXtract backend can cast its SDK value straight across. The remaining values
/// (<see cref="TimsTof"/>, <see cref="Impact"/>, <see cref="Compact"/>, <see cref="SolariX"/>) are
/// pwiz additions for instruments that predate neither CompassXtract nor its enum — cpp marks
/// them "not from CXT".
/// </remarks>
public enum BrukerInstrumentFamily
{
    /// <summary>Esquire / HCT / amaZon ion trap.</summary>
    Trap = 0,
    /// <summary>micrOTOF.</summary>
    Otof = 1,
    /// <summary>micrOTOF-Q / ultrOTOF-Q.</summary>
    OtofQ = 2,
    /// <summary>BioTOF.</summary>
    BioTof = 3,
    /// <summary>BioTOF-Q.</summary>
    BioTofQ = 4,
    /// <summary>flex-series MALDI TOF.</summary>
    MaldiTof = 5,
    /// <summary>apex FT-ICR.</summary>
    Ftms = 6,
    /// <summary>maXis.</summary>
    Maxis = 7,
    /// <summary>timsTOF (not reported by CompassXtract).</summary>
    TimsTof = 9,
    /// <summary>impact (not reported by CompassXtract).</summary>
    Impact = 90,
    /// <summary>compact (not reported by CompassXtract).</summary>
    Compact = 91,
    /// <summary>solariX (not reported by CompassXtract).</summary>
    SolariX = 92,
    /// <summary>Unknown / not reported.</summary>
    Unknown = 255,
}

/// <summary>Translation between Bruker's stored instrument-family codes and
/// <see cref="BrukerInstrumentFamily"/>.</summary>
internal static class BrukerInstrumentFamilyCodes
{
    /// <summary>
    /// Maps the raw <c>InstrumentFamily</c> code stored in the analysis metadata (the TDF/TSF
    /// <c>GlobalMetadata</c> table, the BAF <c>Properties</c> table) onto the SDK enum. Port of
    /// <c>translateInstrumentFamily</c>, which appears — in an anonymous namespace, so each
    /// translation unit gets its own copy — in <c>Baf2Sql.cpp</c>, <c>TimsData.cpp</c> and
    /// <c>TsfData.cpp</c>.
    /// </summary>
    /// <remarks>
    /// <para>One table serves all three on purpose. cpp's BAF copy omits <c>case 9</c> (timsTOF);
    /// the TDF and TSF copies have it. That is a cpp defect, not a format distinction, so this
    /// port deliberately does not reproduce it.</para>
    /// <para>The code is not container-specific: in cpp's BAF copy <c>9</c> is <b>absent</b>, not
    /// remapped to another family, and the three tables are otherwise identical. A timsTOF can
    /// write BAF — the corpus has two, whose <c>InstrumentName</c> says so — and reading one
    /// through cpp's BAF path yields <c>Unknown</c>, hence <c>MS:1000122 instrument model</c> and
    /// a componentList of just the ion source, where the TDF/TSF path yields
    /// <c>MS:1003123 timsTOF series</c> and the full analyzer/detector chain.</para>
    /// <para>Fixed upstream by adding <c>case 9</c> to <c>Baf2Sql.cpp</c>; until that ships, those
    /// files read <c>differs</c> on a corpus sweep for this reason alone and the C# output is the
    /// correct one.</para>
    /// </remarks>
    public static BrukerInstrumentFamily FromGlobalMetadata(IReadOnlyDictionary<string, string> globalMetadata)
    {
        ArgumentNullException.ThrowIfNull(globalMetadata);
        if (!globalMetadata.TryGetValue("InstrumentFamily", out var v)
            || !int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw))
            return BrukerInstrumentFamily.Unknown;

        return raw switch
        {
            1 => BrukerInstrumentFamily.Otof,
            2 => BrukerInstrumentFamily.OtofQ,
            6 => BrukerInstrumentFamily.Maxis,
            7 => BrukerInstrumentFamily.Impact,
            8 => BrukerInstrumentFamily.Compact,
            9 => BrukerInstrumentFamily.TimsTof,
            512 => BrukerInstrumentFamily.Ftms,
            513 => BrukerInstrumentFamily.SolariX,
            _ => BrukerInstrumentFamily.Unknown,
        };
    }
}
