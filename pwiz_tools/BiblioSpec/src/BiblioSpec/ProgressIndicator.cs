// Port of pwiz_tools/BiblioSpec/src/ProgressIndicator.h
//
// Tiny percentage-based progress reporter used by BuildParser to surface long-running
// import progress. The cpp original is header-only (~80 LOC) and prints to stderr; we
// keep the same surface but route output via System.Console.Error so log redirection
// in tests works the same way it does for Verbosity.

using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Reports percentage progress as work items are processed. Nested indicators count
/// their work as a fraction of the parent's range; this lets BuildParser report
/// "reading file 3 of 17, 42% through that file" as a single monotonic percentage.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::ProgressIndicator</c> (ProgressIndicator.h:34). The cpp
/// version uses <c>boost::int64_t</c> internally; C# uses <see cref="long"/>. Throttled
/// output (at most one line per wall-clock second) is preserved.</para>
/// <para>cpp parity: the indicator never emits 100% from <see cref="Add"/> /
/// <see cref="Increment"/>; it only prints the final "100%" line from the dtor /
/// <see cref="Finish"/>. Nested indicators artificially inflate their total by one so
/// they NEVER print 100% — only the outermost parent does.</para>
/// </remarks>
public sealed class ProgressIndicator
{
    private readonly long _total;
    private long _current;
    private int _percent;
    private DateTime _lastOutput;

    /// <summary>Construct an indicator that counts up to <paramref name="total"/> units.</summary>
    /// <remarks>cpp ProgressIndicator.h:37 — assumes a header message was just output, so
    /// the throttle clock starts "now".</remarks>
    public ProgressIndicator(long total)
    {
        _total = total <= 0 ? 1 : total; // cpp parity: guard against divide-by-zero
        _current = 0;
        _percent = 0;
        _lastOutput = DateTime.UtcNow;
    }

    /// <summary>Total units this indicator will count up to.</summary>
    public long Total => _total;

    /// <summary>Units currently processed.</summary>
    public long Processed => _current;

    /// <summary>
    /// Create a child indicator that counts from <c>current/total</c> to
    /// <c>(current+1)/total</c> of this parent. The parent's <see cref="Processed"/>
    /// is left unchanged so the parent can be incremented separately when the child
    /// finishes.
    /// </summary>
    /// <remarks>
    /// cpp parity: ProgressIndicator.h:59. Adds one to the inner total so the inner
    /// indicator NEVER reports 100%; only the outermost parent will.
    /// </remarks>
    public ProgressIndicator NewNestedIndicator(long innerTotal)
    {
        innerTotal++;
        var inner = new ProgressIndicator(innerTotal * _total);
        inner.Add(Math.Max(0L, _current - 1) * innerTotal);
        return inner;
    }

    /// <summary>Add 1 unit of progress.</summary>
    public void Increment() => Add(1);

    /// <summary>Add <paramref name="n"/> units of progress and emit a percentage line if it has changed.</summary>
    /// <remarks>cpp ProgressIndicator.h:73. Output is throttled to at most one line per
    /// wall-clock second to avoid spamming on tight loops.</remarks>
    public void Add(long n)
    {
        _current += n;
        // cpp parity: this function never outputs 100%.
        var percentCurrent = (int)Math.Min(99L, 100L * Math.Max(0L, _current - 1) / _total);
        if (percentCurrent != _percent)
        {
            _percent = percentCurrent;
            var now = DateTime.UtcNow;
            // cpp parity: only print if more than 1 second has elapsed since last output.
            if ((now - _lastOutput).TotalSeconds >= 1)
            {
                Console.Error.WriteLine(_percent.ToString(CultureInfo.InvariantCulture) + "%");
                _lastOutput = now;
            }
        }
    }

    /// <summary>
    /// Set processed = total so future increments don't push past the end. cpp uses this
    /// when the underlying work finishes ahead of the expected count.
    /// </summary>
    /// <remarks>cpp parity: ProgressIndicator.h:99 <c>finish()</c>.</remarks>
    public void Finish()
    {
        _current = _total;
    }

    /// <summary>
    /// Emit the trailing "100%" line if this indicator counted all the way up to
    /// <see cref="Total"/>. cpp emits this from its destructor; in C# we expose it as
    /// an explicit call because there's no analog for the cpp dtor.
    /// </summary>
    /// <remarks>cpp parity: ProgressIndicator.h:47 dtor body.</remarks>
    public void Complete()
    {
        if (_current == _total)
        {
            Console.Error.WriteLine("100%");
        }
    }
}
