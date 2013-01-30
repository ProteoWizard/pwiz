/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.Common.DataBinding;

namespace pwiz.Topograph.Test.DataBinding
{
    /// <summary>
    /// Summary description for IdentifierPathTest
    /// </summary>
    [TestClass]
    public class IdentifierPathTest
    {
        [TestMethod]
        public void TestCompareTo()
        {
            var idPath = IdentifierPath.Parse("HalfLives.[]");
            Assert.AreEqual(-1, IdentifierPath.Root.CompareTo(idPath));
            Assert.AreEqual(1, idPath.CompareTo(IdentifierPath.Root));
            Assert.AreEqual(-1, idPath.Parent.CompareTo(idPath));
            Assert.AreEqual(1, idPath.CompareTo(idPath.Parent));
        }
    }
}
