using System.Net.Http;
using System.Text.Json;

namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// waters_connect-specific HTTP client. Talks to v2 endpoints under
/// <c>/waters_connect/v2.0/sample-sets/{sampleSetId}/injection-data</c>. C# port of cpp
/// <c>WatersConnectImpl</c> in <c>WatersConnectData.ipp</c>.
/// </summary>
/// <remarks>
/// waters_connect organizes injections within sample sets. The metadata endpoint returns the
/// entire injection list; we walk it to find the entry whose id matches our
/// <see cref="UnifiConnectionConfig.InjectionId"/>, then apply the standard sample fields from
/// that injection's data.
/// </remarks>
public sealed class WatersConnectHttpClient : AbstractWatersHttpClient
{
    /// <summary>Constructs and immediately authenticates / fetches metadata.</summary>
    public WatersConnectHttpClient(UnifiConnectionConfig config, HttpClient? httpClient = null)
        : base(EnsureWatersConnect(config), httpClient)
    {
    }

    private static UnifiConnectionConfig EnsureWatersConnect(UnifiConnectionConfig c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Api != RemoteApiType.WatersConnect)
            throw new ArgumentException($"WatersConnectHttpClient requires a waters_connect config, got {c.Api}", nameof(c));
        if (string.IsNullOrEmpty(c.SampleSetId))
            throw new ArgumentException("waters_connect config missing SampleSetId", nameof(c));
        if (string.IsNullOrEmpty(c.InjectionId))
            throw new ArgumentException("waters_connect config missing InjectionId", nameof(c));
        return c;
    }

    /// <summary>Base URL for all per-injection v2 endpoints. Mirrors cpp's <c>_baseUrl</c>
    /// in <c>WatersConnectData.ipp</c>.</summary>
    public string BaseUrl => Config.SampleResultUrl.GetLeftPart(UriPartial.Authority);

    /// <inheritdoc/>
    /// <remarks>cpp <c>injectionsMetadataEndpoint</c> (WatersConnectData.ipp:260) returns the
    /// list of injections for the sample set; we filter by injectionId in
    /// <see cref="ParseSampleMetadata"/>.</remarks>
    protected override string SampleMetadataEndpoint
        => $"{BaseUrl}/waters_connect/v2.0/sample-sets/{Config.SampleSetId}/injection-data";

    /// <inheritdoc/>
    protected override void ParseSampleMetadata(JsonDocument json)
    {
        // cpp WatersConnectData.ipp:608-619 walks the injection list and picks the entry whose
        // id matches _injectionId; throws when not found. The matched entry exposes the same
        // fields the UNIFI sample-result root does (name, description, sample.{...}), so we can
        // delegate to ApplyStandardSampleFields once we've isolated it. Some servers wrap the
        // list in a {"value":[...]} envelope; others return the bare array. Tolerate both.
        var root = json.RootElement;
        JsonElement array;
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
        }
        else if (root.ValueKind == JsonValueKind.Object
                 && root.TryGetProperty("value", out var inner) && inner.ValueKind == JsonValueKind.Array)
        {
            array = inner;
        }
        else
        {
            throw new InvalidOperationException(
                $"waters_connect metadata response was neither an array nor a {{value:[]}} envelope; URL was {SampleMetadataEndpoint}");
        }

        foreach (var injection in array.EnumerateArray())
        {
            if (injection.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                && string.Equals(id.GetString(), Config.InjectionId, StringComparison.Ordinal))
            {
                ApplyStandardSampleFields(injection);
                return;
            }
        }

        throw new InvalidOperationException(
            $"injection with id {Config.InjectionId} not found in sample set {Config.SampleSetId}");
    }
}
