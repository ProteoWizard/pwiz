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

namespace pwiz.Skyline.Util
{
    public class DocumentStreams : IDisposable
    {
        private IDocumentContainer _documentContainer;
        public static List<DocumentStreams> AllDocumentStreams = new List<DocumentStreams>();

        private List<IPooledStream> _additionalStreams;

        public DocumentStreams(IEnumerable<IPooledStream> streams) : this(null, null)
        {
            foreach (var stream in streams)
            {
                AddStream(stream);
            }
        }
        public DocumentStreams(IDocumentContainer documentContainer) : this(documentContainer, documentContainer.Document)
        {
        }

        public DocumentStreams(IDocumentContainer documentContainer, SrmDocument document)
        {
            _documentContainer = documentContainer;
            Document = document;
            lock (AllDocumentStreams)
            {
                AllDocumentStreams.Add(this);
            }
        }

        public SrmDocument Document { get; }

        public void AddStream(IPooledStream pooledStream)
        {
            _additionalStreams ??= new List<IPooledStream>();
            _additionalStreams.Add(pooledStream);
        }

        public IEnumerable<IPooledStream> GetTrackedStreams()
        {
            return new[] { Document?.GetOpenStreams(), _additionalStreams }.SelectMany(streams =>
                streams ?? Array.Empty<IPooledStream>());
        }

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
        }
    }
}
