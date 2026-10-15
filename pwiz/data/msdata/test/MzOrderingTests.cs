using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Peak-order repair on read - <c>SpectrumListBase.EnsureMzAscending</c> - and the axes it has to
/// leave alone. Mirrors cpp's SpectrumListBaseTest.cpp.
/// </summary>
[TestClass]
public class MzOrderingTests
{
    /// <summary>
    /// One test method for the whole rule, because the cases only mean something as a set: each
    /// "left alone" case is only interesting next to the "repaired" case it could be confused
    /// with, and half of them exist to show that an exempt spectrum does not settle the verdict
    /// for the spectrum after it.
    /// </summary>
    [TestMethod]
    public void PeaksAreOrderedByMzExceptWhereTheOrderMeansSomethingElse()
    {
        UnsortedSpectrumIsReordered();
        SortedSpectrumIsUntouched();
        ShortOrderedLeaderDoesNotSettleTheFile();
        CondemnedFileStaysCondemned();
        CombinedIonMobilitySpectrumIsLeftAlone();
        CombinedIonMobilitySpectrumDoesNotVouchForTheWriter();
        WavelengthSpectrumIsNeitherSortedNorEvidence();
        ExtraPerPeakArraysTravelWithTheirPeaks();
        SrmSpectrumIsLeftAlone();
        SrmSpectrumDoesNotVouchForTheWriter();
        SimSpectrumIsStillRepaired();
        MetadataOnlySpectrumSettlesNothing();
    }

    /// <summary>
    /// The defect this exists for: peaks stored in ascending intensity rather than ascending m/z.
    /// Every intensity must still come back attached to the m/z it arrived with - sorting the m/z
    /// axis alone would leave each value plausible and each pairing wrong, a worse failure than
    /// the unsorted input.
    /// </summary>
    private static void UnsortedSpectrumIsReordered()
    {
        var list = new ReaderListStub();
        list.Add(MakeSpectrum("scan=1", UNSORTED_MZS, UNSORTED_INTENSITIES));

        var spectrum = list.GetSpectrum(0, true);
        CollectionAssert.AreEqual(new[] { 200.2, 300.3, 500.5, 700.7 }, MzsOf(spectrum));
        CollectionAssert.AreEqual(new[] { 40.0, 20.0, 10.0, 30.0 }, IntensitiesOf(spectrum));
    }

    /// <summary>A file already in m/z order comes through untouched.</summary>
    private static void SortedSpectrumIsUntouched()
    {
        var mzs = Ascending(20);
        var intensities = Constant(20, 1.0);

        var list = new ReaderListStub();
        list.Add(MakeSpectrum("scan=1", mzs, intensities));

        var spectrum = list.GetSpectrum(0, true);
        CollectionAssert.AreEqual(mzs, MzsOf(spectrum));
        CollectionAssert.AreEqual(intensities, IntensitiesOf(spectrum));
    }

    /// <summary>
    /// The case a first-spectrum-only probe gets wrong. A short leading spectrum that happens to
    /// ascend says nothing about the writer - early scans can precede the sample and carry almost
    /// no peaks - so the checking has to continue until a spectrum with enough peaks settles it.
    /// </summary>
    private static void ShortOrderedLeaderDoesNotSettleTheFile()
    {
        var shortMzs = new[] { 150.1, 250.2, 350.3 };
        var shortIntensities = new[] { 5.0, 7.0, 9.0 };

        var list = new ReaderListStub();
        list.Add(MakeSpectrum("scan=1", shortMzs, shortIntensities));
        list.Add(MakeSpectrum("scan=2", UNSORTED_MZS, UNSORTED_INTENSITIES));

        CollectionAssert.AreEqual(shortMzs, MzsOf(list.GetSpectrum(0, true)));
        Assert.AreEqual(200.2, MzsOf(list.GetSpectrum(1, true))[0], 1e-9);
    }

    /// <summary>
    /// Once a file is condemned it stays condemned, so a later spectrum that happens to ascend
    /// cannot switch the checking off and let the ones after it through in writer order.
    /// </summary>
    private static void CondemnedFileStaysCondemned()
    {
        var list = new ReaderListStub();
        list.Add(MakeSpectrum("scan=1", UNSORTED_MZS, UNSORTED_INTENSITIES));
        list.Add(MakeSpectrum("scan=2", Ascending(20), Constant(20, 1.0)));
        list.Add(MakeSpectrum("scan=3", UNSORTED_MZS, UNSORTED_INTENSITIES));

        list.GetSpectrum(0, true);
        list.GetSpectrum(1, true);
        Assert.AreEqual(200.2, MzsOf(list.GetSpectrum(2, true))[0], 1e-9);
    }

    /// <summary>
    /// A combined ion mobility spectrum is legitimately ordered by m/z only within each mobility
    /// bin, so it must be left exactly as it is - a global sort would shred the bin structure.
    /// </summary>
    private static void CombinedIonMobilitySpectrumIsLeftAlone()
    {
        // 200.2 rolls over into the next mobility bin
        var mzs = new[] { 500.5, 600.6, 200.2, 300.3 };
        var intensities = new[] { 10.0, 20.0, 30.0, 40.0 };

        var list = new ReaderListStub();
        list.Add(MakeSpectrum("merged=1", mzs, intensities,
            CVID.MS_mean_inverse_reduced_ion_mobility_array));

        var spectrum = list.GetSpectrum(0, true);
        CollectionAssert.AreEqual(mzs, MzsOf(spectrum));
        CollectionAssert.AreEqual(intensities, IntensitiesOf(spectrum));
    }

    /// <summary>
    /// The rollover guard has to run ahead of the verdict, not just ahead of the sort. An ion
    /// mobility spectrum with enough peaks to settle the question would otherwise vouch for a
    /// writer it says nothing about, switching the checking off for the rest of the file.
    /// </summary>
    private static void CombinedIonMobilitySpectrumDoesNotVouchForTheWriter()
    {
        var imsMzs = Ascending(20);

        var list = new ReaderListStub();
        list.Add(MakeSpectrum("merged=1", imsMzs, Constant(20, 1.0),
            CVID.MS_mean_inverse_reduced_ion_mobility_array));
        list.Add(MakeSpectrum("scan=2", UNSORTED_MZS, UNSORTED_INTENSITIES));

        CollectionAssert.AreEqual(imsMzs, MzsOf(list.GetSpectrum(0, true)));
        Assert.AreEqual(200.2, MzsOf(list.GetSpectrum(1, true))[0], 1e-9);
    }

    /// <summary>
    /// <see cref="Spectrum.GetMZArray"/> returns a wavelength array too, so a diode-array trace
    /// would otherwise be judged as if its wavelength axis were m/z - and an ascending UV trace
    /// with enough points would settle the verdict and disable the repair for every real spectrum
    /// in the file.
    /// </summary>
    private static void WavelengthSpectrumIsNeitherSortedNorEvidence()
    {
        var descending = new double[20];
        for (int i = 0; i < descending.Length; i++)
            descending[i] = 400.0 - i;

        var uv = new Spectrum { Id = "scan=1", DefaultArrayLength = descending.Length };
        uv.Params.Set(CVID.MS_EMR_spectrum);
        uv.SetMZIntensityArrays(descending, Constant(20, 1.0), CVID.MS_number_of_detector_counts);
        // Held before the params are cleared, since matching on them is how GetMZArray finds it.
        var xArray = uv.GetMZArray()!;
        xArray.CVParams.Clear();
        xArray.Set(CVID.MS_wavelength_array);

        var list = new ReaderListStub();
        list.Add(uv);
        list.Add(MakeSpectrum("scan=2", UNSORTED_MZS, UNSORTED_INTENSITIES));

        // The UV trace is left exactly as written, descending and all.
        CollectionAssert.AreEqual(descending, MzsOf(list.GetSpectrum(0, true)));

        // And it neither condemned nor vouched for the file.
        Assert.AreEqual(200.2, MzsOf(list.GetSpectrum(1, true))[0], 1e-9);
    }

    /// <summary>
    /// Extra per-peak arrays are ordinary - signal-to-noise, baseline, resolution, charge - and
    /// none is an ordering axis. Every one has to travel with the peak it belongs to; an integer
    /// array is a separate member of <see cref="Spectrum"/> and is just as per-peak.
    /// </summary>
    private static void ExtraPerPeakArraysTravelWithTheirPeaks()
    {
        var spectrum = MakeSpectrum("scan=1", UNSORTED_MZS, UNSORTED_INTENSITIES,
            CVID.MS_signal_to_noise_array);
        var snrArray = spectrum.BinaryDataArrays[^1];
        snrArray.Data.Clear();
        snrArray.Data.AddRange(new[] { 5.0, 6.0, 7.0, 8.0 });

        var chargeArray = new IntegerDataArray();
        chargeArray.Set(CVID.MS_charge_array);
        chargeArray.Data.AddRange(new[] { 1L, 2L, 3L, 4L });
        spectrum.IntegerDataArrays.Add(chargeArray);

        // Shorter than the peak count, so it holds no per-peak value and must be left exactly as
        // it is. Length is the only thing that tells the two kinds of extra array apart.
        var notPerPeak = new BinaryDataArray();
        notPerPeak.Set(CVID.MS_baseline_array);
        notPerPeak.Data.AddRange(new[] { 111.1, 222.2 });
        spectrum.BinaryDataArrays.Add(notPerPeak);

        var list = new ReaderListStub();
        list.Add(spectrum);
        var result = list.GetSpectrum(0, true);

        CollectionAssert.AreEqual(new[] { 200.2, 300.3, 500.5, 700.7 }, MzsOf(result));
        // 8 belonged to 200.2, 6 to 300.3, 5 to 500.5, 7 to 700.7.
        CollectionAssert.AreEqual(new[] { 8.0, 6.0, 5.0, 7.0 }, snrArray.Data.ToArray());
        CollectionAssert.AreEqual(new[] { 4L, 2L, 1L, 3L }, chargeArray.Data.ToArray());
        CollectionAssert.AreEqual(new[] { 111.1, 222.2 }, notPerPeak.Data.ToArray());
    }

    /// <summary>
    /// An SRM spectrum lists one point per transition in the order the method defined them, not
    /// peaks along an m/z continuum. That order is the meaningful one, so it must be left exactly
    /// as written even when the transitions were not set up in ascending m/z.
    /// </summary>
    private static void SrmSpectrumIsLeftAlone()
    {
        var spectrum = MakeSpectrum("sample=1 period=1 cycle=1 experiment=1",
            UNSORTED_MZS, UNSORTED_INTENSITIES);
        spectrum.Params.CVParams.Clear();
        spectrum.Params.Set(CVID.MS_SRM_spectrum);

        var list = new ReaderListStub();
        list.Add(spectrum);

        var result = list.GetSpectrum(0, true);
        CollectionAssert.AreEqual(UNSORTED_MZS, MzsOf(result));
        CollectionAssert.AreEqual(UNSORTED_INTENSITIES, IntensitiesOf(result));
    }

    /// <summary>
    /// The rollover guard has to run ahead of the verdict here too. An SRM spectrum with enough
    /// transitions to settle the question by chance ascending would otherwise vouch for a writer
    /// it says nothing about, switching the checking off for a real spectrum later in the same
    /// file. Uses the CRM term so both exempt terms are covered.
    /// </summary>
    private static void SrmSpectrumDoesNotVouchForTheWriter()
    {
        var srmMzs = Ascending(20);
        var srm = MakeSpectrum("sample=1 period=1 cycle=1 experiment=1", srmMzs, Constant(20, 1.0));
        srm.Params.CVParams.Clear();
        srm.Params.Set(CVID.MS_CRM_spectrum);

        var list = new ReaderListStub();
        list.Add(srm);
        list.Add(MakeSpectrum("scan=2", UNSORTED_MZS, UNSORTED_INTENSITIES));

        CollectionAssert.AreEqual(srmMzs, MzsOf(list.GetSpectrum(0, true)));
        Assert.AreEqual(200.2, MzsOf(list.GetSpectrum(1, true))[0], 1e-9);
    }

    /// <summary>
    /// MS_SIM_spectrum must NOT be exempt, however much it resembles the SRM case. The readers use
    /// the term overwhelmingly for ordinary scans rather than transition lists - Thermo tags every
    /// ScanType_SIM scan with it, and Agilent maps MSScanType_TotalIon, a full-range MS1, to it -
    /// so exempting it would switch the repair off for continuum spectra carrying hundreds of
    /// points.
    /// </summary>
    private static void SimSpectrumIsStillRepaired()
    {
        var sim = MakeSpectrum("controllerType=0 controllerNumber=1 scan=1",
            UNSORTED_MZS, UNSORTED_INTENSITIES);
        sim.Params.CVParams.Clear();
        sim.Params.Set(CVID.MS_SIM_spectrum);

        var list = new ReaderListStub();
        list.Add(sim);

        var result = list.GetSpectrum(0, true);
        CollectionAssert.AreEqual(new[] { 200.2, 300.3, 500.5, 700.7 }, MzsOf(result));
        CollectionAssert.AreEqual(new[] { 40.0, 20.0, 10.0, 30.0 }, IntensitiesOf(result));
    }

    /// <summary>
    /// A metadata-only read carries the array objects with their cvParams and no data, and
    /// consumers do walk whole files that way. Such a spectrum says nothing about the writer -
    /// settling on one would switch the checking off for every real spectrum after it.
    /// </summary>
    private static void MetadataOnlySpectrumSettlesNothing()
    {
        var list = new ReaderListStub();
        list.Add(MakeSpectrum("scan=1", Array.Empty<double>(), Array.Empty<double>()));
        list.Add(MakeSpectrum("scan=2", UNSORTED_MZS, UNSORTED_INTENSITIES));

        Assert.AreEqual(0, MzsOf(list.GetSpectrum(0, true)).Length);
        Assert.AreEqual(200.2, MzsOf(list.GetSpectrum(1, true))[0], 1e-9);
    }

    // Ascending in intensity, which is the order one real writer shipped.
    private static readonly double[] UNSORTED_MZS = { 500.5, 300.3, 700.7, 200.2 };
    private static readonly double[] UNSORTED_INTENSITIES = { 10.0, 20.0, 30.0, 40.0 };

    private static double[] Ascending(int count)
    {
        var values = new double[count];
        for (int i = 0; i < count; i++)
            values[i] = 100.0 + i;
        return values;
    }

    private static double[] Constant(int count, double value)
    {
        var values = new double[count];
        Array.Fill(values, value);
        return values;
    }

    private static Spectrum MakeSpectrum(string id, double[] mzs, double[] intensities,
                                         CVID extraArrayType = CVID.CVID_Unknown)
    {
        var spectrum = new Spectrum { Id = id, DefaultArrayLength = mzs.Length };
        spectrum.Params.Set(CVID.MS_MS1_spectrum);
        spectrum.Params.Set(CVID.MS_ms_level, 1);
        spectrum.SetMZIntensityArrays(mzs, intensities, CVID.MS_number_of_detector_counts);
        if (extraArrayType != CVID.CVID_Unknown)
        {
            var extra = new BinaryDataArray();
            extra.Set(extraArrayType);
            extra.Data.AddRange(Constant(mzs.Length, 1.0));
            spectrum.BinaryDataArrays.Add(extra);
        }
        return spectrum;
    }

    private static double[] MzsOf(Spectrum spectrum) => spectrum.GetMZArray()!.Data.ToArray();

    private static double[] IntensitiesOf(Spectrum spectrum) =>
        spectrum.GetIntensityArray()!.Data.ToArray();

    /// <summary>
    /// A list that reads a format some other tool wrote, standing in for
    /// <c>SpectrumList_Mzml</c> and friends - they call <c>EnsureMzAscending</c> on the way out
    /// of GetSpectrum, which is what these exercise.
    /// </summary>
    private sealed class ReaderListStub : SpectrumListBase
    {
        private readonly List<Spectrum> _spectra = new();

        public override int Count => _spectra.Count;

        public override SpectrumIdentity SpectrumIdentity(int index) =>
            throw new NotSupportedException();

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        {
            var spectrum = _spectra[index];
            EnsureMzAscending(spectrum);
            return spectrum;
        }

        public void Add(Spectrum spectrum)
        {
            spectrum.Index = _spectra.Count;
            _spectra.Add(spectrum);
        }
    }
}
