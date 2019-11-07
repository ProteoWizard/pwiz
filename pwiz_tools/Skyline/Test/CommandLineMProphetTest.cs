/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class CommandLineMProphetTest : AbstractUnitTestEx
    {
        private const string ZIP_FILE = @"Test\CommandLineMProphetTest.zip";

        [TestMethod]
        public void ConsoleMProphetModelTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            const string docName = "mProphetModel.sky";
            var docPath = testFilesDir.GetTestPath(docName);
            const string modelName = "testModel";

            var args = new List<string>
            {
                "--in=" + docPath,
                "--reintegrate-create-model",
                "--reintegrate-overwrite-peaks",
            };

            var output = RunCommand(args.ToArray());

            // Extra warning about spectral library
            Assert.AreEqual(3, CountInstances(Resources.CommandLineTest_ConsoleAddFastaTest_Warning, output));

            args.Add("--reintegrate-model-name=" + modelName);
            args.Add("--save");

            output = RunCommand(args.ToArray());

            Assert.AreEqual(1, CountInstances(Resources.CommandLineTest_ConsoleAddFastaTest_Warning, output));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_CreateScoringModel_Creating_scoring_model__0_, modelName));
            var doc = ResultsUtil.DeserializeDocument(docPath);
            foreach (var peakFeatureCalculator in MProphetPeakScoringModel.GetDefaultCalculators(doc))
            {
                // Not all of the default calculators for the document are valid
                if (peakFeatureCalculator is MQuestRetentionTimePredictionCalc ||
                    peakFeatureCalculator is MQuestRetentionTimeSquaredPredictionCalc ||
                    peakFeatureCalculator is MQuestIntensityCorrelationCalc ||
                    peakFeatureCalculator is NextGenProductMassErrorCalc ||
                    peakFeatureCalculator is NextGenCrossWeightedShapeCalc ||
                    peakFeatureCalculator is LegacyIdentifiedCountCalc)
                    continue;

                AssertEx.Contains(output, peakFeatureCalculator.Name);
            }
            AssertEx.Contains(output, string.Format(Resources.CommandLine_SaveFile_File__0__saved_, docName));
        }
    }
}