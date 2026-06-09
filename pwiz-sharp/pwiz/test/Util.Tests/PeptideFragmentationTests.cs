using Pwiz.Util.Chemistry;
using Pwiz.Util.Proteome;

namespace Pwiz.Util.Tests.Proteome;

/// <summary>
/// Tests the proteome subset (AminoAcid / Peptide / Fragmentation / Modification) ported for
/// the SeeMS Annotation.cs fragment-ion drawing.
/// </summary>
[TestClass]
public class PeptideFragmentationTests
{
    [TestMethod]
    public void AminoAcid_LookupAndComposition()
    {
        // Symbols and full-formula composition match the cpp AminoAcid.cpp tables.
        Assert.AreEqual('A', AminoAcidInfo.Record(AminoAcid.Alanine).Symbol);
        Assert.AreEqual("Cysteine", AminoAcidInfo.Record('C').Name);
        Assert.IsTrue(AminoAcidInfo.IsKnownSymbol('K'));
        Assert.IsFalse(AminoAcidInfo.IsKnownSymbol('@'));

        // Glycine residue C2 H3 N1 O1; full G = residue + H2O = C2 H5 N1 O2
        var gly = AminoAcidInfo.Record(AminoAcid.Glycine);
        Assert.AreEqual(2, gly.ResidueFormula[ElementType.C]);
        Assert.AreEqual(5, gly.FullFormula[ElementType.H]);

        // Throw on unknown symbol.
        Assert.ThrowsException<ArgumentException>(() => AminoAcidInfo.Record('@'));
    }

    [TestMethod]
    public void Peptide_MonoisotopicMass_KnownValue_PEPTIDE()
    {
        // Reference: PEPTIDE neutral mono mass = 799.36 Da (industry-standard sanity check).
        // Via residues: P+E+P+T+I+D+E + H2O.
        var pep = new Peptide("PEPTIDE");
        Assert.AreEqual(799.35994, pep.MonoisotopicMass(), 1e-3);
        // [M+H]+ = (799.35994 + 1.00728) / 1.
        Assert.AreEqual(800.36722, pep.MonoisotopicMass(charge: 1), 1e-3);
    }

    [TestMethod]
    public void Peptide_InlineModificationParsing()
    {
        // Auto-parse: parentheses with formula → add to that residue.
        // PEP(O)TIDE = PEPTIDE + 1× oxygen on residue index 2 (the second P).
        var oxidized = new Peptide("PEP(O)TIDE", ModificationParsing.Auto);
        Assert.AreEqual("PEPTIDE", oxidized.Sequence);
        Assert.IsTrue(oxidized.Modifications.ContainsKey(2));
        // Mass should be PEPTIDE mass + 15.99491 (one oxygen).
        Assert.AreEqual(799.35994 + 15.99491, oxidized.MonoisotopicMass(), 1e-3);

        // Mass-form modification: PEP[15.99]TIDE in brackets.
        var byMass = new Peptide("PEP[15.99]TIDE",
            ModificationParsing.ByMass, ModificationDelimiter.Brackets);
        Assert.AreEqual("PEPTIDE", byMass.Sequence);
        Assert.AreEqual(15.99, byMass.Modifications[2][0].MonoisotopicDeltaMass, 1e-9);
    }

    [TestMethod]
    public void Fragmentation_BAndYIonMasses_PEPTIDE()
    {
        // Standard +1 fragment-ion masses for PEPTIDE (matrix-science fragment-ion calculator).
        // cpp's fragmentation.b(i, 0) returns NEUTRAL = (residues sum) for b and (residues sum +
        // H2O) for y, so the test charges to +1 to compare against canonical literature values.
        //
        //   b1 (P)+   = 98.060034   y1 (E)+ = 148.060434
        //   b2 (PE)+  = 227.102640  y2 (DE)+ = 263.087377
        //   b3 (PEP)+ = 324.155421
        var frag = new Peptide("PEPTIDE").Fragmentation();
        Assert.AreEqual(98.060034, frag.B(1, 1), 1e-3, "b1+");
        Assert.AreEqual(227.102640, frag.B(2, 1), 1e-3, "b2+");
        Assert.AreEqual(324.155421, frag.B(3, 1), 1e-3, "b3+");
        Assert.AreEqual(148.060434, frag.Y(1, 1), 1e-3, "y1+");
        Assert.AreEqual(263.087377, frag.Y(2, 1), 1e-3, "y2+");

        // Charge-2 m/z = (neutral + 2*proton) / 2; we get neutral via the charge=0 overload.
        double y2_neutral = frag.Y(2, charge: 0);
        Assert.AreEqual((y2_neutral + 2 * PhysicalConstants.Proton) / 2.0,
            frag.Y(2, charge: 2), 1e-6, "y2 2+");

        // c on the full peptide length is invalid (no c-ion exists for full length).
        Assert.ThrowsException<InvalidOperationException>(() => frag.C(7));
    }

    [TestMethod]
    public void Modification_FormulaVsMass_Equivalent()
    {
        // A formula-based mod knows both delta masses; a mass-only mod throws on Formula access.
        var ox = new Modification(new Formula("O"));
        Assert.IsTrue(ox.HasFormula);
        Assert.AreEqual(15.99491, ox.MonoisotopicDeltaMass, 1e-4);

        var massOnly = new Modification(15.99491, 16.0);
        Assert.IsFalse(massOnly.HasFormula);
        Assert.ThrowsException<InvalidOperationException>(() => massOnly.Formula);
        Assert.AreEqual(15.99491, massOnly.MonoisotopicDeltaMass, 1e-9);
        Assert.AreEqual(16.0, massOnly.AverageDeltaMass, 1e-9);
    }
}
