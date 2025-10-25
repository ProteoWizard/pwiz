using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class MassDistributionTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestMassDistributionSpeed()
        {
            for (int i = 0; i < 5; i++)
            {
                Console.Out.WriteLine("Current Implementation: {0}", TimeAction(MassDistribution.NewInstance));
                Console.Out.WriteLine("Alt Implementation: {0}", TimeAction(Common.Chemistry.Alt.MassDistribution.NewInstance));
                Console.Out.WriteLine("ValueTuple: {0}", TimeAction(Common.Chemistry.WithValueTuple.MassDistribution.NewInstance));
                Console.Out.WriteLine("KeyValuePair: {0}", TimeAction(Common.Chemistry.WithKeyValuePair.MassDistribution.NewInstance));
                Console.Out.WriteLine("JustValueTuple: {0}", TimeAction(Common.Chemistry.JustValueTuple.MassDistribution.NewInstance));
                Console.Out.WriteLine("SeparateArrays: {0}", TimeAction(Common.Chemistry.SeparateArrays.MassDistribution.NewInstance));
                Console.Out.WriteLine("MassFrequency: {0}", TimeAction(Common.Chemistry.MassFrequency.MassDistribution.NewInstance));
            }
        }

        [TestMethod]
        public void TestAltMassDistributionSpeed()
        {
            TimeAction(pwiz.Common.Chemistry.Alt.MassDistribution.NewInstance);
        }

        [TestMethod]
        public void TestValueTupleMassDistributionSpeed()
        {
            TimeAction(Common.Chemistry.WithValueTuple.MassDistribution.NewInstance);
        }

        [TestMethod]
        public void TestKeyValuePairMassDistributionSpeed()
        {
            TimeAction(Common.Chemistry.WithKeyValuePair.MassDistribution.NewInstance);
        }

        [TestMethod]
        public void TestJustValueTuple()
        {
            TimeAction(Common.Chemistry.JustValueTuple.MassDistribution.NewInstance);
        }

        [TestMethod]
        public void TestSeparateArrays()
        {
            TimeAction(Common.Chemistry.SeparateArrays.MassDistribution.NewInstance);
        }

        [TestMethod]
        public void TestMassFrequency()
        {
            TimeAction(Common.Chemistry.MassFrequency.MassDistribution.NewInstance);
        }

        private TimeSpan TimeAction<T>(Func<IEnumerable<KeyValuePair<double, double>>, double, double, T> newInstanceFunc) where T:IMassDistribution<T>
        {
            var massResolution = .001;
            var minAbundance = .00001;
            var empty = newInstanceFunc(new KeyValuePair<double, double>[] { new KeyValuePair<double, double>(0, 1) }, massResolution, minAbundance);
            var isotopeAbundances = IsotopeAbundances.Default.Keys.ToDictionary(key => key,
                key => newInstanceFunc(IsotopeAbundances.Default[key], massResolution, minAbundance));
            var proteinSequence =
                new string(
                    "Skyline cheerily displays peptide graphs with style yet angrily crashes when handling massively large replicate datasets"
                        .Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());
            var formula = AminoAcidFormulas.Default.GetFormula(proteinSequence);
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            for (int i = 0; i < 10000; i++)
            {
                var abundances = isotopeAbundances;
                var result = empty;
                foreach (var element in formula)
                {
                    result = result.Add(empty.Add(abundances[element.Key]).Multiply(element.Value));
                }
                Assert.IsNotNull(result);
            }

            return stopWatch.Elapsed;
        }
    }
}
