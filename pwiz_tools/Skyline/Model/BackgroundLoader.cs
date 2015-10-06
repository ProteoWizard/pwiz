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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public abstract class BackgroundLoader
    {
        private IStreamManager _streamManager = FileStreamManager.Default;

        private readonly Dictionary<int, IDocumentContainer> _processing =
            new Dictionary<int, IDocumentContainer>();

        public event EventHandler<ProgressUpdateEventArgs> ProgressUpdateEvent;

        public IStreamManager StreamManager
        {
            get { return _streamManager; }
            set { _streamManager = value; }
        }

        public void Register(IDocumentContainer container)
        {
            container.Listen(OnDocumentChanged);
        }

        public void Unregister(IDocumentContainer container)
        {
            container.Unlisten(OnDocumentChanged);
        }

        protected void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            IDocumentContainer container = (IDocumentContainer)sender;
            SrmDocument document = container.Document;
            SrmDocument previous = e.DocumentPrevious;
            if (StateChanged(document, previous))
            {
                CloseRemovedStreams(document, previous);

                if (IsLoaded(document))
                    EndProcessing(document);
                else
                {
                    int docIndex = document.Id.GlobalIndex;
                    lock (_processing)
                    {
                        // Keep track of the documents being processed, to avoid running
                        // processing on the same document on multiple threads.
                        if (_processing.ContainsKey(docIndex))
                            return;
                        _processing.Add(docIndex, container);
                    }

                    Action<IDocumentContainer, SrmDocument> loader = OnLoadBackground;
                    loader.BeginInvoke(container, document, loader.EndInvoke, null);
                }
            }
        }

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
                    id.CloseStream();
            }
        }

        private void OnLoadBackground(IDocumentContainer container, SrmDocument document)
        {
            try
            {
                // Made on a new thread.
                LocalizationHelper.InitThread(GetType().Name + " thread"); // Not L10N
                SrmDocument docCurrent = container.Document;
                // If the document identity has changed, or the current document is loaded,
                // then end the processing.
                if (!document.EqualsId(docCurrent) || IsLoaded(docCurrent))
                {
                    EndProcessing(document);
                    return;
                }

                LoadBackground(container, document, docCurrent);

                // Force a document changed notification, since loading blocks them
                // from triggering new processing, but new processing may have accumulated
                OnDocumentChanged(container, new DocumentChangedEventArgs(docCurrent));
            }
            catch (Exception exception)
            {
                Program.ReportException(exception);
            }
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

        protected UpdateProgressResponse UpdateProgress(ProgressStatus status)
        {
            if (ProgressUpdateEvent != null)
            {
                var args = new ProgressUpdateEventArgs(status);
                ProgressUpdateEvent(this, args);
                return args.Response;
            }
            return UpdateProgressResponse.normal;
        }

        protected bool CompleteProcessing(IDocumentContainer container, SrmDocument docNew, SrmDocument docOriginal)
        {
            lock (_processing)
            {
                if (!container.SetDocument(docNew, docOriginal))
                    return false;

                EndProcessing(docOriginal);
            }
            return true;
        }

        protected void EndProcessing(SrmDocument document)
        {
            lock (_processing)
            {
                _processing.Remove(document.Id.GlobalIndex);
            }
        }

        protected class LoadMonitor : ILoadMonitor
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

            public IStreamManager StreamManager
            {
                get { return _manager.StreamManager; }
            }

            /// <summary>
            /// Cancels loading, if the <see cref="SrmDocument"/> for which it is
            /// being loaded is found not to contain the library.
            /// </summary>
            public bool IsCanceled
            {
                get
                {
                    return _manager.IsCanceled(_container, _tag);
                }
            }

            /// <summary>
            /// Updates progress reporting for this operation.
            /// </summary>
            /// <param name="status"></param>
            public UpdateProgressResponse UpdateProgress(ProgressStatus status)
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

        public UpdateProgressResponse UpdateProgress(ProgressStatus status)
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
