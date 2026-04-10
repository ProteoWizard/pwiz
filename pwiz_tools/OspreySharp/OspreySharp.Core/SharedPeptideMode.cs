namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// How shared peptides are handled during protein inference.
    /// Maps to osprey-core/src/types.rs SharedPeptideMode.
    /// </summary>
    public enum SharedPeptideMode
    {
        All,
        Razor,
        Unique
    }
}
