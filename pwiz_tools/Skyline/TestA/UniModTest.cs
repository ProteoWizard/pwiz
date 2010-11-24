/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class UniModTest
    {
        [TestMethod]
        public void TestUniMod()
        {
            UniMod.Init();
            TestStaticMods(UniMod.StructuralMods);
            TestStaticMods(UniMod.IsotopeMods);
            TestStaticMods(UniMod.HiddenStructuralMods);
            TestStaticMods(UniMod.HiddenIsotopeMods);
        }

        private static void TestStaticMods(IEnumerable<StaticMod> mods)
        {
            foreach (StaticMod mod in mods)
            {
                // UniModCompiler should not set the masses.
                if (mod.Formula == null)
                {
                    Assert.IsNull(mod.MonoisotopicMass);
                    Assert.IsNull(mod.AverageMass);
                }
                else
                {
                    Assert.AreEqual(mod.MonoisotopicMass,
                                    SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, mod.Formula));
                    Assert.AreEqual(mod.AverageMass,
                                    SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, mod.Formula));
                }
                // Everything amino acid/terminus that is part of the modification should be present in   
                // the name of the modification.
                var aasAndTermInName = mod.Name.Split(new[] { ' ' }, 2)[1];
                if (mod.Terminus != null)
                    Assert.IsTrue(aasAndTermInName.Contains(mod.Terminus.ToString()));
                if (mod.AAs != null)
                {
                    Assert.AreEqual(mod.AminoAcids.Count(), aasAndTermInName.Length - 2);
                    foreach (char aa in mod.AminoAcids)
                    {
                        Assert.IsTrue(aasAndTermInName.Contains(aa));
                    }
                }
                // Should not have label atoms if no amino acids are listed.
                if(!Equals(mod.LabelAtoms, LabelAtoms.None))
                    Assert.IsTrue(mod.AAs != null);
            }
        }
    }
}
