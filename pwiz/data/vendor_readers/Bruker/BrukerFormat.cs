#pragma warning disable CA1707

namespace Pwiz.Vendor.Bruker;

/// <summary>Bruker <c>.d</c> sub-formats, keyed by the metadata file that lives inside.
///
/// The CompassXtract references below are <c>&lt;c&gt;</c> rather than <c>&lt;see cref&gt;</c>
/// on purpose: <c>CompassXtractData.cs</c> is compiled out when
/// <c>$(NativeVendorsAvailable)</c> is not true, and with GenerateDocumentationFile plus
/// TreatWarningsAsErrors an unresolvable cref is CS1574 - i.e. a build ERROR in exactly the
/// no-vendor-licenses configuration. A doc comment cannot be conditionally compiled, so the
/// reference has to be one that needs no resolution.</summary>
public enum BrukerFormat
{
    /// <summary>Unknown / unsupported format.</summary>
    Unknown,
    /// <summary>timsTOF with ion-mobility (<c>analysis.tdf</c> + <c>analysis.tdf_bin</c>).</summary>
    Tdf,
    /// <summary>timsTOF without ion-mobility (<c>analysis.tsf</c> + <c>analysis.tsf_bin</c>).</summary>
    Tsf,
    /// <summary>Bruker Analysis format (<c>analysis.baf</c>), read through baf2sql.</summary>
    Baf,
    /// <summary>Bruker/Agilent YEP format (<c>analysis.yep</c>), read through the CompassXtract
    /// COM server (Windows only) - see <c>CompassXtractData</c>.</summary>
    Yep,
    /// <summary>FID data (a <c>fid</c> file), read through the CompassXtract COM server
    /// (Windows only) - see <c>CompassXtractData</c>.</summary>
    Fid,
    /// <summary>LC-only U2 data (a <c>&lt;name&gt;.u2</c> file). Identified but NOT readable -
    /// cpp is the same, its Reader_Bruker_U2 being a Reader_Bruker_Dummy and its U2 reading
    /// path commented out (CompassData.cpp:530-538). Present so a .d holding one is still
    /// recognized as a Bruker source rather than a plain folder.</summary>
    U2,
}
