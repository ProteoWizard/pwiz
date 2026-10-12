using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mgf;
using Pwiz.Data.MsData.MSn;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.MzXml;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Verifies that <see cref="MzmlWriter"/>, <see cref="MzxmlWriter"/>, and
/// <see cref="MgfSerializer"/> broadcast a per-spectrum <see cref="IterationUpdate"/> through
/// the configured <see cref="IterationListenerRegistry"/>. This is what powers
/// msconvert-sharp's <c>-v</c> progress output. Also covers how every writer's spectrum loop
/// handles a spectrum that fails to load, with and without <see cref="WriteConfig.ContinueOnError"/>.
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

    /// <summary>Decorates a healthy list and throws for one index, the way a vendor reader fails
    /// on a scan it cannot read. Identity still resolves, as it does for a real reader that can
    /// enumerate a scan it cannot load.</summary>
    private sealed class UnreadableSpectrumList : SpectrumListBase
    {
        private readonly ISpectrumList _inner;
        private readonly int _badIndex;

        public UnreadableSpectrumList(ISpectrumList inner, int badIndex)
        {
            _inner = inner;
            _badIndex = badIndex;
        }

        public override int Count => _inner.Count;

        public override SpectrumIdentity SpectrumIdentity(int index) => _inner.SpectrumIdentity(index);

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
            index == _badIndex
                ? throw new InvalidOperationException("unreadable scan")
                : _inner.GetSpectrum(index, getBinaryData);
    }

    // Every format with a writer that streams spectra one at a time. mzMLb shares MzmlWriter's loop.
    private static readonly WriteFormat[] s_streamedFormats =
        { WriteFormat.Mzml, WriteFormat.MzXml, WriteFormat.Mgf, WriteFormat.Ms2, WriteFormat.Cms2 };

    private const int BadIndex = 1;

    private static MSData BuildDocWithUnreadableSpectrum()
    {
        var msd = BuildMsnDocWithSpectra(3);
        msd.Run.SpectrumList = new UnreadableSpectrumList(msd.Run.SpectrumList!, BadIndex);
        return msd;
    }

    private static byte[] WriteThroughMSDataFile(MSData msd, WriteFormat format, bool continueOnError)
    {
        using var ms = new MemoryStream();
        MSDataFile.Write(msd, ms, new WriteConfig { Format = format, ContinueOnError = continueOnError });
        return ms.ToArray();
    }

    private static MSData ReadBack(byte[] bytes, WriteFormat format)
    {
        using var ms = new MemoryStream(bytes);
        var msd = new MSData();
        switch (format)
        {
            case WriteFormat.Mzml:
                return new MzmlReader().Read(ms);
            case WriteFormat.MzXml:
                MzxmlReader.Read(ms, msd);
                return msd;
            case WriteFormat.Mgf:
                using (var reader = new StreamReader(ms))
                    return new MgfSerializer().Read(reader);
            case WriteFormat.Ms2:
                new SerializerMSn(MSnType.Ms2).Read(ms, msd);
                return msd;
            case WriteFormat.Cms2:
                new SerializerMSn(MSnType.Cms2).Read(ms, msd);
                return msd;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    [TestMethod]
    public void AllWriters_ContinueOnError_SkipTheUnreadableSpectrumAndKeepTheRest()
    {
        foreach (var format in s_streamedFormats)
        {
            byte[] bytes = WriteThroughMSDataFile(BuildDocWithUnreadableSpectrum(), format, continueOnError: true);

            var sl = ReadBack(bytes, format).Run.SpectrumList!;
            // The fixture's first peak is 100 + index, so it identifies which spectra survived
            // in every format, including the ones that do not keep the nativeID.
            var firstMzs = Enumerable.Range(0, sl.Count)
                .Select(i => sl.GetSpectrum(i, getBinaryData: true).GetMZArray()!.Data[0])
                .ToArray();
            CollectionAssert.AreEqual(new[] { 100.0, 102.0 }, firstMzs, $"{format}: surviving spectra");

            // mzML is the only one of these formats that stores spectrum indices, so it is the one
            // that has to renumber the survivors to leave no gap.
            if (format == WriteFormat.Mzml)
            {
                for (int i = 0; i < sl.Count; i++)
                    Assert.AreEqual(i, sl.GetSpectrum(i).Index, $"{format}: index of survivor {i}");
            }
        }
    }

    [TestMethod]
    public void AllWriters_ContinueOnError_LogTheSkippedSpectrumById()
    {
        const string expected = "Skipping spectrum 1 \"controllerType=0 controllerNumber=1 scan=2\": unreadable scan";
        var writers = new (string Format, Action<MSData, TextWriter> Write)[]
        {
            ("mzML", (msd, log) => new MzmlWriter { ContinueOnError = true, SkipLog = log }.Write(msd)),
            ("mzXML", (msd, log) => new MzxmlWriter { ContinueOnError = true, SkipLog = log }.Write(msd)),
            ("MGF", (msd, log) => new MgfSerializer { ContinueOnError = true, SkipLog = log }.Write(msd)),
            ("MS2", (msd, log) => new SerializerMSn(MSnType.Ms2) { ContinueOnError = true, SkipLog = log }.Write(msd, Stream.Null)),
            ("CMS2", (msd, log) => new SerializerMSn(MSnType.Cms2) { ContinueOnError = true, SkipLog = log }.Write(msd, Stream.Null)),
        };
        foreach (var (format, write) in writers)
        {
            using var log = new StringWriter();
            write(BuildDocWithUnreadableSpectrum(), log);
            Assert.AreEqual(expected, log.ToString().TrimEnd(), $"{format}: skip diagnostic");
        }
    }

    [TestMethod]
    public void AllWriters_WithoutContinueOnError_ThrowEnumerationException()
    {
        // EnumerationException is what makes msconvert suggest re-running with --continueOnError.
        foreach (var format in s_streamedFormats)
        {
            var ex = Assert.ThrowsException<EnumerationException>(
                () => WriteThroughMSDataFile(BuildDocWithUnreadableSpectrum(), format, continueOnError: false),
                $"{format}: an unreadable spectrum must stop the write");
            Assert.IsInstanceOfType<InvalidOperationException>(ex.InnerException, $"{format}: inner exception");
        }
    }
}
