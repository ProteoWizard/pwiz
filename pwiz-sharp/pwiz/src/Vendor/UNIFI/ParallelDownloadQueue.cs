using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Concurrent;

namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// Carries inputs + outputs for one chunked-download task. Mirrors cpp
/// <c>ParallelDownloadQueue::DownloadInfo</c> (ParallelDownloadQueue.hpp:63-93).
/// </summary>
/// <remarks>
/// The two callback delegates the queue is constructed with mutate this object:
/// <list type="bullet">
///   <item><see cref="SpectrumEndpoint"/> is set by the URL-builder callback after it reads
///   <see cref="TaskIndex"/> + <see cref="ChunkSize"/>.</item>
///   <item><see cref="BytesDownloaded"/> + <see cref="LastSpectrumRetrievedTime"/> are set by
///   the queue once the response is fetched.</item>
/// </list>
/// </remarks>
public sealed class UnifiDownloadInfo
{
    /// <summary>Task / chunk index assigned by the queue.</summary>
    public int TaskIndex { get; set; }

    /// <summary>True when the caller wants centroided data (UNIFI exposes both modes).</summary>
    public bool GetCentroidData { get; set; }

    /// <summary>Number of bytes the queue read from the response stream.</summary>
    public long BytesDownloaded { get; set; }

    /// <summary>Number of spectra in this chunk. Both queue and URL-builder callback may
    /// adjust it before the request goes out (the last chunk is typically truncated).</summary>
    public int ChunkSize { get; set; }

    /// <summary>First spectrum index in the chunk (mutable — the URL-builder callback may
    /// rebase this for waters_connect's per-channel scan-index addressing).</summary>
    public int SpectrumIndexStart { get; set; }

    /// <summary>One-past-last spectrum index in the chunk.</summary>
    public int SpectrumIndexEnd { get; set; }

    /// <summary>Managed thread id that ran the task — informational.</summary>
    public int CurrentThreadId { get; set; }

    /// <summary>Marker for the moment the queue handed the response stream to the consumer
    /// callback. cpp uses this for stall-detection diagnostics.</summary>
    public DateTime LastSpectrumRetrievedTime { get; set; }

    /// <summary>Opaque pointer the queue's owner threads through; the URL-builder callback
    /// uses it to recover its per-instance state without taking a managed closure.</summary>
    public object? UserData { get; set; }

    /// <summary>Set by the URL-builder callback to the absolute URL the chunk should fetch.</summary>
    public string SpectrumEndpoint { get; set; } = string.Empty;
}

/// <summary>
/// Concurrent chunk-by-chunk download queue for UNIFI / waters_connect spectrum endpoints.
/// C# port of cpp <c>ParallelDownloadQueue</c> (ParallelDownloadQueue.cpp).
/// </summary>
/// <remarks>
/// <para>The cpp port runs two task lanes (primary + readahead) using a <c>QueuedTaskScheduler</c>
/// so the consumer's GetSpectrum(N) request always preempts speculative readahead for indexes
/// past N. Same idea here, simpler primitives:</para>
/// <list type="bullet">
///   <item>Task launch: <see cref="GetChunkTaskAsync"/> — looks up an existing in-flight task
///   for <c>(chunkIndex, doCentroid)</c>; launches a new one if absent.</item>
///   <item>Concurrency cap: a <see cref="SemaphoreSlim"/> sized to <c>concurrentTasks + 2</c>
///   matches the cpp HttpClient pool size; primary tasks acquire the semaphore directly,
///   readahead tasks wait for any primary tasks already enqueued first.</item>
///   <item>Retry: on transient errors, exponential backoff up to <c>MaxRetries</c>.
///   HTTP 429 is special-cased — the underlying HttpClient is rotated and the retry runs
///   without backoff (matches cpp ParallelDownloadQueue.cpp:362-380).</item>
///   <item>Cancellation: <see cref="Dispose"/> cancels all in-flight tasks.</item>
/// </list>
/// <para>The two callback delegates (URL-builder + stream-consumer) match cpp's
/// <c>spectrumEndpoint</c> + <c>getSpectraFromStream</c> action signatures so callers
/// hooking up <c>UnifiHttpClient</c> / <c>WatersConnectHttpClient</c> can reuse the same
/// shape.</para>
/// </remarks>
// CA1711 wants type names not to end in "Queue"; we keep the cpp source name
// (ParallelDownloadQueue) for traceability — the analyzer rule is wrong here.
#pragma warning disable CA1711
public sealed class ParallelDownloadQueue : IDisposable
#pragma warning restore CA1711
{
    private const int MaxRequestRetries = 15;
    private const int MaxStreamRetries = 15;

    private readonly Uri _sampleResultUrl;
    private readonly string _accessToken;
    private readonly Func<HttpClient> _httpClientFactory;
    private readonly int _numSpectra;
    private readonly int _chunkSize;
    private readonly int _concurrentTasks;
    private readonly string _acceptHeader;
    private readonly Action<UnifiDownloadInfo> _spectrumEndpoint;
    private readonly Action<Stream, UnifiDownloadInfo> _getSpectraFromStream;
    private readonly object? _userData;
    private readonly bool _debug;

    private readonly ConcurrentQueue<HttpClient> _httpClients = new();
    private readonly ConcurrentDictionary<int, Task> _profileTasks = new();
    private readonly ConcurrentDictionary<int, Task> _centroidTasks = new();
    private readonly SemaphoreSlim _primarySlots;
    private readonly SemaphoreSlim _readaheadSlots;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly DateTime _startTime = DateTime.UtcNow;
    private bool _disposed;

    /// <summary>Constructs the queue and pre-warms its <see cref="HttpClient"/> pool.</summary>
    /// <param name="sampleResultUrl">Sample-result base URI. Used as the per-client BaseAddress.</param>
    /// <param name="accessToken">Bearer token applied as the default Authorization header on every pooled client.</param>
    /// <param name="httpClientFactory">Factory for fresh <see cref="HttpClient"/>s — typically
    /// the consumer's <see cref="UnifiHttpClient"/> handler delegate.</param>
    /// <param name="numSpectra">Total spectra in the source's spectrum list. The last chunk
    /// is automatically truncated to <c>numSpectra - chunkIndex</c>.</param>
    /// <param name="chunkSize">Spectra per request.</param>
    /// <param name="concurrentTasks">Cap on simultaneously-running download tasks. cpp probes
    /// this empirically with <see cref="GetRequestLimit"/>; mirror that on the caller side.</param>
    /// <param name="acceptHeader">HTTP <c>Accept</c> header for chunk requests
    /// (<c>application/octet-stream</c> for legacy UNIFI, <c>application/x-protobuf</c> for waters_connect).</param>
    /// <param name="spectrumEndpoint">Callback that builds the chunk's URL from
    /// <c>UnifiDownloadInfo.TaskIndex</c> + <c>ChunkSize</c> and writes it to
    /// <c>UnifiDownloadInfo.SpectrumEndpoint</c>.</param>
    /// <param name="getSpectraFromStream">Callback that consumes the response stream.</param>
    /// <param name="userData">Opaque object plumbed through to both callbacks.</param>
    public ParallelDownloadQueue(
        Uri sampleResultUrl,
        string accessToken,
        Func<HttpClient> httpClientFactory,
        int numSpectra,
        int chunkSize,
        int concurrentTasks,
        string acceptHeader,
        Action<UnifiDownloadInfo> spectrumEndpoint,
        Action<Stream, UnifiDownloadInfo> getSpectraFromStream,
        object? userData = null)
    {
        ArgumentNullException.ThrowIfNull(sampleResultUrl);
        ArgumentNullException.ThrowIfNull(accessToken);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(spectrumEndpoint);
        ArgumentNullException.ThrowIfNull(getSpectraFromStream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrentTasks);

        _sampleResultUrl = sampleResultUrl;
        _accessToken = accessToken;
        _httpClientFactory = httpClientFactory;
        _numSpectra = numSpectra;
        _chunkSize = chunkSize;
        _concurrentTasks = concurrentTasks;
        _acceptHeader = acceptHeader;
        _spectrumEndpoint = spectrumEndpoint;
        _getSpectraFromStream = getSpectraFromStream;
        _userData = userData;
        _debug = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UNIFI_DEBUG"))
                 && Environment.GetEnvironmentVariable("UNIFI_DEBUG") != "0";

        // cpp ParallelDownloadQueue.cpp:137-146 pre-creates concurrentTasks+2 HttpClients so
        // there's always a spare for the readahead lane while the primary lane churns.
        for (int i = 0; i < concurrentTasks + 2; i++)
            _httpClients.Enqueue(BuildClient(_httpClientFactory, _sampleResultUrl, _accessToken, _acceptHeader));

        // Two semaphores model cpp's primary-vs-readahead schedulers: primary tasks always get
        // a slot first (within the overall cap); readahead tasks share what's left over.
        _primarySlots = new SemaphoreSlim(concurrentTasks, concurrentTasks);
        _readaheadSlots = new SemaphoreSlim(Math.Max(1, concurrentTasks / 2), concurrentTasks);
    }

    /// <summary>Total bytes consumed across all completed downloads (informational).</summary>
    public long TotalBytesDownloaded { get; private set; }

    /// <summary>Returns the in-flight or completed task for a specific chunk index. If no
    /// task exists yet, launches one on the requested lane. Subsequent calls for the same
    /// (chunkIndex, doCentroid) tuple return the same task — cpp's
    /// <c>ParallelDownloadQueue::getChunkTask</c> (ParallelDownloadQueue.cpp:253-271).</summary>
    /// <param name="chunkIndex">Zero-based chunk start spectrum index. Caller is responsible
    /// for ensuring this is a multiple of <c>chunkSize</c> when that constraint matters.</param>
    /// <param name="doCentroid">True for centroid mode; false for profile.</param>
    /// <param name="primary">True for foreground (consumer-driven) requests; false for
    /// speculative readahead.</param>
    public Task GetChunkTaskAsync(int chunkIndex, bool doCentroid, bool primary)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var bucket = doCentroid ? _centroidTasks : _profileTasks;
        return bucket.GetOrAdd(chunkIndex, idx => RunChunkAsync(idx, doCentroid, primary));
    }

    private async Task RunChunkAsync(int chunkIndex, bool doCentroid, bool primary)
    {
        SemaphoreSlim slot = primary ? _primarySlots : _readaheadSlots;
        await slot.WaitAsync(_cancellation.Token).ConfigureAwait(false);
        HttpClient? client = null;
        try
        {
            client = AcquireClient();
            var info = new UnifiDownloadInfo
            {
                TaskIndex = chunkIndex,
                GetCentroidData = doCentroid,
                ChunkSize = ComputeChunkSize(chunkIndex),
                CurrentThreadId = Environment.CurrentManagedThreadId,
                UserData = _userData,
                SpectrumIndexStart = 0,
            };
            info.SpectrumIndexEnd = info.ChunkSize;
            _spectrumEndpoint(info);
            await DownloadWithRetryAsync(info, client).ConfigureAwait(false);
            TotalBytesDownloaded += info.BytesDownloaded;
        }
        finally
        {
            if (client is not null) _httpClients.Enqueue(client);
            slot.Release();
            // cpp removes the task from its ConcurrentDictionary once finished so future
            // requests for the same chunk re-fetch (which the consumer almost never does, but
            // keeps the dictionary bounded). Mirror that.
            (doCentroid ? _centroidTasks : _profileTasks).TryRemove(chunkIndex, out _);
        }
    }

    private int ComputeChunkSize(int chunkIndex)
    {
        if (_numSpectra <= 0) return _chunkSize;
        int spectraLeft = Math.Max(0, _numSpectra - chunkIndex);
        return Math.Min(_chunkSize, spectraLeft);
    }

    private async Task DownloadWithRetryAsync(UnifiDownloadInfo info, HttpClient client)
    {
        // Two-tier retry mirrors cpp ParallelDownloadQueue.cpp:315-390 — outer (stream) loop
        // covers protobuf-decode failures, inner (request) loop covers connection-level
        // hiccups. HTTP 429 short-circuits to swap the HttpClient with no backoff.
        for (int streamAttempt = 1; streamAttempt <= MaxStreamRetries; streamAttempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await SendWithRetryAsync(client, info.SpectrumEndpoint, info.TaskIndex)
                    .ConfigureAwait(false);
                info.BytesDownloaded = response.Content.Headers.ContentLength.GetValueOrDefault(0);
                info.LastSpectrumRetrievedTime = DateTime.UtcNow;

                using var stream = await response.Content.ReadAsStreamAsync(_cancellation.Token).ConfigureAwait(false);
                _getSpectraFromStream(stream, info);
                return; // success
            }
            catch (HttpRequestException e) when (e.StatusCode == (HttpStatusCode)429)
            {
                // cpp ParallelDownloadQueue.cpp:362-380: rotate the HttpClient (server has tied
                // a connection to a user/limit), no backoff sleep — the queued retry is enough.
                if (_debug) Log($"429 from chunk {info.TaskIndex}: rotating HttpClient");
                client.Dispose();
                client = AcquireClient();
                info.BytesDownloaded = 0;
            }
            catch (Exception) when (streamAttempt < MaxStreamRetries)
            {
                if (_debug) Log($"stream retry {streamAttempt} for chunk {info.TaskIndex}");
                await BackoffAsync(streamAttempt).ConfigureAwait(false);
                info.BytesDownloaded = 0;
            }
            finally
            {
                response?.Dispose();
            }
        }
        throw new InvalidOperationException(
            $"chunk {info.TaskIndex}: stream retries exhausted ({MaxStreamRetries})");
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpClient client, string url, int taskIndex)
    {
        Exception? lastError = null;
        for (int attempt = 1; attempt <= MaxRequestRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, _cancellation.Token)
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return response;
                if (response.StatusCode == (HttpStatusCode)429)
                {
                    response.Dispose();
                    // bubble up to the outer loop's special 429 handler.
                    throw new HttpRequestException($"chunk {taskIndex} got 429", null, (HttpStatusCode)429);
                }
                lastError = new InvalidOperationException(
                    $"chunk {taskIndex}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                response.Dispose();
            }
            catch (HttpRequestException e) when (e.StatusCode == (HttpStatusCode)429) { throw; }
            catch (Exception e) when (attempt < MaxRequestRetries)
            {
                lastError = e;
                if (_debug) Log($"request retry {attempt} for chunk {taskIndex}: {e.Message}");
                await BackoffAsync(attempt).ConfigureAwait(false);
                continue;
            }
            await BackoffAsync(attempt).ConfigureAwait(false);
        }
        throw new InvalidOperationException(
            $"chunk {taskIndex}: request retries exhausted ({MaxRequestRetries})", lastError);
    }

    private Task BackoffAsync(int attempt)
        => Task.Delay(TimeSpan.FromSeconds(2 * Math.Pow(2, Math.Min(attempt, 6))), _cancellation.Token);

    private HttpClient AcquireClient()
    {
        if (_httpClients.TryDequeue(out var client)) return client;
        // Pool exhausted (shouldn't happen with the +2 buffer, but be defensive). Fabricate one.
        return BuildClient(_httpClientFactory, _sampleResultUrl, _accessToken, _acceptHeader);
    }

    private static HttpClient BuildClient(Func<HttpClient> factory, Uri sampleResultUrl, string accessToken, string acceptHeader)
    {
        var client = factory();
        client.BaseAddress = new Uri(sampleResultUrl.GetLeftPart(UriPartial.Authority));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));
        client.Timeout = TimeSpan.FromSeconds(60);
        return client;
    }

    private static void Log(string msg) => Console.Error.WriteLine($"[UNIFI] {msg}");

    /// <summary>Probes the server for an effective concurrent-request limit. Sends
    /// <paramref name="maxConcurrentTasks"/> simultaneous requests to <paramref name="url"/>
    /// and returns <c>maxConcurrentTasks</c> minus the count that came back with HTTP 429.
    /// Mirrors cpp <c>GetRequestLimit</c> (ParallelDownloadQueue.cpp:234-251). Always
    /// returns at least 1 — even fully rate-limited servers give the sequential lane.</summary>
    public static int GetRequestLimit(string url, Func<HttpClient> httpClientFactory, string accessToken, string acceptHeader, int maxConcurrentTasks)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        if (maxConcurrentTasks < 1) return 1;

        var uri = new Uri(url);
        var tasks = new List<Task<bool>>(maxConcurrentTasks);
        for (int i = 0; i < maxConcurrentTasks; i++)
            tasks.Add(Task.Run(() => ProbeOne(uri, httpClientFactory, accessToken, acceptHeader)));

        Task.WhenAll(tasks).GetAwaiter().GetResult();
        int rateLimited = tasks.Count(t => t.Result);
        return Math.Max(1, maxConcurrentTasks - rateLimited);
    }

    private static bool ProbeOne(Uri url, Func<HttpClient> factory, string accessToken, string acceptHeader)
    {
        // Returns true iff this request hit a 429. Mirrors cpp getRequestLimitTask
        // (ParallelDownloadQueue.cpp:174-231) — the cpp version does the same retry-on-error
        // dance and only flags 429 specifically.
        using var client = BuildClient(factory, url, accessToken, acceptHeader);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = client.Send(request);
            return response.StatusCode == (HttpStatusCode)429;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cancellation.Cancel(); } catch { /* best-effort */ }
        while (_httpClients.TryDequeue(out var client))
            client.Dispose();
        _primarySlots.Dispose();
        _readaheadSlots.Dispose();
        _cancellation.Dispose();
    }
}
