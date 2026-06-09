// Port of pwiz_tools/BiblioSpec/src/RefSpectrum.{h,cpp}

using System.Diagnostics;
using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Library reference spectrum: a <see cref="Spectrum"/> plus peptide sequence,
/// modifications, ID number, quality annotation, source-copy count, and small-molecule
/// metadata. Sort by ID (<see cref="LibSpecId"/>) or by ion (charge + sequence + mods).
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::RefSpectrum</c>. Since the library is stored in SQLite, the
/// shape here is sympathetic to row-by-row hydration: every backing field defaults to a
/// well-defined empty value (mostly empty strings / 0).</para>
/// <para>cpp RefSpectrum.cpp:32-41 — the parameterless constructor sets
/// <c>libID = -1</c>, <c>libSpecID = -1</c>, <c>scoreType_ = -1</c>; we preserve those sentinels.</para>
/// <para>cpp RefSpectrum.cpp:69 — <c>newDecoy</c> returns null if the shift would fail
/// (deltaMz == 0, &lt; 5 raw peaks, or &lt; 5 processed peaks when shifting processed). Preserved.</para>
/// </remarks>
public class RefSpectrum : Spectrum
{
    private int _charge;
    private int _copies;
    private int _libId;
    private int _libSpecId;
    private string _pepSeq = string.Empty;
    private string _modsPepSeq = string.Empty;
    private string _prevAa = string.Empty;
    private string _nextAa = string.Empty;
    private double _circShift;
    private double _score;
    private int _scoreType;
    private SmallMolMetadata _smallMolMetadata = new();

    /// <summary>Constructs an empty reference spectrum (cpp sentinels: <c>libID=-1, libSpecID=-1, scoreType=-1</c>).</summary>
    public RefSpectrum()
    {
        _libId = -1;       // 0 means decoy spec
        _libSpecId = -1;
        _scoreType = -1;
        type_ = SpecType.Reference;
    }

    /// <summary>Copy-constructor over another <see cref="RefSpectrum"/>; deep-copies peak lists.</summary>
    public RefSpectrum(RefSpectrum other) : base(other)
    {
        ArgumentNullException.ThrowIfNull(other);
        _charge = other._charge;
        _copies = other._copies;
        _libId = other._libId;
        _libSpecId = other._libSpecId;
        _pepSeq = other._pepSeq;
        _modsPepSeq = other._modsPepSeq;
        _prevAa = other._prevAa;
        _nextAa = other._nextAa;
        _circShift = other._circShift;
        _score = other._score;
        _scoreType = other._scoreType;
        _smallMolMetadata = new SmallMolMetadata
        {
            InchiKey = other._smallMolMetadata.InchiKey,
            PrecursorAdduct = other._smallMolMetadata.PrecursorAdduct,
            ChemicalFormula = other._smallMolMetadata.ChemicalFormula,
            MoleculeName = other._smallMolMetadata.MoleculeName,
            OtherKeys = other._smallMolMetadata.OtherKeys,
            PrecursorMzDeclared = other._smallMolMetadata.PrecursorMzDeclared,
        };
    }

    /// <summary>
    /// Construct a RefSpectrum from a plain <see cref="Spectrum"/>. If the spectrum has exactly
    /// one possible charge, that becomes <see cref="Charge"/>; otherwise charge is 0 and the
    /// possible-charge list is cleared.
    /// </summary>
    public RefSpectrum(Spectrum spectrum) : base(spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        _copies = 0;
        _libId = -1;
        _libSpecId = -1;
        _circShift = 0;
        _scoreType = -1;
        if (possibleCharges_.Count == 1)
        {
            _charge = possibleCharges_[0];
        }
        else
        {
            _charge = 0;
            possibleCharges_.Clear();
        }
        type_ = SpecType.Reference;
    }

    /// <inheritdoc/>
    public override void Clear()
    {
        scanNumber_ = 0;
        mz_ = 0;
        rawPeaks_.Clear();
        processedPeaks_.Clear();
        _charge = 0;
        _copies = 0;
        _libId = -1;
        _libSpecId = 0;       // cpp RefSpectrum.cpp:144 — note this is 0, not -1
        _pepSeq = string.Empty;
        _modsPepSeq = string.Empty;
    }

    /// <summary>Precursor charge (a RefSpectrum may have only one charge state).</summary>
    public int Charge
    {
        get => _charge;
        set => _charge = value;
    }

    /// <summary>
    /// Overrides <see cref="Spectrum.AddCharge"/> to force exactly one charge — a RefSpectrum
    /// can have only a single charge state.
    /// </summary>
    public override void AddCharge(int charge)
    {
        _charge = charge;
        possibleCharges_.Clear();
        possibleCharges_.Add(charge);
    }

    /// <summary>Peptide sequence (no modifications). Setting null is normalised to empty.</summary>
    public string Sequence
    {
        get => _pepSeq;
        set => _pepSeq = value ?? string.Empty;
    }

    /// <summary>
    /// Modified peptide sequence — typically the bracketed form, e.g. <c>"PEPM[15.99]TIDE"</c>.
    /// Setting null is normalised to empty.
    /// </summary>
    /// <remarks>cpp encodes mods inline; this matches the format <see cref="BlibUtils.GetPeptideMass"/> consumes.</remarks>
    public string ModifiedSequence
    {
        get => _modsPepSeq;
        set => _modsPepSeq = value ?? string.Empty;
    }

    /// <summary>Library row id in the multi-library BiblioLibrary table; -1 if not in a library, 0 for a decoy.</summary>
    public int LibId
    {
        get => _libId;
        set => _libId = value;
    }

    /// <summary>Row id in the RefSpectra table (per-library spectrum id).</summary>
    public int LibSpecId
    {
        get => _libSpecId;
        set => _libSpecId = value;
    }

    /// <summary>Number of source spectra this entry was selected from in a filtered library.</summary>
    public int Copies
    {
        get => _copies;
        set => _copies = value;
    }

    /// <summary>Residue preceding the peptide in the source protein (empty if unknown).</summary>
    public string PrevAa
    {
        get => _prevAa;
        set => _prevAa = value ?? string.Empty;
    }

    /// <summary>Residue following the peptide in the source protein (empty if unknown).</summary>
    public string NextAa
    {
        get => _nextAa;
        set => _nextAa = value ?? string.Empty;
    }

    /// <summary>m/z amount by which peaks have been circularly shifted; 0 for an observed spectrum.</summary>
    public double CircShift => _circShift;

    /// <summary>Score for this PSM (interpretation depends on <see cref="ScoreType"/>).</summary>
    public double Score
    {
        get => _score;
        set => _score = value;
    }

    /// <summary>
    /// Score type tag — usually the integer value of a <see cref="PsmScoreType"/>, but
    /// represented as an int because cpp uses -1 to mean "not set" and -1 isn't a valid
    /// enum value.
    /// </summary>
    public int ScoreType
    {
        get => _scoreType;
        set => _scoreType = value;
    }

    /// <summary>Friendly molecule name (small molecules only).</summary>
    public string MoleculeName
    {
        get => _smallMolMetadata.MoleculeName;
        set => _smallMolMetadata.MoleculeName = value ?? string.Empty;
    }

    /// <summary>Neutral chemical formula (small molecules only).</summary>
    public string ChemicalFormula
    {
        get => _smallMolMetadata.ChemicalFormula;
        set => _smallMolMetadata.ChemicalFormula = value ?? string.Empty;
    }

    /// <summary>Ionising adduct (small molecules only).</summary>
    public string Adduct
    {
        get => _smallMolMetadata.PrecursorAdduct;
        set => _smallMolMetadata.PrecursorAdduct = value ?? string.Empty;
    }

    /// <summary>InChI key (small molecules only).</summary>
    public string InchiKey
    {
        get => _smallMolMetadata.InchiKey;
        set => _smallMolMetadata.InchiKey = value ?? string.Empty;
    }

    /// <summary>Tab-separated alternative identifiers (small molecules only).</summary>
    public string OtherKeys
    {
        get => _smallMolMetadata.OtherKeys;
        set => _smallMolMetadata.OtherKeys = value ?? string.Empty;
    }

    /// <summary>All small-molecule metadata, as a single struct.</summary>
    public SmallMolMetadata SmallMolMetadata => _smallMolMetadata;

    /// <summary>
    /// Display name: returns the peptide sequence if present, otherwise the small-molecule
    /// name. cpp RefSpectrum.cpp:235.
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(_pepSeq) ? _smallMolMetadata.MoleculeName : _pepSeq;

    /// <summary>
    /// Returns a tab-separated <c>formula \t name \t inchikey \t otherkeys \t adduct \t</c>
    /// concatenation when this is a non-proteomic spectrum (no modifications). Returns
    /// empty otherwise. cpp RefSpectrum.cpp:323.
    /// </summary>
    /// <remarks>cpp quirk: trailing tab on every field, including the last. Preserved.</remarks>
    public string GetSmallMoleculeIonId()
    {
        if (!string.IsNullOrEmpty(_modsPepSeq))
            return string.Empty; // not a small molecule

        var ids = new[]
        {
            _smallMolMetadata.ChemicalFormula,
            _smallMolMetadata.MoleculeName,
            _smallMolMetadata.InchiKey,
            _smallMolMetadata.OtherKeys,
            _smallMolMetadata.PrecursorAdduct,
        };
        var hasAny = false;
        foreach (var v in ids)
        {
            if (!string.IsNullOrEmpty(v)) { hasAny = true; break; }
        }
        if (!hasAny) return string.Empty;
        return string.Concat(ids.Select(s => s + "\t"));
    }

    /// <summary>
    /// Create a decoy as a copy of this spectrum, applying a circular m/z shift. Returns
    /// <c>null</c> if the shift would fail (deltaMz == 0, &lt; 5 raw peaks, or &lt; 5 processed
    /// peaks when not shifting raw peaks).
    /// </summary>
    /// <param name="shiftDelta">m/z shift to apply.</param>
    /// <param name="shiftRawSpectrum">When true, shift raw peaks; when false, shift processed peaks.</param>
    public RefSpectrum? NewDecoy(double shiftDelta, bool shiftRawSpectrum)
    {
        if (shiftDelta == 0 || rawPeaks_.Count < 5 ||
            (!shiftRawSpectrum && processedPeaks_.Count < 5))
        {
            return null;
        }

        var decoy = new RefSpectrum(this)
        {
            // all decoys have libID = 0, negate spec ID to indicate decoy
            _libId = 0,
            _libSpecId = -1 * _libSpecId,
        };

        decoy.CircularShift(shiftDelta, shiftRawSpectrum);
        return decoy;
    }

    /// <summary>
    /// Create a null spectrum by shifting all peaks by <paramref name="deltaMz"/>; peaks that
    /// shift out of the m/z range are wrapped to the other end.
    /// </summary>
    /// <remarks>
    /// <para>cpp RefSpectrum.cpp:393. Three modes:</para>
    /// <list type="bullet">
    ///   <item>shifting processed peaks (raw untouched);</item>
    ///   <item>shifting raw peaks with no processed peaks present;</item>
    ///   <item>shifting raw peaks WITH processed peaks — the processed list is cleared because it's now stale.</item>
    /// </list>
    /// <para>Does nothing if <paramref name="deltaMz"/> is 0 or the target list has &lt; 5 peaks.</para>
    /// </remarks>
    public void CircularShift(double deltaMz, bool shiftRawPeaks)
    {
        var peaks = shiftRawPeaks ? rawPeaks_ : processedPeaks_;
        if (deltaMz == 0 || peaks.Count < 5) return;

        Verbosity.Comment(
            VerbosityLevel.Debug,
            string.Format(CultureInfo.InvariantCulture, "Circular shifting spec {0} with {1} peaks.", _libSpecId, peaks.Count));

        var minMz = peaks[0].Mz;
        var maxMz = peaks[^1].Mz;
        var range = maxMz - minMz;

        ShiftModRange(ref deltaMz, range);
        Debug.Assert(Math.Abs((int)deltaMz) < range);

        // Shift every peak
        for (var i = 0; i < peaks.Count; i++)
        {
            var p = peaks[i];
            p.Mz += deltaMz;
            peaks[i] = p;
        }

        if (deltaMz > 0)
        {
            // peaks pushed off the top get wrapped to the bottom
            var moveIdx = peaks.Count - 1;
            while (peaks[moveIdx].Mz > maxMz)
            {
                var p = peaks[moveIdx];
                p.Mz -= (range + 1);
                peaks[moveIdx] = p;
                moveIdx--;
            }
            Debug.Assert(moveIdx + 1 >= 0 && moveIdx + 1 < peaks.Count);
            // std::rotate(first, middle, last) — moves [middle, last) to the front, then [first, middle) after.
            Rotate(peaks, moveIdx + 1);
        }
        else
        {
            // peaks pushed off the bottom get wrapped to the top
            var moveIdx = 0;
            while (moveIdx < peaks.Count && peaks[moveIdx].Mz < minMz)
            {
                var p = peaks[moveIdx];
                p.Mz += (range + 1);
                peaks[moveIdx] = p;
                moveIdx++;
            }
            Debug.Assert(moveIdx >= 0 && moveIdx < peaks.Count);
            Rotate(peaks, moveIdx);
        }

        // If we shifted raw peaks, processed peaks are now stale and we drop them.
        if (shiftRawPeaks && processedPeaks_.Count > 0)
        {
            processedPeaks_.Clear();
        }

        _circShift = deltaMz;
    }

    /// <summary>
    /// std::rotate equivalent: moves elements [middle, end) to the front of the list,
    /// then [start, middle) after them.
    /// </summary>
    private static void Rotate(List<PeakT> list, int middle)
    {
        if (middle <= 0 || middle >= list.Count) return;
        var head = list.GetRange(0, middle);
        list.RemoveRange(0, middle);
        list.AddRange(head);
    }

    /// <summary>
    /// Reduce <paramref name="shift"/> so |shift| &lt; range, preserving sign.
    /// </summary>
    /// <remarks>cpp RefSpectrum.cpp:367 — simple modular reduction with sign restored at the end.</remarks>
    private static void ShiftModRange(ref double shift, double range)
    {
        var negShift = shift < 0;
        if (negShift) shift *= -1;
        while (shift > range) shift -= range;
        if (negShift) shift *= -1;
    }
}
