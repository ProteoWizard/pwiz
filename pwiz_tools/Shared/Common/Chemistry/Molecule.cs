using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Common.Chemistry
{
    public class Molecule : ImmutableDictionary<String, int>
    {
        public static Molecule Parse(String formula)
        {
            var result = new SortedDictionary<String, int>();
            String currentElement = null;
            int currentQuantity = 0;
            for (int ich = 0; ich < formula.Length; ich++)
            {
                char ch = formula[ich];
                if (Char.IsDigit(ch))
                {
                    currentQuantity = currentQuantity * 10 + (ch - '0');
                }
                else if (Char.IsUpper(ch))
                {
                    if (currentElement != null)
                    {
                        if (currentQuantity == 0)
                        {
                            currentQuantity = 1;
                        }
                        if (result.ContainsKey(currentElement))
                        {
                            result[currentElement] = result[currentElement] + currentQuantity;
                        }
                        else
                        {
                            result[currentElement] = currentQuantity;
                        }
                    }
                    currentQuantity = 0;
                    currentElement = "" + ch;
                }
                else if (Char.IsLower(ch))
                {
                    currentElement = currentElement + ch;
                }
            }
            if (currentElement != null)
            {
                if (currentQuantity == 0)
                {
                    currentQuantity = 1;
                }
                if (result.ContainsKey(currentElement))
                {
                    result[currentElement] = result[currentElement] + currentQuantity;
                }
                else
                {
                    result[currentElement] = currentQuantity;
                }
            }
            return new Molecule(result);
        }
        public Molecule SetAtomCount(String element, int count)
        {
            var dict = new Dictionary<String, int>(this);
            dict[element] = count;
            return new Molecule(dict);
        }
        private Molecule(IDictionary<String,int> formula) : base(formula)
        {
        }
        public override String ToString()
        {
            var result = new StringBuilder();
            foreach (var entry in this)
            {
                result.Append(entry.Key);
                if (entry.Value != 1)
                {
                    result.Append(entry.Value);
                }
            }
            return result.ToString();
        }
    }
}
