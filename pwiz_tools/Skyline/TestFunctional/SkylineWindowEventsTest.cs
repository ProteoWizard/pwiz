/*
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SkylineWindowEventsTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestSkylineWindowEvents()
        {
            RunFunctionalTest();
        }

        // Tests SkylineWindow's DocumentSaved event. Especially important to make sure IsSaveAs is set
        // correctly when:
        //      (1) Un-saved documents are saved for the first time
        //      (2) Existing documents are saved to a new path for the first time
        protected override void DoTest()
        {
            // Start with an empty, un-saved document
            var emptyDocument = SrmDocumentHelper.MakeEmptyDocument();
            RunUI(() => SkylineWindow.SwitchDocument(emptyDocument, null));
            WaitForDocumentLoaded();

            var savedPath = TestContext.GetTestResultsPath("documentSavedEvent.sky");
            var subscriberCount = SkylineWindow.DocumentSavedEventSubscriberCount();

            var savedDocument = false;
            var testSavedPath = string.Empty;
            var eventHandled = false;
            var isSaveAs = false;

            SkylineWindow.DocumentSavedEvent += OnDocumentSaved;

            Assert.IsNull(SkylineWindow.DocumentFilePath);
            Assert.AreEqual(subscriberCount + 1, SkylineWindow.DocumentSavedEventSubscriberCount());

            //
            // Scenario #1: new document saved to a path for the first time
            //
            RunUI(() => savedDocument = SkylineWindow.SaveDocument(savedPath));

            Assert.IsTrue(savedDocument);
            Assert.IsTrue(eventHandled);
            Assert.IsTrue(isSaveAs);
            Assert.AreEqual(savedPath, testSavedPath);
            Assert.AreEqual(savedPath, SkylineWindow.DocumentFilePath);

            //
            // Scenario #2: document saved to the same path
            //
            testSavedPath = string.Empty;
            savedDocument = isSaveAs = eventHandled = false;

            RunUI(() => savedDocument = SkylineWindow.SaveDocument());

            Assert.IsTrue(savedDocument);
            Assert.IsTrue(eventHandled);
            Assert.IsFalse(isSaveAs);
            Assert.AreEqual(savedPath, testSavedPath);
            Assert.AreEqual(savedPath, SkylineWindow.DocumentFilePath);

            //
            // Scenario #3: existing saved document saved to a new path
            //
            testSavedPath = string.Empty;
            savedDocument = isSaveAs = eventHandled = false;

            var newPath = TestContext.GetTestResultsPath("documentSavedToNewPath.sky");

            RunUI(() => savedDocument = SkylineWindow.SaveDocument(newPath));

            Assert.IsTrue(savedDocument);
            Assert.IsTrue(eventHandled);
            Assert.IsTrue(isSaveAs);
            Assert.AreEqual(newPath, testSavedPath);
            Assert.AreEqual(newPath, SkylineWindow.DocumentFilePath);

            subscriberCount = SkylineWindow.DocumentSavedEventSubscriberCount();
            SkylineWindow.DocumentSavedEvent -= OnDocumentSaved;
            Assert.AreEqual(subscriberCount - 1, SkylineWindow.DocumentSavedEventSubscriberCount());

            return;

            void OnDocumentSaved(object sender, DocumentSavedEventArgs args)
            {
                eventHandled = true;
                isSaveAs = args.IsSaveAs;
                testSavedPath = args.DocumentFilePath;
            }
        }
    }

    // Borrowing for now from FindNodeCancelTest. Will consolidate if useful.
    internal static class SrmDocumentHelper
    {
        internal static SrmDocument MakeEmptyDocument()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var transitionSettings = srmSettings.TransitionSettings;
            transitionSettings = transitionSettings
                .ChangeInstrument(transitionSettings.Instrument.ChangeMinMz(50))
                .ChangeFilter(transitionSettings.Filter
                    .ChangePeptidePrecursorCharges(new[] { Adduct.SINGLY_PROTONATED })
                    .ChangePeptideProductCharges(new[] { Adduct.SINGLY_PROTONATED })
                    .ChangePeptideIonTypes(new[] { IonType.precursor, IonType.b, IonType.y }));
            srmSettings = srmSettings.ChangeTransitionSettings(transitionSettings);
            return new SrmDocument(srmSettings);
        }
    }
}
