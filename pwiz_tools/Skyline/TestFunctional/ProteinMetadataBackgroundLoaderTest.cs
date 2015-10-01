/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// functional tests to make sure background protein metadata loader 
    /// fires properly, in concert with the background proteome loader
    /// </summary>

    [TestClass]
    public class ProteinMetadataBackgroundTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void ProteinMetadataBackgroundLoaderTest()
        {
            TestFilesZip = @"TestFunctional\ProteinMetadataBackgroundLoaderTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Open the .sky file, and and a version 0.0 protdb file that needs digesting and metadata lookup
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("backgroundtest.sky")));
            int millis = (AllowInternetAccess ? 300 : 60) *1000;
            WaitForCondition(millis, () => SkylineWindow.Document.PeptideGroupCount > 0); // Doc loaded
            WaitForCondition(millis, () => (!(SkylineWindow.Document.PeptideGroups.Where(node => string.IsNullOrEmpty(node.ProteinMetadata.Accession))).Any())); // Easy protein metadata loaded
            WaitForBackgroundProteomeLoaderCompleted();  // Make sure we're done with yeast.protdb (may still be loading protein metadata) so test exits cleanly
        }
    }

    [TestClass]
    public class WebAccessProteinMetadataTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void WebAccessProteinMetadataBackgroundLoaderTest()
        {
            TestFilesZip = @"TestFunctional\ProteinMetadataBackgroundLoaderTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            if (!AllowInternetAccess)
                return; // Not really a success, but certainly not a failure CONSIDER can we have a new attribure for tests like these?

            // Open the .sky file, and and a version 0.0 protdb file that needs digesting and metadata lookup
            // The background loaders should actually hit the web services if SkylineTestRunner has enabled internet access.
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("backgroundtest.sky")));
            WaitForCondition(180 * 1000, () => SkylineWindow.Document.PeptideGroupCount > 0); // doc loaded
            WaitForCondition(6 * 60 * 1000, () => (!(SkylineWindow.Document.PeptideGroups.Where(node => string.IsNullOrEmpty(node.ProteinMetadata.Accession))).Any())); // Easy protein metadata loaded
            WaitForCondition(6 * 60 * 1000, () => (!(SkylineWindow.Document.PeptideGroups.Where(node => string.IsNullOrEmpty(node.ProteinMetadata.Gene))).Any())); // Uniprot search metadata loaded
            WaitForBackgroundProteomeLoaderCompleted();  // Make sure we're done with yeast.protdb (may still be loading protein metadata) so test exits cleanly
        }
    }
}
