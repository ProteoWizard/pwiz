/*
 * Original author: Brian Pratt <bspratt .at. proteinms . net>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

//
// Tests a problem with molecules defined by name and mass only during multi-file
// import when we stitch the individual .skyd files together
//

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    [TestClass]
    public class ImportMassOnlyMoleculesTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestImportMassOnlyMolecules()
        {
            Run(GetPerfTestDataURL(@"ImportMassOnlyMoleculesTest.zip"));
        }

        protected override void DoTest()
        {
            OpenDocument("FIATestMix_26OCT17.sky");
            // If the problem persists, this will fail with a message about "custom molecules must specify a formula or mass" during skyd stitching
            ImportResults(new[]{"1_FIATestmix_25OCT17_1.raw","2_FIATestmix_25OCT17_2.raw"});
        }
    }
}