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
using pwiz.SkylineTestUtil;

namespace CommonTest.DataBinding
{
    [TestClass]
    public class PropertyPathTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestDataBindingCompareTo()
        {
            var idPath = PropertyPath.Parse("HalfLives!*");
            Assert.AreEqual(-1, PropertyPath.Root.CompareTo(idPath));
            Assert.AreEqual(1, idPath.CompareTo(PropertyPath.Root));
            Assert.AreEqual(-1, idPath.Parent.CompareTo(idPath));
            Assert.AreEqual(1, idPath.CompareTo(idPath.Parent));
        }
        [TestMethod]
        public void TestDataBindingParse()
        {
            Assert.IsTrue(ParseVariants("", (string) null).IsRoot);
            var property = ParseVariants("property", "\"property\"");
            Assert.IsTrue(property.IsProperty);
            Assert.IsFalse(property.IsLookup);
            Assert.AreEqual("property", property.Name);
            var lookup = ParseVariants("!lookup", "!\"lookup\"");
            Assert.IsFalse(lookup.IsProperty);
            Assert.IsTrue(lookup.IsLookup);
            Assert.AreEqual("lookup", lookup.Name);
            var propertyLookup = ParseVariants("property!lookup", "\"property\"!lookup", "property!\"lookup\"", "\"property\"!\"lookup\"");
            Assert.IsFalse(propertyLookup.IsProperty);
            Assert.IsTrue(propertyLookup.Parent.IsProperty);

            var unboundAtRoot = ParseCanonical("!*");
            Assert.IsTrue(unboundAtRoot.IsUnboundLookup);
            var lookupStar = ParseCanonical("!\"*\"");
            Assert.IsFalse(lookupStar.IsUnboundLookup);
        }
        [TestMethod]
        public void TestDataBindingParseErrors()
        {
            ParseWithErrorAt("\"property", 0);
            ParseWithErrorAt("\"propert\"y", 9);
            ParseWithErrorAt("*", 0);
            ParseWithErrorAt("prop.*", 5);
            ParseWithErrorAt(".property", 0);
            ParseWithErrorAt("prop1..prop2", 6);
        }

        private static PropertyPath ParseCanonical(string str)
        {
            var propertyPath = PropertyPath.Parse(str);
            Assert.AreEqual(str, propertyPath.ToString());
            return propertyPath;
        }

        private static PropertyPath ParseVariants(string strCanonical, params string[] strVariants)
        {
            var propertyPath = ParseCanonical(strCanonical);
            foreach (var strVariant in strVariants)
            {
                var variant = PropertyPath.Parse(strVariant);
                Assert.AreEqual(propertyPath, variant);
                Assert.AreEqual(strCanonical, variant.ToString());
            }
            return propertyPath;
        }
        private static void ParseWithErrorAt(string str, int expectedLocation)
        {
            try
            {
                var propertyPath = PropertyPath.Parse(str);
                Assert.Fail("Unexpected success parsing {0}:{1}", str, propertyPath);
            }
            catch (PropertyPath.ParseException parseException)
            {
                Assert.AreEqual(expectedLocation, parseException.Location);
            }
        }
    }
}
