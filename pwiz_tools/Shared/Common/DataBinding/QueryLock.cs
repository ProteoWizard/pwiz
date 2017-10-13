/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

namespace pwiz.Common.DataBinding
{
    public class QueryLock
    {
        private readonly CancellationToken _rootCancellationToken;
        private readonly ReaderWriterLock _readerWriterLock = new ReaderWriterLock();
        private CancellationTokenSource _cancellationTokenSource;

        public QueryLock(CancellationToken rootCancellationToken)
        {
            _rootCancellationToken = rootCancellationToken;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_rootCancellationToken);
        }

        public CancellationToken CancellationToken
        {
            get
            {
                lock (this)
                {
                    return _cancellationTokenSource.Token;
                }
            }
        }

        public IDisposable GetReadLock()
        {
            CancellationToken.ThrowIfCancellationRequested();
            _readerWriterLock.AcquireReaderLock(int.MaxValue);
            return new DisposableLock(_readerWriterLock, false);
        }

        public IDisposable CancelAndGetWriteLock()
        {
            lock (this)
            {
                _cancellationTokenSource.Cancel();
            }
            _readerWriterLock.AcquireWriterLock(int.MaxValue);
            lock (this)
            {
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_rootCancellationToken);
            }
            return new DisposableLock(_readerWriterLock, true);
        }

        private class DisposableLock : IDisposable
        {
            private ReaderWriterLock _readerWriterLock;
            private readonly bool _writeLock;

            public DisposableLock(ReaderWriterLock readerWriterLock, bool writeLock)
            {
                _readerWriterLock = readerWriterLock;
                _writeLock = writeLock;
            }

            public void Dispose()
            {
                ReaderWriterLock readerWriterLock = Interlocked.Exchange(ref _readerWriterLock, null);
                if (null != readerWriterLock)
                {
                    if (_writeLock)
                    {
                        readerWriterLock.ReleaseWriterLock();
                    }
                    else
                    {
                        readerWriterLock.ReleaseReaderLock();
                    }
                }
            }
        }
    }
}
