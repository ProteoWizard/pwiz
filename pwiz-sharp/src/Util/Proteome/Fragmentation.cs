using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Proteome;

/// <summary>
/// Provides fragment-ion masses (a/b/c/x/y/z/zRadical) for a peptide, optionally
/// modification-aware and either monoisotopic or average. Built once and reused for
/// repeated <c>fragmentation.b(i, charge)</c> queries.
/// </summary>
/// <remarks>Port of <c>pwiz::proteome::Fragmentation</c>.</remarks>
public sealed class Fragmentation
{
    // Series-mass deltas. Values match the formulas in pwiz cpp Peptide.cpp:578-586:
    //   a = -CO          b = (proton only)    c = +NH3
    //   x = +CO + H2O    y = +H2O             z = -NH + H2O - H2 (collapses to "-NH + H2O - H2")
    private static readonly Formula s_aFormula = new("C-1 O-1");
    private static readonly Formula s_bFormula = new(""); // proton only
    private static readonly Formula s_cFormula = new("N1 H3");
    private static readonly Formula s_xFormula = new Formula("C1 O1 H-2") + new Formula("H2 O1");
    private static readonly Formula s_yFormula = new("H2 O1");
    private static readonly Formula s_zFormula = new Formula("N-1 H-3") + new Formula("H2 O1");

    private readonly double[] _masses; // cumulative N-terminal residue masses
    private readonly double _aMass, _bMass, _cMass, _xMass, _yMass, _zMass;
    private readonly double _nTerm;
    private readonly double _cTerm;
    private readonly int _maxLength;

    /// <summary>Construct from a peptide.</summary>
    public Fragmentation(Peptide peptide, bool monoisotopic, bool modified)
    {
        ArgumentNullException.ThrowIfNull(peptide);
        _aMass = monoisotopic ? s_aFormula.MonoisotopicMass : s_aFormula.MolecularWeight;
        _bMass = monoisotopic ? s_bFormula.MonoisotopicMass : s_bFormula.MolecularWeight;
        _cMass = monoisotopic ? s_cFormula.MonoisotopicMass : s_cFormula.MolecularWeight;
        _xMass = monoisotopic ? s_xFormula.MonoisotopicMass : s_xFormula.MolecularWeight;
        _yMass = monoisotopic ? s_yFormula.MonoisotopicMass : s_yFormula.MolecularWeight;
        _zMass = monoisotopic ? s_zFormula.MonoisotopicMass : s_zFormula.MolecularWeight;

        var seq = peptide.Sequence;
        _maxLength = seq.Length;
        _masses = new double[_maxLength];

        var mods = peptide.Modifications;
        var modItr = mods.GetEnumerator();
        bool hasMod = modItr.MoveNext();

        if (modified && hasMod && modItr.Current.Key == ModificationMap.NTerminus)
        {
            foreach (var m in modItr.Current.Value)
                _nTerm += monoisotopic ? m.MonoisotopicDeltaMass : m.AverageDeltaMass;
            hasMod = modItr.MoveNext();
        }

        double cumulative = 0;
        for (int i = 0; i < _maxLength; i++)
        {
            var residue = AminoAcidInfo.Record(seq[i]).ResidueFormula;
            cumulative += monoisotopic ? residue.MonoisotopicMass : residue.MolecularWeight;
            if (modified && hasMod && modItr.Current.Key == i)
            {
                foreach (var m in modItr.Current.Value)
                    cumulative += monoisotopic ? m.MonoisotopicDeltaMass : m.AverageDeltaMass;
                hasMod = modItr.MoveNext();
            }
            _masses[i] = cumulative;
        }

        if (modified && hasMod && modItr.Current.Key == ModificationMap.CTerminus)
        {
            foreach (var m in modItr.Current.Value)
                _cTerm += monoisotopic ? m.MonoisotopicDeltaMass : m.AverageDeltaMass;
        }
    }

    private double F(int length) => length == 0 ? 0 : _masses[length - 1];

    private static double Charged(double neutral, int charge) =>
        charge == 0 ? neutral : (neutral + PhysicalConstants.Proton * charge) / charge;

    private double NTermFragment(int length, double seriesMass, int charge) =>
        Charged(_nTerm + F(length) + seriesMass, charge);

    private double CTermFragment(int length, double seriesMass, int charge) =>
        Charged(_cTerm + _masses[_maxLength - 1] - F(_maxLength - length) + seriesMass, charge);

    /// <summary>a-ion of length <paramref name="length"/> at the given charge (0 = neutral mass).</summary>
    public double A(int length, int charge = 0) => NTermFragment(length, _aMass, charge);

    /// <summary>b-ion of length <paramref name="length"/> at the given charge (0 = neutral mass).</summary>
    public double B(int length, int charge = 0) => NTermFragment(length, _bMass, charge);

    /// <summary>c-ion of length <paramref name="length"/>; <c>length == peptide-length</c> is invalid.</summary>
    public double C(int length, int charge = 0)
    {
        if (length == _maxLength)
            throw new InvalidOperationException("c for full peptide length is impossible");
        return NTermFragment(length, _cMass, charge);
    }

    /// <summary>x-ion of length <paramref name="length"/>; <c>length == peptide-length</c> is invalid.</summary>
    public double X(int length, int charge = 0)
    {
        if (length == _maxLength)
            throw new InvalidOperationException("x for full peptide length is impossible");
        return CTermFragment(length, _xMass, charge);
    }

    /// <summary>y-ion of length <paramref name="length"/> at the given charge (0 = neutral mass).</summary>
    public double Y(int length, int charge = 0) => CTermFragment(length, _yMass, charge);

    /// <summary>z-ion of length <paramref name="length"/> at the given charge (0 = neutral mass).</summary>
    public double Z(int length, int charge = 0) => CTermFragment(length, _zMass, charge);

    /// <summary>z• (radical z) ion of length <paramref name="length"/> at the given charge.</summary>
    public double ZRadical(int length, int charge = 0)
    {
        // cpp form: zRadical_charge0 = zMass + Proton; zRadical_chargeN = (z_neutral_with_charge0 + Proton*(charge+1)) / charge
        double neutral = _cTerm + _masses[_maxLength - 1] - F(_maxLength - length) + _zMass;
        return charge == 0
            ? neutral + PhysicalConstants.Proton
            : (neutral + PhysicalConstants.Proton * (charge + 1)) / charge;
    }
}
