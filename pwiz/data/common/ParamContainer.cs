using System.Globalization;
using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.Common.Params;

/// <summary>
/// Base class for elements that may hold CVParams, UserParams, and references to ParamGroups.
/// Port of pwiz/data::ParamContainer.
/// </summary>
public class ParamContainer : IEquatable<ParamContainer>
{
    /// <summary>
    /// Returns this container itself. Lets cpp/CLI-shaped client code that goes through
    /// <c>x.Params.CvParam(...)</c> work uniformly across types — Spectrum and Chromatogram
    /// hold a separate <c>Params</c> <see cref="ParamContainer"/> while Precursor / SelectedIon /
    /// Scan / IsolationWindow / ScanWindow / ScanList / ProcessingMethod inherit directly from
    /// this class. This self-referential property makes both shapes behave the same.
    /// </summary>
    public ParamContainer Params => this;

    /// <summary>References to ParamGroups defined elsewhere in the document.</summary>
    public List<ParamGroup> ParamGroups { get; } = new();

    /// <summary>CV-defined parameters.</summary>
    public List<CVParam> CVParams { get; } = new();

    /// <summary>Free-form user parameters.</summary>
    public List<UserParam> UserParams { get; } = new();

    /// <summary>
    /// Finds the first CVParam with the exact given <paramref name="cvid"/>, recursing into ParamGroups.
    /// Returns an empty <see cref="CVParam"/> (Cvid == CVID_Unknown) if not found.
    /// </summary>
    public CVParam CvParam(CVID cvid)
    {
        foreach (var p in CVParams)
            if (p.Cvid == cvid) return p;

        foreach (var pg in ParamGroups)
        {
            var p = pg?.CvParam(cvid);
            if (p is not null && p.Cvid != CVID.CVID_Unknown) return p;
        }

        return new CVParam();
    }

    /// <summary>
    /// Finds the first CVParam whose id is_a child of <paramref name="cvid"/>, recursing into ParamGroups.
    /// </summary>
    public CVParam CvParamChild(CVID cvid)
    {
        foreach (var p in CVParams)
            if (CvLookup.CvIsA(p.Cvid, cvid)) return p;

        foreach (var pg in ParamGroups)
        {
            var p = pg?.CvParamChild(cvid);
            if (p is not null && p.Cvid != CVID.CVID_Unknown) return p;
        }

        return new CVParam();
    }

    /// <summary>Returns the value of <see cref="CvParam"/> for <paramref name="cvid"/>, or <paramref name="defaultValue"/> if missing.</summary>
    public T CvParamValueOrDefault<T>(CVID cvid, T defaultValue)
    {
        var p = CvParam(cvid);
        return p.IsEmpty ? defaultValue : p.ValueAs<T>();
    }

    /// <summary>Returns the value of <see cref="CvParamChild"/> for <paramref name="cvid"/>, or <paramref name="defaultValue"/> if missing.</summary>
    public T CvParamChildValueOrDefault<T>(CVID cvid, T defaultValue)
    {
        var p = CvParamChild(cvid);
        return p.IsEmpty ? defaultValue : p.ValueAs<T>();
    }

    /// <summary>Returns all CVParams whose id is_a child of <paramref name="cvid"/> (recursive).</summary>
    public List<CVParam> CvParamChildren(CVID cvid)
    {
        var results = new List<CVParam>();
        foreach (var p in CVParams)
            if (CvLookup.CvIsA(p.Cvid, cvid)) results.Add(p);
        foreach (var pg in ParamGroups)
            if (pg is not null) results.AddRange(pg.CvParamChildren(cvid));
        return results;
    }

    /// <summary>True iff any CVParam (recursive) has the given exact CVID.</summary>
    public bool HasCVParam(CVID cvid) => CvParam(cvid).Cvid != CVID.CVID_Unknown;

    /// <summary>True iff any CVParam (recursive) is_a child of the given CVID.</summary>
    public bool HasCVParamChild(CVID cvid) => CvParamChild(cvid).Cvid != CVID.CVID_Unknown;

    /// <summary>Finds the first local UserParam with the given name (not recursive).</summary>
    public UserParam UserParam(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (var u in UserParams)
            if (u.Name == name) return u;
        return new UserParam();
    }

    /// <summary>Sets or adds a CVParam (not recursive).</summary>
    public void Set(CVID cvid, string value = "", CVID units = CVID.CVID_Unknown)
    {
        foreach (var p in CVParams)
        {
            if (p.Cvid == cvid) { p.Value = value ?? string.Empty; p.Units = units; return; }
        }
        CVParams.Add(new CVParam(cvid, value, units));
    }

    /// <summary>Sets or adds a CVParam with a double value, formatted to match pwiz C++.</summary>
    public void Set(CVID cvid, double value, CVID units = CVID.CVID_Unknown)
        => Set(cvid, PwizFloat.ToPwizString(value), units);

    /// <summary>Sets or adds a CVParam with a float value, formatted to match pwiz C++.</summary>
    public void Set(CVID cvid, float value, CVID units = CVID.CVID_Unknown)
        => Set(cvid, PwizFloat.ToPwizString(value), units);

    /// <summary>Sets or adds a CVParam with an int value.</summary>
    public void Set(CVID cvid, int value, CVID units = CVID.CVID_Unknown)
        => Set(cvid, value.ToString(CultureInfo.InvariantCulture), units);

    /// <summary>Sets or adds a CVParam with a long value.</summary>
    public void Set(CVID cvid, long value, CVID units = CVID.CVID_Unknown)
        => Set(cvid, value.ToString(CultureInfo.InvariantCulture), units);

    /// <summary>Sets or adds a CVParam with a bool value.</summary>
    public void Set(CVID cvid, bool value, CVID units = CVID.CVID_Unknown)
        => Set(cvid, value ? "true" : "false", units);

    /// <summary>True iff the container has no params or param-group references.</summary>
    public virtual bool IsEmpty =>
        ParamGroups.Count == 0 && CVParams.Count == 0 && UserParams.Count == 0;

    /// <summary>Clears all params and references.</summary>
    public virtual void Clear()
    {
        ParamGroups.Clear();
        CVParams.Clear();
        UserParams.Clear();
    }

    /// <inheritdoc/>
    public bool Equals(ParamContainer? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        // Equality: same CVParams (order-insensitive) and UserParams (order-insensitive) recursively through ParamGroups.
        // This matches pwiz's Diff-based equality semantics for the ParamContainer shape.
        return SetEqual(CVParams, other.CVParams)
               && SetEqual(UserParams, other.UserParams)
               && DeepGroupEqual(ParamGroups, other.ParamGroups);
    }

    private static bool SetEqual<T>(List<T> a, List<T> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var x in a) if (!b.Contains(x)) return false;
        return true;
    }

    private static bool DeepGroupEqual(List<ParamGroup> a, List<ParamGroup> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var ga in a)
        {
            bool found = false;
            foreach (var gb in b)
                if (ga.Equals(gb)) { found = true; break; }
            if (!found) return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ParamContainer);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int h = 0;
        foreach (var p in CVParams) h ^= p.GetHashCode();
        foreach (var u in UserParams) h ^= u.GetHashCode();
        return h;
    }
}

/// <summary>
/// A referenceable group of CVParams and UserParams, identified by id.
/// Port of pwiz/data::ParamGroup.
/// </summary>
public sealed class ParamGroup : ParamContainer, IEquatable<ParamGroup>
{
    /// <summary>The id used to reference this group from elsewhere in the document.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Creates an empty ParamGroup.</summary>
    public ParamGroup() { }

    /// <summary>Creates a ParamGroup with the given id.</summary>
    public ParamGroup(string id) => Id = id ?? string.Empty;

    /// <inheritdoc/>
    public override bool IsEmpty => string.IsNullOrEmpty(Id) && base.IsEmpty;

    /// <inheritdoc/>
    public bool Equals(ParamGroup? other) =>
        other is not null && Id == other.Id && base.Equals(other);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ParamGroup);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id, base.GetHashCode());
}
