namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Statistical method for FDR estimation.
    /// Maps to osprey-core/src/types.rs FdrMethod.
    /// </summary>
    public enum FdrMethod
    {
        Percolator,
        Mokapot,
        Simple
    }
}
