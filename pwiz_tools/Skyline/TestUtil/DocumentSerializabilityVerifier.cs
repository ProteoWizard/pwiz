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
using System.Threading;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Listens to the <see cref="Skyline.SkylineWindow.DocumentChangedEvent"/> and calls
    /// <see cref="AssertEx.Serializable(pwiz.Skyline.Model.SrmDocument)"/>
    /// to make sure the document in memory would always be able to round-trip to XML
    /// </summary>
    public class DocumentSerializabilityVerifier : IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private SkylineWindow _skylineWindow;
        
        public void Start()
        {
            var thread = ActionUtil.RunAsync(WatchForSkylineWindow);
            thread.Name = "Document Serializability Verifier";
        }

        public SkylineWindow SkylineWindow
        {
            get
            {
                return _skylineWindow;
            }
            set
            {
                lock (this)
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
        }

        private void SkylineWindow_DocumentChangedEvent(object sender, DocumentChangedEventArgs e)
        {
            var document = SkylineWindow?.Document;
            if (document != null)
            {
                AssertEx.Serializable(document);
            }
        }

        private void WatchForSkylineWindow()
        {
            var cancellationToken = _cancellationTokenSource.Token;
            while (true)
            {
                lock (this) 
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var skylineWindow = Program.MainWindow;
                    if (skylineWindow != null)
                    {
                        SkylineWindow = skylineWindow;
                        return;
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (this)
            {
                _cancellationTokenSource.Cancel();
                SkylineWindow = null;
            }
        }
    }
}
