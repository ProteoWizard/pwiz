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
                    double calculatedMz = isCustomIon
                        ? Adduct.FromChargeProtonated(i).MzFromNeutralMass(mass)
                        : SequenceMassCalc.GetMZ(mass, i);
                    double delta = mz - calculatedMz;
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
                        return Adduct.FromCharge(result, isCustomIon ? Adduct.ADDUCT_TYPE.non_proteomic : Adduct.ADDUCT_TYPE.proteomic);
                    }
                    if (deltaAbs < nearestDelta)
                    {
                        nearestDelta = deltaAbs;
                        nearestCharge = i;
                    }
                    // If the charge is positive and the calculated m/z is smaller than the desired m/z
                    // increasing the charge further cannot possibly produce a match
                    if (massShiftType == MassShiftType.none && minCharge > 0 && delta > 0)
                        break;
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
                                              int? precursorZ,
                                              double precursorMz,
                                              double tolerance,
                                              bool isCustomIon,
                                              bool isDecoy,
                                              out int massShift,
                                              out int nearestCharge)
        {
            return CalcCharge(precursorMassH, precursorMz, tolerance, isCustomIon,
                precursorZ ?? TransitionGroup.MIN_PRECURSOR_CHARGE,
                precursorZ ?? TransitionGroup.MAX_PRECURSOR_CHARGE,
                TransitionGroup.MassShifts,
                isDecoy ? MassShiftType.shift_only : MassShiftType.none,
                out massShift, out nearestCharge);
        }

        private static Adduct CalcProductCharge(TypedMass productMassH, int? productZ, double productMz, double tolerance, bool isCustomIon,
                                             Adduct maxCharge, MassShiftType massShiftType, out int massShift, out int nearestCharge)
        {
            return CalcCharge(productMassH, productMz, tolerance, isCustomIon,
                productZ ?? Transition.MIN_PRODUCT_CHARGE,
                productZ ?? Math.Min(maxCharge.AdductCharge, Transition.MAX_PRODUCT_CHARGE),
                Transition.MassShifts,
                massShiftType,
                out massShift,
                out nearestCharge);
        }

        public enum MassShiftType { none, shift_only, either }

        public static Adduct CalcProductCharge(TypedMass productPrecursorMass,
            int? productZ,
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
            double? minFragmentMass = null, maxFragmentMass = null, maxLoss = null;
            if (massShiftType == MassShiftType.none)
            {
                if (!productZ.HasValue)
                    minFragmentMass = productMz - tolerance;
                else
                {
                    minFragmentMass = SequenceMassCalc.GetMH(productMz - tolerance, productZ.Value);
                    maxFragmentMass = SequenceMassCalc.GetMH(productMz + tolerance, productZ.Value);
                }
            }

            var bestCharge = Adduct.EMPTY;
            IonType? bestIonType = null;
            int? bestOrdinal = null;
            TransitionLosses bestLosses = null;
            int bestMassShift = 0;

            // Check to see if it is the precursor
            foreach (var lossesTrial in TransitionGroup.CalcTransitionLosses(IonType.precursor, 0, massType, potentialLosses))
            {
                var productMass = productPrecursorMass;
                if (lossesTrial != null)
                {
                    productMass -= lossesTrial.Mass;
                    maxLoss = Math.Max(maxLoss ?? 0, lossesTrial.Mass);
                }
                int potentialMassShift;
                int nearestCharge;
                var charge = CalcProductCharge(productMass, productZ, productMz, tolerance, false, precursorCharge,
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

            if (maxLoss.HasValue)
                maxFragmentMass += maxLoss.Value;

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

                // The peptide length is 1 longer than the mass array
                for (int ord = len; ord > 0; ord--)
                {
                    int offset = Transition.OrdinalToOffset(type, ord, len + 1);
                    var productMassBase = productMasses[type, offset];
                    // Until below the maximum fragment mass no possible matches
                    if (maxFragmentMass.HasValue && productMassBase > maxFragmentMass.Value)
                        continue;
                    // Once below the minimum fragment mass no more possible matches, so stop
                    if (minFragmentMass.HasValue && productMassBase < minFragmentMass.Value)
                        break;

                    foreach (var lossesTrial in TransitionGroup.CalcTransitionLosses(type, offset, massType, potentialLosses))
                    {
                        // Look for the closest match.
                        var productMass = productMassBase;
                        if (lossesTrial != null)
                            productMass -= lossesTrial.Mass;
                        int potentialMassShift;
                        int nearestCharge;
                        var chargeFound = CalcProductCharge(productMass, productZ, productMz, tolerance, false, precursorCharge,
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
                                bestOrdinal = ord;
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
            // RECONSIDERED: Let's try only ion type pairs which are possible together
//            foreach (var ionType in acceptedIonTypes)
//            {
//                if (ionType != IonType.precursor)   // Precursor is handled separately
//                    yield return new IonTypeAccepted { IonType = ionType, Accepted = true };
//            }
            // CONSIDER: Should we allow types other than the ones in the settings?
            //           We always have, but this also makes it impossible to keep Skyline from
            //           guessing weird types like c, x and z, when the problem is something else
            for (int i = 0; i < Transition.PEPTIDE_ION_TYPES.Length; i++)
            {
                var ionType = Transition.PEPTIDE_ION_TYPES[i];
                int pairIndex = i % 2 == 0 ? i + 1 : i - 1;
                var ionTypePair = Transition.PEPTIDE_ION_TYPES[pairIndex];
                if (acceptedIonTypes.Contains(ionType) || acceptedIonTypes.Contains(ionTypePair))
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
                        case IonType.zh:
                        case IonType.zhh:
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
