using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;


namespace pwiz.Topograph.Util
{
    public class AutoLock : IDisposable
    {
        public AutoLock(ReaderWriterLockSlim readerWriterLock, bool isWriterLock)
        {
            ReaderWriterLock = readerWriterLock;
            IsWriterLock = isWriterLock;
            if (IsWriterLock)
            {
                readerWriterLock.EnterWriteLock();
            }
            else
            {
                readerWriterLock.EnterReadLock();
            }
        }

        public ReaderWriterLockSlim ReaderWriterLock { get; private set; }
        public bool IsWriterLock { get; private set; }
        public bool IsUpgraded { get; private set; }

        public virtual void Dispose()
        {
            if (IsWriterLock)
            {
                ReaderWriterLock.ExitWriteLock();
            }
            else
            {
                ReaderWriterLock.ExitReadLock();
            }
        }
    }
}
