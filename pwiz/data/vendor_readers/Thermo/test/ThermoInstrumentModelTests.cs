using Pwiz.Data.Common.Cv;
using Pwiz.Vendor.Thermo;

namespace Pwiz.Vendor.Thermo.Tests;

/// <summary>
/// Mirrors the instrument-model translation block of pwiz cpp <c>Reader_Thermo_Test.cpp</c>
/// (lines 73–155 in the version of the test that asserts every Thermo CV term has a
/// translation entry and every entry maps to a non-Unknown CV term).
/// </summary>
[TestClass]
public class ThermoInstrumentModelTests
{
    [TestMethod]
    public void Translate_KnownModels_ReturnExpectedCvids()
    {
        // A spread of representative inputs covering Exact / ExactNoSpaces / Contains.
        var cases = new[]
        {
            ("LTQ FT",                 CVID.MS_LTQ_FT),
            ("LTQ-FT",                 CVID.MS_LTQ_FT),
            ("LTQ Orbitrap Velos",     CVID.MS_LTQ_Orbitrap_Velos),     // Contains "ORBITRAP VELOS"
            ("Orbitrap Fusion Lumos",  CVID.MS_Orbitrap_Fusion_Lumos),  // Contains "FUSION LUMOS" (must beat "FUSION")
            ("Orbitrap Eclipse",       CVID.MS_Orbitrap_Eclipse),
            ("Orbitrap Exploris 480",  CVID.MS_Orbitrap_Exploris_480),
            ("Q Exactive",             CVID.MS_Q_Exactive),
            ("Q Exactive Plus",        CVID.MS_Q_Exactive_Plus),
            ("Q Exactive HF-X",        CVID.MS_Q_Exactive_HF_X),
            ("MAT253",                 CVID.MS_MAT253),
            ("MAT 253",                CVID.MS_MAT253),                 // ExactNoSpaces — strips spaces
            ("ELEMENT2",               CVID.MS_Element_2),
            ("Surveyor PDA",           CVID.MS_Surveyor_PDA),
            ("Astral Zoom",            CVID.MS_Orbitrap_Astral_Zoom),   // must beat "ASTRAL"
            ("Stellar",                CVID.MS_Stellar),
            ("",                       CVID.MS_Thermo_Electron_instrument_model),
            ("totally unknown",        CVID.MS_Thermo_Electron_instrument_model),
        };
        foreach (var (input, expected) in cases)
            Assert.AreEqual(expected, ThermoInstrumentModel.Translate(input),
                $"Translate(\"{input}\")");
    }

    [TestMethod]
    public void Translate_TableEntriesEachMapToNonUnknown()
    {
        // Mirrors cpp Reader_Thermo_Test "all instrument types are handled by translation" loop:
        // every name in the table must round-trip to a non-Unknown CVID. Catches regressions
        // where someone adds a name pointing at an enum that's been renumbered or where the
        // catch-all returned MS_Thermo_Electron_instrument_model unexpectedly.
        foreach (var (name, expected) in ThermoInstrumentModel.EnumerateMappings())
        {
            // Verify the table self-consistency: applying Translate to a name should produce its
            // declared CVID (not the catch-all and not a different mapping due to ordering).
            CVID actual = ThermoInstrumentModel.Translate(name);
            Assert.AreEqual(expected, actual,
                $"\"{name}\" was declared to map to {expected} but Translate returned {actual}. " +
                $"Likely a Contains/Exact ordering issue in the table.");
            Assert.AreNotEqual(CVID.CVID_Unknown, actual, $"\"{name}\" mapped to CVID_Unknown");
        }
    }

    [TestMethod]
    public void Translate_TableCoversAllThermoCvids()
    {
        // Mirrors cpp Reader_Thermo_Test "every Thermo CV term has a name mapped to it" check:
        // every CV term that is_a Thermo*_instrument_model should appear at least once as the
        // Cvid of some entry in the translation table.
        var thermoBrands = new HashSet<CVID>
        {
            CVID.MS_Thermo_Finnigan_instrument_model,
            CVID.MS_Thermo_Electron_instrument_model,
            CVID.MS_Thermo_Scientific_instrument_model,
            CVID.MS_Finnigan_MAT_instrument_model,
        };

        var allThermoInstruments = new HashSet<CVID>();
        foreach (var cvid in CvLookup.AllCvids)
        {
            if (thermoBrands.Contains(cvid)) continue;
            if (thermoBrands.Any(brand => CvLookup.CvIsA(cvid, brand)))
                allThermoInstruments.Add(cvid);
        }

        var mappedCvids = new HashSet<CVID>();
        foreach (var (_, cvid) in ThermoInstrumentModel.EnumerateMappings())
            mappedCvids.Add(cvid);

        var unmapped = allThermoInstruments
            .Where(c => !mappedCvids.Contains(c))
            .Select(c => CvLookup.CvTermInfo(c).Name)
            .OrderBy(n => n)
            .ToList();

        if (unmapped.Count > 0)
        {
            Assert.Fail(
                $"{unmapped.Count} Thermo instrument CV term(s) have no entry in ThermoInstrumentModel:\n  " +
                string.Join("\n  ", unmapped));
        }
    }
}
