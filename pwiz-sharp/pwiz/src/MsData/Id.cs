using System.Globalization;
using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.MsData.Spectra;

/// <summary>
/// Helpers for parsing and formatting native spectrum ids
/// (e.g. <c>"controllerType=0 controllerNumber=1 scan=123"</c>).
/// </summary>
/// <remarks>Port of pwiz::msdata::id namespace.</remarks>
public static class Id
{
    /// <summary>Parses an id string into a dictionary of name→value pairs.</summary>
    /// <remarks>
    /// Accepts whitespace-separated <c>name=value</c> tokens. Duplicate names keep the last value seen.
    /// </remarks>
    public static Dictionary<string, string> Parse(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in id.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = token.IndexOf('=');
            if (eq <= 0) continue;
            map[token[..eq]] = token[(eq + 1)..];
        }
        return map;
    }

    /// <summary>Extracts the value for a given name from an id string, or empty if absent.</summary>
    public static string Value(string id, string name)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(name);
        string needle = name + "=";
        int start = id.IndexOf(needle, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        start += needle.Length;
        int end = id.IndexOf(' ', start);
        return end < 0 ? id[start..] : id[start..end];
    }

    /// <summary>Extracts a named value and parses it as <typeparamref name="T"/>, returning default(T) when absent.</summary>
    public static T ValueAs<T>(string id, string name)
        where T : IParsable<T>
    {
        string raw = Value(id, name);
        if (string.IsNullOrEmpty(raw))
            return default!;
        return T.Parse(raw, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Abbreviates an id by dropping the names: <c>"sample=1 period=1 cycle=123"</c> → <c>"1.1.123"</c>.
    /// </summary>
    public static string Abbreviate(string id, char delimiter = '.')
    {
        ArgumentNullException.ThrowIfNull(id);
        var tokens = id.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var parts = new string[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            int eq = tokens[i].IndexOf('=');
            parts[i] = eq >= 0 ? tokens[i][(eq + 1)..] : tokens[i];
        }
        return string.Join(delimiter, parts);
    }

    /// <summary>
    /// Translates a plain integer "scan number" into a native id string matching the given <paramref name="nativeIdFormat"/>.
    /// Returns empty when the format isn't translatable.
    /// </summary>
    public static string TranslateScanNumberToNativeId(CVID nativeIdFormat, string scanNumber)
    {
        if (string.IsNullOrEmpty(scanNumber)) return string.Empty;
        return nativeIdFormat switch
        {
            CVID.MS_Thermo_nativeID_format => $"controllerType=0 controllerNumber=1 scan={scanNumber}",
            CVID.MS_Bruker_Agilent_YEP_nativeID_format => $"scan={scanNumber}",
            CVID.MS_Bruker_BAF_nativeID_format => $"scan={scanNumber}",
            CVID.MS_scan_number_only_nativeID_format => $"scan={scanNumber}",
            CVID.MS_multiple_peak_list_nativeID_format => $"index={scanNumber}",
            CVID.MS_single_peak_list_nativeID_format => $"index={scanNumber}",
            CVID.MS_spectrum_identifier_nativeID_format => $"spectrum={scanNumber}",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Extracts a simple integer "scan number" from a native id for the given <paramref name="nativeIdFormat"/>.
    /// Returns empty when the format isn't translatable.
    /// </summary>
    public static string TranslateNativeIdToScanNumber(CVID nativeIdFormat, string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return nativeIdFormat switch
        {
            CVID.MS_Thermo_nativeID_format => Value(id, "scan"),
            CVID.MS_Bruker_Agilent_YEP_nativeID_format => Value(id, "scan"),
            CVID.MS_Bruker_BAF_nativeID_format => Value(id, "scan"),
            CVID.MS_scan_number_only_nativeID_format => Value(id, "scan"),
            CVID.MS_multiple_peak_list_nativeID_format => Value(id, "index"),
            CVID.MS_single_peak_list_nativeID_format => Value(id, "index"),
            CVID.MS_spectrum_identifier_nativeID_format => Value(id, "spectrum"),
            _ => string.Empty,
        };
    }
}
