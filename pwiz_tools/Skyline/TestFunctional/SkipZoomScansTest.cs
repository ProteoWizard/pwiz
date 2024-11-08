/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that spectra with "zoom scan" ("MS:1000497") are not included in extracted chromatograms.
    /// </summary>
    [TestClass]
    public class SkipZoomScansTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSkipZoomScans()
        {
            TestFilesZip = @"TestFunctional\SkipZoomScansTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {        
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ZoomScanTest.sky"));
            });
            RunLongDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                RunUI(()=>transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Instrument);
                RunDlg<EditSpectrumFilterDlg>(transitionSettingsUi.EditSpectrumFilter, editSpectrumFilterDlg =>
                {
                    editSpectrumFilterDlg.SelectPage(1);
                    var row = editSpectrumFilterDlg.RowBindingList.AddNew();
                    Assert.IsNotNull(row);
                    row.SetProperty(SpectrumClassColumn.IsolationWindowWidth);
                    row.SetOperation(FilterOperations.OP_IS_LESS_THAN);
                    row.SetValue(10);
                    editSpectrumFilterDlg.OkDialog();
                });
            }, transitionSettingsUi=>transitionSettingsUi.OkDialog());
            
            var mzmlPath = TestFilesDir.GetTestPath("zoomtest.mzML");
            ImportResultsFile(mzmlPath);
            using var msdatafile = new MsDataFileImpl(TestFilesDir.GetTestPath(mzmlPath));
            var spectra = Enumerable.Range(0, msdatafile.SpectrumCount).Select(msdatafile.GetSpectrum)
                .ToDictionary(spectrum=> spectrum.Id);
            var zoomSpectra = spectra.Values.Where(spectrum =>IsZoomScan(spectrum)).ToList();
            Assert.AreNotEqual(0, zoomSpectra.Count);

            var document = SkylineWindow.Document;
            var peptideDocNode = document.Molecules.First();
            var measuredResults = document.Settings.MeasuredResults;
            float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            Assert.IsNotNull(measuredResults);
            Assert.IsTrue(measuredResults.TryLoadChromatogram(measuredResults.Chromatograms[0], peptideDocNode, peptideDocNode.TransitionGroups.First(), tolerance, out var chromatogramGroupInfos));
            var chromatogramGroupInfo = chromatogramGroupInfos.Single();
            var chromatogramInfos = Enumerable.Range(0, chromatogramGroupInfo.NumTransitions)
                .Select(i => chromatogramGroupInfo.GetTransitionInfo(i, TransformChrom.raw)).ToList();
            var resultFileMetadata = measuredResults.GetResultFileMetaData(chromatogramGroupInfo.FilePath);
            Assert.IsNotNull(resultFileMetadata);
            foreach (var chromSource in new[] { ChromSource.ms1, ChromSource.fragment })
            {
                var chromatogramInfo = chromatogramInfos.FirstOrDefault(info => info.Source == chromSource);
                Assert.IsNotNull(chromatogramInfo, "Could not find chromatogram with source {0}", chromSource);
                var timeIntensities = chromatogramInfo.TimeIntensities;
                Assert.AreNotEqual(0, timeIntensities.NumPoints);
                for (int iSpectrum = 0; iSpectrum < timeIntensities.NumPoints; iSpectrum++)
                {
                    var spectrumMetadata = resultFileMetadata.SpectrumMetadatas[timeIntensities.ScanIds[iSpectrum]];
                    Assert.IsTrue(spectra.TryGetValue(spectrumMetadata.Id, out var spectrum), "Could not find spectrum {0}", spectrumMetadata.Id);
                    Assert.IsFalse(IsZoomScan(spectrum));
                }
            }
        }

        private bool IsZoomScan(MsDataSpectrum spectrum)
        {
            var precursors = spectrum.GetPrecursorsByMsLevel(1);
            if (precursors.Count == 0)
            {
                return false;
            }
            Assert.AreEqual(1, precursors.Count);
            return 10 < precursors[0].IsolationWidth;
        }
    }
}
