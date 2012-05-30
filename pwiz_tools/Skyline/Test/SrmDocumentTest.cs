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
using System.IO;
using System.Linq;
using System.Xml;
using pwiz.Skyline.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.V01;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// This is a test class for SrmDocumentTest and is intended
    /// to contain all SrmDocumentTest Unit Tests
    /// </summary>
    [TestClass]
    public class SrmDocumentTest
    {
        /// <summary>
        /// Gets or sets the test context which provides
        /// information about and functionality for the current test run.
        /// </summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes

        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //

        #endregion

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

        /// <summary>
        /// A test for SrmDocument deserialization of v0.1 documents and
        /// general serialization.
        /// </summary>
        [TestMethod]
        public void DocumentNodeCountsTest()
        {
            SrmDocument doc = AssertEx.Deserialize<SrmDocument>(DOC_0_1_BOVINE);
            Assert.AreEqual(2, doc.PeptideGroupCount);
            Assert.AreEqual(2, doc.PeptideCount);
            Assert.AreEqual(2, doc.TransitionGroupCount);
            Assert.AreEqual(3, doc.TransitionCount);
            doc = AssertEx.Deserialize<SrmDocument>(DOC_0_1_PEPTIDES);
            Assert.AreEqual(3, doc.PeptideGroupCount);
            Assert.AreEqual(6, doc.PeptideCount);
            Assert.AreEqual(5, doc.TransitionGroupCount);
            Assert.AreEqual(13, doc.TransitionCount);
            PeptideGroupDocNode nodeGroup = (PeptideGroupDocNode) doc.Children[1];
            SrmDocument docNew = (SrmDocument) doc.RemoveChild(nodeGroup);
            Assert.AreEqual(2, docNew.PeptideGroupCount);
            Assert.AreEqual(4, docNew.PeptideCount);
            Assert.AreEqual(4, docNew.TransitionGroupCount);
            Assert.AreEqual(12, docNew.TransitionCount);
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
            Assert.AreEqual(3, docNew.PeptideGroupCount);
            Assert.AreEqual(5, docNew.PeptideCount);
            Assert.AreEqual(4, docNew.TransitionGroupCount);
            Assert.AreEqual(12, docNew.TransitionCount);
            docNew = (SrmDocument) docNew.ChangeChildren(new[] {nodeGroup});
            Assert.AreEqual(1, docNew.PeptideGroupCount);
            Assert.AreEqual(2, docNew.PeptideCount);
            Assert.AreEqual(1, docNew.TransitionGroupCount);
            Assert.AreEqual(1, docNew.TransitionCount);
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
            count = EqualCsvs(DOC_0_1_PEPTIDES_NO_EMPTY, 0, CreateAbiExporters, ExportStrategy.Buckets, 1, 6,
                              ExportMethodType.Standard);
            // TODO: Test scheduled runs
            Assert.AreEqual(3, count);
        }

        private static int EqualCsvs(string xml, int countFields, CreateExporters exporters,
                                     ExportStrategy strategy, int minTransition, int? maxTransition, ExportMethodType methodType)
        {
            XmlSrmDocument target = AssertEx.Deserialize<XmlSrmDocument>(xml);
            SrmDocument actual = AssertEx.Deserialize<SrmDocument>(xml);

            return EqualCsvs(target, actual, countFields, exporters, strategy, minTransition, maxTransition, methodType);
        }

        private static int EqualCsvs(XmlSrmDocument target, SrmDocument actual, int coundFields, CreateExporters exporters,
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
                if (coundFields > 0)
                    AssertEx.FieldsEqual(targetList, actualList, coundFields);
                else
                    AssertEx.NoDiff(targetList, actualList);

                // Import the exported list
                using (var readerImport = new StringReader(actualList))
                {
                    IdentityPath pathAdded;
                    IFormatProvider provider = CultureInfo.InvariantCulture;
                    docImport = docImport.ImportMassList(readerImport, provider, ',', IdentityPath.ROOT, out pathAdded);
                }
            }

            if (minTransition < 2)
                CheckImportSimilarity(actual, docImport);

            return exportedTarget.Count;
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

        private static void CheckImportSimilarity(SrmDocument document, SrmDocument docImport)
        {
            CheckImportSimilarity(document.PeptideGroups, docImport.PeptideGroups,
                (g1, g2) => Assert.AreEqual(g1.Name, g2.Name));
            CheckImportSimilarity(document.Peptides, docImport.Peptides,
                (p1, p2) => Assert.AreEqual(p1.Peptide.Sequence, p2.Peptide.Sequence));
            CheckImportSimilarity(document.TransitionGroups, docImport.TransitionGroups,
                (g1, g2) => Assert.AreEqual(g1.PrecursorMz, g2.PrecursorMz));
            CheckImportSimilarity(document.Transitions, docImport.Transitions,
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

        /// <summary>
        /// A test for PeptideGroup serialization.
        /// </summary>
        [TestMethod]
        public void PeptideGroupSerializeTest()
        {
            foreach (string xml in _peptideGroupValid)
                AssertEx.DeserializeNoError<SrmDocument>(xml);
            foreach (string xml in _peptideGroupInvalid)
                AssertEx.DeserializeError<SrmDocument>(xml);
            foreach (string xml in _peptideGroupInvalidXml)
                AssertEx.DeserializeError<SrmDocument, XmlException>(xml);
        }

        private readonly string[] _peptideGroupValid = new[]
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

        private readonly string[] _peptideGroupInvalid = new[]
            {
                // Protein with invalid sequence
                "<srm_settings><selected_proteins>\n" +
                "  <protein name=\"test\"><sequence>jp</sequence></protein>\n" +
                "</selected_proteins></srm_settings>",
                "<srm_settings><selected_proteins>\n" +
                "  <protein name=\"test\"><sequence>abc</sequence></protein>\n" +
                "</selected_proteins></srm_settings>",
            };

        private readonly string[] _peptideGroupInvalidXml = new[]
            {
                // Missing sequence
                "<srm_settings><protein name=\"test\" description=\"desc\"/></srm_settings>",
                // Peptide list with sequence
                "<srm_settings><peptide_list><sequence>R</sequence></peptide_list></srm_settings>"
            };

        /// <summary>
        /// A test for PeptideGroup serialization.
        /// </summary>
        [TestMethod]
        public void PeptideSerializeTest()
        {
            foreach (string xml in _peptideValid)
                AssertEx.DeserializeNoError<SrmDocument>(xml);
            foreach (string xml in _peptideInvalid)
                AssertEx.DeserializeError<SrmDocument>(xml);
        }

        private readonly string[] _peptideValid = new[]
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

        private readonly string[] _peptideInvalid = new[]
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
    }
}
