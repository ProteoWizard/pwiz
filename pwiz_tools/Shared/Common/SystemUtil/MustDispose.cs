/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

namespace pwiz.Common.SystemUtil
{
    public class MustDispose : IDisposable
    {
        private bool _disposed;
        public bool IsDisposed()
        {
            lock(this)
            {
                return _disposed;
            }
        }
        public virtual void Dispose()
        {
            lock(this)
            {
                _disposed = true;
            }
        }
        public void CheckDisposed()
        {
            if (IsDisposed())
            {
                ThrowObjectDisposed();
            }
        }

        protected void ThrowObjectDisposed()
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        protected void DisposeMember<T>(ref T member) where T : IDisposable
        {
            T localValue;
            lock(this)
            {
                localValue = member;
                member = default(T);
            }
            if (!ReferenceEquals(null, localValue))
            {
                localValue.Dispose();
            }
        }

        protected T DetachMember<T>(ref T member) where T : IDisposable
        {
            T localValue;
            lock(this)
            {
                localValue = member;
                member = default(T);
            }
            return localValue;
        }
        protected void AttachMember<T>(ref T member, T newValue) where T : IDisposable
        {
            lock(this)
            {
                if (!Equals(member, default(T)))
                {
                    throw new InvalidOperationException("Already attached"); // Not L10N
                }
                member = newValue;
            }
        }
        protected TResult DetachAndReturn<TMember, TResult>(ref TMember member, TMember expectedValue, TResult result) where TMember : IDisposable
        {
            if (!ReferenceEquals(expectedValue, DetachMember(ref member)))
            {
                ThrowObjectDisposed();
            }
            return result;
        }
    }
}
