﻿/*
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
        /// Calculates the matching charge within a tolerance for a mass, assuming (de)protonation.
        /// </summary>
        /// <param name="mass">The mass to calculate charge for (actually massH if !IsCustomIon)</param>
        /// <param name="mz">The desired m/z value the charge should produce</param>
        /// <param name="tolerance">How far off the actual m/z is allowed to be</param>
        /// <param name="isCustomIon">Is this a custom ion formula?</param>
        /// <param name="minCharge">Minimum charge to consider</param>
        /// <param name="maxCharge">Maximum charge to consider</param>
        /// <param name="massShifts">Possible mass shifts that may have been applied to decoys</param>
        /// <param name="massShiftType"></param>
        /// <param name="massShift">Mass shift required to to achieve this charge state or zero</param>
        /// <param name="nearestCharge">closest matching charge, useful when return value is null</param>
        /// <returns>A matching charge or null, in which case the closest non-matching charge can be found in the nearestCharge value.</returns>
        public static Adduct CalcCharge(TypedMass mass, double mz, double tolerance, bool isCustomIon, int minCharge, int maxCharge,
            ICollection<int> massShifts, MassShiftType massShiftType, out int massShift, out int nearestCharge)
        {
            Assume.IsTrue(minCharge <= maxCharge);

            massShift = 0;

            nearestCharge = 0;
            double nearestDelta = double.MaxValue;

            for (int i = minCharge; i <= maxCharge; i++)
            {
                if (i != 0) // Avoid z=0 if we're entertaining negative charge states
                {
                    double delta = mz - ( isCustomIon ? Adduct.FromChargeProtonated(i).MzFromNeutralMass(mass) : SequenceMassCalc.GetMZ(mass, i) );
                    double deltaAbs = Math.Abs(delta);
                    int potentialShift = (int) Math.Round(deltaAbs);
                    double fractionalDelta = deltaAbs - potentialShift;
                    if (MatchMz(fractionalDelta, tolerance) && MatchMassShift(potentialShift, massShifts, massShiftType))
                    {
                        massShift = potentialShift;
                        if (delta < 0)
                            massShift = -massShift;
                        var result = i;
                        nearestCharge = i;
                        return Adduct.FromChargeProtonated(result);
                    }
                    if (deltaAbs < nearestDelta)
                    {
                        nearestDelta = deltaAbs;
                        nearestCharge = i;
                    }
                }
            }

            Debug.Assert(nearestCharge != 0);   // Could only happen if min > max

            return Adduct.EMPTY;
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

        public static Adduct CalcPrecursorCharge(TypedMass precursorMassH,
                                              double precursorMz,
                                              double tolerance,
                                              bool isCustomIon,
                                              bool isDecoy,
                                              out int massShift,
                                              out int nearestCharge)
        {
            return CalcCharge(precursorMassH, precursorMz, tolerance, isCustomIon,
                TransitionGroup.MIN_PRECURSOR_CHARGE,
                TransitionGroup.MAX_PRECURSOR_CHARGE,
                TransitionGroup.MassShifts,
                isDecoy ? MassShiftType.shift_only : MassShiftType.none,
                out massShift, out nearestCharge);
        }

        private static Adduct CalcProductCharge(TypedMass productMassH, double productMz, double tolerance, bool isCustomIon,
                                             Adduct maxCharge, MassShiftType massShiftType, out int massShift, out int nearestCharge)
        {
            return CalcCharge(productMassH, productMz, tolerance, isCustomIon,
                Transition.MIN_PRODUCT_CHARGE,
                Math.Min(maxCharge.AdductCharge, Transition.MAX_PRODUCT_CHARGE),
                Transition.MassShifts,
                massShiftType,
                out massShift,
                out nearestCharge);
        }

        public enum MassShiftType { none, shift_only, either }

        public static Adduct CalcProductCharge(TypedMass productPrecursorMass,
                                            Adduct precursorCharge,
                                            IList<IonType> acceptedIonTypes,
                                            IonTable<TypedMass> productMasses,
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
            double? minDelta = null;
            var bestCharge = Adduct.EMPTY;
            IonType? bestIonType = null;
            int? bestOrdinal = null;
            TransitionLosses bestLosses = null;
            int bestMassShift = 0;

            // Check to see if it is the precursor
            foreach (var lossesTrial in TransitionGroup.CalcTransitionLosses(IonType.precursor, 0, massType, potentialLosses))
            {
                var productMass = productPrecursorMass - (lossesTrial != null ? lossesTrial.Mass : 0);
                int potentialMassShift;
                int nearestCharge;
                var charge = CalcProductCharge(productMass, productMz, tolerance, false, precursorCharge,
                                               massShiftType, out potentialMassShift, out nearestCharge);
                if (Equals(charge, precursorCharge))
                {
                    double potentialMz = SequenceMassCalc.GetMZ(productMass, charge) + potentialMassShift;
                    double delta = Math.Abs(productMz - potentialMz);

                    if (CompareIonMatch(delta, lossesTrial, potentialMassShift, minDelta, bestLosses, bestMassShift) < 0)
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

            var categoryLast = -1;
            foreach (var typeAccepted in GetIonTypes(acceptedIonTypes))
            {
                var type = typeAccepted.IonType;
                var category = typeAccepted.IonCategory;

                // Types have priorities.  If changing type category, and there is already a
                // suitable answer stop looking.
                if (category != categoryLast && minDelta.HasValue && MatchMz(minDelta.Value, tolerance))
                    break;
                categoryLast = category;

                for (int offset = 0; offset < len; offset++)
                {
                    foreach (var lossesTrial in TransitionGroup.CalcTransitionLosses(type, offset, massType, potentialLosses))
                    {
                        // Look for the closest match.
                        var productMass = productMasses[type, offset];
                        if (lossesTrial != null)
                            productMass -= lossesTrial.Mass;
                        int potentialMassShift;
                        int nearestCharge;
                        var chargeFound = CalcProductCharge(productMass, productMz, tolerance, false, precursorCharge,
                                                       massShiftType, out potentialMassShift, out nearestCharge);
                        if (!chargeFound.IsEmpty)
                        {
                            var charge = chargeFound;
                            double potentialMz = SequenceMassCalc.GetMZ(productMass, charge) + potentialMassShift;
                            double delta = Math.Abs(productMz - potentialMz);
                            if (CompareIonMatch(delta, lossesTrial, potentialMassShift, minDelta, bestLosses, bestMassShift) < 0)
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

            ionType = bestIonType;
            ordinal = bestOrdinal;
            losses = bestLosses;
            massShift = bestMassShift;
            return bestCharge;
        }

        private static int CompareIonMatch(double delta, TransitionLosses losses, int shift,
            double? bestDelta, TransitionLosses bestLosses, int bestShift)
        {
            if (!bestDelta.HasValue)
            {
                return -1;
            }
            // No shift is always better (less than) a shift
            if ((shift == 0) != (bestShift == 0))
                return shift == 0 ? -1 : 1;
            // No losses is always better (less than) losses
            if ((losses == null) != (bestLosses == null))
                return losses == null ? -1 : 1;
            // Otherise, compare the deltas
            return delta.CompareTo(bestDelta);
        }

        private static IEnumerable<IonTypeAccepted> GetIonTypes(IList<IonType> acceptedIonTypes)
        {
            foreach (var ionType in acceptedIonTypes)
            {
                if (ionType != IonType.precursor)   // Precursor is handled separately
                    yield return new IonTypeAccepted { IonType = ionType, Accepted = true };
            }
            // CONSIDER: Should we allow types other than the ones in the settings?
            //           We always have, but this also makes it impossible to keep Skyline from
            //           guessing wierd types like c, x and z, when the problem is something else
            foreach (var ionType in Transition.PEPTIDE_ION_TYPES)
            {
                if (!acceptedIonTypes.Contains(ionType))
                    yield return new IonTypeAccepted { IonType = ionType };
            }
        }

        private struct IonTypeAccepted
        {
            public IonType IonType { get; set; }
            public bool Accepted { get; set; }

            public int IonCategory
            {
                get
                {
                    switch (IonType)
                    {
                        case IonType.y:
                        case IonType.b:
                            return 0;
                        case IonType.z:
                        case IonType.c:
                            return 1;
                        case IonType.x:
                        case IonType.a:
                            return 2;
                        default:
                            return -1;
                    }
                }
            }
        }
    }
}
