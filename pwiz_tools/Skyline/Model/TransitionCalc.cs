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
        /// <param name="massShifts">Possible mass shifts that may have been applied to decoys</param>
        /// <param name="massShiftType"></param>
        /// <param name="massShift">Mass shift required to to achieve this charge state or zero</param>
        /// <returns>A matching charge or the closest non-matching charge negated.</returns>
        public static int CalcCharge(double massH, double mz, double tolerance, int min, int max,
            ICollection<int> massShifts, MassShiftType massShiftType, out int massShift)
        {
            Debug.Assert(min <= max);

            massShift = 0;

            int nearestCharge = 0;
            double nearestDelta = double.MaxValue;

            for (int i = min; i <= max; i++)
            {
                double delta = mz - SequenceMassCalc.GetMZ(massH, i);
                double deltaAbs = Math.Abs(delta);
                int potentialShift = (int) Math.Round(deltaAbs);
                double fractionalDelta = deltaAbs - potentialShift;
                if (MatchMz(fractionalDelta, tolerance) && MatchMassShift(potentialShift, massShifts, massShiftType))
                {
                    massShift = potentialShift;
                    if (delta < 0)
                        massShift = -massShift;
                    return i;
                }
                if (deltaAbs < nearestDelta)
                {
                    nearestDelta = deltaAbs;
                    nearestCharge = i;
                }
            }

            Debug.Assert(nearestCharge != 0);   // Could only happen if min > max

            return -nearestCharge;
        }

        private static bool MatchMassShift(int potentialShift, ICollection<int> massShifts, MassShiftType massShiftType)
        {
            return (massShiftType != MassShiftType.shift_only && potentialShift == 0) ||
                   (massShiftType != MassShiftType.none && massShifts.Contains(potentialShift));
        }

        private static bool MatchMz(double delta, double tolerance)
        {
            return (delta <= tolerance);
        }

        public static int CalcPrecursorCharge(double precursorMassH,
                                              double precursorMz,
                                              double tolerance,
                                              bool isDecoy,
                                              out int massShift)
        {
            return CalcCharge(precursorMassH, precursorMz, tolerance,
                TransitionGroup.MIN_PRECURSOR_CHARGE,
                TransitionGroup.MAX_PRECURSOR_CHARGE,
                TransitionGroup.MassShifts,
                isDecoy ? MassShiftType.shift_only : MassShiftType.none,
                out massShift);
        }

        private static int CalcProductCharge(double productMassH, double productMz, double tolerance,
                                             int maxCharge, MassShiftType massShiftType, out int massShift)
        {
            return CalcCharge(productMassH, productMz, tolerance,
                Transition.MIN_PRODUCT_CHARGE,
                maxCharge,
                Transition.MassShifts,
                massShiftType,
                out massShift);
        }

        public enum MassShiftType { none, shift_only, either }

        public static int CalcProductCharge(double productPrecursorMass,
                                            int precursorCharge,
                                            double[,] productMasses,
                                            IList<IList<ExplicitLoss>> potentialLosses,
                                            double productMz,
                                            double tolerance,
                                            MassType massType,
                                            MassShiftType massShiftType,
                                            out IonType? ionType,
                                            out int? ordinal,
                                            out TransitionLosses losses,
                                            out int massShift)
        {
            // Get length of fragment ion mass array
            int len = productMasses.GetLength(1);

            // Check all possible ion types and offsets
            double minDelta = double.MaxValue, minDeltaNs = double.MaxValue;
            int bestCharge = 0, bestChargeNs = 0;
            IonType? bestIonType = null, bestIonTypeNs = null;
            int? bestOrdinal = null, bestOrdinalNs = null;
            TransitionLosses bestLosses = null, bestLossesNs = null;
            int bestMassShift = 0;

            // Check to see if it is the precursor
            foreach (var lossesTrial in TransitionGroup.CalcTransitionLosses(IonType.precursor, 0, massType, potentialLosses))
            {
                double productMass = productPrecursorMass - (lossesTrial != null ? lossesTrial.Mass : 0);
                int potentialMassShift;
                int charge = CalcProductCharge(productMass, productMz, tolerance, precursorCharge,
                                               massShiftType, out potentialMassShift);
                if (charge == precursorCharge)
                {
                    double potentialMz = SequenceMassCalc.GetMZ(productMass, charge) + potentialMassShift;
                    double delta = Math.Abs(productMz - potentialMz);

                    if (potentialMassShift == 0 && minDeltaNs > delta)
                    {
                        bestChargeNs = charge;
                        bestIonTypeNs = IonType.precursor;
                        bestOrdinalNs = len + 1;
                        bestLossesNs = lossesTrial;

                        minDeltaNs = delta;
                    }
                    else if (potentialMassShift != 0 && minDelta > delta)
                    {
                        bestCharge = charge;
                        bestIonType = IonType.precursor;
                        bestOrdinal = len + 1;
                        bestLosses = lossesTrial;
                        bestMassShift = potentialMassShift;

                        minDelta = delta;
                    }
                }
            }

            foreach (IonType type in Transition.ALL_TYPES)
            {
                // Types have priorities.  If moving to a lower priority type, and there is already a
                // suitable answer stop looking.
                if ((type == Transition.ALL_TYPES[2] || type == Transition.ALL_TYPES[2]) &&
                        (MatchMz(minDelta, tolerance) || MatchMz(minDeltaNs, tolerance)))
                    break;

                for (int offset = 0; offset < len; offset++)
                {
                    foreach (var lossesTrial in TransitionGroup.CalcTransitionLosses(type, offset, massType, potentialLosses))
                    {
                        // Look for the closest match.
                        double productMass = productMasses[(int) type, offset];
                        if (lossesTrial != null)
                            productMass -= lossesTrial.Mass;
                        int potentialMassShift;
                        int charge = CalcProductCharge(productMass, productMz, tolerance, precursorCharge,
                                                       massShiftType, out potentialMassShift);
                        if (charge > 0)
                        {
                            double potentialMz = SequenceMassCalc.GetMZ(productMass, charge) + potentialMassShift;
                            double delta = Math.Abs(productMz - potentialMz);
                            if (potentialMassShift == 0 && minDeltaNs > delta)
                            {
                                bestChargeNs = charge;
                                bestIonTypeNs = type;
                                // The peptide length is 1 longer than the mass array
                                bestOrdinalNs = Transition.OffsetToOrdinal(type, offset, len + 1);
                                bestLossesNs = lossesTrial;

                                minDeltaNs = delta;
                            }
                            else if (potentialMassShift != 0 && minDelta > delta)
                            {
                                bestCharge = charge;
                                bestIonType = type;
                                // The peptide length is 1 longer than the mass array
                                bestOrdinal = Transition.OffsetToOrdinal(type, offset, len + 1);
                                bestLosses = lossesTrial;
                                bestMassShift = potentialMassShift;

                                minDelta = delta;
                            }
                        }
                    }
                }
            }

            // Pefer no-shift to shift, even if the shift value is closer
            if (MatchMz(minDelta, tolerance) && !MatchMz(minDeltaNs, tolerance))
            {
                ionType = bestIonType;
                ordinal = bestOrdinal;
                losses = bestLosses;
                massShift = bestMassShift;
                return bestCharge;
            }

            ionType = bestIonTypeNs;
            ordinal = bestOrdinalNs;
            losses = bestLossesNs;
            massShift = 0;
            return bestChargeNs;
        }

    }
}
