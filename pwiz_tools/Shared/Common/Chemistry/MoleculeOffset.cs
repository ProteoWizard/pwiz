using System.Collections.Generic;
using System.Linq;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Chemistry
{
    public class MoleculeMassOffset : Immutable
    {
        public static readonly MoleculeMassOffset EMPTY = new MoleculeMassOffset(Molecule.Empty, 0);
        public MoleculeMassOffset(Molecule molecule, double massOffset)
        {
            Molecule = molecule;
            MassOffset = massOffset;
        }

        public Molecule Molecule { get; private set; }
        public double MassOffset { get; private set; }

        public MoleculeMassOffset Add(MoleculeMassOffset moleculeMassOffset)
        {
            var dictionary = new Dictionary<string, int>();
            foreach (var entry in Molecule.Concat(moleculeMassOffset.Molecule))
            {
                int count;
                dictionary.TryGetValue(entry.Key, out count);
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
            return new MoleculeMassOffset(Molecule.FromDict(dictionary), MassOffset + moleculeMassOffset.MassOffset);
        }
    }
}
