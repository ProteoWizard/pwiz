using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Pwiz.Data.Common.Obo;

namespace Pwiz.Data.Common.Cv;

/// <summary>How <see cref="CvLookup"/> treats an accession that no loaded CV version defines.</summary>
public enum CvOnlineUpdateMode
{
    /// <summary>Load a newer CV version from the disk cache, or download it.</summary>
    Enabled,

    /// <summary>Load a newer CV version from the disk cache only; never touch the network.</summary>
    CacheOnly,

    /// <summary>Report the term as unknown.</summary>
    Disabled,
}

/// <summary>
/// A CV release version, ordered within its <see cref="CvFamily"/>: psi-ms numbers its
/// releases (4.1.257), UO and Unimod date them.
/// </summary>
internal readonly record struct CvVersion : IComparable<CvVersion>
{
    private CvVersion(Version? number, DateTime date)
    {
        Number = number;
        Date = date;
    }

    public Version? Number { get; }
    public DateTime Date { get; }

    public static CvVersion FromNumber(Version number) => new(number, default);
    public static CvVersion FromDate(DateTime date) => new(null, date.Date);

    // A family uses only one of the two kinds; ordering numbers before dates just keeps the
    // comparison total.
    public int CompareTo(CvVersion other)
    {
        if (Number is not null && other.Number is not null) return Number.CompareTo(other.Number);
        if (Number is null && other.Number is null) return Date.CompareTo(other.Date);
        return Number is null ? 1 : -1;
    }

    /// <summary>The version as a GitHub release tag uses it, and as a cache file name.</summary>
    public override string ToString() =>
        Number?.ToString() ?? Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// One downloadable CV: which accession prefixes it defines, where its versions are published,
/// and how its files state their version.
/// </summary>
internal sealed class CvFamily
{
    // Declared before the instances below, whose constructors parse the embedded versions.
    private static readonly Regex s_numberRegex = new(@"^\s*v?(\d+(?:\.\d+){1,3})\s*$", RegexOptions.Compiled);
    private static readonly Regex s_isoDateRegex = new(@"(\d{4})-(\d{2})-(\d{2})", RegexOptions.Compiled);
    private static readonly Regex s_oboDateRegex = new(@"(\d{2}):(\d{2}):(\d{4})", RegexOptions.Compiled);
    private static readonly SearchValues<char> s_queryStart = SearchValues.Create("?#");
    private static readonly SearchValues<char> s_pathSeparators = SearchValues.Create("/\\");

    public static readonly CvFamily Ms = new("MS", new[] { "MS", "PEFF" }, "psi-ms.obo",
        CvLookup.OboFiles.PsiMs, datedVersions: false, versionHeader: "data-version:",
        latestUrls: new[]
        {
            "https://raw.githubusercontent.com/HUPO-PSI/psi-ms-CV/master/psi-ms.obo",
            "http://purl.obolibrary.org/obo/ms/psi-ms.obo",
        },
        tagUrl: v => $"https://raw.githubusercontent.com/HUPO-PSI/psi-ms-CV/v{v}/psi-ms.obo");

    public static readonly CvFamily Uo = new("UO", new[] { "UO" }, "uo.obo",
        CvLookup.OboFiles.Unit, datedVersions: true, versionHeader: "data-version:",
        latestUrls: new[] { "http://purl.obolibrary.org/obo/uo.obo" },
        tagUrl: v => $"https://raw.githubusercontent.com/bio-ontology-research-group/unit-ontology/v{v}/uo.obo");

    // unimod.obo has no data-version; its "date:" header is the release date.
    public static readonly CvFamily Unimod = new("UNIMOD", new[] { "UNIMOD" }, "unimod.obo",
        CvLookup.OboFiles.Unimod, datedVersions: true, versionHeader: "date:",
        latestUrls: new[] { "https://www.unimod.org/obo/unimod.obo" },
        tagUrl: null);

    public static IReadOnlyList<CvFamily> All { get; } = new[] { Ms, Uo, Unimod };

    private readonly bool _datedVersions;
    private readonly string _versionHeader;
    private readonly Func<CvVersion, string>? _tagUrl;

    private CvFamily(string name, string[] prefixes, string fileName, ObOntology embedded,
        bool datedVersions, string versionHeader, string[] latestUrls, Func<CvVersion, string>? tagUrl)
    {
        Name = name;
        Prefixes = new HashSet<string>(prefixes, StringComparer.Ordinal);
        PrimaryPrefix = prefixes[0];
        FileName = fileName;
        _datedVersions = datedVersions;
        _versionHeader = versionHeader;
        LatestUrls = latestUrls;
        _tagUrl = tagUrl;
        // A version that cannot be read makes every download count as newer, which is the
        // safe direction: downloaded terms then win, as they would for any newer release.
        EmbeddedVersion = VersionOf(embedded) ?? CvVersion.FromNumber(new Version(0, 0));
        EmbeddedTermCount = embedded.Terms.Count(t => t.Prefix == PrimaryPrefix);
    }

    /// <summary>"MS", "UO" or "UNIMOD".</summary>
    public string Name { get; }

    /// <summary>The accession prefixes this CV defines (psi-ms.obo defines both MS and PEFF).</summary>
    public IReadOnlySet<string> Prefixes { get; }

    public string PrimaryPrefix { get; }

    public string FileName { get; }

    /// <summary>Where the current release is published, in the order to try them.</summary>
    public IReadOnlyList<string> LatestUrls { get; }

    public CvVersion EmbeddedVersion { get; }

    /// <summary>Terms with <see cref="PrimaryPrefix"/> in the embedded file, to recognize a
    /// truncated download of the latest release.</summary>
    public int EmbeddedTermCount { get; }

    public static CvFamily? ForPrefix(string prefix)
    {
        foreach (var family in All)
        {
            if (family.Prefixes.Contains(prefix))
                return family;
        }
        return null;
    }

    /// <summary>The family a file's CV declaration refers to: by the file name in its URI, then
    /// by its id (which some writers spell "PSI-MS").</summary>
    public static CvFamily? ForCv(CV cv)
    {
        string uri = cv.Uri.Trim();
        int query = uri.AsSpan().IndexOfAny(s_queryStart);
        if (query >= 0) uri = uri[..query];
        string file = uri[(uri.AsSpan().LastIndexOfAny(s_pathSeparators) + 1)..].ToLowerInvariant();
        switch (file)
        {
            case "psi-ms.obo": return Ms;
            case "uo.obo":
            case "unit.obo": return Uo;
            case "unimod.obo": return Unimod;
        }

        return cv.Id.Trim().ToUpperInvariant() switch
        {
            "MS" or "PSI-MS" or "PSI_MS" => Ms,
            "UO" => Uo,
            "UNIMOD" => Unimod,
            _ => null,
        };
    }

    /// <summary>Parses a version as this family writes it ("4.1.257", "releases/2026-01-16",
    /// "17:02:2026 11:36"), or null when it is not one - such as the "1.4" format-version that
    /// pwiz once wrote as Unimod's version.</summary>
    public CvVersion? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (!_datedVersions)
        {
            var number = s_numberRegex.Match(text);
            return number.Success ? CvVersion.FromNumber(Version.Parse(number.Groups[1].Value)) : null;
        }

        var iso = s_isoDateRegex.Match(text);
        if (iso.Success)
            return MakeDate(iso.Groups[1].Value, iso.Groups[2].Value, iso.Groups[3].Value);
        var obo = s_oboDateRegex.Match(text);
        if (obo.Success)
            return MakeDate(obo.Groups[3].Value, obo.Groups[2].Value, obo.Groups[1].Value);
        return null;
    }

    private static CvVersion? MakeDate(string year, string month, string day)
    {
        return DateTime.TryParseExact($"{year}-{month}-{day}", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? CvVersion.FromDate(date)
            : null;
    }

    /// <summary>The release version an OBO file states in its header.</summary>
    public CvVersion? VersionOf(ObOntology obo)
    {
        foreach (var line in obo.Header)
        {
            if (line.StartsWith(_versionHeader, StringComparison.Ordinal))
                return ParseVersion(line[_versionHeader.Length..]);
        }
        return null;
    }

    /// <summary>The immutable URL of a release, or null when the CV does not tag its releases.</summary>
    public string? TagUrl(CvVersion version) => _tagUrl?.Invoke(version);

    public bool IsLatestUrl(string url) =>
        LatestUrls.Any(latest => string.Equals(StripScheme(latest), StripScheme(url), StringComparison.OrdinalIgnoreCase));

    private static string StripScheme(string url)
    {
        int separator = url.IndexOf("://", StringComparison.Ordinal);
        return (separator >= 0 ? url[(separator + 3)..] : url).TrimEnd('/');
    }

    public override string ToString() => Name;
}

/// <summary>A CV version loaded from the cache or the network.</summary>
internal sealed record CvSource(CvFamily Family, CvVersion Version, ObOntology Obo);

/// <summary>
/// Resolves accessions the loaded CVs don't define by loading another version of their CV.
/// </summary>
/// <remarks>
/// On a miss, for the accession's CV, in order, until the term turns up:
/// <list type="number">
/// <item>The newest version any file registered, even one older than the embedded version (a
/// term deleted from later releases). Its sources: registered URIs that name the version, the
/// release tag, then the other registered URIs - and the download must state that version.</item>
/// <item>Registered URIs with no usable version, taking whatever version they serve.</item>
/// <item>The latest release, refetched at most every <see cref="LatestRefreshInterval"/>.</item>
/// </list>
/// Each step runs once per process, unless it failed for a reason that may pass - the network,
/// a server error, or a mode that kept it offline - in which case it may run again after
/// <see cref="RetryInterval"/>. A host that fails to connect is skipped for that long too, so
/// a machine without internet access pays one connect timeout per host, not one per request.
/// Every version is cached on disk, so another process starts from the cache. Downloads send
/// no credentials or cookies, follow only http(s) redirects, and are size- and time-limited;
/// a failure is reported through <see cref="Message"/> and the term stays unknown.
/// </remarks>
internal static class CvOnlineUpdater
{
    internal const string EnvironmentVariable = "PWIZ_CV_ONLINE_UPDATE";
    internal static readonly TimeSpan LatestRefreshInterval = TimeSpan.FromHours(24);
    internal static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);
    private const long MaxDownloadBytes = 50L * 1024 * 1024;
    private const int MaxRedirects = 5;
    private const string LatestName = "latest";
    private const string LatestStep = "latest";

    // A latest download with fewer terms than this fraction of the embedded file's is truncated.
    private const double MinLatestTermFraction = 0.9;

    // Serializes merging into the table, which two CVs may do at once.
    private static readonly object s_publishLock = new();
    private static Dictionary<CvFamily, FamilyState> s_states = NewStates();
    private static ConcurrentDictionary<string, DateTime> s_unreachableHosts = new(StringComparer.OrdinalIgnoreCase);
    private static int s_mode = (int)DefaultMode();

    // Read once per resolution, so a change mid-download takes effect on the next miss.
    internal static CvOnlineUpdateMode Mode
    {
        get => (CvOnlineUpdateMode)Volatile.Read(ref s_mode);
        set => Volatile.Write(ref s_mode, (int)value);
    }

    internal static event Action<string>? Message;

    // ---- test seams ----

    internal static Func<HttpMessageHandler> HandlerFactory { get; set; } = CreateDefaultHandler;

    internal static string? CacheRootOverride { get; set; }

    internal static Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    internal static void Reset()
    {
        lock (s_publishLock)
        {
            Volatile.Write(ref s_states, NewStates());
            Volatile.Write(ref s_unreachableHosts, new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase));
            CvLookup.Publish(CvTermTable.Embedded);
        }
        Mode = DefaultMode();
        HandlerFactory = CreateDefaultHandler;
        CacheRootOverride = null;
        UtcNow = () => DateTime.UtcNow;
    }

    private static Dictionary<CvFamily, FamilyState> NewStates() =>
        CvFamily.All.ToDictionary(f => f, f => new FamilyState(f));

    private static CvOnlineUpdateMode DefaultMode()
    {
        string? setting = Environment.GetEnvironmentVariable(EnvironmentVariable)?.Trim().ToLowerInvariant();
        return setting switch
        {
            "0" or "false" or "off" or "no" or "disabled" => CvOnlineUpdateMode.Disabled,
            "cache" or "cacheonly" => CvOnlineUpdateMode.CacheOnly,
            _ => CvOnlineUpdateMode.Enabled,
        };
    }

    private static HttpMessageHandler CreateDefaultHandler() => new SocketsHttpHandler
    {
        // Least privilege for a URL that came out of a data file: no cookies, no credentials,
        // and the handler follows only http(s) redirects (never https -> http).
        UseCookies = false,
        Credentials = null,
        PreAuthenticate = false,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = MaxRedirects,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = ConnectTimeout,
    };

    // ---- registration ----

    internal static void Register(CV cv)
    {
        if (CvFamily.ForCv(cv) is not { } family)
            return;
        Volatile.Read(ref s_states)[family].Register(family.ParseVersion(cv.Version), cv.Uri.Trim());
    }

    // ---- resolution ----

    /// <summary>The CV and CVID of an MS, PEFF, UO or UNIMOD accession, however its number is
    /// padded; false for any other accession.</summary>
    internal static bool TryParseAccession(string accession, [NotNullWhen(true)] out CvFamily? family, out CVID cvid)
    {
        family = null;
        cvid = CVID.CVID_Unknown;
        int colon = accession.IndexOf(':');
        if (colon <= 0)
            return false;
        string prefix = accession[..colon];
        if (CvFamily.ForPrefix(prefix) is not { } found
            || !uint.TryParse(accession.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out uint number)
            || CvLookup.CvidFor(prefix, number) is not { } parsed)
            return false;
        family = found;
        cvid = parsed;
        return true;
    }

    /// <summary>Tries to load a version of <paramref name="family"/> that defines
    /// <paramref name="cvid"/>; true when the table now has it.</summary>
    internal static bool TryResolve(CvFamily family, CVID cvid, string accession)
    {
        var mode = Mode;
        if (mode == CvOnlineUpdateMode.Disabled)
            return false;
        var state = Volatile.Read(ref s_states)[family];
        // Every cvParam of a term no version defines lands here; once nothing is left to try,
        // answer without the lock until a file registers something new or a retry falls due.
        if (state.IsExhausted(UtcNow()))
            return false;

        lock (state.LoadLock)
        {
            var context = new StepContext(mode, accession);
            // Another thread may have loaded it while this one waited.
            while (!CvLookup.Table.Contains(cvid))
            {
                var now = UtcNow();
                int registrations = state.Registrations;
                if (!TryNextStep(state, context, now))
                {
                    state.MarkExhausted(registrations, state.NextRetry(now));
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>What one resolution knows: the mode it read, and whether a step failed in a way
    /// that may pass.</summary>
    private sealed class StepContext
    {
        public StepContext(CvOnlineUpdateMode mode, string accession)
        {
            Mode = mode;
            Accession = accession;
        }

        public CvOnlineUpdateMode Mode { get; }
        public string Accession { get; }
        public bool Retryable { get; set; }
    }

    /// <summary>Runs the first step that is due; false when none is.</summary>
    private static bool TryNextStep(FamilyState state, StepContext context, DateTime now)
    {
        var family = state.Family;

        var (newest, newestUris) = state.NewestRegistered();
        if (newest is { } version && state.IsDue(VersionStep(version), now))
        {
            // The embedded file IS that version, so there is nothing to load.
            if (version.CompareTo(family.EmbeddedVersion) != 0)
                RunStep(state, VersionStep(version), context, now, () => LoadVersion(family, version, newestUris, context));
            else
                state.Finish(VersionStep(version), retryable: false, now);
            return true;
        }

        foreach (string uri in state.UnversionedUris())
        {
            // The latest step covers a latest URL, with its cache and refresh interval.
            if (family.IsLatestUrl(uri) || !state.IsDue(UriStep(uri), now))
                continue;
            RunStep(state, UriStep(uri), context, now, () => LoadUnversioned(family, uri, context));
            return true;
        }

        if (state.IsDue(LatestStep, now))
        {
            RunStep(state, LatestStep, context, now, () => LoadLatest(family, context));
            return true;
        }
        return false;
    }

    private static string VersionStep(CvVersion version) => "version:" + version;

    private static string UriStep(string uri) => "uri:" + uri;

    private static void RunStep(FamilyState state, string step, StepContext context, DateTime now, Func<CvSource?> load)
    {
        context.Retryable = false;
        var source = load();
        state.Finish(step, context.Retryable, now);
        if (source is null || !state.LoadedVersions.Add(source.Version))
            return;
        // A version loaded by any step needs no step of its own.
        state.Finish(VersionStep(source.Version), retryable: false, now);
        if (source.Version.CompareTo(source.Family.EmbeddedVersion) == 0)
            return; // the same release as the embedded file
        lock (s_publishLock)
        {
            if (CvLookup.Table.WithSource(source) is { } table)
                CvLookup.Publish(table);
            else
                Report($"ignoring {source.Family.FileName} version {source.Version}: its is_a relations form a cycle");
        }
    }

    private static CvSource? LoadVersion(CvFamily family, CvVersion version, IReadOnlyList<string> registeredUris,
        StepContext context)
    {
        string cachePath = CachePath(family, version.ToString());
        if (ReadCache(family, cachePath) is { } cached && family.VersionOf(cached) is { } cachedVersion
            && cachedVersion.Equals(version))
            return new CvSource(family, version, cached);
        if (context.Mode != CvOnlineUpdateMode.Enabled)
        {
            context.Retryable = true;
            return null;
        }

        // A URI that names the version is the most specific source; the release tag next; a
        // URI that names no version last, and only if it still serves that version. The
        // published latest URLs are left to the latest step.
        string versionText = version.ToString();
        var candidates = registeredUris.Where(u => u.Contains(versionText, StringComparison.OrdinalIgnoreCase))
            .Append(family.TagUrl(version))
            .Concat(registeredUris.Where(u => !family.IsLatestUrl(u)))
            .OfType<string>()
            .Where(u => u.Length > 0)
            .Distinct(StringComparer.Ordinal);
        foreach (string url in candidates)
        {
            if (Download(family, url, etag: null, context) is not { Obo: { } obo } download)
                continue;
            var downloadedVersion = family.VersionOf(obo);
            if (downloadedVersion is null || !downloadedVersion.Value.Equals(version))
            {
                Report($"{url} serves {family.FileName} version {downloadedVersion?.ToString() ?? "(none)"}, not {version}");
                continue;
            }
            WriteCache(cachePath, download.Bytes);
            Report($"downloaded {family.FileName} version {version} from {url} for unknown term {context.Accession}");
            return new CvSource(family, version, obo);
        }
        return null;
    }

    private static CvSource? LoadUnversioned(CvFamily family, string url, StepContext context)
    {
        if (context.Mode != CvOnlineUpdateMode.Enabled)
        {
            context.Retryable = true;
            return null;
        }
        if (Download(family, url, etag: null, context) is not { Obo: { } obo } download)
            return null;
        if (family.VersionOf(obo) is not { } version)
        {
            Report($"{url} serves a {family.FileName} with no readable version");
            return null;
        }
        WriteCache(CachePath(family, version.ToString()), download.Bytes);
        Report($"downloaded {family.FileName} version {version} from {url} for unknown term {context.Accession}");
        return new CvSource(family, version, obo);
    }

    private static CvSource? LoadLatest(CvFamily family, StepContext context)
    {
        string cachePath = CachePath(family, LatestName);
        string metaPath = Path.ChangeExtension(cachePath, ".meta");
        var cached = ReadCache(family, cachePath);
        var meta = cached is null ? null : CacheMeta.Read(metaPath);
        var cachedSource = cached is not null && family.VersionOf(cached) is { } cachedVersion
            ? new CvSource(family, cachedVersion, cached)
            : null;

        if (cachedSource is not null && meta is not null
            && (context.Mode == CvOnlineUpdateMode.CacheOnly || UtcNow() - meta.FetchedUtc < LatestRefreshInterval))
            return cachedSource;
        if (context.Mode != CvOnlineUpdateMode.Enabled)
        {
            context.Retryable = true;
            return cachedSource;
        }

        foreach (string url in family.LatestUrls)
        {
            string? etag = cachedSource is not null && meta is not null && meta.Url == url ? meta.ETag : null;
            if (Download(family, url, etag, context) is not { } download)
                continue;
            if (download.NotModified)
            {
                (meta! with { FetchedUtc = UtcNow() }).Write(metaPath);
                context.Retryable = false;
                return cachedSource;
            }
            if (download.Obo is not { } obo)
                continue;
            if (family.VersionOf(obo) is not { } version)
            {
                Report($"{url} serves a {family.FileName} with no readable version");
                continue;
            }
            int termCount = obo.Terms.Count(t => t.Prefix == family.PrimaryPrefix);
            if (termCount < family.EmbeddedTermCount * MinLatestTermFraction)
            {
                Report($"{url} serves a {family.FileName} with only {termCount} terms; ignoring it as truncated");
                continue;
            }
            WriteCache(cachePath, download.Bytes);
            new CacheMeta(url, download.ETag, UtcNow()).Write(metaPath);
            WriteCache(CachePath(family, version.ToString()), download.Bytes);
            Report($"downloaded {family.FileName} version {version} from {url} for unknown term {context.Accession}");
            context.Retryable = false;
            return new CvSource(family, version, obo);
        }

        // A stale copy beats none; the refresh is retried if what stopped it may pass.
        if (cachedSource is not null)
            Report($"using the cached {family.FileName} version {cachedSource.Version}, which could not be refreshed");
        return cachedSource;
    }

    // ---- download ----

    private sealed record DownloadResult(byte[] Bytes, ObOntology? Obo, string? ETag, bool NotModified);

    private static DownloadResult? Download(CvFamily family, string url, string? etag, StepContext context)
    {
        // A file can name any URI; only fetch over http(s) - never file:// or a UNC path.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Report($"not downloading {family.FileName} from {url}: not an http(s) URL");
            return null;
        }

        var unreachableHosts = Volatile.Read(ref s_unreachableHosts);
        var now = UtcNow();
        if (unreachableHosts.TryGetValue(uri.Host, out var until) && now < until)
        {
            context.Retryable = true;
            return null;
        }

        try
        {
            using var client = new HttpClient(HandlerFactory(), disposeHandler: true)
            {
                Timeout = DownloadTimeout,
                MaxResponseContentBufferSize = MaxDownloadBytes,
            };
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (etag is not null)
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            using var response = client.Send(request, HttpCompletionOption.ResponseContentRead);
            if (response.StatusCode == HttpStatusCode.NotModified && etag is not null)
                return new DownloadResult(Array.Empty<byte>(), null, etag, NotModified: true);
            if (!response.IsSuccessStatusCode)
            {
                // A server error or throttling may pass; a 404 will not.
                int status = (int)response.StatusCode;
                if (status >= 500 || response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
                    context.Retryable = true;
                Report($"could not download {family.FileName} from {url}: HTTP {status} {response.ReasonPhrase}");
                return null;
            }

            using var buffer = new MemoryStream();
            using (var content = response.Content.ReadAsStream())
                content.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            var obo = ParseObo(family, bytes);
            if (obo is null)
                Report($"{url} did not serve a {family.FileName} file");
            return new DownloadResult(bytes, obo, response.Headers.ETag?.ToString(), NotModified: false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            // No connection, or no answer in time: skip this host for a while rather than make
            // every later request wait out the same timeout.
            unreachableHosts[uri.Host] = now + RetryInterval;
            context.Retryable = true;
            Report($"could not download {family.FileName} from {url}: {ex.Message} (not trying {uri.Host} again for {RetryInterval.TotalMinutes:0} minutes)");
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            Report($"could not download {family.FileName} from {url}: {ex.Message}");
            return null;
        }
    }

    /// <summary>The OBO in <paramref name="bytes"/>, or null when it is not one of this CV's
    /// files (an HTML error page, a captive portal, another ontology).</summary>
    private static ObOntology? ParseObo(CvFamily family, byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string text = reader.ReadToEnd();
        if (!text.StartsWith("format-version:", StringComparison.Ordinal))
            return null;
        var obo = ObOntology.Parse(new StringReader(text));
        return obo.Terms.Any(t => t.Prefix == family.PrimaryPrefix) ? obo : null;
    }

    // ---- disk cache ----

    private static readonly Lazy<string> s_defaultCacheRoot = new(DefaultCacheRoot);

    private static string CacheRoot => CacheRootOverride ?? s_defaultCacheRoot.Value;

    private static string DefaultCacheRoot()
    {
        // A per-machine install can point every user at one shared, pre-populated cache, as
        // with the vendor SDK cache.
        string sharedMarker = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ProteoWizard", "cv-cache-root.txt");
        try
        {
            if (File.Exists(sharedMarker))
            {
                string shared = File.ReadAllText(sharedMarker).Trim();
                if (shared.Length > 0)
                    return shared;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // fall through to the per-user cache
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
            localAppData = Path.GetTempPath();
        return Path.Combine(localAppData, "ProteoWizard", "cv");
    }

    private static string CachePath(CvFamily family, string name) =>
        Path.Combine(CacheRoot, family.Name, name + ".obo");

    private static ObOntology? ReadCache(CvFamily family, string path)
    {
        try
        {
            return File.Exists(path) ? ParseObo(family, File.ReadAllBytes(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Report($"could not read the cached {path}: {ex.Message}");
            return null;
        }
    }

    // Written to a temp file and moved into place, so another process never reads half a file.
    private static void WriteCache(string path, byte[] bytes)
    {
        string temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Report($"could not write the cache file {path}: {ex.Message}");
            try { File.Delete(temp); }
            catch (Exception deleteEx) when (deleteEx is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Where and when the cached latest release was fetched.</summary>
    private sealed record CacheMeta(string Url, string? ETag, DateTime FetchedUtc)
    {
        public static CacheMeta? Read(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var values = File.ReadAllLines(path)
                    .Select(line => line.Split('=', 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
                if (!values.TryGetValue("url", out string? url)
                    || !values.TryGetValue("fetched", out string? fetched)
                    || !DateTime.TryParse(fetched, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fetchedUtc))
                    return null;
                values.TryGetValue("etag", out string? etag);
                return new CacheMeta(url, string.IsNullOrEmpty(etag) ? null : etag, fetchedUtc);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        public void Write(string path)
        {
            string text = $"url={Url}\netag={ETag}\nfetched={FetchedUtc.ToString("O", CultureInfo.InvariantCulture)}\n";
            WriteCache(path, Encoding.UTF8.GetBytes(text));
        }
    }

    private static void Report(string message) => Message?.Invoke($"CV update: {message}");

    // ---- per-CV state ----

    private sealed class FamilyState
    {
        private readonly List<(CvVersion? Version, string Uri)> _registered = new();
        private int _registrations;
        private ExhaustedMark? _exhausted;

        // When each step that has run may run again; DateTime.MaxValue for never.
        private readonly Dictionary<string, DateTime> _nextRun = new(StringComparer.Ordinal);

        public FamilyState(CvFamily family) => Family = family;

        public CvFamily Family { get; }

        /// <summary>Serializes loading for this CV, so concurrent misses download once. The
        /// step bookkeeping below is guarded by it.</summary>
        public object LoadLock { get; } = new();

        public HashSet<CvVersion> LoadedVersions { get; } = new();

        public int Registrations => Volatile.Read(ref _registrations);

        public bool IsDue(string step, DateTime now) =>
            !_nextRun.TryGetValue(step, out var next) || now >= next;

        public void Finish(string step, bool retryable, DateTime now) =>
            _nextRun[step] = retryable ? now + RetryInterval : DateTime.MaxValue;

        /// <summary>When the earliest retryable step falls due, or never.</summary>
        public DateTime NextRetry(DateTime now) =>
            _nextRun.Values.Where(next => next > now).DefaultIfEmpty(DateTime.MaxValue).Min();

        private sealed record ExhaustedMark(int Registrations, DateTime Until);

        public void MarkExhausted(int registrations, DateTime until) =>
            Volatile.Write(ref _exhausted, new ExhaustedMark(registrations, until));

        public bool IsExhausted(DateTime now) =>
            Volatile.Read(ref _exhausted) is { } mark && mark.Registrations == Registrations && now < mark.Until;

        // Registration has its own lock, so a reader registering a file's CVs never waits
        // behind a download.
        public void Register(CvVersion? version, string uri)
        {
            lock (_registered)
            {
                if (_registered.Contains((version, uri)))
                    return;
                _registered.Add((version, uri));
                Interlocked.Increment(ref _registrations);
            }
        }

        public (CvVersion? Version, IReadOnlyList<string> Uris) NewestRegistered()
        {
            lock (_registered)
            {
                var newest = _registered.Where(r => r.Version is not null).Max(r => r.Version);
                if (newest is null)
                    return (null, Array.Empty<string>());
                return (newest, _registered.Where(r => r.Version.Equals(newest) && r.Uri.Length > 0)
                    .Select(r => r.Uri).ToArray());
            }
        }

        public string[] UnversionedUris()
        {
            lock (_registered)
                return _registered.Where(r => r.Version is null && r.Uri.Length > 0).Select(r => r.Uri).ToArray();
        }
    }
}
