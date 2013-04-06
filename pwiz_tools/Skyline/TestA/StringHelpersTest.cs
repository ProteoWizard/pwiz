/*
 * Original author: Shannon Joyner <sjoyner .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for StringHelpersTest
    /// </summary>
    [TestClass]
    public class StringHelpersTest : AbstractUnitTest
    {
        /// <summary>
        /// Test <see cref="Helpers.RemoveRepeatedLabelText"/> function
        /// to make sure it properly removes any repeated text at the beginning,
        /// in the middle, or at the end of string labels.
        /// </summary>
        [TestMethod]
        public void RepeatTextTest()
        {

            // Label with no repeats.
            var stringLabels = new[]
                               {
                                   "0001_data1_a",
                                   "0002_data2_b",
                                   "0003_data3_c",
                                   "0004_data4_d",
                                   "0005_data5_e"
                               };

            Assert.IsFalse(Helpers.RemoveRepeatedLabelText(stringLabels, 0));

            // String with only expect/library label.
            var noStringsWithLabel = new string[1];
            noStringsWithLabel[0] = "Expected";

            Assert.IsFalse(Helpers.RemoveRepeatedLabelText(noStringsWithLabel, 1));

            // String with nothing. 
            var noStringsWithoutLabel = new string[0];
            Assert.IsFalse(Helpers.RemoveRepeatedLabelText(noStringsWithoutLabel, 0));

            // All but one label have repeats.
            stringLabels = new[]
                               {
                                   "0001_data1_a",
                                   "0001_data2_b",
                                   "0001_data3_c",
                                   "0001_data4_d",
                                   "0005_data5_e"
                               };

            Assert.IsFalse(Helpers.RemoveRepeatedLabelText(stringLabels, 0));

            // String with no repeats and expect/library label.
            stringLabels = new[]
                               {
                                   "Expected",
                                   "0002_data2_b",
                                   "0003_data3_c",
                                   "0004_data4_d",
                                   "0005_data5_e",
                               };

            Assert.IsFalse(Helpers.RemoveRepeatedLabelText(stringLabels, 1));

            // String with start repeat and no expect/library.
            stringLabels = new[]
                               {
                                   "0001_data1_a",
                                   "0001_data2_b",
                                   "0001_data3_c",
                                   "0001_data4_d",
                                   "0001_data5_e",
                               };

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            // If we check again, the repeat text should no longer be there. 
            Assert.IsTrue(stringLabels[0] == "data1_a");
            Assert.IsFalse(Helpers.RemoveRepeatedLabelText(stringLabels, 0));

            // String with start repeat and expect/library.
            stringLabels = new[]
                               {
                                   "Library",
                                   "0001_data2_b",
                                   "0001_data3_c",
                                   "0001_data4_d",
                                   "0001_data5_e",
                               };

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 1));
            // If we check again, the repeat text should no longer be there. 
            Assert.IsTrue(stringLabels[1] == "data2_b");
            Assert.IsFalse(Helpers.RemoveRepeatedLabelText(stringLabels, 1));

            // String with end repeat and no expect/library.
            stringLabels = new[]
                               {
                                   "0001_data1_a",
                                   "0002_data2_a",
                                   "0003_data3_a",
                                   "0004_data4_a",
                                   "0005_data5_a",
                               };

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            // If we check again, the repeat text should no longer be there.
            Assert.IsTrue(stringLabels[0] == "0001_data1");
            Assert.IsFalse(Helpers.RemoveRepeatedLabelText(stringLabels, 0));

            // String with end repeat and expect/library.
            stringLabels = new[]
                               {
                                   "Library",
                                   "0002_data2_a",
                                   "0003_data3_a",
                                   "0004_data4_a",
                                   "0005_data5_a",
                               };

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 1));
            // If we check again, the repeat text should no longer be there.
            Assert.IsTrue(stringLabels[1] == "0002_data2");
            Assert.IsFalse(Helpers.RemoveRepeatedLabelText(stringLabels, 1));

            // String with middle repeat and no expect/library.
            stringLabels = new[]
                               {
                                   "0001_data1_a",
                                   "0002_data1_b",
                                   "0003_data1_c",
                                   "0004_data1_d",
                                   "0005_data1_e",
                               };

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
           
            Assert.IsTrue(stringLabels[0] == "0001...a");
            Assert.IsFalse(Helpers.RemoveRepeatedLabelText(stringLabels, 0));

            // String with start and end repeat.
            stringLabels = new[]
                               {
                                   "0001_data1_a",
                                   "0001_data2_a",
                                   "0001_data3_a",
                                   "0001_data4_a",
                                   "0001_data5_a",
                               };

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            // If we check again, only the end repeat text should be there.
            Assert.IsTrue(stringLabels[0] == "data1_a");
            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
   
            Assert.IsTrue(stringLabels[0] == "data1");
            Assert.IsFalse(Helpers.RemoveRepeatedLabelText(stringLabels, 0));

            // String with middle repeat.
            stringLabels = new[]
                               {
                                   "0001_abc_data1_h_a",
                                   "0002_abc_data2_h_b", 
                                   "0003_abc_data3_h_c", 
                                   "0004_abc_data4_h_d",
                                   "0005_abc_data5_h_e",
                               };

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            Assert.IsTrue(stringLabels[0] == "0001...data1_h_a");
            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            Assert.IsTrue(stringLabels[0] == "0001...data1...a");

            // Different space types.
            stringLabels = new[]
                               {
                                   "0001 data1 a",
                                   "0002 data2 a",
                                   "0003 data3 a",
                                   "0004 data4 a",
                                   "0005 data5 a",
                               };

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            Assert.IsTrue(stringLabels[0] == "0001 data1");

            stringLabels = new[]
                               {
                                   "0001-data1-a",
                                   "0002-data2-a",
                                   "0003-data3-a",
                                   "0004-data4-a",
                                   "0005-data5-a",
                               };


            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            Assert.IsTrue(stringLabels[0] == "0001-data1");


            // Remove 2 blocks of repeat text from middle.
            stringLabels = new[]
                               {
                                   "0001-data1-a-01", "0002-data1-a-02", 
                                   "0003-data1-a-03", "0004-data1-a-04",
                                   "0005-data1-a-05"
                               };
            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            Assert.IsTrue(stringLabels[0] == "0001...a-01");

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            Assert.IsTrue(stringLabels[0] == "0001...01");

            //Remove text within a block (multiple blocks).
            stringLabels = new[]
                               {
                                   "20110710_experiment10_sample01",
                                   "20110710_experiment10_sample02",
                                   "20110710_experiment10_sample03",
                                   "20110710_experiment10_sample03",
                               };

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            Assert.IsTrue(stringLabels[0] == "experiment10_sample01");

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            Assert.IsTrue(stringLabels[0] == "sample01");


            // Labels with repeats within label

            stringLabels = new[]
                               {
                                   "remove_01_remove",
                                   "03_02_remove",
                                   "04_05_remove",
                                   "06_07_remove",
                               };

            Assert.IsTrue(Helpers.RemoveRepeatedLabelText(stringLabels, 0));
            Assert.IsTrue(stringLabels[0] == "remove_01");
        }

    }

}
