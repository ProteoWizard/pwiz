using Pwiz.Data.IdentData.PepXml;
using Pwiz.Data.IdentData.Mzid;

namespace Pwiz.Data.IdentData;

/// <summary>
/// Convenience subclass of <see cref="IdentData"/> that loads itself from a file. Picks the
/// reader by extension: <c>.mzid</c> / <c>.mzIdentML</c> → <see cref="MzidReader"/>, anything
/// else (including <c>.pep.xml</c> / <c>.pepXML</c>) → <see cref="PepXmlReader"/>. Port of
/// <c>pwiz::identdata::IdentDataFile</c>.
/// </summary>
/// <remarks>Writers are not ported yet — read-only.</remarks>
public sealed class IdentDataFile : IdentData
{
    /// <summary>Constructs an <see cref="IdentData"/> by reading <paramref name="filename"/>.</summary>
    public IdentDataFile(string filename)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);
        using var fs = File.OpenRead(filename);
        Read(filename, fs);
    }

    /// <summary>Constructs an <see cref="IdentData"/> by reading <paramref name="stream"/>.
    /// <paramref name="filename"/> is used only for format-by-extension dispatch.</summary>
    public IdentDataFile(string filename, Stream stream)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);
        ArgumentNullException.ThrowIfNull(stream);
        Read(filename, stream);
    }

    private void Read(string filename, Stream stream)
    {
        string ext = Path.GetExtension(filename).ToLowerInvariant();
        if (ext is ".mzid" or ".mzidentml")
        {
            new MzidReader().ReadInto(stream, this);
        }
        else
        {
            // Default to pepXML — covers .pep.xml, .pepxml, and .xml.
            new PepXmlReader().ReadInto(stream, this);
        }
    }
}
