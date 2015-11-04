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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Quantification
{
    /// <summary>
    /// Tests for the CalibrationCurveFitter class.
    /// </summary>
    [TestClass]
    public class CalibrationCurveFitterTest : AbstractUnitTest
    {
        const double epsilon = 0.00001;
        [TestMethod]
        public void TestCalibrationCurveFitter()
        {
            var srmDocument = LoadTestDocument();
            var calibrationCurveFitter = GetCalibrationCurveFitter(srmDocument);
            var calibrationCurve = calibrationCurveFitter.GetCalibrationCurve();
            Assert.AreNotEqual(0, calibrationCurve.PointCount);
            Assert.IsNull(calibrationCurve.QuadraticCoefficient);
            Assert.IsNotNull(calibrationCurve.Slope);
            Assert.IsNull(calibrationCurve.ErrorMessage);
        }

        [TestMethod]
        public void TestCalWithNoStandards()
        {
            var srmDocument = LoadTestDocument();
            srmDocument = ChangeStandardConcentrationCount(srmDocument, 0);
            srmDocument = ChangeQuantificationSettings(srmDocument,
                QuantificationSettings.DEFAULT.ChangeRegressionFit(RegressionFit.NONE));
            var calibrationCurveFitter = GetCalibrationCurveFitter(srmDocument);
            var calibrationCurve = calibrationCurveFitter.GetCalibrationCurve();
            Assert.AreEqual(1.0, calibrationCurve.Slope);
            Assert.AreEqual(0, calibrationCurve.PointCount);
        }

        /// <summary>
        /// Tests the case where there are multiple calibrators, but no normalization.
        /// 
        /// X-Axis: "Analyte Concentration"
        /// Y-Axis: "Peak Area"
        /// </summary>
        [TestMethod]
        public void TestMultipointExternalCalibrationWithoutNormalization()
        {
            var srmDocument = LoadTestDocument();
            srmDocument = ChangeQuantificationSettings(srmDocument,
                QuantificationSettings.DEFAULT
                    .ChangeNormalizationMethod(NormalizationMethod.NONE)
                    .ChangeRegressionFit(RegressionFit.LINEAR)
                    .ChangeUnits("ng/mL"));
            PeptideGroupDocNode protein = srmDocument.MoleculeGroups.First();
            PeptideDocNode peptide = srmDocument.Peptides.First();
            peptide = peptide.ChangeInternalStandardConcentration(null);
            CalibrationCurveFitter curveFitter = CalibrationCurveFitter.GetCalibrationCurveFitter(srmDocument.Settings, protein, peptide);
            string expectedXAxis = CalibrationCurveFitter.AppendUnits(QuantificationStrings.Analyte_Concentration, "ng/mL");
            Assert.AreEqual(expectedXAxis, curveFitter.GetXAxisTitle());
            string expectedYAxis = QuantificationStrings.CalibrationCurveFitter_GetYAxisTitle_Peak_Area;
            Assert.AreEqual(expectedYAxis, curveFitter.GetYAxisTitle());
            var chromatograms = srmDocument.Settings.MeasuredResults.Chromatograms;
            var lightPrecursor = peptide.TransitionGroups
                .First(tg => IsotopeLabelType.light.Equals(tg.TransitionGroup.LabelType));
            for (int iReplicate = 0; iReplicate < chromatograms.Count; iReplicate++)
            {
                string msg = string.Format("Replicate {0}", iReplicate);
                var expectedY = lightPrecursor.Results[iReplicate].First().Area.Value;
                Assert.AreEqual(expectedY, curveFitter.GetYValue(iReplicate).Value, .01, msg);
                var calibrationCurve = curveFitter.GetCalibrationCurve();
                Assert.IsNotNull(calibrationCurve.Slope);
                Assert.IsNotNull(calibrationCurve.RSquared);
                double expectedConcentration = calibrationCurve.GetX(expectedY).Value;
                Assert.AreEqual(expectedConcentration,
                    curveFitter.GetCalculatedXValue(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
                Assert.AreEqual(expectedConcentration,
                    curveFitter.GetCalculatedConcentration(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
            }
        }

        /// <summary>
        /// Tests the case where there are multiple calibrators, normalized by ratio to heavy, 
        /// but no Internal Standard concentration has been specified.
        /// 
        /// X-Axis: "Analyte Concentration"
        /// Y-Axis: "Light:Heavy Peak Area Ratio"
        /// </summary>
        [TestMethod]
        public void TestMultipointExternalCalibrationWithoutIsConcentration()
        {
            var srmDocument = LoadTestDocument();
            srmDocument = ChangeQuantificationSettings(srmDocument,
                QuantificationSettings.DEFAULT
                    .ChangeNormalizationMethod(NormalizationMethod.GetNormalizationMethod(IsotopeLabelType.heavy))
                    .ChangeRegressionFit(RegressionFit.LINEAR)
                    .ChangeUnits("ng/mL"));
            PeptideGroupDocNode protein = srmDocument.MoleculeGroups.First();
            PeptideDocNode peptide = srmDocument.Peptides.First();
            peptide = peptide.ChangeInternalStandardConcentration(null);
            CalibrationCurveFitter curveFitter = CalibrationCurveFitter.GetCalibrationCurveFitter(srmDocument.Settings, protein, peptide);
            string expectedXAxis = CalibrationCurveFitter.AppendUnits(QuantificationStrings.Analyte_Concentration, "ng/mL");
            Assert.AreEqual(expectedXAxis, curveFitter.GetXAxisTitle());
            string expectedYAxis = CalibrationCurveFitter.PeakAreaRatioText(IsotopeLabelType.light, IsotopeLabelType.heavy);
            Assert.AreEqual(expectedYAxis, curveFitter.GetYAxisTitle());
            var chromatograms = srmDocument.Settings.MeasuredResults.Chromatograms;
            for (int iReplicate = 0; iReplicate < chromatograms.Count; iReplicate++)
            {
                string msg = string.Format("Replicate {0}", iReplicate);
                var expectedY = peptide.Results[iReplicate].First().LabelRatios.First().Ratio.Ratio;
                Assert.AreEqual(expectedY, curveFitter.GetYValue(iReplicate).Value, .01, msg);
                var calibrationCurve = curveFitter.GetCalibrationCurve();
                Assert.IsNotNull(calibrationCurve.Slope);
                Assert.IsNotNull(calibrationCurve.RSquared);
                double expectedConcentration = calibrationCurve.GetX(expectedY).Value;
                Assert.AreEqual(expectedConcentration,
                    curveFitter.GetCalculatedXValue(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
                Assert.AreEqual(expectedConcentration,
                    curveFitter.GetCalculatedConcentration(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
            }
        }

        /// <summary>
        /// Tests the case where there are multiple calibrators, normalized by ratio to heavy, and the Internal Standard
        /// concentration has been specified.
        /// 
        /// X-Axis: "Light:Heavy Concentration Ratio". This is the concentration as determined by external standards, 
        /// divided by the internal standard concentration.
        /// 
        /// Y-Axis: "Light:Heavy Peak Area Ratio"
        /// </summary>
        [TestMethod]
        public void TestMultipointExternalCalibrationWithIsConcentration()
        {
            const double internalStandardConcentration = 80.0;
            var srmDocument = LoadTestDocument();
            srmDocument = ChangeQuantificationSettings(srmDocument,
                QuantificationSettings.DEFAULT
                    .ChangeNormalizationMethod(NormalizationMethod.GetNormalizationMethod(IsotopeLabelType.heavy))
                    .ChangeRegressionFit(RegressionFit.LINEAR)
                    .ChangeUnits("ng/mL"));
            PeptideGroupDocNode protein = srmDocument.MoleculeGroups.First();
            PeptideDocNode peptide = srmDocument.Peptides.First();
            peptide = peptide.ChangeInternalStandardConcentration(internalStandardConcentration);
            CalibrationCurveFitter curveFitter = CalibrationCurveFitter.GetCalibrationCurveFitter(srmDocument.Settings, protein, peptide);
            string expectedXAxis = CalibrationCurveFitter.ConcentrationRatioText(IsotopeLabelType.light, IsotopeLabelType.heavy);
            Assert.AreEqual(expectedXAxis, curveFitter.GetXAxisTitle());
            string expectedYAxis = CalibrationCurveFitter.PeakAreaRatioText(IsotopeLabelType.light, IsotopeLabelType.heavy);
            Assert.AreEqual(expectedYAxis, curveFitter.GetYAxisTitle());
            var chromatograms = srmDocument.Settings.MeasuredResults.Chromatograms;
            for (int iReplicate = 0; iReplicate < chromatograms.Count; iReplicate++)
            {
                string msg = string.Format("Replicate {0}", iReplicate);
                var expectedY = peptide.Results[iReplicate].First().LabelRatios.First().Ratio.Ratio;
                Assert.AreEqual(expectedY, curveFitter.GetYValue(iReplicate).Value, .01, msg);
                var calibrationCurve = curveFitter.GetCalibrationCurve();
                Assert.IsNotNull(calibrationCurve.Slope);
                Assert.IsNotNull(calibrationCurve.RSquared);
                double expectedConcentration = calibrationCurve.GetX(expectedY).Value*internalStandardConcentration;
                Assert.AreEqual(expectedConcentration/internalStandardConcentration,
                    curveFitter.GetCalculatedXValue(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
                Assert.AreEqual(expectedConcentration,
                    curveFitter.GetCalculatedConcentration(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
            }
        }

        /// <summary>
        /// Test case where there is one external calibrator, a regression through zero, and no normalization.
        /// 
        /// X-Axis: Analyte Concentration
        /// Y-Axis: Peak Area
        /// </summary>
        [TestMethod]
        public void TestSinglePointExternalCalibrationWithoutNormalization()
        {
            var srmDocument = LoadTestDocument();
            srmDocument = ChangeStandardConcentrationCount(srmDocument, 1);
            srmDocument = ChangeQuantificationSettings(srmDocument, QuantificationSettings.DEFAULT
                .ChangeRegressionFit(RegressionFit.LINEAR_THROUGH_ZERO)
                .ChangeNormalizationMethod(NormalizationMethod.NONE));
            PeptideGroupDocNode protein = srmDocument.MoleculeGroups.First();
            PeptideDocNode peptide = protein.Peptides.First();
            peptide = peptide.ChangeInternalStandardConcentration(null);
            PeptideQuantifier peptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(srmDocument.Settings, protein, peptide);
            CalibrationCurveFitter curveFitter = new CalibrationCurveFitter(peptideQuantifier, srmDocument.Settings);
            Assert.AreEqual(QuantificationStrings.Analyte_Concentration, curveFitter.GetXAxisTitle());
            Assert.AreEqual(QuantificationStrings.CalibrationCurveFitter_GetYAxisTitle_Peak_Area, curveFitter.GetYAxisTitle());
            var chromatograms = srmDocument.Settings.MeasuredResults.Chromatograms;
            var lightPrecursor = peptide.TransitionGroups
                .First(tg => IsotopeLabelType.light.Equals(tg.TransitionGroup.LabelType));
            for (int iReplicate = 0; iReplicate < chromatograms.Count; iReplicate++)
            {
                string msg = string.Format("Replicate {0}", iReplicate);
                var expectedY = lightPrecursor.Results[iReplicate].First().Area.Value;
                double? actualY = curveFitter.GetYValue(iReplicate).Value;
                Assert.IsNotNull(actualY);
                Assert.AreEqual(expectedY, actualY.Value, .01, msg);
                var calibrationCurve = curveFitter.GetCalibrationCurve();
                Assert.AreEqual(1, calibrationCurve.PointCount);
                var expectedConcentration = expectedY / calibrationCurve.Slope.Value;
                Assert.AreEqual(expectedConcentration,
                    curveFitter.GetCalculatedXValue(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
                Assert.AreEqual(expectedConcentration,
                    curveFitter.GetCalculatedConcentration(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
                var chromatogramSet = chromatograms[iReplicate];
                if (Equals(chromatogramSet.SampleType, SampleType.STANDARD) && chromatogramSet.AnalyteConcentration.HasValue)
                {
                    // Since there was only one standard, the regression line goes exactly through this sample.
                    Assert.AreEqual(expectedConcentration, chromatogramSet.AnalyteConcentration.Value * peptide.ConcentrationMultiplier.GetValueOrDefault(1.0), epsilon);
                }
            }

        }

        /// <summary>
        /// Test a linear fit through zero normalized by ratio to heavy, but the internal standard concentration has not been specified.
        /// 
        /// X-Axis: Analyte Concentration
        /// Y-Axis: Light:Heavy Peak Area Ratio
        /// </summary>
        [TestMethod]
        public void TestSinglePointExternalCalibrationWithoutIsConcentration()
        {
            var srmDocument = LoadTestDocument();
            srmDocument = ChangeStandardConcentrationCount(srmDocument, 1);
            srmDocument = ChangeQuantificationSettings(srmDocument, QuantificationSettings.DEFAULT
                .ChangeRegressionFit(RegressionFit.LINEAR_THROUGH_ZERO)
                .ChangeNormalizationMethod(NormalizationMethod.GetNormalizationMethod(IsotopeLabelType.heavy)));
            PeptideGroupDocNode protein = srmDocument.MoleculeGroups.First();
            PeptideDocNode peptide = protein.Peptides.First();
            peptide = peptide.ChangeInternalStandardConcentration(null);
            PeptideQuantifier peptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(srmDocument.Settings, protein, peptide);
            CalibrationCurveFitter curveFitter = new CalibrationCurveFitter(peptideQuantifier, srmDocument.Settings);
            Assert.AreEqual(QuantificationStrings.Analyte_Concentration, curveFitter.GetXAxisTitle());
            Assert.AreEqual(CalibrationCurveFitter.PeakAreaRatioText(IsotopeLabelType.light, IsotopeLabelType.heavy), curveFitter.GetYAxisTitle());
            var chromatograms = srmDocument.Settings.MeasuredResults.Chromatograms;
            for (int iReplicate = 0; iReplicate < chromatograms.Count; iReplicate++)
            {
                string msg = string.Format("Replicate {0}", iReplicate);
                var expectedY = peptide.Results[iReplicate].First().LabelRatios.First().Ratio.Ratio;
                double? actualY = curveFitter.GetYValue(iReplicate).Value;
                Assert.IsNotNull(actualY);
                Assert.AreEqual(expectedY, actualY.Value, epsilon, msg);
                var calibrationCurve = curveFitter.GetCalibrationCurve();
                var expectedConcentration = expectedY / calibrationCurve.Slope.Value;
                Assert.AreEqual(expectedConcentration,
                    curveFitter.GetCalculatedXValue(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
                Assert.AreEqual(expectedConcentration,
                    curveFitter.GetCalculatedConcentration(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
                var chromatogramSet = chromatograms[iReplicate];
                if (Equals(chromatogramSet.SampleType, SampleType.STANDARD) && chromatogramSet.AnalyteConcentration.HasValue)
                {
                    // Since there was only one standard, the regression line goes exactly through this sample.
                    Assert.AreEqual(expectedConcentration, chromatogramSet.AnalyteConcentration.Value * peptide.ConcentrationMultiplier.GetValueOrDefault(1.0), epsilon);
                }
            }
        }

        /// <summary>
        /// Test a linear fit through zero normalized by ratio to heavy, with the internal standard concentration specified.
        /// 
        /// X-Axis: Light:Heavy Concentration Ratio
        /// Y-Axis: Light:Heavy Peak Area Ratio
        /// </summary>
        [TestMethod]
        public void TestSinglePointExternalCalibrationWithIsConcentration()
        {
            const double internalStandardConcentration = 80.0;
            var srmDocument = LoadTestDocument();
            srmDocument = ChangeStandardConcentrationCount(srmDocument, 1);
            srmDocument = ChangeQuantificationSettings(srmDocument, QuantificationSettings.DEFAULT
                .ChangeRegressionFit(RegressionFit.LINEAR_THROUGH_ZERO)
                .ChangeNormalizationMethod(NormalizationMethod.GetNormalizationMethod(IsotopeLabelType.heavy)));
            PeptideGroupDocNode protein = srmDocument.MoleculeGroups.First();
            PeptideDocNode peptide = protein.Peptides.First();
            peptide = peptide.ChangeInternalStandardConcentration(internalStandardConcentration);
            PeptideQuantifier peptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(srmDocument.Settings, protein, peptide);
            CalibrationCurveFitter curveFitter = new CalibrationCurveFitter(peptideQuantifier, srmDocument.Settings);
            Assert.AreEqual(CalibrationCurveFitter.ConcentrationRatioText(IsotopeLabelType.light, IsotopeLabelType.heavy), 
                curveFitter.GetXAxisTitle());
            Assert.AreEqual(CalibrationCurveFitter.PeakAreaRatioText(IsotopeLabelType.light, IsotopeLabelType.heavy), curveFitter.GetYAxisTitle());
            var chromatograms = srmDocument.Settings.MeasuredResults.Chromatograms;
            for (int iReplicate = 0; iReplicate < chromatograms.Count; iReplicate++)
            {
                string msg = string.Format("Replicate {0}", iReplicate);
                var expectedY = peptide.Results[iReplicate].First().LabelRatios.First().Ratio.Ratio;
                double? actualY = curveFitter.GetYValue(iReplicate).Value;
                Assert.IsNotNull(actualY);
                Assert.AreEqual(expectedY, actualY.Value, epsilon, msg);
                var calibrationCurve = curveFitter.GetCalibrationCurve();
                Assert.AreEqual(1, calibrationCurve.PointCount);
                var expectedConcentration = expectedY*internalStandardConcentration/calibrationCurve.Slope.Value;
                Assert.AreEqual(expectedConcentration/internalStandardConcentration,
                    curveFitter.GetCalculatedXValue(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
                Assert.AreEqual(expectedConcentration,
                    curveFitter.GetCalculatedConcentration(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
                var chromatogramSet = chromatograms[iReplicate];
                if (Equals(chromatogramSet.SampleType, SampleType.STANDARD) && chromatogramSet.AnalyteConcentration.HasValue)
                {
                    // Since there was only one standard, the regression line goes exactly through this sample.
                    Assert.AreEqual(expectedConcentration, chromatogramSet.AnalyteConcentration.Value*peptide.ConcentrationMultiplier.GetValueOrDefault(1.0), epsilon);
                }
            }
        }

        /// <summary>
        /// Tests case where there are no external calibrators, but an InternalStandardConcentration has been specified.
        /// </summary>
        [TestMethod]
        public void TestInternalCalibration()
        {
            const double internalStandardConcentration = 80.0;
            var srmDocument = LoadTestDocument();
            srmDocument = ChangeStandardConcentrationCount(srmDocument, 0);
            srmDocument = ChangeQuantificationSettings(srmDocument,
                QuantificationSettings.DEFAULT
                    .ChangeRegressionFit(RegressionFit.NONE)
                    .ChangeNormalizationMethod(NormalizationMethod.GetNormalizationMethod(IsotopeLabelType.heavy))
                    .ChangeUnits("ng/mL"));
            PeptideGroupDocNode protein = srmDocument.MoleculeGroups.First();
            PeptideDocNode peptide = protein.Peptides.First();
            peptide = peptide.ChangeInternalStandardConcentration(internalStandardConcentration);
            CalibrationCurveFitter curveFitter = CalibrationCurveFitter.GetCalibrationCurveFitter(srmDocument.Settings, protein, peptide);
            string expectedXAxis = CalibrationCurveFitter.AppendUnits(QuantificationStrings.Analyte_Concentration, "ng/mL");
            Assert.AreEqual(expectedXAxis, curveFitter.GetXAxisTitle());
            string expectedYAxis = CalibrationCurveFitter.PeakAreaRatioText(IsotopeLabelType.light, IsotopeLabelType.heavy);
            Assert.AreEqual(expectedYAxis, curveFitter.GetYAxisTitle());
            var chromatograms = srmDocument.Settings.MeasuredResults.Chromatograms;

            for (int iReplicate = 0; iReplicate < chromatograms.Count; iReplicate++)
            {
                string msg = string.Format("Replicate {0}", iReplicate);
                var expectedY = peptide.Results[iReplicate].First().LabelRatios.First().Ratio.Ratio;
                Assert.AreEqual(expectedY, curveFitter.GetYValue(iReplicate).Value, epsilon, msg);
                var calibrationCurve = curveFitter.GetCalibrationCurve();
                Assert.AreEqual(1 / internalStandardConcentration, calibrationCurve.Slope);
                Assert.IsNull(calibrationCurve.RSquared);
                Assert.AreEqual(expectedY * internalStandardConcentration,
                    curveFitter.GetCalculatedXValue(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
                Assert.AreEqual(expectedY * internalStandardConcentration,
                    curveFitter.GetCalculatedConcentration(calibrationCurve, iReplicate).GetValueOrDefault(double.NaN), epsilon, msg);
            }
        }

        private CalibrationCurveFitter GetCalibrationCurveFitter(SrmDocument document)
        {
            var peptideQuantifier = new PeptideQuantifier(
                document.MoleculeGroups.First(),
                document.Peptides.First(), document.Settings.PeptideSettings.Quantification);
            return new CalibrationCurveFitter(peptideQuantifier, document.Settings);
        }

        private SrmDocument ChangeStandardConcentrationCount(SrmDocument srmDocument, int pointCount)
        {
            var measuredResults = srmDocument.Settings.MeasuredResults;
            var standardConcentrations = measuredResults.Chromatograms
                .Where(chrom => Equals(chrom.SampleType, SampleType.STANDARD))
                .Select(chrom => chrom.AnalyteConcentration)
                .OfType<double>()
                .Distinct()
                .ToArray();
            Array.Sort(standardConcentrations);
            Array.Reverse(standardConcentrations);
            var topConcentrations = new HashSet<double>(standardConcentrations.Take(pointCount));
            var newChromatograms = measuredResults.Chromatograms.ToArray();
            for (int i = 0; i < newChromatograms.Length; i++)
            {
                var chrom = newChromatograms[i];
                if (Equals(SampleType.STANDARD, chrom.SampleType) && chrom.AnalyteConcentration.HasValue)
                {
                    if (!topConcentrations.Contains(chrom.AnalyteConcentration.Value))
                    {
                        chrom = chrom.ChangeSampleType(SampleType.UNKNOWN);
                    }
                }
                newChromatograms[i] = chrom;
            }
            measuredResults = measuredResults.ChangeChromatograms(newChromatograms);
            return srmDocument.ChangeMeasuredResults(measuredResults);
        }

        private SrmDocument ChangeQuantificationSettings(SrmDocument srmDocument, QuantificationSettings quantificationSettings)
        {
            return srmDocument.ChangeSettings(
                srmDocument.Settings.ChangePeptideSettings(
                    srmDocument.Settings.PeptideSettings.ChangeAbsoluteQuantification(
                        quantificationSettings)));
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
