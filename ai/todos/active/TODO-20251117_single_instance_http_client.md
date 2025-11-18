# TODO-20251117_single_instance_http_client.md

## Branch Information
- **Branch**: `Skyline/work/20251117_single_instance_http_client`
- **Created**: 2025-11-17
- **Status**: In progress
- **Objective**: Eliminate thread handle leaks caused by repeated `HttpClient` creation/disposal by implementing a singleton `HttpClient` pattern in `HttpClientWithProgress`, following Microsoft's official guidance.

## Objective
Eliminate thread handle leaks caused by repeated `HttpClient` creation/disposal by implementing a singleton `HttpClient` pattern in `HttpClientWithProgress`, following Microsoft's official guidance.

## CRITICAL: The Usage Pattern Does NOT Change
**The `HttpClientWithProgress` class remains disposable and is used exactly as before:**
```csharp
// This pattern stays EXACTLY THE SAME
using var httpClient = new HttpClientWithProgress(progressMonitor, status, cookieContainer);
httpClient.AddAuthorizationHeader("Basic abc123");
httpClient.DownloadString(uri);
// Dispose() is still called, but it no longer disposes the underlying HttpClient
```

**What changes is INTERNAL ONLY:**
- Each `HttpClientWithProgress` instance uses a **static shared `HttpClient`**
- The static `HttpClient` is created once on first use
- The static `HttpClient` lives until process shutdown (never disposed)
- `HttpClientWithProgress.Dispose()` becomes a no-op (or just cleans up instance state)
- Per-request state (progress, cookies, headers) is stored in each `HttpClientWithProgress` instance
- Per-request configuration is applied via `HttpRequestMessage`, not `DefaultRequestHeaders`

**Key Principle**: `HttpClientWithProgress` is a lightweight wrapper around a shared static `HttpClient`. Creating/disposing `HttpClientWithProgress` instances is cheap - it's only allocating small objects (progress monitors, dictionaries). The expensive `HttpClient` object is created once and reused.

## Background

### Problem Discovery
After merging the Panorama WebClient→HttpClient migration (`TODO-20251023_panorama_webclient_replacement.md`), nightly tests began failing with leaked thread handles in `TestPanoramaDownloadFile`. Investigation revealed:

1. **Test harness detection**: `TestRunnerLib.RunTests.cs` detects thread handle leaks reliably
2. **Named threads pass**: Added named threads to `LongWaitDlg` via `ActionUtil.RunAsync()` with `Assume.IsNotNull(threadName)` - these are NOT leaking
3. **Unnamed threads leak**: Growing numbers of unnamed threads persist even after `HttpClientWithProgress.Dispose()` is called
4. **Minimal reproduction**: Simply showing `PanoramaFilePicker` and canceling it (without downloading files) causes the leak

### Root Cause
The current pattern in `HttpClientWithProgress`:
```csharp
public HttpClientWithProgress(...)
{
    var handler = new HttpClientHandler { /* config */ };
    _httpClient = new HttpClient(handler);
}

public void Dispose()
{
    _httpClient?.Dispose();
}
```

**Every HTTP request creates a new `HttpClientWithProgress` via `using` statements:**
- `HttpPanoramaRequestHelper.CreateHttpClient()` - creates new instance per request
- `PanoramaFilePicker.GetJson()` - creates new `HttpPanoramaRequestHelper` per JSON fetch
- Each folder selection in the UI triggers multiple HTTP requests
- Each `HttpClient` creates internal worker threads via `SocketsHttpHandler` for connection pooling

**The problem**: `HttpClient.Dispose()` doesn't immediately clean up internal thread pool threads. These threads persist in the .NET runtime and accumulate faster than they're garbage collected.

### Microsoft's Official Guidance
From [Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient):

> **HttpClient is intended to be instantiated once and reused throughout the life of an application.** Instantiating an HttpClient class for every request will exhaust the number of sockets available under heavy loads. This will result in SocketException errors.

Key points:
1. `HttpClient` is **thread-safe** - designed for concurrent use
2. `HttpClient` should be **long-lived** - singleton or near-singleton per application
3. Creating/disposing repeatedly causes **socket exhaustion and thread leaks**
4. Connection pooling and DNS refresh are handled internally when reused

## Current Architecture

### HttpClientWithProgress Usage Patterns

**Pattern 1: Short-lived instances (ANTI-PATTERN - causes leaks)**
```csharp
// In HttpPanoramaRequestHelper.DoGet():
using var httpClient = CreateHttpClient();  // New instance
httpClient.ShowTransferSize = false;
return httpClient.DownloadString(uri);      // Disposed after this call
```

**Pattern 2: One-shot operations**
```csharp
// In WebPanoramaClient.ValidateUri():
using var httpClient = new HttpClientWithProgress(new SilentProgressMonitor());
httpClient.DownloadString(uri);
```

**Pattern 3: File downloads with progress**
```csharp
// In WebPanoramaClient.DownloadFile():
using var httpClient = new HttpClientWithProgress(progressMonitor, progressStatus);
if (pServer.HasUserAccount())
    httpClient.AddAuthorizationHeader(pServer.AuthHeader);
httpClient.DownloadFile(fileUrl, fileName, fileSize);
```

### Per-Instance State (Challenges for Singleton Pattern)

`HttpClientWithProgress` currently has **per-operation state**:
1. **Progress reporting**: `_progressMonitor`, `_progressStatus` - different for each operation
2. **Session management**: `_cookieContainer` - different per Panorama server/session
3. **Authentication**: Authorization header via `AddAuthorizationHeader()` - different per server
4. **Custom headers**: CSRF tokens via `AddHeader()` - different per session
5. **Configuration**: `ShowTransferSize` - different per operation type (file vs API call)

### Current Lifecycle

**`HttpPanoramaRequestHelper` creates many instances:**
```csharp
public override string DoGet(Uri uri)
{
    using var httpClient = CreateHttpClient();  // New HttpClient #1
    httpClient.ShowTransferSize = false;
    return httpClient.DownloadString(uri);
}

public override byte[] DoPost(Uri uri, NameValueCollection postData)
{
    using var httpClient = CreateHttpClient();  // New HttpClient #2
    if (!string.IsNullOrEmpty(_csrfToken))
        httpClient.AddHeader(LABKEY_CSRF, _csrfToken);
    return Encoding.UTF8.GetBytes(httpClient.UploadString(...));
}

private void GetCsrfTokenFromServer()
{
    using var httpClient = CreateHttpClient();  // New HttpClient #3
    httpClient.DownloadString(new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH));
    _csrfToken = httpClient.GetCookie(new Uri(_serverUri, "/"), LABKEY_CSRF);
}
```

**A single `PanoramaFilePicker` dialog session can create dozens of `HttpClient` instances** just loading folder trees.

## Proposed Solution

### Architecture: Singleton HttpClient with Per-Request Configuration

**Key Principle**: Separate the **long-lived connection management** (singleton `HttpClient`) from **per-request configuration** (headers, cookies, progress).

### Phase 1: Create Static HttpClient Pool

**New internal class: `HttpClientPool`**
```csharp
// In HttpClientWithProgress.cs
internal static class HttpClientPool
{
    private static readonly Lazy<HttpClient> _instance = new Lazy<HttpClient>(CreateHttpClient);

    public static HttpClient Instance => _instance.Value;

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = true,
            Proxy = WebRequest.DefaultWebProxy,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseDefaultCredentials = true,
            PreAuthenticate = true,
            UseCookies = false,  // Per-request cookies via HttpRequestMessage
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),  // DNS refresh
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
        };
        if (handler.Proxy != null)
        {
            handler.Proxy.Credentials = CredentialCache.DefaultCredentials;
        }

        return new HttpClient(handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan  // We handle timeouts per-chunk
        };
    }
}
```

**Why `SocketsHttpHandler` instead of `HttpClientHandler`?**
- More control over connection pooling behavior
- `PooledConnectionLifetime` - automatic DNS refresh (important for long-running processes)
- Better performance and configurability
- `HttpClientHandler` is a wrapper around `SocketsHttpHandler` anyway

**Why `disposeHandler: false`?**
- The handler should live for the lifetime of the process
- Prevents disposal cleanup issues
- Pool cleanup happens at process termination

### Phase 2: Refactor HttpClientWithProgress to Use Singleton

**CRITICAL: The class remains `IDisposable` and `using` statements work exactly as before. Only the internal implementation changes.**

**Before (creates new HttpClient - CAUSES THREAD LEAKS):**
```csharp
public class HttpClientWithProgress : IDisposable
{
    private readonly HttpClient _httpClient;          // Per-instance (BAD)

    public HttpClientWithProgress(...)
    {
        // Creating NEW HttpClient + HttpClientHandler spawns internal threads
        var handler = new HttpClientHandler { /* config */ };
        _httpClient = new HttpClient(handler);
        // ...
    }

    public void Dispose()
    {
        // Disposing HttpClient doesn't immediately clean up internal threads
        _httpClient?.Dispose();
    }
}
```

**After (uses singleton HttpClient - NO THREAD LEAKS):**
```csharp
public class HttpClientWithProgress : IDisposable
{
    private static readonly HttpClient _sharedHttpClient = HttpClientPool.Instance;  // STATIC - shared by all instances

    // Per-instance state (lightweight - just object references, no threads)
    private readonly CookieContainer _cookieContainer;
    private readonly IProgressMonitor _progressMonitor;
    private IProgressStatus _progressStatus;
    private string _authHeader;  // NEW - store auth header for per-request use
    private readonly Dictionary<string, string> _customHeaders = new Dictionary<string, string>();  // NEW

    public HttpClientWithProgress(...)
    {
        // Only allocating small objects - no HttpClient creation, no thread spawning
        _progressMonitor = progressMonitor ?? new SilentProgressMonitor();
        _progressStatus = status ?? new ProgressStatus();
        _cookieContainer = cookieContainer;  // Store reference, don't configure on handler
    }

    public void Dispose()
    {
        // Nothing to dispose - we don't own the HttpClient
        // The static _sharedHttpClient lives until process shutdown
        // CookieContainer is managed by caller (e.g., HttpPanoramaRequestHelper)
    }
}
```

**Why this works:**
- Creating/disposing `HttpClientWithProgress` is now cheap (just small object allocations)
- The expensive `HttpClient` object with its internal threads is created once
- All `using` statements continue to work - they just call an empty `Dispose()`
- No code outside `HttpClientWithProgress` needs to change

### Phase 3: Handle Per-Request Configuration

**CRITICAL CONCEPT**: With a shared static `HttpClient`, we CANNOT use `DefaultRequestHeaders` (that would affect ALL requests from ALL threads). Instead, we must use per-request configuration via `HttpRequestMessage`.

**Challenge**: How to handle cookies, auth headers, and custom headers with a shared `HttpClient`?

**Solution**:
1. Store per-request state in `HttpClientWithProgress` instance fields
2. Apply state to each `HttpRequestMessage` when making a request
3. Extract response cookies after each request completes

**Current pattern (modifies shared state - WRONG for singleton):**
```csharp
// This modifies HttpClient.DefaultRequestHeaders which is shared across all threads!
_httpClient.DefaultRequestHeaders.Add("Authorization", authHeaderValue);
_httpClient.DefaultRequestHeaders.Add("X-LABKEY-CSRF", csrfToken);
```

**New pattern (per-request state - CORRECT for singleton):**
```csharp
// Store in instance fields when API is called
public void AddAuthorizationHeader(string authHeaderValue)
{
    _authHeader = authHeaderValue;  // Store for later use
}

public void AddHeader(string name, string value)
{
    _customHeaders[name] = value;  // Store for later use
}

// Apply to HttpRequestMessage when making the actual request
private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
{
    var request = new HttpRequestMessage(method, uri);

    // Now add headers to THIS request only
    if (_authHeader != null)
        request.Headers.Add("Authorization", _authHeader);

    foreach (var header in _customHeaders)
        request.Headers.Add(header.Key, header.Value);

    return request;
}
```

```csharp
private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
{
    var request = new HttpRequestMessage(method, uri);

    // Add per-request headers
    if (_authHeader != null)
        request.Headers.Add("Authorization", _authHeader);
    if (_customHeaders != null)
    {
        foreach (var header in _customHeaders)
            request.Headers.Add(header.Key, header.Value);
    }

    // Add cookies to request
    if (_cookieContainer != null)
    {
        var cookies = _cookieContainer.GetCookies(uri);
        if (cookies.Count > 0)
        {
            var cookieHeader = string.Join("; ",
                cookies.Cast<Cookie>().Select(c => $"{c.Name}={c.Value}"));
            request.Headers.Add("Cookie", cookieHeader);
        }
    }

    return request;
}
```

**For responses with Set-Cookie headers:**
```csharp
private void ProcessResponseCookies(HttpResponseMessage response, Uri uri)
{
    if (_cookieContainer != null && response.Headers.TryGetValues("Set-Cookie", out var cookies))
    {
        foreach (var cookie in cookies)
        {
            _cookieContainer.SetCookies(uri, cookie);
        }
    }
}
```

### Phase 4: Refactor Download/Upload Methods

**Current `DownloadString()` - creates request implicitly:**
```csharp
public string DownloadString(Uri uri)
{
    using var memoryStream = new MemoryStream();
    DownloadToStream(uri, memoryStream);
    return Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
}

private void DownloadToStream(Uri uri, Stream destination, long knownTotalBytes = 0)
{
    var response = WithExceptionHandling(uri, () =>
        _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead,
                             _progressMonitor.CancellationToken).Result);
    // ...
}
```

**New pattern - explicit request creation:**
```csharp
public string DownloadString(Uri uri)
{
    using var memoryStream = new MemoryStream();
    DownloadToStream(uri, memoryStream);
    return Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
}

private void DownloadToStream(Uri uri, Stream destination, long knownTotalBytes = 0)
{
    using var request = CreateRequest(HttpMethod.Get, uri);
    var response = WithExceptionHandling(uri, () =>
        _sharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                                     _progressMonitor.CancellationToken).Result);

    ProcessResponseCookies(response, uri);  // Extract Set-Cookie headers

    // Rest of download logic unchanged
    // ...
}
```

### Phase 5: Update API to Store Per-Request State

**CRITICAL: The public API remains EXACTLY the same. Only internal behavior changes.**

**Usage (UNCHANGED - this is the entire point!):**
```csharp
// This code does NOT change at all
var httpClient = new HttpClientWithProgress(progressMonitor, status, cookieContainer);
httpClient.AddAuthorizationHeader("Basic abc123");
httpClient.AddHeader("X-LABKEY-CSRF", token);
httpClient.ShowTransferSize = false;
httpClient.DownloadString(uri);
```

**What changes internally:**

**Before (modified shared HttpClient state - BREAKS with singleton):**
```csharp
public void AddAuthorizationHeader(string authHeaderValue)
{
    _httpClient.DefaultRequestHeaders.Add("Authorization", authHeaderValue);
    // Problem: This modifies the SHARED static HttpClient!
    // All other threads will see this header on their requests!
}
```

**After (stores in instance field - WORKS with singleton):**
```csharp
public void AddAuthorizationHeader(string authHeaderValue)
{
    _authHeader = authHeaderValue;  // Store in instance field
    // Will be applied per-request in CreateRequest()
}
```

**Implementation of storage fields:**
```csharp
public class HttpClientWithProgress : IDisposable
{
    private string _authHeader;
    private readonly Dictionary<string, string> _customHeaders = new Dictionary<string, string>();

    public void AddAuthorizationHeader(string authHeaderValue)
    {
        _authHeader = authHeaderValue;
    }

    public void AddHeader(string name, string value)
    {
        _customHeaders[name] = value;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);

        if (_authHeader != null)
            request.Headers.Add("Authorization", _authHeader);

        foreach (var header in _customHeaders)
            request.Headers.Add(header.Key, header.Value);

        // Add cookies from _cookieContainer
        if (_cookieContainer != null)
        {
            var cookies = _cookieContainer.GetCookies(uri);
            if (cookies.Count > 0)
            {
                var cookieHeader = string.Join("; ",
                    cookies.Cast<Cookie>().Select(c => $"{c.Name}={c.Value}"));
                request.Headers.Add("Cookie", cookieHeader);
            }
        }

        return request;
    }
}
```

**This preserves the current API while changing internal implementation!**

## Implementation Plan

**IMPORTANT: Read this section carefully. The order matters.**

### Phase 1: Create Static HttpClient Pool ✅
- [ ] Add `HttpClientPool` internal static class to `HttpClientWithProgress.cs`
- [ ] Use `Lazy<HttpClient>` for thread-safe singleton initialization
- [ ] Use `SocketsHttpHandler` (not `HttpClientHandler`) with connection pooling configuration
- [ ] Set `UseCookies = false` on handler (we'll handle cookies per-request via HttpRequestMessage)
- [ ] Configure DNS refresh via `PooledConnectionLifetime = TimeSpan.FromMinutes(2)`
- [ ] Pass `disposeHandler: false` to HttpClient constructor (handler lives forever)
- [ ] Set `HttpClient.Timeout = Timeout.InfiniteTimeSpan` (we handle per-chunk timeouts)
- [ ] Verify singleton works: Create multiple `HttpClientWithProgress` instances, verify they share same `HttpClient` instance
- [ ] **TEST**: No threads created until first HTTP request made (lazy initialization)

### Phase 2: Add Per-Request State Storage ✅
- [ ] **REMOVE** `private readonly HttpClient _httpClient;` instance field
- [ ] **ADD** `private static readonly HttpClient _sharedHttpClient = HttpClientPool.Instance;` static field
- [ ] **ADD** `private string _authHeader;` field to store Authorization header
- [ ] **ADD** `private readonly Dictionary<string, string> _customHeaders = new Dictionary<string, string>();`
- [ ] **KEEP** `_cookieContainer` field (already exists, stores per-session cookies)
- [ ] **MODIFY** `AddAuthorizationHeader()` to store in `_authHeader` instead of `DefaultRequestHeaders`
- [ ] **MODIFY** `AddHeader()` to store in `_customHeaders` dictionary instead of `DefaultRequestHeaders`
- [ ] **TEST**: Create instance, add headers, verify they're stored in fields (not sent yet)
- [ ] **TEST**: Create two instances with different headers, verify they don't interfere

### Phase 3: Implement CreateRequest() Helper ✅
- [ ] Add `CreateRequest(HttpMethod method, Uri uri)` method
- [ ] Build `HttpRequestMessage` with stored auth header
- [ ] Add custom headers from `_customHeaders` dictionary
- [ ] Extract cookies from `_cookieContainer` and add to request
- [ ] Add unit tests for cookie handling

### Phase 4: Implement ProcessResponseCookies() Helper ✅
- [ ] Add `ProcessResponseCookies(HttpResponseMessage response, Uri uri)` method
- [ ] Extract `Set-Cookie` headers from response
- [ ] Add cookies to `_cookieContainer`
- [ ] Handle cookie parsing edge cases
- [ ] Add unit tests for cookie round-tripping

### Phase 5: Refactor All HTTP Methods ✅

**CRITICAL: Change internal implementation but preserve exact external behavior.**

- [ ] Update `DownloadToStream()`:
  - Replace `_httpClient.GetAsync()` with `_sharedHttpClient.SendAsync(CreateRequest(HttpMethod.Get, uri))`
  - Add `ProcessResponseCookies(response, uri)` after getting response
  - Verify progress reporting still works
  - Verify cancellation still works
- [ ] Update `UploadFromStream()`:
  - Create request via `CreateRequest(HttpMethod.Post, uri)`
  - Set `request.Content = new StreamContent(...)`
  - Replace `_httpClient.SendAsync()` with `_sharedHttpClient.SendAsync(request)`
  - Add `ProcessResponseCookies(response, uri)` after getting response
  - Verify progress reporting still works
- [ ] Update `UploadString()`:
  - Use `CreateRequest()` to build request with stored headers
  - Set `request.Content = new StringContent(...)`
  - Call `_sharedHttpClient.SendAsync(request)`
- [ ] Update `UploadValues()`:
  - Use `CreateRequest()` to build request
  - Set `request.Content = new FormUrlEncodedContent(...)`
- [ ] Update `SendRequest(HttpRequestMessage request)`:
  - Currently takes pre-built request - need to ADD headers to it
  - Extract stored headers and add to the passed-in request
  - Call `_sharedHttpClient.SendAsync(request)`
  - Add `ProcessResponseCookies()`
- [ ] **Find and replace ALL `_httpClient.` with `_sharedHttpClient.`** (should be in every HTTP method)
- [ ] Verify all methods preserve existing behavior with unit tests

### Phase 6: Update Dispose() ✅
- [ ] Remove `_httpClient?.Dispose()` (no instance field anymore)
- [ ] Make `Dispose()` a no-op (or just clear instance state if needed)
- [ ] Add comment: "Static HttpClient is never disposed - lives until process shutdown"
- [ ] Verify `using` statements still compile and work (they call empty Dispose())
- [ ] **CRITICAL**: Verify this doesn't break any code - all `using` statements stay in place

### Phase 7: Testing ✅
- [ ] Run all existing `HttpClientWithProgressIntegrationTest` tests - should pass unchanged
- [ ] Run `PanoramaClientPublishTest` - should pass unchanged
- [ ] Run `PanoramaClientDownloadTest.TestPanoramaDownloadFile` - **should NOT leak threads**
- [ ] Run full nightly test suite with thread leak detection
- [ ] Verify handle count doesn't grow across multiple test iterations
- [ ] Add specific test for thread leak regression

### Phase 8: Validate Thread Leak Fix ✅
- [ ] Run `TestRunner.exe` with leak detection enabled
- [ ] Verify thread handle count is stable (not growing)
- [ ] Check that only named threads remain (no unnamed threads from HttpClient)
- [ ] Run test multiple iterations (10+) to confirm no accumulation
- [ ] Test in Docker/parallel mode to ensure no cross-contamination

### Phase 9: Documentation ✅
- [ ] Update `HttpClientWithProgress` class documentation
- [ ] Add XML comments explaining singleton pattern
- [ ] Document why `Dispose()` is a no-op
- [ ] Add comments about thread-safety guarantees
- [ ] Update `MEMORY.md` with HttpClient singleton pattern guidance
- [ ] Add to `CRITICAL-RULES.md`: "Always use HttpClientWithProgress, never create HttpClient directly"

## Key Files to Modify

### Core Implementation
- **`pwiz_tools/Shared/CommonUtil/SystemUtil/HttpClientWithProgress.cs`** - Main refactoring
  - Add `HttpClientPool` internal class
  - Add per-request state fields (`_authHeader`, `_customHeaders`)
  - Add `CreateRequest()` and `ProcessResponseCookies()` helpers
  - Refactor all HTTP methods to use `CreateRequest()`
  - Update `Dispose()` to be a no-op
  - Replace `_httpClient` with `_sharedHttpClient`

### No Changes Required (API preserved)
- **`pwiz_tools/Shared/PanoramaClient/RequestHelper.cs`** - `HttpPanoramaRequestHelper`
  - No changes needed! API is identical
- **`pwiz_tools/Shared/PanoramaClient/PanoramaClient.cs`** - `WebPanoramaClient`
  - No changes needed! API is identical
- **`pwiz_tools/Shared/PanoramaClient/PanoramaFilePicker.cs`**
  - No changes needed! API is identical

### Tests
- **`pwiz_tools/Skyline/TestFunctional/HttpClientWithProgressIntegrationTest.cs`** - Should pass unchanged
- **`pwiz_tools/Skyline/TestConnected/PanoramaClientDownloadTest.cs`** - Should pass WITHOUT thread leaks
- **`pwiz_tools/Skyline/TestRunnerLib/RunTests.cs`** - Thread leak detection (already in place)

## Success Criteria

### Functional Requirements ✅
- [ ] All existing tests pass without modification
- [ ] Cookie-based authentication still works (Panorama sessions)
- [ ] Authorization headers work correctly
- [ ] CSRF token headers work correctly
- [ ] Progress reporting unchanged
- [ ] Cancellation behavior unchanged
- [ ] Error handling unchanged (NetworkRequestException, etc.)

### Performance Requirements ✅
- [ ] No thread handle leaks detected by `TestRunner.exe`
- [ ] Thread count stable across multiple test iterations
- [ ] No unnamed threads accumulate
- [ ] Connection pooling works (faster subsequent requests)
- [ ] DNS refresh happens automatically (via `PooledConnectionLifetime`)

### Code Quality Requirements ✅
- [ ] Public API unchanged (backward compatible)
- [ ] No new ReSharper warnings
- [ ] All methods have XML documentation
- [ ] Unit tests cover new helper methods
- [ ] Integration tests verify end-to-end behavior

## Risks & Mitigations

### Risk: Cookie Handling Edge Cases
**Problem**: Moving from `HttpClientHandler.CookieContainer` (automatic) to manual cookie extraction might miss edge cases.

**Mitigation**:
- Comprehensive unit tests for cookie round-tripping
- Test with real Panorama server (existing connected tests)
- Review `Set-Cookie` parsing for all cookie attributes (Domain, Path, Secure, HttpOnly)

### Risk: Thread Safety Issues
**Problem**: Shared `HttpClient` with concurrent requests could cause issues.

**Mitigation**:
- `HttpClient` is documented as thread-safe by Microsoft
- Per-request state is isolated via `HttpRequestMessage`
- Each `HttpClientWithProgress` instance has its own `_cookieContainer`
- Existing tests already run in parallel (nightly test suite)

### Risk: Connection Pool Exhaustion
**Problem**: Single `HttpClient` might limit concurrent connections.

**Mitigation**:
- `SocketsHttpHandler` has a default `MaxConnectionsPerServer = int.MaxValue`
- Connection pooling actually **improves** concurrent performance
- Can configure `MaxConnectionsPerServer` if needed
- Monitor connection metrics during nightly tests

### Risk: Breaking Existing Code
**Problem**: Subtle behavior changes could break working code.

**Mitigation**:
- Preserve exact public API (no signature changes)
- Run full test suite (functional, connected, nightly)
- Test both Skyline.exe and SkylineBatch/AutoQC
- Add regression test for thread leaks

## Alternative Approaches Considered

### Alternative 1: Reduce CreateHttpClient() Frequency
**Approach**: Reuse `HttpPanoramaRequestHelper` (which creates `HttpClientWithProgress`) for multiple requests instead of creating per-request.

**Rejected because**:
- Reduces frequency but doesn't eliminate root cause
- Thread leaks still accumulate, just slower
- Doesn't follow Microsoft's guidance (singleton is THE solution)
- Band-aid solution that requires changing usage patterns throughout codebase
- Still creates/disposes HttpClient instances, just less often
- Our investigation showed this was initially considered but rejected in favor of proper fix

### Alternative 2: Force Thread Cleanup
**Approach**: Call `GC.Collect()` or `ThreadPool.SetMinThreads()` after disposal.

**Rejected because**:
- Doesn't work - .NET runtime controls thread pool threads
- Can't force cleanup of `SocketsHttpHandler` internal threads
- Performance impact from frequent GC
- Hack, not a solution

### Alternative 3: Multiple HttpClient Pool
**Approach**: Pool of `HttpClient` instances per configuration (with cookies, without, etc.).

**Rejected because**:
- More complex than singleton
- Doesn't provide significant benefit
- Connection pooling works best with single instance
- Cookies are handled per-request, not per-handler

### Alternative 4: IHttpClientFactory (.NET Core pattern)
**Approach**: Use `IHttpClientFactory` for managed `HttpClient` lifecycle.

**Rejected because**:
- Requires dependency injection framework
- Overkill for our use case
- Adds complexity for minimal benefit
- Singleton pattern is simpler and sufficient

## References

### Microsoft Documentation
- [HttpClient Class](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) - Official guidance on singleton pattern
- [SocketsHttpHandler](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler) - Connection pooling configuration
- [You're using HttpClient wrong](https://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/) - Classic blog post on the anti-pattern
- [HttpClient Lifetime Management](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests) - Connection exhaustion issues

### Related TODOs
- **`TODO-20251023_panorama_webclient_replacement.md`** (completed) - Original WebClient→HttpClient migration that introduced this issue
- **`TODO-tools_webclient_replacement.md`** (backlog) - Will benefit from this fix

### Thread Leak Investigation Notes
- **File**: `pwiz_tools/Skyline/TestConnected/PanoramaClientDownloadTest.cs:99-112` - Minimal reproduction (show dialog, cancel)
- **File**: `pwiz_tools/Skyline/TestRunnerLib/RunTests.cs:515-532` - Thread handle leak detection code
- **File**: `pwiz_tools/Skyline/Util/Extensions/ActionUtil.cs:56` - `Assume.IsNotNull(threadName)` added to detect unnamed threads
- **File**: `pwiz_tools/Skyline/Controls/LongWaitDlg.cs:160` - Named thread creation via `ActionUtil.RunAsync()`

## Handoff Prompt for Branch Creation

```
I want to start work on eliminating HttpClient thread leaks from TODO-single-instance-http-client.md.

This work follows investigation of thread leaks in TestPanoramaDownloadFile after the Panorama WebClient→HttpClient migration. The root cause is repeated HttpClient creation/disposal creating unnamed internal threads that persist. Microsoft's official guidance is to use a singleton HttpClient.

Please:
1. Read ai/todos/STARTUP.md for workflow
2. Read CRITICAL-RULES.md and MEMORY.md for project context
3. Create branch: Skyline/work/YYYYMMDD_single_instance_http_client (use today's date)
4. Move TODO file from backlog/ to active/ with date prefix
5. Update the TODO file header with actual branch information
6. Begin Phase 1: Create Static HttpClient Pool

CRITICAL UNDERSTANDING:
- The usage pattern does NOT change - all `using var httpClient = new HttpClientWithProgress(...)` statements stay exactly as they are
- Only the INTERNAL implementation changes - each instance uses a static shared HttpClient
- HttpClientWithProgress.Dispose() becomes a no-op (or just clears instance state)
- The static HttpClient is created once on first use and lives until process shutdown
- Per-request state (progress, cookies, headers) is stored in instance fields and applied via HttpRequestMessage
- This preserves thread-safety because HttpClient is thread-safe and per-request state is isolated

Key context: This refactoring preserves the public API of HttpClientWithProgress but changes internals to use a singleton HttpClient with per-request headers/cookies. The goal is zero thread leaks while maintaining all existing functionality and all existing usage patterns.
```
