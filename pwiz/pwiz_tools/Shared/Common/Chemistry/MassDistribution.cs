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
using System.Text;
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
        private MassDistribution(
            SortedDictionary<double,double> dictionary, 
            double massResolution, 
            double minimumAbundance) : base(dictionary)
        {
            MassResolution = massResolution;
            MinimumAbundance = minimumAbundance;
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
            var map = new Dictionary<Double, Double>();
            foreach (var thisEntry in this)
            {
                foreach (var thatEntry in rhs)
                {
                    double mass = thisEntry.Key + thatEntry.Key;
                    double frequency;
                    map.TryGetValue(mass, out frequency);
                    frequency += thisEntry.Value * thatEntry.Value;
                    map[mass] = frequency;
                }
            }
            return NewInstance(map, MassResolution, MinimumAbundance);
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
            MassDistribution result = Add(this);
            if (factor >= 4) {
                result = result.Multiply(factor / 2);
            }
            if (factor % 2 != 0) {
                result = result.Add(this);
            }
            return result;
        }
        /// <summary>
        /// Returns a MassDistribution which is the result of adding the specified
        /// value to each mass value, and dividing by the scale.
        /// </summary>
        public MassDistribution OffsetAndDivide(double offset, int scale) 
        {
            var map = new SortedDictionary<double,double>();
            foreach (var entry in this) {
                map.Add((entry.Key + offset) / scale, entry.Value);
            }
            return new MassDistribution(map, MassResolution, MinimumAbundance);
        }
        public MassDistribution SetCharge(int charge)
        {
            return OffsetAndDivide(AminoAcidFormulas.ProtonMass*charge, charge);
        }
        /// <summary>
        /// Performs a weighted average of this MassDistribution with another.
        /// </summary>
        public MassDistribution Average(MassDistribution massDistribution, double weight)
        {
            var dict = new Dictionary<double, double>();
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
            get
            {
                double minMass = double.MaxValue;
                foreach (var key in Keys)
                {
                    minMass = Math.Min(minMass, key);
                }
                return minMass;
            }
        }
        public double MaxMass
        { 
            get
            {
                double maxMass = 0;
                foreach (var mass in Keys)
                {
                    maxMass = Math.Max(maxMass, mass);
                }
                return maxMass;
            }
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

        public List<KeyValuePair<double,double>> MassesSortedByAbundance()
        {
            var result = new List<KeyValuePair<double, double>>(this);
            result.Sort((a,b)=>b.Value.CompareTo(a.Value));
            return result;
        }

        public static MassDistribution NewInstance(IDictionary<double,double> values, 
            double massResolution, double minimumAbundance)
        {
            var sortedKeys = values.Keys.ToArray();
            Array.Sort(sortedKeys);
            double curMass = 0.0;
            double curFrequency = 0.0;
            double totalAbundance = 0;
            var map = new Dictionary<double, double>();
            foreach (var mass in sortedKeys)
            {
                var frequency = values[mass];
                if (frequency == 0)
                {
                    continue;
                }
                if (mass - curMass > massResolution)
                {
                    if (curFrequency > minimumAbundance)
                    {
                        map.Add(curMass, curFrequency);
                        totalAbundance += curFrequency;
                    }
                    curMass = mass;
                    curFrequency = frequency;
                }
                else
                {
                    curMass = (curMass * curFrequency + mass * frequency) / (curFrequency + frequency);
                    curFrequency += frequency;
                }
            }
            if (curFrequency > minimumAbundance)
            {
                map.Add(curMass, curFrequency);
                totalAbundance += curFrequency;
            }
            if (totalAbundance != 1)
            {
                foreach (var entry in map.ToArray())
                {
                    map[entry.Key] = entry.Value / totalAbundance;
                }
            }
            return new MassDistribution(new SortedDictionary<double, double>(map), massResolution, minimumAbundance);
        }
    }
}
