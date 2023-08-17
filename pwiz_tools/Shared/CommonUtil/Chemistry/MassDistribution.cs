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
using System.Collections;
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
    public class MassDistribution : IReadOnlyList<KeyValuePair<double, double>>
    {
        private readonly ImmutableList<double> _masses;
        private readonly ImmutableList<double> _frequencies;
        /// <summary>
        /// Constructs a MassDistribution consisting only of a mass of 0 with 100% probability.
        /// </summary>
        public MassDistribution(double massResolution, double minimumAbundance)
            : this(ImmutableList.Singleton(0.0), ImmutableList.Singleton(1.0), massResolution, minimumAbundance)
        {
        }

        /// <summary>
        /// Private constructor used by NewInstance to create all useful MassDistributions.
        /// </summary>
        private MassDistribution(
            ImmutableList<double> masses,
            ImmutableList<double> frequencies,
            double massResolution, 
            double minimumAbundance)
        {
            _masses = masses;
            _frequencies = frequencies;
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
            var arrayCount = PartialAdd(0, Count, rhs);
            return ApplyBinning(arrayCount.Item1, arrayCount.Item2);
        }

        private Tuple<KeyValuePair<double, double>[], int> PartialAdd(int start, int end, MassDistribution rhs)
        {
            if (end == start + 1)
            {
                var myMass = _masses[start];
                var myFrequency = _frequencies[start];
                var array = new KeyValuePair<double, double>[rhs.Count];
                for (int i = 0; i < rhs.Count; i++)
                {
                    array[i] = new KeyValuePair<double, double>(myMass + rhs._masses[i], myFrequency * rhs._frequencies[i]);
                }
                return Tuple.Create(array, array.Length);
            }
            var mid = (start + end) / 2;
            var left = PartialAdd(start, mid, rhs);
            var right = PartialAdd(mid, end, rhs);
            return Merge(left, right);
        }

        private static Tuple<KeyValuePair<double, double>[], int> Merge(Tuple<KeyValuePair<double, double>[], int> list1,
            Tuple<KeyValuePair<double, double>[], int> list2)
        {
            int resultCount = 0;
            var resultArray = new KeyValuePair<double, double>[list1.Item2 + list2.Item2];
            int index1 = 0;
            int index2 = 0;
            while (true)
            {
                int compare;
                if (index1 < list1.Item2)
                {
                    if (index2 < list2.Item2)
                    {
                        compare = list1.Item1[index1].Key.CompareTo(list2.Item1[index2].Key);
                    }
                    else
                    {
                        compare = -1;
                    }
                }
                else
                {
                    if (index2 < list2.Item2)
                    {
                        compare = 1;
                    }
                    else
                    {
                        break;
                    }
                }
                if (compare < 0)
                {
                    resultArray[resultCount] = list1.Item1[index1];
                }
                else if (compare > 0)
                {
                    resultArray[resultCount] = list2.Item1[index2];
                }
                else
                {
                    resultArray[resultCount] = new KeyValuePair<double, double>(list1.Item1[index1].Key, list1.Item1[index1].Value + list2.Item1[index2].Value);
                }
                resultCount++;
                if (compare <= 0)
                {
                    index1++;
                }
                if (compare >= 0)
                {
                    index2++;
                }
            }
            return Tuple.Create(resultArray, resultCount);
        }

        public Dictionary<double, double> ToDictionary()
        {
            var dict = new Dictionary<double, double>(Count);
            for (int i = 0; i < Count; i++)
            {
                dict.Add(_masses[i], _frequencies[i]);
            }
            return dict;
        }

        public ImmutableList<double> Keys { get { return _masses; } }

        public ImmutableList<double> Values { get { return _frequencies; } }

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
            // First calculate two times this
            var result = Add(this);
            if (factor >= 4)
            {
                result = result.Multiply(factor / 2);
            }
            if (factor % 2 != 0)
            {
                // If factor is odd, then we still have to add one more "this" in.
                result = result.Add(this);
            }
            return result;
        }

        private MassDistribution ApplyBinning(KeyValuePair<double, double>[] array, int count)
        {
            List<double> newMasses = new List<double>(count);
            List<double> newFrequencies = new List<double>(count);
            // Filter masses by resolution and abundance
            double curMass = 0.0;
            double curFrequency = 0.0;
            double totalAbundance = 0;
            for (int i = 0; i < count; i++)
            {
                var frequency = array[i].Value;
                if (frequency.Equals(0.0))
                    continue;

                var mass = array[i].Key;

                // If the delta between the next mass and the current center of mass is
                // greater than the mass resolution
                if (mass - curMass > MassResolution)
                {
                    // If the abundance of the current center of mass is greater than
                    // the minimum, add it to the new set
                    if (curFrequency > MinimumAbundance)
                    {
                        newMasses.Add(curMass);
                        newFrequencies.Add(curFrequency);
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
                newMasses.Add(curMass);
                newFrequencies.Add(curFrequency);
                totalAbundance += curFrequency;
            }
            // If filtered abundances do not total 100%, recalculate the proportions
            if (!totalAbundance.Equals(1.0))
            {
                for (int i = 0; i < newFrequencies.Count; i++)
                {
                    newFrequencies[i] = newFrequencies[i] / totalAbundance;
                }
            }
            return new MassDistribution(ImmutableList.ValueOf(newMasses), ImmutableList.ValueOf(newFrequencies), MassResolution, MinimumAbundance);
        }

        public int Count { get { return _masses.Count; } }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<double, double>> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        public KeyValuePair<double, double> this[int index]
        {
            get { return new KeyValuePair<double, double>(_masses[index], _frequencies[index]); }
        }

        /// <summary>
        /// Returns a MassDistribution which is the result of adding the specified
        /// value to each mass value, and dividing by the scale.
        /// </summary>
        public MassDistribution OffsetAndDivide(double offset, int scale) 
        {
            int absScale = Math.Abs(scale); // Scale is typically a charge, which may be negative, but masses are always positive
            var newMasses = ImmutableList.ValueOf(_masses.Select(mass => (mass + offset) / absScale));
            return new MassDistribution(
                newMasses, _frequencies, MassResolution, MinimumAbundance);
        }

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
            get { return _masses[0]; }
        }

        public double MaxMass
        { 
            get { return _masses[_masses.Count - 1]; }
        }

        public double MostAbundanceMass
        {
            get
            {
                double monoMass = 0;
                double maxAbundance = 0;
                for (int i = 0; i < Count; i++)
                {
                    if (_frequencies[i] > maxAbundance)
                    {
                        monoMass = _masses[i];
                        maxAbundance = _frequencies[i];
                    }
                }
                return monoMass;
            }
        }

        // ReSharper disable UnusedMethodReturnValue.Local
        private bool TryGetValue(double mass, out double frequency )
        {
            int i = CollectionUtil.BinarySearch(_masses, mass);
            if (i < 0)
            {
                frequency = 0;
                return false;
            }
            frequency = _frequencies[i];
            return true;
        }
        // ReSharper restore UnusedMethodReturnValue.Local

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
            double totalAbundance = _frequencies.Sum();
            double currentAbundance;
            TryGetValue(mass, out currentAbundance);
            if (abundance >= totalAbundance - currentAbundance)
            {
                return new MassDistribution(ImmutableList.Singleton(mass), ImmutableList.Singleton(1.0), MassResolution, MinimumAbundance);
            }
            double newAbundance = currentAbundance +
                                  abundance * totalAbundance * totalAbundance /
                                  (totalAbundance - currentAbundance - abundance);
            var map = ToDictionary();
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
            var result = this.ToList();
            result.Sort((a,b)=>b.Value.CompareTo(a.Value));
            return result;
        }

        public static MassDistribution NewInstance(IEnumerable<KeyValuePair<double,double>> values, 
            double massResolution, double minimumAbundance)
        {
            var array = values.ToArray();
            Array.Sort(array, (kvp1, kvp2)=>kvp1.Key.CompareTo(kvp2.Key));
            return new MassDistribution(massResolution, minimumAbundance)
                .ApplyBinning(array, array.Length);
        }
    }
}
