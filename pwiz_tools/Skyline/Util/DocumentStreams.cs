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
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Util
{
    public class DocumentStreams : IDisposable
    {
#if DEBUG
        public static HashSet<ReferenceValue<DocumentStreams>> AllDocumentStreams = new HashSet<ReferenceValue<DocumentStreams>>();
#endif
        private IDocumentContainer _documentContainer;

        private List<IPooledStream> _additionalStreams;

        public DocumentStreams(IEnumerable<IPooledStream> streams) : this(null, null)
        {
            foreach (var stream in streams)
            {
                AddStream(stream);
            }
        }

        public DocumentStreams(IPooledStream stream) : this(new[] { stream })
        {
        }

        public DocumentStreams(IDocumentContainer documentContainer) : this(documentContainer, documentContainer.Document)
        {
        }

        public DocumentStreams(IDocumentContainer documentContainer, SrmDocument document)
        {
            _documentContainer = documentContainer;
            Document = document;
#if DEBUG
            lock (AllDocumentStreams)
            {
                AllDocumentStreams.Add(this);
            }
#endif
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

        public static void EnsureTracked(Identity identity)
        {
#if DEBUG
            if (!(identity is IPooledStream pooledStream))
            {
                Console.Out.WriteLine("Not a pooled stream");
                return;
            }

            if (!IsTracked(pooledStream))
            {
                Console.Out.WriteLine("Untracked stream opening");
            }
#endif
        }

#if DEBUG
        public static bool IsTracked(IPooledStream pooledStream)
        {
            if (true == Program.MainWindow?.Document?.GetOpenStreams().Any(stream => ReferenceEquals(stream, pooledStream)))
            {
                return true;
            }
            lock (AllDocumentStreams)
            {
                return AllDocumentStreams.Any(docStreams =>
                    docStreams.Value.GetTrackedStreams().Any(stream => ReferenceEquals(stream, pooledStream)));
            }
        }
        private IEnumerable<IPooledStream> GetTrackedStreams()
        {
            return new[] { Document?.GetOpenStreams(), _additionalStreams }.SelectMany(streams =>
                streams ?? Array.Empty<IPooledStream>());
        }
#endif
        public void Dispose()
        {
            var newDocument = _documentContainer?.Document;
            if (ReferenceEquals(newDocument, Document ?? newDocument) && _additionalStreams == null)
            {
                return;
            }

            var newStreams = new HashSet<ReferenceValue<IPooledStream>>();
            if (newDocument != null)
            {
                newStreams.UnionWith(newDocument.GetOpenStreams().Select(ReferenceValue.Of));
            }
            if (_additionalStreams != null)
            {
                foreach (var stream in _additionalStreams)
                {
                    if (newStreams.Add(ReferenceValue.Of(stream)))
                    {
                        stream.CloseStream();
                    }
                }
            }
            if (!ReferenceEquals(newDocument, Document ?? newDocument))
            {
                foreach (var stream in Document.GetOpenStreams())
                {
                    if (newStreams.Add(ReferenceValue.Of(stream)))
                    {
                        stream.CloseStream();
                    }
                }
            }
#if DEBUG
            AllDocumentStreams.Remove(this);
#endif
        }
    }
}
