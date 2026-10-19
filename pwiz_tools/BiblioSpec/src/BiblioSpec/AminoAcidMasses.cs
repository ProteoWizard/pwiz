// Port of pwiz_tools/BiblioSpec/src/AminoAcidMasses.{h,cpp}

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Hardcoded residue mass tables and peptide mass calculation. Element values come from
/// http://physics.nist.gov/PhysRefData/Compositions/index.html (per cpp header comment).
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::AminoAcidMasses</c>. The numbers here are the authoritative
/// reference for golden-file parity — they are NOT taken from <c>Pwiz.Util.Chemistry</c>
/// because the cpp code carries small quirks (e.g. average carbon mass <c>12.01085</c>,
/// commented "Matching Skyline (see BioMassCalc.cs)") that an isotope-table calculator
/// would not reproduce exactly.</para>
/// <para>cpp AminoAcidMasses.cpp:88-90 — X is treated as L/I, B is the average of N and D,
/// Z is the average of Q and E. These quirks are preserved here.</para>
/// </remarks>
public static class AminoAcidMasses
{
    /// <summary>Mass of a proton (Da). Matches cpp <c>PROTON_MASS</c> in AminoAcidMasses.h.</summary>
    public const double ProtonMass = 1.007276;

    /// <summary>
    /// Initialise a 128-entry mass table indexed by ASCII character. Mirrors cpp
    /// <c>AminoAcidMasses::initializeMass</c>. After calling, <c>masses['A']</c> etc. hold
    /// residue masses (free amino acid minus water — i.e. residue, not free-AA mass).
    /// </summary>
    /// <param name="masses">Pre-allocated buffer of length &gt;= 128.</param>
    /// <param name="monoisotopic">When true, populate monoisotopic masses; otherwise average.</param>
    public static void InitializeMass(double[] masses, bool monoisotopic)
    {
        ArgumentNullException.ThrowIfNull(masses);
        if (masses.Length < 128)
            throw new ArgumentException("mass table must have at least 128 entries", nameof(masses));

        double h, o, c, n, p, s;
        if (monoisotopic)
        {
            h = masses['h'] = 1.00782503521;   // hydrogen
            o = masses['o'] = 15.9949146221;   // oxygen
            c = masses['c'] = 12.0000000;      // carbon
            n = masses['n'] = 14.0030740052;   // nitrogen
            p = masses['p'] = 30.97376151;     // phosphorus
            s = masses['s'] = 31.97207069;     // sulphur
        }
        else
        {
            h = masses['h'] = 1.00794;   // hydrogen
            o = masses['o'] = 15.9994;   // oxygen
            // cpp AminoAcidMasses.cpp:60 — "Matching Skyline (see BioMassCalc.cs)";
            // this is 12.01085, NOT NIST's 12.0107. Preserved verbatim for parity.
            c = masses['c'] = 12.01085;  // carbon
            n = masses['n'] = 14.0067;   // nitrogen
            p = masses['p'] = 30.973761; // phosphorus
            s = masses['s'] = 32.065;    // sulphur
        }

        masses['G'] = c * 2 + h * 3 + n + o;
        masses['A'] = c * 3 + h * 5 + n + o;
        masses['S'] = c * 3 + h * 5 + n + o * 2;
        masses['P'] = c * 5 + h * 7 + n + o;
        masses['V'] = c * 5 + h * 9 + n + o;
        masses['T'] = c * 4 + h * 7 + n + o * 2;
        masses['C'] = c * 3 + h * 5 + n + o + s;
        masses['L'] = c * 6 + h * 11 + n + o;
        masses['I'] = c * 6 + h * 11 + n + o;
        masses['N'] = c * 4 + h * 6 + n * 2 + o * 2;
        masses['D'] = c * 4 + h * 5 + n + o * 3;
        masses['Q'] = c * 5 + h * 8 + n * 2 + o * 2;
        masses['K'] = c * 6 + h * 12 + n * 2 + o;
        masses['E'] = c * 5 + h * 7 + n + o * 3;
        masses['M'] = c * 5 + h * 9 + n + o + s;
        masses['H'] = c * 6 + h * 7 + n * 3 + o;
        masses['F'] = c * 9 + h * 9 + n + o;
        masses['R'] = c * 6 + h * 12 + n * 4 + o;
        masses['Y'] = c * 9 + h * 9 + n + o * 2;
        masses['W'] = c * 11 + h * 10 + n * 2 + o;

        masses['O'] = c * 5 + h * 12 + n * 2 + o * 2;
        // cpp AminoAcidMasses.cpp:88 — "treat X as L or I for no good reason"
        masses['X'] = masses['L'];
        // cpp AminoAcidMasses.cpp:89-90 — B/Z averaged from their pair
        masses['B'] = (masses['N'] + masses['D']) / 2.0;
        masses['Z'] = (masses['Q'] + masses['E']) / 2.0;
    }

    /// <summary>
    /// Build a fresh 128-entry mass table (monoisotopic or average) keyed by ASCII residue char.
    /// </summary>
    public static double[] BuildMassTable(bool monoisotopic)
    {
        var table = new double[128];
        InitializeMass(table, monoisotopic);
        return table;
    }
}
