using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Data.MsData;

/// <summary>
/// The per-item fetch shared by every writer's spectrum and chromatogram loop, so
/// <see cref="WriteConfig.ContinueOnError"/> means the same thing in every output format, and
/// the rewrite that corrects the item counts mzML and mzXML write before their items.
/// </summary>
internal static class ContinueOnErrorPolicy
{
    // Each pass after the first is a whole rewrite, so a reader that fails differently every
    // time it is asked must not be able to keep the write going indefinitely.
    private const int MAX_WRITE_PASSES = 3;

    /// <summary>
    /// Runs <paramref name="writePass"/>, which writes the whole output while leaving out the
    /// items it is given and returns the ones it skipped itself, until a pass skips nothing new.
    /// A format that writes its item counts before its items (mzML, mzXML) needs this to get them
    /// right after a skip. When <paramref name="canRewrite"/> is false, or the passes run out, the
    /// output keeps its overstated counts and a warning saying so goes to <paramref name="skipLog"/>.
    /// </summary>
    /// <param name="writePass">Writes the whole output once; see above.</param>
    /// <param name="canRewrite">Whether a second pass can replace what the first one wrote.</param>
    /// <param name="countsStillInclude">Completes the warning, naming the format's counts, e.g.
    /// "the msRun scanCount still includes".</param>
    /// <param name="skipLog">Where the warning goes.</param>
    public static void WriteUntilNothingNewIsSkipped(Func<SkippedItems, SkippedItems> writePass,
        bool canRewrite, string countsStillInclude, TextWriter skipLog)
    {
        var excluded = new SkippedItems();
        for (int pass = 1; ; pass++)
        {
            var skipped = writePass(excluded);
            if (skipped.Count == 0)
                return;
            if (!canRewrite)
            {
                skipLog.WriteLine($"Warning: the output cannot be rewritten, so {countsStillInclude} " +
                                  $"the {skipped.Count} item(s) skipped");
                return;
            }
            if (pass == MAX_WRITE_PASSES)
            {
                skipLog.WriteLine($"Warning: items were still failing after {pass} passes, so {countsStillInclude} " +
                                  $"the {skipped.Count} item(s) skipped in the last one");
                return;
            }
            excluded.Spectra.UnionWith(skipped.Spectra);
            excluded.Chromatograms.UnionWith(skipped.Chromatograms);
        }
    }

    /// <summary>
    /// <see cref="WriteUntilNothingNewIsSkipped"/> into a caller's stream: a rewrite rewinds to
    /// where the first pass started, which only a seekable stream allows.
    /// </summary>
    public static void WriteToStream(Stream stream, Func<Stream, SkippedItems, SkippedItems> writePass,
        bool canRewrite, string countsStillInclude, TextWriter skipLog)
    {
        canRewrite &= stream.CanSeek;
        long start = canRewrite ? stream.Position : 0;
        WriteUntilNothingNewIsSkipped(excluded =>
        {
            if (excluded.Count > 0)
            {
                stream.Position = start;
                stream.SetLength(start);
            }
            return writePass(stream, excluded);
        }, canRewrite, countsStillInclude, skipLog);
    }

    /// <summary>
    /// <see cref="WriteUntilNothingNewIsSkipped"/> into a fresh stream from
    /// <paramref name="openOutput"/> on every pass, so a rewrite also works for output that
    /// cannot seek, such as gzip.
    /// </summary>
    public static void WriteToFreshStreams(Func<Stream> openOutput, Func<Stream, SkippedItems, SkippedItems> writePass,
        bool canRewrite, string countsStillInclude, TextWriter skipLog)
    {
        WriteUntilNothingNewIsSkipped(excluded =>
        {
            using var stream = openOutput();
            return writePass(stream, excluded);
        }, canRewrite, countsStillInclude, skipLog);
    }

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

/// <summary>Indices of the spectra and chromatograms a write pass skipped, or that a rewrite
/// leaves out because an earlier pass skipped them.</summary>
internal sealed class SkippedItems
{
    public HashSet<int> Spectra { get; } = new();
    public HashSet<int> Chromatograms { get; } = new();
    public int Count => Spectra.Count + Chromatograms.Count;
}
