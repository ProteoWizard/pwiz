namespace Pwiz.Util.Chemistry;

/// <summary>
/// Enumeration of chemical elements plus a handful of named isotopes.
/// Matches pwiz/chemistry::Element::Type exactly; order must be preserved.
/// </summary>
/// <remarks>
/// Entries before (and including) <see cref="_15N"/> form the hot-path "CHONSP" set
/// (plus labeled variants) that <see cref="Formula"/> stores in a fixed-size array
/// rather than a dictionary.
/// </remarks>
#pragma warning disable CA1028, CA1707, CA1720 // enum backing type, underscores (for IUPAC labels), type-name-like members
public enum ElementType
{
    C, H, O, N, S, P, _13C, _2H, _18O, _15N, // Order matters: _15N is the end of the CHONSP entries
    He, Li, Be, B, F, Ne,
    Na, Mg, Al, Si, Cl, Ar, K, Ca,
    Sc, Ti, V, Cr, Mn, Fe, Co, Ni, Cu, Zn,
    Ga, Ge, As, Se, Br, Kr, Rb, Sr, Y, Zr,
    Nb, Mo, Tc, Ru, Rh, Pd, Ag, Cd, In, Sn,
    Sb, Te, I, Xe, Cs, Ba, La, Ce, Pr, Nd,
    Pm, Sm, Eu, Gd, Tb, Dy, Ho, Er, Tm, Yb,
    Lu, Hf, Ta, W, Re, Os, Ir, Pt, Au, Hg,
    Tl, Pb, Bi, Po, At, Rn, Fr, Ra, Ac, Th,
    Pa, U, Np, Pu, Am, Cm, Bk, Cf, Es, Fm,
    Md, No, Lr, Rf, Db, Sg, Bh, Hs, Mt, Uun,
    Uuu, Uub, Uuq, Uuh, _3H,
}
#pragma warning restore CA1028, CA1707, CA1720
