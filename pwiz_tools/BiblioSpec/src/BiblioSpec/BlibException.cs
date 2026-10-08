// Port of pwiz_tools/BiblioSpec/src/BlibException.h

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// A general exception class for BiblioSpec programs. Carries the message plus a
/// <see cref="HasFilename"/> flag so the catcher can decide what to add to the string.
/// </summary>
/// <remarks>
/// Port of <c>BiblioSpec::BlibException</c>. The cpp version exposes printf/boost::format
/// style variadic constructors; in C# call sites should format the message themselves
/// (e.g. <c>$"..."</c> or <see cref="string.Format(System.IFormatProvider, string, object?[])"/>)
/// before constructing the exception.
/// </remarks>
public class BlibException : Exception
{
    private bool _hasFilename;

    /// <summary>Constructs a BlibException with the default message and no filename flag.</summary>
    public BlibException()
        : base("BiblioSpec exception thrown.")
    {
    }

    /// <summary>Constructs a BlibException carrying a pre-formatted message.</summary>
    /// <param name="message">The exception message.</param>
    public BlibException(string message)
        : base(message)
    {
    }

    /// <summary>Constructs a BlibException carrying a pre-formatted message and a hasFilename flag.</summary>
    /// <param name="hasFilename">True if the catcher should prepend a filename to the message.</param>
    /// <param name="message">The exception message.</param>
    public BlibException(bool hasFilename, string message)
        : base(message)
    {
        _hasFilename = hasFilename;
    }

    /// <summary>Constructs a BlibException with a message and inner exception.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The wrapped exception.</param>
    public BlibException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>True if the catcher should prepend a filename to the exception message.</summary>
    public virtual bool HasFilename
    {
        get => _hasFilename;
        set => _hasFilename = value;
    }

    /// <summary>
    /// True if this exception was raised by <see cref="Verbosity.Error(string)"/>, which
    /// already wrote the message to stderr. The CLI <c>Program.Main</c> uses this to avoid
    /// double-printing the error.
    /// </summary>
    /// <remarks>
    /// cpp parity workaround: cpp <c>Verbosity::log(V_ERROR, ...)</c> calls <c>exit(1)</c>
    /// directly, so cpp's main never sees the exception. In C# the exception bubbles up and
    /// would be re-printed by the top-level handler without this flag.
    /// </remarks>
    public bool AlreadyLogged { get; set; }

    /// <summary>Appends the given message to this exception's message.</summary>
    /// <remarks>
    /// cpp BlibException.h:84 — <c>addMessage</c> uses boost::format; in C# the caller is
    /// expected to pass a pre-formatted string.
    /// </remarks>
    /// <param name="message">Pre-formatted string to append to the existing message.</param>
    public virtual void AddMessage(string message)
    {
        // Exception.Message is normally read-only; expose a virtual override that mirrors
        // the appended text. Subclasses can choose to compose differently if needed.
        _appendedMessage = (_appendedMessage ?? string.Empty) + message;
    }

    private string? _appendedMessage;

    /// <inheritdoc/>
    public override string Message =>
        _appendedMessage is null ? base.Message : base.Message + _appendedMessage;
}
