using System.Net;
using System.Net.Http;
using System.Text;

namespace Pwiz.Vendor.UNIFI.Tests;

[TestClass]
public class ParallelDownloadQueueTests
{
    [TestMethod]
    public void GetChunkTask_DedupesPerIndex_AndDispatchesUrlBuilder()
    {
        // The queue's contract: a second call for the same (index, doCentroid) returns the
        // running task instead of launching a duplicate. URL-builder + stream-consumer fire
        // exactly once per chunk.
        var handler = new RecordingHandler((req, ct) =>
        {
            return ResponseFor("payload-" + req.RequestUri!.AbsoluteUri);
        });
        int urlBuilderCalls = 0;
        int streamConsumerCalls = 0;
        var capturedUrls = new List<string>();
        var capturedStreams = new List<string>();

        using var queue = new ParallelDownloadQueue(
            new Uri("https://demo.example/sampleresults(g)"),
            "TOK",
            httpClientFactory: () => new HttpClient(handler) { BaseAddress = new Uri("https://demo.example") },
            numSpectra: 100,
            chunkSize: 10,
            concurrentTasks: 2,
            acceptHeader: "application/x-protobuf",
            spectrumEndpoint: info =>
            {
                Interlocked.Increment(ref urlBuilderCalls);
                info.SpectrumEndpoint = $"https://demo.example/sampleresults(g)/spectra?$skip={info.TaskIndex}&$top={info.ChunkSize}";
                lock (capturedUrls) capturedUrls.Add(info.SpectrumEndpoint);
            },
            getSpectraFromStream: (stream, info) =>
            {
                Interlocked.Increment(ref streamConsumerCalls);
                using var reader = new StreamReader(stream);
                lock (capturedStreams) capturedStreams.Add(reader.ReadToEnd());
            });

        var t1 = queue.GetChunkTaskAsync(0, doCentroid: false, primary: true);
        var t2 = queue.GetChunkTaskAsync(0, doCentroid: false, primary: true);
        t1.GetAwaiter().GetResult();
        t2.GetAwaiter().GetResult();

        Assert.AreSame(t1, t2, "Second call should reuse the in-flight task");
        Assert.AreEqual(1, urlBuilderCalls);
        Assert.AreEqual(1, streamConsumerCalls);
        Assert.AreEqual(1, capturedUrls.Count);
        StringAssert.Contains(capturedUrls[0], "$skip=0");
        StringAssert.Contains(capturedUrls[0], "$top=10");
        Assert.AreEqual("payload-" + capturedUrls[0], capturedStreams[0]);
    }

    [TestMethod]
    public void GetChunkTask_TruncatesLastChunk()
    {
        // numSpectra=15, chunkSize=10 → chunk-at-10 should truncate to 5.
        int observedChunkSize = -1;
        var handler = new RecordingHandler((_, _) => ResponseFor("ok"));
        using var queue = new ParallelDownloadQueue(
            new Uri("https://demo.example/sampleresults(g)"),
            "TOK",
            httpClientFactory: () => new HttpClient(handler),
            numSpectra: 15,
            chunkSize: 10,
            concurrentTasks: 1,
            acceptHeader: "application/x-protobuf",
            spectrumEndpoint: info =>
            {
                observedChunkSize = info.ChunkSize;
                info.SpectrumEndpoint = "https://demo.example/x";
            },
            getSpectraFromStream: (_, _) => { });

        queue.GetChunkTaskAsync(10, doCentroid: false, primary: true).GetAwaiter().GetResult();
        Assert.AreEqual(5, observedChunkSize);
    }

    [TestMethod]
    public void Profile_And_Centroid_AreSeparateTasks()
    {
        // Each (index, centroid-flag) pair has its own task slot. cpp uses two
        // ConcurrentDictionaries — same idea here.
        int builds = 0;
        var handler = new RecordingHandler((_, _) => ResponseFor("ok"));
        using var queue = new ParallelDownloadQueue(
            new Uri("https://demo.example/sampleresults(g)"),
            "TOK",
            httpClientFactory: () => new HttpClient(handler),
            numSpectra: 100,
            chunkSize: 10,
            concurrentTasks: 2,
            acceptHeader: "application/x-protobuf",
            spectrumEndpoint: info =>
            {
                Interlocked.Increment(ref builds);
                info.SpectrumEndpoint = $"https://demo.example/x?c={info.GetCentroidData}";
            },
            getSpectraFromStream: (_, _) => { });

        var profile = queue.GetChunkTaskAsync(0, doCentroid: false, primary: true);
        var centroid = queue.GetChunkTaskAsync(0, doCentroid: true, primary: true);
        Task.WaitAll(profile, centroid);
        // Two builds = profile and centroid each ran the URL-builder. We don't assert the
        // task references differ because synchronously-completing async methods in .NET 8
        // can return the singleton Task.CompletedTask when there's no actual yielding.
        Assert.AreEqual(2, builds);
    }

    [TestMethod]
    public void Retry_Eventually_Succeeds_AfterTransient500()
    {
        // Server fails twice, then returns 200. Backoff-with-retry should still surface
        // a successful download. We override the backoff via UNIFI_DEBUG-irrelevant path —
        // the test depends on the queue actually retrying, not on its delay duration.
        int attempts = 0;
        var handler = new RecordingHandler((req, _) =>
        {
            int n = Interlocked.Increment(ref attempts);
            if (n < 3)
                return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("nope") };
            return ResponseFor("good");
        });

        bool consumed = false;
        using var queue = new ParallelDownloadQueue(
            new Uri("https://demo.example/sampleresults(g)"),
            "TOK",
            httpClientFactory: () => new HttpClient(handler),
            numSpectra: 100,
            chunkSize: 10,
            concurrentTasks: 1,
            acceptHeader: "application/x-protobuf",
            spectrumEndpoint: info => info.SpectrumEndpoint = "https://demo.example/x",
            getSpectraFromStream: (_, _) => consumed = true);

        // Use a short-circuited-backoff variant by completing within the test budget. The
        // queue's exponential backoff starts at 4s but we only need 2 retries (~12s worst case
        // for attempt 1+2). Bump the test timeout via TestMethod metadata isn't available in
        // MSTest 3.x without an attribute; accept the wall-clock cost for this scenario.
        // (This is the only test that has to actually wait — the others all return on first
        // attempt.)
        queue.GetChunkTaskAsync(0, doCentroid: false, primary: true).Wait(TimeSpan.FromSeconds(60));
        Assert.IsTrue(consumed, "Stream consumer should run after retries succeed");
        Assert.AreEqual(3, attempts);
    }

    [TestMethod]
    public void GetRequestLimit_CountsRateLimited()
    {
        // 5 simultaneous requests, server replies 429 to 2 of them. Should report a limit
        // of 5 - 2 = 3.
        int count = 0;
        var handler = new RecordingHandler((_, _) =>
        {
            int n = Interlocked.Increment(ref count);
            // Mark requests 4 + 5 as rate-limited.
            if (n >= 4) return new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("rl") };
            return ResponseFor("ok");
        });

        int limit = ParallelDownloadQueue.GetRequestLimit(
            "https://demo.example/sampleresults(g)",
            httpClientFactory: () => new HttpClient(handler),
            accessToken: "TOK",
            acceptHeader: "application/x-protobuf",
            maxConcurrentTasks: 5);

        Assert.AreEqual(3, limit);
    }

    [TestMethod]
    public void GetRequestLimit_AlwaysReturnsAtLeastOne()
    {
        // Even when the server 429s every request, the sequential lane still works.
        // cpp: `Math.Max(1, maxConcurrentTasks)` (ParallelDownloadQueue.cpp:250).
        var handler = new RecordingHandler((_, _) =>
            new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("rl") });

        int limit = ParallelDownloadQueue.GetRequestLimit(
            "https://demo.example/x",
            () => new HttpClient(handler),
            "TOK",
            "application/x-protobuf",
            maxConcurrentTasks: 4);

        Assert.AreEqual(1, limit);
    }

    [TestMethod]
    public void Dispose_CancelsInFlightTasks()
    {
        // Slow handler that respects cancellation. Disposing the queue should cancel.
        var releaseAll = new ManualResetEventSlim();
        var handler = new RecordingHandler(async (_, ct) =>
        {
            // Wait until released or cancelled, then return a successful response.
            await Task.Run(() => releaseAll.Wait(ct), ct);
            return ResponseFor("late");
        });

        var queue = new ParallelDownloadQueue(
            new Uri("https://demo.example/sampleresults(g)"),
            "TOK",
            () => new HttpClient(handler),
            100, 10, 1, "application/x-protobuf",
            info => info.SpectrumEndpoint = "https://demo.example/x",
            (_, _) => { });

        var task = queue.GetChunkTaskAsync(0, false, true);
        queue.Dispose();
        // The task should fault with a cancellation/operation-canceled rather than hang. Wait
        // briefly to confirm; AggregateException unwraps to OperationCanceledException.
        Assert.ThrowsException<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(5)));
        releaseAll.Set();
    }

    private static HttpResponseMessage ResponseFor(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/octet-stream"),
        };

    /// <summary>HttpClient handler that delegates each request to a synchronous or async
    /// callback. Sync overload covers the simple cases; async covers cancellation tests.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
            : this((req, ct) => Task.FromResult(respond(req, ct))) { }

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _respond(request, cancellationToken);

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => _respond(request, cancellationToken).GetAwaiter().GetResult();
    }
}
