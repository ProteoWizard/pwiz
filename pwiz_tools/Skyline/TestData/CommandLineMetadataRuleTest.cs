/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    /// <summary>
    /// Tests that Result File Rules are correctly applied to replicates when importing from the commandline.
    /// </summary>
    [TestClass]
    public class CommandLineMetadataRuleTest : AbstractUnitTestEx
    {
        [TestMethod]
        public void TestCommandLineMetadataRule()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineMetadataRuleTest.zip");
            var docPath = TestFilesDir.GetTestPath("Rat_plasma.sky");
            RunCommand(
                "--in=" + docPath,
                "--import-all=" + Path.GetDirectoryName(docPath),
                "--save"
            );
            var doc = ResultsUtil.DeserializeDocument(docPath);

            // There should be one metadata rule which extracts the "SubjectId" annotation from the filename
            Assert.AreEqual(1, doc.Settings.DataSettings.MetadataRuleSets.Count);
            var metadataRuleSet = doc.Settings.DataSettings.MetadataRuleSets[0];
            Assert.AreEqual(1, metadataRuleSet.Rules.Count);
            var metadataRule = metadataRuleSet.Rules[0];
            Assert.AreEqual(PropertyPath.Root.Property(nameof(ResultFile.FileName)), metadataRule.Source);
            Assert.AreEqual(PropertyPath.Root.Property(nameof(ResultFile.Replicate)).Property(AnnotationDef.ANNOTATION_PREFIX + "SubjectId"),
                metadataRule.Target);

            // Verify that the SubjectId was correctly set on all four replicates that were imported
            Assert.IsTrue(doc.Settings.HasResults);
            Assert.AreEqual(4, doc.Settings.MeasuredResults.Chromatograms.Count);
            foreach (var chromatogramSet in doc.Settings.MeasuredResults.Chromatograms)
            {
                var expectedSubjectId = chromatogramSet.Name.Substring(0, 5);
                var actualSubjectId = chromatogramSet.Annotations.GetAnnotation("SubjectId");
                Assert.AreEqual(expectedSubjectId, actualSubjectId, "Annotation 'SubjectId' incorrect on replicate {0}", chromatogramSet.Name);
            }
        }
    }
}
