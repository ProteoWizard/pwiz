using System.Threading;
using System.Threading.Tasks;

namespace SharedBatch
{
    public class LongWaitOperation
    {

        private  bool _showLongWaitDlg;
        private readonly LongWaitDlg _longWaitDlg;
        public CancellationTokenSource CancelToken { get; private set; }

        public LongWaitOperation(LongWaitDlg longWaitDlg)
        {
            _longWaitDlg = longWaitDlg;
            CancelToken = _longWaitDlg.CancelToken;
        }

        public LongWaitOperation(CancellationTokenSource cancelToken = null)
        {
            _showLongWaitDlg = false;
            CancelToken = cancelToken ?? new CancellationTokenSource();
        }

        public bool Cancelled => CancelToken.IsCancellationRequested;
        public bool Completed { get; private set; }

        public void Start(bool showLongWaitDlg, LongOperation operation, Callback callback)
        {
            _showLongWaitDlg = showLongWaitDlg;
            new Task(() =>
            {
                StartAsync(operation, callback);
            }).Start();
            if (_showLongWaitDlg && _longWaitDlg != null)
                _longWaitDlg.ShowDialog(_longWaitDlg.ParentForm);
        }

        private void StartAsync(LongOperation operation, Callback callback)
        {
            // method that takes in update progress arg
            operation(UpdateProgress, CancelToken.Token);
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
