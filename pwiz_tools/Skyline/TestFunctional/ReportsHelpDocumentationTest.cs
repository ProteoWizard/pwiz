/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ReportsHelpDocumentationTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestReportsHelpDocumentationViewer()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();

            // Mixed mode
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.mixed));
            using (var documentationViewerHelper =
                   new DocumentationViewerHelper(TestContext, SkylineWindow.ShowReportsDocumentation))
            {
                VerifyIsMixed(GetHtmlDocument(documentationViewerHelper.DocViewer));
            }
            RunLongDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                SetUiMode(viewEditor, SrmDocument.DOCUMENT_TYPE.mixed);
                using (var documentationViewerHelper =
                       new DocumentationViewerHelper(TestContext, () => viewEditor.ShowColumnDocumentation(false)))
                {
                    VerifyIsMixed(GetHtmlDocument(documentationViewerHelper.DocViewer));
                }
                SetUiMode(viewEditor, SrmDocument.DOCUMENT_TYPE.proteomic);
                using (var documentationViewerHelper =
                       new DocumentationViewerHelper(TestContext, () => viewEditor.ShowColumnDocumentation(false)))
                {
                    VerifyIsProteomic(GetHtmlDocument(documentationViewerHelper.DocViewer));
                }
                SetUiMode(viewEditor, SrmDocument.DOCUMENT_TYPE.small_molecules);
                using (var documentationViewerHelper =
                       new DocumentationViewerHelper(TestContext, () => viewEditor.ShowColumnDocumentation(false)))
                {
                    VerifyIsSmallMolecule(GetHtmlDocument(documentationViewerHelper.DocViewer));
                }
            }, viewEditor => viewEditor.Close());


            // Proteomic mode
            RunUI(() =>
            {
                SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.proteomic);
            });
            using (var documentationViewerHelper =
                   new DocumentationViewerHelper(TestContext, SkylineWindow.ShowReportsDocumentation))
            {
                VerifyIsProteomic(GetHtmlDocument(documentationViewerHelper.DocViewer));
            }
            RunLongDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                SetUiMode(viewEditor, SrmDocument.DOCUMENT_TYPE.proteomic);
                using (var documentationViewerHelper =
                       new DocumentationViewerHelper(TestContext, () => viewEditor.ShowColumnDocumentation(false)))
                {
                    var htmlDoc = GetHtmlDocument(documentationViewerHelper.DocViewer);
                    VerifyIsProteomic(htmlDoc);
                }
                SetUiMode(viewEditor, SrmDocument.DOCUMENT_TYPE.mixed);
                using (var documentationViewerHelper =
                       new DocumentationViewerHelper(TestContext, () => viewEditor.ShowColumnDocumentation(false)))
                {
                    var htmlDoc = GetHtmlDocument(documentationViewerHelper.DocViewer);
                    VerifyIsMixed(htmlDoc);
                }
            }, viewEditor=>viewEditor.Close());

            // Small molecule mode
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));
            using (var documentationViewerHelper =
                   new DocumentationViewerHelper(TestContext, SkylineWindow.ShowReportsDocumentation))
            {
                var htmlDoc = GetHtmlDocument(documentationViewerHelper.DocViewer);
                var proteinNode = htmlDoc.GetElementbyId(typeof(Protein).FullName);
                Assert.AreEqual(ColumnCaptions.MoleculeList, proteinNode.InnerText.Trim());
                Assert.IsNull(FindNodeWithInnerText(htmlDoc, "td", ColumnToolTips.ProteinSequence));
            }
            RunLongDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                SetUiMode(viewEditor, SrmDocument.DOCUMENT_TYPE.small_molecules);
                using (var documentationViewerHelper =
                       new DocumentationViewerHelper(TestContext, () => viewEditor.ShowColumnDocumentation(false)))
                {
                    var htmlDoc = GetHtmlDocument(documentationViewerHelper.DocViewer);
                    VerifyIsSmallMolecule(htmlDoc);
                }
                SetUiMode(viewEditor, SrmDocument.DOCUMENT_TYPE.mixed);
                using (var documentationViewerHelper =
                       new DocumentationViewerHelper(TestContext, () => viewEditor.ShowColumnDocumentation(false)))
                {
                    var htmlDoc = GetHtmlDocument(documentationViewerHelper.DocViewer);
                    VerifyIsMixed(htmlDoc);
                }
            }, viewEditor => viewEditor.Close());
        }

        private HtmlDocument GetHtmlDocument(DocumentationViewer documentationViewer)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(documentationViewer.GetWebView2HtmlContent());
            return htmlDoc;
        }

        private HtmlNode FindNodeWithInnerText(HtmlDocument doc, string tag, string innerText)
        {
            innerText = NormalizeWhitespace(innerText);
            return doc.DocumentNode.Descendants(tag)
                .FirstOrDefault(node => NormalizeWhitespace(node.InnerText) == innerText);
        }

        private string NormalizeWhitespace(string s)
        {
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        private void VerifyIsProteomic(HtmlDocument htmlDoc)
        {
            var proteinNode = htmlDoc.GetElementbyId(typeof(Protein).FullName);
            Assert.AreEqual(ColumnCaptions.Protein, proteinNode.InnerText.Trim());
            Assert.IsNotNull(FindNodeWithInnerText(htmlDoc, "td", ColumnToolTips.ProteinSequence));
        }

        private void VerifyIsMixed(HtmlDocument htmlDoc)
        {
            var proteinNode = htmlDoc.GetElementbyId(typeof(Protein).FullName);
            Assert.AreEqual(ColumnCaptions.MoleculeList, proteinNode.InnerText.Trim());
            Assert.IsNotNull(FindNodeWithInnerText(htmlDoc, "td", ColumnToolTips.ProteinSequence));
        }

        private void VerifyIsSmallMolecule(HtmlDocument htmlDoc)
        {
            var proteinNode = htmlDoc.GetElementbyId(typeof(Protein).FullName);
            Assert.AreEqual(ColumnCaptions.MoleculeList, proteinNode.InnerText.Trim());
            // Protein sequence column should not be present in small molecule mode
            Assert.IsNull(FindNodeWithInnerText(htmlDoc, "td", ColumnToolTips.ProteinSequence));
        }

        private void SetUiMode(ViewEditor viewEditor, SrmDocument.DOCUMENT_TYPE uiMode)
        {
            var viewInfo = viewEditor.ViewInfo;
            var newViewInfo = new ViewInfo(ColumnDescriptor.RootColumn(viewInfo.DataSchema,
                viewInfo.ParentColumn.PropertyType, uiMode.ToString()), viewInfo.ViewSpec);
            Assert.AreEqual(uiMode.ToString(), newViewInfo.ParentColumn.UiMode);
            Assert.AreEqual(uiMode.ToString(), newViewInfo.ViewSpec.UiMode);
            RunUI(()=>
            {
                viewEditor.SetViewInfo(newViewInfo, Array.Empty<PropertyPath>());
            });
        }
    }
}
