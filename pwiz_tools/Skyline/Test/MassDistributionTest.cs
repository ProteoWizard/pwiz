using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class MassDistributionTest : AbstractUnitTest
    {
        private static readonly MassDistribution EmptyDistribution = new MassDistribution(0.001, 0.00001);

        private static readonly pwiz.Common.Chemistry.Alt.MassDistribution EmptyAltDistribution =
            new pwiz.Common.Chemistry.Alt.MassDistribution(0.001, 0.00001);

        private static readonly Dictionary<string, pwiz.Common.Chemistry.Alt.MassDistribution> _altAbundances =
            IsotopeAbundances.Default.Keys.ToDictionary(key=>key, key =>
                pwiz.Common.Chemistry.Alt.MassDistribution.NewInstance(IsotopeAbundances.Default[key], 0, 0));

        private static readonly pwiz.Common.Chemistry.WithValueTuple.MassDistribution EmptyValueTupleDistribution =
            new pwiz.Common.Chemistry.WithValueTuple.MassDistribution(0.001, 0.00001);
        private static readonly Dictionary<string, pwiz.Common.Chemistry.WithValueTuple.MassDistribution> _valueTupleAbundances =
            IsotopeAbundances.Default.Keys.ToDictionary(key => key, key =>
                pwiz.Common.Chemistry.WithValueTuple.MassDistribution.NewInstance(IsotopeAbundances.Default[key], 0, 0));
        private static readonly pwiz.Common.Chemistry.WithKeyValuePair.MassDistribution EmptyKeyValuePairDistribution =
            new pwiz.Common.Chemistry.WithKeyValuePair.MassDistribution(0.001, 0.00001);
        private static readonly Dictionary<string, pwiz.Common.Chemistry.WithKeyValuePair.MassDistribution> _keyValuePairAbundances =
            IsotopeAbundances.Default.Keys.ToDictionary(key => key, key =>
                pwiz.Common.Chemistry.WithKeyValuePair.MassDistribution.NewInstance(IsotopeAbundances.Default[key], 0, 0));


        private static readonly pwiz.Common.Chemistry.JustValueTuple.MassDistribution EmptyJustValueTupleDistribution =
            new pwiz.Common.Chemistry.JustValueTuple.MassDistribution(0.001, 0.00001);
        private static readonly Dictionary<string, pwiz.Common.Chemistry.JustValueTuple.MassDistribution> _justValueTupleAbundances =
            IsotopeAbundances.Default.Keys.ToDictionary(key => key, key =>
                pwiz.Common.Chemistry.JustValueTuple.MassDistribution.NewInstance(IsotopeAbundances.Default[key], 0, 0));

        private static readonly pwiz.Common.Chemistry.SeparateArrays.MassDistribution EmptySeparateArraysDistribution =
            new pwiz.Common.Chemistry.SeparateArrays.MassDistribution(0.001, 0.00001);
        private static readonly Dictionary<string, pwiz.Common.Chemistry.SeparateArrays.MassDistribution> SeparateArraysAbundances =
            IsotopeAbundances.Default.Keys.ToDictionary(key => key, key =>
                pwiz.Common.Chemistry.SeparateArrays.MassDistribution.NewInstance(IsotopeAbundances.Default[key], 0, 0));
        private static readonly pwiz.Common.Chemistry.MassFrequency.MassDistribution EmptyMassFrequencyDistribution =
            new pwiz.Common.Chemistry.MassFrequency.MassDistribution(0.001, 0.00001);
        private static readonly Dictionary<string, pwiz.Common.Chemistry.MassFrequency.MassDistribution> MassFrequencyAbundances =
            IsotopeAbundances.Default.Keys.ToDictionary(key => key, key =>
                pwiz.Common.Chemistry.MassFrequency.MassDistribution.NewInstance(IsotopeAbundances.Default[key], 0, 0));


        [TestMethod]
        public void TestMassDistributionSpeed()
        {
            TimeAction(mol => GetMassDistribution(mol));
        }

        [TestMethod]
        public void TestAltMassDistributionSpeed()
        {
            TimeAction(mol=>GetAltMassDistribution(mol));
        }

        [TestMethod]
        public void TestValueTupleMassDistributionSpeed()
        {
            TimeAction(mol => GetValueTupleMassDistribution(mol));
        }

        [TestMethod]
        public void TestKeyValuePairMassDistributionSpeed()
        {
            TimeAction(mol => GetKeyValuePairMassDistribution(mol));
        }

        [TestMethod]
        public void TestJustValueTuple()
        {
            TimeAction(mol=> GetJustValueTupleMassDistribution(mol));
        }

        [TestMethod]
        public void TestSeparateArrays()
        {
            TimeAction(mol=> GetSeparateArraysMassDistribution(mol));
        }

        [TestMethod]
        public void TestMassFrequency()
        {
            TimeAction(mol=>GetMassFrequencyMassDistribution(mol));
        }

        private void TimeAction(Action<Molecule> action)
        {
            var proteinSequence =
                new string(
                    "Skyline cheerily displays peptide graphs with style yet angrily crashes when handling massively large replicate datasets"
                        .Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());
            var formula = AminoAcidFormulas.Default.GetFormula(proteinSequence);
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            for (int i = 0; i < 10000; i++)
            {
                action(formula);
            }

            var elapsed = stopWatch.Elapsed;
            Console.Out.WriteLine("Elapsed time: {0}", elapsed);
        }

        private MassDistribution GetMassDistribution(Molecule molecule)
        {
            var abundances = IsotopeAbundances.Default;
            var result = EmptyDistribution;
            foreach (var element in molecule)
            {
                result = result.Add(EmptyDistribution.Add(abundances[element.Key]).Multiply(element.Value));
            }

            return result;
        }

        private pwiz.Common.Chemistry.Alt.MassDistribution GetAltMassDistribution(Molecule molecule)
        {
            var abundances = _altAbundances;
            var result = EmptyAltDistribution;
            foreach (var element in molecule)
            {
                result = result.Add(EmptyAltDistribution.Add(abundances[element.Key]).Multiply(element.Value));
            }

            return result;

        }

        private pwiz.Common.Chemistry.WithValueTuple.MassDistribution GetValueTupleMassDistribution(Molecule molecule)
        {
            var abundances = _valueTupleAbundances;
            var result = EmptyValueTupleDistribution;
            foreach (var element in molecule)
            {
                result = result.Add(EmptyValueTupleDistribution.Add(abundances[element.Key]).Multiply(element.Value));
            }

            return result;

        }
        private pwiz.Common.Chemistry.WithKeyValuePair.MassDistribution GetKeyValuePairMassDistribution(Molecule molecule)
        {
            var abundances = _keyValuePairAbundances;
            var result = EmptyKeyValuePairDistribution;
            foreach (var element in molecule)
            {
                result = result.Add(EmptyKeyValuePairDistribution.Add(abundances[element.Key]).Multiply(element.Value));
            }

            return result;

        }
        private pwiz.Common.Chemistry.JustValueTuple.MassDistribution GetJustValueTupleMassDistribution(Molecule molecule)
        {
            var abundances = _justValueTupleAbundances;
            var result = EmptyJustValueTupleDistribution;
            foreach (var element in molecule)
            {
                result = result.Add(EmptyJustValueTupleDistribution.Add(abundances[element.Key]).Multiply(element.Value));
            }

            return result;

        }
        private pwiz.Common.Chemistry.SeparateArrays.MassDistribution GetSeparateArraysMassDistribution(Molecule molecule)
        {
            var abundances = SeparateArraysAbundances;
            var result = EmptySeparateArraysDistribution;
            foreach (var element in molecule)
            {
                result = result.Add(EmptySeparateArraysDistribution.Add(abundances[element.Key]).Multiply(element.Value));
            }

            return result;

        }

        private pwiz.Common.Chemistry.MassFrequency.MassDistribution GetMassFrequencyMassDistribution(Molecule molecule)
        {
            var abundances = MassFrequencyAbundances;
            var result = EmptyMassFrequencyDistribution;
            foreach (var element in molecule)
            {
                result = result.Add(EmptyMassFrequencyDistribution.Add(abundances[element.Key]).Multiply(element.Value));
            }

            return result;

        }

    }
}
