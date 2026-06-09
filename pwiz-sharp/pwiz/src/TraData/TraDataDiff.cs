using Pwiz.Data.Common.Params;

namespace Pwiz.Data.TraData;

/// <summary>
/// Equality helpers for <see cref="TraData"/> and its sub-types. Port of
/// <c>pwiz::tradata::Diff</c>, exposed as a single <c>IsEqual</c> per type with a
/// human-readable mismatch reason — matches the shape used by
/// <see cref="Pwiz.Data.Common.Proteome.ProteomeDataDiff"/>.
/// </summary>
public static class TraDataDiff
{
    /// <summary>Compare two TraML documents. Returns true iff every list and the
    /// version match (skipping <see cref="TraData.Id"/> when
    /// <paramref name="ignoreMetadata"/> is set, mirroring cpp's <c>DiffConfig.ignoreMetadata</c>).</summary>
    public static bool IsEqual(TraData a, TraData b, out string reason,
                               bool ignoreMetadata = false)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        reason = string.Empty;

        if (!ignoreMetadata && a.Id != b.Id) { reason = $"id '{a.Id}' vs '{b.Id}'"; return false; }
        if (a.Version != b.Version) { reason = $"version '{a.Version}' vs '{b.Version}'"; return false; }

        if (a.CVs.Count != b.CVs.Count) { reason = $"CVs.Count {a.CVs.Count} vs {b.CVs.Count}"; return false; }
        if (a.Contacts.Count != b.Contacts.Count) { reason = $"Contacts.Count {a.Contacts.Count} vs {b.Contacts.Count}"; return false; }
        if (a.Publications.Count != b.Publications.Count) { reason = $"Publications.Count {a.Publications.Count} vs {b.Publications.Count}"; return false; }
        if (a.Instruments.Count != b.Instruments.Count) { reason = $"Instruments.Count {a.Instruments.Count} vs {b.Instruments.Count}"; return false; }
        if (a.Software.Count != b.Software.Count) { reason = $"Software.Count {a.Software.Count} vs {b.Software.Count}"; return false; }
        if (a.Proteins.Count != b.Proteins.Count) { reason = $"Proteins.Count {a.Proteins.Count} vs {b.Proteins.Count}"; return false; }
        if (a.Peptides.Count != b.Peptides.Count) { reason = $"Peptides.Count {a.Peptides.Count} vs {b.Peptides.Count}"; return false; }
        if (a.Compounds.Count != b.Compounds.Count) { reason = $"Compounds.Count {a.Compounds.Count} vs {b.Compounds.Count}"; return false; }
        if (a.Transitions.Count != b.Transitions.Count) { reason = $"Transitions.Count {a.Transitions.Count} vs {b.Transitions.Count}"; return false; }
        if (a.Targets.IncludeList.Count != b.Targets.IncludeList.Count)
        { reason = $"Targets.IncludeList.Count {a.Targets.IncludeList.Count} vs {b.Targets.IncludeList.Count}"; return false; }
        if (a.Targets.ExcludeList.Count != b.Targets.ExcludeList.Count)
        { reason = $"Targets.ExcludeList.Count {a.Targets.ExcludeList.Count} vs {b.Targets.ExcludeList.Count}"; return false; }

        for (int i = 0; i < a.Contacts.Count; i++)
            if (!IsEqualContact(a.Contacts[i], b.Contacts[i], out reason))
            { reason = $"Contacts[{i}]: {reason}"; return false; }

        for (int i = 0; i < a.Software.Count; i++)
            if (!IsEqualSoftware(a.Software[i], b.Software[i], out reason))
            { reason = $"Software[{i}]: {reason}"; return false; }

        for (int i = 0; i < a.Proteins.Count; i++)
            if (!IsEqualProtein(a.Proteins[i], b.Proteins[i], out reason))
            { reason = $"Proteins[{i}]: {reason}"; return false; }

        for (int i = 0; i < a.Peptides.Count; i++)
            if (!IsEqualPeptide(a.Peptides[i], b.Peptides[i], out reason))
            { reason = $"Peptides[{i}]: {reason}"; return false; }

        for (int i = 0; i < a.Transitions.Count; i++)
            if (!IsEqualTransition(a.Transitions[i], b.Transitions[i], out reason))
            { reason = $"Transitions[{i}]: {reason}"; return false; }

        return true;
    }

    /// <summary>Per-protein equality: id, sequence, params.</summary>
    public static bool IsEqualProtein(Protein a, Protein b, out string reason)
    {
        reason = string.Empty;
        if (a.Id != b.Id) { reason = $"id '{a.Id}' vs '{b.Id}'"; return false; }
        if (a.Sequence != b.Sequence) { reason = "sequence differs"; return false; }
        if (!ParamsEqual(a, b, out reason)) return false;
        return true;
    }

    /// <summary>Per-peptide equality: id, sequence, mods, protein refs (by id), retention times, params.</summary>
    public static bool IsEqualPeptide(Peptide a, Peptide b, out string reason)
    {
        reason = string.Empty;
        if (a.Id != b.Id) { reason = $"id '{a.Id}' vs '{b.Id}'"; return false; }
        if (a.Sequence != b.Sequence) { reason = "sequence differs"; return false; }
        if (a.Modifications.Count != b.Modifications.Count)
        { reason = $"Modifications.Count {a.Modifications.Count} vs {b.Modifications.Count}"; return false; }
        if (a.Proteins.Count != b.Proteins.Count)
        { reason = $"Proteins.Count {a.Proteins.Count} vs {b.Proteins.Count}"; return false; }
        for (int i = 0; i < a.Proteins.Count; i++)
            if (a.Proteins[i].Id != b.Proteins[i].Id)
            { reason = $"Proteins[{i}].Id '{a.Proteins[i].Id}' vs '{b.Proteins[i].Id}'"; return false; }
        for (int i = 0; i < a.Modifications.Count; i++)
        {
            var ma = a.Modifications[i]; var mb = b.Modifications[i];
            if (ma.Location != mb.Location || ma.MonoisotopicMassDelta != mb.MonoisotopicMassDelta
                || ma.AverageMassDelta != mb.AverageMassDelta)
            { reason = $"Modifications[{i}]: numeric field mismatch"; return false; }
        }
        if (!ParamsEqual(a, b, out reason)) return false;
        return true;
    }

    /// <summary>Per-transition equality.</summary>
    public static bool IsEqualTransition(Transition a, Transition b, out string reason)
    {
        reason = string.Empty;
        if (a.Id != b.Id) { reason = $"id '{a.Id}' vs '{b.Id}'"; return false; }
        if ((a.Peptide?.Id ?? string.Empty) != (b.Peptide?.Id ?? string.Empty))
        { reason = $"peptideRef '{a.Peptide?.Id}' vs '{b.Peptide?.Id}'"; return false; }
        if ((a.Compound?.Id ?? string.Empty) != (b.Compound?.Id ?? string.Empty))
        { reason = $"compoundRef '{a.Compound?.Id}' vs '{b.Compound?.Id}'"; return false; }
        if (!ParamsEqual(a, b, out reason)) return false;
        if (!ParamsEqual(a.Precursor, b.Precursor, out reason))
        { reason = $"Precursor: {reason}"; return false; }
        if (!ParamsEqual(a.Product, b.Product, out reason))
        { reason = $"Product: {reason}"; return false; }
        return true;
    }

    /// <summary>Per-software equality: id, version, params.</summary>
    public static bool IsEqualSoftware(Software a, Software b, out string reason)
    {
        reason = string.Empty;
        if (a.Id != b.Id) { reason = $"id '{a.Id}' vs '{b.Id}'"; return false; }
        if (a.Version != b.Version) { reason = $"version '{a.Version}' vs '{b.Version}'"; return false; }
        if (!ParamsEqual(a, b, out reason)) return false;
        return true;
    }

    /// <summary>Per-contact equality: id, params.</summary>
    public static bool IsEqualContact(Contact a, Contact b, out string reason)
    {
        reason = string.Empty;
        if (a.Id != b.Id) { reason = $"id '{a.Id}' vs '{b.Id}'"; return false; }
        if (!ParamsEqual(a, b, out reason)) return false;
        return true;
    }

    // ParamContainer comparison: same count + same cvParams (by Cvid + value) + same userParams.
    // Order-sensitive, matching cpp's vector<CVParam>.
    private static bool ParamsEqual(ParamContainer a, ParamContainer b, out string reason)
    {
        reason = string.Empty;
        if (a.CVParams.Count != b.CVParams.Count)
        { reason = $"CVParams.Count {a.CVParams.Count} vs {b.CVParams.Count}"; return false; }
        if (a.UserParams.Count != b.UserParams.Count)
        { reason = $"UserParams.Count {a.UserParams.Count} vs {b.UserParams.Count}"; return false; }
        for (int i = 0; i < a.CVParams.Count; i++)
        {
            var ca = a.CVParams[i]; var cb = b.CVParams[i];
            if (ca.Cvid != cb.Cvid || ca.Value != cb.Value || ca.Units != cb.Units)
            { reason = $"CVParams[{i}] '{ca.Cvid}'='{ca.Value}' vs '{cb.Cvid}'='{cb.Value}'"; return false; }
        }
        for (int i = 0; i < a.UserParams.Count; i++)
        {
            var ua = a.UserParams[i]; var ub = b.UserParams[i];
            if (ua.Name != ub.Name || ua.Value != ub.Value || ua.Type != ub.Type || ua.Units != ub.Units)
            { reason = $"UserParams[{i}] '{ua.Name}'='{ua.Value}' vs '{ub.Name}'='{ub.Value}'"; return false; }
        }
        return true;
    }
}
