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
    /// Task which runs in the background and keeps calling <see cref="AssertEx.Serializable(pwiz.Skyline.Model.SrmDocument)"/>
    /// on <see cref="pwiz.Skyline.SkylineWindow.Document"/> to make sure the document in memory would always
    /// be able to round-trip to XML
    /// </summary>
    public class DocumentSerializabilityVerifier : IDisposable
    {
        private static bool RUN_SYNCHRONOUSLY = true;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private SkylineWindow _skylineWindow;
        
        public void Start()
        {
            var thread = ActionUtil.RunAsync(RunOnThisThread);
            thread.Name = "Document Serializability Verifier";
            thread.Priority = ThreadPriority.Lowest;
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

                    if (RUN_SYNCHRONOUSLY)
                    {
                        if (SkylineWindow != null)
                        {
                            SkylineWindow.DocumentChangedEvent -= SkylineWindow_DocumentChangedEvent;
                        }
                    }

                    _skylineWindow = value;
                    if (RUN_SYNCHRONOUSLY)
                    {
                        if (SkylineWindow != null)
                        {
                            SkylineWindow.DocumentChangedEvent += SkylineWindow_DocumentChangedEvent;
                        }
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

        private void RunOnThisThread()
        {
            var cancellationToken = _cancellationTokenSource.Token;
            SrmDocument lastDocument = null;
            while (true)
            {
                SrmDocument document;
                lock (this) 
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    SkylineWindow = Program.MainWindow;
                    document = SkylineWindow?.Document;
                }

                if (document == null || ReferenceEquals(document, lastDocument))
                {
                    // No work to do: Wait for 1 millisecond or until cancelled
                    cancellationToken.WaitHandle.WaitOne(1);
                    continue;
                }

                if (!RUN_SYNCHRONOUSLY)
                {
                    AssertEx.Serializable(document);
                }
                lastDocument = document;
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
