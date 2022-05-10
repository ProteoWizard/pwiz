/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class OnDemandFeatureCalculatorTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestForcedIntegrationForUserIntegratedPeaks()
        {
            using (var testFilesDir = new TestFilesDir(TestContext, @"Test\OnDemandFeatureCalculatorTest.zip"))
            {
                SrmDocument document;
                using (var stream = File.OpenRead(testFilesDir.GetTestPath("OnDemandFeatureCalculatorTest.sky")))
                {
                    document = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
                }

                var chromatogramCache = ChromatogramCache.Load(
                    testFilesDir.GetTestPath("OnDemandFeatureCalculatorTest.skyd"), new ProgressStatus(),
                    new DefaultFileLoadMonitor(new SilentProgressMonitor()), false);

            }
        }
    }
}
