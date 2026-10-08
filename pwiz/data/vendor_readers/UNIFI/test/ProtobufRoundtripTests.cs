using System.IO;
using Google.Protobuf;
using Pwiz.Vendor.UNIFI.Protobuf;

namespace Pwiz.Vendor.UNIFI.Tests;

/// <summary>
/// Round-trips the UNIFI / waters_connect protobuf contracts through Google.Protobuf
/// (matching Skyline's serializer choice — see UNIFI.csproj reference to
/// pwiz_tools/Shared/Lib/Google.Protobuf.dll). The wire format is the same protobuf binary
/// spec the cpp UNIFI client emits via its bundled <c>unifi-protobuf-net.dll</c>, so a
/// successful round-trip here means our schema lines up tag-for-tag with the cpp port.
/// </summary>
[TestClass]
public class ProtobufRoundtripTests
{
    [TestMethod]
    public void LegacyUnifi_MseAndDdaRoundTripThroughBaseSpectrum()
    {
        // Schema sanity: the legacy UNIFI hierarchy uses Spectrum -> MassSpectrum (tag 200)
        // -> {MSeMassSpectrum (100), DDAMassSpectrum (101)}. cpp UnifiData.cpp:119-190.
        // Each subtype is represented as a singleton message field in proto3.
        var mseRoot = new Spectrum
        {
            MassSpectrum = new MassSpectrum
            {
                MseMassSpectrum = new MSeMassSpectrum
                {
                    RetentionTime = 12.345,
                    EnergyLevel = ProtoEnergyLevel.EnergyLevelHigh,
                    IonizationPolarity = ProtoPolarity.PolarityPositive,
                },
            },
        };
        mseRoot.Intensities.AddRange(new[] { 1.0f, 2.0f, 3.0f });
        mseRoot.MassSpectrum.Masses.AddRange(new[] { 100.1f, 200.2f, 300.3f });
        mseRoot.MassSpectrum.ScanSize.AddRange(new[] { 1, 2 });

        var mseRoundTrip = RoundTrip(mseRoot, Spectrum.Parser);
        Assert.IsNotNull(mseRoundTrip.MassSpectrum);
        Assert.IsNotNull(mseRoundTrip.MassSpectrum!.MseMassSpectrum);
        Assert.AreEqual(12.345, mseRoundTrip.MassSpectrum.MseMassSpectrum!.RetentionTime);
        Assert.AreEqual(ProtoEnergyLevel.EnergyLevelHigh, mseRoundTrip.MassSpectrum.MseMassSpectrum.EnergyLevel);
        Assert.AreEqual(ProtoPolarity.PolarityPositive, mseRoundTrip.MassSpectrum.MseMassSpectrum.IonizationPolarity);
        CollectionAssert.AreEqual(new[] { 100.1f, 200.2f, 300.3f }, mseRoundTrip.MassSpectrum.Masses);
        CollectionAssert.AreEqual(new[] { 1.0f, 2.0f, 3.0f }, mseRoundTrip.Intensities);
        CollectionAssert.AreEqual(new[] { 1, 2 }, mseRoundTrip.MassSpectrum.ScanSize);

        var ddaRoot = new Spectrum
        {
            MassSpectrum = new MassSpectrum
            {
                DdaMassSpectrum = new DDAMassSpectrum
                {
                    RetentionTime = 7.89,
                    SetMass = 489.5f,
                    MsType = ProtoMSType.MsTypeMs2,
                },
            },
        };
        ddaRoot.Intensities.AddRange(new[] { 5.0f, 6.0f });
        ddaRoot.MassSpectrum.Masses.AddRange(new[] { 110.0f, 220.0f });

        var ddaRoundTrip = RoundTrip(ddaRoot, Spectrum.Parser);
        Assert.IsNotNull(ddaRoundTrip.MassSpectrum?.DdaMassSpectrum);
        Assert.AreEqual(7.89, ddaRoundTrip.MassSpectrum!.DdaMassSpectrum!.RetentionTime);
        Assert.AreEqual(489.5f, ddaRoundTrip.MassSpectrum.DdaMassSpectrum.SetMass);
        Assert.AreEqual(ProtoMSType.MsTypeMs2, ddaRoundTrip.MassSpectrum.DdaMassSpectrum.MsType);
    }

    [TestMethod]
    public void WatersConnect_NestedHierarchyRoundTrips()
    {
        // Build a representative response — one channel with two spectra (continuum +
        // precursor oneof, then centroid + product oneof). Hits every key tag we ported.
        var collection = new MzSpectraDtoV2Collection();
        var channel = new MzSpectraDtoV2
        {
            ParentChannelId = "channel-guid-1",
            ChannelName = "1: TOF MSe (50-1200) 4eV ESI+ - Low CE",
            Title = "MSe Low",
            TotalSpectrumCount = 2,
            CorrectionStatus = "OK",
            MsTechnique = new MSTechniqueDtoV4
            {
                IsLockMassData = false,
                InstrumentId = "INSTR-1",
                InstrumentInternalName = "Synapt-G2-Si",
                BasicMsProperties = new BasicMsPropertiesDtoV3
                {
                    ScanTime = 0.5,
                    InterScanDelay = 0.01,
                    IonisationMode = IonisationModeDto.Positive,
                    IonisationType = "ESI",
                    ConeVoltage = 30.0,
                    MassAnalyser = "TIME OF FLIGHT",
                    ScanningMethod = "MS",
                    AcquiredMOverZRange = new RangeDtoDouble { Start = 50.0, End = 1200.0 },
                },
                MsCalibration = new MsCalibrationDto { Resolution = 10000.0 },
                FragmentationProperties = new FragmentationPropertiesDto
                {
                    EnergyLevelType = EnergyLevelDto.Low,
                    CollisionEnergy = 4.0,
                },
                TofProperties = new TofPropertiesDto
                {
                    TimeZero = 0.1,
                    PusherFrequency = 21739.13,
                    Lteff = 800.0,
                    Veff = 3307.7,
                    AdcAcquisitionMode = AdcAcquisitionModeDto.AdcPeakDetecting,
                    AdcIonResponse = new AdcIonResponseDto { MassOverCharge = 556.2, Charge = 1, AverageSingleIonResponse = 33.88 },
                },
                TargetProperties = new TargetPropertiesDto
                {
                    TargetId = "target-guid",
                    TargetName = "PEPTIDE",
                    TargetGroup = "AssayA",
                },
            },
        };
        channel.MsTechnique.BasicMsProperties.SetMasses.AddRange(new[] { 100.0, 200.0 });

        // Spectrum 0: continuum + precursor oneof (survey scan).
        var spec0 = new MzSpectrumItemDtoV2
        {
            ScanIndex = 0,
            RetentionTime = 0.123,
            ContinuumSpectrumData = new ContinuumSpectrumDataDto(),
            MzPrecursorSpectrumItem = new MzPrecursorSpectrumItemDtoV2(),
        };
        spec0.ContinuumSpectrumData.Masses.AddRange(new[] { 100.0, 200.0, 300.0 });
        spec0.ContinuumSpectrumData.Intensities.AddRange(new[] { 10.0, 20.0, 30.0 });
        var product = new ProductInfoDto { SetMassMz = 489.5, PrecursorMz = 489.4892, PrecursorIntensity = 12345.0 };
        product.ProductScanInfoes.Add(new ScanInfoDto { ChannelId = "channel-guid-2", ScanIndex = 5, ScanRt = 0.456 });
        spec0.MzPrecursorSpectrumItem.ProductsInfoes.Add(product);
        channel.Values.Add(spec0);

        // Spectrum 1: centroid + product oneof (fragment scan).
        var spec1 = new MzSpectrumItemDtoV2
        {
            ScanIndex = 1,
            RetentionTime = 0.456,
            SetMassMz = 489.5,
            CentroidSpectrumData = new CentroidSpectrumDataDto(),
            MzProductSpectrumItem = new MzProductSpectrumItemDtoV2
            {
                PrecursorMz = 489.4892,
                PrecursorIntensity = 12345.0,
                PrecursorScanInfo = new ScanInfoDto { ChannelId = "channel-guid-1", ScanIndex = 0, ScanRt = 0.123 },
            },
        };
        spec1.CentroidSpectrumData.MOverZs.AddRange(new[] { 110.1, 220.2 });
        spec1.CentroidSpectrumData.Intensities.AddRange(new[] { 5.0, 6.0 });
        spec1.CentroidSpectrumData.AccurateFlagIndexes.Add(0);
        channel.Values.Add(spec1);

        collection.Items.Add(channel);

        var rt = RoundTrip(collection, MzSpectraDtoV2Collection.Parser);
        Assert.AreEqual(1, rt.Items.Count);
        var ch = rt.Items[0];
        Assert.AreEqual("channel-guid-1", ch.ParentChannelId);
        Assert.AreEqual("1: TOF MSe (50-1200) 4eV ESI+ - Low CE", ch.ChannelName);
        Assert.AreEqual(2, ch.TotalSpectrumCount);
        Assert.AreEqual("OK", ch.CorrectionStatus);

        Assert.IsNotNull(ch.MsTechnique);
        Assert.AreEqual("INSTR-1", ch.MsTechnique.InstrumentId);
        Assert.AreEqual("Synapt-G2-Si", ch.MsTechnique.InstrumentInternalName);
        Assert.AreEqual(IonisationModeDto.Positive, ch.MsTechnique.BasicMsProperties.IonisationMode);
        Assert.AreEqual(50.0, ch.MsTechnique.BasicMsProperties.AcquiredMOverZRange.Start);
        Assert.AreEqual(1200.0, ch.MsTechnique.BasicMsProperties.AcquiredMOverZRange.End);
        Assert.AreEqual(EnergyLevelDto.Low, ch.MsTechnique.FragmentationProperties.EnergyLevelType);
        Assert.AreEqual(AdcAcquisitionModeDto.AdcPeakDetecting, ch.MsTechnique.TofProperties.AdcAcquisitionMode);
        Assert.AreEqual(556.2, ch.MsTechnique.TofProperties.AdcIonResponse.MassOverCharge);

        Assert.AreEqual(2, ch.Values.Count);

        // First spectrum: continuum + precursor oneof.
        var rtSpec0 = ch.Values[0];
        Assert.AreEqual(MzSpectrumItemDtoV2.SubtypeOneofCase.MzPrecursorSpectrumItem, rtSpec0.SubtypeCase);
        Assert.IsNotNull(rtSpec0.ContinuumSpectrumData);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0 }, rtSpec0.ContinuumSpectrumData.Masses);
        Assert.IsNull(rtSpec0.CentroidSpectrumData);
        Assert.IsNotNull(rtSpec0.MzPrecursorSpectrumItem);
        Assert.IsNull(rtSpec0.MzProductSpectrumItem);
        Assert.AreEqual("channel-guid-2",
            rtSpec0.MzPrecursorSpectrumItem.ProductsInfoes[0].ProductScanInfoes[0].ChannelId);

        // Second spectrum: centroid + product oneof.
        var rtSpec1 = ch.Values[1];
        Assert.AreEqual(MzSpectrumItemDtoV2.SubtypeOneofCase.MzProductSpectrumItem, rtSpec1.SubtypeCase);
        Assert.IsNull(rtSpec1.ContinuumSpectrumData);
        Assert.IsNotNull(rtSpec1.CentroidSpectrumData);
        CollectionAssert.AreEqual(new[] { 110.1, 220.2 }, rtSpec1.CentroidSpectrumData.MOverZs);
        Assert.IsNotNull(rtSpec1.MzProductSpectrumItem);
        Assert.IsNull(rtSpec1.MzPrecursorSpectrumItem);
        Assert.AreEqual("channel-guid-1", rtSpec1.MzProductSpectrumItem.PrecursorScanInfo.ChannelId);
    }

    private static T RoundTrip<T>(T value, MessageParser<T> parser) where T : IMessage<T>
    {
        using var ms = new MemoryStream();
        value.WriteTo(ms);
        ms.Position = 0;
        return parser.ParseFrom(ms);
    }
}
