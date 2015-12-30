/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class MemoryDocumentContainer : IDocumentContainer
    {
        private SrmDocument _document;
        private event EventHandler<DocumentChangedEventArgs> DocumentChangedEvent;

        private static readonly object CHANGE_EVENT_LOCK = new object();

        public SrmDocument Document
        {
            get { return _document; }
        }

        public string DocumentFilePath { get; set; }

        public IProgressMonitor ProgressMonitor { get; set; }

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

                    bool complete = docNew.IsLoaded || (LastProgress != null && LastProgress.IsError);
                    if (wait)
                    {
                        if (!complete)
                            Monitor.Wait(CHANGE_EVENT_LOCK);    // Wait for completing document changed event
                    }
                    else if (complete)
                    {
                        Monitor.Pulse(CHANGE_EVENT_LOCK);
                    }
                }
            }

            return true;
        }

        public void ResetProgress()
        {
            LastProgress = null;
        }

        public ProgressStatus LastProgress { get; private set; }

        private void UpdateProgress(object sender, ProgressUpdateEventArgs e)
        {
            // Unblock the waiting thread, if there was a cancel or error
            lock (CHANGE_EVENT_LOCK)
            {
                // Keep track of last progress, but do not overwrite an error
                if (LastProgress == null || !LastProgress.IsError)
                    LastProgress = (!e.Progress.IsComplete ? e.Progress : null);
                if (e.Progress.IsCanceled || e.Progress.IsError)
                    Monitor.Pulse(CHANGE_EVENT_LOCK);
            }

            if (ProgressMonitor != null)
                ProgressMonitor.UpdateProgress(e.Progress);
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

    public class ResultsMemoryDocumentContainer : MemoryDocumentContainer, IDisposable
    {
        public ResultsMemoryDocumentContainer(SrmDocument docInitial, string pathInitial)
            : this(docInitial, pathInitial, false)
        {            
        }

        public ResultsMemoryDocumentContainer(SrmDocument docInitial, string pathInitial, bool wait)
        {
            SetDocument(docInitial, null, wait);
            // Chromatogram loader needs file path to know how to place the .skyd file
            DocumentFilePath = pathInitial;

            ChromatogramManager = new ChromatogramManager();
            ChromatogramManager.Register(this);
            Register(ChromatogramManager);

            LibraryManager = new LibraryManager();
            LibraryManager.Register(this);
            Register(LibraryManager);

            RetentionTimeManager = new RetentionTimeManager();
            RetentionTimeManager.Register(this);
            Register(RetentionTimeManager);

            IonMobilityManager = new IonMobilityLibraryManager();
            IonMobilityManager.Register(this);
            Register(IonMobilityManager);

            IrtDbManager = new IrtDbManager();
            IrtDbManager.Register(this);
            Register(IrtDbManager);
        }

        public ChromatogramManager ChromatogramManager { get; private set; }

        public LibraryManager LibraryManager { get; private set; }

        public RetentionTimeManager RetentionTimeManager { get; private set; }

        public IonMobilityLibraryManager IonMobilityManager { get; private set; }

        public IrtDbManager IrtDbManager { get; private set; }

        public void Dispose()
        {
            // Release current document to ensure the streams are closed on it
            SetDocument(new SrmDocument(SrmSettingsList.GetDefault()), Document);
        }
    }
}
