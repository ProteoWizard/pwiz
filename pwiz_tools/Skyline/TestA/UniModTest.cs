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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class UniModTest : AbstractUnitTest
    {
        private const string STATIC_LIST_FILE = "UniModStaticList.xml";
        private const string HEAVY_LIST_FILE = "UniModHeavyList.xml";

        [TestMethod]
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

        public Stream GetTestStream(string fileName)
        {
            return typeof (UniModTest).Assembly.GetManifestResourceStream(GetType().Namespace + "." + fileName);
        }

        public void UpdateTestXML()
        {
            WriteModListXml(STATIC_LIST_FILE, "StaticModList", new[] { UniMod.DictStructuralModNames, UniMod.DictHiddenStructuralModNames });
            WriteModListXml(HEAVY_LIST_FILE, "HeavyModList", new[] { UniMod.DictIsotopeModNames, UniMod.DictHiddenIsotopeModNames });
        }

        private static void WriteModListXml(string name, string tagName, IEnumerable<Dictionary<string, StaticMod>> dicts)
        {
            var modListPath = GetProjectDirectory(string.Format(@"TestA\{0}", name));
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
                Indent = true
            };
            XmlWriter xmlWriter = XmlWriter.Create(fileStream, settings);
            xmlWriter.WriteStartElement(tagName);
            foreach (var dict in dicts)
            {
                xmlWriter.WriteElements(dict.Values);
            }
            xmlWriter.WriteEndElement();
            xmlWriter.Close();
            fileStream.Close();
        }

        public static string GetProjectDirectory(string relativePath)
        {
            for (string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    directory != null && directory.Length > 10;
                    directory = Path.GetDirectoryName(directory))
            {
                if (File.Exists(Path.Combine(directory, Program.Name + ".sln")))
                    return Path.Combine(directory, relativePath);
            }
            return null;
        }

    }
}
