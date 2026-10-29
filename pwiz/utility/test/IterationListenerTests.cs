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
    public void IterationUpdate_IndexClamping()
    {
        // Known count: index >= count is clamped to count-1.
        Assert.AreEqual(9, new IterationUpdate(100, 10).IterationIndex);
        // Unknown count (0): no clamping, index is returned as-is.
        Assert.AreEqual(1000, new IterationUpdate(1000, 0).IterationIndex);
    }

    [TestMethod]
    public void Broadcast_PeriodFiltering_AndLastUpdateAlways()
    {
        // period=5 should fire on indices divisible by 5; the last update (i == count-1)
        // always fires regardless of period.
        var reg = new IterationListenerRegistry();
        var listener = new RecordingListener();
        reg.AddListener(listener, iterationPeriod: 5);
        for (int i = 0; i < 20; i++) reg.Broadcast(new IterationUpdate(i, 20));
        CollectionAssert.AreEqual(
            new[] { 0, 5, 10, 15, 19 },
            listener.Updates.ConvertAll(u => u.IterationIndex),
            "period=5 + last-index always");

        // Standalone last-update test: period=100 ignores everything but the count-1 index.
        var regLast = new IterationListenerRegistry();
        var lastListener = new RecordingListener();
        regLast.AddListener(lastListener, iterationPeriod: 100);
        regLast.Broadcast(new IterationUpdate(9, 10));
        Assert.AreEqual(1, lastListener.Updates.Count, "last-update fires past period");
        Assert.AreEqual(9, lastListener.Updates[0].IterationIndex);
    }

    [TestMethod]
    public void Listener_Lifecycle_CancelRemoveAndPeriodValidation()
    {
        var reg = new IterationListenerRegistry();

        // Cancel result from a listener bubbles up as the Broadcast return.
        var canceler = new RecordingListener { Result = IterationStatus.Cancel };
        reg.AddListener(canceler, 1);
        Assert.AreEqual(IterationStatus.Cancel, reg.Broadcast(new IterationUpdate(0, 10)));

        // RemoveListener stops further deliveries to that listener.
        var removable = new RecordingListener();
        reg.AddListener(removable, 1);
        reg.RemoveListener(removable);
        reg.Broadcast(new IterationUpdate(1, 10));
        Assert.AreEqual(0, removable.Updates.Count, "removed listener should not receive");

        // AddListener rejects zero / negative periods up-front.
        var validation = new IterationListenerRegistry();
        var listener = new RecordingListener();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => validation.AddListener(listener, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => validation.AddListener(listener, -1));
    }
}
