using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Data.MsData;

/// <summary>
/// The per-spectrum fetch shared by every writer's spectrum loop, so <see cref="WriteConfig.ContinueOnError"/>
/// means the same thing in every output format.
/// </summary>
internal static class ContinueOnErrorPolicy
{
    /// <summary>
    /// Reads spectrum <paramref name="index"/> with its binary data. On failure, when
    /// <paramref name="continueOnError"/> is true, logs the skip to <paramref name="skipLog"/> and
    /// returns null; otherwise wraps the failure in <see cref="EnumerationException"/>, which is
    /// what lets msconvert suggest re-running with <c>--continueOnError</c>.
    /// </summary>
    /// <remarks>
    /// Only the fetch is guarded; a failure while writing a spectrum that was read is a writer bug
    /// and still aborts. Catches <see cref="Exception"/> because vendor SDKs throw arbitrary types
    /// with no shared base, the same reason cpp catches <c>std::exception&amp;</c>.
    /// </remarks>
    public static Spectrum? GetSpectrumOrSkip(ISpectrumList list, int index, bool continueOnError, TextWriter skipLog)
    {
        try
        {
            return list.GetSpectrum(index, getBinaryData: true);
        }
        catch (Exception ex)
        {
            if (!continueOnError)
                throw new EnumerationException(ex.Message, ex);

            string id;
            try { id = list.SpectrumIdentity(index).Id; }
            catch { id = "?"; }
            skipLog.WriteLine($"Skipping spectrum {index} \"{id}\": {ex.Message}");
            return null;
        }
    }
}
