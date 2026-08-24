using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// Shared HTTP + OAuth machinery for UNIFI and waters_connect sample-result clients. C# port
/// of the cpp <c>UnifiData::Impl</c> base shared by <c>UnifiData::Impl</c> and
/// <c>WatersConnectImpl</c> (UnifiData.cpp + WatersConnectData.ipp).
/// </summary>
/// <remarks>
/// Does the OAuth-2.0 "password" grant exchange and stashes the access token for the
/// connection's lifetime; subclasses implement the per-API metadata fetches and per-spectrum
/// download endpoints. The cpp side uses <c>IdentityModel.Client.TokenClient</c> for the token
/// exchange — equivalent here is a plain <see cref="HttpClient"/> POST to <c>/connect/token</c>.
///
/// Constructor flow follows cpp <c>connect()</c> (UnifiData.cpp:218-222 / WatersConnectData.ipp:161-163):
/// init HttpClient → exchange access token → set Bearer auth header → fetch sample metadata.
/// Spectrum / chromatogram listing is layered in subsequent commits (the heavy lifting depends
/// on <c>ParallelDownloadQueue</c> and the protobuf schema, which land separately).
/// </remarks>
public abstract class AbstractWatersHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    /// <summary>The parsed input URL.</summary>
    public UnifiConnectionConfig Config { get; }

    /// <summary>OAuth2 access token returned by the identity server, set in the constructor.</summary>
    public string AccessToken { get; private set; } = string.Empty;

    /// <summary>Sample (or injection) name from the metadata endpoint.</summary>
    public string SampleName { get; protected set; } = string.Empty;

    /// <summary>Free-form description; may be empty.</summary>
    public string SampleDescription { get; protected set; } = string.Empty;

    /// <summary>1-based replicate index for repeated injections of the same sample.</summary>
    public int ReplicateNumber { get; protected set; } = 1;

    /// <summary>Vendor well-position string (e.g. <c>"1:C,4"</c>); empty when not reported.</summary>
    public string WellPosition { get; protected set; } = string.Empty;

    /// <summary>Acquisition start time (UTC). <see cref="DateTime.MinValue"/> when not reported.</summary>
    public DateTime AcquisitionStartTimeUtc { get; protected set; }

    /// <summary>Constructs the client and immediately performs the OAuth handshake +
    /// sample-metadata fetch. Pass a custom <paramref name="httpClient"/> for tests; otherwise
    /// the client owns a fresh <see cref="HttpClient"/> for the connection's lifetime.</summary>
    /// <param name="config">Parsed URL + OAuth knobs.</param>
    /// <param name="httpClient">Optional HttpClient — useful for testing with a mock handler;
    /// when null, a default instance is created and disposed alongside this client.</param>
    protected AbstractWatersHttpClient(UnifiConnectionConfig config, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        Config = config;

        if (httpClient is null)
        {
            _httpClient = new HttpClient();
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }

        AccessToken = AcquireAccessToken();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        FetchSampleMetadata();
    }

    /// <summary>Endpoint for the sample-result metadata GET. cpp's
    /// <c>sampleResultMetadataEndpoint</c> for UNIFI, <c>injectionsMetadataEndpoint</c>
    /// for waters_connect.</summary>
    protected abstract string SampleMetadataEndpoint { get; }

    /// <summary>Project-specific JSON parser hook. Default behavior pulls UNIFI-shaped fields
    /// (<c>$.name</c>, <c>$.description</c>, <c>$.sample.replicateNumber</c>,
    /// <c>$.sample.wellPosition</c>, <c>$.sample.acquisitionStartTime</c>); waters_connect
    /// overrides to walk the <c>injection-data</c> array and pick the matching injectionId.</summary>
    protected abstract void ParseSampleMetadata(JsonDocument json);

    /// <summary>Sends an authenticated GET. Subclass-friendly so spectrum / chromatogram
    /// callers in later commits don't need to re-implement the auth header dance.</summary>
    protected async Task<HttpResponseMessage> SendAuthenticatedGetAsync(string url, string? acceptHeader = null, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (acceptHeader is not null)
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));
        return await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
    }

    /// <summary>Synchronous GET against the data source. Wraps the underlying HttpClient's
    /// Send method so callers (e.g. <see cref="HttpUnifiDataSource"/>) don't have to push
    /// async-over-sync through their construction-time discovery walks.</summary>
    public HttpResponseMessage SendAuthenticatedGetSync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        return _httpClient.Send(request);
    }

    /// <summary>Synchronous POST. <paramref name="request"/> is sent as-is — the caller is
    /// responsible for setting <c>Content</c>; the bearer auth header is already on the
    /// HttpClient's defaults.</summary>
    public HttpResponseMessage SendAuthenticatedPostSync(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _httpClient.Send(request);
    }

    private string AcquireAccessToken()
    {
        // cpp UnifiData.cpp:985-1018 / WatersConnectData.ipp:475-510 — OAuth password grant.
        // Username/password live in the URL's userinfo (preferred) or the env-var fallback
        // (UNIFI_USERNAME/PASSWORD or WC_USERNAME/PASSWORD). The cpp port routes through
        // IdentityModel.Client.TokenClient with BasicAuthentication, which on the wire is a
        // POST to /connect/token with x-www-form-urlencoded body and an
        // `Authorization: Basic <base64(clientId:clientSecret)>` header. Match that exactly.
        var (username, password) = ResolveCredentials();

        using var request = new HttpRequestMessage(HttpMethod.Post, Config.TokenEndpoint);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password,
            ["scope"] = Config.ClientScope,
        });
        var basic = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"{Config.ClientId}:{Config.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        HttpResponseMessage response;
        try
        {
            response = _httpClient.Send(request);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"authentication error: could not reach token endpoint {Config.TokenEndpoint}: {e.Message}", e);
        }

        using (response)
        {
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"authentication error: incorrect hostname, username, or password? "
                    + $"({(int)response.StatusCode} {response.ReasonPhrase}; body: {Truncate(body, 400)})");

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("access_token", out var token) || token.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException(
                    $"authentication error: token endpoint returned an unexpected payload "
                    + $"(no access_token field): {Truncate(body, 400)}");
            return token.GetString()!;
        }
    }

    private (string Username, string Password) ResolveCredentials()
    {
        // Production code requires credentials embedded in the URL — pwiz-sharp does NOT
        // read *_PASSWORD env vars. Tests that exercise the live demo backend splice
        // credentials in before calling Reader.Read (mirroring how Skyline's
        // pwiz_tools/Skyline/TestUtil/{Unifi,WatersConnect}TestUtil.cs builds URLs for
        // its own integration tests).
        if (Config.Credentials is { } urlCreds)
            return urlCreds;
        throw new InvalidOperationException(
            "credentials missing: embed them in the URL ("
            + (Config.Api == RemoteApiType.WatersConnect
                ? "username:password@host:port/?sampleSetId=...&injectionId=..."
                : "username:password@host:port/unifi/v1/sampleresults(GUID)")
            + ").");
    }

    private void FetchSampleMetadata()
    {
        HttpResponseMessage response;
        try
        {
            response = _httpClient.Send(new HttpRequestMessage(HttpMethod.Get, SampleMetadataEndpoint));
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"error reaching sample metadata endpoint {SampleMetadataEndpoint}: {e.Message}", e);
        }

        using (response)
        {
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"error fetching sample metadata: "
                    + $"{(int)response.StatusCode} {response.ReasonPhrase}; URL was {SampleMetadataEndpoint}; body: {Truncate(body, 400)}");

            try
            {
                using var doc = JsonDocument.Parse(body);
                ParseSampleMetadata(doc);
            }
            catch (JsonException e)
            {
                throw new InvalidOperationException(
                    $"error parsing sample metadata JSON: {e.Message}; body: {Truncate(body, 400)}", e);
            }
        }
    }

    /// <summary>Reads <c>$.name</c>, <c>$.description</c>, <c>$.sample.replicateNumber</c>,
    /// <c>$.sample.wellPosition</c>, and <c>$.sample.acquisitionStartTime</c> from the standard
    /// UNIFI sample-result JSON. Used by both <see cref="UnifiHttpClient"/> directly and by
    /// <see cref="WatersConnectHttpClient"/> after it picks the matching injection.</summary>
    protected void ApplyStandardSampleFields(JsonElement root)
    {
        SampleName = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString() ?? string.Empty
            : string.Empty;
        SampleDescription = root.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
            ? d.GetString() ?? string.Empty
            : string.Empty;

        if (root.TryGetProperty("sample", out var sample) && sample.ValueKind == JsonValueKind.Object)
        {
            if (sample.TryGetProperty("replicateNumber", out var rep) && rep.TryGetInt32(out int repInt))
                ReplicateNumber = repInt;
            if (sample.TryGetProperty("wellPosition", out var wp) && wp.ValueKind == JsonValueKind.String)
                WellPosition = wp.GetString() ?? string.Empty;
            if (sample.TryGetProperty("acquisitionStartTime", out var ts) && ts.ValueKind == JsonValueKind.String
                && DateTime.TryParse(ts.GetString(), CultureInfo.InvariantCulture,
                                     DateTimeStyles.RoundtripKind, out var parsed))
            {
                AcquisitionStartTimeUtc = parsed.ToUniversalTime();
                // cpp UnifiData.cpp:1053-1056 clamps the year to the boost::gregorian range when
                // the source has a corrupt date. Mirror that — defensive against the same files.
                if (AcquisitionStartTimeUtc.Year > 10000)
                    AcquisitionStartTimeUtc = AcquisitionStartTimeUtc.AddYears(10000 - AcquisitionStartTimeUtc.Year);
                else if (AcquisitionStartTimeUtc.Year < 1400)
                    AcquisitionStartTimeUtc = AcquisitionStartTimeUtc.AddYears(1400 - AcquisitionStartTimeUtc.Year);
            }
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsHttpClient) _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
