using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Sources;
using Pwiz.Util;

namespace Pwiz.Data.MsData;

/// <summary>
/// File-level helpers for <see cref="MSData"/>. Port of the standalone functions in
/// <c>pwiz/data/msdata/MSDataFile.cpp</c> (checksum computation etc.).
/// </summary>
public static class MSDataFile
{
    /// <summary>
    /// Computes and sets the <see cref="CVID.MS_SHA_1"/> cvParam on <paramref name="sourceFile"/>
    /// by hashing the file at <c>Location + "/" + Name</c>. Silently returns if the location is
    /// not a <c>file://</c> URI, the file is missing, or the cvParam is already set.
    /// </summary>
    public static void CalculateSourceFileSha1(SourceFile sourceFile)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        if (sourceFile.HasCVParam(CVID.MS_SHA_1)) return;

        const string uriPrefix = "file://";
        string location = sourceFile.Location;
        if (!location.StartsWith(uriPrefix, StringComparison.OrdinalIgnoreCase)) return;
        location = location[uriPrefix.Length..].TrimStart('/');
        string path = Path.Combine(location, sourceFile.Name);

        if (!File.Exists(path)) return;
        sourceFile.Set(CVID.MS_SHA_1, Sha1Calculator.HashFile(path));
    }

    /// <summary>Computes SHA-1 for every <see cref="SourceFile"/> in <paramref name="msd"/>.</summary>
    public static void CalculateSha1Checksums(MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);
        foreach (var sf in msd.FileDescription.SourceFiles)
            CalculateSourceFileSha1(sf);
    }
}
