using System.Globalization;
using System.Xml;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData.PepXml;

/// <summary>
/// Reads a TPP-format pepXML file into an <see cref="IdentData"/> tree. Streaming
/// XmlReader-based; populates the schema subset currently ported (analysis software, sequence
/// collection, data collection / analysis data / spectrum identification chain).
/// </summary>
/// <remarks>
/// Translates pepXML's flat hit-per-spectrum_query model to mzIdentML's hierarchical
/// SpectrumIdentificationList → ...Result → ...Item structure. Score-name strings are
/// translated to mzIdentML CV ids via <see cref="PepXmlTranslator.PepXmlScoreNameToCVID"/>;
/// the search-engine identity is taken from the <c>analysis</c> attribute on the
/// <c>analysis_summary</c> element.
/// </remarks>
public sealed class PepXmlReader
{
    /// <summary>Reads <paramref name="stream"/> as TPP pepXML and populates
    /// <paramref name="target"/>.</summary>
#pragma warning disable CA1822 // kept as instance for symmetry with MzidReader / future buffered state
    public void ReadInto(Stream stream, IdentData target)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(target);

        var settings = new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Ignore,
        };
        using var xr = XmlReader.Create(stream, settings);

        // pepXML files generally describe a single search run. We accumulate one
        // SpectrumIdentificationList and inflate per-search analyses as we encounter them.
        var sil = new SpectrumIdentificationList { Id = "SIL_1" };
        target.DataCollection.AnalysisData.SpectrumIdentificationList.Add(sil);

        AnalysisSoftware? currentSoftware = null;
        CVID currentSoftwareCvid = CVID.CVID_Unknown;

        // Each pepXML <spectrum_query> becomes one SpectrumIdentificationResult; each
        // contained <search_hit> becomes one SpectrumIdentificationItem.
        var peptideById = new Dictionary<string, Peptide>(StringComparer.Ordinal);

        while (xr.Read())
        {
            if (xr.NodeType != XmlNodeType.Element) continue;
            switch (xr.LocalName)
            {
                case "msms_pipeline_analysis":
                    target.CreationDate = xr.GetAttribute("date") ?? string.Empty;
                    break;
                case "analysis_summary":
                    var analysis = xr.GetAttribute("analysis") ?? string.Empty;
                    var version = xr.GetAttribute("version") ?? string.Empty;
                    currentSoftwareCvid = PepXmlTranslator.PepXmlSoftwareNameToCVID(analysis);
                    currentSoftware = new AnalysisSoftware
                    {
                        Id = string.IsNullOrEmpty(analysis) ? $"AS_{target.AnalysisSoftwareList.Count + 1}" : analysis,
                        Name = analysis,
                        Version = version,
                    };
                    if (currentSoftwareCvid != CVID.CVID_Unknown)
                        currentSoftware.SoftwareName.CVParams.Add(new CVParam(currentSoftwareCvid));
                    target.AnalysisSoftwareList.Add(currentSoftware);
                    break;
                case "spectrum_query":
                    sil.SpectrumIdentificationResult.Add(ReadSpectrumQuery(xr, peptideById, currentSoftwareCvid, target));
                    break;
            }
        }
    }

    private static SpectrumIdentificationResult ReadSpectrumQuery(XmlReader xr,
        Dictionary<string, Peptide> peptideById, CVID softwareCvid, IdentData target)
    {
        var sir = new SpectrumIdentificationResult
        {
            Id = $"SIR_{xr.GetAttribute("spectrum") ?? xr.GetAttribute("start_scan") ?? "x"}",
            SpectrumID = ResolveSpectrumNativeId(xr),
        };
        int chargeState = 0;
        double precursorNeutralMass = 0;
        if (int.TryParse(xr.GetAttribute("assumed_charge"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
            chargeState = z;
        if (double.TryParse(xr.GetAttribute("precursor_neutral_mass"), NumberStyles.Float, CultureInfo.InvariantCulture, out var mneut))
            precursorNeutralMass = mneut;

        if (xr.IsEmptyElement) return sir;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "search_result")
                ReadSearchResult(sub, sir, peptideById, softwareCvid, chargeState, precursorNeutralMass, target);
        }
        return sir;
    }

    private static string ResolveSpectrumNativeId(XmlReader xr)
    {
        // pepXML usually stores a TPP "basename.scan.scan.charge" id; mzIdentML wants a native
        // mzML-style id (e.g. "scan=1234"). Take the start_scan / end_scan attributes when
        // present and synthesize "scan=N"; fall back to the spectrum attribute verbatim.
        var startScan = xr.GetAttribute("start_scan");
        if (!string.IsNullOrEmpty(startScan)) return $"scan={startScan}";
        return xr.GetAttribute("spectrum") ?? string.Empty;
    }

    private static void ReadSearchResult(XmlReader xr, SpectrumIdentificationResult sir,
        Dictionary<string, Peptide> peptideById, CVID softwareCvid,
        int chargeState, double precursorNeutralMass, IdentData target)
    {
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "search_hit")
                sir.SpectrumIdentificationItem.Add(ReadSearchHit(sub, peptideById, softwareCvid,
                    chargeState, precursorNeutralMass, target));
        }
    }

    private static SpectrumIdentificationItem ReadSearchHit(XmlReader xr,
        Dictionary<string, Peptide> peptideById, CVID softwareCvid,
        int chargeState, double precursorNeutralMass, IdentData target)
    {
        string peptideSeq = xr.GetAttribute("peptide") ?? string.Empty;
        string hitRank = xr.GetAttribute("hit_rank") ?? "1";
        string protein = xr.GetAttribute("protein") ?? string.Empty;

        // Reuse one Peptide per sequence so peptide_ref equality survives the round-trip.
        if (!peptideById.TryGetValue(peptideSeq, out var pep))
        {
            pep = new Peptide
            {
                Id = $"PEP_{peptideById.Count + 1}",
                PeptideSequence = peptideSeq,
            };
            peptideById[peptideSeq] = pep;
            target.SequenceCollection.Peptides.Add(pep);
        }

        var sii = new SpectrumIdentificationItem
        {
            Id = $"SII_{Guid.NewGuid():N}",
            ChargeState = chargeState,
            PassThreshold = true, // pepXML doesn't carry an explicit pass flag; assume true
            PeptidePtr = pep,
        };
        if (int.TryParse(hitRank, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)) sii.Rank = rank;
        if (double.TryParse(xr.GetAttribute("calc_neutral_pep_mass"), NumberStyles.Float, CultureInfo.InvariantCulture, out var calcMass) && chargeState > 0)
            sii.CalculatedMassToCharge = NeutralMassToMz(calcMass, chargeState);
        if (chargeState > 0)
            sii.ExperimentalMassToCharge = NeutralMassToMz(precursorNeutralMass, chargeState);

        if (!xr.IsEmptyElement)
        {
            using var sub = xr.ReadSubtree();
            while (sub.Read())
            {
                if (sub.NodeType != XmlNodeType.Element) continue;
                if (sub.LocalName == "search_score")
                {
                    string name = sub.GetAttribute("name") ?? string.Empty;
                    string value = sub.GetAttribute("value") ?? string.Empty;
                    var scoreCvid = PepXmlTranslator.PepXmlScoreNameToCVID(softwareCvid, name);
                    if (scoreCvid != CVID.CVID_Unknown)
                        sii.CVParams.Add(new CVParam(scoreCvid, value));
                    else
                        sii.UserParams.Add(new UserParam(name, value));
                }
            }
        }
        return sii;
    }

    private const double ProtonMass = 1.00727646677;
    private static double NeutralMassToMz(double neutralMass, int charge) =>
        (neutralMass + charge * ProtonMass) / charge;
}
