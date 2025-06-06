﻿/*
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
    }
}
