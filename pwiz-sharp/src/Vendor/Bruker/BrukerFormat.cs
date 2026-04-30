#pragma warning disable CA1707

namespace Pwiz.Vendor.Bruker;

/// <summary>Bruker <c>.d</c> sub-formats, keyed by the metadata file that lives inside.</summary>
public enum BrukerFormat
{
    /// <summary>Unknown / unsupported format.</summary>
    Unknown,
    /// <summary>timsTOF with ion-mobility (<c>analysis.tdf</c> + <c>analysis.tdf_bin</c>).</summary>
    Tdf,
    /// <summary>timsTOF without ion-mobility (<c>analysis.tsf</c> + <c>analysis.tsf_bin</c>).</summary>
    Tsf,
    /// <summary>Bruker Analysis format (<c>analysis.baf</c>). Not ported yet.</summary>
    Baf,
    /// <summary>Bruker/Agilent YEP format (<c>analysis.yep</c>). Not ported yet.</summary>
    Yep,
    /// <summary>FID data (a <c>fid</c> file). Not ported yet.</summary>
    Fid,
}
