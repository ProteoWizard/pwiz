using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.IdentData.PepXml;

/// <summary>
/// Lookup tables that translate between mzIdentML CV terms and the (search-engine, score-name)
/// strings pepXML uses. Port of cpp's <c>AnalysisSoftwareTranslator</c> +
/// <c>ScoreTranslator</c> (<c>Serializer_pepXML.cpp</c>). Case-insensitive on the string side.
/// </summary>
public static class PepXmlTranslator
{
    /// <summary>
    /// Maps a pepXML <c>analysis</c> attribute (or other software name) to its mzIdentML CV id.
    /// Returns <see cref="CVID.CVID_Unknown"/> when no entry is registered.
    /// </summary>
    public static CVID PepXmlSoftwareNameToCVID(string softwareName) =>
        _softwareNameToCvid.TryGetValue(softwareName, out var cvid) ? cvid : CVID.CVID_Unknown;

    /// <summary>The preferred software name pepXML uses for a given software CV term.</summary>
    public static string SoftwareCVIDToPepXmlSoftwareName(CVID softwareCvid) =>
        _preferredSoftwareNameByCvid.TryGetValue(softwareCvid, out var name) ? name : string.Empty;

    /// <summary>
    /// Maps a (search-engine, score-name) pair to its mzIdentML CV id (e.g.
    /// <c>(MS_SEQUEST, "xcorr")</c> → <c>MS_SEQUEST_xcorr</c>). Returns
    /// <see cref="CVID.CVID_Unknown"/> when no entry is registered.
    /// </summary>
    public static CVID PepXmlScoreNameToCVID(CVID softwareCvid, string scoreName) =>
        _scoreNameLookup.TryGetValue(softwareCvid, out var byName)
        && byName.TryGetValue(scoreName, out var scoreCvid)
            ? scoreCvid : CVID.CVID_Unknown;

    /// <summary>The preferred pepXML score name for a (software, score) CV pair.</summary>
    public static string ScoreCVIDToPepXmlScoreName(CVID softwareCvid, CVID scoreCvid) =>
        _preferredScoreNameLookup.TryGetValue(softwareCvid, out var byCvid)
        && byCvid.TryGetValue(scoreCvid, out var name)
            ? name : string.Empty;

    // -------- table data (mirrors cpp scoreTranslationTable + analysisSoftwareTranslationTable) --------

    private static readonly Dictionary<string, CVID> _softwareNameToCvid;
    private static readonly Dictionary<CVID, string> _preferredSoftwareNameByCvid;
    private static readonly Dictionary<CVID, Dictionary<string, CVID>> _scoreNameLookup;
    private static readonly Dictionary<CVID, Dictionary<CVID, string>> _preferredScoreNameLookup;

    static PepXmlTranslator()
    {
        _softwareNameToCvid = new(StringComparer.OrdinalIgnoreCase);
        _preferredSoftwareNameByCvid = new();
        foreach (var (cvid, names) in SoftwareTable)
        {
            var split = names.Split(';');
            _preferredSoftwareNameByCvid[cvid] = split[0];
            foreach (var n in split) _softwareNameToCvid[n] = cvid;
        }

        _scoreNameLookup = new();
        _preferredScoreNameLookup = new();
        foreach (var (softwareCvid, scoreCvid, names) in ScoreTable)
        {
            var split = names.Split(';');
            if (!_scoreNameLookup.TryGetValue(softwareCvid, out var byName))
                _scoreNameLookup[softwareCvid] = byName = new Dictionary<string, CVID>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in split) byName[n] = scoreCvid;

            if (!_preferredScoreNameLookup.TryGetValue(softwareCvid, out var byCvid))
                _preferredScoreNameLookup[softwareCvid] = byCvid = new Dictionary<CVID, string>();
            byCvid[scoreCvid] = split[0];
        }
    }

    // First name in each row is the preferred form pepXML emits; alternates are accepted on read.
    private static readonly (CVID Cvid, string Names)[] SoftwareTable =
    {
        (CVID.MS_ProteoWizard_software, "ProteoWizard software;ProteoWizard"),
        (CVID.MS_SEQUEST, "Sequest"),
        (CVID.MS_Mascot, "Mascot"),
        (CVID.MS_OMSSA, "OMSSA"),
        (CVID.MS_Phenyx, "Phenyx"),
        (CVID.MS_greylag, "greylag"),
        (CVID.MS_ProteinPilot_Software, "ProteinPilot;Protein Pilot"),
        (CVID.MS_ProteinLynx_Global_Server, "ProteinLynx;Protein Lynx;PLGS"),
        (CVID.MS_MyriMatch, "MyriMatch"),
        (CVID.MS_TagRecon, "TagRecon"),
        (CVID.MS_Pepitome, "Pepitome"),
        (CVID.MS_X_Tandem, "X! Tandem;X!Tandem;xtandem;X! Tandem (k-score)"),
        (CVID.MS_Spectrum_Mill_for_MassHunter_Workstation, "Spectrum Mill;SpectrumMill"),
        (CVID.MS_Proteios, "Proteios"),
        (CVID.MS_MS_GF_, "MS-GF+"),
        (CVID.MS_Comet, "Comet"),
        (CVID.MS_Percolator, "Percolator"),
    };

    // (softwareCvid, scoreCvid, "preferredName;altName1;altName2;...").
    private static readonly (CVID SoftwareCvid, CVID ScoreCvid, string Names)[] ScoreTable =
    {
        (CVID.MS_SEQUEST, CVID.MS_SEQUEST_xcorr, "xcorr"),
        (CVID.MS_SEQUEST, CVID.MS_SEQUEST_deltacn, "deltacn;deltcn"),
        (CVID.MS_Mascot, CVID.MS_Mascot_score, "ionscore;score"),
        (CVID.MS_Mascot, CVID.MS_Mascot_identity_threshold, "identityscore"),
        (CVID.MS_Mascot, CVID.MS_Mascot_homology_threshold, "homologyscore"),
        (CVID.MS_Mascot, CVID.MS_Mascot_expectation_value, "expect"),
        (CVID.MS_OMSSA, CVID.MS_OMSSA_pvalue, "pvalue"),
        (CVID.MS_OMSSA, CVID.MS_OMSSA_evalue, "expect"),
        (CVID.MS_Phenyx, CVID.MS_Phenyx_Pepzscore, "zscore"),
        (CVID.MS_Phenyx, CVID.MS_Phenyx_PepPvalue, "zvalue"),
        (CVID.MS_MyriMatch, CVID.MS_MyriMatch_MVH, "mvh"),
        (CVID.MS_TagRecon, CVID.MS_MyriMatch_MVH, "mvh"),
        (CVID.MS_Pepitome, CVID.MS_MyriMatch_MVH, "mvh"),
        (CVID.MS_MyriMatch, CVID.MS_MyriMatch_mzFidelity, "mzFidelity"),
        (CVID.MS_TagRecon, CVID.MS_MyriMatch_mzFidelity, "mzFidelity"),
        (CVID.MS_Pepitome, CVID.MS_MyriMatch_mzFidelity, "mzFidelity"),
        (CVID.MS_X_Tandem, CVID.MS_X_Tandem_hyperscore, "hyperscore"),
        (CVID.MS_X_Tandem, CVID.MS_X_Tandem_expect, "expect"),
        (CVID.MS_MS_GF, CVID.MS_MS_GF_RawScore, "raw"),
        (CVID.MS_MS_GF, CVID.MS_MS_GF_DeNovoScore, "denovo"),
        (CVID.MS_MS_GF, CVID.MS_MS_GF_Energy, "energy"),
        (CVID.MS_MS_GF, CVID.MS_MS_GF_EValue, "EValue"),
        (CVID.MS_MS_GF, CVID.MS_MS_GF_QValue, "QValue"),
        (CVID.MS_MS_GF, CVID.MS_MS_GF_SpecEValue, "SpecEValue"),
        (CVID.MS_MS_GF, CVID.MS_MS_GF_PepQValue, "PepQValue"),
        (CVID.MS_MS_GF, CVID.MS_MS_GF_PEP, "PEP"),
        (CVID.MS_MS_GF_, CVID.MS_MS_GF_RawScore, "raw"),
        (CVID.MS_MS_GF_, CVID.MS_MS_GF_DeNovoScore, "denovo"),
        (CVID.MS_MS_GF_, CVID.MS_MS_GF_Energy, "energy"),
        (CVID.MS_MS_GF_, CVID.MS_MS_GF_EValue, "EValue"),
        (CVID.MS_MS_GF_, CVID.MS_MS_GF_QValue, "QValue"),
        (CVID.MS_MS_GF_, CVID.MS_MS_GF_SpecEValue, "SpecEValue"),
        (CVID.MS_MS_GF_, CVID.MS_MS_GF_PepQValue, "PepQValue"),
        (CVID.MS_MS_GF_, CVID.MS_MS_GF_PEP, "PEP"),
        (CVID.MS_Comet, CVID.MS_Comet_xcorr, "xcorr"),
        (CVID.MS_Comet, CVID.MS_Comet_deltacn, "deltacn"),
        (CVID.MS_Comet, CVID.MS_Comet_deltacnstar, "deltacnstar"),
        (CVID.MS_Comet, CVID.MS_Comet_sprank, "sprank"),
        (CVID.MS_Comet, CVID.MS_Comet_spscore, "spscore"),
        (CVID.MS_Comet, CVID.MS_Comet_expectation_value, "expect"),
        (CVID.MS_Percolator, CVID.MS_percolator_score, "percolator_score"),
        (CVID.MS_Percolator, CVID.MS_percolator_Q_value, "qvalue;percolator_qvalue"),
        (CVID.MS_Percolator, CVID.MS_percolator_PEP, "PEP;percolator_PEP"),
    };
}
