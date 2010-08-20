/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009-2010 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    public class TestDocumentContainer : IDocumentContainer
    {
        private SrmDocument _document;
        private event EventHandler<DocumentChangedEventArgs> DocumentChangedEvent;

        private static readonly object CHANGE_EVENT_LOCK = new object();

        public SrmDocument Document
        {
            get { return Interlocked.Exchange(ref _document, _document); }
        }

        public string DocumentFilePath { get; set; }

        /// <summary>
        /// Override for background loaders that update the document with
        /// partially complete results, to keep the container waiting until
        /// the document is complete.  Default returns true to return control
        /// to the test on the first document change.
        /// </summary>
        /// <param name="docNew">A new document being set to the container</param>
        /// <returns>True if no more processing is necessary</returns>
        protected virtual bool IsComplete(SrmDocument docNew)
        {
            return true;
        }

        public bool SetDocument(SrmDocument docNew, SrmDocument docOriginal)
        {
            return SetDocument(docNew, docOriginal, false);
        }

        public bool SetDocument(SrmDocument docNew, SrmDocument docOriginal, bool wait)
        {
            var docResult = Interlocked.CompareExchange(ref _document, docNew, docOriginal);
            if (!ReferenceEquals(docResult, docOriginal))
                return false;

            if (DocumentChangedEvent != null)
            {
                lock (CHANGE_EVENT_LOCK)
                {
                    DocumentChangedEvent(this, new DocumentChangedEventArgs(docOriginal));

                    if (wait)
                        Monitor.Wait(CHANGE_EVENT_LOCK, 10000000);
                    else if (IsComplete(docNew))
                        Monitor.Pulse(CHANGE_EVENT_LOCK);
                }
            }

            return true;
        }

        public ProgressStatus LastProgress { get; private set; }

        public void AssertComplete()
        {
            if (LastProgress != null)
            {
                if (LastProgress.IsError)
                    throw LastProgress.ErrorException;
                else if (LastProgress.IsCanceled)
                    Assert.Fail("Loader cancelled");
                else
                    Assert.Fail("Unknown progress state");
            }
        }

        private void UpdateProgress(object sender, ProgressUpdateEventArgs e)
        {
            // Unblock the waiting thread, if there was a cancel or error
            lock (CHANGE_EVENT_LOCK)
            {
                LastProgress = (!e.Progress.IsComplete ? e.Progress : null);
                if (e.Progress.IsCanceled || e.Progress.IsError)
                    Monitor.Pulse(CHANGE_EVENT_LOCK);
            }
        }

        public void Register(BackgroundLoader loader)
        {
            loader.ProgressUpdateEvent += UpdateProgress;
        }

        public void Unregister(BackgroundLoader loader)
        {
            loader.ProgressUpdateEvent -= UpdateProgress;
        }

        public void Listen(EventHandler<DocumentChangedEventArgs> listener)
        {
            DocumentChangedEvent += listener;
        }

        public void Unlisten(EventHandler<DocumentChangedEventArgs> listener)
        {
            DocumentChangedEvent -= listener;
        }
    }
}