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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis.Matrices;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.MSstats.Averaging
{
    [TestClass]
    public class MSstatsAveragingTest : AbstractUnitTest
    {
        [TestMethod]
        public void CompareAveragingWithMSstats()
        {
            var srmDocument = LoadRatPlasmaDocument();
            var documentContainer = new MemoryDocumentContainer();
            documentContainer.SetDocument(srmDocument, documentContainer.Document);
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

        /// <summary>
        /// Tests that the Abundance value on the ProteinResult can be used to get the same fold change values
        /// </summary>
        [TestMethod]
        public void TestProteinAbundance()
        {
            var srmDocument = LoadRatPlasmaDocument();
            var documentContainer = new MemoryDocumentContainer();
            documentContainer.SetDocument(srmDocument, documentContainer.Document);
            var skylineDataSchema = new SkylineDataSchema(documentContainer, DataSchemaLocalizer.INVARIANT);
            GroupComparisonModel model = new GroupComparisonModel(documentContainer, null);
            model.GroupComparisonDef = GroupComparisonDef.EMPTY.ChangeControlAnnotation("Condition")
                .ChangeControlValue("Healthy")
                .ChangeSummarizationMethod(SummarizationMethod.AVERAGING)
                .ChangePerProtein(true);
            var groupComparer = new GroupComparer(model.GroupComparisonDef, srmDocument, new QrFactorizationCache());
            foreach (var moleculeGroup in srmDocument.MoleculeGroups)
            {
                if (moleculeGroup.Molecules.Any(mol => null != mol.GlobalStandardType))
                {
                    continue;
                }
                var foldChangeResult = groupComparer.CalculateFoldChange(new GroupComparisonSelector(moleculeGroup, null, IsotopeLabelType.light, null, new GroupIdentifier("Diseased")), null);
                var xValues = new List<double>();
                var yValues = new List<double>();
                var protein = new Protein(skylineDataSchema, new IdentityPath(moleculeGroup.PeptideGroup));
                foreach (var proteinResult in protein.Results.Values)
                {
                    var abundance = proteinResult.Abundance?.Strict;
                    if (!abundance.HasValue)
                    {
                        continue;
                    }

                    var condition = proteinResult.Replicate.ChromatogramSet.Annotations.GetAnnotation("Condition");
                    if (condition == "Healthy")
                    {
                        xValues.Add(0);
                    }
                    else if (condition == "Diseased")
                    {
                        xValues.Add(1);
                    }
                    yValues.Add(Math.Log(abundance.Value));
                }
                Assert.AreEqual(xValues.Count, foldChangeResult.ReplicateCount);

                if (!xValues.Any())
                {
                    continue;
                }
                var yStatistics = new Statistics(yValues);
                var xStatistics = new Statistics(xValues);
                var slope = yStatistics.Slope(xStatistics);
                var actualFoldChange = Math.Exp(slope);
                var expectedFoldChange = Math.Pow(2.0, foldChangeResult.LinearFitResult.EstimatedValue);

                AssertEx.AreEqual(expectedFoldChange, actualFoldChange, .01);
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
