using System.Web;

namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// Parsed input URL for a Waters HTTP-API sample result. Mirrors the URL-decoding logic
/// in cpp <c>UnifiData::Impl::connect</c> (UnifiData.cpp:200-216) for UNIFI and
/// <c>WatersConnectImpl::connect</c> (WatersConnectData.ipp:135-159) for waters_connect.
/// </summary>
/// <remarks>
/// Carries the post-parse derived fields (identity-server URL, OAuth scope/secret/clientId,
/// API version, sample-result URL, query overrides) that the HTTP client needs but doesn't
/// itself produce. Constructing this record never makes a network call — it's pure URL
/// parsing — so callers can validate input shape before mounting the OAuth flow.
/// </remarks>
public sealed record UnifiConnectionConfig
{
    /// <summary>Which API family the URL targets (UNIFI vs waters_connect).</summary>
    public required RemoteApiType Api { get; init; }

    /// <summary>API version. UNIFI: 3 (port 50034) or 4 (everything else); waters_connect: 2.</summary>
    public required int ApiVersion { get; init; }

    /// <summary>Base URI of the sample-result endpoint (path only — no query string).
    /// For UNIFI: <c>https://host:port/unifi/v1/sampleresults(GUID)</c>.
    /// For waters_connect: <c>https://host:port/</c> (the per-injection paths are appended later).</summary>
    public required Uri SampleResultUrl { get; init; }

    /// <summary>Identity-server token endpoint base URL (defaulted from the data URL's host
    /// + port-derived port; overridable via <c>?identity=</c>).</summary>
    public required Uri IdentityServerUrl { get; init; }

    /// <summary>OAuth password-grant scope. Default depends on API family + version.</summary>
    public required string ClientScope { get; init; }

    /// <summary>OAuth client-id (default <c>resourceownerclient</c> for UNIFI,
    /// <c>resourceownerclient_jwt</c> for waters_connect).</summary>
    public required string ClientId { get; init; }

    /// <summary>OAuth client secret (default <c>secret</c> — Waters' demo backends use this).</summary>
    public required string ClientSecret { get; init; }

    /// <summary>Optional <c>username:password</c> from the URL's userinfo. <c>null</c> when not present
    /// (callers fall back to <c>UNIFI_USERNAME</c>/<c>UNIFI_PASSWORD</c> or
    /// <c>WC_USERNAME</c>/<c>WC_PASSWORD</c> env vars in that case).</summary>
    public (string Username, string Password)? Credentials { get; init; }

    /// <summary>waters_connect-only: GUID of the sample set containing the injection.</summary>
    public string? SampleSetId { get; init; }

    /// <summary>waters_connect-only: GUID of the injection within the sample set.</summary>
    public string? InjectionId { get; init; }

    /// <summary>Optional spectrum-page chunk size override (<c>?chunkSize=</c>).</summary>
    public int? ChunkSizeOverride { get; init; }

    /// <summary>Optional concurrent-page readahead override (<c>?chunkReadahead=</c>).</summary>
    public int? ChunkReadaheadOverride { get; init; }

    /// <summary>Optional friendly name carried by the <c>?name=</c> query param. UNIFI lists use it for
    /// the run id when the API doesn't surface a sample name.</summary>
    public string? FriendlyName { get; init; }

    /// <summary>Token endpoint URI (Identity-server base + version-specific path).</summary>
    public Uri TokenEndpoint => new Uri(
        IdentityServerUrl,
        Api == RemoteApiType.Unifi && ApiVersion == 3 ? "/identity/connect/token" : "/connect/token");

    /// <summary>Parses <paramref name="rawUrl"/> into a <see cref="UnifiConnectionConfig"/>.</summary>
    /// <param name="rawUrl">A UNIFI or waters_connect sample-result URL. Schemes other than
    /// <c>http</c>/<c>https</c> are wrapped with <c>https://</c> for tolerance with cpp's parser
    /// (UnifiData.cpp:202-205 / WatersConnectData.ipp:143-146).</param>
    /// <exception cref="ArgumentException">URL doesn't match either format, or
    /// waters_connect is missing one of the required <c>sampleSetId</c> / <c>injectionId</c>
    /// query parameters.</exception>
    public static UnifiConnectionConfig Parse(string rawUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawUrl);

        // cpp tolerates a missing scheme by prepending https://; mirror that.
        string normalized = rawUrl;
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            normalized = "https://" + normalized;
        var uri = new Uri(normalized);
        var queryVars = HttpUtility.ParseQueryString(uri.Query);

        // Pull common query knobs once — the per-API parsers add format-specific extras.
        (string, string)? credentials = null;
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            if (parts.Length == 2 && !string.IsNullOrEmpty(parts[0]) && !string.IsNullOrEmpty(parts[1]))
                credentials = (Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1]));
        }
        int? chunkSize = TryParseInt(queryVars["chunkSize"]);
        int? chunkReadahead = TryParseInt(queryVars["chunkReadahead"]);
        string? friendlyName = queryVars["name"];
        string clientSecret = queryVars["secret"] ?? "secret";

        // Route by URL shape. cpp Reader_UNIFI.cpp:35-45 uses the same heuristics.
        bool isUnifi = uri.AbsolutePath.Contains("/sampleresults", StringComparison.OrdinalIgnoreCase);
        bool isWatersConnect = !string.IsNullOrEmpty(queryVars["sampleSetId"])
                              || !string.IsNullOrEmpty(queryVars["injectionId"]);

        if (isUnifi)
        {
            // cpp UnifiData.cpp:207-216: port 50034 → API v3 (with the /identity/connect/token
            // endpoint and "unifi" scope), everything else → v4. Identity server defaults to the
            // same host with a different port (50333 for v3, 48333 for v4).
            int apiVersion = uri.Port == 50034 ? 3 : 4;
            string defaultScope = apiVersion == 3 ? "unifi" : "webapi";
            int idPort = apiVersion == 3 ? 50333 : 48333;
            var defaultIdentity = new Uri($"{uri.Scheme}://{uri.Host}:{idPort}");
            var identity = queryVars["identity"] is string explicitIdentity && !string.IsNullOrEmpty(explicitIdentity)
                ? new Uri(explicitIdentity)
                : defaultIdentity;
            string clientScope = queryVars["scope"] ?? defaultScope;
            string clientId = queryVars["clientId"] ?? "resourceownerclient";
            // GetLeftPart(Path) mirrors cpp's `temp->GetLeftPart(UriPartial::Path)` — strip the
            // query / fragment but keep host + path (the parenthesized GUID belongs in the path).
            var sampleResultUrl = StripUserInfo(new Uri(uri.GetLeftPart(UriPartial.Path)));

            return new UnifiConnectionConfig
            {
                Api = RemoteApiType.Unifi,
                ApiVersion = apiVersion,
                SampleResultUrl = sampleResultUrl,
                IdentityServerUrl = identity,
                ClientScope = clientScope,
                ClientId = clientId,
                ClientSecret = clientSecret,
                Credentials = credentials,
                ChunkSizeOverride = chunkSize,
                ChunkReadaheadOverride = chunkReadahead,
                FriendlyName = friendlyName,
            };
        }

        if (isWatersConnect)
        {
            // cpp WatersConnectData.ipp:148-159: API v2 always, identity port 48333 always,
            // sampleSetId + injectionId both required.
            string? sampleSetId = queryVars["sampleSetId"];
            string? injectionId = queryVars["injectionId"];
            if (string.IsNullOrEmpty(sampleSetId))
                throw new ArgumentException("sampleSetId parameter is required for waters_connect URLs");
            if (string.IsNullOrEmpty(injectionId))
                throw new ArgumentException("injectionId parameter is required for waters_connect URLs");

            var defaultIdentity = new Uri($"{uri.Scheme}://{uri.Host}:48333");
            var identity = queryVars["identity"] is string explicitIdentity && !string.IsNullOrEmpty(explicitIdentity)
                ? new Uri(explicitIdentity)
                : defaultIdentity;
            string clientScope = queryVars["scope"] ?? "webapi";
            string clientId = queryVars["clientId"] ?? "resourceownerclient_jwt";
            var sampleResultUrl = StripUserInfo(new Uri(uri.GetLeftPart(UriPartial.Path)));

            return new UnifiConnectionConfig
            {
                Api = RemoteApiType.WatersConnect,
                ApiVersion = 2,
                SampleResultUrl = sampleResultUrl,
                IdentityServerUrl = identity,
                ClientScope = clientScope,
                ClientId = clientId,
                ClientSecret = clientSecret,
                Credentials = credentials,
                SampleSetId = sampleSetId,
                InjectionId = injectionId,
                ChunkSizeOverride = chunkSize,
                ChunkReadaheadOverride = chunkReadahead,
                FriendlyName = friendlyName,
            };
        }

        throw new ArgumentException(
            $"URL does not match a UNIFI sample-result path or a waters_connect query (sampleSetId/injectionId): {rawUrl}");
    }

    /// <summary>Returns a copy of <paramref name="uri"/> with any <c>userinfo</c> stripped.
    /// Userinfo (the <c>username:password@</c> prefix) carries credentials we already pulled
    /// into <see cref="Credentials"/>; leaving it on the URL would leak it into HTTP request
    /// logs and our test handlers would see it on every request URI.</summary>
    private static Uri StripUserInfo(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.UserInfo)) return uri;
        var b = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty };
        return b.Uri;
    }

    private static int? TryParseInt(string? s)
        => int.TryParse(s, System.Globalization.NumberStyles.Integer,
                       System.Globalization.CultureInfo.InvariantCulture, out int v) ? v : null;
}
