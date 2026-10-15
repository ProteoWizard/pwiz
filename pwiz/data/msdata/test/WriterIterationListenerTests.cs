using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mgf;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.MzXml;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Verifies that <see cref="MzmlWriter"/>, <see cref="MzxmlWriter"/>, and
/// <see cref="MgfSerializer"/> broadcast a per-spectrum <see cref="IterationUpdate"/> through
/// the configured <see cref="IterationListenerRegistry"/>. This is what powers
/// msconvert-sharp's <c>-v</c> progress output.
/// </summary>
[TestClass]
public class WriterIterationListenerTests
{
    private sealed class RecordingListener : IIterationListener
    {
        public List<IterationUpdate> Updates { get; } = new();
        public IterationStatus Update(IterationUpdate message) { Updates.Add(message); return IterationStatus.Ok; }
    }

    private static MSData BuildMsnDocWithSpectra(int count, int msLevel = 2)
    {
        var msd = new MSData();
        msd.CVs.AddRange(MSData.DefaultCVList);
        var list = new SpectrumListSimple();
        for (int i = 0; i < count; i++)
        {
            var spec = new Spectrum
            {
                Index = i,
                Id = $"controllerType=0 controllerNumber=1 scan={i + 1}",
                DefaultArrayLength = 2,
            };
            spec.Params.Set(CVID.MS_ms_level, msLevel);
            spec.Params.Set(CVID.MS_MSn_spectrum);
            spec.Params.Set(CVID.MS_centroid_spectrum);
            spec.Params.Set(CVID.MS_positive_scan);
            spec.Params.Set(CVID.MS_spectrum_title, $"spec-{i}");

            var scan = new Scan();
            scan.Set(CVID.MS_scan_start_time, i, CVID.UO_second);
            spec.ScanList.Scans.Add(scan);

            // MGF needs a precursor + selected ion to actually emit anything.
            var p = new Precursor();
            var si = new SelectedIon();
            si.Set(CVID.MS_selected_ion_m_z, 500.0 + i, CVID.MS_m_z);
            si.Set(CVID.MS_charge_state, 2);
            p.SelectedIons.Add(si);
            spec.Precursors.Add(p);

            spec.SetMZIntensityArrays(
                new[] { 100.0 + i, 200.0 + i },
                new[] { 50.0, 100.0 },
                CVID.MS_number_of_detector_counts);

            list.Spectra.Add(spec);
        }
        msd.Run.SpectrumList = list;
        return msd;
    }

    [TestMethod]
    public void AllWriters_BroadcastOneUpdatePerSpectrum_WithCorrectShape()
    {
        const int count = 5;
        var msd = BuildMsnDocWithSpectra(count);

        // mzML.
        var mzmlReg = new IterationListenerRegistry();
        var mzmlListener = new RecordingListener();
        mzmlReg.AddListener(mzmlListener, iterationPeriod: 1);
        new MzmlWriter { IterationListenerRegistry = mzmlReg }.Write(msd);
        Assert.AreEqual(count, mzmlListener.Updates.Count, "mzML: one update per spectrum");
        for (int i = 0; i < count; i++)
        {
            Assert.AreEqual(i, mzmlListener.Updates[i].IterationIndex, $"mzML[{i}] index");
            Assert.AreEqual(count, mzmlListener.Updates[i].IterationCount, $"mzML[{i}] count");
        }

        // mzXML.
        var mzxmlReg = new IterationListenerRegistry();
        var mzxmlListener = new RecordingListener();
        mzxmlReg.AddListener(mzxmlListener, iterationPeriod: 1);
        new MzxmlWriter { IterationListenerRegistry = mzxmlReg }.Write(msd);
        Assert.AreEqual(count, mzxmlListener.Updates.Count, "mzXML: one update per spectrum");

        // MGF.
        var mgfReg = new IterationListenerRegistry();
        var mgfListener = new RecordingListener();
        mgfReg.AddListener(mgfListener, iterationPeriod: 1);
        new MgfSerializer { IterationListenerRegistry = mgfReg }.Write(msd);
        Assert.AreEqual(count, mgfListener.Updates.Count, "MGF: one update per input spectrum");
    }

    [TestMethod]
    public void Writers_WithNoListenerRegistry_StillWorkUnchanged()
    {
        // Sanity: leaving IterationListenerRegistry null is a no-op — writers must not crash and
        // must still emit normal output. (Default-null is the path most callers will hit.)
        var msd = BuildMsnDocWithSpectra(2);

        string mzml = new MzmlWriter().Write(msd);
        Assert.IsTrue(mzml.Contains("<mzML", StringComparison.Ordinal));

        string mzxml = new MzxmlWriter().Write(msd);
        Assert.IsTrue(mzxml.Contains("<mzXML", StringComparison.Ordinal));

        string mgf = new MgfSerializer().Write(msd);
        Assert.IsTrue(mgf.Contains("BEGIN IONS", StringComparison.Ordinal));
    }
}
