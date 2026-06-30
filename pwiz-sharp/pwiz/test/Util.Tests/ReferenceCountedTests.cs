using Pwiz.Util.Misc;

namespace Pwiz.Util.Tests.Misc;

/// <summary>
/// Tests for <see cref="ReferenceCounted{T}"/> — refcounted shared-ownership semantics
/// (mirrors std::shared_ptr behavior). The wrapped value is disposed exactly once, when
/// the last share is dropped.
/// </summary>
[TestClass]
public class ReferenceCountedTests
{
    /// <summary>Trivial disposable that records the number of Dispose calls so tests can
    /// assert "wrapped value disposed exactly once when the last share dropped".</summary>
    private sealed class Counter : IDisposable
    {
        public int DisposeCalls { get; private set; }
        public void Dispose() => DisposeCalls++;
    }

    [TestMethod]
    public void Construct_ThenDispose_RunsInnerDisposeOnce()
    {
        var inner = new Counter();
        var first = new ReferenceCounted<Counter>(inner);

        // Sanity: the underlying value is reachable.
        Assert.AreSame(inner, first.Value);
        Assert.AreEqual(0, inner.DisposeCalls, "ctor must not dispose");

        first.Dispose();
        Assert.AreEqual(1, inner.DisposeCalls, "last share dropped → inner disposed once");

        // Idempotent on this share.
        first.Dispose();
        Assert.AreEqual(1, inner.DisposeCalls, "double Dispose stays at one");
    }

    [TestMethod]
    public void AddRef_ParallelShares_DisposeIndependently()
    {
        // The architectural goal: parallel shares on the same inner. Disposing one share
        // must NOT invalidate the other; the inner is freed only when both have been disposed.
        var inner = new Counter();
        var a = new ReferenceCounted<Counter>(inner);
        var b = a.AddRef();

        a.Dispose();
        Assert.AreEqual(0, inner.DisposeCalls, "share a dispose: inner still alive (b holds it)");

        // b.Value remains usable
        Assert.AreSame(inner, b.Value);

        b.Dispose();
        Assert.AreEqual(1, inner.DisposeCalls, "last share gone, inner finally disposed");
    }

    [TestMethod]
    public void AddRef_OrderIndependent()
    {
        // Disposing in arbitrary order should give the same end state.
        var inner = new Counter();
        var a = new ReferenceCounted<Counter>(inner);
        var b = a.AddRef();
        var c = a.AddRef();

        c.Dispose();
        b.Dispose();
        Assert.AreEqual(0, inner.DisposeCalls, "primary share still alive");

        a.Dispose();
        Assert.AreEqual(1, inner.DisposeCalls);
    }

    [TestMethod]
    public void AddRef_AfterAllDisposed_Throws()
    {
        // Once the refcount hits zero, the wrapped Value has been disposed. AddRef on the
        // dead handle would attempt to resurrect it and silently double-dispose later;
        // refuse the call so misuse surfaces immediately.
        var inner = new Counter();
        var a = new ReferenceCounted<Counter>(inner);
        a.Dispose();
        Assert.AreEqual(1, inner.DisposeCalls);

        Assert.ThrowsException<ObjectDisposedException>(() => a.AddRef());
        Assert.AreEqual(1, inner.DisposeCalls, "AddRef-after-disposed must not trigger any disposal");
    }

    [TestMethod]
    public void Construct_NullValue_Throws()
    {
        // Null-guard — wrapping nothing isn't a valid state.
        Assert.ThrowsException<ArgumentNullException>(() =>
            new ReferenceCounted<Counter>(null!));
    }
}
