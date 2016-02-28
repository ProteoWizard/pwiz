/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.Chemistry
{
    /// <summary>
    /// A map between mass values and abundances (probabilities).
    /// MassDistribution has methods to combine the probabilities with another MassDistribution.
    /// In order to prevent the size of this structure from growing exponentially, there
    /// are MinimumAbundance and MassResolution properties.
    /// </summary>
    public class MassDistribution : ImmutableDictionary<double,double>
    {
        /// <summary>
        /// Constructs a MassDistribution consisting only of a mass of 0 with 100% probability.
        /// </summary>
        public MassDistribution(double massResolution, double minimumAbundance) 
            : this(new SortedDictionary<double, double>{{0,1}},massResolution,minimumAbundance)
        {
        }

        /// <summary>
        /// Private constructor used by NewInstance to create all useful MassDistributions.
        /// </summary>
        /// <param name="dictionary">Dictionary of masses to abundances</param>
        /// <param name="massResolution">Resolution used to merge masses and their abundances</param>
        /// <param name="minimumAbundance">Minimum abundance used to filter the masses</param>
        private MassDistribution(
            SortedDictionary<double,double> dictionary, 
            double massResolution, 
            double minimumAbundance) : base(dictionary)
        {
            MassResolution = massResolution;
            MinimumAbundance = minimumAbundance;
        }

        private MassDistribution(Helper helper)
            : base(new SortedDictionary<double, double>(helper.Masses.Take(helper.MassCount).ToDictionary(t => t.Mass, t => t.Frequency)))
        {
            MassResolution = helper.MassResolution;
            MinimumAbundance = helper.MinimumAbundance;
        }

        public double MassResolution { get; private set; }
        public double MinimumAbundance { get; private set; }

        /// <summary>
        /// Combines this MassDistribution with another MassDistribution.
        /// The combined MassDistribution is the result of taking each entry in this
        /// MassDistribution and adding the mass to each entry in the second MassDistribution,
        /// and multiplying the probability with the second MassDistribution.
        /// Afterwards, the result is trimmed down using the MassResolution and Minimum abundance.
        /// </summary>
        public MassDistribution Add(MassDistribution rhs)
        {
            var helper = new Helper(this).Add(rhs);
            return new MassDistribution(helper);
        }

        /// <summary>
        /// Returns the result of adding this MassDistribution to itself the specified number of times.
        /// </summary>
        public MassDistribution Multiply(int factor)
        {
            if (factor == 0) {
                return new MassDistribution(MassResolution, MinimumAbundance);
            }
            if (factor == 1) {
                return this;
            }
            var helper = new Helper(this).Multiply(factor);
            return new MassDistribution(helper);
        }

        /// <summary>
        /// Implements MassDistribution logic using arrays instead of dicts for best speed in 
        /// intermediate steps where lookup isn't needed.  This is a hotspot in large Skyline
        /// documents so some fairly unusual things happen here, like arrays that are larger 
        /// than they need to be just to avoid reallocation.
        /// </summary>
        public class Helper
        {
            public MassFrequency[] Masses { get; private set; }
            public int MassCount { get; private set; } // Tail of Masses array may be unused after binning
            public double MassResolution { get; private set; }
            public double MinimumAbundance { get; private set; }

            public Helper(double massResolution, double minimumAbundance)

            {
                Masses = new MassFrequency[1];
                Masses[0].Frequency = 1;
                MassCount = 1;
                MassResolution = massResolution;
                MinimumAbundance = minimumAbundance;
            }

            public Helper(MassDistribution parent)
            {
                Masses = parent.Select(keyValuePair => new MassFrequency(keyValuePair.Key,  keyValuePair.Value)).ToArray();
                MassCount = Masses.Length;
                MassResolution = parent.MassResolution;
                MinimumAbundance = parent.MinimumAbundance;
            }

            public Helper(IEnumerable<MassFrequency> val, double massResolution, double minimumAbundance)
            {
                Masses = val.ToArray();
                MassCount = Masses.Length;
                MassResolution = massResolution;
                MinimumAbundance = minimumAbundance;
            }

            public Helper(MassFrequency[] masses, int masscount, double massResolution, double minimumAbundance)
            {
                Masses = masses;
                MassCount = masscount; // Tail of array may me unused after binning, but we don't reallocate for best speed
                MassResolution = massResolution;
                MinimumAbundance = minimumAbundance;
            }

            public Helper Add(IDictionary<double, double> rhs)
            {
                return Add(rhs.Select(keyValuePair => new MassFrequency(keyValuePair.Key,  keyValuePair.Value)), rhs.Count);
            }

            public Helper Add(Helper rhs)
            {
                return Add(rhs.Masses, rhs.MassCount);
            }

            private Helper Add(IEnumerable<MassFrequency> rhs, int rhsCount)
            {
                var list = new MassFrequency[MassCount * rhsCount];
                var i = 0;
                foreach (var rhsEntry in rhs)
                {
                    if (rhsCount-- <= 0)
                        break;
                    for (var j = MassCount - 1; j >= 0; j--)
                    {
                        list[i].Mass = Masses[j].Mass + rhsEntry.Mass;
                        list[i++].Frequency = Masses[j].Frequency * rhsEntry.Frequency;
                    }
                }
                var result = new Helper(list, list.Length, MassResolution, MinimumAbundance);
                result.ApplyBinning();
                return result;
            }

            public Helper Multiply(int factor)
            {
                if (factor == 0)
                {
                    return new Helper(MassResolution, MinimumAbundance);
                }
                if (factor == 1)
                {
                    return this;
                }
                var result = Add(Masses, MassCount);
                if (factor >= 4)
                {
                    result = result.Multiply(factor / 2);
                }
                if (factor % 2 != 0) {
                    result = result.Add(Masses, MassCount);
                }
                return result;
            }

            public void ApplyBinning()
            {
                // Sort masses in range that's actually in use
                Array.Sort(Masses, 0, MassCount);

                // Filter masses by resolution and abundance
                double curMass = 0.0;
                double curFrequency = 0.0;
                double totalAbundance = 0;
                var count = 0;
                for (var i = 0; i < MassCount; i++)
                {
                    var value = Masses[i];
                    var frequency = value.Frequency;
                    if (frequency == 0)
                        continue;

                    var mass = value.Mass;

                    // If the delta between the next mass and the current center of mass is
                    // greater than the mass resolution
                    if (mass - curMass > MassResolution)
                    {
                        // If the abundance of the current center of mass is greater than
                        // the minimum, add it to the new set
                        if (curFrequency > MinimumAbundance)
                        {
                            Masses[count].Mass = curMass;
                            Masses[count++].Frequency = curFrequency;
                            totalAbundance += curFrequency;
                        }
                        // Reset the current center of mass
                        curMass = mass;
                        curFrequency = frequency;
                    }
                    else
                    {
                        // Add the new mass and adjust the current center of mass
                        curMass = (curMass * curFrequency + mass * frequency) / (curFrequency + frequency);
                        curFrequency += frequency;
                    }
                }
                // Include the last center of mass
                if (curFrequency > MinimumAbundance)
                {
                    Masses[count].Mass = curMass;
                    Masses[count++].Frequency = curFrequency;
                    totalAbundance += curFrequency;
                }
                // If filtered abundances do not total 100%, recalculate the proportions
                if (totalAbundance != 1)
                {
                    for (var i = count - 1; i >= 0; i--)
                    {
                        Masses[i].Frequency /= totalAbundance;
                    }
                }
                MassCount = count;
            }

            // Doesn't return a Helper because it's typically the last step in creating an actual MassDistribution
            public MassDistribution OffsetAndDivide(double offset, int scale)
            {
                var map = new SortedDictionary<double, double>();
                var absScale = Math.Abs(scale); // Scale is typically a charge, which may be negative, but masses are always positive
                for (var i = MassCount; i-- > 0;)
                {
                    map.Add((Masses[i].Mass + offset) / absScale, Masses[i].Frequency);
                }
                return new MassDistribution(map, MassResolution, MinimumAbundance);
            }
        }

        /// <summary>
        /// Returns a MassDistribution which is the result of adding the specified
        /// value to each mass value, and dividing by the scale.
        /// </summary>
        public MassDistribution OffsetAndDivide(double offset, int scale) 
        {
            var map = new SortedDictionary<double,double>();
            int absScale = Math.Abs(scale); // Scale is typically a charge, which may be negative, but masses are always positive
            foreach (var entry in this) {
                map.Add((entry.Key + offset) / absScale, entry.Value);
            }
            return new MassDistribution(map, MassResolution, MinimumAbundance);
        }

        /* If this is brought into use, it must be made aware of non-protonated ions
        public MassDistribution SetCharge(int charge)
        {
            return OffsetAndDivide(AminoAcidFormulas.ProtonMass*charge, charge);
        }
        */

        /// <summary>
        /// Performs a weighted average of this MassDistribution with another.
        /// </summary>
        public MassDistribution Average(MassDistribution massDistribution, double weight)
        {
            var dict = new Dictionary<double, double>(Count);
            foreach (var entry in this)
            {
                dict.Add(entry.Key, entry.Value * (1.0 - weight));
            }
            foreach (var entry in massDistribution)
            {
                double currentAbundance;
                dict.TryGetValue(entry.Key, out currentAbundance);
                dict[entry.Key] = currentAbundance + entry.Value*weight;
            }
            return NewInstance(dict, MassResolution, MinimumAbundance);
        }

        public double AverageMass
        { 
            get
            {
                double average = 0;
                double totalAbundance = 0;
                foreach (var entry in this)
                {
                    average += entry.Key * entry.Value;
                    totalAbundance += entry.Value;
                }
                return average / totalAbundance;
            }
        }

        public double MinMass
        { 
            get { return Keys.Aggregate(double.MaxValue, Math.Min); }
        }

        public double MaxMass
        { 
            get { return Keys.Aggregate(0.0, Math.Max); }
        }

        public double MostAbundanceMass
        {
            get
            {
                double monoMass = 0;
                double maxAbundance = 0;
                foreach (var entry in this)
                {
                    if (entry.Value > maxAbundance)
                    {
                        monoMass = entry.Key;
                        maxAbundance = entry.Value;
                    }
                }
                return monoMass;
            }
        }

        /// <summary>
        /// Increases the abundance of a specified mass within the distribution,
        /// adjusting the abundance of the other isotopes based on their existing
        /// proportions.
        /// </summary>
        /// <param name="mass">The mass to adjust</param>
        /// <param name="abundance">The amount by which to increase its existing abundance</param>
        /// <returns>A new adjusted mass distribution</returns>
        public MassDistribution Enrich(double mass, double abundance)
        {
            double totalAbundance = Values.Sum();
            double currentAbundance;
            TryGetValue(mass, out currentAbundance);
            if (abundance >= totalAbundance - currentAbundance)
            {
                return new MassDistribution(new SortedDictionary<double, double>{{mass, 1.0}}, MassResolution, MinimumAbundance);
            }
            double newAbundance = currentAbundance +
                                  abundance * totalAbundance * totalAbundance /
                                  (totalAbundance - currentAbundance - abundance);
            var map = new Dictionary<Double, Double>(this);
            map[mass] = newAbundance;
            return NewInstance(map, MassResolution, MinimumAbundance);
        }

        /// <summary>
        /// Enriches a specified mass within the distribution to a specified
        /// abundance level, adjusting the abundance of the other isotopes based on
        /// their existing proportions
        /// </summary>
        /// <param name="mass">The mass to adjust</param>
        /// <param name="abundance">The desired final abundance for the mass</param>
        /// <returns>A new adjusted mass distribution</returns>
        public MassDistribution SetAbundance(double mass, double abundance)
        {
            double currentAbundance;
            TryGetValue(mass, out currentAbundance);
            return Enrich(mass, abundance - currentAbundance);
        }

        public List<KeyValuePair<double,double>> MassesSortedByAbundance()
        {
            var result = new List<KeyValuePair<double, double>>(this);
            result.Sort((a,b)=>b.Value.CompareTo(a.Value));
            return result;
        }

        public static MassDistribution NewInstance(IDictionary<double,double> values, 
            double massResolution, double minimumAbundance)
        {
            var arrayValues = new MassFrequency[values.Count];
            var i = 0;
            foreach (var value in values)
            {
                arrayValues[i].Mass = value.Key;
                arrayValues[i++].Frequency = value.Value;
            }
            var helper = new Helper(arrayValues, arrayValues.Length, massResolution, minimumAbundance);
            helper.ApplyBinning();
            return new MassDistribution(new SortedDictionary<double, double>(helper.Masses.Take(helper.MassCount).ToDictionary(m => m.Mass, m => m.Frequency)), massResolution, minimumAbundance);
        }

        public struct MassFrequency : IComparable<MassFrequency>
        {
            public double Mass { get; set; }
            public double Frequency { get; set; }

            public MassFrequency(double mass, double frequency) : this()
            {
                Mass = mass;
                Frequency = frequency;
            }

            public int CompareTo(MassFrequency mf)
            {
                return Mass.CompareTo(mf.Mass);
            }
        }

    }
}
