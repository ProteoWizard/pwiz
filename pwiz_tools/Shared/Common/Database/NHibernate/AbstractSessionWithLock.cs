/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.Common.Properties;

namespace pwiz.Common.Database.NHibernate
{
    public abstract class AbstractSessionWithLock
    {
        private readonly ReaderWriterLock _lock;
        private readonly bool _writeLock;
        private CancellationTokenRegistration _cancellationTokenRegistration;

        protected AbstractSessionWithLock(ReaderWriterLock readerWriterLock, bool writeLock,
            CancellationToken cancellationToken, Action cancellationAction)
        {
            _lock = readerWriterLock;
            _writeLock = writeLock;
            if (_writeLock)
            {
                if (_lock.IsReaderLockHeld)
                {
                    throw new InvalidOperationException(
                        Resources.SessionWithLock_SessionWithLock_Cant_acquire_write_lock_while_holding_read_lock);
                }
                _lock.AcquireWriterLock(int.MaxValue);
            }
            else
            {
                _lock.AcquireReaderLock(int.MaxValue);
            }
            _cancellationTokenRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    cancellationAction();
                }
                catch (Exception)
                {
                    // Ignore
                }
            });
        }

        public virtual void Dispose()
        {
            if (_writeLock)
            {
                _lock.ReleaseWriterLock();
            }
            else
            {
                _lock.ReleaseReaderLock();
            }
            _cancellationTokenRegistration.Dispose();

        }

        protected void EnsureWriteLock()
        {
            if (!_writeLock)
            {
                throw new InvalidOperationException(Resources.SessionWithLock_EnsureWriteLock_Must_have_write_lock);
            }
        }
    }
}
