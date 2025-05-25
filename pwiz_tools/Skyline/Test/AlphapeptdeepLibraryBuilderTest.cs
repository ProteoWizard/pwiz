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
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Basic unit tests for <see cref="AlphapeptdeepLibraryBuilder"/> and its methods that
    /// perform the work before and after running the AlphaPeptDeep python script.
    /// The testing of installing Python and running the AlphaPeptDeep script can be found
    /// in TestPerf\AlphapeptdeepBuildLibraryTest.cs
    /// </summary>
    [TestClass]
    public class AlphapeptdeepLibraryBuilderTest : AbstractUnitTest
    {
        /// <summary>
        /// When true the test writes the answer files that get included as Assembly resources
        /// </summary>
        public bool IsRecordMode => false;

        public string LogOutput => TestContext.GetTestResultsPath("TestConsole.log");

        [TestMethod]
        public void TestAlphapeptdeepModificationInfo()
        {
            var groupedByAccession = AlphapeptdeepLibraryBuilder.MODIFICATION_NAMES
                .GroupBy(item => item.Accession);
            foreach (var group in groupedByAccession)
            {
                Assert.AreEqual(1, group.Count(), "Duplicate accession {0}", group.Key);
            }
        }
        
        private string NeverBuiltBlib => TestContext.GetTestResultsPath("TestAlphaPeptDeepLibrary.blib");

        [TestMethod]
        public void TestAlphapeptdeepLibraryBuilder()
        {
            TestContext.EnsureTestResultsDir();

            var peptides = new[]
            {
                new Peptide("ELVISK"),
                new Peptide("LIVESK"),
                new Peptide("KINGPELVISKINK"),
                new Peptide("LIVER")
            };
         
            var document = CreateTestSimpleDocument(peptides);

            TestGetPrecursorTable(document, SIMPLE_PRECURSOR_TABLE_ANSWER);
            TestGetWarningMods(document, 0); // No warnings should be generated
    
            document = CreateTestImportedDoc();

            TestGetPrecursorTable(document, MIXED_PRECURSOR_TABLE_ANSWER);
            TestGetWarningMods(document, 1);
            TestGetWarningMods(document, 1);

            TestValidateModifications_Supported();
            TestValidateModifications_Unsupported();

            TestTransformPeptDeepOutput();
            TestImportSpectralLibrary();
            
            Assert.IsFalse(File.Exists(NeverBuiltBlib));
        }

        /// <summary>
        /// Test of <see cref="AlphapeptdeepLibraryBuilder.GetPrecursorTable"/>
        /// </summary>
        /// <param name="document">Input <see cref="SrmDocument"/> from which to build the table.</param>
        /// <param name="expectedLines">Expected lines of output.</param>
        public void TestGetPrecursorTable(SrmDocument document, IEnumerable<string> expectedLines)
        {
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", NeverBuiltBlib, document, IrtStandard.BIOGNOSYS_11);
            var precursorTable = builder.GetPrecursorTable(false);

            var generatedResult = precursorTable as string[] ?? precursorTable.ToArray();
           
            if (IsRecordMode)
            {
                File.WriteAllLines(LogOutput, generatedResult);
            }

            var generatedOutput = TextUtil.LineSeparate(generatedResult);
            var expectedOutput = TextUtil.LineSeparate(expectedLines);

            AssertEx.NoDiff(expectedOutput, generatedOutput);
        }

        /// <summary>
        /// Test of <see cref="AlphapeptdeepLibraryBuilder.GetWarningMods"/>
        /// </summary>
        /// <param name="document">Input <see cref="SrmDocument"/> that may generate warnings about modifications.</param>
        /// <param name="expectedWarningCount">Expected count of warnings generated.</param>
        public void TestGetWarningMods(SrmDocument document, int expectedWarningCount)
        {
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", NeverBuiltBlib, document, IrtStandard.BIOGNOSYS_11);
            var warningList = builder.GetWarningMods();

            Assert.AreEqual(expectedWarningCount, warningList.Count);
        }

        /// <summary>
        /// Tests AlphaPeptDeep supported modifications only, of several types.
        /// </summary>
        public void TestValidateModifications_Supported()
        {
            var document = CreateTestEmptyDocument();
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", NeverBuiltBlib, document, IrtStandard.BIOGNOSYS_11);

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
            for (int i = 0; i < peptides.Length; i++)
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
        /// Tests an AlphaPeptDeep unsupported modification.
        /// </summary>
        public void TestValidateModifications_Unsupported()
        {
            var document = CreateTestEmptyDocument();
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", NeverBuiltBlib, document, IrtStandard.BIOGNOSYS_11);

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
        /// Test of <see cref="AlphapeptdeepLibraryBuilder.TransformPeptdeepOutput"/> the function that
        /// transforms between native AlphaPeptDeep output and BlibBuild input formats.
        /// </summary>
        public void TestTransformPeptDeepOutput()
        {
            var document = CreateTestEmptyDocument();
            const string blibName = "predict.speclib.blib";
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", blibName, document, IrtStandard.BIOGNOSYS_11);

            // Mimic AlphaPeptDeep succeeding and writing a .speclib.tsv
            ResourceToTestFile("predict.speclib.tsv", builder.OutputSpectraLibFilepath);
            const string tsvTransformedName = "predict_transformed.speclib.tsv";

            IProgressStatus status = new ProgressStatus();
            builder.TransformPeptdeepOutput(new SilentProgressMonitor(), ref status);

            string output = builder.TransformedOutputSpectraLibFilepath;
            var answer = ResourceToTestFile(tsvTransformedName, output + ".answer");

            using (var answerReader = new StreamReader(answer))
            using (var productReader = new StreamReader(output))
            {
                AssertEx.FieldsEqual(answerReader, productReader, 13, null, true);
            }
        }

        /// <summary>
        /// Test of <see cref="AlphapeptdeepLibraryBuilder.ImportSpectralLibrary"/> to see if it generates
        /// a .blib file with the expected number of spectrum entries.
        /// </summary>
        public void TestImportSpectralLibrary()
        {
            var document = CreateTestEmptyDocument();
            const string blibName = "predict.speclib.blib";
            var output = TestContext.GetTestResultsPath(blibName);
            var builder = new AlphapeptdeepLibraryBuilder("TestLib", output, document, null);

            // Mimic Transformation succeeding prior to running the library build
            ResourceToTestFile("predict_transformed.speclib.tsv", builder.TransformedOutputSpectraLibFilepath);

            IProgressStatus status = new ProgressStatus();
            builder.ImportSpectralLibrary(new SilentProgressMonitor(), ref status);

            var answer = ResourceToTestFile("predict_transformed.speclib.blib", output + ".answer");
            var ansLibrary = LoadLibrary(answer);
            var outLibrary = LoadLibrary(output);
            Assert.AreEqual(ansLibrary.SpectrumCount, outLibrary.SpectrumCount);
        }

        /// <summary>
        /// Returns a loaded <see cref="BiblioSpecLiteLibrary"/> but with a closed stream.
        /// </summary>
        private static Library LoadLibrary(string path)
        {
            var loader = new LibraryLoadTest.TestLibraryLoader { StreamManager = FileStreamManager.Default };
            var librarySpec = new BiblioSpecLiteSpec("Library", path);
            var library = librarySpec.LoadLibrary(loader);
            Assert.IsTrue(library.IsLoaded);
            library.ReadStream.CloseStream();   // Release the library stream
            return library;
        }

        /// <summary>
        /// Copies an Assembly resource to the TestResultsPath folder for use in file operations.
        /// </summary>
        /// <param name="resourceName">The name of the resource to read the file from</param>
        /// <param name="testFile">The path to copy the file to</param>
        /// <returns>The full path to the output file</returns>
        private string ResourceToTestFile(string resourceName, string testFile)
        {
            // First make sure the target folder exists
            string parentDir = Path.GetDirectoryName(testFile);
            if (parentDir != null)
                Directory.CreateDirectory(parentDir);

            using (var stream = GetType().Assembly.GetManifestResourceStream(GetType().Namespace + "." + resourceName))
            using (var fileStream = new FileStream(testFile, FileMode.Create, FileAccess.Write))
            {
                // Copy the resource stream to the file
                Assert.IsNotNull(stream, $@"Resource '{resourceName}' not found.");
                stream.CopyTo(fileStream);
            }

            return testFile;
        }

        /// <summary>
        /// Creates and returns an <see cref="SrmDocument"/> from the embedded resource file "Test-imported_24-11-short.sky"
        /// </summary>
        private SrmDocument CreateTestImportedDoc()
        {
            var document = ResultsUtil.DeserializeDocument("Test-imported_24-11-short.sky", GetType());
            AssertEx.IsDocumentState(document, 0, 1, 9, 27, 327);
            return document;
        }

        /// <summary>
        /// Creates a list of <see cref="PeptideDocNode"/> from a list of <see cref="Peptide"/>,
        /// with an optional set of modifications applied.
        /// </summary>
        /// <param name="peptideList">List of <see cref="Peptide"/>.</param>
        /// <param name="explicitMods">List of <see cref="ExplicitMod"/>, if not null should have same length as peptideList.</param>
        /// <returns>List of <see cref="PeptideDocNode"/>.</returns>
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


            // Add precursors and transitions
            foreach (var peptideNode in peptideNodes)
            {
                peptideNode.ChangeSettings(srmSettings, SrmSettingsDiff.ALL);
            }
            return peptideNodes;

        }

        /// <summary>
        /// Creates and returns an empty test <see cref="SrmDocument"/> with default settings.
        /// </summary>
        private SrmDocument CreateTestEmptyDocument()
        {
            return CreateTestDocumentInternal(Array.Empty<PeptideDocNode>());
        }

        /// <summary>
        /// Creates and returns a simple test <see cref="SrmDocument"/>.
        /// </summary>
        /// <param name="peptideList">List of <see cref="Peptide"/> to place into the document.</param>
        private SrmDocument CreateTestSimpleDocument(IEnumerable<Peptide> peptideList)
        {
            return CreateTestDocumentInternal(CreatePeptideDocNodes(peptideList).ToArray());
        }

        private static SrmDocument CreateTestDocumentInternal(PeptideDocNode[] nodePepArray)
        {
            var doc = new SrmDocument(SrmSettingsList.GetDefault());

            doc = (SrmDocument)doc.ChangeChildren(new[]
            {
                new PeptideGroupDocNode(new PeptideGroup(), "Peptide List", null, nodePepArray)
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
