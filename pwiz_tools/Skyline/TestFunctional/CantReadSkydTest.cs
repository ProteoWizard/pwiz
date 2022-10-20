/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that errors reading from the skyd file during a Document Settings change are
    /// gracefully handled
    /// </summary>
    [TestClass]
    public class CantReadSkydTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCantReadSkyd()
        {
            TestFilesZip = @"TestFunctional\CantReadSkydTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            MzTolerance newMzMatchTolerance = 0.054;
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Human_plasma.sky")));
            WaitForDocumentLoaded();
            Assert.AreNotEqual(newMzMatchTolerance, SkylineWindow.Document.Settings.TransitionSettings.Instrument.IonMatchMzTolerance);

            // Bring up the Transition Settings dialog and change the Instrument Mz Match Tolerance
            // Changing this setting requires reading chromatogram peaks for every precursor in the document
            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            transitionSettings.MZMatchTolerance = newMzMatchTolerance;

            // Lock the .skyd file so that there will be an error when we try to change the document settings
            FileStream stream;
            int retry = 0;
            while (true)
            {
                try
                {
                    FileStreamManager.Default.CloseAllStreams();
                    stream = File.OpenWrite(TestFilesDir.GetTestPath("Human_plasma.skyd"));
                    break;
                }
                catch (IOException)
                {
                    retry++;
                    if (retry >= 10)
                    {
                        throw;
                    }
                    Console.Out.WriteLine("Failed to open file, retry #{0}", retry);
                    Thread.Sleep(100);
                }
            }

            // Try OK'ing the settings dialog and make sure we get an error message
            var alertDlg = ShowDialog<AlertDlg>(transitionSettings.OkDialog);
            Assert.IsNotNull(alertDlg.DetailMessage);
            OkDialog(alertDlg, alertDlg.OkDialog);

            // Release the lock that we have on the skyd file
            stream.Close();

            // OK the Transition Settings dialog
            OkDialog(transitionSettings, transitionSettings.OkDialog);

            // Make sure the change that we made was accepted
            WaitForCondition(() =>
                SkylineWindow.Document.Settings.TransitionSettings.Instrument.IonMatchMzTolerance.Equals(newMzMatchTolerance));
        }
    }
}
