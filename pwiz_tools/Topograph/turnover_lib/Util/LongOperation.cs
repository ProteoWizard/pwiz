using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NHibernate;

namespace pwiz.Topograph.Util
{
    public class LongOperationBroker
    {
        private bool _isCancellable = true;
        private bool _wasCancelled;
        private readonly EventWaitHandle _event = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Exception _exception;
        private String _statusMessage;

        public LongOperationBroker(ILongOperationJob job, ILongOperationUi ui)
        {
            UiDelayMilliseconds = 2000;
            Job = job;
            Ui = ui;
        }

        public LongOperationBroker(Action<LongOperationBroker> job, ILongOperationUi ui) : this(new DefaultJob(job), ui)
        {
        }

        public LongOperationBroker(Action<LongOperationBroker> job, ILongOperationUi ui, ISession session) : this(new DefaultJob(job, session), ui)
        {
            
        }

        public ILongOperationJob Job { get; private set; }
        public ILongOperationUi Ui { get; private set; }
        public bool LaunchJob()
        {
            new Action(RunJobBackground).BeginInvoke(null, null);
            _event.WaitOne(UiDelayMilliseconds);
            if (!IsComplete)
            {
                Ui.DisplayLongOperationUi(this);
            }
            _event.WaitOne();
            if (_exception != null)
            {
                throw new ApplicationException("An exception occurred running the job", _exception);
            }
            return !WasCancelled;
        }

        private void RunJobBackground()
        {
            try
            {
                Job.Run(this);
            }
            catch (Exception e)
            {
                if (!WasCancelled)
                {
                    _exception = e;
                    Console.Out.WriteLine(e);
                }
            }
            finally
            {
                lock(this)
                {
                    IsComplete = true;
                    _event.Set();
                    Ui.LongOperationEnded();
                }
            }
        }

        public int UiDelayMilliseconds
        {
            get; set;
        }
        
        public bool IsCancellable
        {
            get 
            { 
                lock(this)
                {
                    return _isCancellable;
                }
            }
            set
            {
                lock(this)
                {
                    _isCancellable = value;
                    Ui.UpdateLongOperationUi();
                }
            }
        }

        public bool WasCancelled
        {
            get
            {
                lock(this)
                {
                    return _wasCancelled;
                }
            }
        }

        public bool IsComplete
        {
            get; private set;
        }

        public String StatusMessage
        {
            get
            {
                lock(this)
                {
                    return _statusMessage;
                }
            }
        }

        public void UpdateStatusMessage(String message)
        {
            lock (this)
            {
                if (_wasCancelled)
                {
                    throw new JobCancelledException();
                }
                _statusMessage = message;
                Ui.UpdateLongOperationUi();
            }
        }
        public bool Cancel()
        {
            lock(this)
            {
                if (IsComplete || _wasCancelled)
                {
                    return true;
                }
                if (!IsCancellable)
                {
                    return false;
                }
                return _wasCancelled = Job.Cancel();
            }
        }

        public void SetIsCancelleable(bool isCancellable)
        {
            lock(this)
            {
                if (_wasCancelled)
                {
                    throw new JobCancelledException();
                }
                _isCancellable = isCancellable;
                Ui.UpdateLongOperationUi();
            }
        }

        public void WaitUntilFinished()
        {
            _event.WaitOne();
        }

        private class DefaultJob : ILongOperationJob
        {
            private Action<LongOperationBroker> _job;
            private ISession _session;
            public DefaultJob(Action<LongOperationBroker> job) : this(job, null)
            {
            }

            public DefaultJob(Action<LongOperationBroker> job, ISession session)
            {
                _job = job;
                _session = session;
            }

            public void Run(LongOperationBroker longOperationBroker)
            {
                _job.Invoke(longOperationBroker);
            }

            public bool Cancel()
            {
                if (_session != null)
                {
                    _session.CancelQuery();
                }
                return true;
            }
        }
    }

    public class JobCancelledException : ApplicationException
    {
        
    }

    public interface ILongOperationUi
    {
        void DisplayLongOperationUi(LongOperationBroker broker);
        void UpdateLongOperationUi();
        void LongOperationEnded();
    }

    public interface ILongOperationJob
    {
        void Run(LongOperationBroker longOperationBroker);
        bool Cancel();
    }
}
