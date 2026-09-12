/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Threading;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public abstract class BackgroundLoader
    {
        private IStreamManager _streamManager = FileStreamManager.Default;

        private int _activeThreadCount;
        private readonly Dictionary<int, IDocumentContainer> _processing =
            new Dictionary<int, IDocumentContainer>();

        protected bool IsMultiThreadAware { get; set; }

        public event EventHandler<ProgressUpdateEventArgs> ProgressUpdateEvent;

        public IStreamManager StreamManager
        {
            get { return _streamManager; }
            set { _streamManager = value; }
        }

        public void Register(IDocumentContainer container)
        {
            container.Listen(OnDocumentChanged);
            container.AddBackgroundLoader(this);  // Useful information for enforcing orderly test shutdown
        }

        public void Unregister(IDocumentContainer container)
        {
            container.Unlisten(OnDocumentChanged);
            container.RemoveBackgroundLoader(this);  // Useful information for enforcing orderly test shutdown
        }

        protected void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            IDocumentContainer container = (IDocumentContainer)sender;
            SrmDocument document = container.Document;
            SrmDocument previous = e.DocumentPrevious;
            if (IsStateChanged(document, previous))
            {
                CloseRemovedStreams(document, previous);

                if (IsLoaded(document))
                {
                    LoaderTrace(@"changed doc={0} prev={1} -> loaded, EndProcessing", document, previous);
                    EndProcessing(document);
                }
                else
                {
                    if (!IsMultiThreadAware)
                    {
                        int docIndex = document.Id.GlobalIndex;
                        lock (_processing)
                        {
                            // Keep track of the documents being processed, to avoid running
                            // processing on the same document on multiple threads.
                            if (_processing.ContainsKey(docIndex))
                            {
                                LoaderTrace(@"changed doc={0} prev={1} -> SKIPPED, already processing", document, previous);
                                return;
                            }
                            _processing.Add(docIndex, container);
                        }
                    }

                    LoaderTrace(@"changed doc={0} prev={1} -> starting load thread", document, previous);
                    var loadThread = new Thread(() => OnLoadBackground(container, document));
                    Interlocked.Increment(ref _activeThreadCount);
                    loadThread.Start();
                }
            }
            else
            {
                LoaderTrace(@"changed doc={0} prev={1} -> no state change, ignored", document, previous);
            }
        }

        #region Loader lifecycle trace - diagnostics for imports that finish and are never committed

        /// <summary>
        /// A ring of the most recent loader lifecycle events, formatted into a test failure
        /// message so an intermittent "import reached 100% but the document never became loaded"
        /// can say WHY. The thread dump taken at that point cannot: every loader thread has
        /// already exited, so the evidence of what the loader decided is gone before anyone
        /// looks. This records the decisions as they happen and keeps only the tail.
        ///
        /// <para>THE RECORDING COSTS THE PASSING CASE ALMOST NOTHING, which is what makes it
        /// safe to leave switched on. Outside a unit test it is a single static bool read.
        /// Inside one it stores a message REFERENCE (always a literal, so never allocated) and
        /// three ints into a preallocated slot - no formatting, no allocation, no lock, just an
        /// interlocked index. All the expense - composing the text - happens once, in
        /// <see cref="GetLoaderTrace"/>, and only when a test has already failed.</para>
        ///
        /// <para>That matters beyond tidiness: the defect this chases is a race, so
        /// instrumentation heavy enough to shift thread timing would hide the thing it was added
        /// to catch. Cheap is not an optimization here, it is a correctness requirement.</para>
        ///
        /// <para>Readers can see a torn slot if a writer is mid-update. That is accepted: the
        /// alternative is a lock on the hot path, and one garbled diagnostic line is a far
        /// smaller loss than a race that stops reproducing.</para>
        /// </summary>
        private struct LoaderTraceEntry
        {
            public long Ticks;
            public int ThreadId;
            public string Loader;
            public string Message;
            public int Arg0, Arg1, Arg2;
        }

        // Power of two, for the index mask below. Sized for how fast this fills, not for how much
        // history feels sufficient: EVERY registered loader writes on EVERY document change - six
        // under a results container, nine under SkylineWindow - so 512 slots would have held only
        // about 57 document changes, and a chromatogram import makes far more than that. The line
        // the trace exists to capture would have been evicted before the test timed out, with
        // nothing in the output to distinguish "the ring wrapped" from "nothing happened".
        private const int LOADER_TRACE_SIZE = 8192;
        private static readonly LoaderTraceEntry[] LOADER_TRACE = new LoaderTraceEntry[LOADER_TRACE_SIZE];
        private static int _loaderTraceIndex = -1;
        private string _loaderName;

        /// <summary>
        /// Drops everything recorded so far. Called at the start of each test, because the ring is
        /// static and a TestRunner process runs test after test: without this, a failure early in
        /// one test is handed a tail belonging to EARLIER tests and presents it, under a heading
        /// claiming to describe this failure, as evidence. Entries carry loader and thread but no
        /// test identity, so there would be no way to tell.
        /// </summary>
        public static void ClearLoaderTrace()
        {
            _loaderTraceIndex = -1;
            Array.Clear(LOADER_TRACE, 0, LOADER_TRACE.Length);
        }

        protected void LoaderTrace(string message, SrmDocument doc = null, SrmDocument doc2 = null, int arg2 = int.MinValue)
        {
            if (!Program.UnitTest)
                return;
            var i = Interlocked.Increment(ref _loaderTraceIndex) & (LOADER_TRACE_SIZE - 1);
            LOADER_TRACE[i] = new LoaderTraceEntry
            {
                Ticks = DateTime.UtcNow.Ticks,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                Loader = _loaderName ??= GetType().Name,
                Message = message,
                Arg0 = doc?.Id.GlobalIndex ?? -1,
                Arg1 = doc2?.Id.GlobalIndex ?? -1,
                Arg2 = arg2
            };
        }

        /// <summary>
        /// Formats the trace oldest-first. Called only from a failure path, so it can afford to
        /// be as slow and as thorough as it likes.
        /// </summary>
        public static string GetLoaderTrace()
        {
            var last = _loaderTraceIndex;
            if (last < 0)
                return @"(no loader activity recorded)";
            var lines = new List<string>();
            var first = Math.Max(0, last - LOADER_TRACE_SIZE + 1);
            for (long n = first; n <= last; n++)
            {
                var entry = LOADER_TRACE[n & (LOADER_TRACE_SIZE - 1)];
                if (entry.Message == null)
                    continue;   // Never written, or torn mid-write
                var text = entry.Message
                    .Replace(@"{0}", entry.Arg0 < 0 ? @"none" : entry.Arg0.ToString())
                    .Replace(@"{1}", entry.Arg1 < 0 ? @"none" : entry.Arg1.ToString())
                    .Replace(@"{2}", entry.Arg2 == int.MinValue ? @"none" : entry.Arg2.ToString());
                lines.Add(
                    $@"{new DateTime(entry.Ticks, DateTimeKind.Utc).ToLocalTime():HH:mm:ss.fff} [{entry.ThreadId,3}] {entry.Loader}: {text}");
            }
            return lines.Count == 0 ? @"(no loader activity recorded)" : string.Join(Environment.NewLine, lines);
        }

        #endregion

        private void CloseRemovedStreams(SrmDocument document, SrmDocument previous)
        {
            // Finish all cached streams from the previous document, which are no longer
            // in the current document.
            HashSet<int> set = new HashSet<int>();
            foreach (var id in GetOpenStreams(document))
                set.Add(id.GlobalIndex);
            foreach (var id in GetOpenStreams(previous))
            {
                if (!set.Contains(id.GlobalIndex))
                {
                    // DebugLog.Info(@"{0}. {1} - {2}", id.GlobalIndex, id.GetType(), id.IsOpen ? @"removed" : @"checked");
                    id.CloseStream();
                }
            }
        }

        // For use on container shutdown, clear anything cached to restore minimal memory footprint
        public abstract void ClearCache();

        private void OnLoadBackground(IDocumentContainer container, SrmDocument document)
        {
            try
            {
                // Made on a new thread.
                LocalizationHelper.InitThread(GetType().Name + @" thread");
                Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                SrmDocument docCurrent = container.Document;
                // If the document identity has changed, or the current document is loaded,
                // then end the processing.
                if (!document.EqualsId(docCurrent) || IsLoaded(docCurrent))
                {
                    LoaderTrace(@"load doc={0} -> ABANDONED before starting (current={1}, loaded={2})",
                        document, docCurrent, IsLoaded(docCurrent) ? 1 : 0);
                    EndProcessing(document);
                    return;
                }

                LoaderTrace(@"load doc={0} -> LoadBackground begin", document);
                LoadBackground(container, document, docCurrent);
                LoaderTrace(@"load doc={0} -> LoadBackground returned (container now={1})", document, container.Document);

                // Did the container change out its document while we were working?
                EndProcessingNotInContainer(container);

                if (!IsMultiThreadAware)
                {
                    // Force a document changed notification, since loading blocks them
                    // from triggering new processing, but new processing may have accumulated
                    if (!container.IsClosing)
                        OnDocumentChanged(container, new DocumentChangedEventArgs(docCurrent));
                }
                else
                {
                    // No forced re-notification here. If work accumulated while this loader ran,
                    // only a real document change will pick it up - which is the suspicion behind
                    // an import that reaches 100% and is never committed.
                    LoaderTrace(@"load doc={0} -> exiting WITHOUT re-notify (IsMultiThreadAware)", document);
                }
            }
            catch (Exception exception)
            {
                Program.ReportException(exception);
            }
            finally
            {
                Interlocked.Decrement(ref _activeThreadCount);
            }
        }

        public bool IsStateChanged(SrmDocument document, SrmDocument previous)
        {
            if (previous == null || !ReferenceEquals(document.Id, previous.Id))
            {
                return true;
            }

            return StateChanged(document, previous);
        }

        /// <summary>
        /// Quick check to determine whether a particular document change contains
        /// a change that might cause a difference in the loading state managed
        /// by this background loader.
        /// </summary>
        /// <param name="document">The current document</param>
        /// <param name="previous">The document as it was before this change</param>
        protected abstract bool StateChanged(SrmDocument document, SrmDocument previous);

        /// <summary>
        /// Indicates when a document needs this loader to perform background
        /// loading by returning a non null string.
        /// </summary>
        /// <param name="document">The document in question</param>
        /// <returns>Non-null explaining the document requirements of external data to be loaded</returns>
        protected abstract string IsNotLoadedExplained(SrmDocument document);

        /// <summary>
        /// Indicates when a document needs this loader to perform background
        /// loading.
        /// </summary>
        /// <param name="document">The document in question</param>
        /// <returns>True if the document requires external data to be loaded</returns>
        protected bool IsLoaded(SrmDocument document)
        {
            return IsNotLoadedExplained(document) == null;
        }


        /// <summary>
        /// Gets the set of streams open in the specified document for this background
        /// loader type.
        /// </summary>
        /// <param name="document">The document to inspect</param>
        /// <returns>The set of open streams</returns>
        protected abstract IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document);

        /// <summary>
        /// Indicates when an existing loading operation is no longer necessary
        /// for a particular <see cref="IDocumentContainer"/>, usually because
        /// the contained document has changed in a way that makes the external
        /// data unnecessary.
        /// </summary>
        /// <param name="container">The <see cref="IDocumentContainer"/> with
        ///     the <see cref="SrmDocument"/> to check</param>
        /// <param name="tag">An object identifying the running job</param>
        /// <returns>True if the load should be canceled</returns>
        protected abstract bool IsCanceled(IDocumentContainer container, object tag);

        /// <summary>
        /// Performs the core work of loading the external data into the
        /// document on a background thread.
        /// </summary>
        /// <param name="container">The <see cref="IDocumentContainer"/> to update with
        ///     a modified document</param>
        /// <param name="document">The initial document that triggered the load</param>
        /// <param name="docCurrent">The document at the start of background processing</param>
        /// <returns>True if the load succeeded, and the document was modified</returns>
        protected abstract bool LoadBackground(IDocumentContainer container,
            SrmDocument document, SrmDocument docCurrent);

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            if (ProgressUpdateEvent != null)
            {
                var args = new ProgressUpdateEventArgs(status);
                ProgressUpdateEvent(this, args);
                return args.Response;
            }
            return UpdateProgressResponse.normal;
        }

        private bool IsProcessing(SrmDocument document)
        {
            lock (_processing)
            {
                return _processing.ContainsKey(document.Id.GlobalIndex);
            }
        }

        public virtual bool AnyProcessing()
        {
            if (_activeThreadCount > 0)
                return true;

            lock (_processing)
            {
                return _processing.Count > 0;
            }
        }

        protected bool CompleteProcessing(IDocumentContainer container, SrmDocument docNew, SrmDocument docOriginal)
        {
            // Has docOriginal already been removed from the processing list?  If so, don't attempt an update.
            // Unless the brackground loader handles its own thread safety, in which case the processing list
            // is not used.
            if (IsMultiThreadAware || IsProcessing(docOriginal))
            {
                if (!container.SetDocument(docNew, docOriginal))
                {
                    // The commit lost a race with another document change. The caller is expected
                    // to loop and retry against the new document; if the trace shows this without
                    // a following retry, the completed work was dropped here.
                    LoaderTrace(@"commit doc={0}->{1} -> REJECTED, container moved to {2}",
                        docOriginal, docNew, container.Document.Id.GlobalIndex);
                    return false;
                }
                LoaderTrace(@"commit doc={0}->{1} -> accepted", docOriginal, docNew);
            }
            else
            {
                LoaderTrace(@"commit doc={0}->{1} -> SKIPPED, no longer processing", docOriginal, docNew);
            }

            EndProcessing(docOriginal);
            return true;
        }

        private void EndProcessingNotInContainer(IDocumentContainer container)
        {
            lock (_processing)
            {
                foreach (var idContainer in _processing.ToArray())
                {
                    var docNew = container.Document;
                    if (ReferenceEquals(idContainer.Value, container) && idContainer.Key != docNew.Id.GlobalIndex)
                        EndProcessing(idContainer.Key);
                }
            }
        }

        protected void EndProcessing(SrmDocument document)
        {
            EndProcessing(document.Id.GlobalIndex);
        }

        protected void EndProcessing(int documentId)
        {
            lock (_processing)
            {
                _processing.Remove(documentId);
            }
        }

        public virtual void ResetProgress(SrmDocument document)
        {            
        }

        public class LoadMonitor : ILoadMonitor
        {
            private readonly BackgroundLoader _manager;
            private readonly IDocumentContainer _container;
            private readonly object _tag;

            public LoadMonitor(BackgroundLoader manager, IDocumentContainer container, object tag)
            {
                _manager = manager;
                _container = container;
                _tag = tag;
            }

            protected LoadMonitor()
            {
            }

            public virtual IStreamManager StreamManager
            {
                get { return _manager.StreamManager; }
            }

            /// <summary>
            /// Cancels loading, if the <see cref="SrmDocument"/> for which it is
            /// being loaded is found not to contain the library.
            /// </summary>
            public virtual bool IsCanceled
            {
                get
                {
                    // Check for global cancelation of the progress monitor
                    var monitor = _container as IProgressMonitor;
                    if (monitor != null && monitor.IsCanceled)
                        return true;
                    // Check for cancellation of just this item
                    return IsCanceledItem(_tag);
                }
            }

            protected bool IsCanceledItem(object tag)
            {
                return _manager.IsCanceled(_container, tag);
            }

            /// <summary>
            /// Updates progress reporting for this operation.
            /// </summary>
            /// <param name="status"></param>
            public virtual UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                return _manager.UpdateProgress(status);
            }

            public bool HasUI { get; set; }
        }
    }

    /// <summary>
    /// Interface for client notification during a background load operation.
    /// </summary>
    public interface ILoadMonitor : IProgressMonitor
    {
        /// <summary>
        /// Gets the <see cref="StreamManager"/> associated with this loader,
        /// for performing operations against the file system.
        /// </summary>
        IStreamManager StreamManager { get; }
    }

    /// <summary>
    /// Default load monitor implementation for loading from files.
    /// </summary>
    public sealed class DefaultFileLoadMonitor : ILoadMonitor
    {
        private readonly IProgressMonitor _monitor;

        public DefaultFileLoadMonitor(IProgressMonitor monitor)
        {
            _monitor = monitor;
        }

        public bool IsCanceled
        {
            get { return _monitor.IsCanceled; }
        }

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            return _monitor.UpdateProgress(status);
        }

        public bool HasUI { get { return false; } }

        public IStreamManager StreamManager
        {
            get
            {
                return FileStreamManager.Default;
            }
        }
    }
}
