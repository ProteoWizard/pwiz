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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Properties;
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

        [TestMethod]
        public void TestNodeRefGetIdentityPaths()
        {
            var peptideGroup = new PeptideGroup();

            var peptideGroupDocNode = new PeptideGroupDocNode(peptideGroup, Annotations.EMPTY, ProteinMetadata.EMPTY,
                new[]
                {
                    new PeptideDocNode(new Peptide("ELVIS")),
                    new PeptideDocNode(new Peptide("LIVES"))
                }, false);
            var document = (SrmDocument) new SrmDocument(SrmSettingsList.GetDefault()).ChangeChildren(new[] { peptideGroupDocNode });
            VerifyNodeRefGetIdentityPaths(document);
            VerifyNodeRefGetIdentityPaths(LoadDocumentFromResource(typeof(DocumentSerializerTest), "DocumentSerializerTest.sky"));
            VerifyNodeRefGetIdentityPaths(LoadDocumentFromResource(typeof(BookmarkEnumeratorTest), "BookmarkEnumeratorTest.sky"));
        }

        private SrmDocument LoadDocumentFromResource(Type type, string resource)
        {
            using (var stream = type.Assembly.GetManifestResourceStream(type, resource))
            {
                Assert.IsNotNull(stream, "Unable to find resource {0} for type {1}", resource, type);
                return (SrmDocument)new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
            }
        }

        /// <summary>
        /// Verifies that <see cref="NodeRef.GetIdentityPaths"/> works when called on random lists of IdentityPaths.
        /// </summary>
        private void VerifyNodeRefGetIdentityPaths(SrmDocument document)
        {
            var seed = (int)DateTime.UtcNow.Ticks;
            var random = new Random(seed);
            var identityPathEnumerator = EnumerateIdentityPaths(IdentityPath.ROOT, document)
                .OrderBy(x => random.Next());
            var elementRefs = new ElementRefs(document);
            var nodeRefs = new List<NodeRef>();
            List<IdentityPath> identityPaths = new List<IdentityPath>();
            foreach (var identityPath in identityPathEnumerator)
            {
                try
                {
                    nodeRefs.Add(elementRefs.GetNodeRef(identityPath));
                    identityPaths.Add(identityPath);
                    var identityPathsCompare = NodeRef.GetIdentityPaths(document, nodeRefs).ToList();
                    CollectionAssert.AreEqual(identityPaths, identityPathsCompare);
                }
                catch (Exception ex)
                {
                    throw new AssertFailedException(string.Format("Failed on iteration #{0} using random seed {1}", 
                        identityPaths.Count, seed), ex);
                }
            }
        }

        private IEnumerable<IdentityPath> EnumerateIdentityPaths(IdentityPath identityPath, DocNode docNode)
        {
            IEnumerable<IdentityPath> identityPaths = new[] { identityPath };
            if (docNode is DocNodeParent docNodeParent)
            {
                identityPaths =
                    identityPaths.Concat(
                        docNodeParent.Children.SelectMany(child => EnumerateIdentityPaths(new IdentityPath(identityPath, child.Id), child)));
            }

            return identityPaths;
        }
    }
}
