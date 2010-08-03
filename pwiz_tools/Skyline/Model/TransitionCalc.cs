/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public static class TransitionCalc
    {
        /// <summary>
        /// Calculates the matching charge within a tolerance for a mass.
        /// </summary>
        /// <param name="massH">The mass to calculate charge for</param>
        /// <param name="mz">The desired m/z value the charge should produce</param>
        /// <param name="tolerance">How far off the actual m/z is allowed to be</param>
        /// <param name="min">Minimum charge to consider</param>
        /// <param name="max">Maximum charge to consider</param>
        /// <returns>A matching charge or the closest non-matching charge negated.</returns>
        public static int CalcCharge(double massH, double mz, double tolerance, int min, int max)
        {
            Debug.Assert(min <= max);

            int nearestCharge = 0;
            double nearestDelta = double.MaxValue;

            for (int i = min; i <= max; i++)
            {
                double delta = Math.Abs(mz - SequenceMassCalc.GetMZ(massH, i));
                if (MatchMz(delta, tolerance))
                    return i;
                if (delta < nearestDelta)
                {
                    nearestDelta = delta;
                    nearestCharge = i;
                }
            }

            Debug.Assert(nearestCharge != 0);   // Could only happen if min > max

            return -nearestCharge;
        }

        private static bool MatchMz(double delta, double tolerance)
        {
            return (delta <= tolerance);
        }

        public static int CalcPrecursorCharge(double precursorMassH, double precursorMz, double tolerance)
        {
            return CalcCharge(precursorMassH, precursorMz, tolerance,
                TransitionGroup.MIN_PRECURSOR_CHARGE,
                TransitionGroup.MAX_PRECURSOR_CHARGE);
        }

        private static int CalcProductCharge(double productMassH, double productMz, double tolerance, int maxCharge)
        {
            return CalcCharge(productMassH, productMz, tolerance,
                Transition.MIN_PRODUCT_CHARGE,
                maxCharge);
        }

        public static int CalcProductCharge(double productPrecursorMass,
                                            int precursorCharge,
                                            double[,] productMasses,
                                            IList<IList<ExplicitLoss>> potentialLosses,
                                            double productMz,
                                            double tolerance,
                                            MassType massType,
                                            out IonType? ionType,
                                            out int? ordinal,
                                            out TransitionLosses losses)
        {
            // Get length of fragment ion mass array
            int len = productMasses.GetLength(1);

            // Check to see if it is the precursor
            foreach (var lossesTrial in TransitionGroup.CalcTransitionLosses(IonType.precursor, 0, massType, potentialLosses))
            {
                double productMass = productPrecursorMass - (lossesTrial != null ? lossesTrial.Mass : 0);
                int charge = CalcProductCharge(productMass, productMz, tolerance, precursorCharge);
                if (charge == precursorCharge)
                {
                    ionType = IonType.precursor;
                    ordinal = len + 1;
                    losses = lossesTrial;
                    return charge;
                }
            }

            // Check all possible ion types and offsets
            double minDelta = double.MaxValue;
            int bestCharge = 0;
            IonType? bestIonType = null;
            int? bestOrdinal = null;
            TransitionLosses bestLosses = null;

            foreach (IonType type in Transition.ALL_TYPES)
            {
                for (int offset = 0; offset < len; offset++)
                {
                    foreach (var lossesTrial in TransitionGroup.CalcTransitionLosses(type, offset, massType, potentialLosses))
                    {
                        // Look for the closest match.
                        double productMass = productMasses[(int) type, offset];
                        if (lossesTrial != null)
                            productMass -= lossesTrial.Mass;
                        int charge = CalcProductCharge(productMass, productMz, tolerance, precursorCharge);
                        if (charge > 0)
                        {
                            double delta = Math.Abs(productMz - SequenceMassCalc.GetMZ(productMass, charge));
                            if (minDelta > delta)
                            {
                                bestCharge = charge;
                                bestIonType = type;
                                // The peptide length is 1 longer than the mass array
                                bestOrdinal = Transition.OffsetToOrdinal(type, offset, len + 1);
                                bestLosses = lossesTrial;

                                minDelta = delta;
                            }
                        }
                    }
                }
            }

            ionType = bestIonType;
            ordinal = bestOrdinal;
            losses = bestLosses;

            return bestCharge;
        }

    }
}
