/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA.MSstats
{
    /// <summary>
    /// Summary description for PValuesTest
    /// </summary>
    [TestClass]
    public class PValuesTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSimpleAdjustPValues()
        {
            var input = new[] {.005, .02, .25};
            var expected = new[] {.015, .030, .250};
            var actual = PValues.AdjustPValues(input);
            CollectionAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Tests adjusting p values on a list of numbers.
        /// The input data and expected values came from R.
        /// </summary>
        [TestMethod]
        public void TestAdjustListOfPValues()
        {

            var input = new[]
            {
                0, 7.2867665485711353e-01, 1.1395351451692903e-04, 0, 0,
                0, 0, 5.7295134488581922e-03, 3.6202576492172511e-08, 0,
                6.6968629752750530e-08, 0, 0, 0,0,
                0, 0, 0, 0, 0,
                2.6345568091021576e-01, 0, 1.1774885228897247e-02, 2.0858033300541479e-08, 0,
                0, 2.3421400293432981e-05, 0, 0, 0,
                0, 3.3684388611732174e-10, 0, 2.0239094797527057e-01, 0,
                7.9936057773011271e-15, 4.4724224323999806e-12, 0, 0, 4.0897231039323190e-01,
                0, 7.5570363823769249e-03, 0, 4.2761477929438962e-01, 4.5888446988493703e-02,
                9.7037489155127332e-11, 2.0262598487530425e-05, 0
            };
            var expected = new[]
            {
                0, 7.2867665485711353e-01, 1.4025047940545112e-04, 0, 0,
                0, 0, 6.8754161386298304e-03, 4.9649247760693727e-08, 0,
                8.9291506337000698e-08, 0, 0, 0, 0,
                0, 0, 0, 0, 0,
                2.8101939297089679e-01, 0, 1.3457011690168282e-02, 2.9446635247823267e-08, 0,
                0, 2.9584926686441660e-05, 0, 0, 0,
                0, 4.8995474344337708e-10, 0, 2.2079012506393153e-01, 0,
                1.2789769243681804e-14, 6.9250411856515830e-12, 0, 0, 4.2675371519293764e-01,
                0, 8.8472621061973754e-03, 0, 4.3671296608788723e-01, 5.1224312917388319e-02,
                1.4555623373269100e-10, 2.6286614254093528e-05, 0,
            };
            var actual = PValues.AdjustPValues(input);
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] == 0)
                {
                    Assert.AreEqual(expected[i], actual[i]);
                }
                else
                {
                    Assert.AreEqual(expected[i], actual[i], expected[i] / 1E8);
                }
            }
        }
    }
}
