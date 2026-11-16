/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ZedGraphTest : AbstractUnitTest
    {
        /// <summary>
        /// Verifies that changing <see cref="FontSpec.Size"/> can be done repeatedly without exception.
        /// There used to be a bug that <see cref="FontSpec._scaledSize"/> would be a nonsensical value
        /// and eventually overflow.
        /// </summary>
        [TestMethod]
        public void TestFontSpecSize()
        {
            var fontSpec = new FontSpec();
            for (int i = 0; i < 1000; i++)
            {
                fontSpec.Size = 10;
                Assert.AreEqual(10, fontSpec.Size);
                fontSpec.Size = 100;
                Assert.AreEqual(100, fontSpec.Size);
            }
        }

        /// <summary>
        /// Tic values must not carry binary floating point noise, which .NET Core formats in full
        /// (e.g. "1.1102230246251565e-16" for the zero tic of a -0.6 to 0.8 axis by 0.2).
        /// </summary>
        [TestMethod]
        public void TestScaleTicValuesWithoutNoise()
        {
            Assert.AreEqual(0.0, AddSteps(-0.6, 0.2, 3));
            Assert.AreEqual(0.0, AddSteps(0.6, -0.2, 3));
            Assert.AreEqual(8.7, AddSteps(0, 0.05, 174));
            Assert.AreEqual(-0.2, AddSteps(-0.6, 0.2, 2));
            // Beyond the range of decimal, the double result is returned.
            Assert.AreEqual(1.2e30, AddSteps(1e30, 1e29, 2), 1e16);
        }

        private static double AddSteps(double baseVal, double stepSize, double stepCount)
        {
            var addSteps = typeof(Scale).GetMethod(@"AddSteps", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(addSteps);
            return (double) addSteps.Invoke(null, new object[] { baseVal, stepSize, stepCount });
        }
    }
}
