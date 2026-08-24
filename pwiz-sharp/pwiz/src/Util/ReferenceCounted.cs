namespace Pwiz.Util.Misc;

/// <summary>
/// Refcounted shared-ownership wrapper for any <see cref="IDisposable"/> resource. Mirrors
/// <c>std::shared_ptr&lt;T&gt;</c>: each <see cref="ReferenceCounted{T}"/> instance is one
/// share; <see cref="AddRef"/> hands out an independent share on the same underlying object;
/// the wrapped value is disposed exactly once, when the LAST share is dropped.
/// </summary>
/// <remarks>
/// <para>The intended use is for vendor-backed lists in pwiz-sharp where multiple downstream
/// wrappers (peak-pickers, filters, parallel views) need to share a single
/// file-handle-owning inner without one wrapper's <see cref="System.IDisposable.Dispose"/>
/// invalidating the others — but the type is fully generic, so any disposable resource
/// works the same way.</para>
/// <para>Usage:</para>
/// <code>
/// // 1. Construct: refcount = 1, this share is owned by the caller.
/// var primary = new ReferenceCounted&lt;ISpectrumList&gt;(thermoList);
///
/// // 2. Hand out additional shares. Each is independently disposable.
/// var second = primary.AddRef();
///
/// // 3. Each Dispose drops one share. Underlying object disposed only when last share goes.
/// primary.Dispose();   // refcount 1 → 0? No: still 1 (second holds it).
/// second.Dispose();    // refcount 1 → 0 → thermoList.Dispose() runs exactly once.
/// </code>
/// <para>Thread-safe: refcount uses <see cref="System.Threading.Interlocked"/>.</para>
/// </remarks>
public sealed class ReferenceCounted<T> : IDisposable where T : class, IDisposable
{
    private readonly RefCount _state;
    private bool _disposed;

    /// <summary>The wrapped resource. Stays valid until the last share has been disposed.</summary>
    public T Value { get; }

    /// <summary>Creates the first share of <paramref name="value"/> (refcount = 1).</summary>
    public ReferenceCounted(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
        _state = new RefCount();
    }

    private ReferenceCounted(ReferenceCounted<T> other)
    {
        Value = other.Value;
        _state = other._state;
        _state.Acquire();
    }

    /// <summary>Returns an independent share on the same underlying value. Each share must
    /// be disposed once; the wrapped <see cref="Value"/> is disposed when the last share goes.</summary>
    public ReferenceCounted<T> AddRef() => new(this);

    /// <summary>Drops this share. Disposes the wrapped <see cref="Value"/> when the last
    /// share is released. Idempotent — extra Dispose calls on the same instance are no-ops.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_state.Release() == 0)
            Value.Dispose();
    }

    /// <summary>The mutable counter shared between all sibling <see cref="ReferenceCounted{T}"/>
    /// handles of the same underlying value. Lives in its own type so each handle holds the
    /// SAME state by reference.</summary>
    private sealed class RefCount
    {
        private int _count = 1;
        public void Acquire()
        {
            // Increment-and-test-zero: if we go from 0 → 1, the underlying Value has already
            // been disposed. Adding a share at that point would resurrect a dead resource;
            // refuse the call to make the misuse loud rather than silently double-disposing.
            int incremented = System.Threading.Interlocked.Increment(ref _count);
            if (incremented == 1)
            {
                System.Threading.Interlocked.Decrement(ref _count);
                throw new ObjectDisposedException(nameof(ReferenceCounted<T>),
                    "Cannot AddRef a ReferenceCounted whose last share has already been disposed.");
            }
        }
        public int Release() => System.Threading.Interlocked.Decrement(ref _count);
    }
}
