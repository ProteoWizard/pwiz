using Pwiz.Util.Misc;

namespace Pwiz.Util.Tests.Misc;

[TestClass]
public class IterationListenerTests
{
    private sealed class RecordingListener : IIterationListener
    {
        public List<IterationUpdate> Updates { get; } = new();
        public IterationStatus Result { get; set; } = IterationStatus.Ok;

        public IterationStatus Update(IterationUpdate message)
        {
            Updates.Add(message);
            return Result;
        }
    }

    [TestMethod]
    public void IterationUpdate_Clamps_IndexToCountMinusOne()
    {
        var u = new IterationUpdate(100, 10);
        Assert.AreEqual(9, u.IterationIndex);
    }

    [TestMethod]
    public void IterationUpdate_UnknownCount_AllowsAnyIndex()
    {
        var u = new IterationUpdate(1000, 0);
        Assert.AreEqual(1000, u.IterationIndex);
    }

    [TestMethod]
    public void AddListener_BroadcastsOnMatchingPeriod()
    {
        var reg = new IterationListenerRegistry();
        var listener = new RecordingListener();
        reg.AddListener(listener, iterationPeriod: 5);

        for (int i = 0; i < 20; i++)
            reg.Broadcast(new IterationUpdate(i, 20));

        // i mod 5 == 0 → indices 0, 5, 10, 15. Plus i == 19 (last index: count-1) always fires.
        CollectionAssert.AreEqual(
            new[] { 0, 5, 10, 15, 19 },
            listener.Updates.ConvertAll(u => u.IterationIndex));
    }

    [TestMethod]
    public void AddListener_AlwaysDeliversLastUpdate()
    {
        var reg = new IterationListenerRegistry();
        var listener = new RecordingListener();
        reg.AddListener(listener, iterationPeriod: 100);

        reg.Broadcast(new IterationUpdate(9, 10)); // last index, count=10

        Assert.AreEqual(1, listener.Updates.Count);
        Assert.AreEqual(9, listener.Updates[0].IterationIndex);
    }

    [TestMethod]
    public void Broadcast_CancelRequest_Propagates()
    {
        var reg = new IterationListenerRegistry();
        var listener = new RecordingListener { Result = IterationStatus.Cancel };
        reg.AddListener(listener, 1);

        var status = reg.Broadcast(new IterationUpdate(0, 10));
        Assert.AreEqual(IterationStatus.Cancel, status);
    }

    [TestMethod]
    public void RemoveListener_StopsDelivery()
    {
        var reg = new IterationListenerRegistry();
        var listener = new RecordingListener();
        reg.AddListener(listener, 1);
        reg.RemoveListener(listener);

        reg.Broadcast(new IterationUpdate(0, 10));
        Assert.AreEqual(0, listener.Updates.Count);
    }

    [TestMethod]
    public void AddListener_ZeroOrNegativePeriod_Throws()
    {
        var reg = new IterationListenerRegistry();
        var listener = new RecordingListener();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => reg.AddListener(listener, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => reg.AddListener(listener, -1));
    }
}
