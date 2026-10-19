using System.Globalization;
using System.Xml;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData.PepXml;

/// <summary>
/// Serializes an <see cref="IdentData"/> tree to TPP pepXML. Port of
/// <c>pwiz::identdata::Serializer_pepXML</c> (write side).
/// </summary>
/// <remarks>
/// Emits the schema subset that <see cref="PepXmlReader"/> can round-trip: msms_pipeline_analysis
/// header with <c>analysis_summary</c> per-software entries, then one <c>spectrum_query</c> per
/// <see cref="SpectrumIdentificationResult"/> with one <c>search_hit</c> per
/// <see cref="SpectrumIdentificationItem"/>. Score CV terms are translated to pepXML score-name
/// strings via <see cref="PepXmlTranslator.ScoreCVIDToPepXmlScoreName"/>; CVs without a known
/// translation are emitted as user-named <c>search_score</c> entries.
/// </remarks>
public sealed class PepXmlWriter
{
    /// <summary>Writes <paramref name="ident"/> as TPP pepXML to <paramref name="stream"/>.</summary>
#pragma warning disable CA1822 // kept as instance for symmetry with the reader
    public void Write(IdentData ident, Stream stream)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(ident);
        ArgumentNullException.ThrowIfNull(stream);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        using var w = XmlWriter.Create(stream, settings);

        w.WriteStartDocument();
        w.WriteStartElement("msms_pipeline_analysis", "http://regis-web.systemsbiology.net/pepXML");
        if (!string.IsNullOrEmpty(ident.CreationDate))
            w.WriteAttributeString("date", ident.CreationDate);
        w.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        w.WriteAttributeString("xsi", "schemaLocation", null,
            "http://regis-web.systemsbiology.net/pepXML http://sashimi.sourceforge.net/schema_revision/pepXML/pepXML_v117.xsd");

        // Pick the search software to drive score translation. Cpp uses the first software's
        // softwareName.cvParamChild(MS_analysis_software); we use the first SoftwareName CV term
        // when present.
        var (primarySoftware, primaryCvid) = PickPrimarySoftware(ident);

        // analysis_summary per software entry.
        foreach (var sw in ident.AnalysisSoftwareList)
            WriteAnalysisSummary(w, sw);

        WriteMsmsRunSummary(w, ident, primarySoftware, primaryCvid);

        w.WriteEndElement();
        w.WriteEndDocument();
    }

    private static (AnalysisSoftware? Software, CVID Cvid) PickPrimarySoftware(IdentData ident)
    {
        foreach (var sw in ident.AnalysisSoftwareList)
        {
            foreach (var cv in sw.SoftwareName.CVParams)
                if (cv.Cvid != CVID.CVID_Unknown && PepXmlTranslator.SoftwareCVIDToPepXmlSoftwareName(cv.Cvid) != string.Empty)
                    return (sw, cv.Cvid);
        }
        return (ident.AnalysisSoftwareList.FirstOrDefault(), CVID.CVID_Unknown);
    }

    private static void WriteAnalysisSummary(XmlWriter w, AnalysisSoftware sw)
    {
        w.WriteStartElement("analysis_summary");
        var name = sw.SoftwareName.CVParams.FirstOrDefault(p => p.Cvid != CVID.CVID_Unknown);
        if (name is not null && name.Cvid != CVID.CVID_Unknown)
        {
            var pepName = PepXmlTranslator.SoftwareCVIDToPepXmlSoftwareName(name.Cvid);
            w.WriteAttributeString("analysis", string.IsNullOrEmpty(pepName) ? CvLookup.CvTermInfo(name.Cvid).Name : pepName);
        }
        else if (!string.IsNullOrEmpty(sw.Name))
        {
            w.WriteAttributeString("analysis", sw.Name);
        }
        if (!string.IsNullOrEmpty(sw.Version)) w.WriteAttributeString("version", sw.Version);
        w.WriteEndElement();
    }

    private static void WriteMsmsRunSummary(XmlWriter w, IdentData ident,
        AnalysisSoftware? software, CVID softwareCvid)
    {
        w.WriteStartElement("msms_run_summary");

        // Try to derive a meaningful base_name from the first SpectraData entry.
        var sd = ident.DataCollection.Inputs.SpectraData.FirstOrDefault();
        if (sd is not null && !string.IsNullOrEmpty(sd.Location))
            w.WriteAttributeString("base_name", Path.GetFileNameWithoutExtension(sd.Location));

        // Required attribute per pepXML schema (TPP usually emits Thermo / mzML / etc.).
        w.WriteAttributeString("raw_data_type", "raw");
        w.WriteAttributeString("raw_data", ".raw");

        // search_summary describes the search engine + parameters.
        if (software is not null) WriteSearchSummary(w, software);

        // One spectrum_query per SpectrumIdentificationResult.
        int queryIndex = 0;
        foreach (var sil in ident.DataCollection.AnalysisData.SpectrumIdentificationList)
            foreach (var sir in sil.SpectrumIdentificationResult)
                WriteSpectrumQuery(w, sir, ++queryIndex, softwareCvid);

        w.WriteEndElement();
    }

    private static void WriteSearchSummary(XmlWriter w, AnalysisSoftware software)
    {
        w.WriteStartElement("search_summary");
        w.WriteAttributeString("base_name", string.IsNullOrEmpty(software.Name) ? "search" : software.Name);
        var cv = software.SoftwareName.CVParams.FirstOrDefault(p => p.Cvid != CVID.CVID_Unknown);
        if (cv is not null && cv.Cvid != CVID.CVID_Unknown)
        {
            var pepName = PepXmlTranslator.SoftwareCVIDToPepXmlSoftwareName(cv.Cvid);
            w.WriteAttributeString("search_engine", string.IsNullOrEmpty(pepName) ? CvLookup.CvTermInfo(cv.Cvid).Name : pepName);
        }
        if (!string.IsNullOrEmpty(software.Version)) w.WriteAttributeString("search_engine_version", software.Version);
        w.WriteAttributeString("precursor_mass_type", "monoisotopic");
        w.WriteAttributeString("fragment_mass_type", "monoisotopic");
        w.WriteAttributeString("search_id", "1");
        w.WriteEndElement();
    }

    private static void WriteSpectrumQuery(XmlWriter w, SpectrumIdentificationResult sir,
        int queryIndex, CVID softwareCvid)
    {
        w.WriteStartElement("spectrum_query");
        w.WriteAttributeString("spectrum", sir.SpectrumID);
        // Best-effort scan number extraction from "scan=N" or "scan number=N" native ids.
        var scanNumber = ExtractScanNumber(sir.SpectrumID);
        if (scanNumber > 0)
        {
            w.WriteAttributeString("start_scan", scanNumber.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("end_scan", scanNumber.ToString(CultureInfo.InvariantCulture));
        }
        // Use the first SII to pull charge / precursor mass attributes.
        var firstSii = sir.SpectrumIdentificationItem.FirstOrDefault();
        int charge = firstSii?.ChargeState ?? 0;
        if (charge > 0) w.WriteAttributeString("assumed_charge", charge.ToString(CultureInfo.InvariantCulture));
        if (firstSii is not null && charge > 0)
        {
            double neutralMass = firstSii.ExperimentalMassToCharge * charge - charge * ProtonMass;
            w.WriteAttributeString("precursor_neutral_mass", InvDouble(neutralMass));
        }
        w.WriteAttributeString("index", queryIndex.ToString(CultureInfo.InvariantCulture));

        // search_result wraps the per-hit search_hit elements.
        w.WriteStartElement("search_result");
        foreach (var sii in sir.SpectrumIdentificationItem)
            WriteSearchHit(w, sii, softwareCvid);
        w.WriteEndElement();

        w.WriteEndElement();
    }

    private static void WriteSearchHit(XmlWriter w, SpectrumIdentificationItem sii, CVID softwareCvid)
    {
        w.WriteStartElement("search_hit");
        w.WriteAttributeString("hit_rank", InvInt(sii.Rank == 0 ? 1 : sii.Rank));
        if (sii.PeptidePtr is not null && !string.IsNullOrEmpty(sii.PeptidePtr.PeptideSequence))
            w.WriteAttributeString("peptide", sii.PeptidePtr.PeptideSequence);

        // Pre/post / protein from the first peptide-evidence reference, if present.
        if (sii.PeptideEvidencePtr.Count > 0)
        {
            var pe = sii.PeptideEvidencePtr[0];
            if (pe.Pre != '\0') w.WriteAttributeString("peptide_prev_aa", pe.Pre.ToString());
            if (pe.Post != '\0') w.WriteAttributeString("peptide_next_aa", pe.Post.ToString());
            if (pe.DBSequencePtr is not null && !string.IsNullOrEmpty(pe.DBSequencePtr.Accession))
                w.WriteAttributeString("protein", pe.DBSequencePtr.Accession);
        }

        if (sii.ChargeState > 0)
        {
            double calcNeutral = sii.CalculatedMassToCharge * sii.ChargeState - sii.ChargeState * ProtonMass;
            w.WriteAttributeString("calc_neutral_pep_mass", InvDouble(calcNeutral));
            double diff = sii.ExperimentalMassToCharge - sii.CalculatedMassToCharge;
            w.WriteAttributeString("massdiff", InvDouble(diff * sii.ChargeState));
        }
        w.WriteAttributeString("num_tot_proteins", InvInt(System.Math.Max(1, sii.PeptideEvidencePtr.Count)));

        // alternative_protein for additional peptide-evidence entries.
        for (int i = 1; i < sii.PeptideEvidencePtr.Count; i++)
        {
            var pe = sii.PeptideEvidencePtr[i];
            w.WriteStartElement("alternative_protein");
            if (pe.DBSequencePtr is not null && !string.IsNullOrEmpty(pe.DBSequencePtr.Accession))
                w.WriteAttributeString("protein", pe.DBSequencePtr.Accession);
            if (pe.Pre != '\0') w.WriteAttributeString("peptide_prev_aa", pe.Pre.ToString());
            if (pe.Post != '\0') w.WriteAttributeString("peptide_next_aa", pe.Post.ToString());
            w.WriteEndElement();
        }

        // search_score per CV param recognized by the score translator; everything else as user.
        foreach (var cv in sii.CVParams)
        {
            if (cv.Cvid == CVID.CVID_Unknown) continue;
            var name = PepXmlTranslator.ScoreCVIDToPepXmlScoreName(softwareCvid, cv.Cvid);
            if (string.IsNullOrEmpty(name)) name = CvLookup.CvTermInfo(cv.Cvid).Name;
            w.WriteStartElement("search_score");
            w.WriteAttributeString("name", name);
            w.WriteAttributeString("value", cv.Value);
            w.WriteEndElement();
        }
        foreach (var u in sii.UserParams)
        {
            w.WriteStartElement("search_score");
            w.WriteAttributeString("name", u.Name);
            w.WriteAttributeString("value", u.Value);
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private static int ExtractScanNumber(string nativeId)
    {
        if (string.IsNullOrEmpty(nativeId)) return 0;
        // "scan=NNN" or "scan number=NNN" are the common forms.
        foreach (var token in nativeId.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = token.IndexOf('=');
            if (eq < 0) continue;
            string key = token[..eq].Trim();
            string value = token[(eq + 1)..].Trim();
            if (key.Equals("scan", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("scan number", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                    return n;
            }
        }
        return 0;
    }

    private const double ProtonMass = 1.00727646677;
    private static string InvInt(int v) => v.ToString(CultureInfo.InvariantCulture);
    private static string InvDouble(double v) => v.ToString("R", CultureInfo.InvariantCulture);
}
