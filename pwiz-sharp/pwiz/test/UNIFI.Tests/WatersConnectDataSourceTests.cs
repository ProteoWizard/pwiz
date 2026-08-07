using System.Net;
using System.Net.Http;
using System.Text;
using Google.Protobuf;
using Pwiz.Vendor.UNIFI.Protobuf;

namespace Pwiz.Vendor.UNIFI.Tests;

[TestClass]
public class WatersConnectDataSourceTests
{
    private const string Host = "https://wc.example:48444";
    private const string SetId = "SET-1";
    private const string InjectionId = "INJ-2";
    private static readonly string ConnectUrl =
        $"{Host}/?sampleSetId={SetId}&injectionId={InjectionId}";

    [TestMethod]
    public void Discovery_BuildsChannelIndexAndSpectrumCount()
    {
        // Two MS scanning channels (Low + High), one non-MS channel that should be skipped.
        // cpp WatersConnectImpl FetchChannels (WatersConnectData.ipp:765-866).
        var handler = new ScriptedHandler();
        handler.AddOAuth();
        handler.AddInjectionMetadata();
        handler.AddChannelsArray("""
            [
              {
                "id": "ch-low", "name": "1: TOF MSe (50-1200) 4eV ESI+",
                "channelDataShape": "Spectra", "channelDataType": "MSScanning",
                "scanCount": 50,
                "technique": {
                  "basicMsProperties": {
                    "scanningMethod": "MS",
                    "ionisationType": "ESI",
                    "ionisationMode": "Positive",
                    "acquiredMOverZRange": { "start": 50, "end": 1200 }
                  },
                  "fragmentationProperties": { "energyLevelType": "Low" }
                }
              },
              {
                "id": "ch-high", "name": "2: TOF MSe (50-1200) 30V ESI+",
                "channelDataShape": "Spectra", "channelDataType": "MSScanning",
                "scanCount": 50,
                "technique": {
                  "basicMsProperties": {
                    "scanningMethod": "PRODUCT",
                    "ionisationType": "ESI",
                    "ionisationMode": "Positive",
                    "acquiredMOverZRange": { "start": 50, "end": 1200 }
                  },
                  "fragmentationProperties": { "energyLevelType": "High" }
                }
              },
              {
                "id": "ch-uv", "name": "PDA 254nm",
                "channelDataShape": "Trace", "channelDataType": "Optical",
                "scanCount": 100
              }
            ]
            """);

        using var src = BuildSource(handler);

        Assert.AreEqual(RemoteApiType.WatersConnect, src.RemoteApi);
        Assert.AreEqual(2, src.Channels.Count); // PDA channel filtered out (non-Spectra)
        Assert.AreEqual(0, src.Channels[0].Index); // "1: ..." → index 0
        Assert.AreEqual(1, src.Channels[1].Index); // "2: ..." → index 1
        Assert.AreEqual(UnifiEnergyLevel.Low, src.Channels[0].EnergyLevel);
        Assert.AreEqual(UnifiEnergyLevel.High, src.Channels[1].EnergyLevel);
        Assert.AreEqual(ScanningMethod.MS, src.Channels[0].ScanningMethod);
        Assert.AreEqual(ScanningMethod.PRODUCT, src.Channels[1].ScanningMethod);
        Assert.AreEqual(50 + 50, src.NumberOfSpectra);
        Assert.IsFalse(src.HasIonMobilityData);

        // Ms-level routing: Low channel → MS1, High (PRODUCT) → MS2.
        Assert.AreEqual(1, src.GetMsLevel(0));
        Assert.AreEqual(2, src.GetMsLevel(50));

        // Channel/scan-index addressing: spectrum 0 → channel 0 (Low) scan 0;
        // spectrum 49 → channel 0 scan 49; spectrum 50 → channel 1 scan 0.
        Assert.AreEqual((0, 0), src.GetChannelAndScanIndex(0));
        Assert.AreEqual((0, 49), src.GetChannelAndScanIndex(49));
        Assert.AreEqual((1, 0), src.GetChannelAndScanIndex(50));
        handler.AssertExhausted();
    }

    [TestMethod]
    public void GetSpectrum_FetchesContinuumChunkPerChannel()
    {
        var handler = new ScriptedHandler();
        handler.AddOAuth();
        handler.AddInjectionMetadata();
        handler.AddChannelsArray("""
            [
              {
                "id": "ch-low", "name": "1: TOF MS",
                "channelDataShape": "Spectra", "channelDataType": "MSScanning",
                "scanCount": 2,
                "technique": {
                  "basicMsProperties": {
                    "scanningMethod": "MS",
                    "ionisationType": "ESI",
                    "ionisationMode": "Positive",
                    "acquiredMOverZRange": { "start": 50, "end": 1200 }
                  },
                  "fragmentationProperties": { "energyLevelType": "Low" }
                }
              }
            ]
            """);

        // cpp builds the URL as
        //   /ms/spectra?channelFilter=channelId eq <id>&scanFilter=index ge X and index lt Y&spectrumType=Continuum
        // The HttpClient URL-encodes the spaces in the query string, so the assert URL must
        // match the encoded form.
        string spectraUrl = $"{Host}/waters_connect/v2.0/injection-data/{InjectionId}/ms/spectra"
            + "?channelFilter=channelId%20eq%20ch-low"
            + "&scanFilter=index%20ge%200%20and%20index%20lt%202"
            + "&spectrumType=Continuum";

        // Build a protobuf MzSpectraDtoV2Collection with one channel envelope and two spectra.
        var collection = new MzSpectraDtoV2Collection();
        var envelope = new MzSpectraDtoV2 { ParentChannelId = "ch-low" };
        var spec0 = new MzSpectrumItemDtoV2
        {
            ScanIndex = 0,
            RetentionTime = 0.5,
            ContinuumSpectrumData = new ContinuumSpectrumDataDto(),
        };
        spec0.ContinuumSpectrumData.Masses.AddRange(new[] { 100.0, 200.0 });
        spec0.ContinuumSpectrumData.Intensities.AddRange(new[] { 10.0, 20.0 });
        envelope.Values.Add(spec0);

        var spec1 = new MzSpectrumItemDtoV2
        {
            ScanIndex = 1,
            RetentionTime = 1.0,
            ContinuumSpectrumData = new ContinuumSpectrumDataDto(),
        };
        spec1.ContinuumSpectrumData.Masses.AddRange(new[] { 300.0 });
        spec1.ContinuumSpectrumData.Intensities.AddRange(new[] { 30.0 });
        envelope.Values.Add(spec1);

        collection.Items.Add(envelope);
        byte[] payload = collection.ToByteArray();
        handler.Expect("GET", spectraUrl, _ => Bytes(payload));

        using var src = BuildSource(handler);

        var s0 = new UnifiSpectrum();
        src.GetSpectrum(0, s0, getBinaryData: true, doCentroid: false);
        Assert.AreEqual(0.5, s0.RetentionTime);
        Assert.AreEqual(1, s0.MsLevel);
        Assert.AreEqual(UnifiPolarity.Positive, s0.ScanPolarity);
        Assert.AreEqual(2, s0.ArrayLength);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0 }, s0.MzArray);
        CollectionAssert.AreEqual(new[] { 10.0, 20.0 }, s0.IntensityArray);

        var s1 = new UnifiSpectrum();
        src.GetSpectrum(1, s1, getBinaryData: true, doCentroid: false);
        Assert.AreEqual(1, s1.ArrayLength);
        CollectionAssert.AreEqual(new[] { 300.0 }, s1.MzArray);
        handler.AssertExhausted();
    }

    [TestMethod]
    public void GetSpectrum_CentroidUsesDifferentEndpoint()
    {
        var handler = new ScriptedHandler();
        handler.AddOAuth();
        handler.AddInjectionMetadata();
        handler.AddChannelsArray("""
            [
              {
                "id": "ch-low", "name": "1: TOF MS",
                "channelDataShape": "Spectra", "channelDataType": "MSScanning",
                "scanCount": 1,
                "technique": {
                  "basicMsProperties": {
                    "scanningMethod": "MS",
                    "ionisationType": "ESI",
                    "ionisationMode": "Positive",
                    "acquiredMOverZRange": { "start": 50, "end": 1200 }
                  },
                  "fragmentationProperties": { "energyLevelType": "Low" }
                }
              }
            ]
            """);

        string spectraUrl = $"{Host}/waters_connect/v2.0/injection-data/{InjectionId}/ms/spectra"
            + "?channelFilter=channelId%20eq%20ch-low"
            + "&scanFilter=index%20ge%200%20and%20index%20lt%201"
            + "&spectrumType=Centered";

        var collection = new MzSpectraDtoV2Collection();
        var envelope = new MzSpectraDtoV2 { ParentChannelId = "ch-low" };
        var spec = new MzSpectrumItemDtoV2
        {
            ScanIndex = 0,
            RetentionTime = 0.1,
            CentroidSpectrumData = new CentroidSpectrumDataDto(),
        };
        spec.CentroidSpectrumData.MOverZs.AddRange(new[] { 110.5 });
        spec.CentroidSpectrumData.Intensities.AddRange(new[] { 5.0 });
        envelope.Values.Add(spec);
        collection.Items.Add(envelope);
        handler.Expect("GET", spectraUrl, _ => Bytes(collection.ToByteArray()));

        using var src = BuildSource(handler);
        var result = new UnifiSpectrum();
        src.GetSpectrum(0, result, getBinaryData: true, doCentroid: true);
        Assert.AreEqual(1, result.ArrayLength);
        CollectionAssert.AreEqual(new[] { 110.5 }, result.MzArray);
        Assert.IsFalse(result.DataIsContinuous, "centroid path should mark non-continuous");
    }

    [TestMethod]
    public void GetSpectrum_OutOfRange_Throws()
    {
        var handler = new ScriptedHandler();
        handler.AddOAuth();
        handler.AddInjectionMetadata();
        handler.AddChannelsArray("[]");
        using var src = BuildSource(handler);
        Assert.AreEqual(0, src.NumberOfSpectra);
        Assert.ThrowsException<ArgumentOutOfRangeException>(()
            => src.GetSpectrum(0, new UnifiSpectrum(), false, false));
    }

    private static HttpResponseMessage Bytes(byte[] body)
        => new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-protobuf") },
            },
        };

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static WatersConnectDataSource BuildSource(ScriptedHandler handler)
    {
        var client = new WatersConnectHttpClient(
            UnifiConnectionConfig.Parse($"https://u:p@wc.example:48444/?sampleSetId={SetId}&injectionId={InjectionId}"),
            new HttpClient(handler));
        return new WatersConnectDataSource(client, combineIonMobilitySpectra: false, ownsClient: true);
    }

    internal sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<(string Method, string Url, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _expectations = new();

        public void Expect(string method, string url, Func<HttpRequestMessage, HttpResponseMessage> respond)
            => _expectations.Enqueue((method, url, respond));

        public void AddOAuth() => Expect("POST", "https://wc.example:48333/connect/token",
            _ => Json("{\"access_token\":\"WC-TOK\"}"));

        public void AddInjectionMetadata()
        {
            // waters_connect's WatersConnectHttpClient ctor walks the injection list to find
            // the matching injectionId. We respond with a single matching injection.
            Expect("GET", $"{Host}/waters_connect/v2.0/sample-sets/{SetId}/injection-data",
                _ => Json("{\"value\":[{\"id\":\"" + InjectionId + "\",\"name\":\"sample\","
                    + "\"sample\":{\"replicateNumber\":1,\"wellPosition\":\"1:A,1\","
                    + "\"acquisitionStartTime\":\"2018-10-27T09:00:00Z\"}}]}"));
        }

        // The data source ctor walks: /channels, /channels/ms/tic, /channels/mrm in order.
        // Default the TIC + MRM responses to empty arrays so tests that only care about
        // channel + spectrum behavior don't have to stub every chromatogram endpoint.
        public void AddChannelsArray(string body)
        {
            Expect("GET", $"{Host}/waters_connect/v2.0/injection-data/{InjectionId}/channels",
                _ => Json(body));
            Expect("GET", $"{Host}/waters_connect/v2.0/injection-data/{InjectionId}/channels/ms/tic",
                _ => Json("[]"));
            Expect("GET", $"{Host}/waters_connect/v2.0/injection-data/{InjectionId}/channels/mrm",
                _ => Json("[]"));
        }

        public void AssertExhausted()
            => Assert.AreEqual(0, _expectations.Count, "Not all scripted expectations were consumed");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(Send(request, ct));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken ct)
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
