using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Btdx;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Readers;

/// <summary>Identifies and reads Bruker BioTools DataExchange (BTDX) XML files.</summary>
/// <remarks>Port of pwiz::msdata::Reader_BTDX.</remarks>
public sealed class BtdxReaderAdapter : IReader
{
    /// <inheritdoc/>
    public string TypeName => "Bruker Data Exchange";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_Bruker_XML_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".xml" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        // cpp identifies by XML root element name == "root". Files don't carry a specific
        // extension (Bruker reuses .xml), so the content sniff is the only reliable signal.
        if (head is null) return CVID.CVID_Unknown;
        string root = ExtractRootElementName(head);
        return root == "root" ? CvType : CVID.CVID_Unknown;
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        using var stream = File.OpenRead(filename);
        var list = SpectrumListBtdx.Read(stream);

        result.CVs.Clear();
        result.CVs.AddRange(MSData.DefaultCVList);
        result.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);
        result.FileDescription.FileContent.Set(CVID.MS_centroid_spectrum);

        var sourceFile = new SourceFile
        {
            Id = "BTDX1",
            Name = Path.GetFileName(filename),
            Location = "file:///" + Path.GetDirectoryName(Path.GetFullPath(filename))?.Replace('\\', '/'),
        };
        sourceFile.Set(CvType);
        result.FileDescription.SourceFiles.Add(sourceFile);

        string basename = Path.GetFileNameWithoutExtension(filename);
        result.Id = basename;
        result.Run.Id = basename;
        result.Run.SpectrumList = list;
        result.Run.ChromatogramList = new ChromatogramListSimple();
    }

    /// <summary>
    /// Returns the local-name of the document's root element from the first KB of the file,
    /// or empty if no element start is found. Skips XML declaration / processing instructions
    /// / comments.
    /// </summary>
    internal static string ExtractRootElementName(string head)
    {
        // Walk the head looking for the first '<' that starts a real element (not <?xml, <!--, <!DOCTYPE).
        for (int i = 0; i < head.Length; i++)
        {
            char c = head[i];
            if (c != '<') continue;
            if (i + 1 >= head.Length) break;
            char next = head[i + 1];
            if (next == '?')
            {
                int close = head.IndexOf("?>", i + 2, StringComparison.Ordinal);
                if (close < 0) break;
                i = close + 1;
                continue;
            }
            if (next == '!')
            {
                int close = head.IndexOf('>', i + 2);
                if (close < 0) break;
                i = close;
                continue;
            }
            // Real element start.
            int j = i + 1;
            while (j < head.Length)
            {
                char cj = head[j];
                if (cj == ' ' || cj == '\t' || cj == '\r' || cj == '\n' || cj == '>' || cj == '/') break;
                j++;
            }
            return head[(i + 1)..j];
        }
        return string.Empty;
    }
}
