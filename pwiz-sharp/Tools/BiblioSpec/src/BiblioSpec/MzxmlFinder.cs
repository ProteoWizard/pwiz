// Port of pwiz_tools/BiblioSpec/src/mzxmlFinder.h.
//
// Tiny streaming mzXML reader used only by the Spectrum Mill pep.xml path: walks
// <scan> elements and pulls (parentFileName, precursorMz) so BuildParser can map
// each PSM's spec name (a .pkl basename) onto the scan-index in the mzXML.

using System.Globalization;
using System.Text;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// cpp parity: mzxmlFinder.h:39 <c>class mzxmlFinder</c>. SAX-style walk over an mzXML
/// file that, for each <c>&lt;scan&gt;</c>, reads the <c>parentFileName</c> attribute on
/// the nested <c>&lt;scanOrigin&gt;</c> and the text content of <c>&lt;precursorMz&gt;</c>,
/// then looks the (name, precursor) pair up in a caller-supplied table and stores the
/// scan's number / 0-based index there.
/// </summary>
internal sealed class MzxmlFinder
{
    /// <summary>
    /// cpp parity: mzxmlFinder.h:50 <c>SpecInfo</c>. A singly-linked-list node holding one
    /// (precursor, scan) entry — many PSMs can share the same <c>specName</c> with different
    /// precursors, so the table is keyed by name and each value chains every precursor seen.
    /// </summary>
    internal sealed class SpecInfo
    {
        public int Scan { get; set; } = -1;
        public double Precursor { get; }
        public SpecInfo? Next { get; }

        public SpecInfo(double precursor, SpecInfo? next)
        {
            Precursor = precursor;
            Next = next;
        }

        /// <summary>
        /// cpp parity: mzxmlFinder.h:64. Walk the chain; return the first node whose precursor
        /// is within 0.001 of <paramref name="precursor"/>, or null.
        /// </summary>
        public SpecInfo? GetMatch(double precursor)
        {
            for (var info = (SpecInfo?)this; info is not null; info = info.Next)
            {
                if (Math.Abs(info.Precursor - precursor) <= 0.001) return info;
            }
            return null;
        }
    }

    private enum ReturnType { ScanNum, Index }

    private readonly string _fileName;

    public MzxmlFinder(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _fileName = fileName;
    }

    /// <summary>
    /// cpp parity: mzxmlFinder.h:76 — fill <see cref="SpecInfo.Scan"/> with the scan's
    /// <c>num</c> attribute (1-based).
    /// </summary>
    public void FindScanNumFromName(IDictionary<string, SpecInfo> nameNumTable)
        => Parse(nameNumTable, ReturnType.ScanNum);

    /// <summary>
    /// cpp parity: mzxmlFinder.h:82 — fill <see cref="SpecInfo.Scan"/> with the scan's
    /// 0-based document index.
    /// </summary>
    public void FindScanIndexFromName(IDictionary<string, SpecInfo> nameNumTable)
        => Parse(nameNumTable, ReturnType.Index);

    private void Parse(IDictionary<string, SpecInfo> nameNumTable, ReturnType returnType)
    {
        ArgumentNullException.ThrowIfNull(nameNumTable);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            // precursorMz is text content — keep whitespace nodes so we don't drop short
            // numeric children that the writer might have emitted alongside whitespace.
            IgnoreWhitespace = false,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = true,
        };

        using var fs = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = XmlReader.Create(fs, settings);

        int curNum = -1;
        int curIndex = -1;
        var curName = string.Empty;
        double curPrecursor = 0.0;
        var readingPrecursor = false;
        var charBuf = new StringBuilder();

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                {
                    var name = reader.LocalName;
                    var isEmpty = reader.IsEmptyElement;
                    if (name == "scan")
                    {
                        // cpp mzxmlFinder.h:100 — atoi on the `num` attr (0 if missing/invalid).
                        var num = reader.GetAttribute("num");
                        curNum = int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
                        curIndex++;
                    }
                    else if (name == "scanOrigin")
                    {
                        // cpp mzxmlFinder.h:103. <scanOrigin> is always self-closing in mzXML 3.0.
                        curName = reader.GetAttribute("parentFileName") ?? string.Empty;
                    }
                    else if (name == "precursorMz")
                    {
                        readingPrecursor = true;
                        charBuf.Clear();
                    }
                    // Self-closing elements get a matching EndElement dispatch (cpp SAXHandler does
                    // the same): fall through to the EndElement branch.
                    if (isEmpty) goto case XmlNodeType.EndElement;
                    break;
                }
                case XmlNodeType.EndElement:
                {
                    var endName = reader.LocalName;
                    if (endName == "precursorMz")
                    {
                        readingPrecursor = false;
                        if (double.TryParse(charBuf.ToString().Trim(), NumberStyles.Float,
                                CultureInfo.InvariantCulture, out var p))
                        {
                            curPrecursor = p;
                        }
                        charBuf.Clear();
                    }
                    else if (endName == "scan")
                    {
                        TryMatch(nameNumTable, returnType, curName, curPrecursor, curNum, curIndex);
                    }
                    break;
                }
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.SignificantWhitespace:
                case XmlNodeType.Whitespace:
                    if (readingPrecursor) charBuf.Append(reader.Value);
                    break;
            }
        }
    }

    private static void TryMatch(IDictionary<string, SpecInfo> nameNumTable, ReturnType returnType,
        string curName, double curPrecursor, int curNum, int curIndex)
    {
        if (!nameNumTable.TryGetValue(curName, out var head))
        {
            Verbosity.Warn($"Couldn't find '{curName}' ({curPrecursor:F3})");
            return;
        }
        var match = head.GetMatch(curPrecursor);
        if (match is null)
        {
            Verbosity.Warn($"Couldn't find '{curName}' ({curPrecursor:F3})");
            return;
        }
        match.Scan = returnType == ReturnType.ScanNum ? curNum : curIndex;
        // cpp mzxmlFinder.h:120-122 logs `i->second->getScan()` (the chain HEAD's scan, not
        // the matched node's) — preserve that quirk; the message is V_ALL anyway.
        Verbosity.Comment(VerbosityLevel.All,
            $"Scan {curName} has {(returnType == ReturnType.Index ? "index" : "scan number")} {head.Scan}.");
    }
}
