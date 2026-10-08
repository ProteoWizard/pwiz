using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Pwiz.Vendor.UNIFI.Tests;

/// <summary>
/// Verifies the OAuth + sample-metadata round-trip without hitting a real Waters server.
/// Uses an in-memory <see cref="HttpMessageHandler"/> that scripts the expected endpoints —
/// the same shapes the cpp client expects (token endpoint POST + sample-result GET).
/// </summary>
[TestClass]
public class HttpClientFlowTests
{
    [TestMethod]
    public void UnifiHttpClient_OAuthThenMetadata()
    {
        var handler = new ScriptedHandler();
        // Token endpoint: cpp posts grant_type=password + scope to /connect/token (or
        // /identity/connect/token for v3) with an Authorization: Basic clientId:secret header.
        handler.Expect("POST", "https://demo.example:48333/connect/token", req =>
        {
            Assert.IsNotNull(req.Headers.Authorization);
            Assert.AreEqual("Basic", req.Headers.Authorization!.Scheme);
            string basic = Encoding.UTF8.GetString(Convert.FromBase64String(req.Headers.Authorization.Parameter!));
            Assert.AreEqual("resourceownerclient:secret", basic);

            string body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            StringAssert.Contains(body, "grant_type=password");
            StringAssert.Contains(body, "username=alice");
            StringAssert.Contains(body, "password=hunter2");
            StringAssert.Contains(body, "scope=webapi");

            return Json("""{ "access_token":"TOK", "expires_in":3600, "token_type":"Bearer" }""");
        });
        // Sample-result endpoint: cpp GETs the sample-result URL with the bearer token; expects
        // the JSON shape documented in UnifiData.cpp:283-326.
        handler.Expect("GET", "https://demo.example:48505/unifi/v1/sampleresults(abc-123)", req =>
        {
            Assert.IsNotNull(req.Headers.Authorization);
            Assert.AreEqual("Bearer", req.Headers.Authorization!.Scheme);
            Assert.AreEqual("TOK", req.Headers.Authorization.Parameter);
            return Json("""
                {
                    "id": "abc-123",
                    "name": "MM 5.0 ug/kg",
                    "description": "Matrix matched standard 5.0 ug/kg",
                    "sample": {
                        "replicateNumber": 2,
                        "wellPosition": "1:C,4",
                        "acquisitionStartTime": "2017-03-08T15:42:13Z"
                    }
                }
                """);
        });

        using var client = new UnifiHttpClient(
            UnifiConnectionConfig.Parse("https://alice:hunter2@demo.example:48505/unifi/v1/sampleresults(abc-123)"),
            new HttpClient(handler));

        Assert.AreEqual("TOK", client.AccessToken);
        Assert.AreEqual("MM 5.0 ug/kg", client.SampleName);
        Assert.AreEqual("Matrix matched standard 5.0 ug/kg", client.SampleDescription);
        Assert.AreEqual(2, client.ReplicateNumber);
        Assert.AreEqual("1:C,4", client.WellPosition);
        Assert.AreEqual(new DateTime(2017, 3, 8, 15, 42, 13, DateTimeKind.Utc), client.AcquisitionStartTimeUtc);
        handler.AssertExhausted();
    }

    [TestMethod]
    public void WatersConnectHttpClient_ResolvesInjectionFromList()
    {
        var handler = new ScriptedHandler();
        handler.Expect("POST", "https://wc.example:48333/connect/token", req =>
        {
            Assert.IsNotNull(req.Headers.Authorization);
            string basic = Encoding.UTF8.GetString(Convert.FromBase64String(req.Headers.Authorization!.Parameter!));
            Assert.AreEqual("resourceownerclient_jwt:secret", basic);
            return Json("""{ "access_token":"WC-TOK" }""");
        });
        // waters_connect metadata endpoint returns an array of injections; the client picks
        // the one matching injectionId. cpp WatersConnectData.ipp:608-619.
        handler.Expect("GET", "https://wc.example:48444/waters_connect/v2.0/sample-sets/SET-1/injection-data", req =>
        {
            return Json("""
                {
                    "value": [
                        {
                            "id": "OTHER-INJECTION",
                            "name": "Skipped",
                            "sample": { "replicateNumber": 99, "wellPosition": "X,X", "acquisitionStartTime": "2000-01-01T00:00:00Z" }
                        },
                        {
                            "id": "INJ-2",
                            "name": "blank_2",
                            "description": "blank injection",
                            "sample": { "replicateNumber": 1, "wellPosition": "2:A,1", "acquisitionStartTime": "2018-10-27T09:00:00Z" }
                        }
                    ]
                }
                """);
        });

        using var client = new WatersConnectHttpClient(
            UnifiConnectionConfig.Parse("https://bob:pw@wc.example:48444/?sampleSetId=SET-1&injectionId=INJ-2"),
            new HttpClient(handler));

        Assert.AreEqual("WC-TOK", client.AccessToken);
        Assert.AreEqual("blank_2", client.SampleName);
        Assert.AreEqual(1, client.ReplicateNumber);
        Assert.AreEqual("2:A,1", client.WellPosition);
        Assert.AreEqual(new DateTime(2018, 10, 27, 9, 0, 0, DateTimeKind.Utc), client.AcquisitionStartTimeUtc);
        handler.AssertExhausted();
    }

    [TestMethod]
    public void UnifiHttpClient_BadCredentialsBubbleUp()
    {
        var handler = new ScriptedHandler();
        handler.Expect("POST", "https://demo.example:48333/connect/token", _ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"invalid_grant"}""", Encoding.UTF8, "application/json")
            });

        var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            new UnifiHttpClient(
                UnifiConnectionConfig.Parse("https://alice:wrong@demo.example:48505/unifi/v1/sampleresults(abc)"),
                new HttpClient(handler)));
        StringAssert.Contains(ex.Message, "authentication error");
        StringAssert.Contains(ex.Message, "invalid_grant");
    }

    [TestMethod]
    public void WatersConnectHttpClient_MissingInjectionThrows()
    {
        var handler = new ScriptedHandler();
        handler.Expect("POST", "https://wc.example:48333/connect/token", _ => Json("""{"access_token":"T"}"""));
        handler.Expect("GET", "https://wc.example:48444/waters_connect/v2.0/sample-sets/SET-1/injection-data", _ =>
            Json("""{"value":[{"id":"OTHER"}]}"""));

        var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            new WatersConnectHttpClient(
                UnifiConnectionConfig.Parse("https://u:p@wc.example:48444/?sampleSetId=SET-1&injectionId=MISSING"),
                new HttpClient(handler)));
        StringAssert.Contains(ex.Message, "injection with id MISSING not found");
    }

    [TestMethod]
    public void Credentials_MissingFromUrl_Throws()
    {
        // Production code never reads *_PASSWORD env vars. A URL without userinfo must fail
        // up-front with a message that points the caller at the right URL form.
        var handler = new ScriptedHandler();
        // No expectations queued — the OAuth POST should never fire because credential
        // resolution throws synchronously inside the client ctor.

        // Even if the env vars happen to be set on this machine, the production resolver
        // ignores them; clear locally just to make the test invariant.
        var savedUser = Environment.GetEnvironmentVariable("UNIFI_USERNAME");
        var savedPass = Environment.GetEnvironmentVariable("UNIFI_PASSWORD");
        Environment.SetEnvironmentVariable("UNIFI_USERNAME", null);
        Environment.SetEnvironmentVariable("UNIFI_PASSWORD", null);
        try
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                new UnifiHttpClient(
                    UnifiConnectionConfig.Parse("https://demo.example:48505/unifi/v1/sampleresults(g)"),
                    new HttpClient(handler)));
            StringAssert.Contains(ex.Message, "credentials missing");
            StringAssert.Contains(ex.Message, "username:password@");
        }
        finally
        {
            Environment.SetEnvironmentVariable("UNIFI_USERNAME", savedUser);
            Environment.SetEnvironmentVariable("UNIFI_PASSWORD", savedPass);
        }
    }

    private static HttpResponseMessage Json(string body)
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        return resp;
    }

    /// <summary>Records an ordered list of expected request matchers and returns scripted
    /// responses. Asserts each request matches the next expectation in line.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<(string Method, string Url, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _expectations = new();

        public void Expect(string method, string url, Func<HttpRequestMessage, HttpResponseMessage> respond)
            => _expectations.Enqueue((method, url, respond));

        public void AssertExhausted()
            => Assert.AreEqual(0, _expectations.Count, "Not all scripted HTTP expectations were consumed");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_expectations.Count == 0)
                throw new InvalidOperationException(
                    $"Unexpected request: {request.Method} {request.RequestUri} — no scripted expectations remain");
            var next = _expectations.Dequeue();
            Assert.AreEqual(next.Method, request.Method.ToString(), "Wrong HTTP method on next request");
            Assert.AreEqual(next.Url, request.RequestUri!.AbsoluteUri, "Wrong URL on next request");
            return next.Respond(request);
        }
    }
}
