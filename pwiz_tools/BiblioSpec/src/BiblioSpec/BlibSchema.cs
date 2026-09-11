// Port of pwiz_tools/BiblioSpec/src/BlibMaker.cpp schema definitions
//
// The cpp BlibMaker::createTables / createTable methods build CREATE TABLE strings
// inline. Here we extract them into reusable constants so callers (BlibMaker / tests
// / Skyline) can ask for "the canonical version-11 schema" without instantiating a
// BlibMaker. Numeric values and string contents must remain byte-for-byte identical
// to cpp because Skyline's schema-doc generator and downstream BlibUtil readers
// compare against these literals.

using System.Text;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Schema constants for the BiblioSpec .blib SQLite format.
/// </summary>
/// <remarks>
/// Mirrors the C++ <c>BlibMaker.h</c> version macros and the CREATE TABLE strings in
/// <c>BlibMaker.cpp</c>'s <c>createTables</c> / <c>createTable</c>.
/// <para>
/// Schema version is stored in <c>LibInfo.minorVersion</c> (the cpp comment at
/// BlibMaker.h:50 explains why: the original release shipped without a schema version
/// field, so minorVersion was repurposed). Major version increments per build are
/// stored in <c>LibInfo.majorVersion</c>.
/// </para>
/// </remarks>
public static class BlibSchema
{
    /// <summary>cpp <c>MAJOR_VERSION_CURRENT</c> (BlibMaker.h:52).</summary>
    public const int CurrentMajorVersion = 0;

    /// <summary>cpp <c>MINOR_VERSION_CURRENT</c> — also the schema version (BlibMaker.h:53).</summary>
    public const int CurrentMinorVersion = 11;

    /// <summary>cpp <c>MIN_VERSION_WORKFLOW</c> — schema with SpectrumSourceFile.workflowType.</summary>
    public const int MinVersionWorkflow = 11;

    /// <summary>cpp <c>MIN_VERSION_TIC</c> — schema with RefSpectra.totalIonCurrent.</summary>
    public const int MinVersionTic = 10;

    /// <summary>cpp <c>MIN_VERSION_PROTEINS</c> — schema with Proteins / RefSpectraProteins tables.</summary>
    public const int MinVersionProteins = 9;

    /// <summary>cpp <c>MIN_VERSION_RT_BOUNDS</c> — schema with RefSpectra.startTime / endTime.</summary>
    public const int MinVersionRtBounds = 8;

    /// <summary>cpp <c>MIN_VERSION_PEAK_ANNOT</c> — schema with RefSpectraPeakAnnotations.</summary>
    public const int MinVersionPeakAnnot = 7;

    /// <summary>cpp <c>MIN_VERSION_IMS_UNITS</c> — schema generalising ion mobility to value+type.</summary>
    public const int MinVersionImsUnits = 6;

    /// <summary>cpp <c>MIN_VERSION_SMALL_MOL</c> — schema with small-molecule columns.</summary>
    public const int MinVersionSmallMol = 5;

    /// <summary>cpp <c>MIN_VERSION_CCS</c> — schema with collisionalCrossSectionSqA.</summary>
    public const int MinVersionCcs = 4;

    /// <summary>cpp <c>MIN_VERSION_IMS_HEOFF</c> — schema with ion-mobility high-energy offset.</summary>
    public const int MinVersionImsHeoff = 3;

    /// <summary>cpp <c>MIN_VERSION_IMS</c> — schema with ion mobility column.</summary>
    public const int MinVersionIms = 2;

    /// <summary>
    /// SQL comment block describing schema version history. Emitted into the LibInfo table's
    /// CREATE TABLE statement so the history is preserved with the database.
    /// </summary>
    /// <remarks>
    /// Mirrors cpp <c>version_history_comment</c> (BlibMaker.h:66). Keep in lockstep with
    /// the <c>MinVersion*</c> constants.
    /// </remarks>
    public const string VersionHistoryComment =
        "-- Schema version number:\n" +
        "-- Version 11 adds Workflow as a column in SpectrumSourceFile\n" +
        "-- Version 10 adds TIC as a column\n" +
        "-- Version 9 adds Proteins and RefSpectraProteins tables\n" +
        "-- Version 8 adds startTime and endTime\n" +
        "-- Version 7 adds peak annotations\n" +
        "-- Version 6 generalized ion mobility to value, high energy offset, and type (currently drift time msec, and inverse reduced ion mobility Vsec/cm2)\n" +
        "-- Version 5 added small molecule columns\n" +
        "-- Version 4 added collisional cross section for ion mobility, still supports drift time only\n" +
        "-- Version 3 added product ion mobility offset information for Waters Mse IMS\n" +
        "-- Version 2 added ion mobility information\n";

    // --- table create statements -------------------------------------------------------------

    /// <summary>
    /// CREATE TABLE for <c>LibInfo</c>. Mirrors cpp BlibMaker.cpp:339-345 — note the
    /// trailing space after <c>minorVersion INTEGER </c> and the embedded version history
    /// comment. Match byte-for-byte for downstream "blibbuild -d" / Skyline tooling.
    /// </summary>
    public const string CreateLibInfo =
        "CREATE TABLE LibInfo( -- gives top level information about library, including whether it is redundant or non-redundant (nr). Redundant libraries may have more than one spectrum per precursor.\n" +
        "libLSID TEXT, -- LSID of form urn:lsid:<authority>:spectral_library:bibliospec:<type:redundant|nr>:<library name> e.g. urn:lsid:proteome.gs.washington.edu:spectral_library:bibliospec:redundant:byonic.blib\n" +
        "createTime TEXT, -- local creation time in ctime() format e.g. Thu Nov 16 17:02:18 2017\n" +
        "numSpecs INTEGER, -- number of spectra in this library (-1 means not yet counted)\n" +
        "majorVersion INTEGER, -- revision number for this library (count starts at 1)\n" +
        "minorVersion INTEGER " + VersionHistoryComment + ")";

    /// <summary>
    /// CREATE TABLE for <c>RefSpectra</c>. The small-molecule columns are spliced in from
    /// <see cref="SmallMolMetadata.SqlColumnDeclarations"/> to match cpp BlibMaker.cpp:377.
    /// </summary>
    public static readonly string CreateRefSpectra = BuildRefSpectraCreate();

    /// <summary>CREATE TABLE for <c>Modifications</c>. cpp BlibMaker.cpp:385.</summary>
    public const string CreateModifications =
        "CREATE TABLE Modifications ( -- modification masses and positions (peptide use only)\n" +
        "id INTEGER primary key autoincrement not null,\n" +
        "RefSpectraID INTEGER, -- the RefSpectra in which this modification occurs\n" +
        "position INTEGER, -- position of the modified AA in the peptide (1-based)\n" +
        "mass REAL -- incremental mass of the modification\n" +
        ")";

    /// <summary>CREATE TABLE for <c>RefSpectraPeaks</c>. cpp BlibMaker.cpp:392.</summary>
    public const string CreateRefSpectraPeaks =
        "CREATE TABLE RefSpectraPeaks( -- mz and intensity values\n" +
        "RefSpectraID INTEGER, -- ID of the RefSpectra containing these peaks\n" +
        "peakMZ BLOB, -- mz values encoded as little-endian 64 bit doubles, length is determined by the numPeaks value in the corresponding RefSpectra. Usually zlib-compressed if compressed size is less than original size.\n" +
        "peakIntensity BLOB -- mz values encoded as little-endian 32 bit floats, length is determined by the numPeaks value in the corresponding RefSpectra.  Usually zlib-compressed if compressed size is less than original size.\n" +
        ")";

    /// <summary>CREATE TABLE for <c>Proteins</c>. cpp BlibMaker.cpp:481.</summary>
    public const string CreateProteins =
        "CREATE TABLE Proteins -- protein information for RefSpectra.\n" +
        "(id INTEGER primary key autoincrement not null,\n" +
        "accession VARCHAR(200) -- protein accession number\n)";

    /// <summary>CREATE TABLE for <c>RefSpectraProteins</c>. cpp BlibMaker.cpp:486.</summary>
    public const string CreateRefSpectraProteins =
        "CREATE TABLE RefSpectraProteins -- mapping of proteins between RefSpectra and Proteins tables.\n" +
        "(RefSpectraId INTEGER not null, -- the RefSpectra being mapped to a protein\n" +
        "ProteinId INTEGER not null -- the Protein for the RefSpectra\n)";

    /// <summary>CREATE TABLE for <c>RefSpectraPeakAnnotations</c>. cpp BlibMaker.cpp:464.</summary>
    public const string CreateRefSpectraPeakAnnotations =
        "CREATE TABLE RefSpectraPeakAnnotations -- optional annotations for peaks in RefSpectra. There may be more than one annotation per peak, and not every peak in a RefSpectra has to be annotated.\n" +
        "(id INTEGER primary key autoincrement not null,\n" +
        "RefSpectraID INTEGER not null, -- the RefSpectra containing the peak being annotated\n" +
        "peakIndex INTEGER not null, -- index into the mz/intensity list for the RefSpectra\n" +
        "name VARCHAR(256), -- fragment molecule name\n" +
        "formula VARCHAR(256), -- fragment neutral chemical formula\n" +
        "inchiKey VARCHAR(256), -- fragment molecular identifier for structure retrieval\n" +
        "otherKeys VARCHAR(256), -- alternative molecular identifiers for fragment structure retrieval, tab separated e.g. cas:58-08-2\\thmdb:01847 \n" +
        "charge INTEGER, -- integer charge value, must agree with fragment adduct\n" +
        "adduct VARCHAR(256), -- fragment adduct description, can include neutral loss e.g. [M+H] or [M-H2O+] \n" +
        "comment VARCHAR(256), -- freetext comment\n" +
        "mzTheoretical REAL not null, -- calculated mz, should agree with formula and adduct if any\n" +
        "mzObserved REAL not null -- actual measured mz, should agree with the indexed mz found in the RefSpectra\n)";

    /// <summary>CREATE TABLE for <c>SpectrumSourceFiles</c>. cpp BlibMaker.cpp:423.</summary>
    public const string CreateSpectrumSourceFiles =
        "CREATE TABLE SpectrumSourceFiles ( -- information about the file or files from which this spectral library was derived\n" +
        "id INTEGER PRIMARY KEY autoincrement not null,\n" +
        "fileName VARCHAR(512), -- source spectrum file; same as idFilename if embedded spectra were used, otherwise the path to the external spectrum file (mzML/mzXML)\n" +
        "idFileName VARCHAR(512), -- identification file, typically some kind of search tool output\n" +
        "cutoffScore REAL, -- filter threshold used when converting the source file to a BiblioSpec library. See RefSpectra scoreType field for information about the type of cutoff.\n" +
        "workflowType TINYINT -- 0 for DDA, 1 for DIA\n" +
        ")";

    /// <summary>CREATE TABLE for <c>ScoreTypes</c>. cpp BlibMaker.cpp:434.</summary>
    public const string CreateScoreTypes =
        "CREATE TABLE ScoreTypes ( -- information about the various kinds of cutoff scores understood by BiblioSpec\n" +
        "id INTEGER PRIMARY KEY,  -- as used in scoreType field of RefSpectra\n" +
        "scoreType VARCHAR(128), -- name of the score type, \n" +
        "probabilityType VARCHAR(128) -- detail about the cutoff logic used by each score type, PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT, PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT, or NOT_A_PROBABILITY_VALUE\n" +
        ")";

    /// <summary>CREATE TABLE for <c>IonMobilityTypes</c>. cpp BlibMaker.cpp:450.</summary>
    public const string CreateIonMobilityTypes =
        "CREATE TABLE IonMobilityTypes ( -- table of known ion mobility units\n" +
        "id INTEGER PRIMARY KEY, -- as used in ionMobilityType field of RefSpectra\n" +
        "ionMobilityType VARCHAR(128) -- text description of ion mobility units\n" +
        ")";

    // --- index statements (emitted at COMMIT, see cpp BlibMaker::commit) ---------------------

    /// <summary>cpp BlibMaker.cpp:312.</summary>
    public const string CreateIdxPeptide = "CREATE INDEX idxPeptide ON RefSpectra (peptideSeq, precursorCharge)";

    /// <summary>cpp BlibMaker.cpp:313.</summary>
    public const string CreateIdxPeptideMod = "CREATE INDEX idxPeptideMod ON RefSpectra (peptideModSeq, precursorCharge)";

    /// <summary>cpp BlibMaker.cpp:314.</summary>
    public const string CreateIdxRefIdPeaks = "CREATE INDEX idxRefIdPeaks ON RefSpectraPeaks (RefSpectraID)";

    /// <summary>cpp BlibMaker.cpp:315.</summary>
    public const string CreateIdxRefIdPeakAnnotations = "CREATE INDEX idxRefIdPeakAnnotations ON RefSpectraPeakAnnotations (RefSpectraID)";

    /// <summary>cpp BlibMaker.cpp:316.</summary>
    public const string CreateIdxMoleculeName = "CREATE INDEX idxMoleculeName ON RefSpectra (moleculeName, precursorAdduct)";

    /// <summary>cpp BlibMaker.cpp:317.</summary>
    public const string CreateIdxInChiKey = "CREATE INDEX idxInChiKey ON RefSpectra (inchiKey, precursorAdduct)";

    /// <summary>
    /// All CREATE TABLE statements that <see cref="BlibMaker.CreateTables"/> emits, in the
    /// same order as cpp <c>BlibMaker::createTables</c>. The LibInfo INSERT is NOT in this
    /// list because it requires runtime values (LSID, ctime, version) — callers that need
    /// the full bootstrap should call <see cref="BlibMaker.CreateTables"/> instead.
    /// </summary>
    public static IReadOnlyList<string> AllCreateTableStatements { get; } = new[]
    {
        CreateLibInfo,
        CreateRefSpectra,
        CreateModifications,
        CreateRefSpectraPeaks,
        CreateProteins,
        CreateRefSpectraProteins,
        CreateRefSpectraPeakAnnotations,
        CreateSpectrumSourceFiles,
        CreateScoreTypes,
        CreateIonMobilityTypes,
    };

    /// <summary>
    /// All CREATE INDEX statements that <see cref="BlibMaker.Commit"/> emits, in the
    /// same order as cpp <c>BlibMaker::commit</c>.
    /// </summary>
    public static IReadOnlyList<string> AllIndexStatements { get; } = new[]
    {
        CreateIdxPeptide,
        CreateIdxPeptideMod,
        CreateIdxRefIdPeaks,
        CreateIdxRefIdPeakAnnotations,
        CreateIdxMoleculeName,
        CreateIdxInChiKey,
    };

    /// <summary>
    /// Names of the indexes dropped in <c>BlibMaker::init</c> before appending to an
    /// existing library (to speed up bulk inserts). cpp BlibMaker.cpp:219-224.
    /// </summary>
    public static IReadOnlyList<string> AllIndexNames { get; } = new[]
    {
        "idxPeptide",
        "idxPeptideMod",
        "idxRefIdPeaks",
        "idxRefIdPeakAnnotations",
        "idxMoleculeName",
        "idxInChiKey",
    };

    // --- builders for tables that splice in runtime / shared schema --------------------------

    private static string BuildRefSpectraCreate()
    {
        // cpp BlibMaker.cpp:358-383 — fixed prefix, then SmallMolMetadata::sql_col_decls(),
        // then the fileID/score suffix. Preserve the exact whitespace from cpp.
        var sb = new StringBuilder();
        sb.Append(
            "CREATE TABLE RefSpectra ( -- spectrum metadata - actual mz/intensity pairs in RefSpectraPeaks\n" +
            "id INTEGER primary key autoincrement not null, -- lookup key for RefSpectraPeaks\n" +
            "peptideSeq VARCHAR(150), -- unmodified peptide sequence, can be left blank for small molecule use\n" +
            "precursorMZ REAL, -- mz of the precursor that produced this spectrum\n" +
            "precursorCharge INTEGER, -- should agree with adduct if provided\n" +
            "peptideModSeq VARCHAR(200), -- modified peptide sequence, can be left blank for small molecule use\n" +
            "prevAA CHAR(1), -- position of peptide in its parent protein (can be left blank)\n" +
            "nextAA CHAR(1),  -- position of peptide in its parent protein (can be left blank)\n" +
            "copies INTEGER, -- number of copies this spectrum was chosen from if it is in a filtered library\n" +
            "numPeaks INTEGER, -- number of peaks, should agree with corresponding entry in RefSpectraPeaks\n" +
            "ionMobility REAL, -- ion mobility value, if known (see ionMobilityType for units)\n" +
            "collisionalCrossSectionSqA REAL, -- precursor CCS in square Angstroms for ion mobility, if known\n" +
            "ionMobilityHighEnergyOffset REAL, -- ion mobility value increment for fragments (see ionMobilityType for units)\n" +
            "ionMobilityType TINYINT, -- ion mobility units (required if ionMobility is used, see IonMobilityTypes table for key)\n" +
            "retentionTime REAL, -- chromatographic retention time in minutes, if known\n" +
            "startTime REAL, -- start retention time in minutes, if known\n" +
            "endTime REAL, -- end retention time in minutes, if known\n" +
            "totalIonCurrent REAL, -- total ion current of spectrum\n");
        sb.Append(SmallMolMetadata.SqlColumnDeclarations());
        sb.Append(
            "fileID INTEGER, -- index into SpectrumSourceFiles table for source file information\n" +
            "SpecIDinFile VARCHAR(256), -- original spectrum label, id, or description in source file\n" +
            "score REAL, -- spectrum score, typically a probability score (see scoreType)\n" +
            "scoreType TINYINT -- spectrum score type, see ScoreTypes table for meaning\n" +
            ")");
        return sb.ToString();
    }
}
