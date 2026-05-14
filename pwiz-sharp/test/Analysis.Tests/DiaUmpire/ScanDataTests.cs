using Pwiz.Analysis.DiaUmpire;

namespace Pwiz.Analysis.Tests.DiaUmpire;

/// <summary>Unit tests for <see cref="XYPointCollection"/>, <see cref="ScanData"/>, and <see cref="ScanCollection"/>.</summary>
[TestClass]
public class ScanDataTests
{
    private static XYPointCollection MakeAscending(params (float x, float y)[] points)
    {
        var c = new XYPointCollection();
        foreach (var (x, y) in points) c.AddPoint(x, y);
        return c;
    }

    [TestMethod]
    public void AddPoint_TracksMaxY()
    {
        var c = MakeAscending((1, 10), (2, 30), (3, 20));
        Assert.AreEqual(30f, c.MaxY);
        Assert.AreEqual(3, c.PointCount());
    }

    [TestMethod]
    public void GetClosestIndexOfX_FindsNearestPoint()
    {
        var c = MakeAscending((1, 1), (2, 1), (5, 1), (10, 1));
        // closer to 2 than 5
        Assert.AreEqual(1, c.GetClosestIndexOfX(2.9f));
        // closer to 5 than 2
        Assert.AreEqual(2, c.GetClosestIndexOfX(4.1f));
        // out-of-range clamps to bounds
        Assert.AreEqual(0, c.GetClosestIndexOfX(-1f));
        Assert.AreEqual(3, c.GetClosestIndexOfX(99f));
    }

    [TestMethod]
    public void GetLowerHigherIndexOfX_BracketsValue()
    {
        var c = MakeAscending((1, 1), (3, 1), (5, 1), (7, 1));
        // For 4: lower should be 3 (idx 1), higher should be 5 (idx 2)
        Assert.AreEqual(1, c.GetLowerIndexOfX(4));
        Assert.AreEqual(2, c.GetHigherIndexOfX(4));
        // Exact match — both point to that index
        Assert.AreEqual(2, c.GetLowerIndexOfX(5));
        Assert.AreEqual(2, c.GetHigherIndexOfX(5));
    }

    [TestMethod]
    public void GetSubSetByXRange_ClipsToInclusiveRange()
    {
        var c = MakeAscending((1, 10), (2, 20), (3, 30), (4, 40), (5, 50));
        var sub = c.GetSubSetByXRange(2, 4);
        Assert.AreEqual(3, sub.PointCount());
        Assert.AreEqual(2f, sub.Data[0].X);
        Assert.AreEqual(4f, sub.Data[2].X);
    }

    [TestMethod]
    public void AddPointKeepMaxIfCloseValueExisted_MaintainsMaxY()
    {
        var c = MakeAscending((100, 5));
        // 100.00001 is within 1 ppm of 100, so we hit the merge branch — MaxY should still update.
        c.AddPointKeepMaxIfCloseValueExisted(100.0001f, 10, 10);
        Assert.AreEqual(10f, c.MaxY);
    }

    [TestMethod]
    public void Centroiding_CollapsesNeighboringPeaks()
    {
        // Two tight peaks (within resolution gap) should collapse to the higher.
        var scan = new ScanData();
        scan.AddPoint(500.000f, 100);
        scan.AddPoint(500.001f, 200);
        scan.AddPoint(500.002f, 50);
        scan.AddPoint(600.000f, 80);
        scan.Centroiding(resolution: 50_000, minMz: 100);
        // After centroiding we expect the local maxima at ~500.001 and ~600.000.
        Assert.IsTrue(scan.PointCount() <= 2);
        Assert.IsTrue(scan.Data.Any(d => System.Math.Abs(d.X - 500.001f) < 0.01f));
    }

    [TestMethod]
    public void RemoveSignalBelowBG_DropsLowPeaks()
    {
        var s = new ScanData { Background = 15 };
        s.AddPoint(100, 10);
        s.AddPoint(200, 30);
        s.AddPoint(300, 5);
        s.AddPoint(400, 50);
        s.RemoveSignalBelowBG();
        Assert.AreEqual(2, s.PointCount());
        Assert.IsTrue(s.Data.All(p => p.Y > 15));
    }

    [TestMethod]
    public void GenerateTopPeakScanData_KeepsHighestN_FlipsXY()
    {
        // cpp flips x and y: TopPeakScan->AddPoint(peak.getY(), peak.getX())
        var s = new ScanData();
        s.AddPoint(100, 1);
        s.AddPoint(200, 5);
        s.AddPoint(300, 3);
        s.AddPoint(400, 10);
        s.GenerateTopPeakScanData(2);
        Assert.IsNotNull(s.TopPeakScan);
        Assert.AreEqual(2, s.TopPeakScan!.PointCount());
        // top peak was (400, 10); after swap = (10, 400)
        Assert.AreEqual(10f, s.TopPeakScan.Data[0].X);
        Assert.AreEqual(400f, s.TopPeakScan.Data[0].Y);
    }

    [TestMethod]
    public void TotIonCurrent_CachesAfterFirstCall()
    {
        var s = new ScanData();
        s.AddPoint(100, 10);
        s.AddPoint(200, 20);
        Assert.AreEqual(30f, s.TotIonCurrent());
        // remove a peak — cached value should NOT change because we didn't invalidate
        s.Background = 15;
        s.RemoveSignalBelowBG();
        Assert.AreEqual(30f, s.TotIonCurrent());
    }

    [TestMethod]
    public void ScanCollection_AddScan_TracksMS1MS2Counts()
    {
        var col = new ScanCollection(resolution: 17_000);
        col.AddScan(new ScanData { ScanNum = 1, MsLevel = 1, RetentionTime = 0.5f });
        col.AddScan(new ScanData { ScanNum = 2, MsLevel = 2, RetentionTime = 0.6f, PrecursorIntensity = 100 });
        col.AddScan(new ScanData { ScanNum = 3, MsLevel = 2, RetentionTime = 0.7f, PrecursorIntensity = 50 });
        Assert.AreEqual(3, col.Size());
        Assert.AreEqual(1, col.NumScanLevel1);
        Assert.AreEqual(2, col.NumScanLevel2);
        Assert.AreEqual(50f, col.MinPrecursorInt);
        Assert.AreEqual(2, col.GetScanNoArray(2).Count);
    }

    [TestMethod]
    public void ScanCollection_GetTIC_PointsAreInScanOrder()
    {
        var col = new ScanCollection();
        col.AddScan(new ScanData { ScanNum = 1, MsLevel = 1, RetentionTime = 0.1f });
        col.GetScan(1)!.AddPoint(100, 5);
        col.GetScan(1)!.AddPoint(200, 7);
        col.AddScan(new ScanData { ScanNum = 2, MsLevel = 1, RetentionTime = 0.2f });
        col.GetScan(2)!.AddPoint(300, 11);
        var tic = col.GetTIC();
        Assert.AreEqual(2, tic.PointCount());
        Assert.AreEqual(12f, tic.Data[0].Y);
        Assert.AreEqual(11f, tic.Data[1].Y);
    }

    [TestMethod]
    public void ScanCollection_GetParentMSScan_ReturnsPrecedingMS1()
    {
        var col = new ScanCollection();
        col.AddScan(new ScanData { ScanNum = 1, MsLevel = 1 });
        col.AddScan(new ScanData { ScanNum = 2, MsLevel = 2 });
        col.AddScan(new ScanData { ScanNum = 3, MsLevel = 2 });
        col.AddScan(new ScanData { ScanNum = 4, MsLevel = 1 });
        col.AddScan(new ScanData { ScanNum = 5, MsLevel = 2 });
        Assert.AreEqual(1, col.GetParentMSScan(3)!.ScanNum);
        Assert.AreEqual(4, col.GetParentMSScan(5)!.ScanNum);
        Assert.IsNull(col.GetParentMSScan(99));
    }
}
