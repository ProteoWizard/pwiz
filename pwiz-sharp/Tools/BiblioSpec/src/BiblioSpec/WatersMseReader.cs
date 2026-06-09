// Port of pwiz_tools/BiblioSpec/src/WatersMseReader.{h,cpp}
//
// Parses Waters MSE final_fragment.csv files. Each row in the CSV is one product-ion peak;
// rows that share the same (sequence, charge, m/z, retention time) are aggregated into a
// single MsePSM with a peak list. The reader itself doubles as the spectrum-file reader —
// the spectra come straight out of the PSM rather than an external file.

using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Extends the standard <see cref="PSM"/> with the per-spectrum data Waters MSE rows carry.
/// </summary>
/// <remarks>cpp parity: WatersMseReader.h:36 <c>struct MsePSM</c>.</remarks>
internal sealed class MsePSM : PSM
{
    /// <summary>Precursor m/z.</summary>
    public double Mz { get; set; }

    /// <summary>
    /// A non-integer value that reveals relative abundance of charge states. cpp parity:
    /// WatersMseReader.h:38. For example, "2.74" means a mix of 2 and 3, with most being 3.
    /// </summary>
    public double PrecursorNonIntegerCharge { get; set; }

    /// <summary>Precursor ion mobility (drift time in msec after pusher-interval scaling).</summary>
    public double PrecursorIonMobility { get; set; }

    /// <summary>Retention time (minutes).</summary>
    public double RetentionTime { get; set; }

    /// <summary>Product-ion m/z list (parallel to <see cref="Intensities"/>).</summary>
    public List<double> Mzs { get; } = new();

    /// <summary>Product-ion intensities.</summary>
    public List<double> Intensities { get; } = new();

    /// <summary>Per-product-ion ion mobilities (pusher-interval scaled).</summary>
    public List<double> ProductIonMobilities { get; } = new();

    /// <summary>If false, this PSM was marked invalid during parsing and should not be added to the library.</summary>
    public bool Valid { get; set; } = true;

    /// <summary>
    /// Reset all fields to their default empty / zero values. Mirrors cpp WatersMseReader.h:72 <c>clear()</c>.
    /// </summary>
    public override void Clear()
    {
        base.Clear();
        Mz = 0;
        PrecursorIonMobility = 0;
        PrecursorNonIntegerCharge = 0;
        RetentionTime = 0;
        Mzs.Clear();
        Intensities.Clear();
        ProductIonMobilities.Clear();
        Valid = true;
    }

    /// <summary>Copy all MsePSM-specific fields (and the base PSM fields) from <paramref name="other"/>.</summary>
    public void CopyFrom(MsePSM other)
    {
        Clear();

        // Base PSM fields.
        Charge = other.Charge;
        UnmodSeq = other.UnmodSeq;
        ModifiedSeq = other.ModifiedSeq;
        foreach (var mod in other.Mods)
            Mods.Add(mod);
        SpecKey = other.SpecKey;
        SpecIndex = other.SpecIndex;
        Score = other.Score;
        IonMobility = other.IonMobility;
        IonMobilityType = other.IonMobilityType;
        Ccs = other.Ccs;
        SpecName = other.SpecName;
        foreach (var p in other.Proteins)
            Proteins.Add(p);

        // MsePSM additions.
        Mz = other.Mz;
        PrecursorNonIntegerCharge = other.PrecursorNonIntegerCharge;
        PrecursorIonMobility = other.PrecursorIonMobility;
        RetentionTime = other.RetentionTime;
        Mzs.AddRange(other.Mzs);
        Intensities.AddRange(other.Intensities);
        ProductIonMobilities.AddRange(other.ProductIonMobilities);
        Valid = other.Valid;
    }
}

/// <summary>
/// IComparer for MsePSM matching cpp's <c>compMsePsm</c> predicate (WatersMseReader.h:88).
/// Orders by (unmodSeq, charge, mz, retentionTime, precursorIonMobility, then small-mol fields).
/// </summary>
internal sealed class MsePsmComparer : IComparer<MsePSM>
{
    public static readonly MsePsmComparer Instance = new();

    public int Compare(MsePSM? x, MsePSM? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int c = string.CompareOrdinal(x.UnmodSeq, y.UnmodSeq);
        if (c != 0) return c;
        c = x.Charge.CompareTo(y.Charge);
        if (c != 0) return c;
        c = x.Mz.CompareTo(y.Mz);
        if (c != 0) return c;
        c = x.RetentionTime.CompareTo(y.RetentionTime);
        if (c != 0) return c;
        c = x.PrecursorIonMobility.CompareTo(y.PrecursorIonMobility);
        if (c != 0) return c;
        c = string.CompareOrdinal(x.SmallMolMetadata.ChemicalFormula, y.SmallMolMetadata.ChemicalFormula);
        if (c != 0) return c;
        c = string.CompareOrdinal(x.SmallMolMetadata.InchiKey, y.SmallMolMetadata.InchiKey);
        if (c != 0) return c;
        c = string.CompareOrdinal(x.SmallMolMetadata.PrecursorAdduct, y.SmallMolMetadata.PrecursorAdduct);
        return c;
    }
}

/// <summary>
/// Parses Waters MSE <c>final_fragment.csv</c> files. Each CSV row contains one product-ion peak
/// plus the (denormalized) precursor info; rows sharing the same precursor are coalesced into
/// one library entry.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::WatersMseReader</c> at
/// <c>pwiz_tools/BiblioSpec/src/WatersMseReader.{h,cpp}</c>.</para>
/// <para>The cpp reader also implements <c>SpecFileReader</c> directly (since the spectra live
/// on the parsed PSMs, not in a separate file). The C# port mirrors that by setting
/// <see cref="BuildParser.SpecReader"/> to an inner <see cref="MseSpecFileReader"/> that
/// pulls peaks straight off the <see cref="MsePSM"/>.</para>
/// </remarks>
public sealed class WatersMseReader : BuildParser
{
    private readonly string _csvName;
    private readonly double _scoreThreshold;
    private int _lineNum;
    private MsePSM? _curMsePsm;

    // cpp parity: WatersMseReader.h:236 — std::set<MsePSM*, compMsePsm> for unique-PSM dedup.
    // SortedSet matches the cpp set's iteration-in-sorted-order semantics, which the .check
    // golden depends on (PSMs are emitted in unmodSeq ascending order).
    private readonly SortedSet<MsePSM> _uniquePsms = new(MsePsmComparer.Instance);

    // Column layout discovered in the header row. -1 == not present.
    private readonly List<WColumnTranslator> _targetColumns = new();
    private readonly List<WColumnTranslator> _optionalColumns = new();
    private int _numColumns;

    // cpp parity: WatersMseReader.h:242 — pusher interval (msec/bin) used to translate
    // raw drift-bin counts to millisec. -1 means unset.
    private double _pusherInterval = -1;

    /// <summary>Returns true if <paramref name="path"/> ends with <c>final_fragment.csv</c>.</summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith("final_fragment.csv", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// cpp parity: WatersMseReader.cpp:45 placeholder value used in the mods dictionary for
    /// glycosylation entries — the real delta gets filled in from each line's precursorMass-minMass.
    /// </summary>
    private const double GlycolMass = double.MaxValue;

    // cpp parity: WatersMseReader.cpp:60-146 — hardcoded modification map. Inserted in the
    // same order as the cpp file (the actual dictionary uses ordinal-string sorting for
    // lookup, but ordering at insert time doesn't affect a Dictionary<>). The longest-key-
    // first match is implemented via _modsByLongestKey.
    private static readonly Dictionary<string, double> _modsRaw = new(StringComparer.Ordinal)
    {
        ["12C d0"] = 227.1270,
        ["13C"] = 6.020129,
        ["13C N15"] = 10.008269,
        ["13C d9"] = 236.1572,
        ["1H d0"] = 442.2250,
        ["2H d8"] = 450.2752,
        ["Acetyl"] = 42.010565,
        ["Amidation"] = -0.984016,
        ["Biotin"] = 226.077598,
        ["C-Mannosyl"] = 162.0538,
        ["Carbamidomethyl"] = 57.021464,
        ["Carbamyl"] = 43.005814,
        ["Carboxymethyl"] = 58.005479,
        ["Deamidation"] = 0.984016,
        ["Dehydration"] = -18.010565,
        ["Dimethyl"] = 28.0313,
        ["DUPLEX_TANDEM_MASS_TAG126"] = 225.15584,
        ["DUPLEX_TANDEM_MASS_TAG127"] = 225.15584,
        ["Farnesyl"] = 204.187801,
        ["Flavin-adenine"] = 783.141486,
        ["Formyl"] = 27.994915,
        ["Gamma-carboxyglutamic"] = 43.98980,
        ["Geranyl-geranyl"] = 272.250401,
        ["Glycation"] = 162.052824,
        ["Hydroxyl"] = 15.994915,
        ["ICAT-C"] = 227.12698,
        ["ICAT-C13C(9)"] = 236.1518,
        ["ICAT-D"] = 442.2250,
        ["ICAT-D 2H(8)"] = 450.2752,
        ["ICAT-G"] = 486.25122,
        ["ICAT-G 2H(8)"] = 494.30142,
        ["ICAT-H"] = 345.0979,
        ["ICAT-H13(6)"] = 351.11804,
        ["Isobaric 8plex 113"] = 304.2117,
        ["Isobaric 8plex 114"] = 304.2117,
        ["Isobaric 8plex 115"] = 304.2117,
        ["Isobaric 8plex 116"] = 304.2117,
        ["Isobaric 8plex 117"] = 304.2117,
        ["Isobaric 8plex 118"] = 304.2117,
        ["Isobaric 8plex 119"] = 304.2117,
        ["Isobaric 8plex 121"] = 304.2117,
        ["Isobaric 114"] = 144.105863,
        ["Isobaric 115"] = 144.059563,
        ["Isobaric 116"] = 144.102063,
        ["Isobaric 117"] = 144.102063,
        ["Lipoyl"] = 188.032956,
        ["Methyl"] = 14.015650,
        ["Myristoyl"] = 210.198366,
        ["N-Glycosylation"] = GlycolMass,
        ["NATIVE_TANDEM_MASS_TAG126"] = 224.15248,
        ["NATIVE_TANDEM_MASS_TAG127"] = 224.15248,
        ["NIPCAM"] = 99.068414,
        ["O18"] = 4.008491,
        ["O18 label at both C-terminal oxygens"] = 4.008491,
        ["O-GlcNAc"] = 203.0794,
        ["O-Glycosylation"] = GlycolMass,
        ["Oxidation"] = 15.994915,
        ["Palmitoyl"] = 238.229666,
        ["Phosphopantetheine"] = 340.085794,
        ["Phosphoryl"] = 79.966331,
        ["Propionamide"] = 71.037114,
        ["Pyridoxal"] = 229.014009,
        ["Pyrrolidone"] = -17.0265,
        ["S-pyridylethyl"] = 105.057849,
        ["SILAC 13C(1) 2H3"] = 4.022185,
        ["SILAC 13C(3)"] = 3.010064,
        ["SILAC 13C(3) 15N(1)"] = 3.98814,
        ["SILAC 13C(4) 15N(1)"] = 5.010454,
        ["SILAC 13C(5)"] = 5.016774,
        ["SILAC 13C(6)"] = 6.020129,
        ["SILAC 13C(6) 15N(2)"] = 8.014199,
        ["SILAC 13C(6) 15N(4)"] = 10.008269,
        ["SILAC 13C(8) 15N(2)"] = 10.020909,
        ["SILAC 13C(9)"] = 9.030193,
        ["SILAC 13C(9) 15N(1)"] = 10.027228,
        ["SILAC 15N(2) 2H(9)"] = 11.050561,
        ["SILAC 15N(4)"] = 3.98814,
        ["SIXPLEX_TANDEM_MASS_TAG126"] = 229.16293,
        ["SIXPLEX_TANDEM_MASS_TAG127"] = 229.16293,
        ["SIXPLEX_TANDEM_MASS_TAG128"] = 229.16293,
        ["SIXPLEX_TANDEM_MASS_TAG129"] = 229.16293,
        ["SIXPLEX_TANDEM_MASS_TAG130"] = 229.16293,
        ["SIXPLEX_TANDEM_MASS_TAG131"] = 229.16293,
        ["SMA"] = 127.063329,
        ["Sulfo"] = 79.9568,
        ["Trimethyl"] = 42.04695,
    };

    // cpp parity: WatersMseReader.cpp:623 uses reverse iteration over a std::map, which
    // in ordinal-ascending sort happens to give longest-prefix-first for the SILAC keys
    // ("SILAC 13C(6) 15N(4)" sorts AFTER "SILAC 13C(6)" because the space then '1' at
    // position 11 sorts higher than nothing at position 11). We replicate by sorting the
    // keys in ordinal-descending order so the prefix-match loop hits the longer key first.
    private static readonly KeyValuePair<string, double>[] _modsByReverseKey =
        SortModsByReverseKey();

    private static KeyValuePair<string, double>[] SortModsByReverseKey()
    {
        var list = new List<KeyValuePair<string, double>>(_modsRaw);
        list.Sort((a, b) => string.CompareOrdinal(b.Key, a.Key));
        return list.ToArray();
    }

    /// <summary>
    /// Construct a WatersMseReader bound to <paramref name="builder"/> and the file at
    /// <paramref name="csvName"/>.
    /// </summary>
    public WatersMseReader(BlibBuilder builder, string csvName, ProgressIndicator? parentProgress)
        : base(builder, csvName, parentProgress)
    {
        Verbosity.Debug("Creating WatersMseReader.");

        _csvName = csvName;
        _scoreThreshold = GetScoreThreshold(BuildInput.Mse);
        _lineNum = 1;

        // cpp parity: WatersMseReader.cpp:56 — record the csv path as the spec-file, no
        // existence check (the spectra come from this same file, not a separate one).
        SetSpecFileName(csvName, checkFile: false);

        if (builder.ForcedPusherInterval > 0)
        {
            _pusherInterval = builder.ForcedPusherInterval;
            Verbosity.Debug(
                $"Using forced pusher interval of {_pusherInterval.ToString(CultureInfo.InvariantCulture)}.");
        }

        // cpp parity: WatersMseReader.cpp:182 — point the spec reader at ourselves.
        // The inner MseSpecFileReader pulls peaks from each MsePSM directly.
        SpecReader = new MseSpecFileReader();

        InitTargetColumns();
    }

    /// <summary>Score types this reader produces. cpp parity: WatersMseReader.cpp:260.</summary>
    public override IList<PsmScoreType> GetScoreTypes() =>
        new[] { PsmScoreType.WatersMsePeptideScore };

    // cpp parity: WatersMseReader.cpp:200 — define the columns the parser looks for.
    private void InitTargetColumns()
    {
        _targetColumns.Add(new WColumnTranslator("peptide.seq", -1, LineEntry.InsertSequence));
        _targetColumns.Add(new WColumnTranslator("peptide.modification", -1, LineEntry.InsertModification));
        _targetColumns.Add(new WColumnTranslator("peptide.score", -1, LineEntry.InsertScore));
        _targetColumns.Add(new WColumnTranslator("precursor.retT", -1, LineEntry.InsertRetentionTime));
        _targetColumns.Add(new WColumnTranslator("precursor.charge", -1, LineEntry.InsertPrecursorNonIntegerCharge));
        _targetColumns.Add(new WColumnTranslator("precursor.z", -1, LineEntry.InsertPrecursorZ));
        _targetColumns.Add(new WColumnTranslator("precursor.mz", -1, LineEntry.InsertPrecursorMz));
        _targetColumns.Add(new WColumnTranslator("product.m_z", -1, LineEntry.InsertFragmentMz));
        _targetColumns.Add(new WColumnTranslator("product.inten", -1, LineEntry.InsertFragmentIntensity));
        _targetColumns.Add(new WColumnTranslator("peptide.Pass", -1, LineEntry.InsertPass));

        _optionalColumns.Add(new WColumnTranslator("precursor.mhp", -1, LineEntry.InsertPrecursorMass));
        _optionalColumns.Add(new WColumnTranslator("minMass", -1, LineEntry.InsertMinMass));
        _optionalColumns.Add(new WColumnTranslator("precursor.Mobility", -1, LineEntry.InsertPrecursorIonMobility));
        _optionalColumns.Add(new WColumnTranslator("product.Mobility", -1, LineEntry.InsertProductIonMobility));

        _numColumns = _targetColumns.Count;
    }

    /// <summary>
    /// Open the file, read header, parse remaining rows, and flush to the library. cpp parity:
    /// WatersMseReader.cpp:239.
    /// </summary>
    public override bool ParseFile()
    {
        Verbosity.Debug("Parsing File.");

        using var reader = new StreamReader(_csvName);

        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            throw new BlibException(true, $"Empty Waters MSE csv file '{_csvName}'.");
        }
        ParseHeader(headerLine);

        Verbosity.Debug("Collecting Psms.");
        CollectPsms(reader);

        // cpp parity: WatersMseReader.cpp:253 — copy unique psms into BuildParser's vector.
        // SortedSet iteration order matches cpp std::set: ascending by MsePsmComparer.
        Psms.Clear();
        foreach (var psm in _uniquePsms)
            Psms.Add(psm);

        BuildTables(PsmScoreType.WatersMsePeptideScore);

        return true;
    }

    // cpp parity: WatersMseReader.cpp:285 — parse the header line to learn column positions.
    private void ParseHeader(string line)
    {
        var tokens = SplitCsv(line);
        int colNumber = 0;
        foreach (var raw in tokens)
        {
            var token = raw;
            for (int i = 0; i < _targetColumns.Count; i++)
            {
                if (string.Equals(token, _targetColumns[i].Name, StringComparison.Ordinal))
                    _targetColumns[i].Position = colNumber;
            }
            for (int i = 0; i < _optionalColumns.Count; i++)
            {
                if (string.Equals(token, _optionalColumns[i].Name, StringComparison.Ordinal))
                    _optionalColumns[i].Position = colNumber;
            }
            colNumber++;
        }

        // cpp parity: WatersMseReader.cpp:309 — fail loudly if any required col is missing.
        foreach (var col in _targetColumns)
        {
            if (col.Position == -1)
                throw new BlibException(false, $"Did not find required column '{col.Name}'.");
        }

        // cpp parity: WatersMseReader.cpp:317 — if the first two optional columns are both
        // present, insert them BEFORE the last required column. The order matters because
        // the parsing loop walks _targetColumns in column-position order, and the optional
        // entries that get appended later (precursor.Mobility / product.Mobility) want to
        // remain in their natural file position. We replicate cpp's insert-at-(end-1).
        if (_optionalColumns[0].Position != -1 && _optionalColumns[1].Position != -1)
        {
            // cpp inserts BOTH precursor.mhp + minMass + precursor.Mobility + product.Mobility
            // (the full optional list) at end-1; but only the first two are guaranteed present
            // here. The cpp code does:
            //   targetColumns_.insert(targetColumns_.end()-1, optionalColumns_.begin(), optionalColumns_.end());
            // which copies ALL optional translators (even ones with Position == -1) into the
            // target list. The later position-based sort then puts -1 first; but the lookup
            // loop checks "did we match THIS target column at THIS file column?" with == on
            // position, so a Position==-1 entry never fires. Mirroring cpp exactly:
            int insertAt = _targetColumns.Count - 1;
            _targetColumns.InsertRange(insertAt, _optionalColumns);
        }
        else
        {
            // cpp parity: WatersMseReader.cpp:322 — if precursor.mhp / minMass are absent
            // but the mobility columns are present, append just those.
            if (_optionalColumns[2].Position != -1)
                _targetColumns.Add(_optionalColumns[2]);
            if (_optionalColumns[3].Position != -1)
                _targetColumns.Add(_optionalColumns[3]);
        }

        // cpp parity: WatersMseReader.cpp:332 — sort target columns by file position.
        _targetColumns.Sort((a, b) => a.Position.CompareTo(b.Position));
    }

    // cpp parity: WatersMseReader.cpp:339 — read CSV body row-by-row and aggregate into MsePSMs.
    private void CollectPsms(StreamReader reader)
    {
        string? line = reader.ReadLine();
        _lineNum++;

        while (line != null)
        {
            int colListIdx = 0;
            int lineColNumber = 0;
            var entry = new LineEntry();

            try
            {
                var tokens = SplitCsv(line);
                foreach (var token in tokens)
                {
                    if (colListIdx < _targetColumns.Count
                        && lineColNumber == _targetColumns[colListIdx].Position)
                    {
                        _targetColumns[colListIdx].Inserter(entry, token);
                        colListIdx++;
                        if (colListIdx == _targetColumns.Count) break;
                    }
                    lineColNumber++;
                }
            }
            catch (BlibException e)
            {
                throw new BlibException(false,
                    $"{e.Message} caught at line {_lineNum.ToString(CultureInfo.InvariantCulture)}, "
                    + $"column {(lineColNumber + 1).ToString(CultureInfo.InvariantCulture)}");
            }
            catch (Exception e)
            {
                throw new BlibException(false,
                    $"{e.Message} caught at line {_lineNum.ToString(CultureInfo.InvariantCulture)}, "
                    + $"column {(lineColNumber + 1).ToString(CultureInfo.InvariantCulture)}");
            }

            StoreLine(entry);

            line = reader.ReadLine();
            _lineNum++;
        }

        // cpp parity: WatersMseReader.cpp:395 — flush the last PSM after the EOF.
        InsertCurPsm();
    }

    // cpp parity: WatersMseReader.cpp:404 — fold one CSV row into _curMsePsm.
    private void StoreLine(LineEntry entry)
    {
        if (_curMsePsm is null)
        {
            _curMsePsm = new MsePSM { SpecKey = _lineNum };
        }

        // cpp parity: skip rows without a valid peak.
        if (entry.FragmentMz == 0 || entry.FragmentIntensity == 0)
        {
            Verbosity.Comment(VerbosityLevel.Detail, $"Throwing out line {_lineNum} with no peak");
            return;
        }

        // cpp parity: WatersMseReader.cpp:418 — same PSM as the curMsePsm? Just append peaks.
        if (_curMsePsm.UnmodSeq == entry.Sequence
            && _curMsePsm.PrecursorNonIntegerCharge == entry.PrecursorNonIntegerCharge
            && _curMsePsm.Charge == entry.PrecursorZ
            && _curMsePsm.Mz == entry.PrecursorMz)
        {
            _curMsePsm.Mzs.Add(entry.FragmentMz);
            _curMsePsm.Intensities.Add(entry.FragmentIntensity);
            _curMsePsm.ProductIonMobilities.Add(_pusherInterval > 0
                ? _pusherInterval * entry.ProductIonMobility
                : 0);
            return;
        }

        // cpp parity: WatersMseReader.cpp:430 — new PSM: store the previous one first.
        InsertCurPsm();

        if (entry.PrecursorIonMobility > 0 && _pusherInterval <= 0)
        {
            throw new BlibException(false,
                "Drift time data cannot be processed without the original raw data present in "
                + "the same directory as the final_fragment.csv, or by specifying a pusher interval "
                + "on the command line.");
        }

        // cpp parity: WatersMseReader.cpp:447 — Pass1 / Pass2 are the only acceptable values.
        if (entry.Pass == "Pass1" || entry.Pass == "Pass2")
        {
            _curMsePsm!.Charge = entry.PrecursorZ;
            _curMsePsm.UnmodSeq = entry.Sequence;
            _curMsePsm.PrecursorNonIntegerCharge = entry.PrecursorNonIntegerCharge;
            _curMsePsm.Mz = entry.PrecursorMz;
            _curMsePsm.Score = entry.Score;
            _curMsePsm.PrecursorIonMobility = _pusherInterval > 0
                ? _pusherInterval * entry.PrecursorIonMobility
                : 0;
            _curMsePsm.RetentionTime = entry.RetentionTime;
            ParseModString(entry, _curMsePsm);
            _curMsePsm.Mzs.Add(entry.FragmentMz);
            _curMsePsm.Intensities.Add(entry.FragmentIntensity);
            _curMsePsm.ProductIonMobilities.Add(_pusherInterval > 0
                ? _pusherInterval * entry.ProductIonMobility
                : 0);
        }
        else
        {
            _curMsePsm!.Valid = false;
        }
    }

    // cpp parity: WatersMseReader.cpp:470 — evaluate the current PSM and add it to the unique set.
    private void InsertCurPsm()
    {
        if (_curMsePsm is null) return;

        if (!string.IsNullOrEmpty(_curMsePsm.UnmodSeq)
            && _curMsePsm.Valid
            && _curMsePsm.Score > _scoreThreshold)
        {
            // cpp parity: WatersMseReader.cpp:483 — if we have mobility + multiple products
            // + a mixed-charge precursor, split the PSM into two charge states by finding the
            // biggest gap in the sorted product-ion mobilities.
            if (_curMsePsm.PrecursorIonMobility > 0
                && _curMsePsm.ProductIonMobilities.Count > 1
                && _curMsePsm.PrecursorNonIntegerCharge != _curMsePsm.Charge)
            {
                SplitMixedChargeMsePsm();
            }

            // cpp parity: WatersMseReader.cpp:584 — insert into the unique set; on duplicate
            // clear the current PSM (we reuse it).
            if (!_uniquePsms.Add(_curMsePsm))
            {
                _curMsePsm.Clear();
            }
            else
            {
                _curMsePsm = new MsePSM();
            }
        }
        else
        {
            if (!_curMsePsm.Valid)
            {
                Verbosity.Comment(VerbosityLevel.Detail,
                    $"Not inserting invalid psm {_curMsePsm.SpecKey.ToString(CultureInfo.InvariantCulture)}.");
            }
            else if (_curMsePsm.Score <= _scoreThreshold)
            {
                Verbosity.Comment(VerbosityLevel.Detail,
                    $"Not inserting psm {_curMsePsm.SpecKey.ToString(CultureInfo.InvariantCulture)} with score "
                    + _curMsePsm.Score.ToString(CultureInfo.InvariantCulture));
                FilteredOutPsmCount++;
            }
            _curMsePsm.Clear();
        }

        // cpp parity: WatersMseReader.cpp:602 — re-key for the next row.
        _curMsePsm!.SpecKey = _lineNum;
    }

    // cpp parity: WatersMseReader.cpp:483-582 — split a mixed-charge MsePSM into two PSMs.
    // Important: cpp operates on the curMsePSM_ POINTER, which the recursive insertCurPSM()
    // call may reseat to a fresh MsePSM (when the recursive Add succeeds). We mirror that by
    // always going through _curMsePsm rather than a captured local reference.
    //
    // The float-vs-double types here are LOAD-BEARING for golden-file parity: cpp's split
    // logic stores the (mobility, idx) pairs as <float,int> and the running averages as float
    // (WatersMseReader.cpp:493, 517, 523). We replicate the float widths exactly because the
    // 6th-decimal-place values in mse-mobility.check are sensitive to the rounding.
    private void SplitMixedChargeMsePsm()
    {
        // Capture parameters off the current PSM before any mutation.
        var pre = _curMsePsm!;

        // cpp parity: WatersMseReader.cpp:493 — std::pair<float, int>.
        var ordered = new List<(float Mobility, int Idx)>(pre.ProductIonMobilities.Count);
        for (int i = 0; i < pre.ProductIonMobilities.Count; i++)
            ordered.Add(((float)pre.ProductIonMobilities[i], i));
        ordered.Sort((a, b) =>
        {
            int c = a.Mobility.CompareTo(b.Mobility);
            return c != 0 ? c : a.Idx.CompareTo(b.Idx);
        });

        int maxJumpIndex = 0;
        double jumpThreshold = 2 * _pusherInterval;
        double maxJump = 0;
        for (int j = 1; j < ordered.Count; j++)
        {
            double jump = ordered[j].Mobility - ordered[j - 1].Mobility;
            if (jump > maxJump && jump > jumpThreshold
                && !(j + 1 == ordered.Count
                     && maxJump > jumpThreshold
                     && jump > maxJump * 2))
            {
                maxJump = jump;
                maxJumpIndex = j;
            }
        }

        if (maxJumpIndex <= 0)
            return;

        // cpp parity: WatersMseReader.cpp:517 — float accumulator.
        float averageHighChargeReportedProductIonMobility = 0;
        for (int k = 0; k < maxJumpIndex; k++)
            averageHighChargeReportedProductIonMobility += ordered[k].Mobility;
        averageHighChargeReportedProductIonMobility /= maxJumpIndex;

        float averageLowChargeReportedProductIonMobility = 0;
        for (int l = maxJumpIndex; l < ordered.Count; l++)
            averageLowChargeReportedProductIonMobility += ordered[l].Mobility;
        averageLowChargeReportedProductIonMobility /= (ordered.Count - maxJumpIndex);

        // cpp parity: WatersMseReader.cpp:530 — pick the kinetic-energy delta based on
        // which charge is the reported one. cpp: `float = float - double` → does the subtraction
        // in double, then truncates to float on store. Replicate exactly.
        float deltaFromKineticEnergy =
            (int)pre.PrecursorNonIntegerCharge != pre.Charge
                ? (float)(averageHighChargeReportedProductIonMobility - pre.PrecursorIonMobility)
                : (float)(averageLowChargeReportedProductIonMobility - pre.PrecursorIonMobility);

        // Snapshot the original PSM for re-keying. Note: cpp keeps this on the stack as a
        // copy of *curMsePSM_; we likewise own a deep copy via CopyFrom.
        var originalPsm = new MsePSM();
        originalPsm.CopyFrom(pre);

        // cpp parity: WatersMseReader.cpp:543 — iterate isHighCharge in {1, 0}. The
        // high-charge half is inserted via the recursive call; the low-charge half is left
        // in _curMsePsm so the caller's natural Add() (after this method returns) catches it.
        for (int isHighCharge = 1; isHighCharge >= 0; isHighCharge--)
        {
            int targetCharge = isHighCharge == 1
                ? 1 + (int)originalPsm.PrecursorNonIntegerCharge
                : (int)originalPsm.PrecursorNonIntegerCharge;

            // Build the per-charge subset of peaks.
            var newMzs = new List<double>();
            var newIntens = new List<double>();
            var newIonMobs = new List<double>();
            for (int k = 0; k < ordered.Count; k++)
            {
                bool isHighChargeFragment = (k < maxJumpIndex);
                bool isHighChargeSpectrum = (isHighCharge == 1);
                if (isHighChargeFragment == isHighChargeSpectrum)
                {
                    int origIdx = ordered[k].Idx;
                    newMzs.Add(originalPsm.Mzs[origIdx]);
                    newIntens.Add(originalPsm.Intensities[origIdx]);
                    newIonMobs.Add(originalPsm.ProductIonMobilities[origIdx]);
                }
            }

            // cpp parity: WatersMseReader.cpp:562 `*curMsePSM_ = msePSM` overwrites whatever
            // object curMsePSM_ now points at. After the recursive call on the first iteration,
            // _curMsePsm may have been replaced by a fresh instance; we always write through
            // the field, not a captured local.
            _curMsePsm ??= new MsePSM();
            _curMsePsm.Clear();
            _curMsePsm.CopyFrom(originalPsm);
            _curMsePsm.Mzs.Clear();
            _curMsePsm.Intensities.Clear();
            _curMsePsm.ProductIonMobilities.Clear();
            _curMsePsm.Mzs.AddRange(newMzs);
            _curMsePsm.Intensities.AddRange(newIntens);
            _curMsePsm.ProductIonMobilities.AddRange(newIonMobs);

            _curMsePsm.Charge = targetCharge;
            _curMsePsm.PrecursorNonIntegerCharge = targetCharge;

            if (_curMsePsm.Charge != originalPsm.Charge)
            {
                // cpp parity: WatersMseReader.cpp:567 — recompute mz from neutral mass
                // using pwiz::chemistry::Ion::neutralMass / Ion::mz. The cpp helpers use
                // `Proton = 1.00727646688` (Chemistry.hpp:41), NOT AminoAcidMasses.ProtonMass
                // (1.007276). The golden mse-mobility.check's precursorMZ for the split
                // halves (e.g. AVFVDLEPTVIDEVR z=3 = 567.97569215) is sensitive to which
                // proton mass is used — replicate the cpp Ion constant exactly.
                const double Proton = 1.00727646688;
                double mass = originalPsm.Mz * originalPsm.Charge
                              - originalPsm.Charge * Proton;
                _curMsePsm.Mz = (mass + targetCharge * Proton) / targetCharge;
                // cpp parity: WatersMseReader.cpp:569 — `double = float`, then subtract a
                // float `deltaFromKineticEnergy` (result is double). Replicate by promoting
                // the float average to double via assignment.
                double averageProductIonDriftTime = isHighCharge == 1
                    ? averageHighChargeReportedProductIonMobility
                    : averageLowChargeReportedProductIonMobility;
                _curMsePsm.PrecursorIonMobility = averageProductIonDriftTime - deltaFromKineticEnergy;
            }

            if (isHighCharge == 1)
            {
                // cpp parity: WatersMseReader.cpp:577 — recursive insert with a different
                // specKey to avoid colliding with the other half.
                _curMsePsm.SpecKey = _lineNum - 1;
                InsertCurPsm();
            }
            // The isHighCharge==0 iteration leaves _curMsePsm populated; the outer
            // InsertCurPsm continues to its natural Add() / Clear() path.
        }
    }

    // cpp parity: WatersMseReader.cpp:613 — split entry.Modification on ';' and add SeqMods.
    private void ParseModString(LineEntry entry, MsePSM psm)
    {
        if (string.IsNullOrEmpty(entry.Modification))
            return;

        var mods = entry.Modification.Split(';');
        foreach (var rawMod in mods)
        {
            var modString = rawMod.Trim();
            if (modString.Length == 0
                || string.Equals(modString, "None", StringComparison.OrdinalIgnoreCase))
                continue;

            // cpp parity: WatersMseReader.cpp:623 — reverse iterate the mods map for
            // longest-prefix-first matching.
            KeyValuePair<string, double>? matched = null;
            foreach (var kvp in _modsByReverseKey)
            {
                if (modString.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    matched = kvp;
                    break;
                }
            }
            if (matched is null)
            {
                var modList = string.Join("\", \"", _modsRaw.Keys);
                throw new BlibException(false,
                    $"The modification '{modString}' on line {_lineNum.ToString(CultureInfo.InvariantCulture)} "
                    + $"is not recognized. Supported modifications include: \"{modList}\".");
            }

            // cpp parity: WatersMseReader.cpp:636 — find the position in the trailing (pos).
            int openBrace = modString.LastIndexOf('(');
            int position = 0;
            if (openBrace >= 0 && openBrace + 1 < modString.Length)
            {
                // cpp uses atoi which stops at first non-digit; we mimic by walking digits.
                int i = openBrace + 1;
                while (i < modString.Length && char.IsDigit(modString[i]))
                {
                    position = position * 10 + (modString[i] - '0');
                    i++;
                }
            }

            if (position == 0)
            {
                psm.Valid = false;
                psm.Mods.Clear();
                return;
            }

            double deltaMass = matched.Value.Value;
            if (deltaMass == GlycolMass)
                deltaMass = entry.PrecursorMass - entry.MinMass;
            psm.Mods.Add(new SeqMod(position, deltaMass));
        }
    }

    // --- CSV split ------------------------------------------------------------------

    // The Waters MSE final_fragment.csv format has no embedded commas or quoted strings, so a
    // simple split is byte-for-byte equivalent to cpp's boost::escaped_list_separator on these
    // inputs. If a row ever contained an escaped delimiter we'd need a stateful tokenizer.
    private static string[] SplitCsv(string line)
    {
        // Trim trailing CR (StreamReader strips LF but not CR on stray CRLF in CSV body).
        if (line.Length > 0 && line[line.Length - 1] == '\r')
            line = line.Substring(0, line.Length - 1);
        return line.Split(',');
    }

    // --- Column translator types ----------------------------------------------------

    // cpp parity: WatersMseReader.h:114 — typed line accumulator.
    private sealed class LineEntry
    {
        public double PrecursorMz;
        public double PrecursorNonIntegerCharge;
        public int PrecursorZ;
        public double Score;
        public double RetentionTime;
        public string Sequence = string.Empty;
        public string Modification = string.Empty;
        public double FragmentMz;
        public double FragmentIntensity;
        public double PrecursorMass;
        public double MinMass;
        // cpp parity: WatersMseReader.h:130-131 — these two are FLOAT in cpp, not double.
        // The precision loss is observable in the resulting ionMobilityHighEnergyOffset (the
        // mse-mobility.check golden's last-digit values like -0.16169037 only match when the
        // input was rounded to single precision before being multiplied by the pusher interval).
        public float PrecursorIonMobility;
        public float ProductIonMobility;
        public string Pass = string.Empty;

        public static void InsertPrecursorMz(LineEntry le, string v)
            => le.PrecursorMz = ParseDouble(v);
        public static void InsertPrecursorNonIntegerCharge(LineEntry le, string v)
            => le.PrecursorNonIntegerCharge = ParseDouble(v);
        public static void InsertPrecursorZ(LineEntry le, string v)
            => le.PrecursorZ = ParseInt(v);
        public static void InsertScore(LineEntry le, string v)
            => le.Score = ParseDouble(v);
        public static void InsertRetentionTime(LineEntry le, string v)
            => le.RetentionTime = ParseDouble(v);
        public static void InsertSequence(LineEntry le, string v) => le.Sequence = v;
        public static void InsertModification(LineEntry le, string v) => le.Modification = v;
        public static void InsertFragmentMz(LineEntry le, string v)
            => le.FragmentMz = ParseDouble(v);
        public static void InsertFragmentIntensity(LineEntry le, string v)
            => le.FragmentIntensity = ParseDouble(v);
        public static void InsertPrecursorMass(LineEntry le, string v)
            => le.PrecursorMass = ParseDouble(v);
        public static void InsertMinMass(LineEntry le, string v)
            => le.MinMass = ParseDouble(v);
        public static void InsertPrecursorIonMobility(LineEntry le, string v)
            => le.PrecursorIonMobility = ParseFloat(v);
        public static void InsertProductIonMobility(LineEntry le, string v)
            => le.ProductIonMobility = ParseFloat(v);
        public static void InsertPass(LineEntry le, string v) => le.Pass = v;

        private static double ParseDouble(string v)
        {
            if (string.IsNullOrEmpty(v)) return 0;
            return double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static float ParseFloat(string v)
        {
            if (string.IsNullOrEmpty(v)) return 0;
            return float.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static int ParseInt(string v)
        {
            if (string.IsNullOrEmpty(v)) return 0;
            return int.Parse(v, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
    }

    // cpp parity: WatersMseReader.h:190 — name, file-column-position, setter func.
    private sealed class WColumnTranslator
    {
        public string Name { get; }
        public int Position { get; set; }
        public Action<LineEntry, string> Inserter { get; }

        public WColumnTranslator(string name, int position, Action<LineEntry, string> inserter)
        {
            Name = name;
            Position = position;
            Inserter = inserter;
        }
    }

    // --- Inner spec reader ----------------------------------------------------------

    /// <summary>
    /// Spec-file reader that returns peaks straight off the <see cref="MsePSM"/>. cpp parity:
    /// WatersMseReader.cpp:661 — the cpp <c>getSpectrum(PSM*, ...)</c> override pulls all peak
    /// data from the MsePSM rather than opening an external file.
    /// </summary>
    private sealed class MseSpecFileReader : SpecFileReaderBase
    {
        public override void OpenFile(string path, bool mzSort = false)
        {
            // cpp parity: WatersMseReader.cpp:654 — no-op.
        }

        public override SpecIdType IdType
        {
            set { /* no-op */ }
        }

        // cpp parity: WatersMseReader.cpp:661 — PSM-form getSpectrum copies straight from MsePSM.
        public override bool GetSpectrum(PSM psm, SpecIdType findBy, SpecData returnData, bool getPeaks)
        {
            ArgumentNullException.ThrowIfNull(psm);
            ArgumentNullException.ThrowIfNull(returnData);

            if (psm is not MsePSM msePsm)
            {
                return false;
            }

            returnData.Id = msePsm.SpecKey;
            returnData.IonMobility = (float)msePsm.PrecursorIonMobility;
            returnData.IonMobilityType = IonMobilityType.DriftTimeMsec;
            returnData.Ccs = 0;
            returnData.RetentionTime = msePsm.RetentionTime;
            returnData.Mz = msePsm.Mz;
            returnData.NumPeaks = msePsm.Mzs.Count;

            if (getPeaks)
            {
                returnData.Mzs = new double[returnData.NumPeaks];
                returnData.Intensities = new float[returnData.NumPeaks];
                returnData.ProductIonMobilities = new float[returnData.NumPeaks];
                for (int i = 0; i < returnData.NumPeaks; i++)
                {
                    returnData.Mzs[i] = msePsm.Mzs[i];
                    returnData.Intensities[i] = (float)msePsm.Intensities[i];
                    returnData.ProductIonMobilities[i] = (float)msePsm.ProductIonMobilities[i];
                }
            }
            else
            {
                returnData.Mzs = null;
                returnData.Intensities = null;
                returnData.ProductIonMobilities = null;
            }
            return true;
        }

        public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
            => false;

        public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true)
            => false;

        public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true)
            => false;
    }
}
