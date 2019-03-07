/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class CommandLineImportAnnotationsTest : AbstractUnitTestEx
    {
        private const string REPLICATE_ANNOTATION = "MyReplicateAnnotation";
        [TestMethod]
        public void TestCommandLineImportAnnotations()
        {
            var testFilesDir = new TestFilesDir(TestContext, @"Test\CommandLineImportAnnotationsTest.zip");
            var inDocPath = testFilesDir.GetTestPath("original.sky");
            var outDocPath = testFilesDir.GetTestPath("AnnotatedDocument.sky");
            var annotationPath = testFilesDir.GetTestPath("annotations.csv");
            SrmDocument originalDocument;
            using (var stream = new FileStream(inDocPath, FileMode.Open))
            {
                originalDocument = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
            }
            Assert.IsTrue(originalDocument.Settings.HasResults);
            var replicateAnnotation =
                originalDocument.Settings.DataSettings.AnnotationDefs.First(
                    def => def.Name == REPLICATE_ANNOTATION);
            Assert.IsNotNull(replicateAnnotation);
            Assert.IsTrue(replicateAnnotation.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate));
            Assert.AreEqual(AnnotationDef.AnnotationType.text, replicateAnnotation.Type);
            var chromatograms = originalDocument.MeasuredResults.Chromatograms.ToArray();
            Assert.AreNotEqual(0, chromatograms.Length);
            for (int i = 0; i < chromatograms.Length; i++)
            {
                var chromSet = chromatograms[i];
                chromSet = chromSet.ChangeAnnotations(
                    chromSet.Annotations.ChangeAnnotation(replicateAnnotation, "Replicate" + i));
                chromatograms[i] = chromSet;
            }
            SrmDocument annotatedDocument = originalDocument.ChangeMeasuredResults(
                originalDocument.MeasuredResults.ChangeChromatograms(chromatograms));
            Assert.AreNotEqual(originalDocument, annotatedDocument);
            var documentAnnotations = new DocumentAnnotations(annotatedDocument);
            documentAnnotations.WriteAnnotationsToFile(CancellationToken.None, ExportAnnotationSettings.AllAnnotations(annotatedDocument), annotationPath);
            Assert.IsTrue(File.Exists(annotationPath));
            RunCommand("--in=" + inDocPath, "--out=" + outDocPath, "--import-annotations=" + annotationPath);
            Assert.IsTrue(File.Exists(outDocPath));
            SrmDocument outputDocument;
            using (var stream = new FileStream(outDocPath, FileMode.Open))
            {
                outputDocument = (SrmDocument)new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
            }
            Assert.AreEqual(annotatedDocument.Settings.MeasuredResults.Chromatograms, 
                outputDocument.Settings.MeasuredResults.Chromatograms);
        }
    }
}
