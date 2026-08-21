using System.Net;
using System.Net.Http;
using System.Text;
using Google.Protobuf;
using Pwiz.Vendor.UNIFI.Protobuf;

namespace Pwiz.Vendor.UNIFI.Tests;

[TestClass]
public class HttpUnifiDataSourceSpectrumTests
{
    private const string SampleUrl = "https://demo.example:48505/unifi/v1/sampleresults(abc-123)";

    [TestMethod]
    public void GetSpectrum_NonIms_FetchesAndDecodesProtobufChunk()
    {
        // End-to-end: build a protobuf-encoded chunk of two MSeMassSpectrum messages (Low +
        // High), serve them at /spectra/mass.mse?$skip=0, then verify GetSpectrum(0)/GetSpectrum(1)
        // produce UnifiSpectrum objects matching what cpp's convertWatersToPwizSpectrum
        // (UnifiData.cpp:1305-1353) would emit.
        var chunkBytes = BuildChunk(new[]
        {
            BuildSpectrum(retentionTime: 0.5, energy: ProtoEnergyLevel.EnergyLevelLow,
                          polarity: ProtoPolarity.PolarityPositive,
                          masses: new[] { 100.1, 200.2 }, intensities: new[] { 10.0, 20.0 }),
            BuildSpectrum(retentionTime: 0.5, energy: ProtoEnergyLevel.EnergyLevelHigh,
                          polarity: ProtoPolarity.PolarityPositive,
                          masses: new[] { 50.0, 60.0, 70.0 }, intensities: new[] { 1.0, 2.0, 3.0 }),
        });

        using var src = BuildSource(handler =>
        {
            handler.Expect("GET", $"{SampleUrl}/spectra/mass.mse?$skip=0&$top=20",
                _ => Bytes(chunkBytes));
        }, lowSpectra: 1, highSpectra: 1, hasIms: false);

        var ms1 = new UnifiSpectrum();
        src.GetSpectrum(0, ms1, getBinaryData: true, doCentroid: false);
        Assert.AreEqual(1, ms1.MsLevel);
        Assert.AreEqual(0.5, ms1.RetentionTime);
        Assert.AreEqual(UnifiPolarity.Positive, ms1.ScanPolarity);
        Assert.AreEqual(UnifiEnergyLevel.Low, ms1.EnergyLevel);
        Assert.AreEqual((50.0, 1200.0), ms1.ScanRange);
        Assert.AreEqual(2, ms1.ArrayLength);
        // Wire encoding is `repeated float`; arrays are widened to double for the pwiz layer,
        // so an exact literal compare needs the float-rounded value of the test input.
        CollectionAssert.AreEqual(new[] { (double)100.1f, (double)200.2f }, ms1.MzArray);
        CollectionAssert.AreEqual(new[] { (double)10.0f, (double)20.0f }, ms1.IntensityArray);

        var ms2 = new UnifiSpectrum();
        src.GetSpectrum(1, ms2, getBinaryData: true, doCentroid: false);
        Assert.AreEqual(2, ms2.MsLevel);
        Assert.AreEqual(UnifiEnergyLevel.High, ms2.EnergyLevel);
        Assert.AreEqual(3, ms2.ArrayLength);
        CollectionAssert.AreEqual(new[] { (double)50.0f, (double)60.0f, (double)70.0f }, ms2.MzArray);
    }

    [TestMethod]
    public void GetSpectrum_IonMobility_PerBinSlicing()
    {
        // IMS spectrum with ScanSize.Length == 200 — each logical index picks a drift bin
        // by `logicalIndex % 200`, with the m/z array sliced according to the cumulative
        // ScanSize offset. cpp UnifiData.cpp:1334-1352.
        // Build a synthetic IMS spectrum: 200 bins, scan sizes [3, 2, 0, 0, ..., 0] with
        // 5 total points distributed across bins 0 and 1. Bins 2..199 are empty.
        var scanSize = new int[200];
        scanSize[0] = 3;
        scanSize[1] = 2;
        var masses = new[] { 100.0, 110.0, 120.0, 200.0, 210.0 };
        var intensities = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };

        var chunkBytes = BuildChunk(new[]
        {
            BuildSpectrum(retentionTime: 0.5, energy: ProtoEnergyLevel.EnergyLevelLow,
                          polarity: ProtoPolarity.PolarityPositive,
                          masses: masses, intensities: intensities, scanSize: scanSize),
        });

        using var src = BuildSource(handler =>
        {
            handler.Expect("GET", $"{SampleUrl}/spectra/mass.mse?$skip=0&$top=20",
                _ => Bytes(chunkBytes));
        }, lowSpectra: 1, highSpectra: 0, hasIms: true);

        // logicalIndex 0 → bin 0 → 3 points starting at offset 0.
        var bin0 = new UnifiSpectrum();
        src.GetSpectrum(0, bin0, getBinaryData: true, doCentroid: false);
        Assert.AreEqual(3, bin0.ArrayLength);
        CollectionAssert.AreEqual(new[] { (double)100.0f, (double)110.0f, (double)120.0f }, bin0.MzArray);
        Assert.IsTrue(bin0.DriftTime > 0, "drift time should come from bin table");

        // logicalIndex 1 → bin 1 → 2 points starting at offset 3.
        var bin1 = new UnifiSpectrum();
        src.GetSpectrum(1, bin1, getBinaryData: true, doCentroid: false);
        Assert.AreEqual(2, bin1.ArrayLength);
        CollectionAssert.AreEqual(new[] { (double)200.0f, (double)210.0f }, bin1.MzArray);
        Assert.IsTrue(bin1.DriftTime > bin0.DriftTime, "drift time should grow with bin index");

        // logicalIndex 2 → bin 2 → 0 points (empty bin).
        var bin2 = new UnifiSpectrum();
        src.GetSpectrum(2, bin2, getBinaryData: true, doCentroid: false);
        Assert.AreEqual(0, bin2.ArrayLength);
    }

    [TestMethod]
    public void GetSpectrum_OutOfRange_Throws()
    {
        using var src = BuildSource(_ => { }, lowSpectra: 1, highSpectra: 1, hasIms: false);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            src.GetSpectrum(99, new UnifiSpectrum(), false, false));
    }

    [TestMethod]
    public void GetSpectrum_ChunkCached_OnSecondAccess()
    {
        // Verify EnsureChunkLoaded reuses the cache: only one HTTP GET fires for the chunk
        // even when GetSpectrum is called twice for indices in the same chunk.
        int chunkRequests = 0;
        var chunkBytes = BuildChunk(new[]
        {
            BuildSpectrum(retentionTime: 0.1, energy: ProtoEnergyLevel.EnergyLevelLow,
                          polarity: ProtoPolarity.PolarityPositive,
                          masses: new[] { 100.0 }, intensities: new[] { 1.0 }),
            BuildSpectrum(retentionTime: 0.2, energy: ProtoEnergyLevel.EnergyLevelHigh,
                          polarity: ProtoPolarity.PolarityPositive,
                          masses: new[] { 200.0 }, intensities: new[] { 2.0 }),
        });

        using var src = BuildSource(handler =>
        {
            handler.Expect("GET", $"{SampleUrl}/spectra/mass.mse?$skip=0&$top=20", _ =>
            {
                Interlocked.Increment(ref chunkRequests);
                return Bytes(chunkBytes);
            });
        }, lowSpectra: 1, highSpectra: 1, hasIms: false);

        src.GetSpectrum(0, new UnifiSpectrum(), getBinaryData: false, doCentroid: false);
        src.GetSpectrum(1, new UnifiSpectrum(), getBinaryData: false, doCentroid: false);
        Assert.AreEqual(1, chunkRequests, "Both spectra are in the same chunk; only one fetch should fire");
    }

    private static byte[] BuildSpectrum(
        double retentionTime, ProtoEnergyLevel energy, ProtoPolarity polarity,
        double[] masses, double[] intensities, int[]? scanSize = null)
    {
        var spec = new Spectrum();
        // Wire encodes both arrays as `repeated float` — narrow input doubles for the
        // mock payload so the parser decodes them as the live UNIFI server would.
        foreach (var v in intensities) spec.Intensities.Add((float)v);
        var mass = new MassSpectrum();
        foreach (var v in masses) mass.Masses.Add((float)v);
        if (scanSize is not null) mass.ScanSize.AddRange(scanSize);
        var mse = new MSeMassSpectrum
        {
            RetentionTime = retentionTime,
            EnergyLevel = energy,
            IonizationPolarity = polarity,
        };
        mass.MseMassSpectrum = mse;
        spec.MassSpectrum = mass;
        // Length-prefixed encoding — same wire format cpp uses
        // (Serializer.SerializeWithLengthPrefix / PrefixStyle::Base128).
        using var ms = new MemoryStream();
        spec.WriteDelimitedTo(ms);
        return ms.ToArray();
    }

    private static byte[] BuildChunk(IEnumerable<byte[]> spectra)
    {
        // Each spectrum is already length-prefixed. Concatenate.
        using var ms = new MemoryStream();
        foreach (var s in spectra) ms.Write(s);
        return ms.ToArray();
    }

    private static HttpResponseMessage Bytes(byte[] body)
        => new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream") },
            },
        };

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    /// <summary>Builds an <see cref="HttpUnifiDataSource"/> with the discovery endpoints
    /// pre-scripted so tests can focus on the spectrum-fetch behavior.</summary>
    private static HttpUnifiDataSource BuildSource(
        Action<ScriptedHandler> addSpectrumExpectations,
        int lowSpectra, int highSpectra, bool hasIms)
    {
        var handler = new ScriptedHandler();
        handler.AddOAuth();
        handler.AddSampleMetadata();
        var functionsJson = new StringBuilder("{\"value\":[");
        bool first = true;
        if (lowSpectra > 0)
        {
            functionsJson.Append(FunctionJson("low-id", "Low", hasIms));
            first = false;
        }
        if (highSpectra > 0)
        {
            if (!first) functionsJson.Append(',');
            functionsJson.Append(FunctionJson("high-id", "High", hasIms));
        }
        functionsJson.Append("]}");
        handler.Expect("GET", $"{SampleUrl}/spectrumInfos", _ => Json(functionsJson.ToString()));

        if (lowSpectra > 0) handler.AddFunctionData("low-id", lowSpectra);
        if (highSpectra > 0) handler.AddFunctionData("high-id", highSpectra);

        handler.Expect("GET", $"{SampleUrl}/chromatogramInfos",
            _ => Json("{\"value\":[]}"));

        if (hasIms)
        {
            // 200-bin POST response.
            var driftTimes = new StringBuilder("{\"value\":[");
            for (int i = 0; i < 200; i++)
            {
                if (i > 0) driftTimes.Append(',');
                driftTimes.Append((0.071 * (i + 1)).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            driftTimes.Append("]}");
            handler.Expect("POST", $"{SampleUrl}/binToDriftTime", _ => Json(driftTimes.ToString()));
        }

        addSpectrumExpectations(handler);

        var client = new UnifiHttpClient(
            UnifiConnectionConfig.Parse($"https://u:p@{SampleUrl["https://".Length..]}"),
            new HttpClient(handler));
        return new HttpUnifiDataSource(client, combineIonMobilitySpectra: !hasIms, ownsClient: true);
    }

    private static string FunctionJson(string id, string mseLevel, bool hasIms)
        => "{\"id\":\"" + id + "\",\"detectorType\":\"MS\",\"isCentroidData\":false,"
           + "\"isRetentionData\":true,\"isIonMobilityData\":" + (hasIms ? "true" : "false")
           + ",\"hasCCSCalibration\":false,"
           + "\"analyticalTechnique\":{\"lowMass\":50,\"highMass\":1200,"
           + "\"tofGroup\":{\"mseLevel\":\"" + mseLevel + "\"}}}";

    /// <summary>Same scripted handler as the discovery tests — kept locally so the two test
    /// classes don't share mutable test infrastructure.</summary>
    internal sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<(string Method, string Url, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _expectations = new();

        public void Expect(string method, string url, Func<HttpRequestMessage, HttpResponseMessage> respond)
            => _expectations.Enqueue((method, url, respond));

        public void AddOAuth() => Expect("POST", "https://demo.example:48333/connect/token",
            _ => Json("{\"access_token\":\"TOK\"}"));

        public void AddSampleMetadata() => Expect("GET", SampleUrl,
            _ => Json("{\"id\":\"abc-123\",\"name\":\"sample\",\"sample\":{\"replicateNumber\":1,\"wellPosition\":\"1:A,1\",\"acquisitionStartTime\":\"2017-03-08T15:42:13Z\"}}"));

        public void AddFunctionData(string id, int totalSpectra) => Expect("GET",
            $"{SampleUrl}/spectrumInfos({id})/data?$top=1",
            _ => Json("{\"value\":[{\"totalNumberOfSpectra\":" + totalSpectra + "}]}"));

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
