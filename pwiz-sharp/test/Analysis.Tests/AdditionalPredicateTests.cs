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

    [TestMethod]
    public void ChargeState_FilterAndMissing()
    {
        // Filters by an integer set; missing charge is rejected.
        var list = new SpectrumListSimple();
        list.Spectra.Add(MakeMs2(0, 1, 500, CVID.MS_CID));
        list.Spectra.Add(MakeMs2(1, 2, 500, CVID.MS_CID));
        list.Spectra.Add(MakeMs2(2, 3, 500, CVID.MS_CID));
        list.Spectra.Add(MakeMs2(3, null, 500, CVID.MS_CID));

        var set = new IntegerSet();
        set.Insert(2, 3);
        var filtered = new SpectrumListFilter(list, new ChargeStatePredicate(set));
        Assert.AreEqual(2, filtered.Count, "kept charges 2 and 3 only; missing rejected");
    }

    [TestMethod]
    public void ActivationType_HierarchyAndExclusion()
    {
        var list = new SpectrumListSimple();
        list.Spectra.Add(MakeMs2(0, 2, 500, CVID.MS_collision_induced_dissociation));
        list.Spectra.Add(MakeMs2(1, 2, 500, CVID.MS_higher_energy_beam_type_collision_induced_dissociation));
        list.Spectra.Add(MakeMs2(2, 2, 500, CVID.MS_electron_transfer_dissociation));

        // CID matches CID + HCD (HCD is_a CID via the CV hierarchy).
        var cid = new SpectrumListFilter(list,
            new ActivationTypePredicate(new[] { CVID.MS_collision_induced_dissociation }));
        Assert.AreEqual(2, cid.Count, "CID predicate matches CID and HCD descendant");

        // ETD predicate matches only the ETD spectrum (terminal CV, no descendants here).
        var etd = new SpectrumListFilter(list,
            new ActivationTypePredicate(new[] { CVID.MS_electron_transfer_dissociation }));
        Assert.AreEqual(1, etd.Count);
        Assert.AreEqual(2, etd.SpectrumIdentity(0).Index);

        // hasNoneOf inverts the membership: anything NOT in the CID hierarchy survives (just ETD).
        var noneOfCid = new SpectrumListFilter(list,
            new ActivationTypePredicate(
                new[] { CVID.MS_collision_induced_dissociation }, hasNoneOf: true));
        Assert.AreEqual(1, noneOfCid.Count);
        Assert.AreEqual(2, noneOfCid.SpectrumIdentity(0).Index);
    }

    [TestMethod]
    public void PrecursorMz_ToleranceAndExcludeMode()
    {
        // Within 5 ppm of 500.0 → match 500.0 exactly + 500.002 (4 ppm off); skip 800.
        var ppmList = new SpectrumListSimple();
        ppmList.Spectra.Add(MakeMs2(0, 2, 500.0, CVID.MS_CID));
        ppmList.Spectra.Add(MakeMs2(1, 2, 500.002, CVID.MS_CID));
        ppmList.Spectra.Add(MakeMs2(2, 2, 800.0, CVID.MS_CID));
        Assert.AreEqual(2, new SpectrumListFilter(ppmList,
            new PrecursorMzPredicate(new[] { 500.0 }, new MZTolerance(5, MZToleranceUnits.Ppm))).Count);

        // FilterMode.Exclude inverts: keep what doesn't match.
        var excludeList = new SpectrumListSimple();
        excludeList.Spectra.Add(MakeMs2(0, 2, 500.0, CVID.MS_CID));
        excludeList.Spectra.Add(MakeMs2(1, 2, 800.0, CVID.MS_CID));
        var excluded = new SpectrumListFilter(excludeList,
            new PrecursorMzPredicate(new[] { 500.0 }, new MZTolerance(0.01), FilterMode.Exclude));
        Assert.AreEqual(1, excluded.Count);
        Assert.AreEqual(1, excluded.SpectrumIdentity(0).Index);
    }
}
