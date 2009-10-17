using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Common.Chemistry
{
    public class MassDistribution : ImmutableDictionary<double,double>
    {
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
        public MassDistribution OffsetAndDivide(double offset, int scale) 
        {
            var map = new SortedDictionary<double,double>();
            foreach (var entry in this) {
                map.Add((entry.Key + offset) / scale, entry.Value);
            }
            return new MassDistribution(map, MassResolution, MinimumAbundance);
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
