using System;
using System.Globalization;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Represents a neutral loss from a fragment ion.
    /// Maps to osprey-core/src/types.rs NeutralLoss enum.
    /// </summary>
    public class NeutralLoss
    {
        public static readonly NeutralLoss H2O = new NeutralLoss(18.010565);
        public static readonly NeutralLoss NH3 = new NeutralLoss(17.026549);
        public static readonly NeutralLoss H3PO4 = new NeutralLoss(97.976896);

        public double Mass { get; private set; }

        private NeutralLoss(double mass)
        {
            Mass = mass;
        }

        /// <summary>
        /// Creates a custom neutral loss with the specified mass.
        /// </summary>
        public static NeutralLoss Custom(double mass)
        {
            return new NeutralLoss(mass);
        }

        /// <summary>
        /// Parses a string to a <see cref="NeutralLoss"/>. Returns null for empty, "NOLOSS", or unrecognized input.
        /// </summary>
        public static NeutralLoss Parse(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            switch (s.ToUpperInvariant())
            {
                case "H2O":
                case "WATER":
                    return H2O;
                case "NH3":
                case "AMMONIA":
                    return NH3;
                case "H3PO4":
                case "PHOSPHO":
                    return H3PO4;
                case "NOLOSS":
                    return null;
                default:
                    double mass;
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
                        return Custom(mass);
                    return null;
            }
        }
    }
}
