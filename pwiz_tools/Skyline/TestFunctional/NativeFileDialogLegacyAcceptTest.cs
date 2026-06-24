/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// SPIKE (TODO-20260609_native_file_dialog_automation): exercises
    /// <see cref="OpenFileDialogAutomation.EnterPathAndAcceptViaButton"/>, which accepts the dialog
    /// by invoking the Open button through the legacy IAccessible DoDefaultAction (the screen-reader
    /// path) rather than by posting an Enter keystroke. If this passes, an AutomationElement-only
    /// accept is viable on the common file dialog despite InvokePattern.Invoke no-opping there.
    /// </summary>
    [TestClass]
    public class NativeFileDialogLegacyAcceptTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestNativeFileDialogLegacyAccept()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var savePath = TestContext.GetTestResultsPath(@"NativeFileDialogLegacyAccept.sky");
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(savePath);
                SkylineWindow.NewDocument();
                Settings.Default.ActiveDirectory = System.IO.Path.GetTempPath();
            });
            var documentBeforeOpen = SkylineWindow.Document;
            SkylineWindow.BeginInvoke((Action)(() => SkylineWindow.ShowOpenFileDialog()));
            NativeDialog.WaitForDialog<OpenFileDialogAutomation>().EnterPathAndAcceptViaButton(savePath);
            WaitForDocumentChangeLoaded(documentBeforeOpen);
            Assert.AreEqual(savePath, SkylineWindow.DocumentFilePath);
        }
    }
}
