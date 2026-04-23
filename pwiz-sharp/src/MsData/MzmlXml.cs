using System.Globalization;
using System.Xml;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.MsData.Mzml;

/// <summary>
/// Shared helpers for reading/writing the repeating <c>cvParam</c> / <c>userParam</c> /
/// <c>referenceableParamGroupRef</c> elements that make up a <see cref="ParamContainer"/>.
/// </summary>
internal static class MzmlXml
{
    /// <summary>mzML 1.1 default namespace URI.</summary>
    internal const string MzmlNamespace = "http://psi.hupo.org/ms/mzml";

    // ---------- writers ----------

    /// <summary>Writes a ParamContainer's inner params (cvParam / userParam / paramGroupRef) as child elements.</summary>
    internal static void WriteParams(XmlWriter w, ParamContainer pc)
    {
        foreach (var pg in pc.ParamGroups)
        {
            w.WriteStartElement("referenceableParamGroupRef");
            w.WriteAttributeString("ref", pg.Id);
            w.WriteEndElement();
        }
        foreach (var cv in pc.CVParams) WriteCvParam(w, cv);
        foreach (var up in pc.UserParams) WriteUserParam(w, up);
    }

    internal static void WriteCvParam(XmlWriter w, CVParam param)
    {
        var info = CvLookup.CvTermInfo(param.Cvid);
        w.WriteStartElement("cvParam");
        w.WriteAttributeString("cvRef", info.Prefix);
        w.WriteAttributeString("accession", info.Id);
        w.WriteAttributeString("name", info.Name);
        w.WriteAttributeString("value", param.Value);
        if (param.Units != CVID.CVID_Unknown)
        {
            var u = CvLookup.CvTermInfo(param.Units);
            w.WriteAttributeString("unitCvRef", u.Prefix);
            w.WriteAttributeString("unitAccession", u.Id);
            w.WriteAttributeString("unitName", u.Name);
        }
        w.WriteEndElement();
    }

    internal static void WriteUserParam(XmlWriter w, UserParam up)
    {
        w.WriteStartElement("userParam");
        w.WriteAttributeString("name", up.Name);
        if (!string.IsNullOrEmpty(up.Value)) w.WriteAttributeString("value", up.Value);
        if (!string.IsNullOrEmpty(up.Type)) w.WriteAttributeString("type", up.Type);
        if (up.Units != CVID.CVID_Unknown)
        {
            var u = CvLookup.CvTermInfo(up.Units);
            w.WriteAttributeString("unitCvRef", u.Prefix);
            w.WriteAttributeString("unitAccession", u.Id);
            w.WriteAttributeString("unitName", u.Name);
        }
        w.WriteEndElement();
    }

    internal static void WriteCountAttr(XmlWriter w, int count)
        => w.WriteAttributeString("count", count.ToString(CultureInfo.InvariantCulture));

    // ---------- readers ----------

    /// <summary>
    /// Reads a single <c>cvParam</c> element (caller positioned on the start element).
    /// Advances past the end tag.
    /// </summary>
    internal static CVParam ReadCvParam(XmlReader r)
    {
        string? accession = r.GetAttribute("accession");
        string value = r.GetAttribute("value") ?? string.Empty;
        string? unitAcc = r.GetAttribute("unitAccession");

        CVID cvid = CVID.CVID_Unknown;
        if (!string.IsNullOrEmpty(accession))
            cvid = CvLookup.CvTermInfo(accession).Cvid;

        CVID units = CVID.CVID_Unknown;
        if (!string.IsNullOrEmpty(unitAcc))
            units = CvLookup.CvTermInfo(unitAcc).Cvid;

        SkipElement(r);
        return new CVParam(cvid, value, units);
    }

    /// <summary>Reads a single <c>userParam</c> element. Advances past the end tag.</summary>
    internal static UserParam ReadUserParam(XmlReader r)
    {
        var up = new UserParam(
            r.GetAttribute("name") ?? string.Empty,
            r.GetAttribute("value") ?? string.Empty,
            r.GetAttribute("type") ?? string.Empty);
        string? unitAcc = r.GetAttribute("unitAccession");
        if (!string.IsNullOrEmpty(unitAcc))
            up.Units = CvLookup.CvTermInfo(unitAcc).Cvid;
        SkipElement(r);
        return up;
    }

    /// <summary>
    /// Reads <c>cvParam</c> / <c>userParam</c> / <c>referenceableParamGroupRef</c> children
    /// until the caller's end tag. Caller is positioned on the parent start element;
    /// this method does NOT advance into the parent — it assumes the reader has been moved in.
    /// </summary>
    internal static void ReadParams(XmlReader r, ParamContainer pc, IReadOnlyDictionary<string, ParamGroup> paramGroups)
    {
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.NodeType == XmlNodeType.Element)
            {
                switch (r.LocalName)
                {
                    case "cvParam":
                        pc.CVParams.Add(ReadCvParam(r));
                        continue;
                    case "userParam":
                        pc.UserParams.Add(ReadUserParam(r));
                        continue;
                    case "referenceableParamGroupRef":
                        string? pgRef = r.GetAttribute("ref");
                        if (pgRef is not null && paramGroups.TryGetValue(pgRef, out var pg))
                            pc.ParamGroups.Add(pg);
                        SkipElement(r);
                        continue;
                    default:
                        // not a param child — caller handles it after we return.
                        return;
                }
            }
            r.Read();
        }
    }

    /// <summary>Reads and discards the current element (including its children). Caller on the start element.</summary>
    internal static void SkipElement(XmlReader r)
    {
        if (r.NodeType != XmlNodeType.Element)
        {
            r.Read();
            return;
        }
        if (r.IsEmptyElement) { r.Read(); return; }
        r.Skip();
    }

    /// <summary>Advances the reader to the next element, returning true when positioned on one.</summary>
    internal static bool ReadToNextElement(XmlReader r)
    {
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.Element || r.NodeType == XmlNodeType.EndElement)
                return r.NodeType == XmlNodeType.Element;
        }
        return false;
    }

    /// <summary>
    /// Moves to the first child <see cref="XmlNodeType.Element"/> of the current start element.
    /// Contract (so callers can always <c>r.Read()</c> unconditionally to advance past the parent's end tag):
    /// <list type="bullet">
    /// <item>Self-closing <c>&lt;foo/&gt;</c>: returns false, reader stays on the self-closing element.</item>
    /// <item>Empty content <c>&lt;foo&gt;&lt;/foo&gt;</c>: returns false, reader is on <c>&lt;/foo&gt;</c> EndElement.</item>
    /// <item>Has children: returns true, reader is on the first child Element.</item>
    /// </list>
    /// </summary>
    internal static bool MoveToFirstChildElement(XmlReader r)
    {
        if (r.IsEmptyElement) return false; // don't advance — caller's r.Read() will skip the self-closing tag
        r.Read();
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.NodeType == XmlNodeType.Element) return true;
            r.Read();
        }
        return false;
    }
}
