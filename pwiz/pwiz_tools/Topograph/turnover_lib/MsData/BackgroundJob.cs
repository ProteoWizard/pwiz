using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NHibernate;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.MsData
{
    public abstract class BackgroundJob
    {
        private ISession _session;
        private bool _isRunning;
        
        protected BackgroundJob(Workspace workspace)
        {
            Workspace = workspace;
        }
        public Workspace Workspace { get; private set; }
        public Thread BackgroundThread { get; private set; }
        public EventWaitHandle EventWaitHandle { get; private set; }
        protected ISession OpenSession()
        {
            if (_session != null && _session.IsOpen)
            {
                throw new InvalidOperationException("Session already open");
            }
            return _session = Workspace.OpenSession();
        }

        protected abstract void ThreadProc();

        public void Start()
        {
            
        }
    }
}
