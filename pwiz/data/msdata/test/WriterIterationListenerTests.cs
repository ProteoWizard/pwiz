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
        private readonly Func<Exception> _failure;

        public UnreadableSpectrumList(ISpectrumList inner, int badIndex, Func<Exception>? failure = null)
        {
            _inner = inner;
            _badIndex = badIndex;
            _failure = failure ?? (() => new InvalidOperationException("unreadable scan"));
        }

        public override int Count => _inner.Count;

        public override SpectrumIdentity SpectrumIdentity(int index) => _inner.SpectrumIdentity(index);

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
            index == _badIndex
                ? throw _failure()
                : _inner.GetSpectrum(index, getBinaryData);
    }

    /// <summary>Chromatogram counterpart of <see cref="UnreadableSpectrumList"/>.</summary>
    private sealed class UnreadableChromatogramList : ChromatogramListBase
    {
        private readonly IChromatogramList _inner;
        private readonly int _badIndex;

        public UnreadableChromatogramList(IChromatogramList inner, int badIndex)
        {
            _inner = inner;
            _badIndex = badIndex;
        }

        public override int Count => _inner.Count;

        public override ChromatogramIdentity ChromatogramIdentity(int index) => _inner.ChromatogramIdentity(index);

        public override Chromatogram GetChromatogram(int index, bool getBinaryData = false) =>
            index == _badIndex
                ? throw new InvalidOperationException("unreadable chromatogram")
                : _inner.GetChromatogram(index, getBinaryData);
    }

    // Every format with a writer that streams spectra one at a time. mzMLb shares MzmlWriter's loop.
    private static readonly WriteFormat[] s_streamedFormats =
        { WriteFormat.Mzml, WriteFormat.MzXml, WriteFormat.Mgf, WriteFormat.Ms2, WriteFormat.Cms2 };

    private const int BadIndex = 1;

    private static MSData BuildDocWithUnreadableSpectrum(Func<Exception>? failure = null)
    {
        var msd = BuildMsnDocWithSpectra(3);
        msd.Run.SpectrumList = new UnreadableSpectrumList(msd.Run.SpectrumList!, BadIndex, failure);
        return msd;
    }

    private static byte[] WriteThroughMSDataFile(MSData msd, WriteFormat format, bool continueOnError,
        IterationListenerRegistry? ilr = null)
    {
        using var ms = new MemoryStream();
        MSDataFile.Write(msd, ms, new WriteConfig { Format = format, ContinueOnError = continueOnError }, ilr);
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

            // mzML is the only one of these formats that stores spectrum indices and a list count,
            // so it is the one that has to renumber the survivors and count only them.
            if (format == WriteFormat.Mzml)
            {
                for (int i = 0; i < sl.Count; i++)
                    Assert.AreEqual(i, sl.GetSpectrum(i).Index, $"{format}: index of survivor {i}");
                Assert.AreEqual(2, ListCount(System.Text.Encoding.UTF8.GetString(bytes), "spectrumList"),
                    $"{format}: spectrumList count");
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
    public void AllWriters_ContinueOnError_StillReportProgressForTheSkippedSpectrum()
    {
        // mzML rewrites the output once a spectrum is skipped, so its list count holds only the
        // spectra written, and progress starts over for that second pass.
        var onePass = new[] { 0, 1, 2 };
        var twoPasses = new[] { 0, 1, 2, 0, 1, 2 };
        var writers = new (string Format, int[] Expected, Action<MSData, IterationListenerRegistry> Write)[]
        {
            ("mzML", twoPasses, (msd, reg) => new MzmlWriter { ContinueOnError = true, SkipLog = TextWriter.Null, IterationListenerRegistry = reg }.Write(msd)),
            ("mzXML", onePass, (msd, reg) => new MzxmlWriter { ContinueOnError = true, SkipLog = TextWriter.Null, IterationListenerRegistry = reg }.Write(msd)),
            ("MGF", onePass, (msd, reg) => new MgfSerializer { ContinueOnError = true, SkipLog = TextWriter.Null, IterationListenerRegistry = reg }.Write(msd)),
            ("MS2", onePass, (msd, reg) => new SerializerMSn(MSnType.Ms2) { ContinueOnError = true, SkipLog = TextWriter.Null, IterationListenerRegistry = reg }.Write(msd, Stream.Null)),
            ("CMS2", onePass, (msd, reg) => new SerializerMSn(MSnType.Cms2) { ContinueOnError = true, SkipLog = TextWriter.Null, IterationListenerRegistry = reg }.Write(msd, Stream.Null)),
        };
        foreach (var (format, expected, write) in writers)
        {
            var reg = new IterationListenerRegistry();
            var listener = new RecordingListener();
            reg.AddListener(listener, iterationPeriod: 1);
            write(BuildDocWithUnreadableSpectrum(), reg);
            CollectionAssert.AreEqual(expected, listener.Updates.Select(u => u.IterationIndex).ToArray(),
                $"{format}: one update per input spectrum per pass, including the skipped one");
        }
    }

    [TestMethod]
    public void AllWriters_WithoutContinueOnError_ThrowEnumerationException()
    {
        // EnumerationException is what makes msconvert suggest re-running with --continueOnError.
        foreach (var format in s_streamedFormats)
        {
            var reg = new IterationListenerRegistry();
            var listener = new RecordingListener();
            reg.AddListener(listener, iterationPeriod: 1);
            var ex = Assert.ThrowsException<EnumerationException>(
                () => WriteThroughMSDataFile(BuildDocWithUnreadableSpectrum(), format, continueOnError: false, reg),
                $"{format}: an unreadable spectrum must stop the write");
            Assert.IsInstanceOfType<InvalidOperationException>(ex.InnerException, $"{format}: inner exception");
            // Every writer reports a spectrum before fetching it, so the last update names the one that failed.
            Assert.AreEqual(BadIndex, listener.Updates.Last().IterationIndex, $"{format}: last progress update");
        }
    }

    [TestMethod]
    public void AllWriters_Cancellation_PropagatesUnwrappedWithOrWithoutContinueOnError()
    {
        // A cancelled conversion is not an unreadable spectrum: it must neither be skipped (which
        // would finish a truncated file that looks complete) nor wrapped as EnumerationException
        // (which would have msconvert suggest --continueOnError).
        foreach (var format in s_streamedFormats)
        {
            foreach (bool continueOnError in new[] { false, true })
            {
                Assert.ThrowsException<OperationCanceledException>(
                    () => WriteThroughMSDataFile(BuildDocWithUnreadableSpectrum(() => new OperationCanceledException()),
                        format, continueOnError),
                    $"{format}, continueOnError={continueOnError}: cancellation must stop the write");
            }

            // A progress listener asks to cancel by returning Cancel, which is how MSConvertGUI
            // stops a conversion when its progress window closes.
            var reg = new IterationListenerRegistry();
            var canceler = new CancelingListener(cancelAt: 1);
            reg.AddListener(canceler, iterationPeriod: 1);
            Assert.ThrowsException<OperationCanceledException>(
                () => WriteThroughMSDataFile(BuildMsnDocWithSpectra(3), format, continueOnError: true, reg),
                $"{format}: a listener returning Cancel must stop the write");
            CollectionAssert.AreEqual(new[] { 0, 1 }, canceler.Seen, $"{format}: nothing written past the cancel");
        }
    }

    /// <summary>Returns <see cref="IterationStatus.Cancel"/> from the given iteration on.</summary>
    private sealed class CancelingListener : IIterationListener
    {
        private readonly int _cancelAt;

        public CancelingListener(int cancelAt) => _cancelAt = cancelAt;

        public List<int> Seen { get; } = new();

        public IterationStatus Update(IterationUpdate message)
        {
            Seen.Add(message.IterationIndex);
            return message.IterationIndex >= _cancelAt ? IterationStatus.Cancel : IterationStatus.Ok;
        }
    }

    [TestMethod]
    public void Mzml_ContinueOnError_SkipsTheUnreadableChromatogramAndRenumbersTheRest()
    {
        var msd = BuildMsnDocWithSpectra(1);
        var chroms = new ChromatogramListSimple();
        for (int i = 0; i < 3; i++)
            chroms.Chromatograms.Add(new Chromatogram { Index = i, Id = $"chrom-{i}" });
        msd.Run.ChromatogramList = new UnreadableChromatogramList(chroms, BadIndex);

        using var log = new StringWriter();
        string mzml = new MzmlWriter { ContinueOnError = true, SkipLog = log }.Write(msd);
        Assert.AreEqual("Skipping chromatogram 1 \"chrom-1\": unreadable chromatogram", log.ToString().TrimEnd());

        var cl = new MzmlReader().Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(mzml))).Run.ChromatogramList!;
        CollectionAssert.AreEqual(new[] { "chrom-0", "chrom-2" },
            Enumerable.Range(0, cl.Count).Select(i => cl.GetChromatogram(i).Id).ToArray(), "surviving chromatograms");
        for (int i = 0; i < cl.Count; i++)
            Assert.AreEqual(i, cl.GetChromatogram(i).Index, $"index of survivor {i}");
        Assert.AreEqual(2, ListCount(mzml, "chromatogramList"), "chromatogramList count");
    }

    [TestMethod]
    public void Mzml_ContinueOnError_CountsOnlyTheWrittenSpectraInGzipAndMzMLbFiles()
    {
        // Neither output can be rewound in place - a GZipStream cannot seek, and mzMLb also
        // holds the binary arrays in HDF5 datasets - so both are rewritten from the start.
        string dir = Path.Combine(Path.GetTempPath(), $"skipcount-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string gzPath = Path.Combine(dir, "out.mzML.gz");
            MSDataFile.Write(BuildDocWithUnreadableSpectrum(), gzPath,
                new WriteConfig { Format = WriteFormat.Mzml, Gzip = true, ContinueOnError = true });
            string gzMzml;
            using (var gz = new System.IO.Compression.GZipStream(File.OpenRead(gzPath), System.IO.Compression.CompressionMode.Decompress))
            using (var reader = new StreamReader(gz))
                gzMzml = reader.ReadToEnd();
            Assert.AreEqual(2, ListCount(gzMzml, "spectrumList"), "mzML.gz: spectrumList count");
            Assert.AreEqual(2, new MzmlReader().Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(gzMzml))).Run.SpectrumList!.Count,
                "mzML.gz: spectra read back");

            string mzMLbPath = Path.Combine(dir, "out.mzMLb");
            MSDataFile.Write(BuildDocWithUnreadableSpectrum(), mzMLbPath,
                new WriteConfig { Format = WriteFormat.MzMLb, ContinueOnError = true });
            string innerMzml;
            using (var conn = Pwiz.Data.MsData.MzMlb.MzMlbConnection.OpenForRead(mzMLbPath))
            using (var reader = new StreamReader(conn.OpenMzMlStream()))
                innerMzml = reader.ReadToEnd();
            Assert.AreEqual(2, ListCount(innerMzml, "spectrumList"), "mzMLb: spectrumList count");
            var mzMLb = new MSData();
            new Pwiz.Data.MsData.Readers.MzMlbReaderAdapter().Read(mzMLbPath, mzMLb);
            CollectionAssert.AreEqual(new[] { 100.0, 102.0 },
                Enumerable.Range(0, mzMLb.Run.SpectrumList!.Count)
                    .Select(i => mzMLb.Run.SpectrumList.GetSpectrum(i, getBinaryData: true).GetMZArray()!.Data[0]).ToArray(),
                "mzMLb: surviving spectra keep their own peaks");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public void Mzml_ContinueOnError_WarnsWhenTheStreamCannotBeRewound()
    {
        using var log = new StringWriter();
        using var output = new NonSeekableStream();
        new MzmlWriter { ContinueOnError = true, SkipLog = log }.Write(BuildDocWithUnreadableSpectrum(), output);

        var lines = log.ToString().TrimEnd().Split(Environment.NewLine);
        Assert.AreEqual(2, lines.Length, log.ToString());
        StringAssert.StartsWith(lines[0], "Skipping spectrum 1 ");
        Assert.AreEqual(
            "Warning: the output cannot be rewritten, so the spectrumList and chromatogramList counts still include the 1 item(s) skipped",
            lines[1]);
        Assert.AreEqual(3, ListCount(System.Text.Encoding.UTF8.GetString(output.ToArray()), "spectrumList"),
            "the count written before the skip is left as it was");
    }

    [TestMethod]
    public void Mzml_ContinueOnError_StopsRewritingWhenEveryPassSkipsSomethingNew()
    {
        var msd = BuildMsnDocWithSpectra(4);
        msd.Run.SpectrumList = new FailsAnotherSpectrumEachPass(msd.Run.SpectrumList!);
        using var log = new StringWriter();
        string mzml = new MzmlWriter { ContinueOnError = true, SkipLog = log }.Write(msd);

        var lines = log.ToString().TrimEnd().Split(Environment.NewLine);
        CollectionAssert.AreEqual(new[] { "Skipping spectrum 0 ", "Skipping spectrum 1 ", "Skipping spectrum 2 " },
            lines.Take(3).Select(l => l[..l.IndexOf('"')]).ToArray(), "one new skip per pass");
        Assert.AreEqual(
            "Warning: items were still failing after 3 passes, so the spectrumList and chromatogramList counts still include the 1 item(s) skipped in the last one",
            lines[3]);
        Assert.AreEqual(2, ListCount(mzml, "spectrumList"), "the last pass counted the 2 it was told to leave out, not the new one");
    }

    /// <summary>Fails spectrum p on pass p, the way a reader with intermittent failures can keep
    /// turning up new bad spectra. The last spectrum, always read, marks the end of each pass.</summary>
    private sealed class FailsAnotherSpectrumEachPass : SpectrumListBase
    {
        private readonly ISpectrumList _inner;
        private int _pass;

        public FailsAnotherSpectrumEachPass(ISpectrumList inner) => _inner = inner;

        public override int Count => _inner.Count;

        public override SpectrumIdentity SpectrumIdentity(int index) => _inner.SpectrumIdentity(index);

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        {
            if (index == Count - 1)
                _pass++;
            else if (index == _pass)
                throw new InvalidOperationException("intermittent failure");
            return _inner.GetSpectrum(index, getBinaryData);
        }
    }

    private static int ListCount(string mzml, string listElement)
    {
        var match = System.Text.RegularExpressions.Regex.Match(mzml, $"<{listElement} count=\"(\\d+)\"");
        Assert.IsTrue(match.Success, $"no <{listElement} count> in the output");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>A write-only sink that cannot seek, like a pipe or a network stream.</summary>
    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
