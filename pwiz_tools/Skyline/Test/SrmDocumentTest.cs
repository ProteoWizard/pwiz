/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Globalization;
using System.Linq;
using System.Xml;
using pwiz.Skyline.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Model.V01;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using pwiz.SkylineTestUtil.Schemas;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// This is a test class for SrmDocumentTest and is intended
    /// to contain all SrmDocumentTest Unit Tests
    /// </summary>
    [TestClass]
    public class SrmDocumentTest : AbstractUnitTest
    {
        private const double DEFAULT_RUN_LENGTH = 30.0;
        private const double DEFAULT_DWELL_TIME = 20;

        /// <summary>
        /// A test for SrmDocument deserialization of v0.1 documents and
        /// general serialization.
        /// </summary>
        [TestMethod]
        public void DocumentSerialize_0_1_Test()
        {
            AssertEx.Serializable(AssertEx.Deserialize<SrmDocument>(DOC_0_1_BOVINE), 3, AssertEx.DocumentCloned);
            AssertEx.Serializable(AssertEx.Deserialize<SrmDocument>(DOC_0_1_PEPTIDES), 3, AssertEx.DocumentCloned);
        }

        [TestMethod]
        public void ReporterIonDocumentSerializeTest()
        {
            // Test both the older (v1.9) and current ways of handling custom ions
            for (int style = 0; style < 2; style++)
            {
                string xmlText = (style==0) ? 
                    DOC_REPORTER_IONS_19 : // Old style
                    DOC_REPORTER_IONS_262; // new style
                AssertEx.ValidatesAgainstSchema(xmlText);
                SrmDocument document = AssertEx.Deserialize<SrmDocument>(xmlText);
                AssertEx.IsDocumentState(document, null, null, 1, null, 15);
                MeasuredIon customIon1 = new MeasuredIon("Water", "H3O3", null, null, Adduct.M_PLUS);
                MeasuredIon customIon2 = new MeasuredIon("Water2", "H4O3", null, null, Adduct.M_PLUS_2);
                Assert.AreEqual(customIon1,
                    document.Settings.TransitionSettings.Filter.MeasuredIons.Where(ion => ion.Name.Equals("Water"))
                        .ElementAt(0));
                for (int i = 1; i <= 2; i ++)
                {
                    Assert.AreEqual( (i==1) ? customIon1.SettingsCustomIon : customIon2.SettingsCustomIon, document.MoleculeTransitions.ElementAt(i).Transition.CustomIon);
                }
                AssertEx.Serializable(document);
            }
            AssertEx.ValidatesAgainstSchema(DOC_REPORTER_IONS_INCORRECT_NAME); // Matches schema, but
            AssertEx.DeserializeError<SrmDocument>(DOC_REPORTER_IONS_INCORRECT_NAME); // Contains a bad internal reference to a reporter ion
        }

        [TestMethod]
        public void SmallMoleculeMassesDocumentSerializeTest()
        {
            // Verify handling of 3.1 version of small molecule handling, where we assumed no multiple charges or labels
            AssertEx.ValidatesAgainstSchema(DOC_MOLECULE_MASSES_31);
            var doc = AssertEx.Deserialize<SrmDocument>(DOC_MOLECULE_MASSES_31);
            AssertEx.IsDocumentState(doc, null, 1, 2, 2, 2);
            Assert.AreEqual(doc.Molecules.First().CustomMolecule, doc.MoleculeTransitionGroups.First().CustomMolecule);
        }

        [TestMethod]
        public void SmallMoleculeV31DocumentSerializeTest()
        {
            // Verify handling of 3.1 version of small molecule handling, where we assumed no multiple charges or labels
            AssertEx.ValidatesAgainstSchema(DOC_MOLECULES_31);
            var doc = AssertEx.Deserialize<SrmDocument>(DOC_MOLECULES_31);
            AssertEx.IsDocumentState(doc, null, 1, 1, 1, 1);
            Assert.AreEqual("C12H99", doc.MoleculeTransitionGroups.First().CustomMolecule.Formula);
            Assert.AreEqual(doc.Molecules.First().CustomMolecule , doc.MoleculeTransitionGroups.First().CustomMolecule);
        }

        [TestMethod]
        public void MoleculeParseTest()
        {
            // Verify handling of simple formula arithmetic as used in ion forumlas
            const string C12H8S2O6 = "C12H8S2O6";
            const string SO4 = "SO4";
            Assert.AreEqual(311.976229, BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H8S2O6),.00001);
            var subtracted = C12H8S2O6+"-"+SO4;
            AssertEx.ThrowsException<ArgumentException>(() => Molecule.ParseExpressionToDictionary(subtracted + subtracted));  // More than one subtraction operation not supported
            Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(subtracted), BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H8S2O6) - BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(SO4));
            Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H8S2O6+SO4), BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H8S2O6) + BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(SO4));
            var desc = subtracted;
            var counts = new Dictionary<string, int>();
            var expected = new Dictionary<string,int> {{"C",12},{"H",8},{"S",1},{"O",2}};
            BioMassCalc.MONOISOTOPIC.ParseCounts(ref desc, counts, false);
            Assert.IsTrue(CollectionUtil.EqualsDeep(expected, counts));
        }

        [TestMethod]
        public void MoleculeDocumentSerializeTest()
        {
            ValidateDocMolecules(DOC_MOLECULES);  // V3.12, where s_lens and cone_voltage were misnamed
            ValidateDocMolecules(DOC_MOLECULES, false);  // CV instead of DT
            ValidateDocMolecules(DOC_MOLECULES.Replace("3.12", "3.52").Replace("s_lens", "explicit_s_lens").Replace("cone_voltage", "explicit_cone_voltage"));
            ValidateDocMolecules(DOC_MOLECULES.Replace("3.12", "3.61").Replace("s_lens", "explicit_s_lens").Replace("cone_voltage", "explicit_ccs_sqa=\"345.6\" explicit_cone_voltage")); // In 3.61 we have CCS

            // Check document using named labels - without the fix, this throws "error reading mz values - declared mz value 75.07558 does not match calculated value 74.0785450512905"
            AssertEx.ValidatesAgainstSchema(DOC_LABEL_IMPLEMENTED);
            var doc = AssertEx.Deserialize<SrmDocument>(DOC_LABEL_IMPLEMENTED);
            AssertEx.IsDocumentState(doc, null, 1, 1, 3, 3);
        }

        private static void ValidateDocMolecules(string docText, bool imTypeIsDriftTime=true)
        {
            if (!imTypeIsDriftTime)
            {
                docText = docText.Replace("drift_time_msec", "compensation_voltage");
            }
            AssertEx.ValidatesAgainstSchema(docText);
            var doc = AssertEx.Deserialize<SrmDocument>(docText);
            AssertEx.IsDocumentState(doc, null, 1, 1, 1, 2);
            var mzPrecursor = 59.999451; // As declared in XML
            var mzFragment = 54.999451;  // As declared in XML
            var mzToler = 0.0000005;
            var precursorAdduct = Adduct.M_PLUS_H;
            var neutralMassMolecule = precursorAdduct.MassFromMz(mzPrecursor, MassType.Monoisotopic);
            var fragmentAdduct = Adduct.M_PLUS;
            var neutralMassTransition = fragmentAdduct.MassFromMz(mzFragment, MassType.Monoisotopic);
            var transition = new CustomIon(null, precursorAdduct, new TypedMass(neutralMassMolecule, MassType.Monoisotopic), new TypedMass(neutralMassMolecule, MassType.Average), "molecule");
            var transition2 = new CustomIon(null, fragmentAdduct, new TypedMass(neutralMassTransition, MassType.Monoisotopic), new TypedMass(neutralMassTransition, MassType.Average), "molecule fragment");
            var precursor = new CustomMolecule(new TypedMass(neutralMassMolecule, MassType.Monoisotopic), new TypedMass(neutralMassMolecule, MassType.Average), "molecule");
            Assert.AreEqual(BioMassCalc.CalculateIonMz(precursor.GetMass(MassType.Monoisotopic), precursorAdduct), doc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz, 1E-5);
            Assert.AreEqual(BioMassCalc.CalculateIonMz(transition.GetMass(MassType.Monoisotopic), precursorAdduct), doc.MoleculeTransitions.ElementAt(0).Mz, 1E-5);
            Assert.AreEqual(BioMassCalc.CalculateIonMz(transition2.GetMass(MassType.Monoisotopic), fragmentAdduct), doc.MoleculeTransitions.ElementAt(1).Mz, 1E-5);
            Assert.IsTrue(doc.Molecules.ElementAt(0).Peptide.IsCustomMolecule);
            var nodeGroup = doc.MoleculeTransitionGroups.ElementAt(0);
            Assert.AreEqual(4.704984, doc.MoleculeTransitionGroups.ElementAt(0).ExplicitValues.CollisionEnergy);
            Assert.AreEqual(null, doc.MoleculeTransitions.ElementAt(0).ExplicitValues.CollisionEnergy); // Value is found at precursor level
            double expectedIonMobility = 2.34;
            double? expectedCV = imTypeIsDriftTime ? (double?) null : expectedIonMobility;
            Assert.AreEqual(expectedCV, doc.MoleculeTransitionGroups.ElementAt(0).ExplicitValues.CompensationVoltage);
            Assert.AreEqual(4.9, doc.MoleculeTransitions.ElementAt(0).ExplicitValues.DeclusteringPotential);
            Assert.AreEqual(3.45, doc.Molecules.ElementAt(0).ExplicitRetentionTime.RetentionTime);
            Assert.AreEqual(4.56, doc.Molecules.ElementAt(0).ExplicitRetentionTime.RetentionTimeWindow);
            Assert.AreEqual(98, doc.MoleculeTransitions.ElementAt(0).ExplicitValues.SLens);
            Assert.AreEqual(99, doc.MoleculeTransitions.ElementAt(0).ExplicitValues.ConeVoltage);
            Assert.AreEqual(expectedIonMobility, doc.MoleculeTransitionGroups.ElementAt(0).ExplicitValues.IonMobility);
            Assert.AreEqual(-0.12, doc.MoleculeTransitions.ElementAt(0).ExplicitValues.IonMobilityHighEnergyOffset.Value, 1E-12);
            if (doc.FormatVersion.CompareTo(DocumentFormat.VERSION_3_61) >= 0)
                Assert.AreEqual(345.6, doc.MoleculeTransitionGroups.ElementAt(0).ExplicitValues.CollisionalCrossSectionSqA.Value, 1E-12);
            Assert.IsTrue(doc.MoleculeTransitions.ElementAt(0).Transition.IsCustom());
            Assert.AreEqual(transition.MonoisotopicMassMz, doc.MoleculeTransitions.ElementAt(0).Transition.CustomIon.MonoisotopicMassMz, mzToler);
            Assert.AreEqual(transition2.MonoisotopicMassMz, doc.MoleculeTransitions.ElementAt(1).Transition.CustomIon.MonoisotopicMassMz, mzToler);
            Assert.AreEqual(1, doc.MoleculeTransitionGroups.ElementAt(0).TransitionGroup.PrecursorAdduct.AdductCharge);
            Assert.AreEqual("[M+H]", doc.MoleculeTransitionGroups.ElementAt(0).TransitionGroup.PrecursorAdduct.AdductFormula);
            Assert.AreEqual("[M+H]", doc.MoleculeTransitionGroups.ElementAt(0).TransitionGroup.PrecursorAdduct.AsFormulaOrSignedInt());
            Assert.AreEqual(1, doc.MoleculeTransitions.ElementAt(0).Transition.Charge);
            AssertEx.ValidatesAgainstSchema(doc);
            AssertEx.Serializable(doc); // Round trip
            AssertEx.ValidatesAgainstSchema(doc); // Make sure any manipulations are still valid for schema
        }

        /// <summary>
        /// A test for SrmDocument deserialization of v0.1 documents and
        /// general serialization.
        /// </summary>
        [TestMethod]
        public void DocumentNodeCountsTest()
        {
            SrmDocument doc = AssertEx.Deserialize<SrmDocument>(DOC_0_1_BOVINE);
            AssertEx.IsDocumentState(doc, null, 2, 2, 2, 3);
            doc = AssertEx.Deserialize<SrmDocument>(DOC_0_1_PEPTIDES);
            AssertEx.IsDocumentState(doc, null, 3, 6, 5, 13);
            PeptideGroupDocNode nodeGroup = (PeptideGroupDocNode) doc.Children[1];
            SrmDocument docNew = (SrmDocument) doc.RemoveChild(nodeGroup);
            AssertEx.IsDocumentState(docNew, null, 2, 4, 4, 12);
            try
            {
                docNew.ReplaceChild(nodeGroup);                
                Assert.Fail("Unexpected success repacing node that was already removed.");
            }
            catch (IdentityNotFoundException)
            {
                // Expect this exception
            }
            try
            {
                docNew.RemoveChild(nodeGroup);
                Assert.Fail("Unexpected success removing node that was already removed.");
            }
            catch (IdentityNotFoundException)
            {
                // Expect this exception
            }
            PeptideGroupDocNode nodeGroupNew = (PeptideGroupDocNode) nodeGroup.RemoveChild(nodeGroup.Children[0]);
            docNew = (SrmDocument) doc.ReplaceChild(nodeGroupNew);
            AssertEx.IsDocumentState(docNew, null, 3, 5, 4, 12);
            docNew = (SrmDocument) docNew.ChangeChildren(new[] {nodeGroup});
            AssertEx.IsDocumentState(docNew, null, 1, 2, 1, 1);
        }

        /// <summary>
        /// A test for SrmDocument tranistion list export handling of
        /// IgnoreProteins.
        /// </summary>
        [TestMethod]
        public void DocumentExportProteinsTest()
        {
            SrmDocument document = AssertEx.Deserialize<SrmDocument>(DOC_0_1_PEPTIDES_NO_EMPTY);
            var exporter = new ThermoMassListExporter(document)
                               {
                                   Strategy = ExportStrategy.Buckets,
                                   MaxTransitions = 11,
                                   MinTransitions = 1
                               };
            // Should be split along protein boundaries
            ValidateExportState(exporter, new[] { 7, 6 });
            exporter.IgnoreProteins = true;
            // Should still be split along peptide boundaries
            ValidateExportState(exporter, new[] {10, 3});
        }

        private static void ValidateExportState(AbstractMassListExporter exporter, int[] arrayTranCounts)
        {
            exporter.Export(null);
            var names = exporter.MemoryOutput.Keys.ToArray();
            Assert.AreEqual(arrayTranCounts.Length, names.Length, "Incorrect number of transition lists");
            Array.Sort(names);
            for (int i = 0; i < arrayTranCounts.Length; i++)
            {
                Assert.AreEqual(arrayTranCounts[i], LineCount(exporter.MemoryOutput[names[i]].ToString()),
                                "Transitions not distributed correctly");                
            }
        }

        /// <summary>
        /// Count lines in text excluding a final empty line.
        /// </summary>
        private static int LineCount(string text)
        {
            string[] lines = text.Split('\n');
            return (string.IsNullOrEmpty(lines[lines.Length - 1]) ? lines.Length - 1 : lines.Length);
        }

        /// <summary>
        /// A test for SrmDocument deserialization of v0.1 documents and
        /// general serialization.
        /// </summary>
        [TestMethod]
        public void DocumentExport_0_1_Test()
        {
            int count = EqualCsvs(DOC_0_1_BOVINE, 4, ThermoExporters, ExportStrategy.Single, 2, null,
                                  ExportMethodType.Standard);
            Assert.AreEqual(1, count);
            count = EqualCsvs(DOC_0_1_BOVINE, 4, ThermoExporters, ExportStrategy.Single, 1, null,
                                  ExportMethodType.Standard);
            Assert.AreEqual(1, count);
            count = ExportAll(DOC_0_1_PEPTIDES_NO_EMPTY, 4, CreateWatersExporter, ExportStrategy.Single, 2, null,
                                  ExportMethodType.Standard, null, RefinementSettings.ConvertToSmallMoleculesMode.none);
            Assert.AreEqual(1, count);
            count = EqualCsvs(DOC_0_1_PEPTIDES_NO_EMPTY, 0, CreateAbiExporters, ExportStrategy.Buckets, 1, 6,
                              ExportMethodType.Standard);
            // TODO: Test scheduled runs
            Assert.AreEqual(3, count);
        }

        /// <summary>
        /// Test export and reimport of Waters and Agilent transition list formats
        /// </summary>
        [TestMethod]
        public void DocumentExportImportTest()
        {
            DoDocumentExportImportTest(RefinementSettings.ConvertToSmallMoleculesMode.none);
        }

        [TestMethod]
        public void DocumentExportImportTestAsSmallMolecules()
        {
            DoDocumentExportImportTest(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        public void DoDocumentExportImportTest(RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules)
        {
            if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none && SkipSmallMoleculeTestVersions())
            {
                return;
            }

            var pathForLibraries = TestContext.ResultsDirectory;

            int count = ExportAll(DOC_0_1_PEPTIDES_NO_EMPTY, 4, CreateWatersExporter, ExportStrategy.Single, 2, null,
                                  ExportMethodType.Standard, pathForLibraries, asSmallMolecules);
            Assert.AreEqual(1, count);
            count = ExportAll(DOC_0_1_PEPTIDES_NO_EMPTY, 4, CreateAgilentExporter, ExportStrategy.Single, 2, null,
                                  ExportMethodType.Standard, pathForLibraries, asSmallMolecules);
            Assert.AreEqual(1, count);
            count = ExportAll(DOC_0_1_PEPTIDES_NO_EMPTY, 7, CreateThermoQuantivaExporter, ExportStrategy.Single, 2, null,
                              ExportMethodType.Standard, pathForLibraries, asSmallMolecules);
            Assert.AreEqual(1, count);
            count = ExportAll(DOC_0_1_PEPTIDES_NO_EMPTY, 7, CreateThermoQuantivaExporter, ExportStrategy.Single, 2, null,
                              ExportMethodType.Scheduled, pathForLibraries, asSmallMolecules);
            Assert.AreEqual(1, count);
            count = ExportAll(DOC_0_1_PEPTIDES_NO_EMPTY, 8, CreateShimadzuExporter, ExportStrategy.Single, 2, null,
                              ExportMethodType.Standard, pathForLibraries, asSmallMolecules);
            Assert.AreEqual(1, count);
            count = ExportAll(DOC_0_1_PEPTIDES_NO_EMPTY, 8, CreateShimadzuExporter, ExportStrategy.Single, 2, null,
                              ExportMethodType.Scheduled, pathForLibraries, asSmallMolecules);
            Assert.AreEqual(1, count);
            if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none)
            {
                count = ExportAll(DOC_MOLECULES, 8, CreateShimadzuExporter, ExportStrategy.Single, 2, null,
                   ExportMethodType.Scheduled, pathForLibraries, RefinementSettings.ConvertToSmallMoleculesMode.none,
                   "Peptide,ID,Type,Precursor,Product,RT,RT Window,CE,Polarity\r\nmolecule_light,1,,59.999451,59.999451,3.45,4.56,-4.7,0\r\nmolecule_light,1,,59.999451,54.999451,3.45,4.56,-4.7,0\r\n");
                Assert.AreEqual(1, count);
                // Try negative charges - this bumps masses up slightly due to electron gain instead of loss
                count = ExportAll(DOC_MOLECULES.Replace("charge=\"", "charge=\"-").Replace("59.999451", "60.000549").Replace("54.999451","55.000549"), 8, CreateShimadzuExporter, ExportStrategy.Single, 2, null,
                   ExportMethodType.Scheduled, pathForLibraries, RefinementSettings.ConvertToSmallMoleculesMode.none,
                   "Peptide,ID,Type,Precursor,Product,RT,RT Window,CE,Polarity\r\nmolecule_light,1,,60.000549,60.000549,3.45,4.56,4.7,1\r\nmolecule_light,1,,60.000549,55.000549,3.45,4.56,4.7,1\r\n");
                Assert.AreEqual(1, count);
            }
            count = ExportAll(DOC_0_1_PEPTIDES_NO_EMPTY, 37, CreateBrukerExporter, ExportStrategy.Single, 2, null,
                              ExportMethodType.Standard, pathForLibraries, asSmallMolecules);
            Assert.AreEqual(1, count);
            count = ExportAll(DOC_0_1_PEPTIDES_NO_EMPTY, 37, CreateBrukerExporter, ExportStrategy.Single, 2, null,
                              ExportMethodType.Scheduled, pathForLibraries, asSmallMolecules);
            Assert.AreEqual(1, count);
        }

        private static int EqualCsvs(string xml, int countFields, CreateExporters exporters,
                                     ExportStrategy strategy, int minTransition, int? maxTransition, ExportMethodType methodType)
        {
            XmlSrmDocument target = AssertEx.Deserialize<XmlSrmDocument>(xml);
            SrmDocument actual = AssertEx.Deserialize<SrmDocument>(xml);

            return EqualCsvs(target, actual, countFields, exporters, strategy, minTransition, maxTransition, methodType);
        }

        private static int EqualCsvs(XmlSrmDocument target, SrmDocument actual, int countFields, CreateExporters exporters,
                                     ExportStrategy strategy, int minTransition, int? maxTransition, ExportMethodType methodType)
        {
            XmlMassListExporter exporterTarget;
            AbstractMassListExporter exporterActual;
            exporters(target, out exporterTarget, actual, out exporterActual);
            exporterTarget.Strategy = exporterActual.Strategy = strategy;
            exporterTarget.MinTransitions = exporterActual.MinTransitions = minTransition;
            exporterTarget.MaxTransitions = exporterActual.MaxTransitions = maxTransition;
            exporterTarget.MethodType = exporterActual.MethodType = methodType;

            exporterTarget.Export(null);
            var exportedTarget = exporterTarget.TestOutput;

            Assert.AreNotEqual(0, exportedTarget.Count);

            exporterActual.Export(null);
            var exportedActual = exporterActual.MemoryOutput;

            Assert.AreEqual(exportedTarget.Count, exportedActual.Count);

            // Make sure the resulting output can be imported
            SrmDocument docImport = new SrmDocument(actual.Settings);

            foreach (string key in exportedTarget.Keys)
            {
                string targetList = exportedTarget[key].ToString();
                string actualList = exportedActual[key].ToString();

                Assert.AreNotEqual(0, targetList.Length);
                if (countFields > 0)
                    AssertEx.FieldsEqual(targetList, actualList, countFields);
                else
                    AssertEx.NoDiff(targetList, actualList);

                // Import the exported list
                IdentityPath pathAdded;
                var inputs = new MassListInputs(actualList, CultureInfo.InvariantCulture, TextUtil.SEPARATOR_CSV);
                docImport = docImport.ImportMassList(inputs, null, IdentityPath.ROOT, out pathAdded);
            }

            if (minTransition < 2)
                CheckImportSimilarity(actual, docImport);

            return exportedTarget.Count;
        }

        private static int ExportAll(string xml, int countFields, CreateExporter exporters,
                                     ExportStrategy strategy, int minTransition, int? maxTransition,
                                     ExportMethodType methodType, 
                                     string pathForSmallMoleculeLibraries,
                                     RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules,
                                     string expectedOutput = null)
        {
            SrmDocument actual = AssertEx.Deserialize<SrmDocument>(xml);
            var refine = new RefinementSettings();
            actual = refine.ConvertToSmallMolecules(actual, pathForSmallMoleculeLibraries, asSmallMolecules);
            try
            {
                return ExportAll(actual, countFields, exporters, strategy, minTransition, maxTransition, methodType, expectedOutput);
            }
            catch
            {
                return ExportAll(actual, countFields, exporters, strategy, minTransition, maxTransition, methodType, expectedOutput);
            }
        }

        private static int ExportAll(SrmDocument actual, int countFields, CreateExporter exporter,
                                     ExportStrategy strategy, int minTransition, int? maxTransition,
                                     ExportMethodType methodType,
                                     string expectedOutput = null)
        {
            AbstractMassListExporter exporterActual;
            exporter(actual, methodType, strategy, out exporterActual);
            exporterActual.Export(null);
            var exportedActual = exporterActual.MemoryOutput;
            if (expectedOutput != null)
            {
                var actualOutput = exportedActual.Values.First().ToString();
                Assert.AreEqual(expectedOutput, actualOutput);
                return exportedActual.Count;  // Just be satisfied with output correctness, don't try to roundtrip
            }
            
            // Make sure the resulting output can be imported
            SrmDocument docImport = new SrmDocument(actual.Settings);
            foreach (string key in exportedActual.Keys)
            {
                string actualList = exportedActual[key].ToString();

                // Import the exported list
                IdentityPath pathAdded;
                try
                {
                    var inputs = new MassListInputs(actualList, CultureInfo.InvariantCulture, TextUtil.SEPARATOR_CSV);
                    var importer = docImport.PreImportMassList(inputs, null, false);
                    docImport = docImport.ImportMassList(inputs, importer, IdentityPath.ROOT, out pathAdded);
                }
                catch
                {
                    // Is it just that we aren't really sure how to roundtrip a small molecule list yet? Try making it look like peptides again.
                    // CONSIDER(bspratt) keep an eye out for small mol native list formats in the field
                    if (actual.MoleculeTransitionGroups.Any(
                        g => g.Annotations.Note.Contains(RefinementSettings.TestingConvertedFromProteomic)))
                    {
                        actualList = actualList.Replace(
                            RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator, string.Empty);
                    }
                    var inputs = new MassListInputs(actualList, CultureInfo.InvariantCulture, TextUtil.SEPARATOR_CSV);
                    docImport = docImport.ImportMassList(inputs, null, IdentityPath.ROOT, out pathAdded);
                }
            }
            return exportedActual.Count;
        }

        private delegate void CreateExporters(XmlSrmDocument target, out XmlMassListExporter exporterTarget,
                                              SrmDocument actual, out AbstractMassListExporter exporterActual);

        private static void ThermoExporters(XmlSrmDocument target, out XmlMassListExporter exporterTarget,
                                                  SrmDocument actual, out AbstractMassListExporter exporterActual)
        {
            exporterTarget = new XmlThermoMassListExporter(target);
            exporterActual = new ThermoMassListExporter(actual);
        }

        private static void CreateAbiExporters(XmlSrmDocument target, out XmlMassListExporter exporterTarget,
                                               SrmDocument actual, out AbstractMassListExporter exporterActual)
        {
            exporterTarget = new XmlAbiMassListExporter(target);
            exporterActual = new AbiMassListExporter(actual);
        }

        private delegate void CreateExporter(SrmDocument actual, ExportMethodType methodType, ExportStrategy strategy,
                                             out AbstractMassListExporter exporterActual);

        private static void CreateWatersExporter(SrmDocument actual, ExportMethodType methodType, ExportStrategy strategy,
                                                         out AbstractMassListExporter exporterActual)
        {
            exporterActual = new WatersMassListExporter(actual)
                {
                    MethodType = methodType,
                    Strategy = strategy,
                    RunLength = (methodType == ExportMethodType.Standard) ? DEFAULT_RUN_LENGTH : 0
                };
        }

        private static void CreateAgilentExporter(SrmDocument actual, ExportMethodType methodType, ExportStrategy strategy,
                                                  out AbstractMassListExporter exporterActual)
        {
            exporterActual = new AgilentMassListExporter(actual)
                {
                    MethodType = methodType,
                    Strategy = strategy,
                    DwellTime = methodType == (ExportMethodType.Standard) ? DEFAULT_DWELL_TIME : 0
                };
        }

        private static void CreateThermoQuantivaExporter(SrmDocument actual, ExportMethodType methodType, ExportStrategy strategy,
                                                         out AbstractMassListExporter exporterActual)
        {
            exporterActual = new ThermoQuantivaMassListExporter(actual)
                {
                    MethodType = methodType,
                    Strategy = strategy,
                    RunLength = (methodType == ExportMethodType.Standard) ? DEFAULT_RUN_LENGTH : (double?)null
                };
        }

        private static void CreateShimadzuExporter(SrmDocument actual, ExportMethodType methodType, ExportStrategy strategy,
                                                   out AbstractMassListExporter exporterActual)
        {
            exporterActual = new ShimadzuMassListExporter(actual)
                {
                    MethodType = methodType,
                    Strategy = strategy,
                    RunLength = (methodType == ExportMethodType.Standard) ? DEFAULT_RUN_LENGTH : (double?)null
                };
        }

        private static void CreateBrukerExporter(SrmDocument actual, ExportMethodType methodType,
                                                 ExportStrategy strategy,
                                                 out AbstractMassListExporter exporterActual)
        {
            exporterActual = new BrukerMassListExporter(actual)
                {
                    MethodType = methodType,
                    Strategy = strategy,
                    DwellTime = (methodType == ExportMethodType.Standard) ? DEFAULT_DWELL_TIME : (double?)null
                };
        }

        private static void CheckImportSimilarity(SrmDocument document, SrmDocument docImport)
        {
            CheckImportSimilarity(document.MoleculeGroups, docImport.MoleculeGroups,
                (g1, g2) => Assert.AreEqual(g1.Name, g2.Name));
            CheckImportSimilarity(document.Molecules, docImport.Molecules,
                (p1, p2) => Assert.AreEqual(p1.Peptide.Target, p2.Peptide.Target));
            CheckImportSimilarity(document.MoleculeTransitionGroups, docImport.MoleculeTransitionGroups,
                (g1, g2) => Assert.AreEqual(g1.PrecursorMz, g2.PrecursorMz));
            CheckImportSimilarity(document.MoleculeTransitions, docImport.MoleculeTransitions,
                (t1, t2) => Assert.AreEqual(t1.Mz, t2.Mz));
        }

        private static void CheckImportSimilarity<T>(IEnumerable<T> originals, IEnumerable<T> imported, Action<T, T> check)
        {
            var origArray = originals.ToArray();
            var importArray = imported.ToArray();
            Assert.AreEqual(origArray.Length, importArray.Length);
            for (int i = 0; i < origArray.Length; i++)
                check(origArray[i], importArray[i]);
        }

        private const string DOC_0_1_BOVINE =
            "<srm_settings>\n" +
            "  <settings_summary name=\"Default\">\n" +
            "    <peptide_settings>\n" +
            "      <enzyme name=\"Trypsin\" cut=\"KR\" no_cut=\"P\" sense=\"C\" />\n" +
            "      <digest_settings max_missed_cleavages=\"0\" exclude_ragged_ends=\"false\" />\n" +
            "      <peptide_filter start=\"25\" min_length=\"8\" max_length=\"25\" auto_select=\"true\">\n" +
            "        <peptide_exclusions />\n" +
            "      </peptide_filter>\n" +
            "    </peptide_settings>\n" +
            "    <transition_settings>\n" +
            "      <transition_prediction precursor_mass_type=\"Monoisotopic\" fragment_mass_type=\"Monoisotopic\">\n" +
            "        <predict_collision_energy name=\"Thermo\">\n" +
            "          <regressions>\n" +
            "            <regression_ce slope=\"0.034\" intercept=\"3.314\" charge=\"2\" />\n" +
            "            <regression_ce slope=\"0.044\" intercept=\"3.314\" charge=\"3\" />\n" +
            "          </regressions>\n" +
            "        </predict_collision_energy>\n" +
            "      </transition_prediction>\n" +
            "      <transition_filter precursor_charges=\"2\" product_charges=\"1\" fragment_range_first=\"y1\" fragment_range_last=\"last y-ion\" include_n_prolene=\"false\" include_c_glu_asp=\"false\" auto_select=\"true\" />\n" +
            "    </transition_settings>\n" +
            "  </settings_summary>\n" +
            "  <selected_proteins>\n" +
            "    <protein name=\"gi|15674171|30S_ribosomal_pro\" description=\"gi|15674171|ref|NP_268346.1| 30S ribosomal protein S18 [Lactococcus lactis subsp. lactis]\">\n" +
            "      <alternatives>\n" +
            "        <alternative_protein name=\"gi|13878750|sp|Q9CDN0|RS18_LACLA\" description=\"30S ribosomal protein S18\" />\n" +
            "        <alternative_protein name=\"gi|25294831|pir||E86898\" description=\"30S ribosomal protein S18 [imported] - Lactococcus lactis subsp. lactis (strain IL1403)\" />\n" +
            "        <alternative_protein name=\"gi|12725253|gb|AAK06287.1|AE006448_5\" description=\"30S ribosomal protein S18 [Lactococcus lactis subsp. lactis] [MASS=9371]\" />\n" +
            "      </alternatives>\n" +
            "      <sequence>\n" +
            "        MAQQRRGGFK RRKKVDFIAA NKIEVVDYKD TELLKRFISE RGKILPRRVT\n" +
            "        GTSAKNQRKV VNAIKRARVM ALLPFVAEDQ N</sequence>\n" +
            "      <selected_peptides>\n" +
            "        <peptide start=\"68\" end=\"81\" sequence=\"VMALLPFVAEDQN\" prev_aa=\"82\" next_aa=\"45\" calc_neutral_pep_mass=\"1445.722452\" num_missed_cleavages=\"0\">\n" +
            "          <selected_transitions>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"12\" calc_neutral_mass=\"1345.646762\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>723.868502</precursor_mz>\n" +
            "              <product_mz>1347.661314</product_mz>\n" +
            "              <collision_energy>27.925529</collision_energy>\n" +
            "            </transition>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"11\" calc_neutral_mass=\"1214.606277\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>723.868502</precursor_mz>\n" +
            "              <product_mz>1216.620829</product_mz>\n" +
            "              <collision_energy>27.925529</collision_energy>\n" +
            "            </transition>\n" +
            "          </selected_transitions>\n" +
            "        </peptide>\n" +
            "      </selected_peptides>\n" +
            "    </protein>\n" +
            "    <protein name=\"gi|9910844|RL3_METVA_50S_ribo\" description=\"gi|9910844|sp|Q9UWG2|RL3_METVA 50S ribosomal protein L3P [MASS=36728]\">\n" +
            "      <sequence>\n" +
            "        MGMKKNRPRR GSLAFSPRKR AKKLVPKIRS WPADKKVGLQ AFPVYKAGTT\n" +
            "        HALLVENNPK SPNNGQEVFS PVTVLETPEI TVAGIRAYGK TTKGLKALTE\n" +
            "        VWAKQQDKEL GRKLTVTKKE EIKTVESLDA VLEKTVDLRV IVHTNPKTTG\n" +
            "        IPKKKPEVVE IRVGGSSVAE KLAYAKDILG KTLSINDVFE TGEFIDTLAV\n" +
            "        TKGKGFQGPV KRWGVKIQFG KHQRKGVGRQ TGSIGPWRPK RVMWTVPLAG\n" +
            "        QMGFHQRTEY NKRILKLGSE GAEITPKGGF LNYGAVKNGY VVVKGTVQGP\n" +
            "        AKRLVVLRGA VRPAEDKFGL PEVTYISKES KQGN</sequence>\n" +
            "      <selected_peptides>\n" +
            "        <peptide start=\"36\" end=\"46\" sequence=\"VGLQAFPVYK\" prev_aa=\"75\" next_aa=\"65\" calc_neutral_pep_mass=\"1120.628081\" num_missed_cleavages=\"0\">\n" +
            "          <selected_transitions>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"9\" calc_neutral_mass=\"1020.552391\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>561.321317</precursor_mz>\n" +
            "              <product_mz>1022.566943</product_mz>\n" +
            "              <collision_energy>22.398925</collision_energy>\n" +
            "            </transition>\n" +
            "          </selected_transitions>\n" +
            "        </peptide>\n" +
            "      </selected_peptides>\n" +
            "    </protein>\n" +
            "  </selected_proteins>\n" +
            "</srm_settings>";

        private const string DOC_0_1_PEPTIDES = DOC_0_1_PEPTIDES_PART1 +
                                                 DOC_0_1_PEPTIDES_EMPTY +
                                                 DOC_0_1_PEPTIDES_PART2;

        private const string DOC_0_1_PEPTIDES_NO_EMPTY = DOC_0_1_PEPTIDES_PART1 +
                                                         DOC_0_1_PEPTIDES_PART2;

        private const string DOC_0_1_PEPTIDES_PART1 =
            "<srm_settings>\n" +
            "  <settings_summary name=\"Default\">\n" +
            "    <peptide_settings>\n" +
            "      <enzyme name=\"Trypsin\" cut=\"KR\" no_cut=\"P\" sense=\"C\" />\n" +
            "      <digest_settings max_missed_cleavages=\"0\" exclude_ragged_ends=\"false\" />\n" +
            "      <peptide_prediction />\n" +
            "      <peptide_filter start=\"25\" min_length=\"8\" max_length=\"25\" min_transtions=\"1\" auto_select=\"true\">\n" +
            "        <peptide_exclusions />\n" +
            "      </peptide_filter>\n" +
            "      <peptide_modifications>\n" +
            "        <static_modifications>\n" +
            "          <static_modification name=\"Carbamidomethyl Cysteine\" aminoacid=\"C\" formula=\"C2H3ON\" />\n" +
            "        </static_modifications>\n" +
            "      </peptide_modifications>\n" +
            "    </peptide_settings>\n" +
            "    <transition_settings>\n" +
            "      <transition_prediction precursor_mass_type=\"Average\" fragment_mass_type=\"Average\">\n" +
            "        <predict_collision_energy name=\"ABI\">\n" +
            "          <regressions>\n" +
            "            <regression_ce slope=\"0.0431\" intercept=\"4.7556\" charge=\"2\" />\n" +
            "          </regressions>\n" +
            "        </predict_collision_energy>\n" +
            "        <predict_retention_time name=\"Bovine Standard (100A)\" calculator=\"SSRCalc 3.0 (100A)\"\n" +
            "             time_window=\"13.6\">\n" +
            "          <regression_rt slope=\"1.681\" intercept=\"-6.247\" />\n" +
            "        </predict_retention_time>\n" +
            "        <predict_declustering_potential slope=\"0.0729\" intercept=\"31.117\" name=\"ABI\" />\n" +
            "      </transition_prediction>\n" +
            "      <transition_filter precursor_charges=\"2\" product_charges=\"1\" fragment_range_first=\"m/z &gt; precursor\" fragment_range_last=\"start + 3\" include_n_prolene=\"true\" include_c_glu_asp=\"false\" auto_select=\"true\" />\n" +
            "      <transition_instrument min_mz=\"50\" max_mz=\"1500\" />\n" +
            "    </transition_settings>\n" +
            "  </settings_summary>\n" +
            "  <selected_proteins>\n" +
            "    <protein name=\"peptides1\" peptide_list=\"true\">\n" +
            "      <sequence>\n" +
            "        YLGAYLLATLGGNASPSAQDVLK\n" +
            "        VLEAGGLDCDMENANSVVDALK\n" +
            "        </sequence>\n" +
            "      <selected_peptides>\n" +
            "        <peptide start=\"1\" end=\"24\" sequence=\"YLGAYLLATLGGNASPSAQDVLK\" prev_aa=\"88\" next_aa=\"88\" calc_neutral_pep_mass=\"2322.62757\" num_missed_cleavages=\"0\"  predicted_retention_time=\"62.71\">\n" +
            "          <selected_transitions>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"14\" calc_neutral_mass=\"1355.482654\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>1162.321061</precursor_mz>\n" +
            "              <product_mz>1357.497206</product_mz>\n" +
            "              <collision_energy>54.851638</collision_energy>\n" +
            "              <declustering_potential>115.850205</declustering_potential>\n" +
            "            </transition>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"13\" calc_neutral_mass=\"1242.324114\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>1162.321061</precursor_mz>\n" +
            "              <product_mz>1244.338666</product_mz>\n" +
            "              <collision_energy>54.851638</collision_energy>\n" +
            "              <declustering_potential>115.850205</declustering_potential>\n" +
            "            </transition>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"12\" calc_neutral_mass=\"1185.272494\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>1162.321061</precursor_mz>\n" +
            "              <product_mz>1187.287046</product_mz>\n" +
            "              <collision_energy>54.851638</collision_energy>\n" +
            "              <declustering_potential>115.850205</declustering_potential>\n" +
            "            </transition>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"8\" calc_neutral_mass=\"855.961534\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>1162.321061</precursor_mz>\n" +
            "              <product_mz>857.976086</product_mz>\n" +
            "              <collision_energy>54.851638</collision_energy>\n" +
            "              <declustering_potential>115.850205</declustering_potential>\n" +
            "            </transition>\n" +
            "          </selected_transitions>\n" +
            "        </peptide>\n" +
            "        <peptide start=\"25\" end=\"47\" sequence=\"VLEAGGLDCDMENANSVVDALK\" prev_aa=\"88\" next_aa=\"88\" calc_neutral_pep_mass=\"2320.56727\" num_missed_cleavages=\"0\" predicted_retention_time=\"58.2\">\n" +
            "          <selected_transitions>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"13\" calc_neutral_mass=\"1404.531814\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>1161.290911</precursor_mz>\n" +
            "              <product_mz>1406.546366</product_mz>\n" +
            "              <collision_energy>54.807238</collision_energy>\n" +
            "              <declustering_potential>115.775107</declustering_potential>\n" +
            "            </transition>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"12\" calc_neutral_mass=\"1289.443814\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>1161.290911</precursor_mz>\n" +
            "              <product_mz>1291.458366</product_mz>\n" +
            "              <collision_energy>54.807238</collision_energy>\n" +
            "              <declustering_potential>115.775107</declustering_potential>\n" +
            "            </transition>\n" +
            "          </selected_transitions>\n" +
            "        </peptide>\n" +
            "      </selected_peptides>\n" +
            "    </protein>\n" +
            // Older format
            "    <protein name=\"peptides2\">\n" +
            "      <sequence>\n" +
            "        XNQLTINPEN TIFDAKXIIN EPTAAAIAYG LDKXNELESY AYNLKXAEAG\n" +
            "        ENKIVELEEE LRXFVGLMSM IDPPRXSVGI ISDGTETVED IAIRXEMSED\n" +
            "        QLAEIIKXII GYFEPGSVAL KXYADVGSFD YGRXFLASTQ FESTYARX</sequence>\n" +
            "      <selected_peptides>\n" +
            "        <peptide start=\"1\" end=\"16\" sequence=\"NQLTINPENTIFDAK\" prev_aa=\"88\" next_aa=\"88\" calc_neutral_pep_mass=\"1717.88495\" num_missed_cleavages=\"0\" predicted_retention_time=\"48.65\">\n" +
            "          <selected_transitions>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"13\" calc_neutral_mass=\"1474.644464\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>859.949751</precursor_mz>\n" +
            "              <product_mz>1476.659016</product_mz>\n" +
            "              <collision_energy>41.819434</collision_energy>\n" +
            "              <declustering_potential>93.807337</declustering_potential>\n" +
            "            </transition>\n" +
            "          </selected_transitions>\n" +
            "        </peptide>\n";

        private const string DOC_0_1_PEPTIDES_EMPTY =
            "        <peptide start=\"17\" end=\"33\" sequence=\"IINEPTAAAIAYGLDK\" prev_aa=\"88\" next_aa=\"88\" calc_neutral_pep_mass=\"1659.88863\" num_missed_cleavages=\"0\" predicted_retention_time=\"47.16\">\n" +
            "          <selected_transitions/>\n" +
            "        </peptide>\n";

        private const string DOC_0_1_PEPTIDES_PART2 =
            "      </selected_peptides>\n" +
            "    </protein>" +
            // FASTA protein mixed in
            "    <protein name=\"YAL007C\" description=\"ERP2 SGDID:S000000005, Chr I from 138347-137700, reverse complement, Verified ORF, &quot;Protein that forms a heterotrimeric complex with Erp1p, Emp24p, and Erv25p; member, along with Emp24p and Erv25p, of the p24 family involved in ER to Golgi transport and localized to COPII-coated vesicles&quot;\" peptide_list=\"false\">\n" +
            "      <sequence>\n" +
            "        MIKSTIALPS FFIVLILALV NSVAASSSYA PVAISLPAFS KECLYYDMVT\n" +
            "        EDDSLAVGYQ VLTGGNFEID FDITAPDGSV ITSEKQKKYS DFLLKSFGVG\n" +
            "        KYTFCFSNNY GTALKKVEIT LEKEKTLTDE HEADVNNDDI IANNAVEEID\n" +
            "        RNLNKITKTL NYLRAREWRN MSTVNSTESR LTWLSILIII IIAVISIAQV\n" +
            "        LLIQFLFTGR QKNYV</sequence>\n" +
            "      <selected_peptides>\n" +
            "        <peptide start=\"101\" end=\"115\" sequence=\"YTFCFSNNYGTALK\" prev_aa=\"75\" next_aa=\"75\" calc_neutral_pep_mass=\"1685.86477\" num_missed_cleavages=\"0\" predicted_retention_time=\"48.17\">\n" +
            "          <selected_transitions>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"10\" calc_neutral_mass=\"1113.208224\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>843.939661</precursor_mz>\n" +
            "              <product_mz>1115.222776</product_mz>\n" +
            "              <collision_energy>41.129399</collision_energy>\n" +
            "              <declustering_potential>92.640201</declustering_potential>\n" +
            "            </transition>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"9\" calc_neutral_mass=\"966.033014\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>843.939661</precursor_mz>\n" +
            "              <product_mz>968.047566</product_mz>\n" +
            "              <collision_energy>41.129399</collision_energy>\n" +
            "              <declustering_potential>92.640201</declustering_potential>\n" +
            "            </transition>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"8\" calc_neutral_mass=\"878.955264\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>843.939661</precursor_mz>\n" +
            "              <product_mz>880.969816</product_mz>\n" +
            "              <collision_energy>41.129399</collision_energy>\n" +
            "              <declustering_potential>92.640201</declustering_potential>\n" +
            "            </transition>\n" +
            "          </selected_transitions>\n" +
            "        </peptide>\n" +
            "        <peptide start=\"169\" end=\"180\" sequence=\"NMSTVNSTESR\" prev_aa=\"82\" next_aa=\"76\" calc_neutral_pep_mass=\"1225.2939\" num_missed_cleavages=\"0\" predicted_retention_time=\"11.33\">\n" +
            "          <selected_transitions>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"8\" calc_neutral_mass=\"891.908824\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>613.654226</precursor_mz>\n" +
            "              <product_mz>893.923376</product_mz>\n" +
            "              <collision_energy>31.204097</collision_energy>\n" +
            "              <declustering_potential>75.852393</declustering_potential>\n" +
            "            </transition>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"7\" calc_neutral_mass=\"790.804344\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>613.654226</precursor_mz>\n" +
            "              <product_mz>792.818896</product_mz>\n" +
            "              <collision_energy>31.204097</collision_energy>\n" +
            "              <declustering_potential>75.852393</declustering_potential>\n" +
            "            </transition>\n" +
            "            <transition fragment_type=\"Y\" fragment_ordinal=\"6\" calc_neutral_mass=\"691.672534\" precursor_charge=\"2\" product_charge=\"1\">\n" +
            "              <precursor_mz>613.654226</precursor_mz>\n" +
            "              <product_mz>693.687086</product_mz>\n" +
            "              <collision_energy>31.204097</collision_energy>\n" +
            "              <declustering_potential>75.852393</declustering_potential>\n" +
            "            </transition>\n" +
            "          </selected_transitions>\n" +
            "        </peptide>\n" +
            "      </selected_peptides>\n" +
            "    </protein>\n" +
            "  </selected_proteins>\n" +
            "</srm_settings>";

        private const string DOC_MOLECULES =
                "<srm_settings format_version=\"3.12\" software_version=\"Skyline (64-bit) \">\n" +
                "  <settings_summary name=\"Default\">\n" +
                "    <peptide_settings>\n" +
                "      <enzyme name=\"Trypsin\" cut=\"KR\" no_cut=\"P\" sense=\"C\" />\n" +
                "      <digest_settings max_missed_cleavages=\"0\" />\n" +
                "      <peptide_prediction use_measured_rts=\"true\" measured_rt_window=\"2\" use_spectral_library_drift_times=\"false\" />\n" +
                "      <peptide_filter start=\"25\" min_length=\"8\" max_length=\"25\" auto_select=\"true\">\n" +
                "        <peptide_exclusions />\n" +
                "      </peptide_filter>\n" +
                "      <peptide_libraries pick=\"library\" />\n" +
                "      <peptide_modifications max_variable_mods=\"3\" max_neutral_losses=\"1\">\n" +
                "        <static_modifications>\n" +
                "          <static_modification name=\"Carbamidomethyl (C)\" aminoacid=\"C\" formula=\"H3C2NO\" unimod_id=\"4\" short_name=\"CAM\" />\n" +
                "        </static_modifications>\n" +
                "        <heavy_modifications />\n" +
                "      </peptide_modifications>\n" +
                "    </peptide_settings>\n" +
                "    <transition_settings>\n" +
                "      <transition_prediction precursor_mass_type=\"Monoisotopic\" fragment_mass_type=\"Monoisotopic\" optimize_by=\"None\">\n" +
                "        <predict_collision_energy name=\"Thermo TSQ Vantage\" step_size=\"1\" step_count=\"5\">\n" +
                "          <regression_ce charge=\"2\" slope=\"0.03\" intercept=\"2.905\" />\n" +
                "          <regression_ce charge=\"3\" slope=\"0.038\" intercept=\"2.281\" />\n" +
                "        </predict_collision_energy>\n" +
                "      </transition_prediction>\n" +
                "      <transition_filter precursor_charges=\"2\" product_charges=\"1\" fragment_types=\"y\" fragment_range_first=\"m/z &gt; precursor\" fragment_range_last=\"3 ions\" precursor_mz_window=\"0\" auto_select=\"true\">\n" +
                "        <measured_ion name=\"N-terminal to Proline\" cut=\"P\" sense=\"N\" min_length=\"3\" />\n" +
                "      </transition_filter>\n" +
                "      <transition_libraries ion_match_tolerance=\"0.5\" ion_count=\"3\" pick_from=\"all\" />\n" +
                "      <transition_integration />\n" +
                "      <transition_instrument min_mz=\"50\" max_mz=\"1500\" mz_match_tolerance=\"0.055\" />\n" +
                "    </transition_settings>\n" +
                "    <data_settings />\n" +
                "  </settings_summary>\n" +
                "  <peptide_list label_name=\"Molecule Group\" websearch_status=\"X\" auto_manage_children=\"false\">\n" +
                "    <note>we call this a peptide_list but it is really a generalized molecule list</note>\n" +
                "    <molecule explicit_retention_time=\"3.45\" explicit_retention_time_window=\"4.56\" mass_average=\"60\" mass_monoisotopic=\"60\" custom_ion_name=\"molecule\">\n" +
                "      <note>this molecule was specified by mass only</note>\n" +
                "      <precursor charge=\"1\" precursor_mz=\"59.9994514200905\" auto_manage_children=\"false\" mass_average=\"60\" mass_monoisotopic=\"60\" explicit_collision_energy=\"4.704984\" cone_voltage=\"99\" s_lens=\"98\" explicit_drift_time_msec=\"2.34\" explicit_drift_time_high_energy_offset_msec=\"-0.12\" explicit_declustering_potential=\"4.9\">\n" +
                "        <note>this precursor has explicit values set</note>\n" +
                "        <transition fragment_type=\"precursor\" mass_average=\"60\" mass_monoisotopic=\"60\" custom_ion_name=\"molecule\">\n" +
                "          <note>this transition is for the precursor</note>\n" +
                "          <precursor_mz>59.999451</precursor_mz>\n" +
                "          <product_mz>59.999451</product_mz>\n" +
                "          <collision_energy>4.704984</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"custom\" mass_average=\"55\" mass_monoisotopic=\"55\" custom_ion_name=\"molecule fragment\" product_charge=\"1\">\n" +
                "          <note>this transition is for a custom fragment ion</note>\n" +
                "          <precursor_mz>59.999451</precursor_mz>\n" +
                "          <product_mz>54.999451</product_mz>\n" +
                "          <collision_energy>4.704984</collision_energy>\n" +
                "        </transition>\n" +
                "      </precursor>\n" +
                "    </molecule>\n" +
                "  </peptide_list>\n" +
                "</srm_settings>";

        private const string DOC_REPORTER_IONS_262 = "<srm_settings format_version=\"2.62\" software_version=\"Skyline \">\n" +
                "  <settings_summary name=\"Default\">\n" +
                "    <peptide_settings>\n" +
                "      <enzyme name=\"Trypsin\" cut=\"KR\" no_cut=\"P\" sense=\"C\" />\n" +
                "      <digest_settings max_missed_cleavages=\"0\" />\n" +
                "      <peptide_prediction use_measured_rts=\"true\" measured_rt_window=\"2\" use_spectral_library_drift_times=\"false\" />\n" +
                "      <peptide_filter start=\"25\" min_length=\"8\" max_length=\"25\" auto_select=\"true\">\n" +
                "        <peptide_exclusions />\n" +
                "      </peptide_filter>\n" +
                "      <peptide_libraries pick=\"library\" />\n" +
                "      <peptide_modifications max_variable_mods=\"3\" max_neutral_losses=\"1\">\n" +
                "        <static_modifications>\n" +
                "          <static_modification name=\"Carbamidomethyl (C)\" aminoacid=\"C\" formula=\"H3C2NO\" unimod_id=\"4\" short_name=\"CAM\" />\n" +
                "        </static_modifications>\n" +
                "        <heavy_modifications />\n" +
                "      </peptide_modifications>\n" +
                "    </peptide_settings>\n" +
                "    <transition_settings>\n" +
                "      <transition_prediction precursor_mass_type=\"Monoisotopic\" fragment_mass_type=\"Monoisotopic\" optimize_by=\"None\">\n" +
                "        <predict_collision_energy name=\"Thermo TSQ Vantage\" step_size=\"1\" step_count=\"5\">\n" +
                "          <regression_ce charge=\"2\" slope=\"0.03\" intercept=\"2.905\" />\n" +
                "          <regression_ce charge=\"3\" slope=\"0.038\" intercept=\"2.281\" />\n" +
                "        </predict_collision_energy>\n" +
                "      </transition_prediction>\n" +
                "      <transition_filter precursor_charges=\"2\" product_charges=\"1,2\" fragment_types=\"y,p\" fragment_range_first=\"m/z &gt; precursor\" fragment_range_last=\"3 ions\" precursor_mz_window=\"0\" auto_select=\"true\">\n" +
                "        <measured_ion name=\"N-terminal to Proline\" cut=\"P\" sense=\"N\" min_length=\"3\" />\n" +
                "        <measured_ion name=\"Water\" ion_formula=\"H3O3\" mass_monoisotopic=\"51.008219\" mass_average=\"51.02202\" charge=\"1\" />\n" +
                "        <measured_ion name=\"Water2\" ion_formula=\"H4O3\" mass_monoisotopic=\"52.016044\" mass_average=\"52.02996\" charge=\"2\" />\n" +
                "        <measured_ion name=\"Water3\" mass_monoisotopic=\"19.01783\" mass_average=\"18.01528\" charge=\"1\" />\n" +
                "        <measured_ion name=\"Water4\" mass_monoisotopic=\"18.01056\" mass_average=\"18.01528\" charge=\"2\" />\n" +
                "        <measured_ion name=\"MySpecialIonMassOnlyC12H11N2\" mass_monoisotopic=\"183.09222315981893\" mass_average=\"183.23093915981892\" charge=\"2\" />\n" +
                "        <measured_ion name=\"MySpecialIon\" ion_formula=\"C12H8O3N\" mass_monoisotopic=\"214.050418\" mass_average=\"214.19862\" charge=\"2\" />\n" +
                "        <measured_ion name=\"iTRAQ-114\" ion_formula=\"C5C'H13N2\" mass_monoisotopic=\"114.111228\" mass_average=\"114.174225\" charge=\"1\" />\n" +
                "        <measured_ion name=\"iTRAQ-116\" ion_formula=\"C4C'2H13NN'\" mass_monoisotopic=\"116.111618\" mass_average=\"116.160139\" charge=\"1\" />\n" +
                "        <measured_ion name=\"TMT-126\" ion_formula=\"C8H16N\" mass_monoisotopic=\"126.128275\" mass_average=\"126.22054\" charge=\"1\" />\n" +
                "        <measured_ion name=\"TMT-127H\" ion_formula=\"C7C'H16N\" mass_monoisotopic=\"127.131629\" mass_average=\"127.213045\" charge=\"1\" />\n" +
                "      </transition_filter>\n" +
                "      <transition_libraries ion_match_tolerance=\"0.5\" ion_count=\"3\" pick_from=\"all\" />\n" +
                "      <transition_integration />\n" +
                "      <transition_instrument min_mz=\"20\" max_mz=\"1500\" mz_match_tolerance=\"0.055\" />\n" +
                "    </transition_settings>\n" +
                "    <data_settings />\n" +
                "  </settings_summary>\n" +
                "  <peptide_list label_name=\"peptides1\" websearch_status=\"X#Upeptides1\">\n" +
                "    <peptide sequence=\"PEPTIDER\" modified_sequence=\"PEPTIDER\" calc_neutral_pep_mass=\"955.461075\" num_missed_cleavages=\"0\">\n" +
                "      <precursor charge=\"2\" calc_neutral_mass=\"955.461075\" precursor_mz=\"478.737814\" collision_energy=\"17.267134\" modified_sequence=\"PEPTIDER\">\n" +
                "        <transition fragment_type=\"precursor\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>478.737814</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"custom\" measured_ion_name=\"Water\" product_charge=\"1\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>51.00767</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"custom\" measured_ion_name=\"Water2\" product_charge=\"2\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>26.007473</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition measured_ion_name=\"Water3\" product_charge=\"1\">\n" +
                "          <precursor_mz>640.817127</precursor_mz>\n" +
                "          <product_mz>19.017281</product_mz>\n" +
                "        </transition>\n" +
                "        <transition measured_ion_name=\"Water4\" product_charge=\"2\">\n" +
                "          <precursor_mz>640.817127</precursor_mz>\n" +
                "          <product_mz>9.004731</product_mz>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"custom\" measured_ion_name=\"MySpecialIonMassOnlyC12H11N2\" product_charge=\"2\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>91.545563</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"custom\" measured_ion_name=\"MySpecialIon\" product_charge=\"2\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>107.02466</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"custom\" measured_ion_name=\"iTRAQ-114\" product_charge=\"1\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>114.110679</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"custom\" measured_ion_name=\"iTRAQ-116\" product_charge=\"1\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>116.111069</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"custom\" measured_ion_name=\"TMT-126\" product_charge=\"1\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>126.127726</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"custom\" measured_ion_name=\"TMT-127H\" product_charge=\"1\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>127.13108</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"y\" fragment_ordinal=\"6\" calc_neutral_mass=\"729.365718\" product_charge=\"1\" cleavage_aa=\"P\" loss_neutral_mass=\"0\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>730.372994</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"y\" fragment_ordinal=\"5\" calc_neutral_mass=\"632.312954\" product_charge=\"1\" cleavage_aa=\"T\" loss_neutral_mass=\"0\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>633.32023</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"y\" fragment_ordinal=\"4\" calc_neutral_mass=\"531.265276\" product_charge=\"1\" cleavage_aa=\"I\" loss_neutral_mass=\"0\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>532.272552</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"y\" fragment_ordinal=\"6\" calc_neutral_mass=\"729.365718\" product_charge=\"2\" cleavage_aa=\"P\" loss_neutral_mass=\"0\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>365.690135</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "      </precursor>\n" +
                "    </peptide>\n" +
                "  </peptide_list>\n" +
                "</srm_settings>";

        private const string DOC_REPORTER_IONS_19 = "<srm_settings format_version=\"1.9\" software_version=\"Skyline \">\n" +
                "  <settings_summary name=\"Default\">\n" +
                "    <peptide_settings>\n" +
                "      <enzyme name=\"Trypsin\" cut=\"KR\" no_cut=\"P\" sense=\"C\" />\n" +
                "      <digest_settings max_missed_cleavages=\"0\" />\n" +
                "      <peptide_prediction use_measured_rts=\"true\" measured_rt_window=\"2\" use_spectral_library_drift_times=\"false\" />\n" +
                "      <peptide_filter start=\"25\" min_length=\"8\" max_length=\"25\" auto_select=\"true\">\n" +
                "        <peptide_exclusions />\n" +
                "      </peptide_filter>\n" +
                "      <peptide_libraries pick=\"library\" />\n" +
                "      <peptide_modifications max_variable_mods=\"3\" max_neutral_losses=\"1\">\n" +
                "        <static_modifications>\n" +
                "          <static_modification name=\"Carbamidomethyl (C)\" aminoacid=\"C\" formula=\"H3C2NO\" unimod_id=\"4\" short_name=\"CAM\" />\n" +
                "        </static_modifications>\n" +
                "        <heavy_modifications />\n" +
                "      </peptide_modifications>\n" +
                "    </peptide_settings>\n" +
                "    <transition_settings>\n" +
                "      <transition_prediction precursor_mass_type=\"Monoisotopic\" fragment_mass_type=\"Monoisotopic\" optimize_by=\"None\">\n" +
                "        <predict_collision_energy name=\"Thermo TSQ Vantage\" step_size=\"1\" step_count=\"5\">\n" +
                "          <regression_ce charge=\"2\" slope=\"0.03\" intercept=\"2.905\" />\n" +
                "          <regression_ce charge=\"3\" slope=\"0.038\" intercept=\"2.281\" />\n" +
                "        </predict_collision_energy>\n" +
                "      </transition_prediction>\n" +
                "      <transition_filter precursor_charges=\"2\" product_charges=\"1,2\" fragment_types=\"y,p\" fragment_range_first=\"m/z &gt; precursor\" fragment_range_last=\"3 ions\" precursor_mz_window=\"0\" auto_select=\"true\">\n" +
                "        <measured_ion name=\"N-terminal to Proline\" cut=\"P\" sense=\"N\" min_length=\"3\" />\n" +
                "        <measured_ion name=\"Water\" formula=\"H2O3\" charges=\"1\" />\n" +
                "        <measured_ion name=\"Water2\" formula=\"H2O3\" charges=\"2\" />\n" +
                "        <measured_ion name=\"Water3\" mass_monoisotopic=\"19.01783\" mass_average=\"18.01528\" charges=\"1\" />\n" + 
                "        <measured_ion name=\"Water4\" mass_monoisotopic=\"18.01056\" mass_average=\"18.01528\" charges=\"2\" />\n" + 
                "        <measured_ion name=\"MySpecialIonMassOnlyC12H9N2\" mass_monoisotopic=\"181.076573\" mass_average=\"181.21506\" charges=\"2\" />\n" +
                "        <measured_ion name=\"MySpecialIon\" formula=\"C12H8O3N\" charges=\"2\" />\n" +
                "        <measured_ion name=\"iTRAQ-114\" formula=\"C5C'H12N2\" charges=\"1\" />\n" +
                "        <measured_ion name=\"iTRAQ-116\" formula=\"C4C'2H12NN'\" charges=\"1\" />\n" +
                "        <measured_ion name=\"TMT-126\" formula=\"C8H15N\" charges=\"1\" />\n" +
                "        <measured_ion name=\"TMT-127H\" formula=\"C7C'H15N\" charges=\"1\" />\n" +
                "      </transition_filter>\n" +
                "      <transition_libraries ion_match_tolerance=\"0.5\" ion_count=\"3\" pick_from=\"all\" />\n" +
                "      <transition_integration />\n" +
                "      <transition_instrument min_mz=\"50\" max_mz=\"1500\" mz_match_tolerance=\"0.055\" />\n" +
                "    </transition_settings>\n" +
                "    <data_settings />\n" +
                "  </settings_summary>\n" +
                "  <peptide_list label_name=\"peptides1\" label_description=\"Bacteriocin amylovorin-L (Amylovorin-L471) (Lactobin-A)\" accession=\"P80696\" gene=\"amyL\" species=\"Lactobacillus amylovorus\" preferred_name=\"AMYL_LACAM\" websearch_status=\"X#Upeptides1\">\n" +
                "    <peptide sequence=\"PEPTIDER\" modified_sequence=\"PEPTIDER\" calc_neutral_pep_mass=\"955.461075\" num_missed_cleavages=\"0\">\n" +
                "      <precursor charge=\"2\" calc_neutral_mass=\"955.461075\" precursor_mz=\"478.737814\" collision_energy=\"17.267134\" modified_sequence=\"PEPTIDER\">\n" +
                "        <transition fragment_type=\"precursor\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>478.737814</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition measured_ion_name=\"Water\" product_charge=\"1\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>51.00767</product_mz>\n" +
                "        </transition>\n" +
                "        <transition measured_ion_name=\"Water2\" product_charge=\"2\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>26.007473</product_mz>\n" +
                "        </transition>\n" +
                "        <transition measured_ion_name=\"Water3\" product_charge=\"1\">\n" +
                "          <precursor_mz>640.817127</precursor_mz>\n" +
                "          <product_mz>20.025106</product_mz>\n" +  // 19.01783 + mH
                "        </transition>\n" +
                "        <transition measured_ion_name=\"Water4\" product_charge=\"2\">\n" +
                "          <precursor_mz>640.817127</precursor_mz>\n" +
                "          <product_mz>10.012556</product_mz>\n" +  // (18.01056 + 2mH)/2
                "        </transition>\n" +
                "        <transition measured_ion_name=\"MySpecialIonMassOnlyC12H9N2\" product_charge=\"2\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>91.545562</product_mz>\n" +
                "        </transition>\n" +
                "        <transition measured_ion_name=\"MySpecialIon\" product_charge=\"2\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>108.032485</product_mz>\n" +
                "        </transition>\n" +
                "        <transition measured_ion_name=\"iTRAQ-114\" product_charge=\"1\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>114.110679</product_mz>\n" +
                "        </transition>\n" +
                "        <transition measured_ion_name=\"iTRAQ-116\" product_charge=\"1\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>116.111069</product_mz>\n" +
                "        </transition>\n" +
                "        <transition measured_ion_name=\"TMT-126\" product_charge=\"1\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>126.127726</product_mz>\n" +
                "        </transition>\n" +
                "        <transition measured_ion_name=\"TMT-127H\" product_charge=\"1\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>127.13108</product_mz>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"y\" fragment_ordinal=\"6\" calc_neutral_mass=\"729.365718\" product_charge=\"1\" cleavage_aa=\"P\" loss_neutral_mass=\"0\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>730.372994</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"y\" fragment_ordinal=\"5\" calc_neutral_mass=\"632.312954\" product_charge=\"1\" cleavage_aa=\"T\" loss_neutral_mass=\"0\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>633.32023</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"y\" fragment_ordinal=\"4\" calc_neutral_mass=\"531.265276\" product_charge=\"1\" cleavage_aa=\"I\" loss_neutral_mass=\"0\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>532.272552</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"y\" fragment_ordinal=\"6\" calc_neutral_mass=\"729.365718\" product_charge=\"2\" cleavage_aa=\"P\" loss_neutral_mass=\"0\">\n" +
                "          <precursor_mz>478.737814</precursor_mz>\n" +
                "          <product_mz>365.690135</product_mz>\n" +
                "          <collision_energy>17.267134</collision_energy>\n" +
                "        </transition>\n" +
                "      </precursor>\n" +
                "    </peptide>\n" +
                "  </peptide_list>\n" +
                "</srm_settings>";

        private const string DOC_REPORTER_IONS_INCORRECT_NAME = "<srm_settings format_version=\"2.62\" software_version=\"Skyline \">\n" +
                "  <settings_summary name=\"Default\">\n" +
                "    <peptide_settings>\n" +
                "      <enzyme name=\"Trypsin\" cut=\"KR\" no_cut=\"P\" sense=\"C\" />\n" +
                "      <digest_settings max_missed_cleavages=\"0\" />\n" +
                "      <peptide_prediction use_measured_rts=\"true\" measured_rt_window=\"2\" use_spectral_library_drift_times=\"false\" />\n" +
                "      <peptide_filter start=\"25\" min_length=\"8\" max_length=\"25\" auto_select=\"true\">\n" +
                "        <peptide_exclusions />\n" +
                "      </peptide_filter>\n" +
                "      <peptide_libraries pick=\"library\" />\n" +
                "      <peptide_modifications max_variable_mods=\"3\" max_neutral_losses=\"1\">\n" +
                "        <static_modifications>\n" +
                "          <static_modification name=\"Carbamidomethyl (C)\" aminoacid=\"C\" formula=\"H3C2NO\" unimod_id=\"4\" short_name=\"CAM\" />\n" +
                "        </static_modifications>\n" +
                "        <heavy_modifications />\n" +
                "      </peptide_modifications>\n" +
                "    </peptide_settings>\n" +
                "    <transition_settings>\n" +
                "      <transition_prediction precursor_mass_type=\"Monoisotopic\" fragment_mass_type=\"Monoisotopic\" optimize_by=\"None\">\n" +
                "        <predict_collision_energy name=\"Thermo TSQ Vantage\" step_size=\"1\" step_count=\"5\">\n" +
                "          <regression_ce charge=\"2\" slope=\"0.03\" intercept=\"2.905\" />\n" +
                "          <regression_ce charge=\"3\" slope=\"0.038\" intercept=\"2.281\" />\n" +
                "        </predict_collision_energy>\n" +
                "      </transition_prediction>\n" +
                "      <transition_filter precursor_charges=\"2\" product_charges=\"1\" fragment_types=\"y\" fragment_range_first=\"m/z &gt; precursor\" fragment_range_last=\"3 ions\" precursor_mz_window=\"0\" auto_select=\"true\">\n" +
                "        <measured_ion name=\"Water\" ion_formula=\"H2O3\" charge=\"1\" />\n" +
                "      </transition_filter>\n" +
                "      <transition_libraries ion_match_tolerance=\"0.5\" ion_count=\"3\" pick_from=\"all\" />\n" +
                "      <transition_integration />\n" +
                "      <transition_instrument min_mz=\"50\" max_mz=\"1500\" mz_match_tolerance=\"0.055\" />\n" +
                "    </transition_settings>\n" +
                "    <data_settings />\n" +
                "  </settings_summary>\n" +
                "  <peptide_list label_name=\"peptides1\" label_description=\"Bacteriocin amylovorin-L (Amylovorin-L471) (Lactobin-A)\" accession=\"P80696\" gene=\"amyL\" species=\"Lactobacillus amylovorus\" preferred_name=\"AMYL_LACAM\" websearch_status=\"X#Upeptides1\" auto_manage_children=\"false\">\n" +
                "    <peptide sequence=\"EFPDVAVFSGGR\" modified_sequence=\"EFPDVAVFSGGR\" calc_neutral_pep_mass=\"1279.619701\" num_missed_cleavages=\"0\">\n" +
                "      <precursor charge=\"2\" calc_neutral_mass=\"1279.619701\" precursor_mz=\"640.817127\" collision_energy=\"22.129514\" modified_sequence=\"EFPDVAVFSGGR\">\n" +
                "        <transition measured_ion_name=\"Water\" product_charge=\"1\">\n" +
                "          <precursor_mz>640.817127</precursor_mz>\n" +
                "          <product_mz>50.000394</product_mz>\n" +
                "        </transition>\n" +
                "        <transition measured_ion_name=\""+
                
                //Incoret Measured Ion Name
                "Incorrect"
                //*************************
                +"\" product_charge=\"2\">\n" +
                "          <precursor_mz>640.817127</precursor_mz>\n" +
                "          <product_mz>25.503835</product_mz>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"y\" fragment_ordinal=\"9\" calc_neutral_mass=\"906.45593\" product_charge=\"1\" cleavage_aa=\"D\" loss_neutral_mass=\"0\">\n" +
                "          <precursor_mz>640.817127</precursor_mz>\n" +
                "          <product_mz>907.463206</product_mz>\n" +
                "          <collision_energy>22.129514</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"y\" fragment_ordinal=\"8\" calc_neutral_mass=\"791.428987\" product_charge=\"1\" cleavage_aa=\"V\" loss_neutral_mass=\"0\">\n" +
                "          <precursor_mz>640.817127</precursor_mz>\n" +
                "          <product_mz>792.436263</product_mz>\n" +
                "          <collision_energy>22.129514</collision_energy>\n" +
                "        </transition>\n" +
                "        <transition fragment_type=\"y\" fragment_ordinal=\"7\" calc_neutral_mass=\"692.360573\" product_charge=\"1\" cleavage_aa=\"A\" loss_neutral_mass=\"0\">\n" +
                "          <precursor_mz>640.817127</precursor_mz>\n" +
                "          <product_mz>693.367849</product_mz>\n" +
                "          <collision_energy>22.129514</collision_energy>\n" +
                "        </transition>\n" +
                "      </precursor>\n" +
                "    </peptide>\n" +
                "  </peptide_list>\n" +
                "</srm_settings>";
                                                 

        /// <summary>
        /// A test for SrmDocument deserialization with absent and empty tags.
        /// </summary>
        [TestMethod]
        public void DocumentSerializeStubTest()
        {
            AssertEx.Serialization<SrmDocument>(DOC_STUBS1, AssertEx.DocumentCloned);
            AssertEx.Serialization<SrmDocument>(DOC_STUBS2, AssertEx.DocumentCloned);
            AssertEx.Serialization<SrmDocument>(DOC_STUBS3, AssertEx.DocumentCloned);
            AssertEx.Serialization<SrmDocument>(DOC_STUBS4, AssertEx.DocumentCloned);
        }

        private const string DOC_STUBS1 =
            "<srm_settings>\n" +
            "</srm_settings>";

        private const string DOC_STUBS2 =
            "<srm_settings>\n" +
            "  <selected_proteins/>\n" +
            "</srm_settings>";

        private const string DOC_STUBS3 =
            "<srm_settings>\n" +
            "  <settings_summary name=\"Default\">\n" +
            "    <peptide_settings/>\n" +
            "    <transition_settings/>\n" +
            "  </settings_summary>" +
            "  <peptide_list label=\"list1\"/>\n" +
            "  <peptide_list label=\"list2\">\n" +
            "    <selected_peptides/>\n" +
            "  </peptide_list>\n" +
            "  <peptide_list label=\"list3\">\n" +
            "    <selected_peptides>\n" +
            "      <peptide sequence=\"YLGAYLLATLGGNASPSAQDVLK\" calc_neutral_pep_mass=\"2322.62757\" num_missed_cleavages=\"0\"/>\n" +
            "    </selected_peptides>\n" +
            "  </peptide_list>\n" +
            "  <peptide_list label=\"list4\">\n" +
            "    <peptide sequence=\"YLGAYLLATLGGNASPSAQDVLK\" calc_neutral_pep_mass=\"2322.62757\" num_missed_cleavages=\"0\">\n" +
            "       <selected_transitions/>\n" +
            "    </peptide>\n" +
            "  </peptide_list>\n" +
            "</srm_settings>";

        private const string DOC_STUBS4 =
            "<srm_settings>\n" +
            "  <settings_summary name=\"Default\">\n" +
            "    <peptide_settings/>\n" +
            "    <transition_settings/>\n" +
            "  </settings_summary>" +
            "  <selected_proteins>\n" +
            "    <protein name=\"gi|15674171|30S_ribosomal_pro\" description=\"gi|15674171|ref|NP_268346.1| 30S ribosomal protein S18 [Lactococcus lactis subsp. lactis]\">\n" +
            "      <sequence>\n" +
            "        MAQQRRGGFK RRKKVDFIAA NKIEVVDYKD TELLKRFISE RGKILPRRVT\n" +
            "        GTSAKNQRKV VNAIKRARVM ALLPFVAEDQ N</sequence>\n" +
            "    </protein>\n" +
            "  </selected_proteins>\n" +
            "</srm_settings>";

        private const string DOC_MOLECULE_MASSES_31 =
            "<srm_settings format_version=\"3.1\" software_version=\"Skyline (64-bit) \">" +
            "  <settings_summary name=\"Default\">" +
            "    <peptide_settings>" +
            "      <enzyme name=\"Trypsin\" cut=\"KR\" no_cut=\"P\" sense=\"C\" />" +
            "      <digest_settings max_missed_cleavages=\"0\" />" +
            "      <peptide_prediction use_measured_rts=\"true\" measured_rt_window=\"2\" use_spectral_library_drift_times=\"false\" />" +
            "      <peptide_filter start=\"25\" min_length=\"8\" max_length=\"25\" auto_select=\"true\">" +
            "        <peptide_exclusions />" +
            "      </peptide_filter>" +
            "      <peptide_libraries pick=\"library\" />" +
            "      <peptide_modifications max_variable_mods=\"3\" max_neutral_losses=\"1\">" +
            "        <static_modifications>" +
            "          <static_modification name=\"Carbamidomethyl (C)\" aminoacid=\"C\" formula=\"H3C2NO\" unimod_id=\"4\" short_name=\"CAM\" />" +
            "        </static_modifications>" +
            "        <heavy_modifications />" +
            "      </peptide_modifications>" +
            "    </peptide_settings>" +
            "    <transition_settings>" +
            "      <transition_prediction precursor_mass_type=\"Monoisotopic\" fragment_mass_type=\"Monoisotopic\" optimize_by=\"None\">" +
            "        <predict_collision_energy name=\"Thermo TSQ Vantage\" step_size=\"1\" step_count=\"5\">" +
            "          <regression_ce charge=\"2\" slope=\"0.03\" intercept=\"2.905\" />" +
            "          <regression_ce charge=\"3\" slope=\"0.038\" intercept=\"2.281\" />" +
            "        </predict_collision_energy>" +
            "      </transition_prediction>" +
            "      <transition_filter precursor_charges=\"2\" product_charges=\"1\" fragment_types=\"y\" fragment_range_first=\"m/z &gt; precursor\" fragment_range_last=\"3 ions\" precursor_mz_window=\"0\" auto_select=\"true\">" +
            "        <measured_ion name=\"N-terminal to Proline\" cut=\"P\" sense=\"N\" min_length=\"3\" />" +
            "      </transition_filter>" +
            "      <transition_libraries ion_match_tolerance=\"0.5\" ion_count=\"3\" pick_from=\"all\" />" +
            "      <transition_integration />" +
            "      <transition_instrument min_mz=\"50\" max_mz=\"1500\" mz_match_tolerance=\"0.055\" />" +
            "    </transition_settings>" +
            "    <data_settings />" +
            "  </settings_summary>" +
            "  <peptide_list label_name=\"test\" websearch_status=\"X\">" +
            "    <molecule explicit_retention_time=\"1\" explicit_retention_time_window=\"2\" ion_formula=\"C12H19N37\" mass_average=\"681.52896\" mass_monoisotopic=\"681.262414\" custom_ion_name=\"Test_a\">" +
            "      <precursor charge=\"1\" precursor_mz=\"681.261865\" explicit_collision_energy=\"3\" explicit_drift_time_msec=\"4\" explicit_drift_time_high_energy_offset_msec=\"-0.5\" collision_energy=\"23.342856\">" +
            "        <transition fragment_type=\"custom\" ion_formula=\"C2H9N3\" mass_average=\"75.11326\" mass_monoisotopic=\"75.079647\" custom_ion_name=\"test aa\" product_charge=\"1\">" +
            "          <precursor_mz>681.261865</precursor_mz>" +
            "          <product_mz>75.079098</product_mz>" +
            "          <collision_energy>3</collision_energy>" +
            "        </transition>" +
            "      </precursor>" +
            "    </molecule>" +
            "    <molecule explicit_retention_time=\"2\" explicit_retention_time_window=\"3\" mass_average=\"695.53565957991\" mass_monoisotopic=\"695.26548757991\" custom_ion_name=\"\">" +
            "      <precursor charge=\"1\" precursor_mz=\"695.264939\" explicit_collision_energy=\"4\" explicit_drift_time_msec=\"5\" explicit_drift_time_high_energy_offset_msec=\"-0.6\" collision_energy=\"23.762948\">" +
            "        <transition fragment_type=\"custom\" mass_average=\"193.190159579909\" mass_monoisotopic=\"193.095016579909\" custom_ion_name=\"\" product_charge=\"1\">" +
            "          <precursor_mz>695.264939</precursor_mz>" +
            "          <product_mz>193.094468</product_mz>" +
            "          <collision_energy>4</collision_energy>" +
            "        </transition>" +
            "      </precursor>" +
            "    </molecule>" +
            "  </peptide_list>" +
            "</srm_settings>";


        private const string DOC_MOLECULES_31 =
            "<srm_settings format_version=\"3.1\" software_version=\"Skyline (64-bit) \">" +
            "  <settings_summary name=\"Default\">" +
            "    <peptide_settings>" +
            "      <enzyme name=\"Trypsin\" cut=\"KR\" no_cut=\"P\" sense=\"C\" />" +
            "      <digest_settings max_missed_cleavages=\"0\" />" +
            "      <peptide_prediction use_measured_rts=\"true\" measured_rt_window=\"2\" use_spectral_library_drift_times=\"false\" />" +
            "      <peptide_filter start=\"25\" min_length=\"8\" max_length=\"25\" auto_select=\"true\">" +
            "        <peptide_exclusions />" +
            "      </peptide_filter>" +
            "      <peptide_libraries pick=\"library\" />" +
            "      <peptide_modifications max_variable_mods=\"3\" max_neutral_losses=\"1\">" +
            "        <static_modifications>" +
            "          <static_modification name=\"Carbamidomethyl (C)\" aminoacid=\"C\" formula=\"H3C2NO\" unimod_id=\"4\" short_name=\"CAM\" />" +
            "        </static_modifications>" +
            "        <heavy_modifications />" +
            "      </peptide_modifications>" +
            "    </peptide_settings>" +
            "    <transition_settings>" +
            "      <transition_prediction precursor_mass_type=\"Monoisotopic\" fragment_mass_type=\"Monoisotopic\" optimize_by=\"None\">" +
            "        <predict_collision_energy name=\"Thermo TSQ Vantage\" step_size=\"1\" step_count=\"5\">" +
            "          <regression_ce charge=\"2\" slope=\"0.03\" intercept=\"2.905\" />" +
            "          <regression_ce charge=\"3\" slope=\"0.038\" intercept=\"2.281\" />" +
            "        </predict_collision_energy>" +
            "      </transition_prediction>" +
            "      <transition_filter precursor_charges=\"2\" product_charges=\"1\" fragment_types=\"y\" fragment_range_first=\"m/z &gt; precursor\" fragment_range_last=\"3 ions\" precursor_mz_window=\"0\" auto_select=\"true\">" +
            "        <measured_ion name=\"N-terminal to Proline\" cut=\"P\" sense=\"N\" min_length=\"3\" />" +
            "      </transition_filter>" +
            "      <transition_libraries ion_match_tolerance=\"0.5\" ion_count=\"3\" pick_from=\"all\" />" +
            "      <transition_integration />" +
            "      <transition_instrument min_mz=\"50\" max_mz=\"1500\" mz_match_tolerance=\"0.055\" />" +
            "    </transition_settings>" +
            "    <data_settings />" +
            "  </settings_summary>" +
            "  <peptide_list label_name=\"TestMolecule\" websearch_status=\"X\">" +
            "    <molecule explicit_retention_time=\"1\" explicit_retention_time_window=\"2\" ion_formula=\"C12H99\" mass_average=\"243.91626\" mass_monoisotopic=\"243.774678\" custom_ion_name=\"testMol\">" +
            "      <precursor charge=\"1\" precursor_mz=\"243.774129\" explicit_collision_energy=\"3\" explicit_drift_time_msec=\"4\" explicit_drift_time_high_energy_offset_msec=\"-0.5\" collision_energy=\"10.218224\">" +
            "        <transition fragment_type=\"custom\" ion_formula=\"C6H12\" mass_average=\"84.16038\" mass_monoisotopic=\"84.0939\" custom_ion_name=\"testTrans\" product_charge=\"1\">" +
            "          <precursor_mz>243.774129</precursor_mz>" +
            "          <product_mz>84.093351</product_mz>" +
            "          <collision_energy>3</collision_energy>" +
            "        </transition>" +
            "      </precursor>" +
            "    </molecule>" +
            "  </peptide_list>" +
            "</srm_settings>";

        /// <summary>
        /// A test for PeptideGroup serialization.
        /// </summary>
        [TestMethod]
        public void PeptideGroupSerializeTest()
        {
            foreach (string xml in _peptideGroupValid)
                AssertEx.DeserializeNoError<SrmDocument>(xml, false);
            foreach (string xml in _peptideGroupInvalid)
                AssertEx.DeserializeError<SrmDocument>(xml);
            foreach (string xml in _peptideGroupInvalidXml)
                AssertEx.DeserializeError<SrmDocument, XmlException>(xml);
        }

        private readonly string[] _peptideGroupValid =
        {
            // Protein name and desc
            "<srm_settings><selected_proteins>\n" +
            "  <protein name=\"test\" description=\"desc\"><sequence>ABCDEFG HIJKLMNP QRSTUV WXYZ</sequence></protein>\n" +
            "</selected_proteins></srm_settings>",
            // Protein label and label_desc
            "<srm_settings><selected_proteins>\n" +
            "  <protein label=\"test\" label_description=\"desc\"><sequence>R</sequence></protein>\n" +
            "</selected_proteins></srm_settings>",
            // Protein label and label_desc
            "<srm_settings><selected_proteins>\n" +
            "  <protein label=\"test\" label_description=\"desc\"><sequence>R</sequence></protein>\n" +
            "</selected_proteins></srm_settings>",
            // Protein with no name or desc
            "<srm_settings><selected_proteins>\n" +
            "  <protein><sequence>R</sequence></protein>\n" +
            "</selected_proteins></srm_settings>",
            // v0.1-style peptide list with name
            "<srm_settings><selected_proteins>\n" +
            "  <protein name=\"test\" peptide_list=\"true\"><sequence>R</sequence></protein>\n" +
            "</selected_proteins></srm_settings>",
            // peptide list with label
            "<srm_settings><selected_proteins>\n" +
            "  <peptide_list label=\"test\"/>\n" +
            "</selected_proteins></srm_settings>",
            // peptide list with label
            "<srm_settings><selected_proteins>\n" +
            "  <peptide_list label=\"test\" label_description=\"desc\"/>\n" +
            "</selected_proteins></srm_settings>",
            // blank and empty peptide list
            "<srm_settings><selected_proteins>\n" +
            "  <peptide_list/>\n" +
            "</selected_proteins></srm_settings>",
        };

        private readonly string[] _peptideGroupInvalid =
        {
            // Protein with invalid sequence
            "<srm_settings><selected_proteins>\n" +
            "  <protein name=\"test\"><sequence>jp</sequence></protein>\n" +
            "</selected_proteins></srm_settings>",
            "<srm_settings><selected_proteins>\n" +
            "  <protein name=\"test\"><sequence>abc</sequence></protein>\n" +
            "</selected_proteins></srm_settings>",
        };

        private readonly string[] _peptideGroupInvalidXml =
        {
            // Missing sequence
            "<srm_settings><protein name=\"test\" description=\"desc\"/></srm_settings>",
            // Peptide list with sequence
            "<srm_settings><peptide_list><sequence>R</sequence></peptide_list></srm_settings>"
        };

        /// <summary>
        /// A test for Peptide serialization.
        /// </summary>
        [TestMethod]
        public void PeptideSerializeTest()
        {
            foreach (string xml in _peptideValid)
                AssertEx.DeserializeNoError<SrmDocument>(xml, false);
            foreach (string xml in _peptideInvalid)
                AssertEx.DeserializeError<SrmDocument>(xml);
        }

        private readonly string[] _peptideValid =
        {
            // Sequence completely covered
            "<srm_settings><protein><sequence>ABCDEFGHIJKLMNPQRSTUVWXYZ</sequence>\n" +
            "  <peptide start=\"0\" end=\"25\" sequence=\"ABCDEFGHIJKLMNPQRSTUVWXYZ\" calc_neutral_pep_mass=\"1685.86477\" num_missed_cleavages=\"2\"/>\n" +
            "</protein></srm_settings>",
            // Single amino acid peptide
            "<srm_settings><protein><sequence>ABC</sequence>\n" +
            "  <peptide start=\"1\" end=\"2\" sequence=\"B\" num_missed_cleavages=\"0\"/>\n" +
            "</protein></srm_settings>",
            // v0.1-style peptide list with peptide
            "<srm_settings><protein peptide_list=\"true\"><sequence>ABCDEFGHIJKLMNPQRSTUVWXYZ</sequence>\n" +
            "  <peptide sequence=\"ABCDEFGHIJKLMNPQRSTUVWXYZ\"/>\n" +
            "</protein></srm_settings>",
            // peptide list with peptide
            "<srm_settings><peptide_list>\n" +
            "  <peptide sequence=\"ABCDEFGHIJKLMNPQRSTUVWXYZ\" num_missed_cleavages=\"0\"/>\n" +
            "</peptide_list></srm_settings>",
        };

        private readonly string[] _peptideInvalid =
        {
            // Peptide outside bounds of sequence
            "<srm_settings><protein><sequence>ABCDEFGHIJKLMNPQRSTUVWXYZ</sequence>\n" +
            "  <peptide start=\"0\" end=\"26\" sequence=\"ABCDEFGHIJKLMNPQRSTUVWXYZ\"/>\n" +
            "</protein></srm_settings>",
            "<srm_settings><protein><sequence>ABCDEFGHIJKLMNPQRSTUVWXYZ</sequence>\n" +
            "  <peptide start=\"-1\" end=\"24\" sequence=\"ABCDEFGHIJKLMNPQRSTUVWXYZ\"/>\n" +
            "</protein></srm_settings>",
            // Missing bounds
            "<srm_settings><protein><sequence>A</sequence>\n" +
            "  <peptide start=\"0\" sequence=\"A\" />\n" +
            "</protein></srm_settings>",
            "<srm_settings><protein><sequence>AB</sequence>\n" +
            "  <peptide end=\"1\" sequence=\"A\" />\n" +
            "</protein></srm_settings>",
            // Missmatched protein and peptide sequences
            "<srm_settings><protein><sequence>ABC</sequence>\n" +
            "  <peptide start=\"1\" end=\"2\" sequence=\"C\"/>\n" +
            "</protein></srm_settings>",
            // v0.1-style peptide list with bad sequence peptide
            "<srm_settings><protein peptide_list=\"true\"><sequence>ABCDEFGHIJ</sequence>\n" +
            "  <peptide sequence=\"jp\"/>\n" +
            "</protein></srm_settings>",
            // peptide with no sequence
            "<srm_settings><peptide_list>\n" +
            "    <peptide/>\n" +
            "</peptide_list></srm_settings>",
        };

        private readonly string DOC_LABEL_IMPLEMENTED = 
        "<srm_settings format_version=\"4.11\" software_version=\"Skyline-daily (64-bit) 4.1.1.18118\">\n" + // Keep -daily
        "  <settings_summary name=\"Default\">\n" +
        "    <peptide_settings>\n" +
        "      <enzyme name=\"TrypsinR\" cut=\"R\" no_cut=\"P\" sense=\"C\" />\n" +
        "      <digest_settings max_missed_cleavages=\"2\" exclude_ragged_ends=\"true\" />\n" +
        "      <peptide_prediction use_measured_rts=\"true\" measured_rt_window=\"2\" use_spectral_library_drift_times=\"false\" spectral_library_drift_times_peak_width_calc_type=\"resolving_power\" spectral_library_drift_times_resolving_power=\"0\" spectral_library_drift_times_width_at_dt_zero=\"0\" spectral_library_drift_times_width_at_dt_max=\"0\" />\n" +
        "      <peptide_filter start=\"25\" min_length=\"8\" max_length=\"25\" auto_select=\"true\">\n" +
        "        <peptide_exclusions>\n" +
        "          <exclusion name=\"Lys\" regex=\"[K]\" />\n" +
        "        </peptide_exclusions>\n" +
        "      </peptide_filter>\n" +
        "      <peptide_libraries pick=\"library\">\n" +
        "      </peptide_libraries>\n" +
        "      <peptide_modifications max_variable_mods=\"3\" max_neutral_losses=\"1\" internal_standard=\"13C\">\n" +
        "        <heavy_modifications isotope_label=\"13C15N\">\n" +
        "          <static_modification name=\"Label:13C\" label_13C=\"true\" />\n" +
        "        </heavy_modifications>\n" +
        "        <heavy_modifications isotope_label=\"13C\" />\n" +
        "      </peptide_modifications>\n" +
        "    </peptide_settings>\n" +
        "    <transition_settings>\n" +
        "      <transition_prediction precursor_mass_type=\"Monoisotopic\" fragment_mass_type=\"Monoisotopic\" optimize_by=\"None\">\n" +
        "        <predict_collision_energy name=\"Thermo TSQ Vantage\" step_size=\"1\" step_count=\"5\">\n" +
        "          <regression_ce charge=\"2\" slope=\"0.03\" intercept=\"2.905\" />\n" +
        "          <regression_ce charge=\"3\" slope=\"0.038\" intercept=\"2.281\" />\n" +
        "        </predict_collision_energy>\n" +
        "      </transition_prediction>\n" +
        "      <transition_filter precursor_charges=\"2,3,4\" product_charges=\"1\" precursor_adducts=\"[M-3H],[M-2H],[M-H],[M-],[M+H],[M+],[M+2H],[M+3H]\" product_adducts=\"[M-3],[M-2],[M-],[M+],[M+2],[M+3]\" fragment_types=\"p\" small_molecule_fragment_types=\"f\" fragment_range_first=\"m/z &gt; precursor\" fragment_range_last=\"3 ions\" precursor_mz_window=\"0\" auto_select=\"true\">\n" +
        "        <measured_ion name=\"N-terminal to Proline\" cut=\"P\" sense=\"N\" min_length=\"3\" />\n" +
        "      </transition_filter>\n" +
        "      <transition_libraries ion_match_tolerance=\"0.5\" min_ion_count=\"0\" ion_count=\"3\" pick_from=\"none\" />\n" +
        "      <transition_integration integrate_all=\"true\" />\n" +
        "      <transition_instrument min_mz=\"20\" max_mz=\"1000\" mz_match_tolerance=\"0.055\" />\n" +
        "    </transition_settings>\n" +
        "    <data_settings document_guid=\"8506073d-6c6a-40bd-9562-7fd9b26057b5\" />\n" +
        "    <measured_results time_normal_area=\"true\">\n" +
        "    </measured_results>\n" +
        "  </settings_summary>\n" +
        "  <peptide_list label_name=\"all_rpPos_iroa\" label_description=\"\" websearch_status=\"X\" auto_manage_children=\"false\">\n" +
        "    <molecule explicit_retention_time=\"4.73\" explicit_retention_time_window=\"0.5\" auto_manage_children=\"false\" standard_type=\"Surrogate Standard\" neutral_formula=\"C5H9NO2\" neutral_mass_average=\"115.13121\" neutral_mass_monoisotopic=\"115.06332857499999\" custom_ion_name=\"PROLINE\" avg_measured_retention_time=\"4.695422\">\n" +
        "      <precursor charge=\"1\" precursor_mz=\"116.070605\" collision_energy=\"6.387118\" ion_formula=\"C5H9NO2[M+H]\" neutral_mass_average=\"115.13121\" neutral_mass_monoisotopic=\"115.06332857499999\" custom_ion_name=\"PROLINE\">\n" +
        "        <transition fragment_type=\"custom\" ion_formula=\"C4NH8[M+]\" neutral_mass_average=\"70.11362\" neutral_mass_monoisotopic=\"70.06567428\" product_charge=\"1\">\n" +
        "          <precursor_mz>116.070605</precursor_mz>\n" +
        "          <product_mz>70.065126</product_mz>\n" +
        "          <collision_energy>6.387118</collision_energy>\n" +
        "        </transition>\n" +
        "      </precursor>\n" +
        "      <precursor charge=\"1\" isotope_label=\"13C\" precursor_mz=\"121.087379\" collision_energy=\"6.537621\" ion_formula=\"C5H9NO2[M5C13+H]\" neutral_mass_average=\"115.13121\" neutral_mass_monoisotopic=\"115.06332857499999\" custom_ion_name=\"PROLINE\">\n" +
        "        <transition fragment_type=\"custom\" ion_formula=\"C4NH8[M4C13+]\" neutral_mass_average=\"70.11362\" neutral_mass_monoisotopic=\"70.06567428\" product_charge=\"1\">\n" +
        "          <precursor_mz>121.087379</precursor_mz>\n" +
        "          <product_mz>74.078545</product_mz>\n" +
        "          <collision_energy>6.537621</collision_energy>\n" +
        "        </transition>\n" +
        "      </precursor>\n" +
        "      <precursor charge=\"1\" isotope_label=\"13C15N\" precursor_mz=\"122.084414\" auto_manage_children=\"false\" collision_energy=\"6.567532\" ion_formula=\"C5H9NO2[M5C13N15+H]\" neutral_mass_average=\"115.13121\" neutral_mass_monoisotopic=\"115.06332857499999\" custom_ion_name=\"PROLINE\">\n" +
        "        <transition fragment_type=\"custom\" ion_formula=\"C4NH8[M4C13N15+]\" neutral_mass_average=\"70.11362\" neutral_mass_monoisotopic=\"70.06567428\" custom_ion_name=\"C4NH8\" product_charge=\"1\">\n" +
        "          <precursor_mz>122.084414</precursor_mz>\n" +
        "          <product_mz>75.07558</product_mz>\n" +
        "          <collision_energy>6.567532</collision_energy>\n" +
        "        </transition>\n" +
        "      </precursor>\n" +
        "    </molecule>\n" +
        "  </peptide_list>\n" +
        "</srm_settings>";

        [TestMethod]
        public void TestDocumentSchemaDocuments()
        {
            foreach (var skylineVersion in SkylineVersion.SupportedForSharing())
            {
                var schemaFileName =
                    SchemaDocuments.GetSkylineSchemaResourceName(skylineVersion.SrmDocumentVersion.ToString());
                var resourceStream = typeof(SchemaDocuments).Assembly.GetManifestResourceStream(schemaFileName);
                Assert.IsNotNull(resourceStream, "Unable to find schema document {0} for Skyline Version {1}", schemaFileName, skylineVersion);
            }
        }
    }
}
