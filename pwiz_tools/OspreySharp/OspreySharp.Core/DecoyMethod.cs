namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Method used to generate decoy sequences.
    /// Maps to osprey-core/src/types.rs DecoyMethod.
    /// </summary>
    public enum DecoyMethod
    {
        Reverse,
        Shuffle,
        FromLibrary
    }
}
