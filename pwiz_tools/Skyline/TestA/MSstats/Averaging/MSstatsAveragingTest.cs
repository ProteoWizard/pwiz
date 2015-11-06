/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis.Matrices;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA.MSstats.Averaging
{
    [TestClass]
    public class MSstatsAveragingTest : AbstractUnitTest
    {
        [TestMethod]
        public void CompareAveragingWithMSstats()
        {
            var srmDocument = LoadRatPlasmaDocument();
            var documentContainer = new MemoryDocumentContainer();
            documentContainer.SetDocument(documentContainer.Document, srmDocument);
            GroupComparisonModel model = new GroupComparisonModel(documentContainer, null);
            model.GroupComparisonDef = GroupComparisonDef.EMPTY.ChangeControlAnnotation("Condition")
                .ChangeControlValue("Healthy")
                .ChangeIdentityAnnotation("BioReplicate")
                .ChangeSummarizationMethod(SummarizationMethod.AVERAGING)
                .ChangePerProtein(false);
            var expectedValues = MsStatsTestUtil.ReadExpectedResults(typeof (MSstatsAveragingTest),
                "RatPlasmaTestingResult.csv");
            var groupComparer = new GroupComparer(model.GroupComparisonDef, srmDocument, new QrFactorizationCache());
            foreach (var protein in srmDocument.PeptideGroups)
            {
                foreach (var peptide in protein.Peptides)
                {
                    var result = groupComparer.CalculateFoldChange(new GroupComparisonSelector(protein, peptide, IsotopeLabelType.light, null, new GroupIdentifier("Diseased")), null);
                    var expectedResult = expectedValues[peptide.Peptide.Sequence];
                    Assert.AreEqual(expectedResult.EstimatedValue, result.LinearFitResult.EstimatedValue,
                        (expectedResult.StandardError + result.LinearFitResult.StandardError) * 2, peptide.Peptide.Sequence);
                }
            }

        }

        private SrmDocument LoadRatPlasmaDocument()
        {
            return (SrmDocument)new XmlSerializer(typeof(SrmDocument))
                .Deserialize(GetManifestResourceStream("Rat_plasma.sky"));
        }

        private Stream GetManifestResourceStream(string name)
        {
            return typeof (MSstatsAveragingTest).Assembly.GetManifestResourceStream(
                typeof (MSstatsAveragingTest), name);
        }
    }
}
