/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    //This class holds basic unit tests for AlphapeptdeepLibraryBuilder internals,
    //most of the testing of AlphaPeptDeep resides in TestPerf\AlphapeptdeepBuildLibraryTest.cs 
    [TestClass]
    public class AlphapeptdeepLibraryBuilderTest : AbstractUnitTest
    {
        /// <summary>
        /// When true the test write the answer file
        /// </summary>
        public bool IsRecordMode => false;

        public string LogOutput => Path.Combine(TestContext.GetTestResultsPath(), "TestConsole.log");


        [TestMethod]
        public void TestModificationInfo()
        {
            var groupedByAccession = AlphapeptdeepLibraryBuilder.MODIFICATION_NAMES
                .GroupBy(item => item.Accession);
            foreach (var group in groupedByAccession)
            {
                Assert.AreEqual(1, group.Count(), "Duplicate accession {0}", group.Key);
            }
        }

        [TestMethod]
        public void TestAlphapeptdeepLibraryBuilder()
        {
            string outputPath = TestContext.GetTestResultsPath();
            
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
         
            var peptides = new[]
            {
                new Peptide("ELVISK"),
                new Peptide("LIVESK"),
                new Peptide("KINGPELVISKINK"),
                new Peptide("LIVER")
            };
         
            SrmDocument document = CreateTestSimpleDocument(peptides);

            TestGetPrecursorTable(document, outputPath, SIMPLE_PRECURSOR_TABLE_ANSWER);
            TestGetWarningMods(document, outputPath); // No warnings should be generated
    
            document = CreateTestImportedDoc();

            TestGetPrecursorTable(document, outputPath, MIXED_PRECURSOR_TABLE_ANSWER);
            TestGetWarningMods(document, outputPath, 1);
            TestGetWarningMods(document, outputPath, 1);

            TestValidateModifications_Supported(outputPath);
            TestValidateModifications_Unsupported(outputPath);

            TestTransformPeptDeepOutput(outputPath);
            TestImportSpectralLibrary(outputPath);

            Directory.Delete(outputPath, true);
        }

        /// <summary>
        /// Tests an AlphaPeptDeep unsupported modification.
        /// </summary>
        /// <param name="outputPath">Path to the output directory.</param>
        public void TestImportSpectralLibrary(string outputPath)
        {
            var document = CreateTestEmptyDocument();
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", outputPath, document, null);
            var progress = new SilentProgressMonitor();
            IProgressStatus status = new ProgressStatus();


            var input = "predict_transformed.speclib.tsv";
            var output = "predict_transformed.speclib.blib";
            var answer = "predict_transformed.speclib.blib.answer";

            using (var stream = GetType().Assembly.GetManifestResourceStream(GetType().Namespace + "." + input))
            {
                using (var fileStream = new FileStream(Path.Combine(outputPath, input), FileMode.Create, FileAccess.Write))
                {
                    // Copy the resource stream to the file
                    if (stream != null)
                        stream.CopyTo(fileStream);
                    else
                        Console.WriteLine($@"Resource '{input}' not found.");
                }
            }

            using (var stream = GetType().Assembly.GetManifestResourceStream(GetType().Namespace + "." + output))
            {
                using (var fileStream = new FileStream(Path.Combine(outputPath, answer), FileMode.Create, FileAccess.Write))
                {
                    // Copy the resource stream to the file
                    if (stream != null)
                        stream.CopyTo(fileStream);
                    else
                        Console.WriteLine($@"Resource '{input}' not found.");
                }
            }
         
            answer = Path.Combine(outputPath, answer);
            input = Path.Combine(outputPath, input);
            output = Path.Combine(outputPath, output);
            builder.ImportSpectralLibrary(progress, ref status, input, output);

            using (var answerReader = new StreamReader(answer))
            using (var productReader = new StreamReader(output))
            {
                var ansInfo = new FileInfo(answer);
                var outInfo = new FileInfo(output);
                Assert.AreEqual(ansInfo.Length, outInfo.Length);
            }
        }
        /// <summary>
        /// Tests an AlphaPeptDeep unsupported modification.
        /// </summary>
        /// <param name="outputPath">Path to the output directory.</param>
        public void TestTransformPeptDeepOutput(string outputPath)
        {
            var document = CreateTestEmptyDocument();
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", outputPath, document, IrtStandard.BIOGNOSYS_11);
            var progress = new SilentProgressMonitor();
            IProgressStatus status = new ProgressStatus();


            var input = "predict.speclib.tsv";
            var output = "predict_transformed.speclib.tsv";
            var answer = "predict_transformed.speclib.tsv.answer";

            using (var stream = GetType().Assembly.GetManifestResourceStream(GetType().Namespace + "." + input))
            {
                using (var fileStream = new FileStream(Path.Combine(outputPath, input), FileMode.Create, FileAccess.Write))
                {
                    // Copy the resource stream to the file
                    if (stream != null) 
                        stream.CopyTo(fileStream);
                    else 
                        Console.WriteLine($@"Resource '{input}' not found.");
                }
            }



            using (var stream = GetType().Assembly.GetManifestResourceStream(GetType().Namespace + "." + output))
            {
                using (var fileStream = new FileStream(Path.Combine(outputPath, answer), FileMode.Create, FileAccess.Write))
                {
                    // Copy the resource stream to the file
                    if (stream != null)
                        stream.CopyTo(fileStream);
                    else
                        Console.WriteLine($@"Resource '{input}' not found.");
                }
            }
         
            answer = Path.Combine(outputPath, answer);
            input = Path.Combine(outputPath, input);
            output = Path.Combine(outputPath, output);
            builder.TransformPeptdeepOutput(progress, ref status, input, output);

            using (var answerReader = new StreamReader(answer))
            using (var productReader = new StreamReader(output))
            {
                AssertEx.FieldsEqual(answerReader, productReader, 13, null);
            }
        }
        /// <summary>
        /// Test of AlphapeptdeepLibraryBuilder.GetWarningMods()
        /// </summary>
        /// <param name="document">Input SrmDocument.</param>
        /// <param name="outputPath">Path to the output directory.</param>
        /// <param name="expectedWarningCount">Expected count of warnings generated.</param>
        public void TestGetWarningMods(SrmDocument document, string outputPath, int expectedWarningCount = 0)
        {
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", outputPath, document, IrtStandard.BIOGNOSYS_11);
            var warningList = builder.GetWarningMods();

            Assert.AreEqual(expectedWarningCount, warningList.Count);
        }

        /// <summary>
        /// Test of AlphapeptdeepLibraryBuilder.GetPrecursorTable
        /// </summary>
        /// <param name="document">Input SrmDocument.</param>
        /// <param name="outputPath">Path to the output directory.</param>
        /// <param name="expectedLines">Expected lines of output.</param>
        public void TestGetPrecursorTable(SrmDocument document, string outputPath, IEnumerable<string> expectedLines)
        {
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", outputPath, document, IrtStandard.BIOGNOSYS_11);
            var precursorTable = builder.GetPrecursorTable(false);

            var generatedResult = precursorTable as string[] ?? precursorTable.ToArray();
           
            if (IsRecordMode)
            {
                File.WriteAllLines(LogOutput, generatedResult);
            }

            var generatedOutput = String.Join(Environment.NewLine, generatedResult);
            var expectedOutput = String.Join(Environment.NewLine, expectedLines);

            Assert.AreEqual(expectedOutput, generatedOutput);
        }

        /// <summary>
        /// Tests an AlphaPeptDeep unsupported modification.
        /// </summary>
        /// <param name="outputPath">Path to the output directory.</param>
        public void TestValidateModifications_Unsupported(string outputPath)
        {
            var document = CreateTestEmptyDocument();
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", outputPath, document, IrtStandard.BIOGNOSYS_11);

            var peptideList = new[]
            {
                new Peptide("MSGSHSNDEDDVVQVPETSSPTK")
            };

            var answer_mods = new[]
            {
                "Acetyl-Oxidation (N-term-M)" //unsupported
            };

            var answer_modSites = new[]
            {
                "1" //unsupported
            };

            var aceOxMetMod = new StaticMod("Acetyl-Oxidation (N-term-M)", "M",ModTerminus.N, true, "H2C2O2", LabelAtoms.None, RelativeRT.Unknown, null,
                null, null, -1, "Acetyl-Ox");

            var explicitMods1 = new[]
            {
                new ExplicitMod(0, aceOxMetMod)
            };

            IEnumerable<ExplicitMods> explicitMods = new[]
            {
                new ExplicitMods(peptideList[0], explicitMods1, null, true),
            };

            var peptides = CreatePeptideDocNodes(peptideList, explicitMods).ToArray();
            for (int i = 0; i < peptides.Length; i++)
            {
                var modifiedSeq = ModifiedSequence.GetModifiedSequence(document.Settings, peptides[i], IsotopeLabelType.light);
                string mods;
                string modSites;
                Assert.IsFalse(builder.ValidateModifications(modifiedSeq, out mods, out modSites));
                Assert.AreNotEqual(answer_mods[i], mods);
                Assert.AreNotEqual(answer_modSites[i], modSites);
            }
        }

        /// <summary>
        /// Tests AlphaPeptDeep supported modifications only, of several types.
        /// </summary>
        /// <param name="outputPath">Path to the output directory.</param>
        public void TestValidateModifications_Supported(string outputPath)
        {
            var document = CreateTestEmptyDocument();
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", outputPath, document, IrtStandard.BIOGNOSYS_11);

            var peptideList = new[]
            {
                new Peptide("CELVISK"),
                new Peptide("CLIVECSK"),
                new Peptide("CKINGMMPELVISCCKIMK"),
                new Peptide("CKINGMMPELVISCCKIMK"),
                new Peptide("CKINGMMPELVISCCKIMK"),
                new Peptide("LIVER")
            };

            var answer_mods = new[]
            {
                "Carbamidomethyl@C",
                "Carbamidomethyl@C;Carbamidomethyl@C",
                "Carbamidomethyl@C;Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C",
                "Carbamidomethyl@C;Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C",
                "Carbamidomethyl@C;Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C;Oxidation@M",
                ""
            };

            var answer_modSites = new[]
            {
                "1",
                "1;6",
                "1;6;14;15",
                "1;7;14;15",
                "1;7;14;15;18",
                ""
            };

            var oxMetMod = new StaticMod("Oxidation (M)", "M", null, true, "O", LabelAtoms.None, RelativeRT.Unknown, null,
                null, null, 35, "Ox");

            var explicitMods2 = new[]
            {
                new ExplicitMod(5, oxMetMod)
            };

            var explicitMods3 = new[]
            {
                new ExplicitMod(6, oxMetMod)
            };

            var explicitMods4 = new[]
            {
                new ExplicitMod(6, oxMetMod),
                new ExplicitMod(17, oxMetMod)
            };

            IEnumerable<ExplicitMods> explicitMods = new[]
            {
                null,
                null,
                new ExplicitMods(peptideList[2], explicitMods2, null, true),
                new ExplicitMods(peptideList[3], explicitMods3, null, true),
                new ExplicitMods(peptideList[4], explicitMods4, null, true),
                null
            };
            
            var peptides = CreatePeptideDocNodes(peptideList, explicitMods).ToArray();
            for (int i = 0; i< peptides.Length; i++)
            {
                var modifiedSeq = ModifiedSequence.GetModifiedSequence(document.Settings, peptides[i], IsotopeLabelType.light);
                string mods;
                string modSites;
                Assert.IsTrue(builder.ValidateModifications(modifiedSeq, out mods, out modSites));
                Assert.AreEqual(answer_mods[i], mods);
                Assert.AreEqual(answer_modSites[i], modSites);
            }
        }
        /// <summary>
        /// Creates an SrmDocument from the embedded resource file "Test-imported_24-11-short.sky"
        /// </summary>
        /// <returns>An SrmDocument.</returns>
        private SrmDocument CreateTestImportedDoc()
        {
            var document = ResultsUtil.DeserializeDocument("Test-imported_24-11-short.sky", GetType());
            AssertEx.IsDocumentState(document, 0, 1, 9, 27, 327);
            return document;
        }

        /// <summary>
        /// Creates a list of PeptideDocNodes.
        /// </summary>
        /// <param name="peptideList">List of Peptides.</param>
        /// <param name="explicitMods">List of ExplicitMods, if not null should have same length as peptideList.</param>
        /// <returns>List of PeptideDocNodes.</returns>
        private IEnumerable<PeptideDocNode> CreatePeptideDocNodes(IEnumerable<Peptide> peptideList, IEnumerable<ExplicitMods> explicitMods = null)
        {
            var srmSettings = SrmSettingsList.GetDefault();


            var transitions = new TransitionGroupDocNode[1];
            var peptides = peptideList as Peptide[] ?? peptideList.ToArray();
            var peptideNodes = new PeptideDocNode[peptides.Length];
            var expMods = explicitMods == null ? null : explicitMods as ExplicitMods[] ?? explicitMods.ToArray();

            for (int i = 0; i < peptideNodes.Length; i++)
            {
                ExplicitMods mods = null;
                
                if (expMods != null)
                    mods = expMods[i];

                transitions[0] = new TransitionGroupDocNode(
                    new TransitionGroup(peptides[i], Adduct.DOUBLY_PROTONATED, IsotopeLabelType.light),
                    Annotations.EMPTY, srmSettings,
                    mods, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);

                peptideNodes[i] = new PeptideDocNode(peptides[i], srmSettings, mods, null,
                    ExplicitRetentionTimeInfo.EMPTY, transitions, true);
            }


            //Add precursors and transitions
            foreach (var peptideNode in peptideNodes)
            {
                peptideNode.ChangeSettings(srmSettings, SrmSettingsDiff.ALL);
            }
            return peptideNodes;

        }

        /// <summary>
        /// Creates an empty test SrmDocument.
        /// </summary>
        /// <returns>An empty SrmDocument.</returns>
        private SrmDocument CreateTestEmptyDocument()
        {
            var peptideNodes = new PeptideDocNode[]{};
            var doc = new SrmDocument(SrmSettingsList.GetDefault());
         
            doc = (SrmDocument)doc.ChangeChildren(new[]
            {
                new PeptideGroupDocNode(new PeptideGroup(), "Peptide List", null, peptideNodes)
            });

            return doc;
        }

        /// <summary>
        /// Creates a simple test SrmDocument.
        /// </summary>
        /// <param name="peptideList">List of peptides to place into the document.</param>
        /// <returns>A simple SrmDocument.</returns>
        private SrmDocument CreateTestSimpleDocument(IEnumerable<Peptide> peptideList)
        {
            var peptideNodes = CreatePeptideDocNodes(peptideList).ToArray();
            var doc = new SrmDocument(SrmSettingsList.GetDefault());

            doc = (SrmDocument)doc.ChangeChildren(new[]
            {
                new PeptideGroupDocNode(new PeptideGroup(), "Peptide List", null, peptideNodes)
            });

            return doc;
        }
        private readonly IEnumerable<string> SIMPLE_PRECURSOR_TABLE_ANSWER =
            new [] {
                "sequence	mods	mod_sites	charge",
                "LGGNEQVTR			2",
                "GAGSSEPVTGLDAK			2",
                "VEATFGVDESNAK			2",
                "YILAGVENSK			2",
                "TPVISGGPYEYR			2",
                "TPVITGAPYEYR			2",
                "DGLDAASYYAPVR			2",
                "ADVTPADFSEWSK			2",
                "GTFIIDPGGVIR			2",
                "GTFIIDPAAVIR			2",
                "LFLQFGAQGSPFLK			2",
                "ELVISK			2",
                "LIVESK			2",
                "KINGPELVISKINK			2",
                "LIVER			2"
            };
        private readonly IEnumerable<string> MIXED_PRECURSOR_TABLE_ANSWER =
            new[] {
                "sequence	mods	mod_sites	charge",                
                "LGGNEQVTR			2",
                "GAGSSEPVTGLDAK			2",
                "VEATFGVDESNAK			2",
                "YILAGVENSK			2",
                "TPVISGGPYEYR			2",
                "TPVITGAPYEYR			2",
                "DGLDAASYYAPVR			2",
                "ADVTPADFSEWSK			2",
                "GTFIIDPGGVIR			2",
                "GTFIIDPAAVIR			2",
                "LFLQFGAQGSPFLK			2",
                "MSGSHSNDEDDVVQVPETSSPTK	Acetyl@Protein_N-term	1	2",
                "MSGSHSNDEDDVVQVPETSSPTK	Acetyl@Protein_N-term	1	3",
                "MSGSHSNDEDDVVQVPETSSPTK	Acetyl@Protein_N-term	1	4",
                "YDVSFLKNRNFNVVVYDEGHMLK	Oxidation@M	21	2",
                "YDVSFLKNRNFNVVVYDEGHMLK	Oxidation@M	21	3",
                "YDVSFLKNRNFNVVVYDEGHMLK	Oxidation@M	21	4",
                "EDMSYMTDFELHKLCCNFPNTLSK	Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C	3;15;16	2",
                "EDMSYMTDFELHKLCCNFPNTLSK	Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C	3;15;16	3",
                "EDMSYMTDFELHKLCCNFPNTLSK	Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C	3;15;16	4",
                "EDMSYMTDFELHKLCCNFPNTLSK	Carbamidomethyl@C;Carbamidomethyl@C	15;16	2",
                "EDMSYMTDFELHKLCCNFPNTLSK	Carbamidomethyl@C;Carbamidomethyl@C	15;16	3",
                "EDMSYMTDFELHKLCCNFPNTLSK	Carbamidomethyl@C;Carbamidomethyl@C	15;16	4",
                "EDMSYMTDFELHKLCCNFPNTLSK	Oxidation@M;Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C	3;6;15;16	2",
                "EDMSYMTDFELHKLCCNFPNTLSK	Oxidation@M;Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C	3;6;15;16	3",
                "EDMSYMTDFELHKLCCNFPNTLSK	Oxidation@M;Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C	3;6;15;16	4",
                "VLIFSLFTQVLDILEMVLSTLDYK	Oxidation@M	16	2",
                "VLIFSLFTQVLDILEMVLSTLDYK	Oxidation@M	16	3",
                "VLIFSLFTQVLDILEMVLSTLDYK	Oxidation@M	16	4",
                "LCCNFPNTLSKYQLHNDEWMQSGK	Carbamidomethyl@C;Carbamidomethyl@C	2;3	2",
                "LCCNFPNTLSKYQLHNDEWMQSGK	Carbamidomethyl@C;Carbamidomethyl@C	2;3	3",
                "LCCNFPNTLSKYQLHNDEWMQSGK	Carbamidomethyl@C;Carbamidomethyl@C	2;3	4",
                "YDVIVTTYNLAAGNKYDVSFLKNR			2",
                "YDVIVTTYNLAAGNKYDVSFLKNR			3",
                "YDVIVTTYNLAAGNKYDVSFLKNR			4"
            };
    }
}
