using System.Net.Http;
using System.Text.Json;

namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// UNIFI-specific HTTP client. Talks to <c>/unifi/v1/sampleresults(GUID)</c>-style endpoints
/// and follows the v3 / v4 API split (port 50034 vs everything else). C# port of cpp
/// <c>UnifiData::Impl</c> in <c>UnifiData.cpp</c>.
/// </summary>
public sealed class UnifiHttpClient : AbstractWatersHttpClient
{
    /// <summary>Constructs and immediately authenticates / fetches metadata.</summary>
    public UnifiHttpClient(UnifiConnectionConfig config, HttpClient? httpClient = null)
        : base(EnsureUnifi(config), httpClient)
    {
    }

    private static UnifiConnectionConfig EnsureUnifi(UnifiConnectionConfig c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Api != RemoteApiType.Unifi)
            throw new ArgumentException($"UnifiHttpClient requires a UNIFI config, got {c.Api}", nameof(c));
        return c;
    }

    /// <inheritdoc/>
    /// <remarks>cpp <c>sampleResultMetadataEndpoint</c> (UnifiData.cpp:326): the sample-result
    /// URL itself is the metadata endpoint — UNIFI returns a JSON document describing the run.</remarks>
    protected override string SampleMetadataEndpoint => Config.SampleResultUrl.AbsoluteUri;

    /// <inheritdoc/>
    protected override void ParseSampleMetadata(JsonDocument json)
    {
        ApplyStandardSampleFields(json.RootElement);
    }
}
