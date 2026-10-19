using System.Globalization;
using System.Runtime.Versioning;

// Compiled only when $(NativeVendorsAvailable) is true (Windows + vendor licenses) - see
// Bruker.csproj. The [SupportedOSPlatform] annotations stay for documentation.
#pragma warning disable CA1416

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Read-only view over a CompassXtract spectrum's <c>MSSpectrumParameterCollection</c>. Port of
/// <c>MSSpectrumParameterListImpl</c> + <c>MSSpectrumParameterIterator::Impl</c> in
/// <c>CompassData.cpp:140-190</c>.
/// </summary>
/// <remarks>
/// The COM collection is <b>1-based</b> (<c>CompassData.cpp:161</c> stores <c>index+1</c> and
/// <c>CompassData.cpp:184</c> range-checks against <c>[1, Count]</c>); this wrapper exposes the
/// 0-based indexing the rest of pwiz-sharp uses and adds the +1 internally. Every accessor is a
/// COM round trip, which is exactly why <see cref="CompassXtractParameterCache"/> exists.
/// Callers must already hold a <see cref="CompassXtractActivationContext.Activate"/> scope.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class CompassXtractParameterList
{
    private readonly EDAL.IMSSpectrumParameterCollection _collection;

    internal CompassXtractParameterList(EDAL.IMSSpectrumParameterCollection collection) =>
        _collection = collection;

    /// <summary>Number of parameters on the spectrum, as COM reports it. Valid indices for
    /// <see cref="this[int]"/> are <c>0 .. Count-1</c>.</summary>
    public int Count => _collection.Count;

    /// <summary>
    /// Number of parameters an <i>enumeration</i> yields, which is one fewer than
    /// <see cref="Count"/>: the last parameter is never enumerated.
    /// </summary>
    /// <remarks>
    /// <para>This is not a defensive margin, it is cpp's behavior and it changes output.
    /// <c>MSSpectrumParameterIterator::equal</c> (<c>CompassData.cpp:225-239</c>) ends an
    /// iteration when <c>index_ &gt;= parameterCollection_-&gt;Count</c>, and <c>index_</c> is
    /// one-based (<c>:161</c>), so the element at <c>index_ == Count</c> — the last one — compares
    /// equal to <c>end()</c> and is skipped. Everything that walks the list in cpp inherits it:
    /// <c>ParameterCache::update</c> (<c>:110</c>) and <c>createInstrumentConfigurations</c>
    /// (<c>Reader_Bruker_Detail.cpp:146</c>). Random access does not — <c>ParameterCache::get</c>
    /// builds an iterator directly at the wanted index and dereferences it without ever comparing
    /// against <c>end()</c> — hence the two different counts here.</para>
    /// <para>It matters in practice. On the <c>Sample_1-A,1_01_985.d</c> YEP fixture the MS2
    /// parameter list has 154 entries and <c>MS(n) Isol Width = 4</c> is the last of them, so cpp
    /// never indexes it, <c>getIsolationWidth()</c> returns 0, and no
    /// <c>isolation window lower/upper offset</c> is emitted. Enumerating all 154 makes this
    /// reader emit offsets of 2.0 that neither the reference mzML nor a current msconvert
    /// produces.</para>
    /// </remarks>
    public int EnumeratedCount => Math.Max(0, Count - 1);

    /// <summary>Group / name / value of the parameter at <paramref name="index"/> (0-based).</summary>
    public (string Group, string Name, string Value) this[int index]
    {
        get
        {
            var p = _collection[index + 1];
            return (p.GroupName ?? string.Empty,
                    p.ParameterName ?? string.Empty,
                    // ParameterValue is a VARIANT (typed `object` by tlbimp); cpp calls
                    // ->ToString() on the boxed value (CompassData.cpp:188).
                    Convert.ToString(p.ParameterValue, CultureInfo.InvariantCulture) ?? string.Empty);
        }
    }
}

/// <summary>
/// Name-to-position cache over a spectrum's parameter list, with Bruker's alternative-name
/// aliasing. Port of <c>ParameterCache</c> in <c>CompassData.cpp:55-123</c>.
/// </summary>
/// <remarks>
/// One instance per MS level (<c>CompassData.cpp:689</c> keys the map by
/// <c>MSMSStage</c>): spectra acquired at the same stage share a parameter layout, so the
/// position found for one spectrum is reused for the next and only the single indexed COM
/// access is paid per query. When the layout does turn out to differ, the whole table is
/// rebuilt and the query retried — same self-healing as cpp.
/// </remarks>
internal sealed class CompassXtractParameterCache
{
    /// <summary>
    /// Verbatim copy of <c>parameterAlternativeNames</c> (<c>CompassData.cpp:46-50</c>):
    /// <c>canonical:alias;alias;…</c>, split on <c>:</c> and <c>;</c> alike.
    /// </summary>
    private static readonly string[] AlternativeNames =
    {
        "IsolationWidth:MS(n) Isol Width;Isolation Resolution FWHM",
        "ChargeState:Trigger Charge MS(2);Trigger Charge MS(3);Trigger Charge MS(4);Trigger Charge MS(5);Precursor Charge State",
    };

    private readonly Dictionary<string, int> _indexByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _alternativeNameMap = new(StringComparer.Ordinal);

    /// <summary>
    /// Value of <paramref name="parameterName"/> on <paramref name="parameters"/>, or the empty
    /// string when the spectrum has no such parameter (nor any of its aliases).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public string Get(string parameterName, CompassXtractParameterList parameters) =>
        Get(parameterName, parameters, depth: 0);

    [SupportedOSPlatform("windows")]
    private string Get(string parameterName, CompassXtractParameterList parameters, int depth)
    {
        if (!_indexByName.TryGetValue(parameterName, out int index))
        {
            Update(parameters);

            // if still not found, return empty string (CompassData.cpp:77-79)
            if (!_indexByName.TryGetValue(parameterName, out index))
                return string.Empty;
        }

        // cpp indexes straight into the collection here and would read out of range if the
        // cached position outlived a shorter parameter list; rebuild instead.
        if (index < 0 || index >= parameters.Count)
            return Rebuild(parameterName, parameters, depth);

        var parameter = parameters[index];
        if (!string.Equals(parameter.Name, parameterName, StringComparison.Ordinal)
            && !_alternativeNameMap.ContainsKey(parameter.Name))
        {
            // if parameter name doesn't match, invalidate the cache and try again
            // (CompassData.cpp:85-90)
            return Rebuild(parameterName, parameters, depth);
        }

        return parameter.Value;
    }

    [SupportedOSPlatform("windows")]
    private string Rebuild(string parameterName, CompassXtractParameterList parameters, int depth)
    {
        // After one Update the table maps parameterName either to a parameter of that exact name
        // or to one of its registered aliases, so a single retry always terminates. cpp recurses
        // without a bound; the cap only differs from cpp in a case cpp would loop forever.
        if (depth >= 1) return string.Empty;
        Update(parameters);
        return Get(parameterName, parameters, depth + 1);
    }

    /// <summary>Rebuilds the name-to-position table. Port of <c>ParameterCache::update</c>.</summary>
    [SupportedOSPlatform("windows")]
    private void Update(CompassXtractParameterList parameters)
    {
        _indexByName.Clear();
        _alternativeNameMap.Clear();

        foreach (string spec in AlternativeNames)
        {
            string[] tokens = spec.Split(':', ';');
            for (int j = 1; j < tokens.Length; j++)
                _alternativeNameMap[tokens[j]] = tokens[0];
        }

        // Enumeration, not random access - so it stops one short of Count. See EnumeratedCount.
        int count = parameters.EnumeratedCount;
        for (int i = 0; i < count; i++)
        {
            string name = parameters[i].Name;
            // Last occurrence wins, as with cpp's std::map operator[] assignment.
            if (_alternativeNameMap.TryGetValue(name, out string? canonical))
                _indexByName[canonical] = i;
            else
                _indexByName[name] = i;
        }
    }
}
