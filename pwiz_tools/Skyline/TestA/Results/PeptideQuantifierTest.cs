/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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

using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA.Results
{
    [TestClass]
    public class PeptideQuantifierTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestRatioToStandard()
        {
            var doc = LoadTestDocument();
            foreach (var peptideGroup in doc.MoleculeGroups)
            {
                foreach (var peptide in peptideGroup.Peptides)
                {
                    var peptideQuantifier = new PeptideQuantifier(peptideGroup, peptide, 
                        QuantificationSettings.DEFAULT.ChangeNormalizationMethod(NormalizationMethod.GetNormalizationMethod(IsotopeLabelType.heavy)));
                    for (int replicateIndex = 0; replicateIndex < doc.Settings.MeasuredResults.Chromatograms.Count; replicateIndex++)
                    {
                        var expected = peptide.Results[replicateIndex].First().LabelRatios.First().Ratio.Ratio;
                        var actual = PeptideQuantifier.SumQuantities(
                            peptideQuantifier.GetTransitionIntensities(doc.Settings, replicateIndex).Values, 
                            peptideQuantifier.NormalizationMethod).Value;
                        Assert.AreEqual(expected, actual, .0001, "Error on replicate {0}", replicateIndex);
                    }
                }
            }
        }

        private SrmDocument LoadTestDocument()
        {
            using (var stream =
                    typeof(PeptideQuantifierTest).Assembly.GetManifestResourceStream(
                        typeof(PeptideQuantifierTest),
                        "PeptideQuantifierTest.sky"))
            {
                Assert.IsNotNull(stream);
                var serializer = new XmlSerializer(typeof(SrmDocument));
                return (SrmDocument)serializer.Deserialize(stream);
            }
        }
    }
}
