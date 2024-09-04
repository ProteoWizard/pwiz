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
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that when loading older documents, "ignore_sim_scans" gets translated into
    /// <see cref="TransitionFullScan.IgnoreSimScansFilter"/> and vice-versa when creating a
    /// .sky.zip in older Skyline formats.
    /// </summary>
    [TestClass]
    public class LegacyIgnoreSimTest : AbstractFunctionalTest
    {
        private const string ignore_sim_scans = "ignore_sim_scans";
        [TestMethod]
        public void TestLegacyIgnoreSim()
        {
            TestFilesZip = @"TestFunctional\LegacyIgnoreSimTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var documentFilePath = TestFilesDir.GetTestPath("IgnoreSim.sky");
            RunUI(()=>SkylineWindow.OpenFile(documentFilePath));
            var transitionFullScan = SkylineWindow.Document.Settings.TransitionSettings.FullScan;
            Assert.IsFalse(transitionFullScan.IgnoreSimScans);
            CollectionAssert.Contains(transitionFullScan.SpectrumClassFilter.Clauses.ToList(),
                TransitionFullScan.IgnoreSimScansFilter);
            var originalFullScan = LoadFullScanElement(documentFilePath);
            Assert.IsTrue(originalFullScan.HasAttribute(ignore_sim_scans));
            Assert.AreEqual("true", originalFullScan.GetAttribute(ignore_sim_scans));
            RunUI(()=>SkylineWindow.SaveDocument());
            var currentFullScan = LoadFullScanElement(documentFilePath);
            Assert.IsFalse(currentFullScan.HasAttribute(ignore_sim_scans));
            RunUI(()=>
            {
                var skyZipPath = TestFilesDir.GetTestPath("OldVersion.sky.zip");
                SkylineWindow.ShareDocument(skyZipPath, ShareType.COMPLETE.ChangeSkylineVersion(SkylineVersion.V24_1));
                SkylineWindow.OpenSharedFile(skyZipPath);
            });
            var legacyFullScan = LoadFullScanElement(SkylineWindow.DocumentFilePath);
            Assert.IsTrue(legacyFullScan.HasAttribute(ignore_sim_scans));
            Assert.AreEqual("true", legacyFullScan.GetAttribute(ignore_sim_scans));
            RunUI(() =>
            {
                var skyZipPath = TestFilesDir.GetTestPath("CurrentVersion.sky.zip");
                SkylineWindow.ShareDocument(skyZipPath, ShareType.COMPLETE.ChangeSkylineVersion(SkylineVersion.CURRENT));
                SkylineWindow.OpenSharedFile(skyZipPath);
            });
            var sharedFullScan = LoadFullScanElement(SkylineWindow.DocumentFilePath);
            Assert.IsFalse(sharedFullScan.HasAttribute(ignore_sim_scans));
        }

        private XmlElement GetFullScanElement(XmlDocument doc)
        {
            return (XmlElement)doc.DocumentElement!.SelectSingleNode(
                "/srm_settings/settings_summary/transition_settings/transition_full_scan");
        }

        private XmlElement LoadFullScanElement(string filePath)
        {
            var xDoc = new XmlDocument();
            xDoc.Load(filePath);
            return GetFullScanElement(xDoc);
        }
    }
}
