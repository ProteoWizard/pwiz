/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AddTransitionsTest : AbstractUnitTest
    {
        private float[] _times1;
        private float[] _intensities1;
        private float[] _times2;
        private float[] _intensities2;

        [TestMethod]
        public void TestAddTransitions()
        {
            // Both empty.
            _times1 = new float[0];
            _times2 = new float[0];
            _intensities1 = new float[0];
            _intensities2 = new float[0];
            Check(false, null, null);

            // One empty.
            _times1 = new float[0];
            _times2 = new[] {1.0f, 2.0f, 3.0f};
            _intensities1 = new float[0];
            _intensities2 = new[] {100.0f, 200.0f, 300.0f};
            Check(false, null, null);

            // Single point.
            _times1 = new[] {1.0f};
            _times2 = new[] {1.0f, 2.0f, 3.0f};
            _intensities1 = new[] {10.0f};
            _intensities2 = new[] {100.0f, 200.0f, 300.0f};
            Check(true, _times1, new[] {110.0f});

            // Single interpolated point.
            _times1 = new[] {1.5f};
            _times2 = new[] {1.0f, 2.0f, 3.0f};
            _intensities1 = new[] {10.0f};
            _intensities2 = new[] {100.0f, 200.0f, 300.0f};
            Check(true, _times1, new[] {160.0f});

            // Identical times.
            _times1 = new[] {1.0f, 2.0f, 3.0f};
            _times2 = _times1;
            _intensities1 = new[] {10.0f, 20.0f, 30.0f};
            _intensities2 = new[] {100.0f, 200.0f, 300.0f};
            Check(true, _times1, new[] {110.0f, 220.0f, 330.0f});

            // times1 shorter from beginning.
            _times1 = new[] {1.0f, 2.0f, 3.0f};
            _times2 = new[] {0.0f, 1.0f, 2.0f, 3.0f};
            _intensities1 = new[] {10.0f, 20.0f, 30.0f};
            _intensities2 = new[] {50.0f, 100.0f, 200.0f, 300.0f};
            Check(true, _times1, new[] {110.0f, 220.0f, 330.0f});

            // times1 shorter from end.
            _times1 = new[] {1.0f, 2.0f, 3.0f};
            _times2 = new[] {1.0f, 2.0f, 3.0f, 4.0f};
            _intensities1 = new[] {10.0f, 20.0f, 30.0f};
            _intensities2 = new[] {100.0f, 200.0f, 300.0f, 400.0f};
            Check(true, _times1, new[] {110.0f, 220.0f, 330.0f});

            // times1 interpolated shorter from beginning.
            _times1 = new[] {1.0f, 2.0f, 3.0f};
            _times2 = new[] {0.5f, 1.5f, 2.5f};
            _intensities1 = new[] {10.0f, 20.0f, 30.0f};
            _intensities2 = new[] {50.0f, 100.0f, 200.0f, 300.0f};
            Check(true, new[] {1.0f, 2.0f}, new[] {85.0f, 170.0f});

            // times1 interpolated shorter from end.
            _times1 = new[] {1.0f, 2.0f, 3.0f};
            _times2 = new[] {1.5f, 2.5f, 3.5f, 4.5f};
            _intensities1 = new[] {10.0f, 20.0f, 30.0f};
            _intensities2 = new[] {100.0f, 200.0f, 300.0f, 400.0f};
            Check(true, new[] {1.5f, 2.5f}, new[] {115.0f, 225.0f});

            // times1 interpolated shorter from both ends.
            _times1 = new[] {1.0f, 2.0f, 3.0f};
            _times2 = new[] {0.5f, 1.5f, 2.5f, 3.5f};
            _intensities1 = new[] {10.0f, 20.0f, 30.0f};
            _intensities2 = new[] {50.0f, 100.0f, 200.0f, 300.0f};
            Check(true, _times1, new[] {85.0f, 170.0f, 280.0f});

            // times1 higher frequency.
            _times1 = new[] {0.0f, 1.0f};
            _times2 = new[] {0.25f, 0.5f, 0.75f};
            _intensities1 = new[] {0f, 100.0f};
            _intensities2 = new[] {100f, 200f, 300f};
            Check(true, _times2, new[] {125.0f, 250.0f, 375.0f});

            // times2 higher frequency.
            // CONSIDER: AddTransitions.Add chooses the sampling rate of the times array with the
            // greater starting time.  In this case, times1 has a higher sampling rate and would
            // be the better choice.
            _times1 = new[] {0f, 1f, 2f, 3f, 4f};
            _times2 = new[] {1f, 3f};
            _intensities1 = new[] {0f, 100f, 200f, 300f, 400f};
            _intensities2 = new[] {1000f, 3000f};
            Check(true, new[] {1f, 3f}, new[] {1100f, 3300f});
        }

        private void Check(bool expectedResult, float[] expectedSumTimes, float[] expectedSumIntensities)
        {
            float[] sumTimes;
            float[] sumIntensities;

            bool result = AddTransitions.Add(_times1, _intensities1, _times2, _intensities2, out sumTimes,
                out sumIntensities);
            Assert.AreEqual(expectedResult, result);
            Assert.IsTrue(ArrayUtil.EqualsDeep(expectedSumTimes, sumTimes));
            Assert.IsTrue(ArrayUtil.EqualsDeep(expectedSumIntensities, sumIntensities));

            // Reverse arguments.
            result = AddTransitions.Add(_times2, _intensities2, _times1, _intensities1, out sumTimes,
                            out sumIntensities);
            Assert.AreEqual(expectedResult, result);
            Assert.IsTrue(ArrayUtil.EqualsDeep(expectedSumTimes, sumTimes));
            Assert.IsTrue(ArrayUtil.EqualsDeep(expectedSumIntensities, sumIntensities));
        }
    }
}
