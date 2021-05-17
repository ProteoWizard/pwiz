using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharedBatch
{
    public class LongWaitOperation
    {

        private  bool _showLongWaitDlg;
        private readonly LongWaitDlg _longWaitDlg;
        private readonly Form _parentForm;
        public CancellationTokenSource CancelToken { get; private set; }

        public LongWaitOperation(Form parentForm, LongWaitDlg longWaitDlg)
        {
            _longWaitDlg = longWaitDlg;
            _parentForm = parentForm;
            CancelToken = _longWaitDlg.CancelToken;
        }

        public LongWaitOperation(CancellationTokenSource cancelToken = null)
        {
            _showLongWaitDlg = false;
            CancelToken = cancelToken ?? new CancellationTokenSource();
        }

        public bool Cancelled => CancelToken.IsCancellationRequested;
        public bool Completed { get; private set; }

        public void Start(bool willBeLong, LongOperation operation, Callback callback)
        {
            if (willBeLong && _parentForm != null)
            {
                _showLongWaitDlg = true;
                _longWaitDlg.Show(_parentForm);
            }
            new Task( () =>
            {
                StartAsync(operation, callback);
            }).Start();
        }

        private void StartAsync(LongOperation operation, Callback callback)
        {
            // method that takes in update progress arg
            operation(UpdateProgress);
            if (!Cancelled && _showLongWaitDlg) _longWaitDlg?.Finish();
            callback(!Cancelled);
            Completed = !Cancelled;
        }

        /*public async Task Start()
        {
            await Task.Run(() =>
            {
                _operation(UpdateProgress);
            }, _cancelToken.Token);
        }*/

        public void Cancel()
        {
            CancelToken.Cancel();
        }

        private void UpdateProgress(int currentPercent, int maxPercent)
        {
            if (_showLongWaitDlg) _longWaitDlg.UpdateProgress(currentPercent, maxPercent);
        }
    }
}
