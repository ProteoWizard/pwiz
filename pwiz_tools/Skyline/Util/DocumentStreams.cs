/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Remembers the streams that an operation is working with, and, when the operation is over,
    /// closes the ones which the container's current document does not have. An operation may open
    /// streams on a document which the container has replaced while the operation was running, or
    /// create streams for a document which never makes it into the container at all, and this is
    /// what closes those streams.
    /// </summary>
    public class DocumentStreams : IDisposable
    {
        /// <summary>
        /// The <see cref="DocumentStreams"/> which currently exist, which is what
        /// <see cref="EnsureTracked"/> looks in to decide whether anything is going to close a
        /// stream which is being opened. Null, the default, means none of this is tracked, and
        /// <see cref="EnsureTracked"/> says nothing. Initialize this to a new ConcurrentDictionary
        /// when a stream is not getting closed and it is necessary to find out which code opened
        /// it, and then run the test.
        /// </summary>
        private static readonly ConcurrentDictionary<ReferenceValue<DocumentStreams>, bool> AllDocumentStreams = null;

        private readonly IDocumentContainer _documentContainer;
        private List<IPooledStream> _additionalStreams;

        public DocumentStreams(IDocumentContainer documentContainer)
            : this(documentContainer, documentContainer.Document)
        {
        }

        public DocumentStreams(IDocumentContainer documentContainer, SrmDocument document)
        {
            _documentContainer = documentContainer;
            Document = document;
            AllDocumentStreams?.TryAdd(ReferenceValue.Of(this), true);
        }

        public SrmDocument Document { get; }

        public void AddStream(IPooledStream pooledStream)
        {
            _additionalStreams ??= new List<IPooledStream>();
            _additionalStreams.Add(pooledStream);
        }

        public void AddStreams(PeptideLibraries libraries)
        {
            foreach (var stream in libraries.Libraries.SelectMany(lib =>
                         lib?.ReadStreams ?? Array.Empty<IPooledStream>()))
            {
                AddStream(stream);
            }
        }

        public void AddStreams(MeasuredResults measuredResults)
        {
            foreach (var stream in measuredResults?.ReadStreams ?? Array.Empty<IPooledStream>())
            {
                AddStream(stream);
            }
        }

        private IEnumerable<IPooledStream> GetTrackedStreams()
        {
            return new[] { Document?.GetOpenStreams(), _additionalStreams }.SelectMany(streams =>
                streams ?? Array.Empty<IPooledStream>());
        }

        /// <summary>
        /// When set, every pooled stream event of the streams whose <see cref="object.ToString"/>
        /// matches is written to the console, whether or not the test it happens in goes on to
        /// fail. Null, the default, dumps nothing. To track down a stream which is not being
        /// closed, set this to something such as <c>new Regex(@"\.skyd")</c>, recompile, and run
        /// the test, and then compare the events of a run which leaks a stream against the events
        /// of a run which does not.
        /// </summary>
        public static Regex DumpStreamsRegex { get; set; }

        /// <summary>
        /// Complains when a stream is opened which neither an active document nor any
        /// <see cref="DocumentStreams"/> knows about, since there is nothing to close such a
        /// stream when it stops being needed.
        /// </summary>
        public static void EnsureTracked(Identity identity)
        {
            if (AllDocumentStreams == null)
                return;
            if (!(identity is IPooledStream pooledStream) || IsTracked(pooledStream))
                return;
            Console.Out.WriteLine(@"UNTRACKED STREAM OPENING: {0}{1}{2}", identity, Environment.NewLine,
                new System.Diagnostics.StackTrace(true));
        }

        private static bool IsTracked(IPooledStream pooledStream)
        {
            if (true == Program.MainWindow?.Document?.GetOpenStreams()
                    .Any(stream => ReferenceEquals(stream, pooledStream)))
            {
                return true;
            }
            return AllDocumentStreams.Keys.Any(docStreams =>
                docStreams.Value.GetTrackedStreams().Any(stream => ReferenceEquals(stream, pooledStream)));
        }

        public void Dispose()
        {
            try
            {
                var newDocument = _documentContainer?.Document;
                if (ReferenceEquals(newDocument, Document ?? newDocument) && _additionalStreams == null)
                    return;

                var newStreams = new HashSet<ReferenceValue<IPooledStream>>();
                if (newDocument != null)
                    newStreams.UnionWith(newDocument.GetOpenStreams().Select(ReferenceValue.Of));
                if (_additionalStreams != null)
                {
                    foreach (var stream in _additionalStreams)
                    {
                        if (newStreams.Add(ReferenceValue.Of(stream)))
                            stream.CloseStream();
                    }
                }
                if (!ReferenceEquals(newDocument, Document ?? newDocument))
                {
                    foreach (var stream in Document.GetOpenStreams())
                    {
                        if (newStreams.Add(ReferenceValue.Of(stream)))
                            stream.CloseStream();
                    }
                }
            }
            finally
            {
                AllDocumentStreams?.TryRemove(ReferenceValue.Of(this), out _);
            }
        }
    }
}
