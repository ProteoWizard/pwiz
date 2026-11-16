/*
 * Test-only HTTP behavior for SkylineBatch's remote-file-source functional test.
 *
 * SkylineBatch's remote-file-source UI validates a Panorama server whenever the source combo
 * selection changes (RemoteFileControl.CheckIfPanoramaSource -> WebPanoramaClient.ValidateServer),
 * making two synchronous HTTP GETs ON THE UI THREAD: admin-healthCheck.view (is this a LabKey
 * server?) then security/home/ensureLogin.view (are these credentials valid?). Against a live server
 * that never responds (offline CI, firewall) these block forever, hanging the functional test. This
 * mock intercepts both via the static HttpClientWithProgress.TestBehavior seam and answers them the
 * way the real server would:
 *   - health check   -> {"healthy": true}  (panoramaweb is a LabKey server; no login involved)
 *   - ensureLogin    -> succeeds ONLY for the exact expected account (username AND password),
 *                       returning the currentUser.email PanoramaUtil.IsValidEnsureLoginResponse
 *                       checks; any other credentials get a response that fails validation (so
 *                       ValidateServer throws PanoramaServerException and the Panorama button hides).
 * Keeping the credential check real keeps the button-visibility assertions meaningful instead of
 * rubber-stamping every login.
 *
 * Only these two VALIDATION calls are intercepted - they run synchronously on the UI thread with no
 * connect timeout, so they are what hang the test against an unresponsive server. Other requests (the
 * WebDAV ".../@files?method=json" folder-content listings the source/file pickers make) fall through
 * to the live server, which this test has always depended on for real folder data; mocking those too
 * would be a larger, separate change.
 */
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using pwiz.Common.SystemUtil;

namespace SkylineBatchTest
{
    public class PanoramaValidationTestBehavior : HttpClientWithProgress.IHttpClientTestBehavior
    {
        private readonly string _validUsername;
        private readonly string _validPassword;

        public PanoramaValidationTestBehavior(string validUsername, string validPassword)
        {
            _validUsername = validUsername;
            _validPassword = validPassword;
        }

        // Never simulate a blanket failure - ensureLogin is answered per-credentials below.
        public Exception FailureException => null;

        public Stream GetMockResponseStreamFromRequest(HttpRequestMessage request)
        {
            var uri = request?.RequestUri;
            if (uri == null)
                return null;
            var path = uri.AbsolutePath;

            // Server health check: confirm it's a LabKey server. No login involved.
            if (path.EndsWith(@"admin-healthCheck.view", StringComparison.OrdinalIgnoreCase))
                return Json(@"{""healthy"":true}");

            // Login validation.
            if (path.EndsWith(@"ensureLogin.view", StringComparison.OrdinalIgnoreCase))
            {
                GetBasicAuthCredentials(request, out var username, out var password);
                if (username == null)
                    // Anonymous request (no credentials): the real server grants anonymous access to
                    // public panoramaweb folders, which the tests rely on. Return an empty
                    // currentUser.email - matching the empty server username IsValidEnsureLoginResponse
                    // compares against - so the source validates.
                    return Json(@"{""currentUser"":{""email"":""""}}");

                // With credentials, succeed only for the exact valid account (username AND password) -
                // so a wrong password is actually rejected; anything else returns no currentUser, so
                // validation fails and the caller hides the Panorama button.
                var authenticated = string.Equals(username, _validUsername, StringComparison.Ordinal) &&
                                    string.Equals(password, _validPassword, StringComparison.Ordinal);
                return Json(authenticated
                    ? @"{""currentUser"":{""email"":""" + username + @"""}}"
                    : @"{}");
            }

            // Everything else (e.g. WebDAV ".../@files?method=json" file listings) is real folder-content
            // browsing this test still does against the live server. We only intercept the two server-
            // VALIDATION calls above, because those run synchronously on the UI thread with no connect
            // timeout and are what hang the test offline; returning null here lets the rest use the network
            // as before. (Fully mocking the live folder browsing is a larger, separate change.)
            return null;
        }

        private static Stream Json(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

        public Stream GetMockResponseStream(Uri uri, out long contentLength)
        {
            // Reached only when there is no request (uri-only lookup); the request-based method above
            // handles every real call.
            contentLength = 0;
            return null;
        }

        private static void GetBasicAuthCredentials(HttpRequestMessage request, out string username, out string password)
        {
            username = null;
            password = null;
            var auth = request.Headers.Authorization;
            if (auth == null || !string.Equals(auth.Scheme, @"Basic", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(auth.Parameter))
                return;
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth.Parameter));
                var colon = decoded.IndexOf(':');
                if (colon < 0)
                {
                    username = decoded;
                    return;
                }
                username = decoded.Substring(0, colon);
                password = decoded.Substring(colon + 1);
            }
            catch
            {
                // malformed header -> treat as no credentials
            }
        }

        // Remaining members are no-ops for this mock.
        public Stream GetMockUploadStream(Uri uri) => null;
        public void OnResponse(Uri uri, HttpResponseMessage response) { }
        public Stream WrapResponseStream(Uri uri, Stream responseStream, long contentLength) => responseStream;
        public void OnFailedResponse(Uri uri, HttpResponseMessage response, string responseBody, Exception exception) { }
        public void OnException(Uri uri, Exception exception) { }
    }
}
