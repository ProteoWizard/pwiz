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

namespace pwiz.SkylineTest.Quantification
{
    [TestClass]
    public class CalibrationCurveFitterTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestCalibrationCurveFitter()
        {
            var srmDocument = LoadTestDocument();
            var calibrationCurveFitter = GetCalibrationCurveFitter(srmDocument);
            var calibrationCurve = calibrationCurveFitter.GetCalibrationCurve(null);
            Assert.IsNull(calibrationCurve.QuadraticCoefficient);
            Assert.IsNotNull(calibrationCurve.Slope);
            Assert.IsNull(calibrationCurve.ErrorMessage);
        }

        [TestMethod]
        public void TestCalWithNoStandards()
        {
            var srmDocument = LoadTestDocument();
            var measuredResults = srmDocument.Settings.MeasuredResults;
            measuredResults =
                measuredResults.ChangeChromatograms(
                    measuredResults.Chromatograms.Select(chrom =>
                        Equals(chrom.SampleType, SampleType.STANDARD)
                            ? chrom.ChangeSampleType(SampleType.UNKNOWN)
                            : chrom).ToArray());
            srmDocument = srmDocument.ChangeMeasuredResults(measuredResults);
            var calibrationCurveFitter = GetCalibrationCurveFitter(srmDocument);
            var calibrationCurve = calibrationCurveFitter.GetCalibrationCurve(null);
            Assert.AreEqual(1.0, calibrationCurve.Slope);
            Assert.AreEqual(0, calibrationCurve.PointCount);
        }

        private CalibrationCurveFitter GetCalibrationCurveFitter(SrmDocument document)
        {
            var peptideQuantifier = new PeptideQuantifier(
                document.MoleculeGroups.First(),
                document.Peptides.First(), document.Settings.PeptideSettings.Quantification.NormalizationMethod);
            return new CalibrationCurveFitter(peptideQuantifier, document.Settings);
        }

        private SrmDocument LoadTestDocument()
        {
            using (var stream =
                    typeof (CalibrationCurveFitterTest).Assembly.GetManifestResourceStream(
                        typeof (CalibrationCurveFitterTest),
                        "CalibrationCurveFitterTest.sky"))
            {
                Assert.IsNotNull(stream);
                var serializer = new XmlSerializer(typeof (SrmDocument));
                return (SrmDocument) serializer.Deserialize(stream);
            }
        }
    }
}
