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
 */
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Model;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Listens to the <see cref="SkylineWindow.DocumentChangedEvent"/> and calls
    /// <see cref="AssertEx.Serializable(SrmDocument)"/>
    /// to make sure the document in memory would always be able to round-trip to XML
    /// </summary>
    public class DocumentSerializableVerifier : IDisposable
    {
        private StartPage _startPage;
        private SkylineWindow _skylineWindow;
        
        public DocumentSerializableVerifier()
        {
            SkylineWindow = Program.MainWindow;
            if (SkylineWindow == null)
            {
                // If we don't have a SkylineWindow let, then listen for the StartPage close event so
                // we can see when the SkylineWindow gets created.
                StartPage = Program.StartWindow;
                Assert.IsNotNull(StartPage);
            }
        }

        private void StartPage_Closed(object sender, EventArgs e)
        {
            SkylineWindow = StartPage.MainWindow;
            StartPage = null;
        }

        public SkylineWindow SkylineWindow
        {
            get
            {
                return _skylineWindow;
            }
            set
            {
                if (ReferenceEquals(SkylineWindow, value))
                {
                    return;
                }

                if (SkylineWindow != null)
                {
                    SkylineWindow.DocumentChangedEvent -= SkylineWindow_DocumentChangedEvent;
                }
                _skylineWindow = value;
                if (SkylineWindow != null)
                {
                    SkylineWindow.DocumentChangedEvent += SkylineWindow_DocumentChangedEvent;
                }
            }
        }

        public StartPage StartPage
        {
            get
            {
                return _startPage;
            }
            set
            {
                if (ReferenceEquals(StartPage, value))
                {
                    return;
                }

                if (StartPage != null)
                {
                    StartPage.Closed -= StartPage_Closed;
                }

                _startPage = value;
                if (StartPage != null)
                {
                    StartPage.Closed += StartPage_Closed;
                }
            }
        }

        private void SkylineWindow_DocumentChangedEvent(object sender, DocumentChangedEventArgs e)
        {
            var document = SkylineWindow?.Document;
            if (document != null)
            {
                AssertEx.Serializable(document);
            }
        }

        public void Dispose()
        {
            SkylineWindow = null;
            StartPage = null;
        }
    }
}
