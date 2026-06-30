namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Static factory for <see cref="IBrukerData"/>. Detects the Bruker sub-format from the
/// files present in the <c>.d</c> directory and returns the appropriate concrete wrapper.
/// Port of the <c>CompassData::create</c> static factory in pwiz C++ (renamed because
/// "Compass" is an old Bruker acquisition-software brand, not related to TDF / TSF).
/// </summary>
public static class BrukerData
{
    /// <summary>
    /// Opens the <c>.d</c> directory at <paramref name="rawPath"/>. Currently supports TDF
    /// (<c>analysis.tdf</c>) and TSF (<c>analysis.tsf</c>); throws <see cref="NotSupportedException"/>
    /// for BAF / YEP / FID until their backends are ported.
    /// </summary>
    public static IBrukerData Create(string rawPath, bool useRecalibratedState = true)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        if (!Directory.Exists(rawPath))
            throw new DirectoryNotFoundException($"Bruker .d directory not found: {rawPath}");

        string full = Path.GetFullPath(rawPath);
        if (File.Exists(Path.Combine(full, "analysis.tdf")) || File.Exists(Path.Combine(full, "Analysis.tdf")))
            return new TdfData(full, useRecalibratedState);
        if (File.Exists(Path.Combine(full, "analysis.tsf")) || File.Exists(Path.Combine(full, "Analysis.tsf")))
            return new TsfData(full, useRecalibratedState);
        if (File.Exists(Path.Combine(full, "analysis.baf")) || File.Exists(Path.Combine(full, "Analysis.baf")))
            return new Baf2SqlData(full, useRecalibratedState);

        throw new NotSupportedException(
            $"Bruker format at {rawPath} is not yet supported (TDF, TSF, BAF are ported; YEP / FID still pending).");
    }
}
