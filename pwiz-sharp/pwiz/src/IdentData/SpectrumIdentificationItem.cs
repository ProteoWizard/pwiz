namespace Pwiz.Data.IdentData;

/// <summary>
/// A single peptide-spectrum match: the assignment of one spectrum to one peptide candidate
/// with its scores and rank. Port of <c>pwiz::identdata::SpectrumIdentificationItem</c>.
/// </summary>
public sealed class SpectrumIdentificationItem : IdentifiableParamContainer
{
    /// <summary>Charge state of the precursor that produced this PSM.</summary>
    public int ChargeState { get; set; }

    /// <summary>Observed precursor m/z (the value the search engine queried with).</summary>
    public double ExperimentalMassToCharge { get; set; }

    /// <summary>Theoretical m/z of the matched peptide at <see cref="ChargeState"/>.</summary>
    public double CalculatedMassToCharge { get; set; }

    /// <summary>Theoretical isoelectric point of the matched peptide.</summary>
    public double CalculatedPI { get; set; }

    /// <summary>The matched peptide.</summary>
    public Peptide? PeptidePtr { get; set; }

    /// <summary>1-based rank of this match among the candidates for the same spectrum.</summary>
    public int Rank { get; set; }

    /// <summary>True if this match passes the search engine's threshold.</summary>
    public bool PassThreshold { get; set; }

    /// <summary>Peptide-evidence entries (one per protein the matched peptide maps into).</summary>
    public List<PeptideEvidence> PeptideEvidencePtr { get; } = new();

    /// <summary>Per-ion fragmentation results (b3, y5, ...). Most search engines leave this empty.</summary>
    public List<IonType> Fragmentation { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && ChargeState == 0
        && ExperimentalMassToCharge == 0
        && CalculatedMassToCharge == 0
        && CalculatedPI == 0
        && PeptidePtr is null
        && Rank == 0
        && !PassThreshold
        && PeptideEvidencePtr.Count == 0
        && Fragmentation.Count == 0;
}
