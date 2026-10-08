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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Optimization;
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
        private readonly List<BackgroundLoader> _backgroundLoaders = new List<BackgroundLoader>();
             
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

            // If the document is changing, clear progress for the previous document
            if (docOriginal != null && docNew.Id.GlobalIndex != docOriginal.Id.GlobalIndex)
                _backgroundLoaders.ForEach(l => l.ResetProgress(docOriginal));
                
            if (DocumentChangedEvent != null)
            {
                lock (CHANGE_EVENT_LOCK)
                {
                    DocumentChangedEvent(this, new DocumentChangedEventArgs(docOriginal));

                    if (wait)
                    {
                        WaitForComplete();
                    }
                    else if (IsFinal(Document))
                    {
                        Monitor.Pulse(CHANGE_EVENT_LOCK);
                    }
                }
            }

            return true;
        }

        public void WaitForComplete()
        {
            lock (CHANGE_EVENT_LOCK)
            {
                // Wait for completing document changed event
                uint nLoops = 0;
                int cancelLoops = 0;
                // Order matters: IsSupersededCancel must run every pass so it can reset its count
                while (IsSupersededCancel(ref cancelLoops) || !IsFinal(Document))
                {
                    Monitor.Wait(CHANGE_EVENT_LOCK, 1000);  // Check every second or risk deadlock

                    // Help for debugging occasional hangs in tests - report after an hour's wait, and once an hour after that
                    if (++nLoops % 3600 == 0 && Program.UnitTest && !IsFinal(Document))
                    {
                        const string PREAMBLE = @"# "; // Leading hash is a cue to SkylineTester to ignore these informational lines
                        Console.WriteLine(PREAMBLE + @"unusually long WaitForComplete():");
                        foreach (var why in Document.NonLoadedStateDescriptionsFull)
                        {
                            Console.WriteLine(PREAMBLE + why);
                        }

                        if (LastProgress != null)
                        {
                            Console.WriteLine(PREAMBLE + @"LastProgress status:");
                            Console.WriteLine(PREAMBLE + LastProgress.State);
                            if (!string.IsNullOrEmpty(LastProgress.Message))
                            {
                                Console.WriteLine(PREAMBLE + LastProgress.Message);
                            }
                            if (!string.IsNullOrEmpty(LastProgress.WarningMessage))
                            {
                                Console.WriteLine(PREAMBLE + LastProgress.WarningMessage);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Number of one-second waits allowed for a loader to restart after it cancelled itself
        /// because its document was superseded. The restart is normally posted within
        /// milliseconds, so this only has to outlast a heavily loaded machine.
        /// </summary>
        private const int CANCEL_RESTART_LOOPS = 30;

        /// <summary>
        /// Whether to wait out a loader that cancelled itself because its document was
        /// superseded, rather than take that cancellation for the end of the load.
        ///
        /// <para>A background loader cancels its own work whenever the document it was handed is
        /// superseded - <see cref="RetentionTimeManager"/>'s test for it is nothing but a
        /// reference comparison against the container's current document. Nothing was cancelled
        /// in any meaningful sense: <see cref="BackgroundLoader"/> re-notifies on the way out and
        /// the loader starts again on the new document. But that hand-off posts a cancelled
        /// status, and <see cref="IsFinal"/> counts any cancellation as terminal, so the wait
        /// could end on a document still short of loaded. Under a parallel run, where a
        /// chromatogram load commits its new document just as a retention time alignment begins,
        /// that surfaced as an intermittent "Loader cancelled" - the alignment had been running
        /// for 0% of its work.</para>
        ///
        /// <para>So give the restart a bounded chance to post its own status. Anything that
        /// arrives - progress, completion, or the document simply becoming loaded - ends this and
        /// the wait proceeds normally. A cancellation that really is the end of the line still
        /// gets reported, just <see cref="CANCEL_RESTART_LOOPS"/> seconds later.</para>
        ///
        /// <para>OFF by default, because <see cref="CommandLine"/> waits on this same container
        /// and already has its own handling for an early return, and buying a test's stability
        /// with up to <see cref="CANCEL_RESTART_LOOPS"/> seconds added to a shipping import's
        /// failure path is not a trade worth making blind. The same early return is a real gap
        /// there - it can leave the command line writing a document whose alignments never
        /// finished - but that is a separate change, made deliberately and measured on its own.
        /// </para>
        ///
        /// <para>SETTABLE rather than overridden, because "every results test wants this" turned
        /// out to be false. Two callers depend on the fail-fast it removes:
        /// <c>AssertEx.ForceDocumentLoad</c>, whose own comment explains that its callers sit in a
        /// bare catch-and-retry where a longer wait silently deletes the fix it exists to apply;
        /// and CommandLineTest, which drives the shipping CommandLine path through a TEST
        /// container, so switching this on there would make the production gap named above
        /// permanently invisible to the suite that covers it. Both switch it back off.</para>
        /// </summary>
        public bool WaitForCancelRestart { get; set; }

        private bool IsSupersededCancel(ref int cancelLoops)
        {
            if (!WaitForCancelRestart || LastProgress == null || !LastProgress.IsCanceled || Document.IsLoaded)
            {
                // Deliberately NOT resetting cancelLoops. The budget is spent per WAIT, not per
                // cancellation episode, because LastProgress is one container-wide status written
                // by every registered loader - six of them here, nine under SkylineWindow. Resetting
                // whenever it reads non-cancelled would let an unrelated loader's progress buy a
                // fresh 30 seconds each time round, and WaitForComplete has no overall deadline: a
                // named "Loader cancelled" failure would become an open-ended hang, diagnosable
                // only as a harness timeout. Now the cap bounds the whole wait.
                return false;
            }
            if (cancelLoops == 0 && Program.UnitTest)
            {
                // Leading hash is a cue to SkylineTester to ignore these informational lines.
                // Worth saying out loud: without it a run that rides out a hand-off looks exactly
                // like a run where the race never happened, and the fix cannot be told from luck.
                Console.WriteLine(@"# Waiting out a cancelled loader that lost its document: {0}",
                    LastProgress.Message);
            }
            return ++cancelLoops <= CANCEL_RESTART_LOOPS;
        }

        private bool IsFinal(SrmDocument doc)
        {
            // The document is fully loaded: definitely final.
            if (doc.IsLoaded)
                return true;
            // Not loaded and the loader has not reached a final state yet: keep waiting.
            if (LastProgress == null || !LastProgress.IsFinal)
                return false;
            // A terminal error or cancellation is final regardless of load state.
            if (LastProgress.IsError || LastProgress.IsCanceled)
                return true;
            // Successful-looking final status but the document is not loaded. This happens
            // in two very different situations that must not be conflated:
            //  1. Multi-file loading posts a *final* status as each file finishes, but leaves
            //     the document at a checkpoint with more files still to import -- the loader
            //     re-triggers to build them (e.g. ThermoFormatsTest importing a second file
            //     into a document that already has a final cache). Returning true here would
            //     abandon the remaining files' import.
            //  2. The loader genuinely finished but left doc.IsLoaded == false (e.g.
            //     WatersCacheTest on net8 - pwiz-sharp Reader_Waters emits chromatograms that
            //     Skyline's ChromCacheBuilder finishes producing without flipping the doc into
            //     a loaded state). Returning false here would hang forever.
            // Distinguish them by whether any data file still needs importing into a cache:
            // in case 1 the pending file is not cached (keep waiting), while in case 2 every
            // file is already cached (final, so the test surface fails fast rather than hangs).
            var results = doc.Settings.MeasuredResults;
            return results == null ||
                   results.Chromatograms.All(chromSet => chromSet.MSDataFilePaths.All(results.IsCachedFile));
        }

        public virtual void ResetProgress()
        {
            LastProgress = null;
        }

        public IProgressStatus LastProgress { get; private set; }

        private void UpdateProgress(object sender, ProgressUpdateEventArgs e)
        {
            var status = e.Progress;
            if (ProgressMonitor != null)
                ProgressMonitor.UpdateProgress(status);

            // Unblock the waiting thread, if there was a cancel or error
            lock (CHANGE_EVENT_LOCK)
            {
                // Keep track of last progress, but do not overwrite an error, unless
                // this is a MultiProgressStatus, where useful information may be added
                // even after the first error.
                if (status is MultiProgressStatus)
                {
                    // But avoid overwriting a final progress with a non-final progress for the same operation
                    if (IsProgressIdChanging(status) || !LastProgress.IsFinal)
                        LastProgress = status;
                }
                else
                {
                    if (IsProgressIdChanging(status) || !LastProgress.IsError)
                        LastProgress = !status.IsComplete ? status : null;
                }

                if (status.IsCanceled || status.IsError)
                    Monitor.Pulse(CHANGE_EVENT_LOCK);
            }
        }

        private bool IsProgressIdChanging(IProgressStatus status)
        {
            return LastProgress == null || !ReferenceEquals(LastProgress.Id, status.Id);
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

        public bool IsClosing { get { return false; } }

        /// <summary>
        /// Tracking active background loaders for a container - helps in test harness teardown
        /// </summary>
        public IEnumerable<BackgroundLoader> BackgroundLoaders
        {
            get {  return _backgroundLoaders; }
        }
        
        public void AddBackgroundLoader(BackgroundLoader loader)
        {
            _backgroundLoaders.Add(loader);
        }

        public void RemoveBackgroundLoader(BackgroundLoader loader)
        {
            _backgroundLoaders.Remove(loader);
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

            ChromatogramManager = new ChromatogramManager(false);
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

            OptimizationDbManager = new OptimizationDbManager();
            OptimizationDbManager.Register(this);
            Register(OptimizationDbManager);
        }

        public ChromatogramManager ChromatogramManager { get; private set; }

        public LibraryManager LibraryManager { get; private set; }

        public RetentionTimeManager RetentionTimeManager { get; private set; }

        public IonMobilityLibraryManager IonMobilityManager { get; private set; }

        public IrtDbManager IrtDbManager { get; private set; }

        public OptimizationDbManager OptimizationDbManager { get; private set; }


        public override void ResetProgress()
        {
            base.ResetProgress();

            ChromatogramManager.ResetProgress(Document);
            LibraryManager.ResetProgress(Document);
            RetentionTimeManager.ResetProgress(Document);
            IonMobilityManager.ResetProgress(Document);
            IrtDbManager.ResetProgress(Document);
        }

        public virtual void Dispose()
        {
            ChromatogramManager.Dispose();

            // Release current document to ensure the streams are closed on it
            SetDocument(new SrmDocument(SrmSettingsList.GetDefault()), Document);
            foreach (var loader in BackgroundLoaders.ToList())
            {
                loader.Unregister(this);
                loader.ClearCache();
            }
        }
    }
}
