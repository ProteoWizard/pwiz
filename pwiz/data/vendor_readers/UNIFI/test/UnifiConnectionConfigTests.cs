namespace Pwiz.Vendor.UNIFI.Tests;

[TestClass]
public class UnifiConnectionConfigTests
{
    [TestMethod]
    public void UnifiParserTest()
    {
        // V4 (default port path): identity defaults to host:48333, scope "webapi",
        // /connect/token, clientId "resourceownerclient".
        var v4 = UnifiConnectionConfig.Parse(
            "https://democonnect.waters.com:48505/unifi/v1/sampleresults(64d5ef18-6220-4ffe-99de-7aeae45ea53a)?chunkReadahead=2&name=Hi3_ClpB");
        Assert.AreEqual(RemoteApiType.Unifi, v4.Api);
        Assert.AreEqual(4, v4.ApiVersion);
        Assert.AreEqual("webapi", v4.ClientScope);
        Assert.AreEqual("resourceownerclient", v4.ClientId);
        Assert.AreEqual("secret", v4.ClientSecret);
        Assert.AreEqual(
            "https://democonnect.waters.com:48505/unifi/v1/sampleresults(64d5ef18-6220-4ffe-99de-7aeae45ea53a)",
            v4.SampleResultUrl.AbsoluteUri);
        Assert.AreEqual("https://democonnect.waters.com:48333/", v4.IdentityServerUrl.AbsoluteUri);
        Assert.AreEqual("https://democonnect.waters.com:48333/connect/token", v4.TokenEndpoint.AbsoluteUri);
        Assert.AreEqual(2, v4.ChunkReadaheadOverride);
        Assert.AreEqual("Hi3_ClpB", v4.FriendlyName);
        Assert.IsNull(v4.Credentials);

        // V3 (legacy port 50034): identity port 50333, scope "unifi",
        // /identity/connect/token endpoint.
        var v3 = UnifiConnectionConfig.Parse("https://demo.example:50034/unifi/v1/sampleresults(abc-123)");
        Assert.AreEqual(3, v3.ApiVersion);
        Assert.AreEqual("unifi", v3.ClientScope);
        Assert.AreEqual("https://demo.example:50333/", v3.IdentityServerUrl.AbsoluteUri);
        Assert.AreEqual("https://demo.example:50333/identity/connect/token", v3.TokenEndpoint.AbsoluteUri);

        // ?identity=, ?scope=, ?clientId=, ?secret=, ?chunkSize=, ?chunkReadahead= overrides.
        var overridden = UnifiConnectionConfig.Parse(
            "https://host:48505/unifi/v1/sampleresults(g)"
            + "?identity=https://elsewhere:9999"
            + "&scope=customScope&clientId=customClient&secret=customSecret"
            + "&chunkSize=42&chunkReadahead=7");
        Assert.AreEqual("https://elsewhere:9999/", overridden.IdentityServerUrl.AbsoluteUri);
        Assert.AreEqual("customScope", overridden.ClientScope);
        Assert.AreEqual("customClient", overridden.ClientId);
        Assert.AreEqual("customSecret", overridden.ClientSecret);
        Assert.AreEqual(42, overridden.ChunkSizeOverride);
        Assert.AreEqual(7, overridden.ChunkReadaheadOverride);

        // username:password@ extracted with URL-decoding.
        var withCreds = UnifiConnectionConfig.Parse(
            "https://alice:secret%40123@host:48505/unifi/v1/sampleresults(g)");
        Assert.IsNotNull(withCreds.Credentials);
        Assert.AreEqual("alice", withCreds.Credentials!.Value.Username);
        Assert.AreEqual("secret@123", withCreds.Credentials.Value.Password);

        // Bare host:port with no scheme — cpp prepends https:// (UnifiData.cpp:202-205).
        var noScheme = UnifiConnectionConfig.Parse("host:48505/unifi/v1/sampleresults(g)");
        Assert.AreEqual("https", noScheme.SampleResultUrl.Scheme);

        // Non-UNIFI URL rejected.
        Assert.ThrowsException<ArgumentException>(()
            => UnifiConnectionConfig.Parse("https://host/some/random/path"));
    }

    [TestMethod]
    public void WatersConnectParserTest()
    {
        // Both query keys present: API v2 always, identity port 48333, scope "webapi",
        // clientId "resourceownerclient_jwt".
        var c = UnifiConnectionConfig.Parse(
            "https://devconnect.waters.com:48444/?sampleSetId=c21577c6-93c9-4348-ad06-dd38aa5ad8c5"
            + "&injectionId=d1a3f900-2e89-4730-a2fa-aa88745b239c&chunkReadahead=2");
        Assert.AreEqual(RemoteApiType.WatersConnect, c.Api);
        Assert.AreEqual(2, c.ApiVersion);
        Assert.AreEqual("webapi", c.ClientScope);
        Assert.AreEqual("resourceownerclient_jwt", c.ClientId);
        Assert.AreEqual("c21577c6-93c9-4348-ad06-dd38aa5ad8c5", c.SampleSetId);
        Assert.AreEqual("d1a3f900-2e89-4730-a2fa-aa88745b239c", c.InjectionId);
        Assert.AreEqual("https://devconnect.waters.com:48333/", c.IdentityServerUrl.AbsoluteUri);
        Assert.AreEqual("https://devconnect.waters.com:48333/connect/token", c.TokenEndpoint.AbsoluteUri);

        // Both query keys are required — missing one throws with a useful message.
        var missingInjection = Assert.ThrowsException<ArgumentException>(()
            => UnifiConnectionConfig.Parse("https://host/?sampleSetId=only"));
        StringAssert.Contains(missingInjection.Message, "injectionId");

        var missingSampleSet = Assert.ThrowsException<ArgumentException>(()
            => UnifiConnectionConfig.Parse("https://host/?injectionId=only"));
        StringAssert.Contains(missingSampleSet.Message, "sampleSetId");
    }
}
