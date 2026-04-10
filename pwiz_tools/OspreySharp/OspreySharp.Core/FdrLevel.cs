namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Level at which FDR is controlled.
    /// Maps to osprey-core/src/types.rs FdrLevel.
    /// </summary>
    public enum FdrLevel
    {
        Precursor,
        Peptide,
        Both
    }
}
