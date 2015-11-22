/*
 * Original author: Shannon Joyner <sjoyner .at. u.washington.edu>,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for ModEquivalentTest
    /// </summary>
    [TestClass]
    public class ModEquivalentTest : AbstractUnitTest
    {
        [TestMethod]
        public void EquivalentTest()
        {
            var dictHiddenIsotopeModNames = UniMod.DictHiddenIsotopeModNames;
            var dictHiddenStructuralModNames = UniMod.DictHiddenStructuralModNames;
            var dictIsotopeModNames = UniMod.DictIsotopeModNames;
            var dictStructuralModNames = UniMod.DictStructuralModNames;

            var compareDict = new SortedDictionary<string, int>();
            var massCalc = new SequenceMassCalc(MassType.Monoisotopic);

            foreach (var dict in new[]
                        {
                            dictStructuralModNames,
                            dictIsotopeModNames,
                            dictHiddenStructuralModNames,
                            dictHiddenIsotopeModNames
                        })
            {
                var unimodArray = dict.ToArray();
                var actualDict = new SortedDictionary<string, int>();
                int actualCount = EquivalentValues(actualDict, unimodArray);

                int count, totalCount = 0;
                // Check that modifications are identified with improper name.
                StaticMod modToMatch;
                for (int i = 0; i < unimodArray.Length; i++)
                {
                    var original = unimodArray[i].Value;

                    modToMatch = (StaticMod) original.ChangeName("Test");

                    count = CountEquivalent(unimodArray, modToMatch, compareDict, i);
                    Assert.IsTrue(count >= 1);
                    totalCount += count;
                }

                Assert.AreEqual(actualCount, totalCount);
                Assert.IsTrue(ArrayUtil.EqualsDeep(actualDict.ToArray(), compareDict.ToArray()));

                compareDict.Clear();
                totalCount = 0;

                // Modify arrangement of formula.
                // Should still be able to identify correct modification.
                for (int i = 0; i < unimodArray.Length; i++)
                {
                    var original = unimodArray[i].Value;
                    modToMatch = (StaticMod)original.ChangeName("Test");
                    var formula = original.Formula;
                    if (formula != null)
                    {

                        var dictCounts = new Dictionary<string, int>();
                        massCalc.ParseModCounts(formula, dictCounts);

                        string newFormula = GetFormula(formula, dictCounts);

                        modToMatch = modToMatch.ChangeFormula(newFormula);
                    }

                    count = CountEquivalent(unimodArray, modToMatch, compareDict, i);
                    Assert.IsTrue(count >= 1);
                    totalCount += count;
                }
                Assert.IsTrue(actualCount == totalCount);
                Assert.IsTrue(ArrayUtil.EqualsDeep(actualDict.ToArray(), compareDict.ToArray()));

                compareDict.Clear();
                totalCount = 0;


                // Add and substract 5 hydrogen atoms to test complex formulas.
                for (int i = 0; i < unimodArray.Length; i++)
                {
                    var original = unimodArray[i].Value;

                    var formula = original.Formula;

                    modToMatch = (StaticMod) original.ChangeName("Test");
                    if (formula != null)
                    {

                        var dictCounts = new Dictionary<string, int>();
                        massCalc.ParseModCounts(formula, dictCounts);

                        if (dictCounts.TryGetValue("H", out count))
                            dictCounts["H"] = count + 5;
                        else
                            dictCounts["H"] = 5;

                        string newFormula = GetFormula(formula, dictCounts);
                        if (newFormula.Contains("-"))
                            newFormula = newFormula + "H5";
                        else
                            newFormula = newFormula + " - H5";
                        modToMatch = modToMatch.ChangeFormula(newFormula);
                    }
                    
                    count = CountEquivalent(unimodArray, modToMatch, compareDict, i);
                    Assert.IsTrue(count >= 1);
                    totalCount += count;
                }
                Assert.IsTrue(actualCount == totalCount);
                Assert.IsTrue(ArrayUtil.EqualsDeep(actualDict.ToArray(), compareDict.ToArray()));

                compareDict.Clear();
                totalCount = 0;

                // Change label.
                for (int i = 0; i < unimodArray.Length; i++)
                {
                    var original = unimodArray[i].Value;

                    var labelAtoms = original.LabelAtoms;

                    modToMatch = (StaticMod)original.ChangeName("Test");
                    if (labelAtoms != LabelAtoms.None && original.AAs != null && original.AAs.Length == 1)
                    {
                        double unexplainedMass;
                        string newFormula = massCalc.GetModFormula(original.AAs[0], original, out unexplainedMass);
                        Assert.AreEqual(0, unexplainedMass);
                        modToMatch = modToMatch.ChangeFormula(newFormula).ChangeLabelAtoms(LabelAtoms.None);
                    }

                    count = CountEquivalent(unimodArray, modToMatch, compareDict, i);
                    Assert.IsTrue(count >= 1);
                    totalCount += count;
                }
                Assert.IsTrue(actualCount == totalCount);
                Assert.IsTrue(ArrayUtil.EqualsDeep(actualDict.ToArray(), compareDict.ToArray()));

                // Nonexisting formulas.  
                foreach (StaticMod original in dict.Values)
                {
                    modToMatch = (StaticMod)original.ChangeName("Test");
                    if (original.Formula != null || original.Losses != null)
                        modToMatch = modToMatch.ChangeFormula("H2OCl");
                    else if (original.LabelAtoms != LabelAtoms.None)
                        modToMatch = modToMatch.ChangeFormula("H2OCl").ChangeLabelAtoms(LabelAtoms.None);

                    count = CountEquivalent(unimodArray, modToMatch, compareDict, -1);
                    Assert.AreEqual(0, count);
                }
                compareDict.Clear();
            }
        }

        private static string GetFormula(string formula, Dictionary<string, int> dictCounts)
        {
            var sbNewFormula = new StringBuilder();

            var sbSubtractFormula = new StringBuilder();
            var firstMod = new KeyValuePair<string, int>(null, 0);
            foreach (var modCount in dictCounts)
            {
                if (firstMod.Key == null)
                {
                    string atom = modCount.Key;
                    if (formula.StartsWith(atom))
                    {
                        firstMod = modCount;
                        continue;
                    }
                    firstMod = new KeyValuePair<string, int>(atom, 0);
                }
                // If the value is negative, add a minus sign.
                if (modCount.Value >= 0)
                    sbNewFormula.Append(modCount.Key).Append(modCount.Value);
                else
                    sbSubtractFormula.Append(modCount.Key).Append(Math.Abs(modCount.Value));
            }
            if (firstMod.Value != 0)
            {
               sbNewFormula.Append(firstMod.Key).Append(firstMod.Value);
            }
            if (sbSubtractFormula.Length > 0)
                sbNewFormula.Append("-").Append(sbSubtractFormula);
            return sbNewFormula.ToString();
        }

        public int EquivalentValues(SortedDictionary<string, int> equivMods, KeyValuePair<string, StaticMod>[] dict)
            {
                int totalCount = 0;
                for (int i = 0; i < dict.Length; i++)
                {
                    StaticMod modToMatch = dict[i].Value;
                    int count = CountEquivalent(dict, modToMatch, equivMods, i);
                    totalCount += count;
                }
                return totalCount;
            }
        

        /// <summary>
        /// Returns the number of StaticMod values in the dictionary that are equivalent to modToMatch.
        /// </summary>
        /// <param name="dict">Dictionary containing StaticMod values.</param>
        /// <param name="modToMatch">StaticMod we're comparing to values in dictionary.</param>
        /// <param name="equivMods">Dictionary we add count for StaticMods equivalent to modToMatch.</param>
        /// <param name="index">Index of modToMatch in dict.</param>
        /// <returns>Number of StaticMods equivalent to modToMatch. If there are no equivalent values, function will return 1.</returns>
        public int CountEquivalent(KeyValuePair<string, StaticMod>[] dict, StaticMod modToMatch, SortedDictionary<string, int> equivMods, int index)
        {
            int totalCount = 0;
            for (int i = 0; i < dict.Length; i++)
            {
                int count = 0;
                StaticMod mod = dict[i].Value;

                if (index != -1 && index == i)
                    Assert.IsTrue(mod.Equivalent(modToMatch));

                if (mod.Equivalent(modToMatch))
                    count++;
                if (!equivMods.ContainsKey(mod.Name))
                    equivMods.Add(mod.Name, count);
                else
                {
                    equivMods[mod.Name] += count;
                }
                totalCount += count;
            }

            return totalCount;
        }

       
    }
}
