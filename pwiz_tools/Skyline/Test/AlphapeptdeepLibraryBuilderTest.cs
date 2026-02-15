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
using pwiz.Skyline.Model.Koina.Models;
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

        /// <summary>
        /// This library never gets built, but its work directory does get created and used.
        /// </summary>
        private string NeverBuiltBlib => TestContext.GetTestResultsPath("TestAlphaPeptDeepLibrary.blib");

        private const string TEST_LIB_NAME = "Test|Lib";    // Use a name that fail if included in a path

        [TestMethod]
        public void AlphapeptdeepLibraryBuilderUnitTest()
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
            TestGetWarningMods(document, Array.Empty<string>()); // No warnings should be generated
    
            document = CreateTestImportedNonUnimodModsDoc();
            TestGetPrecursorTable(document, MIXED_PRECURSOR_TABLE_ANSWER);
            TestGetWarningMods(document, new [] { "Acetyl (N-term)", "Acetyl-Oxidation (N-term-M)" } ); // 2 warnings are generated

            document = CreateTestImportedOnlyUnimodModsDoc();
            TestGetPrecursorTable(document, MIXED_PRECURSOR_TABLE_ANSWER);
            TestGetWarningMods(document, new[] { "Acetyl (N-term)" }); // 1 warning is generated

            TestValidateModifications_Supported();
            TestValidateModifications_Unsupported();
            TestValidateModifications_Limited_Support();


            TestTransformPeptDeepOutput();
            TestImportSpectralLibrary();
        }

        /// <summary>
        /// Test of <see cref="AlphapeptdeepLibraryBuilder.GetPrecursorTable"/>
        /// </summary>
        /// <param name="document">Input <see cref="SrmDocument"/> from which to build the table.</param>
        /// <param name="expectedLines">Expected lines of output.</param>
        public void TestGetPrecursorTable(SrmDocument document, IEnumerable<string> expectedLines)
        {
            var builder = new AlphapeptdeepLibraryBuilder(TEST_LIB_NAME, NeverBuiltBlib, document, IrtStandard.BIOGNOSYS_11);
            var precursorTable = builder.GetPrecursorTable(false);

            var generatedResult = precursorTable as string[] ?? precursorTable.ToArray();
           
            if (IsRecordMode)
            {
                File.WriteAllLines(LogOutput, generatedResult);
            }

            var generatedOutput = TextUtil.LineSeparate(generatedResult);
            var expectedOutput = TextUtil.LineSeparate(expectedLines);

            AssertEx.NoDiff(expectedOutput, generatedOutput);
            CheckBuilderFiles(builder, false);
        }

        private void CheckBuilderFiles(AlphapeptdeepLibraryBuilder builder, bool libraryBuilt)
        {
            Assert.IsTrue(Directory.Exists(builder.WorkDir));
            Directory.Delete(builder.WorkDir, true);
            Assert.AreEqual(libraryBuilt, File.Exists(builder.LibrarySpec.FilePath));
        }

        /// <summary>
        /// Test of <see cref="AlphapeptdeepLibraryBuilder.GetWarningMods"/>
        /// </summary>
        /// <param name="document">Input <see cref="SrmDocument"/> that may generate warnings about modifications.</param>
        /// <param name="expectedWarningMods">List of modifications for which warnings should be generated.</param>
        public void TestGetWarningMods(SrmDocument document, IList<string> expectedWarningMods)
        {
            var builder = new AlphapeptdeepLibraryBuilder(TEST_LIB_NAME, NeverBuiltBlib, document, IrtStandard.BIOGNOSYS_11);
            var warningModSupports = builder.GetWarningMods();

            CollectionAssert.AreEquivalent(expectedWarningMods.OrderBy(n => n).ToList(),
                warningModSupports.Where(kvp => !IsSupported(kvp.Value, PredictionSupport.fragmentation)).Select(kvp => kvp.Key).ToList());
            CheckBuilderFiles(builder, false);
        }

        /// <summary>
        /// Tests AlphaPeptDeep supported modifications only, of several types.
        /// </summary>
        public void TestValidateModifications_Supported()
        {
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
            var document = CreateTestDocumentInternal(peptides);
            var builder = new AlphapeptdeepLibraryBuilder(TEST_LIB_NAME, NeverBuiltBlib, document, IrtStandard.BIOGNOSYS_11);
            for (int i = 0; i < peptides.Length; i++)
            {
                var modifiedSeq = ModifiedSequence.GetModifiedSequence(document.Settings, peptides[i], IsotopeLabelType.light);
                string mods;
                string modSites;
                    
                var predictionSupport = builder.ValidateSequenceModifications(modifiedSeq, out mods, out modSites);
                Assert.AreEqual(PredictionSupport.fragmentation, predictionSupport & PredictionSupport.fragmentation);
                Assert.AreEqual(answer_mods[i], mods);
                Assert.AreEqual(answer_modSites[i], modSites);
            }
            CheckBuilderFiles(builder, false);
        }


        /// <summary>
        /// Tests an AlphaPeptDeep partially supported modification.
        /// </summary>
        public void TestValidateModifications_Limited_Support()
        {
            var peptideList = new[]
            {
                new Peptide("MSGSHSNDEDDVVQVPETSSPTK"),
                new Peptide("MSGSHSNDEDDVVQVPETSSPTK"),
                new Peptide("MMSGSHSNDEDDVVQVPETSSPTK"),
                new Peptide("MSGSHSNDVPETSSPTK"),
                new Peptide("MSGSHSNDVPETSSPTK")
            };

            var answer_modstrings = new[]
            {
                "Oxidation@M",
                "Acetyl@Protein_N-term",
                "Acetyl@Protein_N-term;Oxidation@M",
                "Acetyl@K",
                "GG@K"
            };

            var answer_ms2_supported = new[]
            {
                true,
                false,
                false,
                false,
                true
            };

            var answer_rt_supported = new[]
            {
                true,
                false,
                false,
                false,
                false
            };

            var answer_ccs_supported = new[]
            {
                true,
                false,
                false,
                false,
                false
            };


            var answer_modsites = new[]
            {
                "1",
                "1", 
                "1;2",
                "17",
                "17"
            };

            var aceMod = new StaticMod("Acetyl (N-term)", "M", ModTerminus.N, true, "H2C2O", LabelAtoms.None, RelativeRT.Unknown, null,
                null, null, 1, "1Ac");
            var aceModK = new StaticMod("Acetyl (K)", "K", ModTerminus.N, true, "H2C2O", LabelAtoms.None, RelativeRT.Unknown, null,
                null, null, 1, "1Ac");
            var oxMod = new StaticMod("Oxidation (M)", "M", null, true, "O", LabelAtoms.None, RelativeRT.Unknown, null,
                null, null, 35, "Oxi");
            var aceOxMetMod = new StaticMod("Acetyl-Oxidation (N-term-M)", "M", ModTerminus.N, true, "H2C2O2", LabelAtoms.None, RelativeRT.Unknown, null,
                null, null, null, "Acetyl-Ox");
            var gGLysMod = new StaticMod("GG (K)", "K", null, true, "H6C4ON22", LabelAtoms.None, RelativeRT.Unknown, null,
                null, null, 121, "GG");

            var answer_warn_counts = new[]
            {
                0,
                1,
                1,
                2,
                1
            };

            var explicitMods = new[]
            {
                // Supported Mod
                new ExplicitMods(peptideList[0], new[] { new ExplicitMod(0, oxMod) }, null, true),
                // Unsupported Mod
                new ExplicitMods(peptideList[0], new[] { new ExplicitMod(0, aceMod) }, null, true),
                // Unsupported Mod and Supported Mod
                new ExplicitMods(peptideList[1], new[] { new ExplicitMod(0, aceMod), new ExplicitMod(1, oxMod) }, null, true),
                // Unsupported Mod and Unsupported Mod
                new ExplicitMods(peptideList[2], new[] { new ExplicitMod(0, aceOxMetMod), new ExplicitMod(16, aceModK) }, null, true),
                // Partially Supported Mod
                new ExplicitMods(peptideList[2], new[] { new ExplicitMod(16, gGLysMod) }, null, true),
            };

            var answer_warningList = new[]
            {
                aceMod.Name,
                aceModK.Name,
                aceOxMetMod.Name,
                gGLysMod.Name
            };

  

            var peptides = CreatePeptideDocNodes(peptideList, explicitMods).ToArray();
            var document = CreateTestDocumentInternal(peptides);
            var builder = new AlphapeptdeepLibraryBuilder(TEST_LIB_NAME, NeverBuiltBlib, document, IrtStandard.BIOGNOSYS_11);


            var answer_messages = new[]
            {
                new string[] {} ,
                new[]
                {
                    string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[1], IsotopeLabelType.light),
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[1], IsotopeLabelType.light)
                            .ExplicitMods[0].Name,
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[1], IsotopeLabelType.light)
                            .ExplicitMods[0].UnimodIdWithName, AlphapeptdeepLibraryBuilder.ALPHAPEPTDEEP)
                },
                new[]
                {
                    string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[2], IsotopeLabelType.light),
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[2], IsotopeLabelType.light)
                            .ExplicitMods[0].Name,
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[2], IsotopeLabelType.light)
                            .ExplicitMods[0].UnimodIdWithName, AlphapeptdeepLibraryBuilder.ALPHAPEPTDEEP)
                },
                new[] 
                {
                    string.Format(ModelsResources.BuildPrecursorTable_UnsupportedModification,
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[3], IsotopeLabelType.light),
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[3], IsotopeLabelType.light).ExplicitMods[0].Name, AlphapeptdeepLibraryBuilder.ALPHAPEPTDEEP),
                    
                    string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[3], IsotopeLabelType.light),
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[3], IsotopeLabelType.light).ExplicitMods[1].Name,
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[3], IsotopeLabelType.light).ExplicitMods[1].UnimodIdWithName, AlphapeptdeepLibraryBuilder.ALPHAPEPTDEEP)

                },
                new[]
                {
                    string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[4], IsotopeLabelType.light),
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[4], IsotopeLabelType.light).ExplicitMods[0].Name,
                        ModifiedSequence.GetModifiedSequence(document.Settings, peptides[4], IsotopeLabelType.light).ExplicitMods[0].UnimodIdWithName, AlphapeptdeepLibraryBuilder.ALPHAPEPTDEEP)

                }
            };

            using (var capture = new UserMessageCapture())
            {
                for (int i = 0; i < peptides.Length; i++)
                {
                    var modifiedSeq = ModifiedSequence.GetModifiedSequence(document.Settings, peptides[i], IsotopeLabelType.light);
                    string mods;
                    string modSites;
                    var predictionSupport =  builder.ValidateSequenceModifications(modifiedSeq, out mods, out modSites);

                    Assert.AreEqual(answer_ms2_supported[i], IsSupported(predictionSupport, PredictionSupport.fragmentation));
                    Assert.AreEqual(answer_rt_supported[i], IsSupported(predictionSupport, PredictionSupport.retention_time));
                    Assert.AreEqual(answer_ccs_supported[i], IsSupported(predictionSupport, PredictionSupport.ccs));
                    Assert.AreEqual(answer_modstrings[i], mods);
                    Assert.AreEqual(answer_modsites[i], modSites);
                    Assert.AreEqual(answer_warn_counts[i], capture.CapturedMessages.Count);

                    CollectionAssert.AreEquivalent(answer_messages[i].ToList(), capture.CapturedMessages.ToList());
              
                    capture.CapturedMessages.Clear();

                    var warningModSupports = builder.GetWarningMods();
                    Assert.AreEqual(3, warningModSupports.Where(kvp =>
                        !IsSupported(kvp.Value, PredictionSupport.fragmentation)).Select(kvp => kvp.Key).ToList().Count);
                    Assert.AreEqual(4, warningModSupports.Where(kvp =>
                        !IsSupported(kvp.Value, PredictionSupport.retention_time)).Select(kvp => kvp.Key).ToList().Count);
                    Assert.AreEqual(4, warningModSupports.Where(kvp =>
                        !IsSupported(kvp.Value, PredictionSupport.ccs)).Select(kvp => kvp.Key).ToList().Count);
                    CollectionAssert.AreEquivalent(answer_warningList.ToList(), warningModSupports.Where(kvp =>
                        !IsSupported(kvp.Value, PredictionSupport.retention_time)).Select(kvp => kvp.Key).ToList());
                }
            }
            CheckBuilderFiles(builder, false);
        }

        /// <summary>
        /// Tests an AlphaPeptDeep unsupported modification.
        /// </summary>
        public void TestValidateModifications_Unsupported()
        {
            var answer_warn_counts = new[]
            {
                1,
                2,
                1
            };

            var peptideList = new[]
            {
                new Peptide("MSGSHSNDEDDVVQVPETSSPTK"),
                new Peptide("MMSGSHSNDEDDVVQVPETSSPTK"),
                new Peptide("MSGSHSNDVPETSSPTK"),
            };

            const string unsupportedName = "Acetyl-Oxidation (N-term-M)";
            var answer_mods = new[]
            {
                unsupportedName,
                unsupportedName,
                unsupportedName
            };

            var aceOxMetMod = new StaticMod("Acetyl-Oxidation (N-term-M)", "M",ModTerminus.N, true, "H2C2O2", LabelAtoms.None, RelativeRT.Unknown, null,
                null, null, null, "Acetyl-Ox");
            var fakeUnimodAceOxMetMod = new StaticMod("Acetyl-Oxidation (N-term-M)", "M", ModTerminus.N, true, "H2C2O2", LabelAtoms.None, RelativeRT.Unknown, null,
                null, null, 35, "Acetyl-Ox");

            var explicitMods = new[]
            {
                // Non-unimod
                new ExplicitMods(peptideList[0], new[] { new ExplicitMod(0, aceOxMetMod) }, null, true),
                // Non-unimod in multiple locations
                new ExplicitMods(peptideList[1], new[] { new ExplicitMod(0, aceOxMetMod), new ExplicitMod(1, aceOxMetMod) }, null, true),
                // Unrecognized unimod
                new ExplicitMods(peptideList[2], new[] { new ExplicitMod(0, fakeUnimodAceOxMetMod) }, null, true),
            };

            var peptides = CreatePeptideDocNodes(peptideList, explicitMods).ToArray();
            var document = CreateTestDocumentInternal(peptides);
            var builder = new AlphapeptdeepLibraryBuilder(TEST_LIB_NAME, NeverBuiltBlib, document, IrtStandard.BIOGNOSYS_11);

            using (var capture = new UserMessageCapture())
            {
                for (int i = 0; i < peptides.Length; i++)
                {
                    var modifiedSeq = ModifiedSequence.GetModifiedSequence(document.Settings, peptides[i], IsotopeLabelType.light);
                    string mods;
                    string modSites;

                    var predictionSupport =
                        builder.ValidateSequenceModifications(modifiedSeq, out mods, out modSites);
                    Assert.IsFalse(IsSupported(predictionSupport, PredictionSupport.fragmentation));
                    
                    Assert.AreEqual(string.Empty, mods);
                    Assert.AreEqual(string.Empty, modSites);
                    Assert.AreEqual(answer_warn_counts[i], capture.CapturedMessages.Count);
                    
                    string expectedMsg;
                    var unsupportedMod = modifiedSeq.ExplicitMods[0];
                    if (unsupportedMod.UnimodId.HasValue)
                    {
                        expectedMsg = string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                            modifiedSeq, unsupportedMod.Name, unsupportedMod.UnimodIdWithName, AlphapeptdeepLibraryBuilder.ALPHAPEPTDEEP);
                    }
                    else
                    {
                        expectedMsg = string.Format(ModelsResources.BuildPrecursorTable_UnsupportedModification,
                            modifiedSeq, unsupportedMod.Name, AlphapeptdeepLibraryBuilder.ALPHAPEPTDEEP);
                    }
                    Assert.AreEqual(expectedMsg, capture.CapturedMessages[0]);
                    capture.CapturedMessages.Clear();

                    var warningModSupports = builder.GetWarningMods();
                    Assert.AreEqual(1, warningModSupports.Where(kvp =>
                        !IsSupported(kvp.Value, PredictionSupport.fragmentation)).Select(kvp => kvp.Key).ToList().Count);
                    Assert.AreEqual(answer_mods[i], warningModSupports.Where(kvp =>
                        !IsSupported(kvp.Value, PredictionSupport.fragmentation)).Select(kvp => kvp.Key).ToList()[0]);
                }
            }
            CheckBuilderFiles(builder, false);
        }

        private bool IsSupported(PredictionSupport ps, PredictionSupport psCheck)
        {
            return (ps & psCheck) == psCheck;
        }

        /// <summary>
        /// Test of <see cref="AlphapeptdeepLibraryBuilder.TransformPeptdeepOutput"/> the function that
        /// transforms between native AlphaPeptDeep output and BlibBuild input formats.
        /// </summary>
        public void TestTransformPeptDeepOutput()
        {
            var document = CreateTestEmptyDocument();
            const string blibName = "predict.speclib.blib";
            var blibPath = TestContext.GetTestResultsPath(blibName);
            var builder = new AlphapeptdeepLibraryBuilder(TEST_LIB_NAME, blibPath, document, IrtStandard.BIOGNOSYS_11);

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
            CheckBuilderFiles(builder, false);
        }

        /// <summary>
        /// Test of <see cref="AlphapeptdeepLibraryBuilder.ImportSpectralLibrary"/> to see if it generates
        /// a .blib file with the expected number of spectrum entries.
        /// </summary>
        public void TestImportSpectralLibrary()
        {
            var document = CreateTestEmptyDocument();
            const string blibName = "predict.speclib.blib";
            // Test that a programming error that went unnoticed now throws an argument exception
            AssertEx.ThrowsException<ArgumentException>(() => new AlphapeptdeepLibraryBuilder(TEST_LIB_NAME, blibName, document, null));

            var output = TestContext.GetTestResultsPath(blibName);
            var builder = new AlphapeptdeepLibraryBuilder(TEST_LIB_NAME, output, document, null);

            // Mimic Transformation succeeding prior to running the library build
            ResourceToTestFile("predict_transformed.speclib.tsv", builder.TransformedOutputSpectraLibFilepath);

            IProgressStatus status = new ProgressStatus();
            builder.ImportSpectralLibrary(new SilentProgressMonitor(), ref status);

            var answer = ResourceToTestFile("predict_transformed.speclib.blib", output + ".answer");
            var ansLibrary = LoadLibrary(answer);
            var outLibrary = LoadLibrary(output);
            Assert.AreEqual(ansLibrary.SpectrumCount, outLibrary.SpectrumCount);
            CheckBuilderFiles(builder, true);
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
        /// Creates and returns an <see cref="SrmDocument"/> from the embedded resource file "Test-imported_24-11-short_onlyUnimodMods.sky"
        /// </summary>
        private SrmDocument CreateTestImportedOnlyUnimodModsDoc()
        {
            var document = ResultsUtil.DeserializeDocument("Test-imported_24-11-short_onlyUnimodMods.sky", GetType());
            AssertEx.IsDocumentState(document, 0, 1, 8, 24, 291);
            return document;
        }

        /// <summary>
        /// Creates and returns an <see cref="SrmDocument"/> from the embedded resource file "Test-imported_24-11-short_nonUnimodMods.sky"
        /// </summary>
        private SrmDocument CreateTestImportedNonUnimodModsDoc()
        {
            var document = ResultsUtil.DeserializeDocument("Test-imported_24-11-short_nonUnimodMods.sky", GetType());
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
