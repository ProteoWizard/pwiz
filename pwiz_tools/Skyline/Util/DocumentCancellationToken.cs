/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Provides a CancellationToken which becomes cancelled when the SrmDocument in a DocumentContainer changes
    /// in a way determined by a specified function.
    /// Listens for changes on the DocumentContainer, and calls that function every time the document changes.
    /// This class must be disposed properly so that the IDocumentContainer listener is removed.
    /// </summary>
    public class DocumentCancellationToken : IDisposable
    {
        private IDocumentContainer _documentContainer;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly Func<SrmDocument, bool> _checkCancelledFunc;

        public DocumentCancellationToken(CancellationToken parentCancellationToken, IDocumentContainer documentContainer,
            Func<SrmDocument, bool> checkCancelledFunc)
        {
            _documentContainer = documentContainer;
            _documentContainer.Listen(DocumentChangedEvent);
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentCancellationToken);
            _checkCancelledFunc = checkCancelledFunc;
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_documentContainer != null)
                {
                    _documentContainer.Unlisten(DocumentChangedEvent);
                    _documentContainer = null;
                }

                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }

        public CancellationToken Token
        {
            get
            {
                return _cancellationTokenSource?.Token ?? CancellationToken.None;
            }
        }

        private void DocumentChangedEvent(object sender, EventArgs args)
        {
            lock (this)
            {
                var document = _documentContainer?.Document;
                if (document == null || !_checkCancelledFunc(document))
                {
                    return;
                }
                _cancellationTokenSource?.Cancel();
            }
        }
    }
}
