using System;
using System.IO;

namespace pwiz.Common.SystemUtil
{
    public class ProgressStream : Stream
    {
        private Stream _input;
        private long _length;
        private long _position;

        public ProgressStream(Stream input) : this(input, input.Length)
        {
        }

        public ProgressStream(Stream input, long expectedSize)
        {
            _input = input;
            _length = expectedSize;
        }

        public override void Flush()
        {
            _input.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _input.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _input.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = _input.Read(buffer, offset, count);
            _position += n;
            UpdateProgress(_position);
            return n;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get { return _position; }
            set { throw new InvalidOperationException(); }
        }

        public IProgressMonitor ProgressMonitor { get; private set; }
        public IProgressStatus ProgressStatus { get; set; }

        public void SetProgressMonitor(IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            ProgressMonitor = progressMonitor;
            ProgressStatus = progressStatus;
        }

        private void UpdateProgress(long newPosition)
        {
            if (ProgressMonitor == null || ProgressStatus == null || Length == 0)
            {
                return;
            }

            int newPercentComplete = (int)Math.Min(99, 100 * newPosition / Length);
            if (newPercentComplete != ProgressStatus.PercentComplete)
            {
                ProgressMonitor.UpdateProgress(ProgressStatus = ProgressStatus.ChangePercentComplete(newPercentComplete));
            }
        }
    }
}