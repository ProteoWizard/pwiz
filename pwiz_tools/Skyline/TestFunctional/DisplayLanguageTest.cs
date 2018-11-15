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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that overriding the UI language (Tools > Options > Language) correctly results in
    /// CSV files using that language's text, although numbers are formatted using the original CultureInfo.
    /// </summary>
    [TestClass]
    public class DisplayLanguageTest : AbstractFunctionalTest
    {
        private CultureInfo _language;
        [TestMethod]
        public void TestDisplayLanguage()
        {
            var languagesToTest = new[] {LocalizationHelper.CurrentUICulture}
                .Concat(CultureUtil.AvailableDisplayLanguages());

            foreach (var language in languagesToTest)
            {
                var oldLanguage = LocalizationHelper.CurrentUICulture;
                try
                {
                    LocalizationHelper.CurrentUICulture = language;
                    _language = language;
                    RunFunctionalTest();
                }
                finally
                {
                    LocalizationHelper.CurrentUICulture = oldLanguage;
                }
            }
        }

        protected override void DoTest()
        {
            Assert.AreEqual(_language, CultureInfo.CurrentUICulture);
            RunUI(()=>SkylineWindow.Paste(TextUtil.LineSeparate("ELVIS", "LIVES")));
            var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(()=>exportLiveReportDlg.ReportName = Resources.Resources_ReportSpecList_GetDefaults_Peptide_Quantification);

            Directory.CreateDirectory(TestContext.TestDir);
            string outputFile = Path.Combine(TestContext.TestDir, "file.csv");
            OkDialog(exportLiveReportDlg, () => exportLiveReportDlg.OkDialog(outputFile, TextUtil.CsvSeparator));
            using (var textReader = new StreamReader(outputFile))
            {
                var csvReader = new DsvFileReader(textReader, TextUtil.CsvSeparator);
                CollectionAssert.Contains(csvReader.FieldNames, ColumnCaptions.CalibrationCurve);
                Assert.IsNotNull(csvReader.ReadLine());
                string actualCalibrationCurve = csvReader.GetFieldByName(ColumnCaptions.CalibrationCurve);
                string expectedCalibrationCurve = string.Format(
                    QuantificationStrings.CalibrationCurve_ToString_Slope___0_,
                    1.0.ToString(Formats.CalibrationCurve));
                Assert.AreEqual(expectedCalibrationCurve, actualCalibrationCurve);
            }
        }
    }
}
