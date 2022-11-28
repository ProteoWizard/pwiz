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
