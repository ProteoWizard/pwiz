namespace Pwiz.Data.MsData.MSn;

/// <summary>The MSn family file types. Port of pwiz::msdata::MSn_Type.</summary>
public enum MSnType
{
    /// <summary>Unknown / not yet determined.</summary>
    Unknown = 0,
    /// <summary>Uncompressed binary MS1.</summary>
    Bms1 = 1,
    /// <summary>Zlib-compressed binary MS1.</summary>
    Cms1 = 2,
    /// <summary>Uncompressed binary MS2.</summary>
    Bms2 = 3,
    /// <summary>Zlib-compressed binary MS2.</summary>
    Cms2 = 4,
    /// <summary>Plain-text MS1.</summary>
    Ms1 = 5,
    /// <summary>Plain-text MS2.</summary>
    Ms2 = 6,
}

/// <summary>Convenience predicates over <see cref="MSnType"/>.</summary>
public static class MSnTypeExtensions
{
    /// <summary>True iff this is an MS1-level format (Ms1 / Bms1 / Cms1).</summary>
    public static bool IsMs1(this MSnType t) =>
        t is MSnType.Ms1 or MSnType.Bms1 or MSnType.Cms1;

    /// <summary>True iff this is a text format (Ms1 / Ms2).</summary>
    public static bool IsText(this MSnType t) => t is MSnType.Ms1 or MSnType.Ms2;

    /// <summary>True iff this is a compressed binary format (Cms1 / Cms2).</summary>
    public static bool IsCompressed(this MSnType t) => t is MSnType.Cms1 or MSnType.Cms2;

    /// <summary>True iff this is a binary format (Bms1 / Bms2 / Cms1 / Cms2).</summary>
    public static bool IsBinary(this MSnType t) =>
        t is MSnType.Bms1 or MSnType.Bms2 or MSnType.Cms1 or MSnType.Cms2;
}
