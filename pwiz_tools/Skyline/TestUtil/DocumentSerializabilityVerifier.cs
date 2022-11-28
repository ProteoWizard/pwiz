/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Task which runs in the background and keeps calling <see cref="AssertEx.Serializable(pwiz.Skyline.Model.SrmDocument)"/>
    /// on <see cref="SkylineWindow.Document"/> to make sure the document in memory would always
    /// be able to round-trip to XML
    /// </summary>
    public class DocumentSerializabilityVerifier : IDisposable
    {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public void RunAsync()
        {
            ActionUtil.RunAsync(RunOnThisThread);
        }

        private void RunOnThisThread()
        {
            var cancellationToken = _cancellationTokenSource.Token;
            SrmDocument lastDocument = null;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                var document = Program.MainWindow?.Document;
                if (document == null || ReferenceEquals(document, lastDocument))
                {
                    // No work to do: Wait for 1 millisecond or until cancelled
                    cancellationToken.WaitHandle.WaitOne(1);
                    continue;
                }

                AssertEx.Serializable(document);
                lastDocument = document;
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
