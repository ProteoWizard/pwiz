using System.Net;
using System.Net.Http;
using System.Text;

namespace Pwiz.Vendor.UNIFI.Tests;

[TestClass]
public class HttpUnifiDataSourceTests
{
    private const string SampleUrl = "https://demo.example:48505/unifi/v1/sampleresults(abc-123)";

    [TestMethod]
    public void Discovery_ParsesFunctionsAndChromatograms_NoIms()
    {
        // Two MS retention-data functions (Low + High) plus one non-MSe TIC function that
        // should be skipped. cpp UnifiData.cpp:1071-1164 (function discovery) +
        // 1178-1259 (chromatogram discovery).
        var handler = new ScriptedHandler();
        handler.AddOAuth();
        handler.AddSampleMetadata("MM 5.0");
        handler.AddSpectrumInfos("""
            {
              "value": [
                {
                  "id": "low-id", "detectorType": "MS", "isCentroidData": false,
                  "isRetentionData": true, "isIonMobilityData": false, "hasCCSCalibration": false,
                  "analyticalTechnique": {
                    "lowMass": 50, "highMass": 1200,
                    "tofGroup": { "mseLevel": "Low" }
                  }
                },
                {
                  "id": "high-id", "detectorType": "MS", "isCentroidData": false,
                  "isRetentionData": true, "isIonMobilityData": false, "hasCCSCalibration": false,
                  "analyticalTechnique": {
                    "lowMass": 50, "highMass": 1200,
                    "tofGroup": { "mseLevel": "High" }
                  }
                },
                {
                  "id": "ref-id", "detectorType": "MS", "isCentroidData": false,
                  "isRetentionData": true, "isIonMobilityData": false, "hasCCSCalibration": false,
                  "analyticalTechnique": {
                    "lowMass": 551, "highMass": 562,
                    "tofGroup": { "mseLevel": "Unknown" }
                  }
                }
              ]
            }
            """);
        // Per-function /data endpoint returns totalNumberOfSpectra. Order doesn't matter
        // (the data source GETs each one as it walks).
        handler.AddFunctionData("low-id", totalSpectra: 100);
        handler.AddFunctionData("high-id", totalSpectra: 100);
        handler.AddChromatogramInfos("""
            {
              "value": [
                { "id": "tic-low", "name": "1: TOF MSe (50-1200) 4eV ESI+ (TIC)", "detectorType": "MS" },
                { "id": "bpi-low", "name": "1: TOF MSe (50-1200) 4eV ESI+ (BPC)", "detectorType": "MS" },
                { "id": "uv-1", "name": "Detector A 254nm", "detectorType": "UV" },
                { "id": "flr-1", "name": "FLR A", "detectorType": "FLR" }
              ]
            }
            """);

        var client = new UnifiHttpClient(
            UnifiConnectionConfig.Parse($"https://alice:pw@{SampleUrl["https://".Length..]}"),
            new HttpClient(handler));
        using var src = new HttpUnifiDataSource(client, combineIonMobilitySpectra: false, ownsClient: true);

        Assert.AreEqual(RemoteApiType.Unifi, src.RemoteApi);
        Assert.IsFalse(src.HasIonMobilityData);
        Assert.AreEqual(200, src.NumberOfSpectra); // Low(100) + High(100)
        Assert.AreEqual(2, src.Functions.Count);
        Assert.AreEqual(UnifiEnergyLevel.Low, src.Functions[0].EnergyLevel);
        Assert.AreEqual(UnifiEnergyLevel.High, src.Functions[1].EnergyLevel);
        Assert.AreEqual("low-id", src.Functions[0].Id);
        Assert.AreEqual("high-id", src.Functions[1].Id);

        // Chromatogram catalog: TIC → ChromatogramType.TIC, BPI → also TIC (cpp groups them
        // by name-contains-"TIC" only), UV → UV, FLR → FLR.
        Assert.AreEqual(4, src.ChromatogramInfo.Count);
        Assert.AreEqual(ChromatogramType.TIC, src.ChromatogramInfo[0].Type);
        Assert.AreEqual(ChromatogramType.Unknown, src.ChromatogramInfo[1].Type); // "(BPC)" without "TIC" → Unknown
        Assert.AreEqual(ChromatogramType.UV, src.ChromatogramInfo[2].Type);
        Assert.AreEqual(ChromatogramType.FLR, src.ChromatogramInfo[3].Type);
        handler.AssertExhausted();
    }

    [TestMethod]
    public void Discovery_IonMobility_FetchesBinTable_AndMultipliesSpectrumCount()
    {
        // cpp UnifiData.cpp:1265-1289: when any function has IMS data, POST /binToDriftTime
        // with bins 1..200 and parse the 200-element response into the bin→drift-time table.
        // The total spectrum count when combineIonMobilitySpectra=false multiplies the IMS
        // function's numSpectra by 200.
        var handler = new ScriptedHandler();
        handler.AddOAuth();
        handler.AddSampleMetadata("HDMSe sample");
        handler.AddSpectrumInfos("""
            {
              "value": [
                {
                  "id": "hd-low", "detectorType": "MS", "isCentroidData": false,
                  "isRetentionData": true, "isIonMobilityData": true, "hasCCSCalibration": false,
                  "analyticalTechnique": {
                    "lowMass": 50, "highMass": 1200,
                    "tofGroup": { "mseLevel": "Low" }
                  }
                }
              ]
            }
            """);
        handler.AddFunctionData("hd-low", totalSpectra: 50);
        handler.AddChromatogramInfos("""{"value":[]}""");
        // Synthesize the 200-bin response. cpp asserts size == 200; we mirror that.
        var driftTimes = new StringBuilder("{\"value\":[");
        for (int i = 0; i < 200; i++)
        {
            if (i > 0) driftTimes.Append(',');
            driftTimes.Append((0.071 * (i + 1)).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        driftTimes.Append("]}");
        handler.Expect("POST", $"{SampleUrl}/binToDriftTime", _ => Json(driftTimes.ToString()));

        var client = new UnifiHttpClient(
            UnifiConnectionConfig.Parse($"https://u:p@{SampleUrl["https://".Length..]}"),
            new HttpClient(handler));
        using var src = new HttpUnifiDataSource(client, combineIonMobilitySpectra: false, ownsClient: true);

        Assert.IsTrue(src.HasIonMobilityData);
        Assert.AreEqual(50 * 200, src.NumberOfSpectra);
        Assert.AreEqual(200, src.BinToDriftTime.Count);
        Assert.AreEqual(0.071, src.BinToDriftTime[0], 1e-9);
        Assert.AreEqual(14.2, src.BinToDriftTime[199], 1e-9);
        handler.AssertExhausted();
    }

    [TestMethod]
    public void Discovery_OnlyMseAndHdMseSupported()
    {
        // cpp UnifiData.cpp:1175-1176: error out when no MSe data is present. We mirror with
        // the same message so callers can match on it.
        var handler = new ScriptedHandler();
        handler.AddOAuth();
        handler.AddSampleMetadata("LC only");
        handler.AddSpectrumInfos("""
            {
              "value": [
                {
                  "id": "uv-only", "detectorType": "UV", "isCentroidData": false,
                  "isRetentionData": true, "isIonMobilityData": false, "hasCCSCalibration": false
                }
              ]
            }
            """);
        // We never get to chromatogramInfos because the discovery throws first.

        var ex = Assert.ThrowsException<InvalidOperationException>(() =>
        {
            var client = new UnifiHttpClient(
                UnifiConnectionConfig.Parse($"https://u:p@{SampleUrl["https://".Length..]}"),
                new HttpClient(handler));
            using var src = new HttpUnifiDataSource(client, combineIonMobilitySpectra: false, ownsClient: true);
        });
        StringAssert.Contains(ex.Message, "only MSe and HD-MSe data is supported");
    }

    [TestMethod]
    public void GetChromatogram_FetchesAndParsesArrays()
    {
        var handler = new ScriptedHandler();
        handler.AddOAuth();
        handler.AddSampleMetadata("data");
        handler.AddSpectrumInfos("""
            {
              "value": [
                {
                  "id": "low-id", "detectorType": "MS", "isCentroidData": false,
                  "isRetentionData": true, "isIonMobilityData": false, "hasCCSCalibration": false,
                  "analyticalTechnique": {
                    "lowMass": 50, "highMass": 1200,
                    "tofGroup": { "mseLevel": "Low" }
                  }
                }
              ]
            }
            """);
        handler.AddFunctionData("low-id", totalSpectra: 5);
        handler.AddChromatogramInfos("""
            {
              "value": [
                { "id": "tic-low", "name": "1: TOF MSe (TIC)", "detectorType": "MS" }
              ]
            }
            """);
        // Per-chromatogram data fetch.
        handler.Expect("GET", $"{SampleUrl}/chromatogramInfos(tic-low)/data", _ => Json("""
            {
              "value": [
                {
                  "id": "tic-low",
                  "retentionTimes": [0.0, 0.5, 1.0, 1.5],
                  "intensities": [100.0, 200.0, 300.0, 250.0]
                }
              ]
            }
            """));

        var client = new UnifiHttpClient(
            UnifiConnectionConfig.Parse($"https://u:p@{SampleUrl["https://".Length..]}"),
            new HttpClient(handler));
        using var src = new HttpUnifiDataSource(client, combineIonMobilitySpectra: false, ownsClient: true);

        var chrom = new UnifiChromatogram();
        src.GetChromatogram(0, chrom, getBinaryData: true);

        Assert.AreEqual(4, chrom.ArrayLength);
        Assert.AreEqual(ChromatogramType.TIC, chrom.Type);
        Assert.AreEqual("1: TOF MSe (TIC)", chrom.Name);
        CollectionAssert.AreEqual(new[] { 0.0, 0.5, 1.0, 1.5 }, chrom.TimeArray);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0, 250.0 }, chrom.IntensityArray);
        handler.AssertExhausted();
    }

    [TestMethod]
    public void GetMsLevel_AlternatesByEvenOdd()
    {
        // cpp UnifiData.cpp:264-268: even index → MS1, odd → MS2 (MSe alternation).
        var handler = new ScriptedHandler();
        handler.AddOAuth();
        handler.AddSampleMetadata("x");
        handler.AddSpectrumInfos("""
            {"value":[
              {"id":"low","detectorType":"MS","isCentroidData":false,"isRetentionData":true,
               "isIonMobilityData":false,"hasCCSCalibration":false,
               "analyticalTechnique":{"lowMass":50,"highMass":1200,"tofGroup":{"mseLevel":"Low"}}}
            ]}
            """);
        handler.AddFunctionData("low", 10);
        handler.AddChromatogramInfos("""{"value":[]}""");

        var client = new UnifiHttpClient(
            UnifiConnectionConfig.Parse($"https://u:p@{SampleUrl["https://".Length..]}"),
            new HttpClient(handler));
        using var src = new HttpUnifiDataSource(client, combineIonMobilitySpectra: false, ownsClient: true);

        Assert.AreEqual(1, src.GetMsLevel(0));
        Assert.AreEqual(2, src.GetMsLevel(1));
        Assert.AreEqual(1, src.GetMsLevel(2));
        Assert.AreEqual(2, src.GetMsLevel(3));
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    /// <summary>Scripted HTTP handler — same shape as the OAuth flow tests, with helpers for
    /// the discovery endpoints UNIFI hits during ctor.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<(string Method, string Url, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _expectations = new();

        public void Expect(string method, string url, Func<HttpRequestMessage, HttpResponseMessage> respond)
            => _expectations.Enqueue((method, url, respond));

        public void AddOAuth() => Expect("POST", "https://demo.example:48333/connect/token",
            _ => Json("""{"access_token":"TOK"}"""));

        public void AddSampleMetadata(string sampleName) => Expect("GET", SampleUrl,
            _ => Json("{\"id\":\"abc-123\",\"name\":\"" + sampleName + "\",\"sample\":{\"replicateNumber\":1,\"wellPosition\":\"1:A,1\",\"acquisitionStartTime\":\"2017-03-08T15:42:13Z\"}}"));

        public void AddSpectrumInfos(string body) => Expect("GET", $"{SampleUrl}/spectrumInfos", _ => Json(body));

        public void AddFunctionData(string id, int totalSpectra) => Expect("GET",
            $"{SampleUrl}/spectrumInfos({id})/data?$top=1",
            _ => Json("{\"value\":[{\"totalNumberOfSpectra\":" + totalSpectra + "}]}"));

        public void AddChromatogramInfos(string body) => Expect("GET", $"{SampleUrl}/chromatogramInfos", _ => Json(body));

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
