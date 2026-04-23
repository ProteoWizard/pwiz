using Pwiz.Analysis.Filters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.Filters;

[TestClass]
public class AdditionalPredicateTests
{
    private static Spectrum MakeMs2(int index, int? charge, double? precursorMz, CVID? activation)
    {
        var s = new Spectrum { Index = index, Id = $"scan={index + 1}" };
        s.Params.Set(CVID.MS_ms_level, 2);
        var precursor = new Precursor();
        var si = new SelectedIon();
        if (precursorMz is not null) si.Set(CVID.MS_selected_ion_m_z, precursorMz.Value, CVID.MS_m_z);
        if (charge is not null) si.Set(CVID.MS_charge_state, charge.Value);
        precursor.SelectedIons.Add(si);
        if (activation is not null) precursor.Activation.Set(activation.Value);
        s.Precursors.Add(precursor);
        return s;
    }

    // ---- ChargeState ----

    [TestMethod]
    public void ChargeState_FilterByCharge()
    {
        var list = new SpectrumListSimple();
        list.Spectra.Add(MakeMs2(0, 1, 500, CVID.MS_CID));
        list.Spectra.Add(MakeMs2(1, 2, 500, CVID.MS_CID));
        list.Spectra.Add(MakeMs2(2, 3, 500, CVID.MS_CID));

        var set = new IntegerSet();
        set.Insert(2, 3);
        var filtered = new SpectrumListFilter(list, new ChargeStatePredicate(set));
        Assert.AreEqual(2, filtered.Count);
    }

    [TestMethod]
    public void ChargeState_MissingCharge_Rejected()
    {
        var list = new SpectrumListSimple();
        list.Spectra.Add(MakeMs2(0, null, 500, CVID.MS_CID));

        var set = new IntegerSet();
        set.Insert(2);
        var filtered = new SpectrumListFilter(list, new ChargeStatePredicate(set));
        Assert.AreEqual(0, filtered.Count);
    }

    // ---- ActivationType ----

    [TestMethod]
    public void ActivationType_MatchesCidAndChildrenViaCvHierarchy()
    {
        // HCD is_a beam-type CID is_a CID, so a predicate targeting CID accepts HCD too.
        // This is the intended behavior — pwiz's ActivationType predicate uses CV hierarchy,
        // not exact-term matching. Use a terminal term to exclude children.
        var list = new SpectrumListSimple();
        list.Spectra.Add(MakeMs2(0, 2, 500, CVID.MS_collision_induced_dissociation));
        list.Spectra.Add(MakeMs2(1, 2, 500, CVID.MS_higher_energy_beam_type_collision_induced_dissociation));
        list.Spectra.Add(MakeMs2(2, 2, 500, CVID.MS_electron_transfer_dissociation));

        // Filter for CID → matches CID and its HCD descendant (2 spectra).
        var cidMatches = new SpectrumListFilter(list,
            new ActivationTypePredicate(new[] { CVID.MS_collision_induced_dissociation }));
        Assert.AreEqual(2, cidMatches.Count);

        // Filter for only ETD → matches just that spectrum.
        var etdMatches = new SpectrumListFilter(list,
            new ActivationTypePredicate(new[] { CVID.MS_electron_transfer_dissociation }));
        Assert.AreEqual(1, etdMatches.Count);
        Assert.AreEqual(2, etdMatches.SpectrumIdentity(0).Index);
    }

    [TestMethod]
    public void ActivationType_NoneOf_Inverts()
    {
        var list = new SpectrumListSimple();
        list.Spectra.Add(MakeMs2(0, 2, 500, CVID.MS_collision_induced_dissociation));
        list.Spectra.Add(MakeMs2(1, 2, 500, CVID.MS_electron_transfer_dissociation));

        var filtered = new SpectrumListFilter(list,
            new ActivationTypePredicate(
                new[] { CVID.MS_collision_induced_dissociation }, hasNoneOf: true));
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual(1, filtered.SpectrumIdentity(0).Index); // the ETD one survives
    }

    // ---- PrecursorMz ----

    [TestMethod]
    public void PrecursorMz_MatchesWithinTolerance()
    {
        var list = new SpectrumListSimple();
        list.Spectra.Add(MakeMs2(0, 2, 500.0, CVID.MS_CID));
        list.Spectra.Add(MakeMs2(1, 2, 500.002, CVID.MS_CID)); // 4 ppm off
        list.Spectra.Add(MakeMs2(2, 2, 800.0, CVID.MS_CID));

        var filtered = new SpectrumListFilter(list,
            new PrecursorMzPredicate(new[] { 500.0 }, new MZTolerance(5, MZToleranceUnits.Ppm)));
        Assert.AreEqual(2, filtered.Count); // 500.0 exact and 500.002 (within 5ppm)
    }

    [TestMethod]
    public void PrecursorMz_ExcludeMode_Inverts()
    {
        var list = new SpectrumListSimple();
        list.Spectra.Add(MakeMs2(0, 2, 500.0, CVID.MS_CID));
        list.Spectra.Add(MakeMs2(1, 2, 800.0, CVID.MS_CID));

        var filtered = new SpectrumListFilter(list,
            new PrecursorMzPredicate(new[] { 500.0 }, new MZTolerance(0.01), FilterMode.Exclude));
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual(1, filtered.SpectrumIdentity(0).Index);
    }
}
