/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that saving a document to a new name does not cause RetentionTimeManager to
    /// recompute the retention time alignments. When a document aligns to a Document Library, saving
    /// to a new name renames that library. The alignment target holds a reference to the library, so
    /// unless the alignment target is updated in step with the rename, the target looks different and
    /// all the retention time alignments get needlessly discarded and recomputed.
    /// </summary>
    [TestClass]
    public class RetentionTimeManagerSaveAsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRetentionTimeManagerSaveAs()
        {
            TestFilesZip = @"TestFunctional\RetentionTimeManagerTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ThreeReplicates.sky")));
            WaitForDocumentLoaded();

            // The document aligns to its Document Library, so it has result file alignments.
            var docBefore = SkylineWindow.Document;
            var alignmentsBefore = docBefore.Settings.DocumentRetentionTimes.ResultFileAlignments;
            Assert.IsFalse(alignmentsBefore.IsEmpty);
            Assert.IsInstanceOfType(docBefore.Settings.DocumentRetentionTimes.AlignmentTarget,
                typeof(AlignmentTarget.LibraryTarget));
            var functionsBefore = GetAlignmentFunctions(alignmentsBefore);
            Assert.AreNotEqual(0, functionsBefore.Count);

            RunUI(() => Assert.IsTrue(SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("Renamed.sky"))));
            WaitForDocumentLoaded();

            // The alignment inputs did not change, so RetentionTimeManager should have reused the
            // existing alignment functions rather than recomputing them. Each alignment function
            // should be the very same object as before the save.
            var alignmentsAfter = SkylineWindow.Document.Settings.DocumentRetentionTimes.ResultFileAlignments;
            Assert.IsFalse(alignmentsAfter.IsEmpty);
            var functionsAfter = GetAlignmentFunctions(alignmentsAfter);
            CollectionAssert.AreEquivalent(functionsBefore.Keys.ToList(), functionsAfter.Keys.ToList());
            foreach (var entry in functionsBefore)
            {
                Assert.AreSame(entry.Value, functionsAfter[entry.Key],
                    @"Alignment function for {0} was recomputed instead of being reused", entry.Key);
            }
        }

        private static Dictionary<MsDataFileUri, PiecewiseLinearMap> GetAlignmentFunctions(
            ResultFileAlignments alignments)
        {
            return alignments.GetAlignmentFunctions()
                .Where(kvp => kvp.Value != null)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
