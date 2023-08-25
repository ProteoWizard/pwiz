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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Provides a CancellationToken which gets cancelled when a specified function returns true.
    /// Starts up a thread which periodically calls that function to check if it should be cancelled.
    /// This class must be disposed so that the polling thread is destroyed.
    /// </summary>
    public class PollingCancellationToken : IDisposable
    {
        private const int POLLING_INTERVAL = 100;
        private Func<bool> _isCancelledFunc;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;
        public PollingCancellationToken(Func<bool> isCancelledFunc) : this (CancellationToken.None, isCancelledFunc)
        {
        }

        public PollingCancellationToken(CancellationToken token, Func<bool> isCancelledFunc)
        {
            _isCancelledFunc = isCancelledFunc;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            CommonActionUtil.RunAsync(CheckCancelledThreadProc);
        }

        public CancellationToken Token
        {
            get
            {
                return _cancellationTokenSource.Token;
            }
        }

        public void Dispose()
        {
            lock (this)
            {
                _cancellationTokenSource.Dispose();
                _isDisposed = true;
            }
        }

        private void CheckCancelledThreadProc()
        {
            while (true)
            {
                lock (this)
                {
                    if (_isDisposed)
                    {
                        return;
                    }

                    if (_isCancelledFunc())
                    {
                        _cancellationTokenSource.Cancel();
                        return;
                    }
                }
                Thread.Sleep(POLLING_INTERVAL);
            }
        }
    }
}
