/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
 */using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class SubstringFinderTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSubstringFinder()
        {
            for (int maxSubstringLength = 0; maxSubstringLength < 6; maxSubstringLength++)
            {
                var substringFinder = new SubstringFinder("hello", maxSubstringLength);
                AssertEx.IsTrue(substringFinder.ContainsSubstring("ell"));
                AssertEx.IsTrue(substringFinder.ContainsSubstring("hello"));
                AssertEx.IsTrue(substringFinder.ContainsSubstring("ello"));
                AssertEx.IsTrue(substringFinder.ContainsSubstring(string.Empty));
                AssertEx.IsFalse(substringFinder.ContainsSubstring("lloh"));
                AssertEx.IsFalse(substringFinder.ContainsSubstring("olleh"));
                AssertEx.IsFalse(substringFinder.ContainsSubstring("leh"));
                AssertEx.IsFalse(substringFinder.ContainsSubstring("hellox"));
            }
            AssertEx.IsTrue(new SubstringFinder(string.Empty).ContainsSubstring(string.Empty));
            AssertEx.IsFalse(new SubstringFinder(string.Empty).ContainsSubstring("x"));
        }
    }
}
