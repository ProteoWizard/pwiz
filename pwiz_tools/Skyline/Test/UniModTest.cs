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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class UniModTest : AbstractUnitTest
    {
        private const string STATIC_LIST_FILE = "UniModStaticList.xml";
        private const string HEAVY_LIST_FILE = "UniModHeavyList.xml";

        [TestMethod, NoParallelTesting(TestExclusionReason.SHARED_DIRECTORY_WRITE)] // Writes XML to the project
        public void TestUniMod()
        {
            // UpdateTestXML is used to update the test files if the modifications in UniMod.cs
            // have changed. Before UpdateTestXML is called, TestUniMod should pass with the new changes 
            // to make sure we haven't lost/broken any modifications from earlier versions of UniMod.cs.
            // Note: The test will always run against the last build XML. Run twice when updating.
            UpdateTestXML();

            foreach (StaticMod mod in UniMod.DictUniModIds.Values)
            {
                // UniModCompiler should not set the masses.
                if (!ParsedMolecule.IsNullOrEmpty(mod.ParsedMolecule))
                {
                    Assert.AreEqual(mod.MonoisotopicMass,
                        SequenceMassCalc.FormulaMass(BioMassCalc.MONOISOTOPIC, mod.ParsedMolecule, SequenceMassCalc.MassPrecision));
                    Assert.AreEqual(mod.AverageMass,
                        SequenceMassCalc.FormulaMass(BioMassCalc.AVERAGE, mod.ParsedMolecule, SequenceMassCalc.MassPrecision));
                }
                else if (mod.LabelAtoms != LabelAtoms.None && mod.AAs is { Length: 1 })
                {
                    char aa = mod.AAs[0];
                    Assert.AreEqual(mod.MonoisotopicMass,
                        new SequenceMassCalc(MassType.Monoisotopic).GetModMass(aa, mod));
                    Assert.AreEqual(mod.AverageMass,
                        new SequenceMassCalc(MassType.Average).GetModMass(aa, mod));
                }
                else
                {
                    Assert.IsNull(mod.MonoisotopicMass);
                    Assert.IsNull(mod.AverageMass);
                }
                // Everything amino acid/terminus that is part of the modification should be present in   
                // the name of the modification.
                var aasAndTermInName = mod.Name.Split(new[] { ' ' }, 2)[1];
                if (mod.Terminus != null)
                    Assert.IsTrue(aasAndTermInName.Contains(mod.Terminus.Value.ToString()));
                if (mod.AAs != null)
                {
                    foreach (char aa in mod.AminoAcids)
                    {
                        Assert.IsTrue(aasAndTermInName.Contains(aa));
                    }
                }
                // Should not have label atoms if no amino acids are listed.
                if(!Equals(mod.LabelAtoms, LabelAtoms.None))
                    Assert.IsTrue(mod.AAs != null);
            }

            // Testing ValidateID.
            var phospho = UniMod.DictStructuralModNames["Phospho (ST)"];
            Assert.IsTrue(UniMod.ValidateID(phospho.ChangeExplicit(true)));
            Assert.IsTrue(UniMod.ValidateID(phospho.ChangeVariable(true)));
            Assert.IsFalse(UniMod.ValidateID((StaticMod)phospho.ChangeName("Phospho")));

            StreamReader staticReader = new StreamReader(GetTestStream(STATIC_LIST_FILE));
            string staticMods = staticReader.ReadToEnd();
            staticReader.Close();
            AssertEx.DeserializeNoError<StaticModList>(staticMods, false);

            StreamReader heavyReader = new StreamReader(GetTestStream(HEAVY_LIST_FILE));
            string heavyMods = heavyReader.ReadToEnd();
            heavyReader.Close();
            AssertEx.DeserializeNoError<HeavyModList>(heavyMods, false);
        }

        /// <summary>
        /// The obo ids UniModData.cs does not carry, each with the reason. Should change only
        /// with a regenerated UniModData.cs, whose trailing "//Not in table:" lines list them.
        /// </summary>
        private static readonly Dictionary<int, string> OBO_IDS_NOT_IN_UNIMOD_DATA = new Dictionary<int, string>
        {
            { 107, PROTEIN_TERMINAL_ONLY },  // FormylMet
            { 394, PROTEIN_TERMINAL_ONLY },  // GPIanchor
            { 432, PROTEIN_TERMINAL_ONLY },  // Cholesterol
            { 437, PROTEIN_TERMINAL_ONLY },  // MicrocinC7
            { 448, PROTEIN_TERMINAL_ONLY },  // Microcin
            { 2144, PROTEIN_TERMINAL_ONLY }, // Dehydromethionine
        };

        private const string PROTEIN_TERMINAL_ONLY =
            "only protein-terminal specificities, which a Skyline modification cannot express";

        private const string REGENERATE_CURE =
            "Regenerate Model\\DocSettings\\UniModData.cs with Executables\\DevTools\\UniModCompiler (see its README.md).";

        /// <summary>
        /// Holds Skyline's UniMod table to pwiz/data/common/unimod.obo, the UniMod snapshot the rest
        /// of ProteoWizard reads, so no two tools disagree on what a modification is: every entry's
        /// id is in the obo, its formula's monoisotopic mass is the obo's delta_mono_mass at six
        /// decimals, and its residues and terminus are among the obo's specificities; and every obo
        /// id the table lacks is on <see cref="OBO_IDS_NOT_IN_UNIMOD_DATA"/> with a reason.
        /// Reports every violation at once, each with its cure.
        /// <para>
        /// The mass is computed with UniMod's element masses (<see cref="UNIMOD_ELEMENT_MASSES"/>),
        /// which makes it a check of the formula's atoms. Skyline's own element masses differ from
        /// UniMod's beyond the sixth decimal for most elements outside CHNOPS and for the heavy
        /// isotopes, so a Skyline-computed mass misses the published value by 1-26e-6 Da for about
        /// 50 entries (iTRAQ, TMTpro, metal cations, halogens, Se, Hg).
        /// </para>
        /// </summary>
        [TestMethod]
        public void TestUniModAgreesWithObo()
        {
            var oboPath = GetProjectDirectory(@"..\..\pwiz\data\common\unimod.obo");
            if (oboPath == null) // running from an installed SkylineTester, without the source tree
                return;
            var oboMods = ReadOboModifications(oboPath);
            Assert.IsTrue(oboMods.Count > 1000, "Failed to read modifications from {0}", oboPath);
            var violations = new List<string>();
            var tableIds = new HashSet<int>();
            // The compiler's template adds a few Skyline modifications that are not UniMod entries
            // (Water Loss, Label:13C, ...); they carry no id
            var allMods = UniModData.UNI_MOD_DATA.Where(data => data.ID.HasValue)
                .Select(data => UniMod.GetModification(data.Name, data.Structural)).ToArray();
            var oboIdsByName = oboMods.Values.ToDictionary(m => m.Name, m => m.Id);
            var formulaModsById = allMods.Where(mod => mod.UnimodId.HasValue && !ParsedMolecule.IsNullOrEmpty(mod.ParsedMolecule))
                .GroupBy(mod => mod.UnimodId.Value).ToDictionary(g => g.Key, g => g.First());
            foreach (var mod in allMods)
            {
                // ReSharper disable once PossibleInvalidOperationException
                int id = mod.UnimodId.Value;
                tableIds.Add(id);
                if (!oboMods.TryGetValue(id, out var oboMod))
                {
                    violations.Add(string.Format("{0}: UNIMOD:{1} is not in unimod.obo", mod.Name, id));
                    continue;
                }

                // Monoisotopic mass, at the six decimals UniMod publishes
                if (!ParsedMolecule.IsNullOrEmpty(mod.ParsedMolecule))
                {
                    double mass = UniModMass(mod.ParsedMolecule.Molecule, mod.Name, violations);
                    // An isotope label of a structural parent ("Dimethyl:2H(4)") carries only the
                    // label atoms; the obo mass is then the parent's plus the label's
                    var parentMod = UniMod.IsStructuralModification(mod.Name) ? null : GetParentMod(oboMod, oboIdsByName, formulaModsById);
                    if (!IsSixDecimalMatch(mass, oboMod.MonoMass) && parentMod != null)
                        mass += UniModMass(parentMod.ParsedMolecule.Molecule, parentMod.Name, violations);
                    else
                        parentMod = null;
                    if (!IsSixDecimalMatch(mass, oboMod.MonoMass))
                    {
                        violations.Add(string.Format("{0}: formula {1}{2} has monoisotopic mass {3:F6}, unimod.obo UNIMOD:{4} has {5:F6}",
                            mod.Name, mod.Formula, parentMod != null ? " on " + parentMod.Formula : string.Empty,
                            mass, id, oboMod.MonoMass));
                    }
                }
                else if (mod.LabelAtoms != LabelAtoms.None)
                {
                    foreach (char aa in mod.AminoAcids)
                    {
                        // The label atoms the modification adds to the residue, as heavy minus light
                        double mass = UniModMass(SequenceMassCalc.GetHeavyFormula(aa, mod.LabelAtoms).Molecule, mod.Name, violations);
                        if (!IsSixDecimalMatch(mass, oboMod.MonoMass))
                        {
                            violations.Add(string.Format("{0}: label {1} on {2} has monoisotopic mass {3:F6}, unimod.obo UNIMOD:{4} has {5:F6}",
                                mod.Name, mod.LabelAtoms, aa, mass, id, oboMod.MonoMass));
                        }
                    }
                }
                else
                {
                    violations.Add(string.Format("{0}: has neither a formula nor label atoms", mod.Name));
                }

                // Residues and terminus
                if (mod.AAs != null)
                {
                    foreach (char aa in mod.AminoAcids.Where(aa => !oboMod.Sites.Contains(aa.ToString())))
                    {
                        violations.Add(string.Format("{0}: residue {1} is not among the unimod.obo UNIMOD:{2} sites {3}",
                            mod.Name, aa, id, string.Join(@",", oboMod.Sites)));
                    }
                }
                if (mod.Terminus.HasValue && !oboMod.HasTerminus(mod.Terminus.Value))
                {
                    violations.Add(string.Format("{0}: {1}-terminus is not among the unimod.obo UNIMOD:{2} specificities",
                        mod.Name, mod.Terminus.Value, id));
                }
            }

            foreach (var oboMod in oboMods.Values.Where(m => !tableIds.Contains(m.Id) && !OBO_IDS_NOT_IN_UNIMOD_DATA.ContainsKey(m.Id)))
            {
                violations.Add(string.Format("UNIMOD:{0} {1}: is in unimod.obo but not in UniModData.cs. {2} " +
                                             "If the compiler cannot place it, add it to OBO_IDS_NOT_IN_UNIMOD_DATA with the reason.",
                    oboMod.Id, oboMod.Name, REGENERATE_CURE));
            }
            foreach (var id in OBO_IDS_NOT_IN_UNIMOD_DATA.Keys.Where(tableIds.Contains))
            {
                violations.Add(string.Format("UNIMOD:{0}: is in UniModData.cs; remove it from OBO_IDS_NOT_IN_UNIMOD_DATA", id));
            }
            foreach (var id in OBO_IDS_NOT_IN_UNIMOD_DATA.Keys.Where(id => !oboMods.ContainsKey(id)))
            {
                violations.Add(string.Format("UNIMOD:{0}: is no longer in unimod.obo; remove it from OBO_IDS_NOT_IN_UNIMOD_DATA", id));
            }

            Assert.AreEqual(0, violations.Count,
                "UniModData.cs disagrees with {0}. {1}\n{2}", oboPath, REGENERATE_CURE, string.Join("\n", violations));
        }

        public Stream GetTestStream(string fileName)
        {
            return typeof (UniModTest).Assembly.GetManifestResourceStream(GetType().Namespace + "." + fileName);
        }

        /// <summary>
        /// UniMod's monoisotopic element masses (the umod:elements list of unimod.xml), by Skyline
        /// symbol (H' for 2H, C' for 13C, N' for 15N, O' for 18O).
        /// </summary>
        private static readonly Dictionary<string, double> UNIMOD_ELEMENT_MASSES = new Dictionary<string, double>
        {
            { @"H", 1.007825035 }, { @"H'", 2.014101779 }, { @"Li", 7.016003 }, { @"C", 12 },
            { @"C'", 13.00335483 }, { @"N", 14.003074 }, { @"N'", 15.00010897 }, { @"O", 15.99491463 },
            { @"O'", 17.9991603 }, { @"F", 18.99840322 }, { @"Na", 22.9897677 }, { @"P", 30.973762 },
            { @"S", 31.9720707 }, { @"Cl", 34.96885272 }, { @"K", 38.9637074 }, { @"Ca", 39.9625906 },
            { @"Fe", 55.9349393 }, { @"Ni", 57.9353462 }, { @"Zn", 63.9291448 }, { @"Se", 79.9165196 },
            { @"Br", 78.9183361 }, { @"Ag", 106.905092 }, { @"Hg", 201.970617 }, { @"Au", 196.966543 },
            { @"I", 126.904473 }, { @"Mo", 97.9054073 }, { @"Cu", 62.9295989 }, { @"B", 11.0093055 },
            { @"As", 74.9215942 }, { @"Cd", 113.903357 }, { @"Cr", 51.9405098 }, { @"Co", 58.9331976 },
            { @"Mn", 54.9380471 }, { @"Mg", 23.9850423 }, { @"Pd", 105.903478 }, { @"Al", 26.9815386 },
            { @"Pt", 194.964766 }, { @"Ru", 101.9043485 }, { @"Si", 27.9769271 },
        };

        private static double UniModMass(Molecule molecule, string modName, List<string> violations)
        {
            double mass = 0;
            foreach (var atom in molecule)
            {
                if (UNIMOD_ELEMENT_MASSES.TryGetValue(atom.Key, out var elementMass))
                    mass += elementMass * atom.Value;
                else
                    violations.Add(string.Format("{0}: element {1} is not a UniMod element", modName, atom.Key));
            }
            return mass;
        }

        /// <summary>
        /// True when <paramref name="mass"/> rounds to <paramref name="published"/>, a value UniMod
        /// rounded to six decimals. Compared with a half-unit tolerance rather than Math.Round, which
        /// rounds a midpoint to even and is at the mercy of the last binary digit.
        /// </summary>
        private static bool IsSixDecimalMatch(double mass, double published)
        {
            return Math.Abs(mass - published) <= 0.5e-6 + 1e-9;
        }

        private static StaticMod GetParentMod(OboModification oboMod, Dictionary<string, int> oboIdsByName,
            Dictionary<int, StaticMod> formulaModsById)
        {
            int colon = oboMod.Name.IndexOf(':');
            if (colon == -1 || !oboIdsByName.TryGetValue(oboMod.Name.Substring(0, colon), out int parentId))
                return null;
            formulaModsById.TryGetValue(parentId, out var parentMod);
            return parentMod;
        }

        /// <summary>
        /// Reads the record id, name, monoisotopic delta mass and specificities of each unimod.obo
        /// term. Read directly rather than through Pwiz.Data.Common.Unimod, which the Skyline test
        /// projects do not reference and which keeps one site per spec_N key number.
        /// </summary>
        private static Dictionary<int, OboModification> ReadOboModifications(string path)
        {
            var mods = new Dictionary<int, OboModification>();
            OboModification mod = null;
            var xref = new Regex(@"^xref: (\S+) ""(.*)""$");
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith(@"["))
                {
                    mod = line == @"[Term]" ? new OboModification() : null;
                    continue;
                }
                if (mod == null)
                    continue;
                if (line.StartsWith(@"name: "))
                {
                    mod.Name = line.Substring(@"name: ".Length);
                    continue;
                }
                var match = xref.Match(line);
                if (!match.Success)
                    continue;
                string key = match.Groups[1].Value, value = match.Groups[2].Value;
                if (key == @"record_id")
                {
                    mod.Id = int.Parse(value, CultureInfo.InvariantCulture);
                    mods.Add(mod.Id, mod);
                }
                else if (key == @"delta_mono_mass")
                    mod.MonoMass = double.Parse(value, CultureInfo.InvariantCulture);
                else if (key.StartsWith(@"spec_") && key.EndsWith(@"_site"))
                    mod.Sites.Add(value);
                else if (key.StartsWith(@"spec_") && key.EndsWith(@"_position"))
                    mod.Positions.Add(value);
            }
            return mods;
        }

        private class OboModification
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public double MonoMass { get; set; }
            public List<string> Sites { get; } = new List<string>();
            public List<string> Positions { get; } = new List<string>();

            /// <summary>
            /// True for a terminal site or position, protein-terminal included, as UniModCompiler
            /// matches a Skyline terminus against either.
            /// </summary>
            public bool HasTerminus(ModTerminus terminus)
            {
                string term = terminus == ModTerminus.N ? @"N-term" : @"C-term";
                return Sites.Contains(term) || Positions.Any(p => p.EndsWith(term));
            }
        }

        public void UpdateTestXML()
        {
            WriteModListXml(STATIC_LIST_FILE, "StaticModList", new[] { UniMod.DictStructuralModNames, UniMod.DictHiddenStructuralModNames });
            WriteModListXml(HEAVY_LIST_FILE, "HeavyModList", new[] { UniMod.DictIsotopeModNames, UniMod.DictHiddenIsotopeModNames });
        }

        private static void WriteModListXml(string name, string tagName, IEnumerable<Dictionary<string, StaticMod>> dicts)
        {
            var modListPath = GetProjectDirectory(string.Format(@"Test\{0}", name));
            if (modListPath == null) // don't update mod list if running under SkylineTester
                return;

            FileStream fileStream;
            try
            {
                fileStream = File.Create(modListPath);
            }
            catch (Exception)
            {
                // Retry. Most likely we are in a quality run and this test is being re-run in quick succession.  Give the OS a chance to close the previous file properly.
                Thread.Sleep(1000);
                fileStream = File.Create(modListPath);
            }

            XmlWriterSettings settings = new XmlWriterSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                Indent = true,
                Encoding = new System.Text.UTF8Encoding(false) // UTF-8 without BOM
            };
            XmlWriter xmlWriter = XmlWriter.Create(fileStream, settings);
            // Some versions of ReSharper think XmlWriter.Create can return a null, others don't, disable this check to satisfy either
            // ReSharper disable PossibleNullReferenceException
            xmlWriter.WriteStartElement(tagName);
            foreach (var dict in dicts)
            {
                xmlWriter.WriteElements(dict.Values);
            }
            xmlWriter.WriteEndElement();
            xmlWriter.Close();
            // ReSharper restore PossibleNullReferenceException
            fileStream.Close();
        }

        public static string GetProjectDirectory(string relativePath)
        {
            for (string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    directory != null && directory.Length > 10;
                    directory = Path.GetDirectoryName(directory))
            {
                if (File.Exists(Path.Combine(directory, "Skyline.sln")))
                    return Path.Combine(directory, relativePath);
            }
            return null;
        }

    }
}
