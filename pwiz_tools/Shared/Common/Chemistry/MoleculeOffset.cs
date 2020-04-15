using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Chemistry
{
    public class MoleculeMassOffset : Immutable, IFormattable
    {
        public static readonly MoleculeMassOffset EMPTY = new MoleculeMassOffset(Molecule.Empty, 0);
        public MoleculeMassOffset(Molecule molecule, double massOffset)
        {
            Molecule = molecule;
            MassOffset = massOffset;
        }

        public Molecule Molecule { get; private set; }
        public double MassOffset { get; private set; }

        public MoleculeMassOffset Plus(MoleculeMassOffset moleculeMassOffset)
        {
            var newMolecule = MoleculeFromEntries(Molecule.Concat(moleculeMassOffset.Molecule));
            return new MoleculeMassOffset(newMolecule, MassOffset + moleculeMassOffset.MassOffset);
        }

        public override string ToString()
        {
            return ToString(null, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (Molecule.Count != 0)
            {
                if (Molecule.Values.Any(v => v < 0))
                {
                    var positiveMolecule = MoleculeFromEntries(Molecule.Where(entry => entry.Value > 0));
                    var negativeMolecule = MoleculeFromEntries(Molecule.Where(entry => entry.Value < 0)
                        .Select(entry => new KeyValuePair<string, int>(entry.Key, -entry.Value)));
                    stringBuilder.Append(positiveMolecule);
                    stringBuilder.Append(@"-");
                    stringBuilder.Append(negativeMolecule);
                }
                else
                {
                    stringBuilder.Append(Molecule);
                }
            }

            if (MassOffset != 0)
            {
                if (MassOffset > 0)
                {
                    stringBuilder.Append(@"+");
                }
                stringBuilder.Append(MassOffset.ToString(format, formatProvider));
            }

            return stringBuilder.ToString();
        }

        protected bool Equals(MoleculeMassOffset other)
        {
            return Molecule.Equals(other.Molecule) && MassOffset.Equals(other.MassOffset);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MoleculeMassOffset) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Molecule.GetHashCode() * 397) ^ MassOffset.GetHashCode();
            }
        }

        private static Molecule MoleculeFromEntries(IEnumerable<KeyValuePair<string, int>> entries)
        {
            var dictionary = new Dictionary<string, int>();
            foreach (var entry in entries)
            {
                int count;
                if (dictionary.TryGetValue(entry.Key, out count))
                {
                    count += entry.Value;
                    if (count == 0)
                    {
                        dictionary.Remove(entry.Key);
                    }
                    else
                    {
                        dictionary[entry.Key] = count;
                    }
                }
                else
                {
                    if (entry.Value != 0)
                    {
                        dictionary.Add(entry.Key, entry.Value);
                    }
                }
            }

            return Molecule.FromDict(dictionary);
        }
    }
}
