/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Randomly failing tests to check failure reporting in SkylineStress.
    /// </summary>
    //[TestClass]
    public class RandomTest : AbstractUnitTest
    {
        private static readonly Random RANDOM = new Random();

        //[TestMethod]
        public void Fail5Percent()
        {
            var x = RANDOM.Next(100);
            Assert.IsTrue(x >= 5, "5% failure for testing");
        }

        //[TestMethod]
        public void Fail3Or7Percent()
        {
            var x = RANDOM.Next(100);
            Assert.IsTrue(x >= 7, "7% failure for testing");
            Assert.IsTrue(x >= 10, "3% failure for testing");
        }
    }
}
