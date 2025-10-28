# TODO-close_panorama_client_testing_gaps.md

## Objective
Improve test coverage of `HttpPanoramaRequestHelper` by refactoring `PanoramaClientPublishTest` to use modern `HttpClientTestHelper` infrastructure and real Panorama server credentials (similar to AutoQC pattern).

## Background

### Current State (After WebClient Migration)

**Coverage Status:**
- **AutoQC:** 100% coverage of `HttpPanoramaRequestHelper` usage ✅
- **Skyline Download Paths:** 100% coverage ✅
- **Skyline Upload/Publish Paths:** 17% coverage ❌

**The Gap:**

`PanoramaClientPublishTest.cs` uses a **legacy mock pattern** that bypasses actual network code:

```csharp
// Current pattern (pre-HttpClientTestHelper):
public class TestLabKeyErrorPanoramaClient : AbstractPanoramaClient
{
    public override IRequestHelper GetRequestHelper(bool forUpload = false)
    {
        // Returns custom mocks that never touch HttpPanoramaRequestHelper
        return new TestRequestHelper(); // or NoJsonResponseRequestHelper, etc.
    }
}
```

**Custom Mock Hierarchy:**
- `TestRequestHelper` - Base mock implementation
- `NoJsonResponseRequestHelper` - Simulates bad JSON response
- `FailOnDoRequestRequestHelper` - Simulates HTTP request failures (HEAD, MOVE)
- `FailOnFileUploadRequestHelper` - Simulates upload failures with LabKey errors
- `FailOnSubmitPipelineJobRequestHelper` - Simulates pipeline job submission errors
- `FailOnCheckJobStatusRequestHelper` - Simulates job status check errors

**What Gets Tested:**
- ✅ UI error message formatting and display
- ✅ Error dialog behavior
- ✅ Integration between UI and PanoramaClient abstraction layer

**What Doesn't Get Tested:**
- ❌ Actual `HttpPanoramaRequestHelper.DoPost()` calls (0% coverage)
- ❌ CSRF token retrieval and management (0% coverage)
- ❌ File upload via `HttpClientWithProgress` (0% coverage)
- ❌ Cookie management across requests (0% coverage)
- ❌ Real network error handling in `HttpPanoramaRequestHelper`

### Why This Pattern Existed

**Historical Context:**
1. `PanoramaClientPublishTest` was written **before** `HttpClientTestHelper` existed
2. No centralized testing infrastructure for mocking HTTP operations
3. Developers created custom test interfaces to avoid calling real network code
4. Similar patterns found in:
   - `ToolStoreDlg` (had `IToolStoreClient`, refactored in Phase 1)
   - `SkypSupport` (had `IDownloadClient`, refactored in Phase 1)
   - Multiple network features rolled their own abstractions

**Technical Debt Pattern:**
- Lack of centralized `HttpClientWithProgress` infrastructure
- Each feature implemented custom network code
- Each test suite implemented custom mocking
- Result: Fragmentation, duplication, incomplete coverage

**Phase 1 WebClient Migration Payoff:**
- Created `HttpClientWithProgress` as centralized infrastructure
- Created `HttpClientTestHelper` for consistent mocking
- Refactored `SkypTest` and `ToolStoreDlgTest` to use new pattern
- **Now ready to apply same pattern to Panorama tests**

### Success Story: AutoQC Pattern

**AutoQC achieves 100% coverage** using:

1. **Real server credentials via environment variables:**
```csharp
public static string GetPanoramaWebUsername()
{
    var username = Environment.GetEnvironmentVariable("PANORAMAWEB_USERNAME");
    return string.IsNullOrWhiteSpace(username) ? DEFAULT_USER : username;
}

public static string GetPanoramaWebPassword()
{
    var password = Environment.GetEnvironmentVariable("PANORAMAWEB_PASSWORD");
    if (string.IsNullOrWhiteSpace(password))
        Assert.Fail("PANORAMAWEB_PASSWORD not set");
    return password;
}
```

2. **Real network operations in tests:**
```csharp
var requestHelper = new HttpPanoramaRequestHelper(
    new PanoramaServer(panoramaServerUri, 
        TestUtils.GetPanoramaWebUsername(),
        TestUtils.GetPanoramaWebPassword()));

// Actually calls HttpPanoramaRequestHelper.DoGet()
var jsonAsString = requestHelper.DoGet(labKeyQuery);
```

3. **Runs on real server** (`panoramaweb.org`) with test folder
4. **Falls back gracefully** when credentials not set (for local dev)

## Goals

### Primary Goals
1. **Increase coverage** of `HttpPanoramaRequestHelper` from 17% to >80%
2. **Test real network code paths** (POST, upload, CSRF tokens)
3. **Maintain existing test scenarios** (error handling, UI validation)
4. **Use modern patterns** (`HttpClientTestHelper`, environment variables)

### Secondary Goals
5. **Improve test maintainability** - Centralized mocking instead of custom classes
6. **Enable local development** - Tests pass without server access (mocked)
7. **Enable CI validation** - Tests can run with real credentials on TeamCity
8. **Document best practices** - Serve as example for future network tests

## Implementation Plan

### Phase 1: Analysis & Setup

**1.1 Understand Current Test Scenarios**

Document what each test case validates:
- `TestDisplayLabKeyErrors()` - Main test method
- Bad JSON response handling
- File upload failures (with LabKey error JSON)
- HEAD request failures (file confirmation)
- MOVE request failures (file rename)
- Pipeline job submission failures
- Pipeline job status check failures

**1.2 Review AutoQC Test Pattern**

Study successful pattern:
- `AutoQCTest/PanoramaTest.cs` - Main test file
- `AutoQCTest/TestUtils.cs` - Credential helpers
- Environment variable usage
- Real vs. mock execution paths

**1.3 Set Up Environment Variables** (Optional - for real server tests)

```powershell
# PowerShell - User-level persistent variables (restart VS after setting):
[Environment]::SetEnvironmentVariable("PANORAMAWEB_USERNAME", "your.email@example.com", "User")
[Environment]::SetEnvironmentVariable("PANORAMAWEB_PASSWORD", "your_password", "User")

# To clear:
[Environment]::SetEnvironmentVariable("PANORAMAWEB_USERNAME", $null, "User")
[Environment]::SetEnvironmentVariable("PANORAMAWEB_PASSWORD", $null, "User")
```

**SECURITY NOTE:** Never commit credentials. Always use environment variables.

### Phase 2: Refactor to HttpClientTestHelper Pattern

**Approach A: Mock-Based Testing (Recommended First)**

Refactor tests to use `HttpClientTestHelper` without requiring real server:

```csharp
// Old pattern (bypasses network code):
var publishClient = new TestLabKeyErrorPublishClient(...);
// Uses custom mock that never calls HttpPanoramaRequestHelper

// New pattern (tests real code with mocked HTTP):
using var helper = HttpClientTestHelper.SimulateHttpStatus(HttpStatusCode.InternalServerError);
helper.SetMockResponseBody(@"{""exception"": ""Couldn't create file on server"", ""statusCode"": 500}");

// Use REAL WebPanoramaClient, mocked at HTTP level
var panoramaClient = new WebPanoramaClient(serverUri, username, password);
panoramaClient.SendZipFile(...); // Executes real HttpPanoramaRequestHelper code!

// Verify error handling
var errorMessage = WaitForOpenForm<MessageDlg>().Message;
Assert.IsTrue(errorMessage.Contains("Couldn't create file"));
```

**Benefits:**
- Tests **real** `HttpPanoramaRequestHelper` code
- No network access required (fast, reliable)
- Covers all code paths (POST, CSRF, uploads)
- Easy to simulate edge cases (timeouts, malformed responses)

**Test Scenarios to Add:**

1. **Successful file upload:**
```csharp
using var helper = HttpClientTestHelper.SimulateUploadSuccess();
helper.SetMockResponseJson(@"{""success"": true}");

var panoramaClient = new WebPanoramaClient(...);
panoramaClient.SendZipFile(zipPath, folderPath);

// Validate uploaded data
var uploadedData = helper.GetCapturedUploadStream();
Assert.AreEqual(expectedZipBytes, uploadedData);
```

2. **CSRF token flow:**
```csharp
using var helper = HttpClientTestHelper.SimulateSuccess();
helper.SetMockCookie(new Uri(serverUri), "X-LABKEY-CSRF", "test-csrf-token");

var requestHelper = new HttpPanoramaRequestHelper(new PanoramaServer(...));
requestHelper.DoPost(uri, postData);

// Verify CSRF token was sent in subsequent requests
Assert.IsTrue(helper.GetCapturedHeaders().Contains("X-LABKEY-CSRF: test-csrf-token"));
```

3. **Network failure scenarios:**
```csharp
using var helper = HttpClientTestHelper.SimulateDnsFailure();
// ... verify proper error message to user
```

**Approach B: Real Server Testing** (After Approach A works)

Add optional real server tests (like AutoQC):

```csharp
[TestMethod]
[TestCategory("Connected")] // Only runs when explicitly requested
public void TestPublishToPanoramaRealServer()
{
    // Skip if no credentials
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PANORAMAWEB_USERNAME")))
    {
        Assert.Inconclusive("PANORAMAWEB_USERNAME not set - skipping real server test");
        return;
    }

    var username = TestUtils.GetPanoramaWebUsername();
    var password = TestUtils.GetPanoramaWebPassword();
    
    var panoramaClient = new WebPanoramaClient(
        new Uri("https://panoramaweb.org/"),
        username,
        password);

    // Upload to test folder, verify on server
    panoramaClient.SendZipFile(...);
    
    // Verify file exists via API query
    var files = panoramaClient.GetFilesInFolder("TestFolder");
    Assert.IsTrue(files.Contains(expectedFileName));
}
```

### Phase 3: Migration Strategy

**3.1 Incremental Refactoring**

Don't rewrite everything at once:

1. **Keep existing tests** - They validate UI error handling
2. **Add new tests** - Using `HttpClientTestHelper` for network code coverage
3. **Gradually replace** - Once new tests prove stable
4. **Measure coverage** - Use dotCover to verify gaps are closed

**3.2 Test Organization**

```
PanoramaClientPublishTest.cs
├─ TestDisplayLabKeyErrors() - KEEP (validates UI)
│  └─ Uses legacy mocks for UI-focused testing
│
├─ TestPublishWithHttpClientHelper() - NEW
│  ├─ TestPublishSuccess_ValidatesUploadedData()
│  ├─ TestPublishFailure_BadJson()
│  ├─ TestPublishFailure_UploadError_WithLabKeyJson()
│  ├─ TestPublishFailure_NetworkTimeout()
│  ├─ TestPublishFailure_DnsFailure()
│  ├─ TestCsrfTokenRetrievalAndUsage()
│  └─ TestCookieManagementAcrossRequests()
│
└─ TestPublishToPanoramaRealServer() - NEW (Optional, [TestCategory("Connected")])
   └─ Uses real credentials from env vars
```

**3.3 Coverage Validation**

After refactoring, run dotCover and verify:

**Target:**
- `HttpPanoramaRequestHelper` overall: >80% (currently 17%)
- `DoPost(Uri, NameValueCollection)`: >80% (currently 0%)
- `DoPost(Uri, string)`: >80% (currently 0%)
- `DoAsyncFileUpload()`: >80% (currently 0%)
- `GetCsrfTokenFromServer()`: 100% (currently 0%)
- `CreateHttpClient()`: >80% (currently 55%)

**Acceptable Gaps:**
- `CancelAsyncUpload()` - Async cancellation edge cases
- `ClearCsrfToken()` - Error recovery path rarely used
- `AddHeader()` / `RemoveHeader()` - Low-level plumbing (tested indirectly)

### Phase 4: Documentation & Best Practices

**4.1 Update TESTING.md**

Add section on "Testing Panorama Client":
- How to use `HttpClientTestHelper` for Panorama scenarios
- Environment variable pattern for real server credentials
- How to capture/validate uploaded data
- CSRF token testing patterns

**4.2 Code Comments**

Add explanatory comments to tests:
```csharp
/// <summary>
/// Tests that CSRF token is correctly retrieved from initial server response
/// and included in subsequent POST requests to LabKey Server.
/// Uses HttpClientTestHelper to mock server responses without network access.
/// </summary>
[TestMethod]
public void TestCsrfTokenRetrievalAndUsage()
{
    // ...
}
```

**4.3 Document in MEMORY.md**

Add to "Common Gotchas" or "Lessons Learned":
- Avoid custom test interfaces when centralized infrastructure exists
- Use `HttpClientTestHelper` for all HTTP testing
- Follow AutoQC pattern for real server tests with env vars
- Reference this TODO as example of technical debt paydown

## Success Criteria

### Must Have
- ✅ `HttpPanoramaRequestHelper` coverage >80% (from 17%)
- ✅ All `DoPost()` methods have test coverage
- ✅ CSRF token flow is tested
- ✅ File upload via `HttpClientWithProgress` is tested
- ✅ All existing test scenarios still pass
- ✅ No regressions in Panorama publishing workflows

### Should Have
- ✅ Tests use `HttpClientTestHelper` (modern pattern)
- ✅ Tests run fast without network access (mocked)
- ✅ Clear documentation for future Panorama test authoring
- ✅ Coverage gaps documented in dotCover JSON reports

### Nice to Have
- ✅ Optional real server tests with env var credentials
- ✅ Integration with TeamCity for connected tests
- ✅ Serve as example for other network feature testing

## Risks & Mitigation

### Risk: Breaking Existing Tests

**Mitigation:**
- Add new tests first, keep old tests
- Incremental replacement strategy
- Run full test suite after each change
- Use feature flags if needed for gradual rollout

### Risk: Real Server Tests Flaky

**Mitigation:**
- Use `[TestCategory("Connected")]` to make them opt-in
- Implement retry logic for transient failures
- Clear test data between runs
- Document expected server setup (test folder permissions, etc.)

### Risk: CSRF Token Complexity

**Mitigation:**
- Study AutoQC implementation (it works!)
- Use `HttpClientTestHelper` to mock token exchange
- Add detailed comments explaining flow
- Collaborate with senior developer who built this originally

## Collaboration with Senior Developer

**Questions to Ask:**

1. **Test Folder Setup:**
   - What Panorama test folder should we use for real server tests?
   - What permissions are required?
   - How to clean up test data?

2. **CSRF Token Flow:**
   - Walk through exact sequence of requests
   - When is token retrieved vs. sent?
   - How to handle token expiration?

3. **LabKey Error Handling:**
   - What JSON formats does LabKey return for different error types?
   - How to distinguish between HTTP errors vs. LabKey errors?
   - Any edge cases to test?

4. **Upload Process:**
   - Full sequence: upload → HEAD confirm → MOVE/rename → pipeline job
   - What can go wrong at each step?
   - How to simulate each failure mode?

5. **Best Practices:**
   - How does AutoQC handle this so well?
   - Any gotchas or lessons learned?
   - Preferred testing approach?

## References

**Related Work:**
- `TODO-20251023_panorama_webclient_replacement.md` - Parent migration effort
- `todos/completed/TODO-20251010_webclient_replacement.md` - Phase 1 patterns

**Key Files:**
- `pwiz_tools/Skyline/TestFunctional/PanoramaClientPublishTest.cs` - File to refactor
- `pwiz_tools/Skyline/Executables/AutoQC/AutoQCTest/PanoramaTest.cs` - Success pattern
- `pwiz_tools/Skyline/Executables/AutoQC/AutoQCTest/TestUtils.cs` - Env var helpers
- `pwiz_tools/Skyline/TestUtil/HttpClientTestHelper.cs` - Mocking infrastructure
- `pwiz_tools/Shared/PanoramaClient/RequestHelper.cs` - HttpPanoramaRequestHelper

**Documentation:**
- `TESTING.md` section 9 - Code Coverage Validation
- `TESTING.md` section 6 - HttpClient Testing with HttpClientTestHelper
- `MEMORY.md` - Project patterns and gotchas

**Coverage Reports:**
- `pwiz_tools/Skyline/TestResults/SkylineCoverage.json` - Current baseline (17%)
- Target: Re-run after refactor, verify >80%

## Timeline Estimate

**Week 1: Analysis & Planning**
- Day 1-2: Study current tests, AutoQC pattern, coverage reports
- Day 3: Collaborate with senior developer on approach
- Day 4-5: Set up environment, write first mock-based test

**Week 2: Implementation**
- Day 1-3: Refactor main test scenarios with HttpClientTestHelper
- Day 4: Add CSRF token and cookie management tests
- Day 5: Add file upload validation tests

**Week 3: Real Server Tests & Polish**
- Day 1-2: Implement optional real server tests with env vars
- Day 3: Run coverage analysis, close remaining gaps
- Day 4: Documentation (TESTING.md, code comments)
- Day 5: Code review, final validation

**Total:** ~15 days of focused work (3 weeks)

**Dependencies:**
- Senior developer availability for collaboration (3-5 hours)
- Access to Panorama test server credentials
- No blocking dependencies on other work

## Notes

**This is a "Pay Down Technical Debt" effort:**
- Fixes root cause: lack of centralized testing infrastructure (now solved!)
- Prevents future fragmentation: `HttpClientTestHelper` is established pattern
- Serves as example: Other network features can follow this pattern
- Improves quality: Real code coverage instead of mock coverage

**Long-term Benefit:**
- Future Panorama features get proper test coverage by default
- Developers use centralized `HttpClientWithProgress` instead of rolling their own
- `HttpClientTestHelper` becomes the standard for all HTTP testing
- Less duplication, better quality, easier maintenance

**Historical Context Appreciated:**
The original developers did the best they could with the tools available. Now we have better infrastructure, so we can improve the tests while honoring their original intent (validate error handling, ensure good UX).

This is not about "fixing bad code" - it's about leveraging new infrastructure to close gaps that couldn't be closed before. 🎯

