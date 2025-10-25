# TODO-normalize_skyp_panorama_client.md

## Branch Information (Future)
- **Branch**: Not yet created - will be `Skyline/work/YYYYMMDD_normalize_skyp_panorama_client`
- **Objective**: Normalize error messaging between SkypSupport and PanoramaClient, and refactor SkypSupport to use PanoramaClient for all Panorama/LabKey server communication.

## Background

SkypSupport and PanoramaClient both communicate with Panorama/LabKey servers, but provide **inconsistent error messages** to users. This creates a confusing user experience where the quality and helpfulness of error messages depends on which code path is used, rather than the actual error encountered.

### Current Inconsistency

**SkypSupport (downloading .skyp files):**
- ✅ Provides Panorama-specific context and guidance
- ✅ Explains credential issues with actionable next steps
- ✅ Distinguishes between authentication failures and permission issues
- ✅ Offers to update credentials or explains username mismatches

Example messages:
- "Credentials saved in Skyline for the Panorama server {X} are invalid. Would you like to update the credentials?"
- "You do not have permissions to download this file from {server}."
- "Credentials saved in Skyline for the Panorama server {X} are for the user {Y}. This user does not have permissions to download the file. The skyp file was downloaded by {Z}."

**PanoramaClient (publishing, downloading files):**
- ❌ Shows only generic HTTP error messages from `HttpClientWithProgress`
- ❌ No Panorama-specific context or guidance
- ❌ Doesn't help users understand how to resolve the issue

Example messages:
- "Access to https://panoramaweb.org/_webdav/... was denied (HTTP 401). Authentication may be required."
- "Access to https://panoramaweb.org/_webdav/... was forbidden (HTTP 403). You may not have permission to access this resource."

### User Experience Problem

Users interacting with the same Panorama server get different quality error messages:
1. **Via .skyp file download** → Helpful, actionable guidance
2. **Via Panorama publishing/download** → Generic HTTP errors with no actionable guidance

This is confusing and makes it harder for users to resolve authentication and permission issues when publishing to or downloading from Panorama.

## Proposed Solution (DRY Approach)

### Phase 1: Create Static Error Message Builder

Add a **static helper method** to `PanoramaClient` (or `AbstractPanoramaClient`) that builds Panorama-specific error messages from `NetworkRequestException`:

```csharp
public static class PanoramaClientErrorHelper
{
    /// <summary>
    /// Builds a user-friendly error message for Panorama/LabKey server errors.
    /// Adds Panorama-specific context and guidance based on HTTP status code.
    /// </summary>
    /// <param name="exception">The NetworkRequestException from HttpClientWithProgress</param>
    /// <param name="operationDescription">What the user was trying to do (e.g., "download file {name}")</param>
    /// <param name="serverUri">The Panorama server URI</param>
    /// <param name="username">The username being used (if available)</param>
    /// <returns>Complete error message with Panorama-specific guidance</returns>
    public static string BuildPanoramaErrorMessage(
        NetworkRequestException exception,
        string operationDescription,
        Uri serverUri,
        string username = null)
    {
        var message = new List<string>
        {
            operationDescription,
            exception.Message // The HTTP error from HttpClientWithProgress
        };

        var serverName = serverUri.Host;

        switch (exception.StatusCode)
        {
            case HttpStatusCode.Unauthorized: // 401
                if (!string.IsNullOrEmpty(username))
                {
                    message.Add(string.Format(
                        Resources.PanoramaClient_Credentials_saved_for_server__0__user__1__are_invalid,
                        serverName, username));
                }
                else
                {
                    message.Add(string.Format(
                        Resources.PanoramaClient_Authentication_required_for_server__0__,
                        serverName));
                }
                message.Add(Resources.PanoramaClient_Would_you_like_to_update_credentials);
                break;

            case HttpStatusCode.Forbidden: // 403
                if (!string.IsNullOrEmpty(username))
                {
                    message.Add(string.Format(
                        Resources.PanoramaClient_User__0__does_not_have_permissions_on_server__1__,
                        username, serverName));
                }
                else
                {
                    message.Add(string.Format(
                        Resources.PanoramaClient_Insufficient_permissions_on_server__0__,
                        serverName));
                }
                message.Add(Resources.PanoramaClient_Contact_project_administrator);
                break;

            case HttpStatusCode.InternalServerError: // 500
            case HttpStatusCode.ServiceUnavailable: // 503
                message.Add(string.Format(
                    Resources.PanoramaClient_Server_error_on__0__contact_administrator,
                    serverName));
                break;
        }

        return TextUtil.LineSeparate(message.ToArray());
    }
}
```

### Phase 2: Use Helper in PanoramaClient Callers

Update `SkylineWindow.DownloadPanoramaFile()`:

```csharp
catch (Exception e)
{
    if (ExceptionUtil.IsProgrammingDefect(e))
        throw;

    if (e is NetworkRequestException netEx)
    {
        string message;
        if (netEx.StatusCode == HttpStatusCode.NotFound)
        {
            // Special case for 404 - file deleted
            message = Resources.SkylineWindow_DownloadPanoramaFile_File_does_not_exist__It_may_have_been_deleted_on_the_server_;
        }
        else
        {
            // Use Panorama-specific error message builder
            var operationDescription = string.Format(
                Resources.SkylineWindow_DownloadPanoramaFile_Error_downloading__0__from_Panorama,
                fileName);
            message = PanoramaClientErrorHelper.BuildPanoramaErrorMessage(
                netEx, operationDescription, curServer.URI, curServer.Username);
        }
        MessageDlg.ShowWithException(this, message, e);
    }
    else
    {
        MessageDlg.ShowException(this, e);
    }
    return false;
}
```

### Phase 3: Refactor SkypSupport to Use PanoramaClient

Currently, SkypSupport uses `HttpClientWithProgress` directly. Long-term, it should use `PanoramaClient`:

```csharp
// Current: SkypSupport uses HttpClientWithProgress directly
using var httpClient = new HttpClientWithProgress(progressMonitor, progressStatus);
httpClient.AddAuthorizationHeader(pServer.AuthHeader);
httpClient.DownloadFile(skyp.SkylineDocUri, skyp.DownloadPath);

// Future: SkypSupport uses PanoramaClient
var panoramaClient = new WebPanoramaClient(pServer.URI, pServer.Username, pServer.Password);
panoramaClient.DownloadFile(skyp.SkylineDocUri.ToString(), skyp.DownloadPath, 
    fileSize: 0, realName: skyp.GetSkylineDocName(), pm: progressMonitor, progressStatus);
```

This ensures:
- ✅ All Panorama communication goes through one interface
- ✅ Consistent error handling everywhere
- ✅ DRY - no duplication of Panorama-specific logic

## Prerequisites

- ✅ `TODO-20251023_panorama_webclient_replacement.md` merged to master (PanoramaClient fully migrated to HttpClientWithProgress)

## Task Checklist

### Phase 1: Static Error Message Builder
- [ ] Create `PanoramaClientErrorHelper` class with `BuildPanoramaErrorMessage()`
- [ ] Add required resource strings to `Resources.resx`:
  - [ ] `PanoramaClient_Credentials_saved_for_server__0__user__1__are_invalid`
  - [ ] `PanoramaClient_Authentication_required_for_server__0__`
  - [ ] `PanoramaClient_Would_you_like_to_update_credentials`
  - [ ] `PanoramaClient_User__0__does_not_have_permissions_on_server__1__`
  - [ ] `PanoramaClient_Insufficient_permissions_on_server__0__`
  - [ ] `PanoramaClient_Contact_project_administrator`
  - [ ] `PanoramaClient_Server_error_on__0__contact_administrator`
  - [ ] `SkylineWindow_DownloadPanoramaFile_Error_downloading__0__from_Panorama`
- [ ] Add unit tests for error message builder (various status codes, with/without username)

### Phase 2: Use Helper in Existing Code
- [ ] Update `SkylineWindow.DownloadPanoramaFile()` to use `BuildPanoramaErrorMessage()`
- [ ] Update `SkylineWindow.ShowPublishDlg()` error handling (if needed)
- [ ] Update any other PanoramaClient error handling locations
- [ ] Update tests to verify new error messages (use resource strings)
- [ ] Test in all locales

### Phase 3: Refactor SkypSupport
- [ ] Analyze `SkypSupport.Download()` current implementation
- [ ] Determine if `PanoramaClient.DownloadFile()` API needs extensions for .skyp use case
- [ ] Refactor `SkypSupport` to use `WebPanoramaClient` instead of `HttpClientWithProgress`
- [ ] Remove `SkypSupport.GetMessage()` and related helpers (now redundant)
- [ ] Update `SkypTest` to verify new error messages match
- [ ] Test .skyp downloads with various error conditions
- [ ] Test in all locales

### Phase 4: Documentation & Cleanup
- [ ] Update `MEMORY.md` if this establishes a new pattern for error message helpers
- [ ] Remove any now-redundant error message building code
- [ ] Document `PanoramaClientErrorHelper` usage for future developers

## Benefits

1. **Consistent User Experience:** Users get the same quality of error messages regardless of which Skyline feature they're using to interact with Panorama
2. **DRY:** Single source of truth for Panorama error message formatting
3. **Maintainable:** When Panorama error handling needs improvement, change it in one place
4. **Testable:** Static helper method is easy to unit test
5. **Localized:** All error messages use resource strings, work in all supported locales
6. **Actionable:** Users know what to do when they encounter authentication/permission errors

## Risks & Considerations

- **Message Regression:** Existing error messages will change - need thorough testing
- **Translation Work:** New resource strings need translation to all supported locales
- **SkypSupport Refactoring Complexity:** May discover edge cases in how .skyp downloads differ from regular Panorama downloads
- **API Compatibility:** Changing error messages might affect tests or scripts that parse them (unlikely but possible)

## Success Criteria

- ✅ All Panorama-related errors show consistent, helpful messages
- ✅ Users receive actionable guidance for 401/403 errors
- ✅ SkypSupport uses PanoramaClient for all downloads
- ✅ No duplication of error message building logic
- ✅ All tests pass in all locales
- ✅ Error messages are translation-proof (use resource strings)

## Related Work

- `TODO-20251023_panorama_webclient_replacement.md` (completed) - Migrated PanoramaClient to HttpClientWithProgress
- `MEMORY.md` - DRY principle documentation
- `TESTING.md` - Translation-proof testing with resource strings

