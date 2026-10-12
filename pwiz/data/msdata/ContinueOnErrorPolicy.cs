using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Data.MsData;

/// <summary>
/// The per-item fetch shared by every writer's spectrum and chromatogram loop, so
/// <see cref="WriteConfig.ContinueOnError"/> means the same thing in every output format.
/// </summary>
internal static class ContinueOnErrorPolicy
{
    /// <summary>
    /// Reads spectrum <paramref name="index"/> with its binary data. On failure, when
    /// <paramref name="continueOnError"/> is true, logs the skip to <paramref name="skipLog"/> and
    /// returns null; otherwise wraps the failure in <see cref="EnumerationException"/>, which is
    /// what lets msconvert suggest re-running with <c>--continueOnError</c>.
    /// </summary>
    public static Spectrum? GetSpectrumOrSkip(ISpectrumList list, int index, bool continueOnError, TextWriter skipLog) =>
        FetchOrSkip(() => list.GetSpectrum(index, getBinaryData: true),
            "spectrum", index, () => list.SpectrumIdentity(index).Id, continueOnError, skipLog);

    /// <summary>Chromatogram counterpart of <see cref="GetSpectrumOrSkip"/>.</summary>
    public static Chromatogram? GetChromatogramOrSkip(IChromatogramList list, int index, bool continueOnError, TextWriter skipLog) =>
        FetchOrSkip(() => list.GetChromatogram(index, getBinaryData: true),
            "chromatogram", index, () => list.ChromatogramIdentity(index).Id, continueOnError, skipLog);

    /// <remarks>
    /// Only the fetch is guarded; a failure while writing an item that was read is a writer bug
    /// and still aborts. Catches <see cref="Exception"/> because vendor SDKs throw arbitrary types
    /// with no shared base, the same reason cpp catches <c>std::exception&amp;</c>. Cancellation is
    /// the exception: it is not a bad item, so it propagates unwrapped rather than being skipped
    /// or reported as something <c>--continueOnError</c> could get past.
    /// </remarks>
    private static T? FetchOrSkip<T>(Func<T> fetch, string kind, int index, Func<string> getId,
        bool continueOnError, TextWriter skipLog) where T : class
    {
        try
        {
            return fetch();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!continueOnError)
                throw new EnumerationException(ex.Message, ex);

            string id;
            try { id = getId(); }
            catch { id = "?"; }
            skipLog.WriteLine($"Skipping {kind} {index} \"{id}\": {ex.Message}");
            return null;
        }
    }
}
