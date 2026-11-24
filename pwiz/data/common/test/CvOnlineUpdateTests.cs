using System.Net;
using Pwiz.Data.Common.Cv;
using static Pwiz.Data.Common.Tests.Cv.CvOnlineUpdateFixtures;

namespace Pwiz.Data.Common.Tests.Cv;

[TestClass]
public class CvOnlineUpdateTests
{
    // An accession no embedded CV defines, and its CVID: the MS block starts at 0.
    private const string NewAccession = "MS:9999999";
    private const CVID NewCvid = (CVID)9999999;

    private static readonly string NewTerm = Term(NewAccession, "new widget", isA: "MS:1000031 ! instrument model");

    private FakeCvServer _server = null!;
    private string _cacheDir = null!;
    private DateTime _now;
    private readonly List<string> _messages = new();

    [TestInitialize]
    public void Initialize()
    {
        _server = new FakeCvServer();
        _cacheDir = Path.Combine(Path.GetTempPath(), "pwiz-cv-update-test-" + Guid.NewGuid().ToString("N"));
        _now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
        StartProcess();
        CvLookup.OnlineUpdateMessage += OnMessage;
    }

    [TestCleanup]
    public void Cleanup()
    {
        CvLookup.OnlineUpdateMessage -= OnMessage;
        CvLookup.ResetOnlineUpdateForTests();
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    private void OnMessage(string message)
    {
        lock (_messages)
            _messages.Add(message);
    }

    private void StartProcess() => CvOnlineUpdateFixtures.StartProcess(_server, _cacheDir, () => _now);

    private string[] TakeRequests()
    {
        var requests = _server.Requests.ToArray();
        _server.Requests.Clear();
        return requests;
    }

    [TestMethod]
    public void UnknownTerm_ResolvedFromRegisteredNewerVersion()
    {
        _server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0",
            NewTerm,
            Term("MS:1000531", "software (renamed)")));
        CvLookup.RegisterSource(MsCv("99.0.0"));

        var info = CvLookup.CvTermInfo(NewAccession);
        Assert.AreEqual(NewCvid, info.Cvid);
        Assert.AreEqual(NewAccession, info.Id);
        Assert.AreEqual("new widget", info.Name);
        Assert.AreEqual("The new widget.", info.Def);

        // The term is a CVID like any other from here on.
        Assert.AreSame(info, CvLookup.CvTermInfo(NewCvid));
        Assert.IsTrue(CvLookup.CvIsA(NewCvid, CVID.MS_instrument_model), "is_a an embedded term");
        CollectionAssert.Contains(CvLookup.AllCvids.ToList(), NewCvid);

        // A newer version wins over the embedded one; the CV version pwiz writes stays the
        // embedded one, so output does not depend on what earlier files loaded.
        Assert.AreEqual("software (renamed)", CvLookup.CvTermInfo(CVID.MS_software).Name);
        Assert.AreEqual(CvFamily.Ms.EmbeddedVersion.ToString(), CvLookup.GetCv("MS").Version);

        // The registered purl serves the latest release, not 99.0.0, so only the tag is asked.
        CollectionAssert.AreEqual(new[] { MsTagUrl("99.0.0") }, TakeRequests());
        Assert.IsTrue(_messages.Any(m => m.Contains("version 99.0.0", StringComparison.Ordinal)), "download reported");
    }

    [TestMethod]
    public void OlderRegisteredVersion_OnlyFillsGaps()
    {
        _server.Serve(MsTagUrl("1.0.0"), MsObo("1.0.0",
            NewTerm,
            Term("MS:1000531", "software (old name)")));
        CvLookup.RegisterSource(MsCv("1.0.0"));

        // An older release can still hold a term later ones dropped, so it is tried first...
        Assert.AreEqual("new widget", CvLookup.CvTermInfo(NewAccession).Name);
        CollectionAssert.AreEqual(new[] { MsTagUrl("1.0.0") }, TakeRequests());

        // ...but never reverts what the embedded version defines.
        Assert.AreEqual("software", CvLookup.CvTermInfo(CVID.MS_software).Name);
    }

    [TestMethod]
    public void OlderDownload_NeverReplacesANewerDownload()
    {
        // Latest (99.1.0) loads first; then a file registers 99.0.0, which alone has another
        // new term, and names two terms differently.
        _server.Serve(MsLatestUrl, FullMsObo("99.1.0", NewTerm));
        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);

        _server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0",
            Term("MS:9999998", "old widget"),
            Term(NewAccession, "new widget (99.0)"),
            Term("MS:1000531", "software 99.0")));
        CvLookup.RegisterSource(MsCv("99.0.0"));
        Assert.AreEqual("old widget", CvLookup.CvTermInfo("MS:9999998").Name);

        // 99.1.0 defines both of those, and it is newer.
        Assert.AreEqual("software", CvLookup.CvTermInfo(CVID.MS_software).Name);
        Assert.AreEqual("new widget", CvLookup.CvTermInfo(NewCvid).Name);
        CollectionAssert.AreEqual(new[] { MsLatestUrl, MsTagUrl("99.0.0") }, TakeRequests());
    }

    [TestMethod]
    public void PaddedAccession_MatchesTheKnownTerm()
    {
        // mz5 writes every accession as prefix:%07d, so Unimod's come out zero-padded.
        Assert.AreEqual(CVID.UNIMOD_Acetyl, CvLookup.CvTermInfo("UNIMOD:0000001").Cvid);
        Assert.AreEqual(CVID.UO_second, CvLookup.CvTermInfo("UO:10").Cvid);
        Assert.AreEqual(0, TakeRequests().Length);

        // A padded spelling of a term only a download defines resolves through it too.
        _server.Serve(MsLatestUrl, FullMsObo("99.1.0", NewTerm));
        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo("MS:09999999").Cvid);
    }

    [TestMethod]
    public void IsACycle_RejectsTheDownload()
    {
        _server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0",
            Term(NewAccession, "new widget", isA: "MS:9999998"),
            Term("MS:9999998", "other widget", isA: NewAccession)));
        CvLookup.RegisterSource(MsCv("99.0.0"));

        // CvIsA would never return over that cycle, so the version is not merged at all.
        Assert.AreEqual(CVID.CVID_Unknown, CvLookup.CvTermInfo(NewAccession).Cvid);
        Assert.IsTrue(_messages.Any(m => m.Contains("cycle", StringComparison.Ordinal)), string.Join(" | ", _messages));
        Assert.IsTrue(CvLookup.CvIsA(CVID.MS_software, CVID.MS_software));
    }

    [TestMethod]
    public void UnreachableHost_SkippedForAWhile_ThenRetried()
    {
        _server.Throw(MsLatestUrl, new HttpRequestException("No connection could be made."));
        _server.Throw(MsPurl, new HttpRequestException("No connection could be made."));
        Assert.AreEqual(CVID.CVID_Unknown, CvLookup.CvTermInfo(NewAccession).Cvid);
        CollectionAssert.AreEqual(new[] { MsLatestUrl, MsPurl }, TakeRequests());

        // The release tag lives on the same host as latest, so a newly registered version is
        // not asked for until the pause is over.
        _server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0", NewTerm));
        CvLookup.RegisterSource(MsCv("99.0.0"));
        Assert.AreEqual(CVID.CVID_Unknown, CvLookup.CvTermInfo(NewAccession).Cvid);
        Assert.AreEqual(0, TakeRequests().Length);

        // Once it is over, the step that failed for want of a network runs again.
        _now += CvOnlineUpdater.RetryInterval + TimeSpan.FromMinutes(1);
        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);
        CollectionAssert.AreEqual(new[] { MsTagUrl("99.0.0") }, TakeRequests());
    }

    [TestMethod]
    public void Registration_IsLazy_AndNewestRegisteredVersionWins()
    {
        _server.Serve(MsTagUrl("98.0.0"), MsObo("98.0.0", NewTerm));
        _server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0", NewTerm));

        foreach (var order in new[] { new[] { "98.0.0", "99.0.0" }, new[] { "99.0.0", "98.0.0" } })
        {
            StartProcess();
            foreach (string version in order)
                CvLookup.RegisterSource(MsCv(version));
            Assert.AreEqual("software", CvLookup.CvTermInfo("MS:1000531").Name);
            Assert.AreEqual(0, TakeRequests().Length, "registering and known lookups stay offline");

            Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);
            CollectionAssert.AreEqual(new[] { MsTagUrl("99.0.0") }, TakeRequests(), string.Join(",", order));
            Directory.Delete(_cacheDir, recursive: true);
        }
    }

    [TestMethod]
    public void EmbeddedVersionRegistered_GoesStraightToLatest()
    {
        _server.Serve(MsLatestUrl, FullMsObo("99.1.0", NewTerm));
        CvLookup.RegisterSource(MsCv(CvLookup.GetCv("MS").Version));

        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);
        CollectionAssert.AreEqual(new[] { MsLatestUrl }, TakeRequests());
    }

    [TestMethod]
    public void NoRegisteredSource_GoesToLatest_FallingBackToThePurl()
    {
        _server.Fail(MsLatestUrl, HttpStatusCode.ServiceUnavailable);
        _server.Serve(MsPurl, FullMsObo("99.1.0", NewTerm));

        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);
        CollectionAssert.AreEqual(new[] { MsLatestUrl, MsPurl }, TakeRequests());
    }

    [TestMethod]
    public void RegisteredUriOrder_AndVersionCheck()
    {
        const string deadUrl = "http://psidev.cvs.sourceforge.net/viewvc/*checkout*/psidev/psi/psi-ms/mzML/controlledVocabulary/psi-ms.obo";
        const string versionedUrl = "https://mirror.example.org/cv/99.0.0/psi-ms.obo";

        // (registered CV, what the server has) -> URLs asked, in order
        var cases = new (string Name, CV Cv, Action Serve, string[] Expected)[]
        {
            // A URI naming the version comes before the tag; it must serve that version.
            ("versioned uri", MsCv("99.0.0", versionedUrl),
                () => _server.Serve(versionedUrl, MsObo("99.0.0", NewTerm)),
                new[] { versionedUrl }),
            ("versioned uri serving another version", MsCv("99.0.0", versionedUrl),
                () =>
                {
                    _server.Serve(versionedUrl, MsObo("98.0.0", NewTerm));
                    _server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0", NewTerm));
                },
                new[] { versionedUrl, MsTagUrl("99.0.0") }),
            // A dead URI an old writer recorded: after the tag, then on to latest.
            ("dead uri", MsCv("99.0.0", deadUrl),
                () =>
                {
                    _server.Serve(deadUrl, "<html><body>SourceForge has moved</body></html>");
                    _server.Serve(MsLatestUrl, FullMsObo("99.1.0", NewTerm));
                },
                new[] { MsTagUrl("99.0.0"), deadUrl, MsLatestUrl }),
            // Some writers call psi-ms "PSI-MS" and give no URI.
            ("PSI-MS id", new CV { Id = "PSI-MS", Version = "99.0.0" },
                () => _server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0", NewTerm)),
                new[] { MsTagUrl("99.0.0") }),
            // Only http(s) is fetched, never a local or UNC path.
            ("file uri", MsCv("99.0.0", "file:///C:/cv/99.0.0/psi-ms.obo"),
                () => _server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0", NewTerm)),
                new[] { MsTagUrl("99.0.0") }),
            ("unc path", MsCv("99.0.0", @"\\server\share\99.0.0\psi-ms.obo"),
                () => _server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0", NewTerm)),
                new[] { MsTagUrl("99.0.0") }),
        };

        foreach (var (name, cv, serve, expected) in cases)
        {
            _server = new FakeCvServer();
            StartProcess();
            serve();
            CvLookup.RegisterSource(cv);

            Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid, name);
            CollectionAssert.AreEqual(expected, TakeRequests(), name);
            if (Directory.Exists(_cacheDir))
                Directory.Delete(_cacheDir, recursive: true);
        }
    }

    [TestMethod]
    public void ConcurrentMisses_DownloadOnce_AndEachStepRunsOnce()
    {
        _server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0", NewTerm));
        CvLookup.RegisterSource(MsCv("99.0.0"));

        Parallel.For(0, 32, new ParallelOptions { MaxDegreeOfParallelism = 16 },
            _ => Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid));
        CollectionAssert.AreEqual(new[] { MsTagUrl("99.0.0") }, TakeRequests());

        // A term no version has: latest is tried once, and later misses stay offline.
        Assert.AreEqual(CVID.CVID_Unknown, CvLookup.CvTermInfo("MS:9999998").Cvid);
        CollectionAssert.AreEqual(new[] { MsLatestUrl, MsPurl }, TakeRequests());
        Assert.AreEqual(CVID.CVID_Unknown, CvLookup.CvTermInfo("MS:9999998").Cvid);
        Assert.AreEqual(CVID.CVID_Unknown, CvLookup.CvTermInfo("MS:9999997").Cvid);
        Assert.AreEqual(0, TakeRequests().Length);
    }

    [TestMethod]
    public void NonCvAccessions_NeverTriggerAnUpdate()
    {
        foreach (string accession in new[] { "XX:9999999", "NCIT:C25330", "MS:", "MS:abc", "MS:100000000", "nonsense" })
            Assert.AreEqual(CVID.CVID_Unknown, CvLookup.CvTermInfo(accession).Cvid, accession);
        Assert.AreEqual(0, TakeRequests().Length);
    }

    [TestMethod]
    public void VersionedCache_ServesLaterProcessesOffline()
    {
        _server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0", NewTerm));
        CvLookup.RegisterSource(MsCv("99.0.0"));
        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);
        Assert.AreEqual(1, TakeRequests().Length);

        // A release never changes, so its cache entry never expires.
        _server = new FakeCvServer();
        _now += TimeSpan.FromDays(365);
        StartProcess();
        CvLookup.RegisterSource(MsCv("99.0.0"));
        Assert.AreEqual("new widget", CvLookup.CvTermInfo(NewAccession).Name);
        Assert.AreEqual(0, TakeRequests().Length);
    }

    [TestMethod]
    public void LatestCache_RefreshedAfterInterval_WithETag()
    {
        _server.Serve(MsLatestUrl, FullMsObo("99.1.0", NewTerm), etag: "\"v1\"");
        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);
        Assert.AreEqual(1, TakeRequests().Length);

        // Within the interval another process uses the cached copy without asking.
        _now += TimeSpan.FromHours(1);
        StartProcess();
        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);
        Assert.AreEqual(0, TakeRequests().Length);

        // After it, the server is asked, and a 304 reuses the cached copy.
        _now += CvOnlineUpdater.LatestRefreshInterval;
        StartProcess();
        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);
        CollectionAssert.AreEqual(new[] { MsLatestUrl }, TakeRequests());

        // The 304 restarted the interval.
        _now += TimeSpan.FromHours(1);
        StartProcess();
        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);
        Assert.AreEqual(0, TakeRequests().Length);

        // CacheOnly uses even a stale copy, and never asks.
        _now += TimeSpan.FromDays(30);
        StartProcess();
        CvLookup.OnlineUpdate = CvOnlineUpdateMode.CacheOnly;
        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);
        Assert.AreEqual(0, TakeRequests().Length);

        // A failed refresh falls back to the stale copy.
        _server.Fail(MsLatestUrl, HttpStatusCode.InternalServerError);
        _server.Fail(MsPurl, HttpStatusCode.InternalServerError);
        StartProcess();
        Assert.AreEqual(NewCvid, CvLookup.CvTermInfo(NewAccession).Cvid);
        CollectionAssert.AreEqual(new[] { MsLatestUrl, MsPurl }, TakeRequests());
    }

    [TestMethod]
    public void FailedDownloads_LeaveTheTermUnknown_AndCacheNothing()
    {
        // what the latest URL does -> the term stays unknown, nothing is cached, and it is reported
        var failures = new (string Name, Action<string> Arrange)[]
        {
            ("html page", url => _server.Serve(url, "<!DOCTYPE html><html>Captive portal</html>")),
            ("server error", url => _server.Fail(url, HttpStatusCode.InternalServerError)),
            ("timeout", url => _server.Throw(url, new TaskCanceledException("The request timed out."))),
            ("connection failure", url => _server.Throw(url, new HttpRequestException("No such host is known."))),
            ("truncated", url => _server.Serve(url, MsObo("99.1.0", NewTerm))),
            ("no version", url => _server.Serve(url, FullMsObo("99.1.0", NewTerm).Replace("data-version:", "remark:"))),
        };

        foreach (var (name, arrange) in failures)
        {
            _server = new FakeCvServer();
            StartProcess();
            arrange(MsLatestUrl);
            arrange(MsPurl);
            lock (_messages)
                _messages.Clear();

            Assert.AreEqual(CVID.CVID_Unknown, CvLookup.CvTermInfo(NewAccession).Cvid, name);
            Assert.AreEqual(2, TakeRequests().Length, name);
            Assert.IsFalse(Directory.Exists(_cacheDir) && Directory.EnumerateFiles(_cacheDir, "*", SearchOption.AllDirectories).Any(),
                name + ": nothing cached");
            Assert.AreEqual(2, _messages.Count, name + ": " + string.Join(" | ", _messages));
        }
    }

    [TestMethod]
    public void Disabled_NeverAsks()
    {
        _server.Serve(MsLatestUrl, FullMsObo("99.1.0", NewTerm));
        CvLookup.OnlineUpdate = CvOnlineUpdateMode.Disabled;
        Assert.AreEqual(CVID.CVID_Unknown, CvLookup.CvTermInfo(NewAccession).Cvid);
        Assert.AreEqual(0, TakeRequests().Length);
    }

    [TestMethod]
    public void EnvironmentVariable_SetsTheDefaultMode()
    {
        string? original = Environment.GetEnvironmentVariable(CvOnlineUpdater.EnvironmentVariable);
        try
        {
            // value -> mode
            var cases = new (string? Value, CvOnlineUpdateMode Expected)[]
            {
                (null, CvOnlineUpdateMode.Enabled),
                ("1", CvOnlineUpdateMode.Enabled),
                ("0", CvOnlineUpdateMode.Disabled),
                ("off", CvOnlineUpdateMode.Disabled),
                ("Disabled", CvOnlineUpdateMode.Disabled),
                ("cacheonly", CvOnlineUpdateMode.CacheOnly),
            };
            foreach (var (value, expected) in cases)
            {
                Environment.SetEnvironmentVariable(CvOnlineUpdater.EnvironmentVariable, value);
                CvLookup.ResetOnlineUpdateForTests();
                Assert.AreEqual(expected, CvLookup.OnlineUpdate, value ?? "(unset)");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(CvOnlineUpdater.EnvironmentVariable, original);
        }
    }

    [TestMethod]
    public void RegisteredCv_MapsToItsFamily()
    {
        // CV declaration -> family it registers for (null: ignored)
        var cases = new (CV Cv, string? Expected)[]
        {
            (new CV { Id = "MS", Uri = MsPurl }, "MS"),
            (new CV { Id = "PSI-MS", Uri = "" }, "MS"),
            (new CV { Id = "cv1", Uri = "https://raw.githubusercontent.com/HUPO-PSI/psi-ms-CV/master/psi-ms.obo?raw=true" }, "MS"),
            (new CV { Id = "UO", Uri = "http://obo.cvs.sourceforge.net/*checkout*/obo/obo/ontology/phenotype/unit.obo" }, "UO"),
            (new CV { Id = "UO", Uri = "" }, "UO"),
            (new CV { Id = "UNIMOD", Uri = "http://www.unimod.org/obo/unimod.obo" }, "UNIMOD"),
            (new CV { Id = "NCIT", Uri = "http://purl.obolibrary.org/obo/ncit.obo" }, null),
            (new CV { Id = "PATO", Uri = "" }, null),
        };
        foreach (var (cv, expected) in cases)
            Assert.AreEqual(expected, CvFamily.ForCv(cv)?.Name, $"{cv.Id} {cv.Uri}");
    }

    [TestMethod]
    public void VersionsParse_AsEachCvWritesThem()
    {
        // family, text -> parsed version (null: not a version of that CV)
        var cases = new (CvFamily Family, string Text, string? Expected)[]
        {
            (CvFamily.Ms, "4.1.257", "4.1.257"),
            (CvFamily.Ms, "v4.2.14", "4.2.14"),
            (CvFamily.Ms, "3.30.0", "3.30.0"),
            (CvFamily.Ms, "2026-01-16", null),
            (CvFamily.Uo, "releases/2026-01-16", "2026-01-16"),
            (CvFamily.Uo, "09:04:2009", "2009-04-09"),
            (CvFamily.Unimod, "17:02:2026 11:36", "2026-02-17"),
            (CvFamily.Unimod, "2011-03-25", "2011-03-25"),
            (CvFamily.Unimod, "1.4", null),
            (CvFamily.Unimod, "", null),
        };
        foreach (var (family, text, expected) in cases)
            Assert.AreEqual(expected, family.ParseVersion(text)?.ToString(), $"{family} {text}");

        // The embedded files' own versions parse.
        Assert.AreEqual(CvLookup.GetCv("MS").Version, CvFamily.Ms.EmbeddedVersion.ToString());
        Assert.IsTrue(CvFamily.Uo.EmbeddedVersion.Date.Year >= 2020);
        Assert.IsTrue(CvFamily.Unimod.EmbeddedVersion.Date.Year >= 2020);
    }
}
