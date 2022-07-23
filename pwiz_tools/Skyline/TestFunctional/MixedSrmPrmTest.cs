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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that when a document contains both SRM and PRM data, the retention time information from
    /// the spectral libraries is never used for peak picking in the SRM runs regardless of the Transition Full Scan
    /// settings (Issue 497)
    /// </summary>
    [TestClass]
    public class MixedSrmPrmTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMixedSrmPrm()
        {
            TestFilesZip = @"TestFunctional\MixedSrmPrmTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var testFiles = new[]
            {
                new TestDataFile(new MsDataFilePath(TestFilesDir.GetTestPath(@"ah_20101029r_BSA_CID_FT_centroid_1.mzML")), 
                    false),
                new TestDataFile(new MsDataFilePath(TestFilesDir.GetTestPath(@"CE_Vantage_15mTorr_0005_REP1.raw")),
                    true)
            };
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MixedSrmPrm.sky")));
            Assert.AreEqual(RetentionTimeFilterType.none,
                SkylineWindow.Document.Settings.TransitionSettings.FullScan.RetentionTimeFilterType);
            ImportResultsFiles(testFiles.Select(file=>file.MsDataFileUri));
            var featureCalculators = new FeatureCalculators(new[] { new MQuestRetentionTimePredictionCalc() });
            var featuresByFile = GetFeaturesByFile(featureCalculators);
            Assert.AreEqual(testFiles.Length, featuresByFile.Count);
            foreach (var fileFeatures in featuresByFile)
            {
                var testFile = testFiles.FirstOrDefault(testFile => Equals(testFile.MsDataFileUri, fileFeatures.Key));
                Assert.IsNotNull(testFile);
                foreach (var peptide in fileFeatures)
                {
                    foreach (var peakGroup in peptide.PeakGroupFeatures)
                    {
                        var retentionTimeScore = peakGroup.Features[0];
                        Assert.AreEqual(float.NaN, retentionTimeScore);
                    }
                }
            }

            bool transitionSettingsClosed = false;
            RunDlg<TransitionSettingsUI>(() =>
            {
                SkylineWindow.ShowTransitionSettingsUI();
                transitionSettingsClosed = true;
            }, transitionSettingsUi =>
            {
                transitionSettingsUi.SetRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, 20);
                transitionSettingsUi.OkDialog();
            });
            WaitForCondition(() => transitionSettingsClosed);
            featuresByFile = GetFeaturesByFile(featureCalculators);
            Assert.AreEqual(testFiles.Length, featuresByFile.Count);
            foreach (var fileFeatures in featuresByFile)
            {
                var testFile = testFiles.FirstOrDefault(testFile => Equals(testFile.MsDataFileUri, fileFeatures.Key));
                Assert.IsNotNull(testFile);
                foreach (var peptide in fileFeatures)
                {
                    foreach (var peakGroup in peptide.PeakGroupFeatures)
                    {
                        var retentionTimeScore = peakGroup.Features[0];
                        if (testFile.IsSrm)
                        {
                            AssertEx.AreEqual(float.NaN, retentionTimeScore);
                        }
                        else
                        {
                            AssertEx.AreNotEqual(float.NaN, retentionTimeScore, null);
                        }
                    }
                }
            }
        }

        private ILookup<MsDataFileUri, PeakTransitionGroupFeatures> GetFeaturesByFile(FeatureCalculators featureCalculators)
        {
            return PeakFeatureEnumerator.GetPeakFeatures(SkylineWindow.Document, featureCalculators).Features
                .ToLookup(featureSet => GetMsDataFileUri(featureSet.Key.FileId));
        }

        private MsDataFileUri GetMsDataFileUri(ChromFileInfoId fileId)
        {
            foreach (var chromatogramSet in SkylineWindow.Document.Settings.MeasuredResults.Chromatograms)
            {
                var chromFileInfo = chromatogramSet.GetFileInfo(fileId);
                if (chromFileInfo != null)
                {
                    return chromFileInfo.FilePath;
                }
            }

            return null;
        }

        class TestDataFile
        {
            public TestDataFile(MsDataFileUri msDataFileUri, bool isSrm)
            {
                MsDataFileUri = msDataFileUri;
                IsSrm = isSrm;
            }
            public MsDataFileUri MsDataFileUri { get; }
            public bool IsSrm { get; }
        }
    }
}
