// Port of pwiz_tools/BiblioSpec/src/BlibMaker.cpp schema definitions
//
// The cpp BlibMaker::createTables / createTable methods build CREATE TABLE strings
// inline. Here we extract them into reusable constants so callers (BlibMaker / tests
// / Skyline / the BlibWriter every tool shares) can ask for "the canonical version-11
// schema" without instantiating a BlibMaker. Numeric values and string contents must
// remain byte-for-byte identical to cpp because Skyline's schema-doc generator and
// downstream BlibUtil readers compare against these literals.
//
// The one definition of the schema: the BiblioSpec port's BlibMaker (BlibBuild, BlibFilter),
// Osprey and CarafeSharp all write through BlibWriter with it, and the port's
// SmallMolMetadata.SqlColumns are the small-molecule columns here. Until 2026-10-16 the port
// kept its own copy, held identical by a test, so that it took no dependency on this project.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace pwiz.BiblioSpec.IO
{
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

        /// <summary>cpp <c>MINOR_VERSION_CURRENT</c> - also the schema version (BlibMaker.h:53).</summary>
        public const int CurrentMinorVersion = 11;

        /// <summary>cpp <c>MIN_VERSION_WORKFLOW</c> - schema with SpectrumSourceFile.workflowType.</summary>
        public const int MinVersionWorkflow = 11;

        /// <summary>cpp <c>MIN_VERSION_TIC</c> - schema with RefSpectra.totalIonCurrent.</summary>
        public const int MinVersionTic = 10;

        /// <summary>cpp <c>MIN_VERSION_PROTEINS</c> - schema with Proteins / RefSpectraProteins tables.</summary>
        public const int MinVersionProteins = 9;

        /// <summary>cpp <c>MIN_VERSION_RT_BOUNDS</c> - schema with RefSpectra.startTime / endTime.</summary>
        public const int MinVersionRtBounds = 8;

        /// <summary>cpp <c>MIN_VERSION_PEAK_ANNOT</c> - schema with RefSpectraPeakAnnotations.</summary>
        public const int MinVersionPeakAnnot = 7;

        /// <summary>cpp <c>MIN_VERSION_IMS_UNITS</c> - schema generalising ion mobility to value+type.</summary>
        public const int MinVersionImsUnits = 6;

        /// <summary>cpp <c>MIN_VERSION_SMALL_MOL</c> - schema with small-molecule columns.</summary>
        public const int MinVersionSmallMol = 5;

        /// <summary>cpp <c>MIN_VERSION_CCS</c> - schema with collisionalCrossSectionSqA.</summary>
        public const int MinVersionCcs = 4;

        /// <summary>cpp <c>MIN_VERSION_IMS_HEOFF</c> - schema with ion-mobility high-energy offset.</summary>
        public const int MinVersionImsHeoff = 3;

        /// <summary>cpp <c>MIN_VERSION_IMS</c> - schema with ion mobility column.</summary>
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

        // --- table names ---------------------------------------------------------------------------
        // The one spelling of each table, for the writer's and reader's probes and statements and
        // for a tool asking the reader about a table; the CREATE statements below spell them again
        // as the literal SQL BiblioSpec has always written.

        public const string TABLE_LIB_INFO = "LibInfo";
        public const string TABLE_REF_SPECTRA = "RefSpectra";
        public const string TABLE_MODIFICATIONS = "Modifications";
        public const string TABLE_REF_SPECTRA_PEAKS = "RefSpectraPeaks";
        public const string TABLE_PROTEINS = "Proteins";
        public const string TABLE_REF_SPECTRA_PROTEINS = "RefSpectraProteins";
        public const string TABLE_REF_SPECTRA_PEAK_ANNOTATIONS = "RefSpectraPeakAnnotations";
        public const string TABLE_SPECTRUM_SOURCE_FILES = "SpectrumSourceFiles";
        public const string TABLE_SCORE_TYPES = "ScoreTypes";
        public const string TABLE_ION_MOBILITY_TYPES = "IonMobilityTypes";
        /// <summary>The per-observation table of a non-redundant library; a redundant library has none.</summary>
        public const string TABLE_RETENTION_TIMES = "RetentionTimes";
        /// <summary>Optional: the library's key-value metadata (<see cref="BlibMetadata"/>).</summary>
        public const string TABLE_LIB_METADATA = "LibMetadata";
        /// <summary>Optional: the decoy and entrapment design of a library that supplies its own decoys.</summary>
        public const string TABLE_DECOY_PAIRS = "DecoyPairs";

        /// <summary>
        /// Every table of the format, the optional ones included, in creation order: what a
        /// writer making a library anew in a file drops first.
        /// </summary>
        public static IReadOnlyList<string> AllTableNames { get; } = new[]
        {
            TABLE_LIB_INFO, TABLE_REF_SPECTRA, TABLE_MODIFICATIONS, TABLE_REF_SPECTRA_PEAKS, TABLE_PROTEINS,
            TABLE_REF_SPECTRA_PROTEINS, TABLE_REF_SPECTRA_PEAK_ANNOTATIONS, TABLE_SPECTRUM_SOURCE_FILES, TABLE_SCORE_TYPES,
            TABLE_ION_MOBILITY_TYPES, TABLE_RETENTION_TIMES, TABLE_LIB_METADATA, TABLE_DECOY_PAIRS,
        };

        // --- small-molecule columns ---------------------------------------------------------------

        /// <summary>
        /// The small-molecule metadata columns of <c>RefSpectra</c>, each with its comment.
        /// Matches cpp <c>SmallMolMetadata::DEFINE_SQL_COLS_AND_COMMENTS</c>; order matters
        /// because cpp uses it for sorting.
        /// </summary>
        public static readonly (string Name, string Description)[] SmallMoleculeColumns =
        {
            ("moleculeName", "precursor molecule's name (not needed for peptides)"),
            ("chemicalFormula", "precursor molecule's neutral formula, may include value for unexplained mass e.g. H84C44N12O13[+3.3122] (not needed for peptides)"),
            ("precursorAdduct", "ionizing adduct e.g. [M+Na], [2M-H2O+2H] etc (not needed for peptides)"),
            ("inchiKey", "molecular identifier for structure retrieval (not needed for peptides)"),
            ("otherKeys", "alternative molecular identifiers for structure retrieval, tab separated name:value pairs e.g. cas:58-08-2\\thmdb:01847 (not needed for peptides)"),
        };

        /// <summary>The small-molecule column names, in schema order.</summary>
        public static IEnumerable<string> SmallMoleculeColumnNames
        {
            get { return SmallMoleculeColumns.Select(c => c.Name); }
        }

        /// <summary>
        /// cpp <c>sql_col_decls()</c> equivalent: the small-molecule column declarations with
        /// comments, one per line, as spliced into <see cref="CreateRefSpectra"/>.
        /// </summary>
        public static string SmallMoleculeColumnDeclarations()
        {
            var sb = new StringBuilder();
            foreach (var (name, description) in SmallMoleculeColumns)
                sb.Append(name).Append(' ').Append("VARCHAR(128)").Append(", -- ").Append(description).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// cpp <c>sql_col_names_csv()</c> equivalent: leading-comma list of the small-molecule
        /// column names usable after an existing column list (e.g. <c>"id, peptide" + SmallMoleculeColumnNamesCsv()</c>).
        /// </summary>
        public static string SmallMoleculeColumnNamesCsv()
        {
            var sb = new StringBuilder();
            foreach (var (name, _) in SmallMoleculeColumns)
                sb.Append(", ").Append(name);
            return sb.ToString();
        }

        // --- table create statements -------------------------------------------------------------

        /// <summary>
        /// CREATE TABLE for <c>LibInfo</c>. Mirrors cpp BlibMaker.cpp:339-345 - note the
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
        /// <see cref="SmallMoleculeColumnDeclarations"/> to match cpp BlibMaker.cpp:377.
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

        /// <summary>
        /// CREATE TABLE for <c>RetentionTimes</c>, the per-observation table of a non-redundant
        /// library: cpp BlibFilter.cpp's <c>createTables</c> (the redundant library has no such
        /// table; every spectrum is its own observation there).
        /// </summary>
        public const string CreateRetentionTimes =
            "CREATE TABLE RetentionTimes (RefSpectraID INTEGER, " +
            "RedundantRefSpectraID INTEGER, " +
            "SpectrumSourceID INTEGER, " +
            "ionMobility REAL, " +
            "collisionalCrossSectionSqA REAL, " +
            "ionMobilityHighEnergyOffset REAL, " +
            "ionMobilityType TINYINT, " +
            "retentionTime REAL, " +
            "startTime REAL, " +
            "endTime REAL, " +
            "score REAL, " +
            "bestSpectrum INTEGER, " +
            "FOREIGN KEY(RefSpectraID) REFERENCES RefSpectra(id) )";

        // --- seed rows -----------------------------------------------------------------------------

        /// <summary>
        /// BiblioSpec's <c>ScoreTypes</c> rows, in id order: the score's name and how to read it
        /// (<c>PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT</c>, <c>..._INCORRECT</c> or
        /// <c>NOT_A_PROBABILITY_VALUE</c>). The id is the port's <c>PsmScoreType</c> ordinal and
        /// <c>RefSpectra.scoreType</c>. cpp BlibUtils.cpp:37 and BlibMaker.cpp:441-446.
        /// </summary>
        public static readonly (string ScoreType, string ProbabilityType)[] ScoreTypes =
        {
            ("UNKNOWN", "NOT_A_PROBABILITY_VALUE"),
            ("PERCOLATOR QVALUE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("PEPTIDE PROPHET SOMETHING", "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT"),
            ("SPECTRUM MILL", "NOT_A_PROBABILITY_VALUE"),
            ("IDPICKER FDR", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("MASCOT IONS SCORE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("TANDEM EXPECTATION VALUE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("PROTEIN PILOT CONFIDENCE", "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT"),
            ("SCAFFOLD SOMETHING", "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT"),
            ("WATERS MSE PEPTIDE SCORE", "NOT_A_PROBABILITY_VALUE"),
            ("OMSSA EXPECTATION SCORE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("PROTEIN PROSPECTOR EXPECTATION SCORE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("SEQUEST XCORR", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("MAXQUANT SCORE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("MORPHEUS SCORE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("MSGF+ SCORE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("PEAKS CONFIDENCE SCORE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("BYONIC SCORE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("PEPTIDE SHAKER CONFIDENCE", "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT"),
            ("GENERIC Q-VALUE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT"),
            ("HARDKLOR IDOTP", "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT"),
        };

        /// <summary>
        /// BiblioSpec's <c>IonMobilityTypes</c> rows, in id order; the id is the port's
        /// <c>IonMobilityType</c> ordinal and <c>RefSpectra.ionMobilityType</c>. cpp
        /// BlibUtils.cpp and BlibMaker.cpp:456-460.
        /// </summary>
        public static readonly string[] IonMobilityTypes =
        {
            "none", "driftTime(msec)", "inverseK0(Vsec/cm^2)", "compensation(V)",
        };

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

        // --- optional tables a writing tool adds (not in AllCreateTableStatements) --------------

        /// <summary>
        /// CREATE TABLE for <c>LibMetadata</c>: single-row text values about the library as a
        /// whole, under keys namespaced by the writing tool (<c>osprey.version</c>,
        /// <c>carafe.command</c>) plus a small unprefixed set every tool honors
        /// (<c>library_contract</c>). Every tool that makes or transforms a library records its
        /// command line under <c>&lt;tool&gt;.command</c>. Optional: a reader tests for the
        /// table, not the schema version. Added 2026-10-15 in place of Osprey's
        /// <c>OspreyMetadata</c>.
        /// </summary>
        public const string CreateLibMetadata = "CREATE TABLE LibMetadata (key TEXT PRIMARY KEY, value TEXT)";

        /// <summary>
        /// CREATE TABLE for <c>DecoyPairs</c>: the decoy and entrapment design of a library that
        /// supplies its own decoys, one row per spectrum in the design. <c>IsDecoy</c> and
        /// <c>IsEntrapment</c> give a row's side and partition (the four kinds of an FDRBench
        /// pairing manifest: target, p_target, decoy, p_decoy); <c>PairingID</c> is the manifest's
        /// <c>peptide_pair_index</c>, shared by a target, its entrapment target and both their
        /// decoys, which is what pairs an entrapment peptide with its original target;
        /// <c>TargetRefSpectraID</c> is a decoy's target spectrum at the same charge, null on a
        /// target or a decoy with no target; <c>Method</c> names how the row's sequence was made
        /// from the row it was made from - <c>reverse</c> or <c>cycle</c> on a decoy,
        /// <c>shuffle</c> or <c>foreign</c> on an entrapment target - and is null on an original
        /// target. Written by CarafeSharp and by
        /// Osprey's library export, read by Osprey, which pairs library decoys and entrapment
        /// from it with no manifest. Optional, as above.
        /// </summary>
        public const string CreateDecoyPairs =
            "CREATE TABLE DecoyPairs (RefSpectraID INTEGER NOT NULL PRIMARY KEY, IsDecoy INTEGER NOT NULL, " +
            "IsEntrapment INTEGER NOT NULL, PairingID INTEGER NOT NULL, " +
            "TargetRefSpectraID INTEGER REFERENCES RefSpectra(id), Method TEXT)";

        /// <summary>The index a reader joining a target to its decoy needs; the primary key serves the other direction.</summary>
        public const string IdxDecoyPairsTargetName = "idx_DecoyPairs_TargetRefSpectraID";
        public const string CreateIdxDecoyPairsTarget = "CREATE INDEX " + IdxDecoyPairsTargetName + " ON DecoyPairs(TargetRefSpectraID)";

        // --- optional columns (BlibOptionalColumn) ---------------------------------------------
        // Not versioned: adding one does not change CurrentMinorVersion, and readers test for the
        // column instead of the version. Each is written only by a source whose value is known to
        // be experiment-wide (global): DIA-NN's Global.Q.Value and Global.PG.Q.Value, Proteome
        // Discoverer's peptide groups and master proteins, Osprey's experiment-level FDR.

        /// <summary>Global (experiment-wide) precursor q-value.</summary>
        public static readonly BlibOptionalColumn PrecursorQValue = new BlibOptionalColumn(TABLE_REF_SPECTRA, "PrecursorQValue", "REAL");

        /// <summary>Global precursor posterior error probability.</summary>
        public static readonly BlibOptionalColumn PrecursorPep = new BlibOptionalColumn(TABLE_REF_SPECTRA, "PrecursorPep", "REAL");

        /// <summary>Global peptide q-value, from a peptide-level competition: one value per modified sequence.</summary>
        public static readonly BlibOptionalColumn PeptideQValue = new BlibOptionalColumn(TABLE_REF_SPECTRA, "PeptideQValue", "REAL");

        /// <summary>Global peptide posterior error probability.</summary>
        public static readonly BlibOptionalColumn PeptidePep = new BlibOptionalColumn(TABLE_REF_SPECTRA, "PeptidePep", "REAL");

        /// <summary>Global protein-group q-value; a <c>Proteins</c> row with it is a protein group.</summary>
        public static readonly BlibOptionalColumn ProteinQValue = new BlibOptionalColumn(TABLE_PROTEINS, "QValue", "REAL");

        /// <summary>Global protein-group posterior error probability.</summary>
        public static readonly BlibOptionalColumn ProteinPep = new BlibOptionalColumn(TABLE_PROTEINS, "Pep", "REAL");

        /// <summary>The optional <c>RefSpectra</c> columns, in the order they are written.</summary>
        public static IReadOnlyList<BlibOptionalColumn> OptionalRefSpectraColumns { get; } = new[]
        {
            PrecursorQValue, PrecursorPep, PeptideQValue, PeptidePep,
        };

        /// <summary>The optional <c>Proteins</c> columns, in the order they are written.</summary>
        public static IReadOnlyList<BlibOptionalColumn> OptionalProteinsColumns { get; } = new[]
        {
            ProteinQValue, ProteinPep,
        };

        /// <summary>
        /// All CREATE TABLE statements that <c>BlibMaker.CreateTables</c> emits, in the
        /// same order as cpp <c>BlibMaker::createTables</c>. The LibInfo INSERT is NOT in this
        /// list because it requires runtime values (LSID, ctime, version) - callers that need
        /// the full bootstrap should call <c>BlibMaker.CreateTables</c> instead.
        /// <see cref="CreateRetentionTimes"/> is not in it either: a redundant library has no
        /// such table.
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
        /// The statements that create an empty library and seed it, in the order
        /// <see cref="AllCreateTableStatements"/> lists the tables: the <c>LibInfo</c> row under
        /// <paramref name="lsid"/> and <paramref name="createTime"/> (<see cref="FormatCreateTime"/>),
        /// then one <c>ScoreTypes</c> row per <see cref="ScoreTypes"/> entry and one
        /// <c>IonMobilityTypes</c> row per <see cref="IonMobilityTypes"/> entry, as literal SQL for
        /// a documentation dump (<c>blibbuild -d</c>) or a tool creating a library by hand. The
        /// tables and seeds are the ones <see cref="BlibWriter.CreateSchema"/> writes.
        /// </summary>
        public static IReadOnlyList<string> CreateScript(string lsid, string createTime)
        {
            if (lsid == null)
                throw new ArgumentNullException(nameof(lsid));
            if (createTime == null)
                throw new ArgumentNullException(nameof(createTime));
            var script = new List<string>();
            foreach (string create in AllCreateTableStatements)
            {
                script.Add(create);
                if (create == CreateLibInfo)
                {
                    // -1 spectra and revision 0: a library not yet counted, as BiblioSpec has always begun one
                    script.Add(string.Format(CultureInfo.InvariantCulture, "INSERT INTO LibInfo values('{0}','{1}',{2},{3},{4})",
                        EscapeApostrophes(lsid), EscapeApostrophes(createTime), -1, CurrentMajorVersion, CurrentMinorVersion));
                }
                else if (create == CreateScoreTypes)
                {
                    for (int i = 0; i < ScoreTypes.Length; i++)
                    {
                        script.Add(string.Format(CultureInfo.InvariantCulture,
                            "INSERT INTO ScoreTypes(id, scoreType, probabilityType) VALUES({0}, '{1}', '{2}')",
                            i, ScoreTypes[i].ScoreType, ScoreTypes[i].ProbabilityType));
                    }
                }
                else if (create == CreateIonMobilityTypes)
                {
                    for (int i = 0; i < IonMobilityTypes.Length; i++)
                    {
                        script.Add(string.Format(CultureInfo.InvariantCulture,
                            "INSERT INTO IonMobilityTypes(id, ionMobilityType) VALUES({0}, '{1}')", i, IonMobilityTypes[i]));
                    }
                }
            }
            return script;
        }

        /// <summary>A text value as a SQL literal's body: each apostrophe doubled.</summary>
        private static string EscapeApostrophes(string value)
        {
            return value.Replace("'", "''");
        }

        /// <summary>
        /// All CREATE INDEX statements that <c>BlibMaker.Commit</c> emits, in the
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

        // --- LibInfo values ----------------------------------------------------------------------

        /// <summary>The LSID of a non-redundant library named <paramref name="libraryName"/>, as BiblioSpec writes it.</summary>
        public static string NonRedundantLsid(string libraryName)
        {
            return "urn:lsid:proteome.gs.washington.edu:spectral_library:bibliospec:nr:" + libraryName;
        }

        /// <summary>
        /// <paramref name="time"/> in ctime() format, as <c>LibInfo.createTime</c> holds it:
        /// <c>Thu Nov  3 17:02:18 2017</c>, with the day of the month space-padded.
        /// </summary>
        public static string FormatCreateTime(DateTime time)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:ddd} {0:MMM} {1,2} {0:HH:mm:ss yyyy}", time, time.Day);
        }

        // --- builders for tables that splice in runtime / shared schema --------------------------

        private static string BuildRefSpectraCreate()
        {
            // cpp BlibMaker.cpp:358-383 - fixed prefix, then SmallMolMetadata::sql_col_decls(),
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
            sb.Append(SmallMoleculeColumnDeclarations());
            sb.Append(
                "fileID INTEGER, -- index into SpectrumSourceFiles table for source file information\n" +
                "SpecIDinFile VARCHAR(256), -- original spectrum label, id, or description in source file\n" +
                "score REAL, -- spectrum score, typically a probability score (see scoreType)\n" +
                "scoreType TINYINT -- spectrum score type, see ScoreTypes table for meaning\n" +
                ")");
            return sb.ToString();
        }
    }
}
