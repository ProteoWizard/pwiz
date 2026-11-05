using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.Common.Tests.Cv;

/// <summary>
/// Serves OBO files to <see cref="CvOnlineUpdater"/> in place of the network, and records every
/// URL asked for. Anything not served is a 404.
/// </summary>
internal sealed class FakeCvServer
{
    private readonly ConcurrentDictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes =
        new(StringComparer.Ordinal);

    public ConcurrentQueue<string> Requests { get; } = new();

    /// <summary>Serves <paramref name="body"/>; with an <paramref name="etag"/>, answers a
    /// matching If-None-Match with 304.</summary>
    public void Serve(string url, string body, string? etag = null) => _routes[url] = request =>
    {
        if (etag is not null && request.Headers.IfNoneMatch.Any(t => t.Tag == etag))
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        if (etag is not null)
            response.Headers.ETag = new EntityTagHeaderValue(etag);
        return response;
    };

    public void Fail(string url, HttpStatusCode status) =>
        _routes[url] = _ => new HttpResponseMessage(status);

    public void Throw(string url, Exception exception) =>
        _routes[url] = _ => throw exception;

    public HttpMessageHandler CreateHandler() => new Handler(this);

    private sealed class Handler : HttpMessageHandler
    {
        private readonly FakeCvServer _server;

        public Handler(FakeCvServer server) => _server = server;

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.AbsoluteUri;
            _server.Requests.Enqueue(url);
            return _server._routes.TryGetValue(url, out var route)
                ? route(request)
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));
    }
}

/// <summary>OBO files, URLs and setup for the online CV update tests.</summary>
internal static class CvOnlineUpdateFixtures
{
    public const string MsLatestUrl = "https://raw.githubusercontent.com/HUPO-PSI/psi-ms-CV/master/psi-ms.obo";
    public const string MsPurl = "http://purl.obolibrary.org/obo/ms/psi-ms.obo";

    public static string MsTagUrl(string version) =>
        $"https://raw.githubusercontent.com/HUPO-PSI/psi-ms-CV/v{version}/psi-ms.obo";

    /// <summary>A psi-ms.obo with just <paramref name="terms"/>.</summary>
    public static string MsObo(string dataVersion, params string[] terms) =>
        $"format-version: 1.2\ndata-version: {dataVersion}\n\n{string.Join("\n", terms)}";

    /// <summary>The embedded psi-ms.obo restamped as <paramref name="dataVersion"/>, plus
    /// <paramref name="terms"/>: a latest release big enough not to look truncated.</summary>
    public static string FullMsObo(string dataVersion, params string[] terms)
    {
        using var stream = typeof(CvLookup).Assembly.GetManifestResourceStream("Pwiz.Data.Common.Cv.psi-ms.obo")!;
        string embedded = new StreamReader(stream).ReadToEnd();
        string restamped = Regex.Replace(embedded, @"^data-version: .*$", $"data-version: {dataVersion}",
            RegexOptions.Multiline);
        return $"{restamped}\n\n{string.Join("\n", terms)}";
    }

    public static string Term(string id, string name, string? isA = null) =>
        $"[Term]\nid: {id}\nname: {name}\ndef: \"The {name}.\" [PSI:MS]\n" +
        (isA is null ? "" : $"is_a: {isA}\n");

    public static CV MsCv(string version, string uri = MsPurl) =>
        new() { Id = "MS", FullName = "Proteomics Standards Initiative Mass Spectrometry Ontology", Version = version, Uri = uri };

    /// <summary>
    /// Starts a fresh "process": no registered sources or downloaded terms, the update enabled,
    /// and every download going to <paramref name="server"/>. The disk cache in
    /// <paramref name="cacheDir"/> survives, as it would between processes.
    /// </summary>
    public static void StartProcess(FakeCvServer server, string cacheDir, Func<DateTime>? clock = null)
    {
        CvLookup.ResetOnlineUpdateForTests();
        CvLookup.OnlineUpdate = CvOnlineUpdateMode.Enabled;
        CvOnlineUpdater.CacheRootOverride = cacheDir;
        CvOnlineUpdater.HandlerFactory = server.CreateHandler;
        if (clock is not null)
            CvOnlineUpdater.UtcNow = clock;
    }
}
