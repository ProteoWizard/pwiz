/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ElementLocatorTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestElementLocators()
        {
            var locatorProtein = new ElementLocator("A0JP43", null).ChangeType("MoleculeGroup");
            VerifyElementLocator(locatorProtein);
            var locatorPeptide = new ElementLocator("ELVIS", null).ChangeParent(locatorProtein).ChangeType("Molecule");
            VerifyElementLocator(locatorPeptide);
            var docKey = new ElementLocator("test", new[]
            {
                new KeyValuePair<string, string>("attr1", null),
                new KeyValuePair<string, string>("attr1", "attr2Value"),
                new KeyValuePair<string, string>("attr3&%", "att3Value*\"")
            }).ChangeParent(locatorPeptide);
            VerifyElementLocator(docKey);
        }

        [TestMethod]
        public void TestElementLocatorQuote()
        {
            Assert.AreEqual("a", ElementLocator.QuoteIfSpecial("a"));
            Assert.AreEqual("\"/\"", ElementLocator.QuoteIfSpecial("/"));
            Assert.AreEqual("\"?\"", ElementLocator.QuoteIfSpecial("?"));
            Assert.AreEqual("\"&\"", ElementLocator.QuoteIfSpecial("&"));
            Assert.AreEqual("\"=\"", ElementLocator.QuoteIfSpecial("="));
            Assert.AreEqual("\"\"\"\"", ElementLocator.QuoteIfSpecial("\""));
        }

        private void VerifyElementLocator(ElementLocator objectReference)
        {
            string str = objectReference.ToString();
            var docKeyRoundTrip = ElementLocator.Parse(str);
            Assert.AreEqual(objectReference, docKeyRoundTrip);
            Assert.AreEqual(str, docKeyRoundTrip.ToString());
        }
    }
}
