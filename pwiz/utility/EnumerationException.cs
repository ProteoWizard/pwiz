namespace Pwiz.Util.Misc;

/// <summary>
/// Thrown by mzML / mzMLb writers when fetching the next spectrum or chromatogram
/// from its list (e.g. vendor SDK failure during <c>SpectrumList.GetSpectrum</c>)
/// fails and <c>WriteConfig.ContinueOnError</c> is <c>false</c>. Lets msconvert
/// distinguish per-item enumeration errors — which the user can retry with
/// <c>--continueOnError</c> to skip — from other write-time errors (I/O,
/// permissions, unsupported format, etc.) where that hint wouldn't help.
/// Port of <c>pwiz::util::enumeration_error</c>
/// (<c>pwiz/utility/misc/Exception.hpp</c>). Renamed from <c>EnumerationError</c>
/// to satisfy CA1710's "Exception suffix" rule; the cpp name uses <c>_error</c>
/// for the same role.
/// </summary>
public sealed class EnumerationException : System.Exception
{
    /// <summary>Creates the exception with the given message.</summary>
    public EnumerationException(string message) : base(message) { }

    /// <summary>Creates the exception with a message and an inner cause (the original
    /// SDK / I/O exception). Use this when wrapping a per-item failure so callers
    /// can still inspect the underlying type via <see cref="System.Exception.InnerException"/>.</summary>
    public EnumerationException(string message, System.Exception innerException)
        : base(message, innerException) { }
}
