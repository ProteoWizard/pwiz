namespace Pwiz.Data.Common.Proteome;

/// <summary>
/// Per-field comparison helpers for <see cref="ProteomeData"/>, <see cref="ProteinList"/>,
/// and <see cref="Protein"/>. Port of <c>pwiz::proteome::Diff</c>.
/// </summary>
/// <remarks>
/// The cpp shape returns the "a minus b" and "b minus a" deltas in two output parameters.
/// We expose a single <c>IsEqual</c> with a textual mismatch report — the harness
/// tests don't currently need the full bidirectional delta form, so this keeps the API
/// compact. Add the cpp-style two-output overload if/when it's needed.
/// </remarks>
public static class ProteomeDataDiff
{
    /// <summary>Compare two proteome documents. Returns true iff they match; on false,
    /// <paramref name="reason"/> contains a short human-readable description of the
    /// first difference seen (id mismatch, list-size mismatch, per-protein content
    /// mismatch).</summary>
    public static bool IsEqual(ProteomeData a, ProteomeData b, out string reason,
                               bool ignoreMetadata = false)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        reason = string.Empty;

        if (!ignoreMetadata && a.Id != b.Id)
        {
            reason = $"id: '{a.Id}' vs '{b.Id}'";
            return false;
        }

        var la = a.ProteinList;
        var lb = b.ProteinList;
        if (la is null && lb is null) return true;
        if (la is null || lb is null)
        {
            reason = "one side has a null ProteinList";
            return false;
        }
        return IsEqual(la, lb, out reason, ignoreMetadata);
    }

    /// <summary>Compare two protein lists. Same contract as the
    /// <see cref="ProteomeData"/> overload.</summary>
    public static bool IsEqual(ProteinList a, ProteinList b, out string reason,
                               bool ignoreMetadata = false)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        reason = string.Empty;
        if (a.Count != b.Count)
        {
            reason = $"ProteinList sizes differ: {a.Count} vs {b.Count}";
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            var pa = a.GetProtein(i, getSequence: true);
            var pb = b.GetProtein(i, getSequence: true);
            if (!IsEqual(pa, pb, out reason, ignoreMetadata))
            {
                reason = $"Protein[{i}]: {reason}";
                return false;
            }
        }
        return true;
    }

    /// <summary>Compare two protein records. Sequence + id are always compared; the
    /// description is compared unless <paramref name="ignoreMetadata"/> is set
    /// (matches cpp's <c>DiffConfig.ignoreMetadata</c>).</summary>
    public static bool IsEqual(Protein a, Protein b, out string reason,
                               bool ignoreMetadata = false)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        reason = string.Empty;
        if (a.Id != b.Id) { reason = $"id '{a.Id}' vs '{b.Id}'"; return false; }
        if (a.Index != b.Index) { reason = $"index {a.Index} vs {b.Index}"; return false; }
        if (!ignoreMetadata && a.Description != b.Description)
        {
            reason = $"description '{a.Description}' vs '{b.Description}'";
            return false;
        }
        if (a.Sequence != b.Sequence)
        {
            reason = $"sequence differs (lengths {a.Sequence.Length} vs {b.Sequence.Length})";
            return false;
        }
        return true;
    }
}
