using System;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Fragment ion types. Maps to osprey-core/src/types.rs IonType enum.
    /// </summary>
    public enum IonType
    {
        B,
        Y,
        A,
        C,
        X,
        Z,
        Precursor,
        Internal,
        Immonium,
        Unknown
    }

    /// <summary>
    /// Extension methods for <see cref="IonType"/>.
    /// </summary>
    public static class IonTypeExtensions
    {
        /// <summary>
        /// Parses a character to an <see cref="IonType"/>, case-insensitive.
        /// </summary>
        public static IonType FromChar(char c)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'B': return IonType.B;
                case 'Y': return IonType.Y;
                case 'A': return IonType.A;
                case 'C': return IonType.C;
                case 'X': return IonType.X;
                case 'Z': return IonType.Z;
                default: return IonType.Unknown;
            }
        }
    }
}
